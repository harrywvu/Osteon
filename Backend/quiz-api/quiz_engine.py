"""Platform-independent question selection and mastery scoring for Osteon.

The HTTP service will own learner state. This module only evaluates the state
passed to it, so the Quest and web clients can share the same quiz rules.
"""

import csv
import hashlib
import math
from functools import lru_cache
from pathlib import Path

import numpy as np


ASSETS = Path(__file__).resolve().parent / "assets"
QUESTIONS_PATH = ASSETS / "questions_datasets.csv"
MODEL_PATH = ASSETS / "random_forest_mastery_model.onnx"


@lru_cache(maxsize=1)
def content_version():
    """Identify the exact question bank and model used by a session."""
    digest = hashlib.sha256()
    for path in (QUESTIONS_PATH, MODEL_PATH):
        digest.update(path.read_bytes())
    return digest.hexdigest()[:16]


@lru_cache(maxsize=1)
def _questions():
    with QUESTIONS_PATH.open(newline="", encoding="utf-8-sig") as file:
        return tuple(csv.DictReader(file))


@lru_cache(maxsize=1)
def _model_session():
    import onnxruntime as rt

    return rt.InferenceSession(str(MODEL_PATH), providers=["CPUExecutionProvider"])


def _validated_mastery(mastery):
    value = float(mastery)
    if not math.isfinite(value) or not 0.0 <= value <= 1.0:
        raise ValueError("mastery must be a finite number between 0 and 1")
    return value


def next_question(mastery, asked_question_ids):
    """Return the first unasked question in the current difficulty tier.

    Return None when the bank is exhausted. The caller must record the returned
    question ID as asked; this function does not change learner state.
    """
    mastery = _validated_mastery(mastery)
    if mastery < 0.50:
        target = "easy"
    elif mastery <= 0.75:
        target = "medium"
    else:
        target = "hard"

    asked = set(asked_question_ids)
    available = [q for q in _questions() if q["question_id"] not in asked]
    if not available:
        return None
    question = next(
        (q for q in available if q["difficulty_level"].lower() == target),
        available[0],
    )
    return question_by_id(question["question_id"])


def question_by_id(question_id):
    """Return a question for display without exposing its answer key."""
    question = next(
        (q for q in _questions() if q["question_id"] == question_id), None
    )
    if question is None:
        raise ValueError(f"unknown question_id: {question_id}")
    return {
        "question_id": question["question_id"],
        "bone_group": question["bone_group"],
        "bone_name": question["bone_name"],
        "difficulty_level": question["difficulty_level"],
        "question_text": question["question_text"],
        "options": {
            letter: question[f"option_{letter.lower()}"] for letter in "ABCD"
        },
    }


def score_answer(question_id, choice, response_seconds, mastery):
    """Score one answer using the teammate's ONNX feature order and guardrails.

    The HTTP service must supply mastery and response time from trusted session
    state; neither value should be accepted as authoritative from a client.
    """
    mastery = _validated_mastery(mastery)
    seconds = float(response_seconds)
    if not math.isfinite(seconds) or seconds < 0:
        raise ValueError("response_seconds must be a finite, nonnegative number")
    if not isinstance(choice, str) or choice.strip().upper() not in ("A", "B", "C", "D"):
        raise ValueError("choice must be A, B, C, or D")
    choice = choice.strip().upper()

    question = next(
        (q for q in _questions() if q["question_id"] == question_id), None
    )
    if question is None:
        raise ValueError(f"unknown question_id: {question_id}")

    correct_option = question["correct_option"].strip().upper()
    correct = choice == correct_option
    difficulty = question["difficulty_level"].strip().lower()
    difficulty_code = {"easy": 0.0, "medium": 1.0, "hard": 2.0}[difficulty]
    features = np.array(
        [[
            float(question["difficulty_score"]),
            seconds,
            1.0 if correct else 0.0,
            mastery,
            1.0 if question["bone_group"].strip().lower() == "appendicular" else 0.0,
            difficulty_code,
        ]],
        dtype=np.float32,
    )

    session = _model_session()
    input_name = session.get_inputs()[0].name
    output_name = session.get_outputs()[0].name
    prediction = session.run([output_name], {input_name: features})[0]
    predicted_delta = float(prediction[0][0])
    if not math.isfinite(predicted_delta):
        raise ValueError("model returned a non-finite mastery change")

    if not correct and predicted_delta >= 0:
        predicted_delta = {"easy": -0.12, "medium": -0.08, "hard": -0.04}[difficulty]
    elif correct and predicted_delta <= 0:
        predicted_delta = 0.05

    updated_mastery = min(1.0, max(0.0, mastery + predicted_delta))
    return {
        "question_id": question_id,
        "correct": correct,
        "correct_option": correct_option,
        "previous_mastery": mastery,
        "updated_mastery": updated_mastery,
        "mastery_change": updated_mastery - mastery,
    }
