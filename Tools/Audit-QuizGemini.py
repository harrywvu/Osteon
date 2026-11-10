"""Review Gemini quiz output without creating accounts or changing learner progress.

Run `python Tools/Audit-QuizGemini.py --full` for 100 question/answer cases plus
12 recommendation cases. The API key is entered at a hidden prompt and is never
written to the report. This calls Gemini directly, not the deployed quiz Lambda.
"""

import argparse
import csv
import getpass
import json
import logging
import os
import re
import sys
import time
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Backend" / "quiz-api"))
import ai_feedback  # noqa: E402


def questions():
    path = ROOT / "Backend" / "quiz-api" / "assets" / "questions_datasets.csv"
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def question_case(row, choice):
    correct = row["correct_option"]
    return {
        "question_id": row["question_id"],
        "question": {
            "question_id": row["question_id"],
            "bone_group": row["bone_group"],
            "bone_name": row["bone_name"],
            "difficulty_level": row["difficulty_level"],
            "question_text": row["question_text"],
            "options": {letter: row["option_" + letter.lower()] for letter in "ABCD"},
        },
        "result": {
            "choice": choice,
            "correct": choice == correct,
            "correct_option": correct,
            "raw_model_delta": 0.05 if choice == correct else -0.05,
            "applied_mastery_change": 0.05 if choice == correct else -0.05,
            "updated_mastery": 0.55 if choice == correct else 0.45,
        },
        "history": [],
    }


def recommendation_cases(row, repeats):
    patterns = {
        "sparse": [],
        "weak": [False, False, True, False, False],
        "improving": [False, True, True, True, True],
        "mixed": [True, False, True, False, True],
    }
    for name, outcomes in patterns.items():
        for repeat in range(repeats):
            case = question_case(row, "B")
            case["kind"] = "recommendation"
            case["scenario"] = name
            case["repeat"] = repeat + 1
            mastery = {"sparse": 0.5, "weak": 0.55, "improving": 0.35,
                       "mixed": 0.5}[name]
            case["history"] = []
            for index, correct in enumerate(outcomes):
                updated = max(0.0, min(1.0, mastery + (0.05 if correct else -0.08)))
                case["history"].append({
                    "question_id": f"prior_{index}", "bone_group": "Axial",
                    "difficulty": "Medium", "choice": "B", "correct": correct,
                    "previous_mastery": round(mastery, 2),
                    "updated_mastery": round(updated, 2),
                })
                mastery = updated
            case["result"]["updated_mastery"] = round(min(1.0, mastery + 0.05), 2)
            yield case


def audit_case(case):
    class CaptureFallback(logging.Handler):
        def __init__(self):
            super().__init__()
            self.reason = None

        def emit(self, record):
            message = record.getMessage()
            if message.startswith("AI feedback fallback: "):
                self.reason = message.removeprefix("AI feedback fallback: ")

    capture = CaptureFallback()
    logger = logging.getLogger(ai_feedback.__name__)
    logger.addHandler(capture)
    start = time.monotonic()
    try:
        response = ai_feedback.generate(case["question"], case["result"], case["history"])
    finally:
        logger.removeHandler(capture)
    elapsed_ms = round((time.monotonic() - start) * 1000)
    explanation = response.get("explanation", "") if isinstance(response, dict) else ""
    action = response.get("recommended_action") if isinstance(response, dict) else None
    assessment = response.get("assessment") if isinstance(response, dict) else None
    correct_text = case["question"]["options"][case["result"]["correct_option"]]
    return {
        "kind": case.get("kind", "explanation"),
        "question_id": case["question_id"],
        "question_text": case["question"]["question_text"],
        "choice": case["result"]["choice"],
        "correct_answer": correct_text,
        "scenario": case.get("scenario"),
        "repeat": case.get("repeat"),
        "history_count": len(case["history"]),
        "updated_mastery": case["result"]["updated_mastery"],
        "elapsed_ms": elapsed_ms,
        "provider_error": capture.reason,
        "explanation": explanation,
        "recommended_action": action,
        "assessment": assessment,
        "needs_review": (
            not explanation or len(explanation) > 600 or
            bool(re.search(r"\b(?:learner|mastery|streak|progress|performance|difficulty)\b",
                           explanation, re.IGNORECASE)) or
            correct_text.casefold() not in explanation.casefold() or
            action not in ("increase", "maintain", "decrease")
        ),
    }


def case_key(case):
    return (case.get("kind", "explanation"), case["question_id"],
            case.get("choice", case.get("result", {}).get("choice")),
            case.get("scenario"), case.get("repeat"))


def pending_cases(cases, existing, recommendations_only=False):
    return [case for case in cases
            if (not recommendations_only or case.get("kind") == "recommendation")
            and not existing.get(case_key(case), {}).get("explanation")]


def save_report(output, results, total_cases):
    report = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "model": os.environ.get("GEMINI_MODEL", "gemini-3.5-flash-lite"),
        "total_cases": total_cases,
        "direct_provider_requests": sum(result.get("attempts", 1) for result in results),
        "successful_cases": sum(bool(result["explanation"]) for result in results),
        "needs_review_count": sum(result["needs_review"] for result in results),
        "recommendation_counts": dict(Counter(result["recommended_action"] for result in results if result["kind"] == "recommendation")),
        "recommendation_by_scenario": {
            name: [result["recommended_action"] for result in results
                   if result["scenario"] == name]
            for name in ("sparse", "weak", "improving", "mixed")
        },
        "results": results,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".tmp")
    temporary.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    temporary.replace(output)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--full", action="store_true", help="Audit all 50 questions, both answer outcomes, plus 12 recommendation runs")
    parser.add_argument("--recommendations-only", action="store_true", help="Run only the 12 recommendation cases; requires --full")
    parser.add_argument("--resume", action="store_true", help="Keep successful cases and retry only missing responses")
    parser.add_argument("--delay", type=float, default=4.0, help="Seconds between provider requests (default: 4)")
    parser.add_argument("--stop-after-failures", type=int, default=3, help="Stop after this many consecutive empty responses (default: 3)")
    parser.add_argument("--output", type=Path, default=ROOT / "Backend" / "quiz-api" / "dist" / "gemini-audit.json")
    args = parser.parse_args()
    if args.delay < 0 or args.stop_after_failures < 1:
        parser.error("delay must be nonnegative and stop-after-failures must be positive")
    if args.recommendations_only and not args.full:
        parser.error("--recommendations-only requires --full")
    rows = questions()
    if len(rows) != 50:
        parser.error(f"expected 50 questions, found {len(rows)}")
    if args.full:
        cases = list(recommendation_cases(
            next(row for row in rows if row["question_id"] == "rib_04"), 3))
        for row in rows:
            correct = row["correct_option"]
            wrong = next(letter for letter in "ABCD" if letter != correct)
            cases.extend((question_case(row, correct), question_case(row, wrong)))
    else:
        cases = [question_case(rows[0], rows[0]["correct_option"]),
                 question_case(rows[0], next(letter for letter in "ABCD" if letter != rows[0]["correct_option"]))]
    if args.resume:
        if not args.output.exists():
            parser.error("no report exists to resume")
        previous = json.loads(args.output.read_text(encoding="utf-8"))
        if previous.get("model") != os.environ.get("GEMINI_MODEL", "gemini-3.5-flash-lite"):
            parser.error("the existing report uses a different model")
        existing = {case_key(result): result for result in previous["results"]}
    else:
        if args.output.exists():
            parser.error("report already exists; use --resume or a different --output")
        existing = {}
    pending = pending_cases(cases, existing, args.recommendations_only)
    print(f"{len(pending)} cases need Gemini responses; report: {args.output.resolve()}")
    if not pending:
        return
    key = getpass.getpass("Gemini API key (hidden; not saved): ").strip()
    if not key:
        parser.error("an API key is required")
    failures = 0
    with patch.dict(os.environ, {"AI_ENABLED": "true"}), patch.object(ai_feedback, "_provider_key", return_value=key):
        for index, case in enumerate(pending, 1):
            if index > 1:
                time.sleep(args.delay)
            result = audit_case(case)
            old = existing.get(case_key(case), {})
            result["attempts"] = old.get("attempts", 1) + 1 if old else 1
            existing[case_key(case)] = result
            results = [existing[case_key(item)] for item in cases if case_key(item) in existing]
            report = save_report(args.output, results, len(cases))
            print(f"{index}/{len(pending)} {case.get('scenario') or case['question_id']} {result['elapsed_ms']} ms; "
                  f"{report['successful_cases']}/{len(cases)} usable")
            failures = 0 if result["explanation"] else failures + 1
            if failures >= args.stop_after_failures:
                print("Stopped after consecutive provider failures; resume after the limit or outage clears.")
                break
    print(f"Saved {args.output.resolve()}")
    print("Flags are screening hints; a person must still check anatomy accuracy and recommendation quality.")


if __name__ == "__main__":
    main()
