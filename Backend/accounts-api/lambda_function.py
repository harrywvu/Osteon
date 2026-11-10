"""Anonymous accounts: API Gateway HTTP API v2, Python 3.12, DynamoDB.

No credentials, request bodies, access codes, or creation request IDs are logged.
"""
import base64
import hashlib
import hmac
import ipaddress
import json
import logging
import os
import re
import secrets
import time
import uuid
from functools import lru_cache

import boto3
from boto3.dynamodb.types import TypeSerializer
from botocore.config import Config
from botocore.exceptions import ClientError
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from program_catalog import BY_ID, catalog

LOG = logging.getLogger(__name__)
TOKEN = re.compile(r"[A-Za-z0-9_-]{43}\Z")
CODE = re.compile(r"[0-9]{6}\Z")


class Failure(Exception):
    def __init__(self, status, error, retry=0):
        self.status, self.error, self.retry = status, error, retry


class Store:
    def __init__(self, table_name, resource):
        self.table = resource.Table(table_name)
        self.serializer = TypeSerializer()
        self.client = boto3.client("dynamodb", region_name=self.table.meta.client.meta.region_name,
                                   config=self.table.meta.client.meta.config)

    def get(self, key):
        return self.table.get_item(Key={"pk": key}, ConsistentRead=True).get("Item")

    def transaction(self, actions):
        # Low-level transactions require DynamoDB AttributeValue encoding.
        encoded = []
        for kind, operation in actions:
            operation = dict(operation, TableName=self.table.name)
            for field in ("Item", "Key", "ExpressionAttributeValues"):
                if field in operation:
                    operation[field] = {k: self.serializer.serialize(v) for k, v in operation[field].items()}
            encoded.append({kind: operation})
        # resource.meta.client applies its own serializer. Use an independent client.
        self.client.transact_write_items(TransactItems=encoded)

    def rate(self, key, limit, now, window):
        bucket = now // window * window
        try:
            self.table.update_item(
                Key={"pk": f"RATE#{key}#{bucket}"},
                UpdateExpression="SET expires_at = :expiry ADD #n :one",
                ConditionExpression="attribute_not_exists(#n) OR #n < :limit",
                ExpressionAttributeNames={"#n": "count"},
                ExpressionAttributeValues={":expiry": bucket + window + 60, ":one": 1, ":limit": limit},
            )
        except ClientError as error:
            if error.response["Error"]["Code"] == "ConditionalCheckFailedException":
                raise Failure(429, "rate_limited", bucket + window - now) from None
            raise


class Accounts:
    def __init__(self, store, keys, clock=time.time, code_generator=None):
        self.store, self.clock = store, clock
        self.lookup_key = bytes.fromhex(keys["code_lookup_key"])
        encryption_key = bytes.fromhex(keys["replay_encryption_key"])
        if len(self.lookup_key) != 32 or len(encryption_key) != 32:
            raise ValueError("Account protection keys must each contain 32 bytes")
        self.cipher = AESGCM(encryption_key)
        self.code_generator = code_generator or (lambda: f"{secrets.randbelow(1000000):06d}")

    def digest(self, kind, value):
        return hmac.new(self.lookup_key, (kind + ":" + value).encode(), hashlib.sha256).hexdigest()

    def limit(self, action, source):
        now = int(self.clock())
        if action == "create":
            self.store.rate("create-global", 100, now, 3600)
            self.store.rate("create-" + self.digest("source", source), 5, now, 60)
        else:
            self.store.rate("login-global", 100, now, 60)
            self.store.rate("login-" + self.digest("source", source), 10, now, 60)

    @staticmethod
    def session_key(token):
        if not isinstance(token, str) or not TOKEN.fullmatch(token):
            raise Failure(401, "authentication_required")
        return "SESSION#" + hashlib.sha256(token.encode()).hexdigest()

    def session(self, profile):
        now = int(self.clock())
        token = secrets.token_urlsafe(32)
        public = {**self.public_profile(profile), "access_token": token, "expires_in": 3600}
        item = {"pk": self.session_key(token), "learner_id": profile["learner_id"], "expires_at": now + 3600}
        return public, item

    def profile(self, learner_id):
        profile = self.store.get("PROFILE#" + learner_id)
        if not profile or profile.get("status") != "active":
            raise Failure(401, "authentication_required")
        return profile

    @staticmethod
    def public_profile(profile):
        fields = ("learner_id", "profile_label", "first_name", "middle_name",
                  "last_name", "program_id", "year_level")
        result = {field: profile.get(field) for field in fields}
        if result["year_level"] is not None:
            result["year_level"] = int(result["year_level"])
        result["profile_complete"] = all(profile.get(field) for field in
                                         ("first_name", "last_name", "program_id", "year_level"))
        return result

    @staticmethod
    def validate_details(details):
        if not isinstance(details, dict) or set(details) != {"first_name", "middle_name", "last_name", "program_id", "year_level"}:
            raise Failure(400, "invalid_profile")
        clean = {}
        for field in ("first_name", "middle_name", "last_name"):
            value = details[field]
            if not isinstance(value, str) or len(value.strip()) > 80 or any(ord(c) < 32 for c in value):
                raise Failure(400, "invalid_profile")
            clean[field] = value.strip()
        if not clean["first_name"] or not clean["last_name"]:
            raise Failure(400, "invalid_profile")
        program = details["program_id"]
        year = details["year_level"]
        if not isinstance(program, str) or program not in BY_ID or type(year) is not int or year not in BY_ID[program]["year_options"]:
            raise Failure(400, "invalid_profile")
        clean.update(program_id=program, year_level=year)
        return clean

    def replay(self, key):
        record = self.store.get(key)
        if not record or record["expires_at"] <= int(self.clock()):
            return None
        plaintext = self.cipher.decrypt(base64.b64decode(record["nonce"]),
                                        base64.b64decode(record["ciphertext"]), key.encode())
        result = json.loads(plaintext)
        self.profile(result["learner_id"])
        session = self.store.get(self.session_key(result["access_token"]))
        result["expires_in"] = max(0, int(session["expires_at"]) - int(self.clock())) if session else 0
        return result

    def create(self, request_id, source, details=None):
        try:
            if not isinstance(request_id, str) or len(request_id) > 36:
                raise ValueError()
            request_id = str(uuid.UUID(request_id))
        except (ValueError, AttributeError):
            raise Failure(400, "invalid_request") from None
        self.limit("create", source)
        key = "CREATE#" + self.digest("request", request_id)
        existing = self.replay(key)
        if existing:
            return existing
        for _ in range(8):
            now = int(self.clock())
            code = self.code_generator()
            if not CODE.fullmatch(code):
                raise ValueError("Invalid code generator")
            learner_id = uuid.uuid4().hex
            profile = {"pk": "PROFILE#" + learner_id, "learner_id": learner_id,
                       "profile_label": "Learner " + learner_id[:6], "status": "active", "created_at": now}
            if details is not None:
                profile.update(self.validate_details(details))
            public, session = self.session(profile)
            public["access_code"] = code
            nonce = secrets.token_bytes(12)
            record = {"pk": key, "expires_at": now + 86400,
                      "nonce": base64.b64encode(nonce).decode(),
                      "ciphertext": base64.b64encode(self.cipher.encrypt(nonce, json.dumps(public).encode(), key.encode())).decode()}
            actions = [("Put", {"Item": item, "ConditionExpression": "attribute_not_exists(pk)"})
                       for item in (profile, {"pk": "CODE#" + self.digest("code", code), "learner_id": learner_id}, session)]
            actions.append(("Put", {"Item": record,
                                    "ConditionExpression": "attribute_not_exists(pk) OR expires_at <= :now",
                                    "ExpressionAttributeValues": {":now": now}}))
            try:
                self.store.transaction(actions)
                return public
            except ClientError as error:
                if error.response["Error"]["Code"] != "TransactionCanceledException":
                    raise
                reasons = error.response.get("CancellationReasons", [])
                if any(r.get("Code") not in ("None", "ConditionalCheckFailed", "TransactionConflict") for r in reasons):
                    raise
                existing = self.replay(key)
                if existing:
                    return existing
        raise Failure(503, "service_unavailable")

    def login(self, code, source):
        if not isinstance(code, str) or not CODE.fullmatch(code):
            raise Failure(400, "invalid_request")
        self.limit("login", source)
        mapping = self.store.get("CODE#" + self.digest("code", code))
        if not mapping:
            raise Failure(401, "authentication_required")
        profile = self.profile(mapping["learner_id"])
        result, session = self.session(profile)
        try:
            self.store.transaction([
                ("ConditionCheck", {"Key": {"pk": profile["pk"]}, "ConditionExpression": "#s = :active",
                                    "ExpressionAttributeNames": {"#s": "status"}, "ExpressionAttributeValues": {":active": "active"}}),
                ("Put", {"Item": session, "ConditionExpression": "attribute_not_exists(pk)"}),
            ])
        except ClientError as error:
            if error.response["Error"]["Code"] == "TransactionCanceledException":
                raise Failure(503, "service_unavailable") from None
            raise
        return result

    def current(self, token):
        record = self.store.get(self.session_key(token))
        now = int(self.clock())
        if not record or record["expires_at"] <= now:
            raise Failure(401, "authentication_required")
        profile = self.profile(record["learner_id"])
        return {**self.public_profile(profile), "expires_in": int(record["expires_at"]) - now}

    def update_profile(self, token, details):
        identity = self.current(token)
        clean = self.validate_details(details)
        self.store.table.update_item(
            Key={"pk": "PROFILE#" + identity["learner_id"]},
            UpdateExpression="SET first_name = :first, middle_name = :middle, last_name = :last, program_id = :program, year_level = :year",
            ConditionExpression="#s = :active",
            ExpressionAttributeNames={"#s": "status"},
            ExpressionAttributeValues={":first": clean["first_name"], ":middle": clean["middle_name"],
                                       ":last": clean["last_name"], ":program": clean["program_id"],
                                       ":year": clean["year_level"], ":active": "active"})
        return self.current(token)

    def logout(self, token):
        self.store.table.delete_item(Key={"pk": self.session_key(token)})


@lru_cache(maxsize=1)
def service():
    config = Config(connect_timeout=2, read_timeout=3, retries={"max_attempts": 1})
    secret = boto3.client("secretsmanager", config=config).get_secret_value(SecretId=os.environ["ACCOUNTS_SECRET_ID"])
    keys = json.loads(secret["SecretString"])
    store = Store(os.environ["ACCOUNTS_TABLE_NAME"], boto3.resource("dynamodb", config=config))
    return Accounts(store, keys)


def response(status, data):
    return {"statusCode": status, "headers": {"content-type": "application/json", "cache-control": "no-store"},
            "body": json.dumps(data, allow_nan=False)}


def body(event):
    raw = event.get("body", "")
    if event.get("isBase64Encoded") or not isinstance(raw, str) or len(raw.encode()) > 8192:
        raise Failure(400, "invalid_request")
    try:
        result = json.loads(raw)
    except (ValueError, TypeError):
        raise Failure(400, "invalid_request") from None
    if not isinstance(result, dict):
        raise Failure(400, "invalid_request")
    return result


def lambda_handler(event, context):
    try:
        if event.get("type") == "REQUEST":
            # API Gateway checks the learner session before admitting profile writes.
            if event.get("routeKey") != "PUT /v1/auth/me/profile":
                return {"isAuthorized": False}
            headers = {str(k).lower(): v for k, v in (event.get("headers") or {}).items()}
            authorization = headers.get("authorization", "")
            match = re.fullmatch(r"Bearer ([A-Za-z0-9_-]{43})", authorization) if isinstance(authorization, str) else None
            if not match:
                return {"isAuthorized": False}
            try:
                service().current(match[1])
            except Failure:
                return {"isAuthorized": False}
            return {"isAuthorized": True}
        http = event.get("requestContext", {}).get("http", {})
        # IAM-authorized direct invocation only; API Gateway always supplies http context.
        if not http and event == {"diagnostic": "connectivity"}:
            service().store.get("DIAGNOSTIC#connectivity")
            return response(200, {"database": "reachable", "secret": "valid"})
        method, path = http.get("method"), event.get("rawPath")
        headers = {str(k).lower(): v for k, v in (event.get("headers") or {}).items()}
        if method == "GET" and path == "/v1/catalog/programs":
            return response(200, {"programs": catalog()})
        if method == "POST" and path in ("/v1/auth/profiles", "/v1/auth/login"):
            try:
                source = str(ipaddress.ip_address(http["sourceIp"]))
            except (KeyError, ValueError):
                raise Failure(400, "invalid_request") from None
            if not str(headers.get("content-type", "")).lower().startswith("application/json"):
                raise Failure(400, "invalid_request")
            data = body(event)
            if path.endswith("/profiles"):
                details = {field: data.get(field) for field in ("first_name", "middle_name", "last_name", "program_id", "year_level")} if "first_name" in data else None
                return response(200, service().create(data.get("request_id"), source, details))
            return response(200, service().login(data.get("access_code"), source))
        if (method, path) not in (("GET", "/v1/auth/me"), ("PUT", "/v1/auth/me/profile"), ("POST", "/v1/auth/logout")):
            return response(404, {"error": "route_not_found"})
        authorization = headers.get("authorization", "")
        match = re.fullmatch(r"Bearer ([A-Za-z0-9_-]{43})", authorization) if isinstance(authorization, str) else None
        if not match:
            raise Failure(401, "authentication_required")
        if path.endswith("/me"):
            return response(200, service().current(match[1]))
        if path.endswith("/profile"):
            if not str(headers.get("content-type", "")).lower().startswith("application/json"):
                raise Failure(400, "invalid_request")
            return response(200, service().update_profile(match[1], body(event)))
        service().logout(match[1])
        return response(200, {"signed_out": True})
    except Failure as error:
        data = {"error": error.error}
        if error.retry:
            data["retry_after_seconds"] = error.retry
        return response(error.status, data)
    except Exception as error:
        if isinstance(error, ClientError):
            LOG.error("Account AWS request failed: operation=%s code=%s", error.operation_name,
                      error.response.get("Error", {}).get("Code", "unknown"))
        else:
            LOG.error("Account request failed (%s)", type(error).__name__)
        return response(503, {"error": "service_unavailable"})
