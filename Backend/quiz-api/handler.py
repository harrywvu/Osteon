"""AWS Lambda entry point for API Gateway HTTP API payload format 2.0."""

import json
import hmac
import os
import re
from functools import lru_cache

from dynamodb_store import DynamoQuizStore
from quiz_service import QuizService, SessionConflict, SessionNotFound


ANSWER_ROUTE = re.compile(r"^/v1/sessions/([^/]+)/answers$")


@lru_cache(maxsize=1)
def _service():
    return QuizService(DynamoQuizStore(os.environ["QUIZ_TABLE_NAME"]))


def _response(status, body):
    return {
        "statusCode": status,
        "headers": {
            "content-type": "application/json",
            "cache-control": "no-store",
        },
        "body": json.dumps(body, allow_nan=False),
    }


def _body(event):
    if event.get("isBase64Encoded"):
        raise ValueError("JSON request body is required")
    raw = event.get("body")
    if raw is None or raw == "":
        return {}
    if not isinstance(raw, str) or len(raw) > 8192:
        raise ValueError("request body is too large or invalid")
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as error:
        raise ValueError("request body must be valid JSON") from error
    if not isinstance(data, dict):
        raise ValueError("request body must be a JSON object")
    return data


def lambda_handler(event, context):
    """Use verified JWT identity, or the explicitly configured one-learner demo key."""
    claims = (
        event.get("requestContext", {})
        .get("authorizer", {})
        .get("jwt", {})
        .get("claims", {})
    )
    learner_id = claims.get("sub") if isinstance(claims, dict) else None
    if not learner_id:
        demo_key = os.environ.get("QUIZ_DEMO_TOKEN", "")
        headers = {key.lower(): value for key, value in event.get("headers", {}).items()}
        authorization = headers.get("authorization", "")
        if demo_key and hmac.compare_digest(authorization, "Bearer " + demo_key):
            learner_id = "osteon-demo-learner"
    if not isinstance(learner_id, str) or not learner_id:
        return _response(401, {"error": "authentication_required"})

    method = event.get("requestContext", {}).get("http", {}).get("method")
    path = event.get("rawPath", "")
    try:
        if method == "POST" and path == "/v1/sessions":
            return _response(200, _service().start(learner_id))
        if method == "GET" and path == "/v1/sessions/current":
            return _response(200, _service().current(learner_id))
        match = ANSWER_ROUTE.fullmatch(path) if method == "POST" else None
        if match:
            data = _body(event)
            for field in ("question_id", "choice", "attempt_id"):
                if not isinstance(data.get(field), str) or not data[field]:
                    raise ValueError(f"{field} is required")
            result = _service().answer(
                learner_id,
                match.group(1),
                data["question_id"],
                data["choice"],
                data["attempt_id"],
            )
            return _response(200, result)
        return _response(404, {"error": "route_not_found"})
    except ValueError as error:
        return _response(400, {"error": "invalid_request", "message": str(error)})
    except SessionNotFound:
        return _response(404, {"error": "session_not_found"})
    except SessionConflict as error:
        return _response(409, {"error": "session_conflict", "message": str(error)})
