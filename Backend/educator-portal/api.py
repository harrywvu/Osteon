"""Educator-only HTTP API. API Gateway verifies the Cognito JWT first."""
import json
import math
import os
import re
from functools import lru_cache

import boto3
from boto3.dynamodb.conditions import Attr

import question_bank
from program_catalog import catalog


DETAIL = re.compile(r"/v1/learners/([a-f0-9]{32})\Z")
PROFILE_FIELDS = ("learner_id", "profile_label", "first_name", "middle_name",
                  "last_name", "program_id", "year_level", "status")


@lru_cache(maxsize=1)
def tables():
    resource = boto3.resource("dynamodb")
    return resource.Table(os.environ["ACCOUNTS_TABLE_NAME"]), resource.Table(os.environ["QUIZ_TABLE_NAME"])


def response(status, data):
    return {"statusCode": status, "headers": {"content-type": "application/json", "cache-control": "no-store"},
            "body": json.dumps(data, allow_nan=False)}


def identity(event):
    claims = event.get("requestContext", {}).get("authorizer", {}).get("jwt", {}).get("claims", {})
    if claims.get("token_use") != "access" or claims.get("client_id") != os.environ.get("EDUCATOR_CLIENT_ID"):
        return None
    # shortcut: every confirmed user in this dedicated pool has educator access; add a role gate when roles are needed.
    return claims.get("sub")


def request_body(event):
    raw = event.get("body")
    if event.get("isBase64Encoded") or not isinstance(raw, str) or len(raw.encode()) > 100000:
        raise ValueError("invalid request body")
    data = json.loads(raw)
    if not isinstance(data, dict):
        raise ValueError("request body must be an object")
    return data


def profile_public(item):
    return {field: int(item[field]) if field == "year_level" and field in item else item.get(field)
            for field in PROFILE_FIELDS}


def progress_public(state):
    if not state:
        return None
    return {"mastery_estimate": float(state.get("mastery", 0.5)),
            "answered_count": len(state.get("asked_ids", [])),
            "completed": bool(state.get("completed")),
            "current_question_id": state.get("current_question_id")}


def recent_analysis(history):
    """Summarize only the retained answer window, never the learner's lifetime."""
    graded = [entry for entry in history if type(entry.get("correct")) is bool]
    correct = sum(entry["correct"] for entry in graded)

    def breakdown(field, labels):
        result = []
        for label in labels:
            items = [entry for entry in graded if str(entry.get(field, "")).casefold() == label.casefold()]
            if items:
                result.append({"label": label, "answered": len(items),
                               "correct": sum(entry["correct"] for entry in items)})
        return result

    estimates = []
    if history:
        first = history[0].get("previous_mastery")
        changes = [entry.get("updated_mastery") for entry in history]
        values = [first, *changes]
        if all(type(value) in (int, float) and math.isfinite(value) and 0 <= value <= 1 for value in values):
            estimates = [float(value) for value in values]
    return {"retained_count": len(history), "graded_count": len(graded),
            "correct_count": correct,
            "by_group": breakdown("bone_group", ("Axial", "Appendicular")),
            "by_difficulty": breakdown("difficulty", ("Easy", "Medium", "Hard")),
            "mastery_estimates": estimates}


def learners(query):
    accounts, quiz = tables()
    term = (query.get("q") or "").strip().casefold()
    if len(term) > 80:
        raise ValueError("search is too long")
    cursor = query.get("cursor") or "0"
    if not isinstance(cursor, str) or not cursor.isascii() or not cursor.isdigit() or len(cursor) > 7:
        raise ValueError("invalid cursor")
    offset = int(cursor)
    profiles = []
    start = None
    # shortcut: projected full scan fits the initial small cohort; add a name index when it grows.
    while True:
        args = {"FilterExpression": Attr("pk").begins_with("PROFILE#"),
                "ProjectionExpression": "pk, learner_id, profile_label, first_name, middle_name, last_name, program_id, year_level, #s",
                "ExpressionAttributeNames": {"#s": "status"}, "Limit": 50}
        if start:
            args["ExclusiveStartKey"] = start
        page = accounts.scan(**args)
        for item in page.get("Items", []):
            name = " ".join(str(item.get(field, "")) for field in ("first_name", "middle_name", "last_name"))
            if not term or term in name.casefold():
                profiles.append(item)
        start = page.get("LastEvaluatedKey")
        if not start:
            break
    profiles.sort(key=lambda item: (" ".join(str(item.get(field, "")) for field in
                       ("first_name", "middle_name", "last_name")).casefold(), item["learner_id"]))
    found = []
    for item in profiles[offset:offset + 20]:
        state = quiz.get_item(Key={"learner_id": item["learner_id"]},
                              ProjectionExpression="payload").get("Item")
        found.append({"profile": profile_public(item),
                      "progress": progress_public(json.loads(state["payload"]) if state else None)})
    return {"learners": found, "next_cursor": str(offset + 20) if offset + 20 < len(profiles) else None}


def learner_detail(learner_id):
    accounts, quiz = tables()
    item = accounts.get_item(Key={"pk": "PROFILE#" + learner_id},
                             ProjectionExpression="learner_id, profile_label, first_name, middle_name, last_name, program_id, year_level, #s",
                             ExpressionAttributeNames={"#s": "status"}).get("Item")
    if not item:
        return None
    record = quiz.get_item(Key={"learner_id": learner_id}, ProjectionExpression="payload").get("Item")
    state = json.loads(record["payload"]) if record else None
    history = []
    retained = (state or {}).get("history", [])[-10:]
    for entry in retained:
        answer = {field: entry.get(field) for field in
                  ("question_id", "choice", "correct", "accepted_at", "bank_version",
                   "bone_group", "difficulty", "previous_mastery", "updated_mastery")}
        version = answer["bank_version"]
        if version:
            try:
                question = next((row for row in question_bank.rows(version) if row["question_id"] == answer["question_id"]), None)
            except (ValueError, RuntimeError):
                question = None
            if question:
                answer["question_text"] = question["question_text"]
                answer["bone_name"] = question["bone_name"]
                answer["bone_group"] = question["bone_group"]
                answer["difficulty"] = question["difficulty_level"]
                answer["chosen_text"] = question.get("option_" + str(answer["choice"]).lower())
                answer["correct_option"] = question["correct_option"]
                answer["correct_text"] = question["option_" + question["correct_option"].lower()]
        history.append(answer)
    return {"profile": profile_public(item), "progress": progress_public(state),
            "analysis": recent_analysis(retained), "recent_answers": history}


def lambda_handler(event, context):
    educator = identity(event)
    if not educator:
        return response(403, {"error": "educator_access_required"})
    method = event.get("requestContext", {}).get("http", {}).get("method")
    path = event.get("rawPath")
    try:
        if (method, path) == ("GET", "/v1/questions"):
            version = question_bank.active_version()
            return response(200, {"version": version, "questions": question_bank.rows(version)})
        if (method, path) == ("GET", "/v1/programs"):
            return response(200, {"programs": catalog()})
        if (method, path) == ("POST", "/v1/questions/publish"):
            data = request_body(event)
            version = question_bank.publish(data.get("base_version"), data.get("questions"),
                                            data.get("request_id"), educator)
            return response(200, {"version": version})
        if (method, path) == ("GET", "/v1/learners"):
            return response(200, learners(event.get("queryStringParameters") or {}))
        match = DETAIL.fullmatch(path or "") if method == "GET" else None
        if match:
            detail = learner_detail(match.group(1))
            return response(200, detail) if detail else response(404, {"error": "learner_not_found"})
        return response(404, {"error": "route_not_found"})
    except question_bank.PublishConflict:
        return response(409, {"error": "bank_changed"})
    except (ValueError, KeyError, json.JSONDecodeError) as error:
        return response(400, {"error": "invalid_request", "message": str(error)})
