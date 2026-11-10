import json
import os
import unittest
import uuid
from unittest.mock import patch

import boto3
from moto import mock_aws
from cryptography.exceptions import InvalidTag

import lambda_function as backend


class AccountsTest(unittest.TestCase):
    def setUp(self):
        self.aws = mock_aws()
        self.aws.start()
        self.env = patch.dict(os.environ, {"AWS_DEFAULT_REGION": "ap-southeast-2",
                              "ACCOUNTS_TABLE_NAME": "test-accounts", "ACCOUNTS_SECRET_ID": "test-keys"})
        self.env.start()
        self.resource = boto3.resource("dynamodb")
        self.table = self.resource.create_table(
            TableName="test-accounts", KeySchema=[{"AttributeName": "pk", "KeyType": "HASH"}],
            AttributeDefinitions=[{"AttributeName": "pk", "AttributeType": "S"}], BillingMode="PAY_PER_REQUEST")
        self.keys = {"code_lookup_key": "01" * 32, "replay_encryption_key": "02" * 32}
        boto3.client("secretsmanager").create_secret(Name="test-keys", SecretString=json.dumps(self.keys))
        self.now = 1800000000
        self.accounts = backend.Accounts(backend.Store("test-accounts", self.resource), self.keys, lambda: self.now)
        self.service_patch = patch.object(backend, "service", return_value=self.accounts)
        self.service_patch.start()

    def tearDown(self):
        self.service_patch.stop()
        self.env.stop()
        self.aws.stop()

    def create(self, request=None, source="203.0.113.10"):
        return self.accounts.create(request or str(uuid.uuid4()), source)

    def request(self, method, path, data=None, token=None, **extra):
        event = {"version": "2.0", "rawPath": path,
                 "requestContext": {"http": {"method": method, "sourceIp": "203.0.113.10"}},
                 "headers": {"content-type": "application/json"}, "body": json.dumps(data or {})}
        if token:
            event["headers"]["authorization"] = "Bearer " + token
        event.update(extra)
        response = backend.lambda_handler(event, None)
        return response["statusCode"], json.loads(response["body"])

    def assert_failure(self, status, operation):
        with self.assertRaises(backend.Failure) as caught:
            operation()
        self.assertEqual(status, caught.exception.status)

    def test_creation_replay_and_protected_records(self):
        self.accounts.code_generator = lambda: "000123"
        request_id = str(uuid.uuid4())
        first = self.create(request_id)
        self.assertEqual("000123", first["access_code"])
        self.assertEqual(first, self.create(request_id))
        items = self.table.scan()["Items"]
        self.assertEqual(1, sum(item["pk"].startswith("PROFILE#") for item in items))
        self.assertNotIn(first["access_token"], json.dumps(items, default=str))
        self.assertNotIn('"access_code"', json.dumps(items, default=str))
        self.assertNotIn("CODE#000123", json.dumps(items, default=str))

    def test_service_restart_preserves_account(self):
        first = self.create()
        fresh = backend.Accounts(backend.Store("test-accounts", self.resource), self.keys, lambda: self.now)
        login = fresh.login(first["access_code"], "203.0.113.11")
        self.assertEqual(first["learner_id"], login["learner_id"])

    def test_collision_does_not_leave_orphan_profile(self):
        codes = iter(["000123", "000123", "345678"])
        self.accounts.code_generator = lambda: next(codes)
        a, b = self.create(), self.create()
        self.assertEqual("345678", b["access_code"])
        self.assertNotEqual(a["learner_id"], b["learner_id"])
        self.assertEqual(2, sum(i["pk"].startswith("PROFILE#") for i in self.table.scan()["Items"]))

    def test_two_devices_logout_and_account_isolation(self):
        a, b = self.create(), self.create()
        a2 = self.accounts.login(a["access_code"], "203.0.113.10")
        self.accounts.logout(a["access_token"])
        self.accounts.logout(a["access_token"])
        self.assert_failure(401, lambda: self.accounts.current(a["access_token"]))
        self.assertEqual(a["learner_id"], self.accounts.current(a2["access_token"])["learner_id"])
        self.assertEqual(b["learner_id"], self.accounts.current(b["access_token"])["learner_id"])

    def test_expiry_without_ttl_deletion(self):
        first = self.create()
        self.now += 3600
        self.assertIsNotNone(self.accounts.store.get(self.accounts.session_key(first["access_token"])))
        self.assert_failure(401, lambda: self.accounts.current(first["access_token"]))
        self.assertEqual(first["learner_id"], self.accounts.login(first["access_code"], "203.0.113.10")["learner_id"])

    def test_disabled_profile_generic_rejection(self):
        first = self.create()
        self.table.update_item(Key={"pk": "PROFILE#" + first["learner_id"]},
                              UpdateExpression="SET #s = :disabled", ExpressionAttributeNames={"#s": "status"},
                              ExpressionAttributeValues={":disabled": "disabled"})
        self.assert_failure(401, lambda: self.accounts.current(first["access_token"]))
        self.assert_failure(401, lambda: self.accounts.login(first["access_code"], "203.0.113.10"))
        self.assert_failure(401, lambda: self.accounts.login("999999", "203.0.113.10"))

    def test_rate_limits_persist_across_workers_and_codes(self):
        for n in range(10):
            self.assert_failure(401, lambda n=n: self.accounts.login(f"{n:06d}", "203.0.113.10"))
        worker = backend.Accounts(backend.Store("test-accounts", self.resource), self.keys, lambda: self.now)
        self.assert_failure(429, lambda: worker.login("123456", "203.0.113.10"))
        self.now += 60
        self.assert_failure(401, lambda: worker.login("123456", "203.0.113.10"))

    def test_creation_rate_limit_and_replay_window(self):
        for _ in range(5):
            self.create()
        self.assert_failure(429, lambda: self.create())
        self.now += 60
        request = str(uuid.uuid4())
        first = self.create(request)
        self.now += 86400
        second = self.create(request)
        self.assertNotEqual(first["learner_id"], second["learner_id"])

    def test_replay_does_not_restore_revoked_session(self):
        request = str(uuid.uuid4())
        first = self.create(request)
        self.accounts.logout(first["access_token"])
        replay = self.create(request)
        self.assertEqual(0, replay["expires_in"])
        self.assertEqual(first["access_code"], replay["access_code"])
        self.assert_failure(401, lambda: self.accounts.current(replay["access_token"]))

    def test_encrypted_replay_authenticates_content(self):
        request = str(uuid.uuid4())
        self.create(request)
        key = "CREATE#" + self.accounts.digest("request", request)
        self.table.update_item(Key={"pk": key}, UpdateExpression="SET ciphertext = :bad",
                              ExpressionAttributeValues={":bad": "dGFtcGVyZWQ="})
        with self.assertRaises(InvalidTag):
            self.accounts.replay(key)

    def test_http_lifecycle_and_headers(self):
        status, first = self.request("POST", "/v1/auth/profiles", {"request_id": str(uuid.uuid4())})
        self.assertEqual(200, status)
        status, login = self.request("POST", "/v1/auth/login", {"access_code": first["access_code"]})
        self.assertEqual(200, status)
        self.assertEqual(first["learner_id"], login["learner_id"])
        status, me = self.request("GET", "/v1/auth/me", token=login["access_token"])
        self.assertEqual(200, status)
        self.assertNotIn("access_code", me)
        self.assertNotIn("access_token", me)
        self.assertEqual(200, self.request("POST", "/v1/auth/logout", token=login["access_token"])[0])
        self.assertEqual(401, self.request("GET", "/v1/auth/me", token=login["access_token"])[0])
        response = backend.lambda_handler({}, None)
        self.assertEqual("no-store", response["headers"]["cache-control"])

    def test_http_invalid_input_and_identity_override(self):
        for value in ("123", "１２３４５６", 123456, None):
            self.assertEqual(400, self.request("POST", "/v1/auth/login", {"access_code": value})[0])
        for raw in ("{", "[]", "x" * 8193):
            self.assertEqual(400, self.request("POST", "/v1/auth/login", body=raw)[0])
        self.assertEqual(400, self.request("POST", "/v1/auth/login", isBase64Encoded=True)[0])
        self.assertEqual(401, self.request("GET", "/v1/auth/me", {"learner_id": "someone-else"})[0])
        self.assertEqual(400, self.request("POST", "/v1/auth/profiles", {"request_id": "invalid"})[0])

    def test_diagnostic_never_returns_secret_values(self):
        result = backend.lambda_handler({"diagnostic": "connectivity"}, None)
        self.assertEqual(200, result["statusCode"])
        self.assertEqual({"database": "reachable", "secret": "valid"}, json.loads(result["body"]))
        self.assertEqual(404, self.request("POST", "/diagnostic", {"diagnostic": "connectivity"})[0])

    def test_real_secret_initialization_and_missing_table(self):
        self.service_patch.stop()
        backend.service.cache_clear()
        try:
            response = backend.lambda_handler({"diagnostic": "connectivity"}, None)
            self.assertEqual(200, response["statusCode"])
            self.table.delete()
            with self.assertLogs(backend.LOG, level="ERROR") as captured:
                response = backend.lambda_handler({"diagnostic": "connectivity"}, None)
            self.assertEqual(503, response["statusCode"])
            self.assertNotIn(self.keys["code_lookup_key"], str(captured.output))
            self.assertIn("ResourceNotFoundException", str(captured.output))
        finally:
            backend.service.cache_clear()
            self.service_patch.start()

    def test_named_profile_creation_completion_and_isolation(self):
        details = {"first_name": "  José  ", "middle_name": "O'Neil", "last_name": "<Santos>",
                   "program_id": "bs_nursing", "year_level": 2}
        created = self.accounts.create(str(uuid.uuid4()), "203.0.113.10", details)
        self.assertTrue(created["profile_complete"])
        self.assertEqual("José", created["first_name"])
        self.assertEqual("<Santos>", self.accounts.current(created["access_token"])["last_name"])
        other = self.create()
        self.assertFalse(other["profile_complete"])
        modified = self.accounts.update_profile(other["access_token"], details)
        self.assertEqual("José", modified["first_name"])
        self.assertEqual("José", self.accounts.current(other["access_token"])["first_name"])
        self.assertEqual(created["learner_id"], self.accounts.current(created["access_token"])["learner_id"])
        self.assertNotEqual(other["learner_id"], created["learner_id"])
        self.assertEqual(200, self.request("PUT", "/v1/auth/me/profile", details,
                                            token=other["access_token"])[0])
        self.assertEqual(401, self.request("PUT", "/v1/auth/me/profile",
                                           {**details, "learner_id": created["learner_id"]})[0])
        self.assertEqual(200, self.request("GET", "/v1/catalog/programs")[0])

    def test_profile_catalog_and_year_are_validated(self):
        details = {"first_name": "Ada", "middle_name": "", "last_name": "Lovelace",
                   "program_id": "bs_computer_science", "year_level": 1}
        for change in ({"program_id": "unknown"}, {"year_level": 0}, {"year_level": True},
                       {"first_name": ""}, {"last_name": "x" * 81}):
            with self.subTest(change=change):
                self.assert_failure(400, lambda: self.accounts.validate_details({**details, **change}))

    def test_profile_write_authorizer_requires_live_learner_session(self):
        created = self.create()
        event = {"version": "2.0", "type": "REQUEST", "routeKey": "PUT /v1/auth/me/profile",
                 "headers": {"authorization": "Bearer " + created["access_token"]}}
        self.assertEqual({"isAuthorized": True}, backend.lambda_handler(event, None))
        self.assertEqual({"isAuthorized": False}, backend.lambda_handler({**event, "headers": {}}, None))
        self.assertEqual({"isAuthorized": False}, backend.lambda_handler({**event, "routeKey": "GET /v1/auth/me"}, None))
        self.accounts.logout(created["access_token"])
        self.assertEqual({"isAuthorized": False}, backend.lambda_handler(event, None))


if __name__ == "__main__":
    unittest.main()
