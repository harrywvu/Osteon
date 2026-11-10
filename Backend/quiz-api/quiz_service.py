"""Session rules shared by the Quest and web API clients."""

import time
import os
import json
import re
from uuid import UUID, uuid4

from quiz_engine import content_version, next_question, question_by_id, score_answer
from question_bank import active_version, model_version
from ai_feedback import generate


class SessionNotFound(Exception):
    pass


class SessionConflict(Exception):
    pass


class QuizService:
    def __init__(self, store, clock=None):
        self.store = store
        self.clock = clock or time.time

    @staticmethod
    def _pinned(state):
        if state.get("bank_version"):
            return {"bank_version": state["bank_version"]}
        if os.environ.get("QUESTION_BANK_TABLE_NAME"):
            try:
                legacy_map = json.loads(os.environ.get("LEGACY_BANK_MAP", "{}"))
            except ValueError:
                raise SessionConflict("legacy quiz bank mapping is invalid") from None
            if not isinstance(legacy_map, dict):
                raise SessionConflict("legacy quiz bank mapping is invalid")
            bank_version = legacy_map.get(state.get("content_version"))
            if isinstance(bank_version, str) and bank_version:
                return {"bank_version": bank_version}
            if os.environ.get("LEGACY_CONTENT_VERSION") and state.get("content_version") == os.environ["LEGACY_CONTENT_VERSION"]:
                return {"bank_version": os.environ["LEGACY_BANK_VERSION"]}
            raise SessionConflict("legacy quiz bank is unavailable; do not restart this learner")
        if state.get("content_version") != content_version():
            raise SessionConflict("quiz content changed; start a new session")
        return {}

    def start(self, learner_id):
        """Resume an active session, or begin one using saved mastery."""
        for _ in range(3):
            previous = self.store.get(learner_id)
            if previous and not previous["completed"]:
                try:
                    self._pinned(previous)
                    return self._public_state(previous)
                except SessionConflict:
                    if os.environ.get("QUESTION_BANK_TABLE_NAME"):
                        raise

            mastery = float(previous["mastery"]) if previous else 0.50
            version = active_version() if os.environ.get("QUESTION_BANK_TABLE_NAME") else None
            question = next_question(mastery, [], bank_version=version) if version else next_question(mastery, [])
            state = {
                "session_id": str(uuid4()),
                "content_version": content_version(),
                "model_version": model_version(),
                **({"bank_version": version} if version else {}),
                "mastery": mastery,
                "asked_ids": [],
                "current_question_id": question["question_id"] if question else None,
                "issued_at": self.clock() if question else None,
                "completed": question is None,
                "last_attempt_id": None,
                "last_result": None,
                "history": previous.get("history", []) if previous and (
                    version or previous.get("content_version") == content_version()) else [],
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
        self._pinned(state)
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
        pinned = self._pinned(state)
        if state["last_attempt_id"] == attempt_id:
            if (state["last_result"]["question_id"] != question_id or
                    state.get("last_choice") != choice):
                raise SessionConflict("attempt_id belongs to a different answer")
            return state["last_result"]
        if state["completed"]:
            raise SessionConflict("quiz session is complete")
        if state["current_question_id"] != question_id:
            raise SessionConflict("question is no longer current")

        now = self.clock()
        seconds = max(0.0, now - float(state["issued_at"]))
        feedback = score_answer(question_id, choice, seconds, float(state["mastery"]), **pinned)
        question = question_by_id(question_id, **pinned)
        raw_delta = feedback.pop("raw_model_delta", feedback["mastery_change"])
        history = [*state.get("history", []), {
            "question_id": question_id, "bone_group": question["bone_group"],
            "difficulty": question["difficulty_level"], "choice": choice,
            "correct": feedback["correct"], "previous_mastery": feedback["previous_mastery"],
            "updated_mastery": feedback["updated_mastery"], "accepted_at": now,
            **pinned,
        }][-10:]
        ai = generate(question, {"choice": choice, "correct": feedback["correct"],
                                 "correct_option": feedback["correct_option"],
                                 "raw_model_delta": raw_delta,
                                 "applied_mastery_change": feedback["mastery_change"],
                                 "updated_mastery": feedback["updated_mastery"]}, history[:-1])
        if not isinstance(ai, dict):
            ai = {}
        explanation = ai.get("explanation")
        if (not isinstance(explanation, str) or not explanation.strip() or len(explanation) > 600 or
                re.search(r"\b(?:learner|mastery|streak|progress|performance|difficulty)\b", explanation, re.I)):
            explanation = ("The correct answer is " + feedback["correct_option"] + ": " +
                           question["options"][feedback["correct_option"]] + ".")
            explanation_source = "fallback"
        else:
            explanation = explanation.strip()
            explanation_source = "gemini"
        action = ai.get("recommended_action")
        if action not in ("increase", "maintain", "decrease"):
            action = None
        tiers = ("easy", "medium", "hard")
        current_tier = question["difficulty_level"].lower()
        recommended_tier = tiers[max(0, min(2, tiers.index(current_tier) +
                                           {"increase": 1, "maintain": 0, "decrease": -1}.get(action, 0)))] if action else None
        asked = [*state["asked_ids"], question_id]
        preferred = recommended_tier if (os.environ.get("AI_ADAPTATION_ENABLED", "false").lower() == "true"
                                         and len(history) >= 5) else None
        baseline = next_question(feedback["updated_mastery"], asked, **pinned)
        following = next_question(feedback["updated_mastery"], asked, preferred, **pinned) if preferred else baseline
        difficulty_source = ("none" if following is None else "gemini" if preferred and
                             following["difficulty_level"].lower() == preferred else "mastery")
        result = {
            **feedback,
            "session_id": session_id,
            "answered_count": len(asked),
            "completed": following is None,
            "next_question": following,
            "explanation": explanation,
            "explanation_source": explanation_source,
            "recommended_action": action,
            "recommended_difficulty": recommended_tier,
            "selected_difficulty": following["difficulty_level"].lower() if following else None,
            "difficulty_source": difficulty_source,
        }
        history[-1]["accepted_at"] = self.clock()
        updated = {
            **state,
            "mastery": feedback["updated_mastery"],
            "asked_ids": asked,
            "current_question_id": following["question_id"] if following else None,
            "issued_at": self.clock() if following else None,
            "completed": following is None,
            "last_attempt_id": attempt_id,
            "last_choice": choice,
            "last_result": result,
            "history": history,
            "revision": state["revision"] + 1,
        }
        if self.store.replace(learner_id, updated, state["revision"]):
            return result

        # An HTTP retry can race the first request. Replay its saved response.
        latest = self.store.get(learner_id)
        if latest and latest["last_attempt_id"] == attempt_id:
            if latest["last_result"]["question_id"] != question_id or latest.get("last_choice") != choice:
                raise SessionConflict("attempt_id belongs to a different answer")
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
            "question": question_by_id(question_id, **QuizService._pinned(state)) if question_id else None,
        }
