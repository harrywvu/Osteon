import importlib.util
import json
import os
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "quiz-api"))
sys.path.insert(0, str(ROOT / "accounts-api"))

import api
import question_bank

if importlib.util.find_spec("moto"):
    import boto3
    from moto import mock_aws


@unittest.skipUnless(importlib.util.find_spec("moto"), "moto is required")
class EducatorApiTests(unittest.TestCase):
    def setUp(self):
        self.aws = mock_aws()
        self.aws.start()
        self.env = patch.dict(os.environ, {"AWS_DEFAULT_REGION": "ap-southeast-2",
                                        "QUESTION_BANK_TABLE_NAME": "bank",
                                        "ACCOUNTS_TABLE_NAME": "accounts",
                                        "QUIZ_TABLE_NAME": "progress",
                                        "EDUCATOR_CLIENT_ID": "educator-client"})
        self.env.start()
        api.tables.cache_clear()
        question_bank.table.cache_clear()
        question_bank.rows.cache_clear()
        resource = boto3.resource("dynamodb")
        self.bank = resource.create_table(TableName="bank", KeySchema=[{"AttributeName": "pk", "KeyType": "HASH"},
                                                                     {"AttributeName": "sk", "KeyType": "RANGE"}],
                                          AttributeDefinitions=[{"AttributeName": "pk", "AttributeType": "S"},
                                                                {"AttributeName": "sk", "AttributeType": "S"}],
                                          BillingMode="PAY_PER_REQUEST")
        self.accounts = resource.create_table(TableName="accounts", KeySchema=[{"AttributeName": "pk", "KeyType": "HASH"}],
                                              AttributeDefinitions=[{"AttributeName": "pk", "AttributeType": "S"}],
                                              BillingMode="PAY_PER_REQUEST")
        self.progress = resource.create_table(TableName="progress", KeySchema=[{"AttributeName": "learner_id", "KeyType": "HASH"}],
                                              AttributeDefinitions=[{"AttributeName": "learner_id", "AttributeType": "S"}],
                                              BillingMode="PAY_PER_REQUEST")
        self.version = "legacy-test"
        self.original = list(question_bank.packaged_rows())
        with self.bank.batch_writer() as batch:
            for order, row in enumerate(self.original):
                batch.put_item(Item={"pk": "VERSION#" + self.version, "sk": "QUESTION#" + row["question_id"],
                                     "order": order, "body": json.dumps(row)})
        self.bank.put_item(Item={"pk": "VERSION#" + self.version, "sk": "META", "count": 50})
        self.bank.put_item(Item={"pk": "ACTIVE", "sk": "META", "version": self.version})
        self.learner_id = "a" * 32
        self.accounts.put_item(Item={"pk": "PROFILE#" + self.learner_id, "learner_id": self.learner_id,
                                "first_name": "José", "last_name": "<Santos>", "program_id": "bs_nursing",
                                "year_level": 2, "profile_label": "Learner aaaaaa", "status": "active"})
        self.accounts.put_item(Item={"pk": "CODE#secret", "learner_id": self.learner_id})
        self.progress.put_item(Item={"learner_id": self.learner_id, "payload": json.dumps({
            "mastery": 0.6, "asked_ids": ["rib_01"], "completed": False,
            "current_question_id": "rib_02", "history": [{"question_id": "rib_01", "choice": "B",
            "correct": True, "bank_version": self.version}]})})

    def tearDown(self):
        api.tables.cache_clear()
        question_bank.table.cache_clear()
        question_bank.rows.cache_clear()
        self.env.stop()
        self.aws.stop()

    def request(self, method, path, body=None, claims=None, query=None):
        event = {"rawPath": path, "requestContext": {"http": {"method": method},
                 "authorizer": {"jwt": {"claims": claims if claims is not None else {
                     "token_use": "access", "client_id": "educator-client", "sub": "educator-1"}}}},
                 "body": json.dumps(body) if body is not None else None,
                 "queryStringParameters": query or {}}
        result = api.lambda_handler(event, None)
        return result["statusCode"], json.loads(result["body"])

    def test_rejects_missing_and_learner_identity(self):
        for claims in ({}, {"token_use": "access", "client_id": "learner-client", "sub": "learner"},
                       {"token_use": "id", "client_id": "educator-client", "sub": "educator"}):
            self.assertEqual(403, self.request("GET", "/v1/questions", claims=claims)[0])

    def test_self_registered_educator_needs_no_group(self):
        status, loaded = self.request("GET", "/v1/questions", claims={
            "token_use": "access", "client_id": "educator-client", "sub": "self-registered"})
        self.assertEqual(200, status)
        self.assertEqual(50, len(loaded["questions"]))

    def test_questions_publish_and_conflict(self):
        status, loaded = self.request("GET", "/v1/questions")
        self.assertEqual(200, status)
        self.assertEqual(50, len(loaded["questions"]))
        revised = json.loads(json.dumps(loaded["questions"]))
        revised[0]["question_text"] = "Revised wording?"
        body = {"base_version": loaded["version"], "questions": revised,
                "request_id": "3e8f7670-4183-4b25-9e3e-e4455a6fd936"}
        self.assertEqual(200, self.request("POST", "/v1/questions/publish", body)[0])
        self.assertEqual(200, self.request("POST", "/v1/questions/publish", body)[0])
        body["request_id"] = "44a6635b-c48e-41b4-9098-48aa925679ec"
        self.assertEqual(409, self.request("POST", "/v1/questions/publish", body)[0])

    def test_learner_projection_and_legacy_history(self):
        status, listing = self.request("GET", "/v1/learners", query={"q": "josé"})
        self.assertEqual(200, status)
        self.assertEqual(1, len(listing["learners"]))
        self.assertNotIn("secret", json.dumps(listing))
        status, detail = self.request("GET", "/v1/learners/" + self.learner_id)
        self.assertEqual(200, status)
        self.assertEqual("<Santos>", detail["profile"]["last_name"])
        self.assertEqual(self.original[0]["question_text"], detail["recent_answers"][0]["question_text"])
        self.assertEqual(self.original[0]["correct_option"], detail["recent_answers"][0]["correct_option"])
        self.assertEqual(1, detail["analysis"]["retained_count"])
        self.assertEqual([], detail["analysis"]["mastery_estimates"])
        self.progress.put_item(Item={"learner_id": self.learner_id, "payload": json.dumps({
            "mastery": 0.5, "asked_ids": [], "completed": False, "history": [
                {"question_id": "rib_01", "choice": "B", "correct": True}]})})
        detail = self.request("GET", "/v1/learners/" + self.learner_id)[1]
        self.assertNotIn("question_text", detail["recent_answers"][0])

    def test_recent_analysis_uses_retained_window_and_handles_missing_fields(self):
        history = [
            {"question_id": "rib_01", "choice": "A", "correct": True, "bone_group": "Axial",
             "difficulty": "Easy", "previous_mastery": 0.50, "updated_mastery": 0.55},
            {"question_id": "rib_02", "choice": "B", "correct": False, "bone_group": "Axial",
             "difficulty": "Medium", "previous_mastery": 0.55, "updated_mastery": 0.53},
            {"question_id": "unknown", "choice": "C", "correct": True, "bone_group": "Appendicular",
             "difficulty": "Hard", "previous_mastery": 0.53, "updated_mastery": 0.58},
        ]
        self.progress.put_item(Item={"learner_id": self.learner_id, "payload": json.dumps({
            "mastery": 0.58, "asked_ids": ["rib_01", "rib_02", "unknown"],
            "completed": False, "history": history})})
        detail = self.request("GET", "/v1/learners/" + self.learner_id)[1]
        analysis = detail["analysis"]
        self.assertEqual((3, 2), (analysis["retained_count"], analysis["correct_count"]))
        self.assertEqual([0.5, 0.55, 0.53, 0.58], analysis["mastery_estimates"])
        self.assertEqual([{"label": "Axial", "answered": 2, "correct": 1},
                          {"label": "Appendicular", "answered": 1, "correct": 1}], analysis["by_group"])
        self.assertEqual(3, len(analysis["by_difficulty"]))
        self.assertNotIn("correct_option", detail["recent_answers"][2])
        history[1].pop("updated_mastery")
        self.assertEqual([], api.recent_analysis(history)["mastery_estimates"])

    def test_search_paginates_profiles_without_credential_rows(self):
        with self.accounts.batch_writer() as batch:
            for number in range(25):
                learner_id = f"{number:032x}"
                batch.put_item(Item={"pk": "PROFILE#" + learner_id, "learner_id": learner_id,
                                     "first_name": "Ada", "last_name": f"Student {number:02d}",
                                     "profile_label": "Learner", "status": "active"})
                batch.put_item(Item={"pk": "CODE#" + learner_id, "learner_id": learner_id})
        status, first = self.request("GET", "/v1/learners", query={"q": "ada"})
        self.assertEqual(200, status)
        self.assertEqual(20, len(first["learners"]))
        self.assertEqual("20", first["next_cursor"])
        status, second = self.request("GET", "/v1/learners", query={"q": "ada", "cursor": first["next_cursor"]})
        self.assertEqual(200, status)
        self.assertEqual(5, len(second["learners"]))
        self.assertIsNone(second["next_cursor"])
        ids = [entry["profile"]["learner_id"] for entry in first["learners"] + second["learners"]]
        self.assertEqual(25, len(set(ids)))
        self.assertNotIn("CODE#", json.dumps(first) + json.dumps(second))
