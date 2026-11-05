import copy
import unittest
from unittest.mock import patch

from quiz_service import QuizService, SessionConflict


class MemoryStore:
    def __init__(self):
        self.states = {}

    def get(self, learner_id):
        state = self.states.get(learner_id)
        return copy.deepcopy(state) if state else None

    def create(self, learner_id, state):
        if learner_id in self.states:
            return False
        self.states[learner_id] = copy.deepcopy(state)
        return True

    def replace(self, learner_id, state, expected_revision):
        old = self.states.get(learner_id)
        if old is None or old["revision"] != expected_revision:
            return False
        self.states[learner_id] = copy.deepcopy(state)
        return True


class QuizServiceTests(unittest.TestCase):
    def setUp(self):
        self.store = MemoryStore()
        self.now = 100.0
        self.service = QuizService(self.store, clock=lambda: self.now)

    def test_start_resumes_and_separates_learners(self):
        alice = self.service.start("alice")
        self.assertEqual(alice["mastery"], 0.5)
        self.assertEqual(alice["question"]["difficulty_level"], "Medium")
        self.assertEqual(self.service.start("alice"), alice)
        bob = self.service.start("bob")
        self.assertNotEqual(bob["session_id"], alice["session_id"])
        self.assertEqual(self.service.current("alice"), alice)

    def test_answer_uses_server_time_and_replays_retry_once(self):
        session = self.service.start("alice")
        self.now = 106.5
        feedback = {
            "question_id": session["question"]["question_id"],
            "correct": True,
            "correct_option": "B",
            "previous_mastery": 0.5,
            "updated_mastery": 0.6,
            "mastery_change": 0.1,
        }
        attempt_id = "3ee09c40-14a4-4a7a-aeee-d5cc1a113f4a"
        with patch("quiz_service.score_answer", return_value=feedback) as scoring:
            first = self.service.answer(
                "alice", session["session_id"], session["question"]["question_id"],
                "B", attempt_id,
            )
            retry = self.service.answer(
                "alice", session["session_id"], session["question"]["question_id"],
                "B", attempt_id,
            )
        self.assertEqual(first, retry)
        scoring.assert_called_once_with(session["question"]["question_id"], "B", 6.5, 0.5)
        self.assertEqual(self.service.current("alice")["mastery"], 0.6)
        self.assertEqual(self.service.current("alice")["answered_count"], 1)

    def test_stale_question_is_rejected(self):
        session = self.service.start("alice")
        with self.assertRaises(SessionConflict):
            self.service.answer(
                "alice", session["session_id"], "rib_01", "A",
                "98c9a143-79c3-4a81-8b82-4cb9e03b6bed",
            )

    def test_ai_feedback_is_committed_once_and_attempt_conflicts(self):
        session = self.service.start("alice")
        qid = session["question"]["question_id"]
        attempt = "3ee09c40-14a4-4a7a-aeee-d5cc1a113f4a"
        feedback = {"question_id": qid, "correct": True, "correct_option": "A",
                    "previous_mastery": 0.5, "updated_mastery": 0.6,
                    "mastery_change": 0.1, "raw_model_delta": 0.1}
        with patch("quiz_service.score_answer", return_value=feedback), patch("quiz_service.generate", return_value={"explanation": "The bone protects the brain.",
                                                           "recommended_action": "increase"}) as ai:
            result = self.service.answer("alice", session["session_id"], qid, "A", attempt)
            retry = self.service.answer("alice", session["session_id"], qid, "A", attempt)
        self.assertEqual(result, retry)
        self.assertEqual(result["explanation_source"], "gemini")
        self.assertEqual(result["difficulty_source"], "mastery")
        self.assertEqual(len(self.store.states["alice"]["history"]), 1)
        ai.assert_called_once()
        with self.assertRaises(SessionConflict):
            self.service.answer("alice", session["session_id"], qid, "B", attempt)

    def test_invalid_advice_keeps_explanation_and_falls_back(self):
        session = self.service.start("alice")
        qid = session["question"]["question_id"]
        feedback = {"question_id": qid, "correct": True, "correct_option": "A",
                    "previous_mastery": 0.5, "updated_mastery": 0.6,
                    "mastery_change": 0.1, "raw_model_delta": 0.1}
        with patch("quiz_service.score_answer", return_value=feedback), patch("quiz_service.generate", return_value={"explanation": "A useful explanation.",
                                                           "recommended_action": "jump"}), \
             patch.dict("os.environ", {"AI_ADAPTATION_ENABLED": "true"}):
            result = self.service.answer("alice", session["session_id"],
                                         session["question"]["question_id"], "A",
                                         "3ee09c40-14a4-4a7a-aeee-d5cc1a113f4a")
        self.assertEqual(result["explanation"], "A useful explanation.")
        self.assertIsNone(result["recommended_action"])
        self.assertEqual(result["difficulty_source"], "mastery")

    def test_progress_summary_is_not_shown_as_anatomy_explanation(self):
        session = self.service.start("alice")
        qid = session["question"]["question_id"]
        feedback = {"question_id": qid, "correct": True, "correct_option": "A",
                    "previous_mastery": 0.5, "updated_mastery": 0.6,
                    "mastery_change": 0.1, "raw_model_delta": 0.1}
        with patch("quiz_service.score_answer", return_value=feedback), \
             patch("quiz_service.generate", return_value={
                 "explanation": "The learner's mastery increased after a correct answer.",
                 "recommended_action": "maintain"}):
            result = self.service.answer("alice", session["session_id"], qid, "A",
                                         "3ee09c40-14a4-4a7a-aeee-d5cc1a113f4a")
        self.assertEqual(result["explanation_source"], "fallback")
        self.assertIn("The correct answer is", result["explanation"])

    def test_provider_failure_falls_back_and_retry_does_not_rescore(self):
        session = self.service.start("alice")
        question = session["question"]
        attempt = "3ee09c40-14a4-4a7a-aeee-d5cc1a113f4a"
        feedback = {"question_id": question["question_id"], "correct": False,
                    "correct_option": "B", "previous_mastery": 0.5,
                    "updated_mastery": 0.45, "mastery_change": -0.05,
                    "raw_model_delta": -0.05}
        with patch("quiz_service.generate", return_value={}) as provider, \
             patch("quiz_service.score_answer", return_value=feedback) as score:
            result = self.service.answer("alice", session["session_id"],
                                         question["question_id"], "A", attempt)
            retry = self.service.answer("alice", session["session_id"],
                                        question["question_id"], "A", attempt)
        self.assertEqual(result, retry)
        self.assertEqual(result["explanation_source"], "fallback")
        self.assertEqual(result["difficulty_source"], "mastery")
        self.assertEqual(provider.call_count, 1)
        self.assertEqual(score.call_count, 1)
        self.assertEqual(self.service.current("alice")["answered_count"], 1)

    def test_adaptation_starts_on_fifth_recent_attempt_and_bounds_history(self):
        session = self.service.start("alice")
        with patch.dict("os.environ", {"AI_ADAPTATION_ENABLED": "true"}), \
             patch("quiz_service.generate", return_value={"explanation": "Brief.",
                                                          "recommended_action": "increase"}), \
             patch("quiz_service.score_answer") as score:
            for index in range(12):
                question = session["question"]
                score.return_value = {"question_id": question["question_id"], "correct": True,
                                      "correct_option": "A", "previous_mastery": 0.5,
                                      "updated_mastery": 0.5, "mastery_change": 0.0,
                                      "raw_model_delta": 0.1}
                result = self.service.answer("alice", session["session_id"],
                                             question["question_id"], "A",
                                             f"3ee09c40-14a4-4a7a-aeee-{index:012d}")
                if index < 4:
                    self.assertEqual(result["difficulty_source"], "mastery")
                if index == 4:
                    self.assertEqual(result["difficulty_source"], "gemini")
                    self.assertEqual(result["selected_difficulty"], "hard")
                session["question"] = result["next_question"]
        self.assertEqual(len(self.store.states["alice"]["history"]), 10)
        self.assertEqual(score.call_count, 12)

    def test_recommendations_select_only_adjacent_tiers_after_five_attempts(self):
        cases = (("decrease", "easy"), ("maintain", "medium"),
                 ("increase", "hard"))
        for action, expected_tier in cases:
            with self.subTest(action=action):
                store = MemoryStore()
                service = QuizService(store, clock=lambda: 100.0)
                session = service.start(action)
                state = store.states[action]
                state["history"] = [{"question_id": f"prior_{i}"} for i in range(4)]
                question = session["question"]
                feedback = {"question_id": question["question_id"], "correct": True,
                            "correct_option": "B", "previous_mastery": 0.5,
                            "updated_mastery": 0.5, "mastery_change": 0.0,
                            "raw_model_delta": 0.0}
                with patch.dict("os.environ", {"AI_ADAPTATION_ENABLED": "true"}), \
                     patch("quiz_service.generate", return_value={
                         "explanation": "Thoracic vertebrae articulate with the ribs.",
                         "recommended_action": action}), \
                     patch("quiz_service.score_answer", return_value=feedback):
                    result = service.answer(action, session["session_id"],
                                            question["question_id"], "B",
                                            "3ee09c40-14a4-4a7a-aeee-d5cc1a113f4a")
                self.assertEqual(result["recommended_difficulty"], expected_tier)
                self.assertEqual(result["selected_difficulty"], expected_tier)
                self.assertEqual(result["difficulty_source"], "gemini")


if __name__ == "__main__":
    unittest.main()
