import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


AUDIT_PATH = Path(__file__).resolve().parents[2] / "Tools" / "Audit-QuizGemini.py"
SPEC = importlib.util.spec_from_file_location("quiz_live_audit", AUDIT_PATH)
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class LiveAuditRunnerTests(unittest.TestCase):
    def test_full_matrix_has_two_answers_per_question_and_repeated_histories(self):
        rows = audit.questions()
        self.assertEqual(len(rows), 50)
        self.assertEqual(len({row["question_id"] for row in rows}), 50)
        fixtures = list(audit.recommendation_cases(
            next(row for row in rows if row["question_id"] == "rib_04"), 3))
        self.assertEqual(len(fixtures), 12)
        self.assertEqual({case["scenario"] for case in fixtures},
                         {"sparse", "weak", "improving", "mixed"})
        sample = [*fixtures, audit.question_case(rows[0], rows[0]["correct_option"])]
        self.assertEqual(audit.pending_cases(sample, {}, recommendations_only=True), fixtures)
        for case in fixtures:
            for previous, current in zip(case["history"], case["history"][1:]):
                self.assertEqual(previous["updated_mastery"], current["previous_mastery"])

    def test_resume_keeps_success_and_retries_only_missing_case(self):
        row = audit.questions()[0]
        correct = audit.question_case(row, row["correct_option"])
        wrong = audit.question_case(row, next(letter for letter in "ABCD" if letter != row["correct_option"]))
        response = {"explanation": "The thoracic cavity is enclosed by the ribs.",
                    "recommended_action": "maintain"}
        with tempfile.TemporaryDirectory(dir=audit.ROOT / "Backend" / "quiz-api" / "dist") as folder:
            output = Path(folder) / "report.json"
            with patch.object(audit.ai_feedback, "generate", side_effect=[response, {}]):
                initial = [audit.audit_case(correct), audit.audit_case(wrong)]
            self.assertEqual(initial[0]["updated_mastery"], 0.55)
            self.assertIsNone(initial[0]["assessment"])
            audit.save_report(output, initial, 2)
            with patch.object(sys, "argv", [str(AUDIT_PATH), "--resume", "--output", str(output)]), \
                 patch.object(audit.getpass, "getpass", return_value="secret-from-prompt"), \
                 patch.object(audit.ai_feedback, "generate", return_value=response) as provider, \
                 patch.object(audit.time, "sleep") as sleep:
                audit.main()
            provider.assert_called_once()
            sleep.assert_not_called()
            report = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(report["successful_cases"], 2)
            self.assertEqual(report["direct_provider_requests"], 3)
            self.assertNotIn("secret-from-prompt", output.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
