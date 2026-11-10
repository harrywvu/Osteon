import copy
import importlib.util
import json
import os
import unittest
from unittest.mock import patch

import question_bank
from quiz_service import QuizService, SessionConflict
from test_quiz_service import MemoryStore

if importlib.util.find_spec("moto"):
    import boto3
    from moto import mock_aws


@unittest.skipUnless(importlib.util.find_spec("moto"), "moto is required")
class BankTests(unittest.TestCase):
    def setUp(self):
        self.aws = mock_aws()
        self.aws.start()
        self.env = patch.dict(os.environ, {"AWS_DEFAULT_REGION": "ap-southeast-2",
                                        "QUESTION_BANK_TABLE_NAME": "bank"})
        self.env.start()
        question_bank.table.cache_clear()
        question_bank.rows.cache_clear()
        self.db = boto3.resource("dynamodb").create_table(
            TableName="bank", KeySchema=[{"AttributeName": "pk", "KeyType": "HASH"},
                                         {"AttributeName": "sk", "KeyType": "RANGE"}],
            AttributeDefinitions=[{"AttributeName": "pk", "AttributeType": "S"},
                                  {"AttributeName": "sk", "AttributeType": "S"}],
            BillingMode="PAY_PER_REQUEST")
        self.original = list(question_bank.packaged_rows())
        self.version = "legacy-test"
        with self.db.batch_writer() as batch:
            for order, row in enumerate(self.original):
                batch.put_item(Item={"pk": "VERSION#" + self.version,
                                     "sk": "QUESTION#" + row["question_id"], "order": order,
                                     "body": json.dumps(row)})
        self.db.put_item(Item={"pk": "VERSION#" + self.version, "sk": "META", "count": len(self.original)})
        self.db.put_item(Item={"pk": "ACTIVE", "sk": "META", "version": self.version})

    def tearDown(self):
        question_bank.table.cache_clear()
        question_bank.rows.cache_clear()
        self.env.stop()
        self.aws.stop()

    def test_publish_pins_old_bank_and_retries(self):
        revised = copy.deepcopy(self.original)
        revised[0]["question_text"] = "Which cavity is protected by the ribs?"
        request = "0268c853-1a0f-4ae1-ae91-00ff2e0b0dfa"
        version = question_bank.publish(self.version, revised, request, "educator-1")
        self.assertEqual(version, question_bank.active_version())
        self.assertEqual(self.original, list(question_bank.rows(self.version)))
        self.assertEqual(revised[0]["question_text"], question_bank.rows(version)[0]["question_text"])
        self.assertEqual(version, question_bank.publish(self.version, revised, request, "educator-1"))
        with self.assertRaises(question_bank.PublishConflict):
            question_bank.publish(self.version, revised, "1c91107d-f998-43b5-a9a7-3b69a91a0fb3", "educator-2")

    def test_invalid_and_incomplete_versions_never_activate(self):
        revised = copy.deepcopy(self.original)
        revised[0]["difficulty_score"] = "nan"
        with self.assertRaises(ValueError):
            question_bank.publish(self.version, revised, "270cd49e-1e3a-49cf-b3e3-73e21be08be9", "educator-1")
        self.assertEqual(self.version, question_bank.active_version())
        self.db.put_item(Item={"pk": "VERSION#incomplete", "sk": "QUESTION#one", "order": 0, "body": "{}"})
        with self.assertRaises(RuntimeError):
            question_bank.rows("incomplete")

    def test_active_session_uses_original_bank_after_publish(self):
        store = MemoryStore()
        service = QuizService(store, clock=lambda: 100.0)
        old = service.start("learner-a")
        question_id = old["question"]["question_id"]
        revised = copy.deepcopy(self.original)
        row = next(row for row in revised if row["question_id"] == question_id)
        row["question_text"] = "A revised question for new sessions?"
        row["correct_option"] = "A" if row["correct_option"] != "A" else "B"
        new_version = question_bank.publish(self.version, revised,
            "91f917bb-5843-47a2-a073-b6e362a76473", "educator-1")
        self.assertEqual(old, service.current("learner-a"))
        feedback = {"question_id": question_id, "correct": True, "correct_option": "B",
                    "previous_mastery": 0.5, "updated_mastery": 0.6,
                    "mastery_change": 0.1, "raw_model_delta": 0.1}
        with patch("quiz_service.score_answer", return_value=feedback) as scoring, \
             patch("quiz_service.generate", return_value={}):
            service.answer("learner-a", old["session_id"], question_id, "B",
                           "d916e008-52b8-47e8-ace6-aac2135f916c")
        self.assertEqual(self.version, scoring.call_args.kwargs["bank_version"])
        self.assertEqual(self.version, store.states["learner-a"]["history"][-1]["bank_version"])
        service.start("learner-b")
        self.assertEqual(new_version, store.states["learner-b"]["bank_version"])

    def test_interrupted_staging_is_not_visible_and_retry_finishes(self):
        revised = copy.deepcopy(self.original)
        revised[0]["question_text"] = "Retry after interrupted staging?"
        request = "49659d4d-b0ad-40bc-b08e-ce46bb95ce53"
        body = json.dumps(question_bank.validate(revised, self.original), ensure_ascii=False,
                          sort_keys=True, separators=(",", ":"))
        import hashlib
        digest = hashlib.sha256((self.version + body).encode()).hexdigest()
        self.db.put_item(Item={"pk": "REQUEST#" + request, "sk": "META", "digest": digest,
                               "educator": "educator-1", "status": "staging"})
        self.db.put_item(Item={"pk": "VERSION#" + request, "sk": "QUESTION#" + revised[0]["question_id"],
                               "order": 0, "body": json.dumps(revised[0])})
        self.assertEqual(self.version, question_bank.active_version())
        with self.assertRaises(RuntimeError):
            question_bank.rows(request)
        self.assertEqual(request, question_bank.publish(self.version, revised, request, "educator-1"))
        self.assertEqual(request, question_bank.active_version())

    def test_legacy_hash_mapping_preserves_active_session(self):
        store = MemoryStore()
        service = QuizService(store, clock=lambda: 100.0)
        started = service.start("legacy-learner")
        state = store.states["legacy-learner"]
        state.pop("bank_version")
        state["content_version"] = "deployed-live-hash"
        with patch.dict(os.environ, {"LEGACY_CONTENT_VERSION": "deployed-live-hash",
                                  "LEGACY_BANK_VERSION": self.version}):
            self.assertEqual(started["question"], service.current("legacy-learner")["question"])
        with self.assertRaises(SessionConflict):
            service.current("legacy-learner")

    def test_multiple_legacy_hashes_map_to_their_own_banks(self):
        store = MemoryStore()
        service = QuizService(store, clock=lambda: 100.0)
        service.start("older-learner")
        state = store.states["older-learner"]
        state.pop("bank_version")
        state["content_version"] = "older-content-hash"
        with patch.dict(os.environ, {"LEGACY_BANK_MAP": json.dumps({
                "older-content-hash": self.version,
                "current-content-hash": "another-version"})}):
            self.assertEqual(self.version, service._pinned(state)["bank_version"])
            self.assertEqual(50, len(question_bank.rows(service._pinned(state)["bank_version"])))
