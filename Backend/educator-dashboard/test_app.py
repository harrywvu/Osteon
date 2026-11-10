import importlib.util
import sys
import types
import unittest
from pathlib import Path
from unittest.mock import patch


spec = importlib.util.spec_from_file_location("educator_app", Path(__file__).with_name("app.py"))
app = importlib.util.module_from_spec(spec)
spec.loader.exec_module(app)


class FakeTable:
    def __init__(self, pages):
        self.pages = pages
        self.calls = []

    def scan(self, **kwargs):
        self.calls.append(kwargs)
        return self.pages[len(self.calls) - 1]


class DashboardTests(unittest.TestCase):
    def test_scan_paginates(self):
        table = FakeTable([{"Items": [{"id": 1}], "LastEvaluatedKey": {"pk": "one"}},
                           {"Items": [{"id": 2}]}])
        self.assertEqual([{"id": 1}, {"id": 2}], app.scan_all(table, ProjectionExpression="id"))
        self.assertEqual({"pk": "one"}, table.calls[1]["ExclusiveStartKey"])

    def test_questions_show_key_and_escape_csv_content(self):
        with patch.object(app, "questions", return_value=[{
            "question_id": "q1", "bone_group": "Axial", "bone_name": "Rib",
            "difficulty_level": "Easy", "difficulty_score": "0.2",
            "question_text": "<script>alert(1)</script>",
            "option_a": "Wrong", "option_b": "Right", "option_c": "C", "option_d": "D",
            "correct_option": "B",
        }]):
            output = app.question_page("")
        self.assertIn("B. Right (correct)", output)
        self.assertIn("&lt;script&gt;", output)
        self.assertNotIn("<script>", output)

    def test_profiles_join_only_profile_rows_and_progress(self):
        fake_conditions = types.ModuleType("boto3.dynamodb.conditions")
        class Attr:
            def __init__(self, name):
                self.name = name
            def begins_with(self, value):
                return (self.name, value)
        fake_conditions.Attr = Attr
        accounts = FakeTable([{"Items": [{"pk": "PROFILE#1", "learner_id": "1", "profile_label": "Learner <1>",
                                           "status": "active", "created_at": 1}]}])
        quiz = FakeTable([{"Items": [{"learner_id": "1", "payload": '{"mastery":0.8,"asked_ids":["q1"],"history":[]}'}]}])
        with patch.dict(sys.modules, {"boto3": types.ModuleType("boto3"),
                                      "boto3.dynamodb": types.ModuleType("boto3.dynamodb"),
                                      "boto3.dynamodb.conditions": fake_conditions}):
            records = app.learner_profiles(accounts, quiz)
        self.assertEqual(0.8, records[0]["progress"]["mastery"])
        self.assertEqual("pk, learner_id, profile_label, #status, created_at", accounts.calls[0]["ProjectionExpression"])
        self.assertIn("Learner &lt;1&gt;", app.learners_page(records))


if __name__ == "__main__":
    unittest.main()
