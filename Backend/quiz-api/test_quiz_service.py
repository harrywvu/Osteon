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


if __name__ == "__main__":
    unittest.main()
