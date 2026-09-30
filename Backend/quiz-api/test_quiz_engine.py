import unittest
from unittest.mock import patch

import numpy as np

import quiz_engine


class FakeSession:
    def __init__(self, delta):
        self.delta = delta
        self.features = None

    def get_inputs(self):
        return [type("Input", (), {"name": "features"})()]

    def get_outputs(self):
        return [type("Output", (), {"name": "delta"})()]

    def run(self, output_names, inputs):
        self.features = inputs["features"]
        return [np.array([[self.delta]], dtype=np.float32)]


class QuizEngineTests(unittest.TestCase):
    def test_question_selection_uses_mastery_tier_and_hides_answer(self):
        easy = quiz_engine.next_question(0.49, set())
        medium = quiz_engine.next_question(0.50, set())
        hard = quiz_engine.next_question(0.76, set())
        self.assertEqual((easy["difficulty_level"], medium["difficulty_level"], hard["difficulty_level"]),
                         ("Easy", "Medium", "Hard"))
        self.assertNotIn("correct_option", medium)
        self.assertEqual(set(medium["options"]), set("ABCD"))

    def test_question_selection_falls_back_then_finishes(self):
        rows = quiz_engine._questions()
        medium_ids = {q["question_id"] for q in rows if q["difficulty_level"] == "Medium"}
        fallback = quiz_engine.next_question(0.50, medium_ids)
        self.assertEqual(fallback["question_id"], rows[0]["question_id"])
        self.assertIsNone(quiz_engine.next_question(0.50, {q["question_id"] for q in rows}))

    def test_model_feature_order_and_positive_result(self):
        fake = FakeSession(0.1)
        with patch.object(quiz_engine, "_model_session", return_value=fake):
            result = quiz_engine.score_answer("rib_01", "b", 4.0, 0.5)
        np.testing.assert_array_equal(
            fake.features,
            np.array([[0.2, 4.0, 1.0, 0.5, 0.0, 0.0]], dtype=np.float32),
        )
        self.assertTrue(result["correct"])
        self.assertAlmostEqual(result["updated_mastery"], 0.6)

    def test_original_guardrails_and_clamp(self):
        with patch.object(quiz_engine, "_model_session", return_value=FakeSession(0.1)):
            incorrect = quiz_engine.score_answer("rib_01", "A", 3.0, 0.5)
        self.assertAlmostEqual(incorrect["updated_mastery"], 0.38)

        with patch.object(quiz_engine, "_model_session", return_value=FakeSession(-0.1)):
            correct = quiz_engine.score_answer("rib_01", "B", 3.0, 0.5)
        self.assertAlmostEqual(correct["updated_mastery"], 0.55)

        with patch.object(quiz_engine, "_model_session", return_value=FakeSession(0.3)):
            capped = quiz_engine.score_answer("rib_01", "B", 3.0, 0.9)
        self.assertEqual(capped["updated_mastery"], 1.0)

    def test_invalid_choice_is_rejected(self):
        for choice in ("", "AB", "E"):
            with self.subTest(choice=choice), self.assertRaises(ValueError):
                quiz_engine.score_answer("rib_01", choice, 3.0, 0.5)


if __name__ == "__main__":
    unittest.main()
