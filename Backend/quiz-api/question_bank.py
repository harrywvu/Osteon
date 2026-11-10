"""Versioned question bank with a packaged legacy bank for old sessions."""
import csv
import hashlib
import json
import math
import os
import time
from uuid import UUID
from functools import lru_cache
from pathlib import Path


ASSETS = Path(__file__).resolve().parent / "assets"
CSV = ASSETS / "questions_datasets.csv"
MODEL = ASSETS / "random_forest_mastery_model.onnx"


@lru_cache(maxsize=1)
def packaged_version():
    digest = hashlib.sha256()
    for path in (CSV, MODEL):
        digest.update(path.read_bytes())
    return digest.hexdigest()[:16]


@lru_cache(maxsize=1)
def model_version():
    return hashlib.sha256(MODEL.read_bytes()).hexdigest()[:16]


@lru_cache(maxsize=1)
def packaged_rows():
    with CSV.open(newline="", encoding="utf-8-sig") as source:
        return tuple(csv.DictReader(source))


@lru_cache(maxsize=1)
def table():
    import boto3
    from botocore.config import Config
    return boto3.resource("dynamodb", config=Config(
        connect_timeout=2, read_timeout=3, retries={"total_max_attempts": 2}
    )).Table(os.environ["QUESTION_BANK_TABLE_NAME"])


def active_version():
    if not os.environ.get("QUESTION_BANK_TABLE_NAME"):
        return packaged_version()
    record = table().get_item(Key={"pk": "ACTIVE", "sk": "META"}, ConsistentRead=True).get("Item")
    if not record:
        raise RuntimeError("active question bank is missing")
    return record["version"]


@lru_cache(maxsize=16)
def rows(version):
    if not isinstance(version, str) or not version or len(version) > 100:
        raise ValueError("invalid bank version")
    if not os.environ.get("QUESTION_BANK_TABLE_NAME"):
        if version != packaged_version():
            raise ValueError("unknown question bank version")
        return packaged_rows()
    from boto3.dynamodb.conditions import Key
    values = []
    kwargs = {"KeyConditionExpression": Key("pk").eq("VERSION#" + version), "ConsistentRead": True}
    while True:
        page = table().query(**kwargs)
        values.extend(page["Items"])
        if "LastEvaluatedKey" not in page:
            break
        kwargs["ExclusiveStartKey"] = page["LastEvaluatedKey"]
    manifest = next((item for item in values if item["sk"] == "META"), None)
    questions = [(int(item["order"]), json.loads(item["body"])) for item in values
                 if item["sk"].startswith("QUESTION#")]
    if not manifest or len(questions) != int(manifest["count"]) or len({q[1]["question_id"] for q in questions}) != len(questions):
        raise RuntimeError("question bank version is incomplete")
    return tuple(row for _, row in sorted(questions))


FIELDS = ("question_id", "bone_group", "bone_name", "difficulty_level",
          "difficulty_score", "question_text", "option_a", "option_b",
          "option_c", "option_d", "correct_option")


def validate(candidate, base):
    if not isinstance(candidate, list) or len(candidate) != len(base) or len(candidate) > 100:
        raise ValueError("submit the complete existing bank")
    ids = [row.get("question_id") for row in candidate if isinstance(row, dict)]
    if (len(ids) != len(candidate) or any(not isinstance(value, str) for value in ids)
            or len(set(ids)) != len(ids) or ids != [row["question_id"] for row in base]):
        raise ValueError("question IDs and order must remain unchanged")
    result = []
    for row in candidate:
        if set(row) != set(FIELDS):
            raise ValueError("invalid question fields")
        clean = {}
        for field in FIELDS:
            value = row[field]
            if field == "difficulty_score":
                try:
                    number = float(value)
                except (TypeError, ValueError):
                    raise ValueError("invalid difficulty score") from None
                if not math.isfinite(number) or not 0 <= number <= 1:
                    raise ValueError("difficulty score must be between 0 and 1")
                clean[field] = str(number)
                continue
            if not isinstance(value, str) or not value.strip():
                raise ValueError(field + " is required")
            clean[field] = value.strip()
        if clean["bone_group"] not in ("Axial", "Appendicular") or clean["difficulty_level"] not in ("Easy", "Medium", "Hard"):
            raise ValueError("unsupported group or difficulty")
        if clean["correct_option"] not in "ABCD" or len(clean["correct_option"]) != 1:
            raise ValueError("correct option must be A, B, C, or D")
        if len(clean["question_text"]) > 180 or len(clean["bone_name"]) > 80 or any(
                len(clean["option_" + letter]) > 70 for letter in "abcd"):
            raise ValueError("question text is too long for the quiz panel")
        result.append(clean)
    return result


def publish(base_version, candidate, request_id, educator):
    """Stage immutable rows, then atomically switch the pointer and mark the request."""
    try:
        version = str(UUID(request_id))
    except (TypeError, ValueError, AttributeError):
        raise ValueError("request_id must be a UUID") from None
    if not isinstance(educator, str) or not educator:
        raise ValueError("educator identity is required")
    base = rows(base_version)
    bank = validate(candidate, base)
    body = json.dumps(bank, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    digest = hashlib.sha256((base_version + body).encode()).hexdigest()
    db = table()
    marker = db.get_item(Key={"pk": "REQUEST#" + version, "sk": "META"}, ConsistentRead=True).get("Item")
    if marker:
        if marker["digest"] != digest or marker["educator"] != educator:
            raise ValueError("request_id was used for different content")
        if marker.get("status") == "published":
            return version
    else:
        try:
            db.put_item(Item={"pk": "REQUEST#" + version, "sk": "META", "digest": digest,
                              "educator": educator, "status": "staging"},
                        ConditionExpression="attribute_not_exists(pk)")
        except db.meta.client.exceptions.ConditionalCheckFailedException:
            return publish(base_version, candidate, request_id, educator)
    manifest_key = {"pk": "VERSION#" + version, "sk": "META"}
    manifest = db.get_item(Key=manifest_key, ConsistentRead=True).get("Item")
    if manifest and manifest["digest"] != digest:
        raise ValueError("request_id was used for different content")
    if not manifest:
        # A retry may safely finish an interrupted staging write with the same request body.
        with db.batch_writer() as batch:
            for order, row in enumerate(bank):
                batch.put_item(Item={"pk": "VERSION#" + version,
                                     "sk": "QUESTION#" + row["question_id"], "order": order,
                                     "body": json.dumps(row, ensure_ascii=False)})
        try:
            db.put_item(Item={**manifest_key, "count": len(bank), "digest": digest,
                              "base_version": base_version, "educator": educator,
                              "published_at": int(time.time())},
                        ConditionExpression="attribute_not_exists(pk)")
        except db.meta.client.exceptions.ConditionalCheckFailedException:
            manifest = db.get_item(Key=manifest_key, ConsistentRead=True).get("Item")
            if not manifest or manifest.get("digest") != digest:
                raise ValueError("request_id was used for different content") from None
    try:
        db.meta.client.transact_write_items(TransactItems=[
            {"Update": {"TableName": db.name, "Key": {"pk": "ACTIVE", "sk": "META"},
                        "UpdateExpression": "SET #v = :new",
                        "ConditionExpression": "#v = :base",
                        "ExpressionAttributeNames": {"#v": "version"},
                        "ExpressionAttributeValues": {":new": version, ":base": base_version}}},
            {"Update": {"TableName": db.name, "Key": {"pk": "REQUEST#" + version, "sk": "META"},
                        "UpdateExpression": "SET #s = :published",
                        "ConditionExpression": "#s = :staging AND digest = :digest",
                        "ExpressionAttributeNames": {"#s": "status"},
                        "ExpressionAttributeValues": {":published": "published", ":staging": "staging", ":digest": digest}}},
        ])
    except db.meta.client.exceptions.TransactionCanceledException:
        marker = db.get_item(Key={"pk": "REQUEST#" + version, "sk": "META"}, ConsistentRead=True).get("Item")
        if marker and marker.get("status") == "published" and marker["digest"] == digest and marker["educator"] == educator:
            return version
        raise PublishConflict("the active bank changed; reload before publishing") from None
    rows.cache_clear()
    return version


class PublishConflict(Exception):
    pass
