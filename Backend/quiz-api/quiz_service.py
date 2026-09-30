"""Session rules shared by the Quest and web API clients."""

import time
from uuid import UUID, uuid4

from quiz_engine import content_version, next_question, question_by_id, score_answer


class SessionNotFound(Exception):
    pass


class SessionConflict(Exception):
    pass


class QuizService:
    def __init__(self, store, clock=None):
        self.store = store
        self.clock = clock or time.time

    def start(self, learner_id):
        """Resume an active session, or begin one using saved mastery."""
        for _ in range(3):
            previous = self.store.get(learner_id)
            if previous and not previous["completed"] and previous.get("content_version") == content_version():
                return self._public_state(previous)

            mastery = float(previous["mastery"]) if previous else 0.50
            question = next_question(mastery, [])
            state = {
                "session_id": str(uuid4()),
                "content_version": content_version(),
                "mastery": mastery,
                "asked_ids": [],
                "current_question_id": question["question_id"] if question else None,
                "issued_at": self.clock() if question else None,
                "completed": question is None,
                "last_attempt_id": None,
                "last_result": None,
                "revision": int(previous["revision"]) + 1 if previous else 0,
            }
            saved = (
                self.store.replace(learner_id, state, previous["revision"])
                if previous else self.store.create(learner_id, state)
            )
            if saved:
                return self._public_state(state)
        raise SessionConflict("session changed while starting")

    def current(self, learner_id):
        state = self.store.get(learner_id)
        if state is None:
            raise SessionNotFound("no quiz session for this learner")
        if state.get("content_version") != content_version():
            raise SessionConflict("quiz content changed; start a new session")
        return self._public_state(state)

    def answer(self, learner_id, session_id, question_id, choice, attempt_id):
        """Score once, then save the result with an optimistic revision check."""
        try:
            UUID(attempt_id)
        except (TypeError, ValueError, AttributeError) as error:
            raise ValueError("attempt_id must be a UUID") from error

        state = self.store.get(learner_id)
        if state is None or state["session_id"] != session_id:
            raise SessionNotFound("quiz session not found")
        if state.get("content_version") != content_version():
            raise SessionConflict("quiz content changed; start a new session")
        if state["last_attempt_id"] == attempt_id:
            return state["last_result"]
        if state["completed"]:
            raise SessionConflict("quiz session is complete")
        if state["current_question_id"] != question_id:
            raise SessionConflict("question is no longer current")

        now = self.clock()
        seconds = max(0.0, now - float(state["issued_at"]))
        feedback = score_answer(question_id, choice, seconds, float(state["mastery"]))
        asked = [*state["asked_ids"], question_id]
        following = next_question(feedback["updated_mastery"], asked)
        result = {
            **feedback,
            "session_id": session_id,
            "answered_count": len(asked),
            "completed": following is None,
            "next_question": following,
        }
        updated = {
            **state,
            "mastery": feedback["updated_mastery"],
            "asked_ids": asked,
            "current_question_id": following["question_id"] if following else None,
            "issued_at": now if following else None,
            "completed": following is None,
            "last_attempt_id": attempt_id,
            "last_result": result,
            "revision": state["revision"] + 1,
        }
        if self.store.replace(learner_id, updated, state["revision"]):
            return result

        # An HTTP retry can race the first request. Replay its saved response.
        latest = self.store.get(learner_id)
        if latest and latest["last_attempt_id"] == attempt_id:
            return latest["last_result"]
        raise SessionConflict("session changed; refresh before answering")

    @staticmethod
    def _public_state(state):
        question_id = state["current_question_id"]
        return {
            "session_id": state["session_id"],
            "mastery": float(state["mastery"]),
            "answered_count": len(state["asked_ids"]),
            "completed": state["completed"],
            "question": question_by_id(question_id) if question_id else None,
        }
