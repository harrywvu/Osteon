"""One bounded Gemini request; caller owns deterministic fallback and scoring."""

import json
import logging
import os
import http.client
import time
from functools import lru_cache


SCHEMA = {"type": "object", "properties": {
    "explanation": {"type": "string", "description": "Learner-facing anatomy explanation of the correct answer; no progress assessment."},
    "recommended_action": {"type": "string", "enum": ["increase", "maintain", "decrease"]},
    "assessment": {"type": "string"},
}, "required": ["explanation", "recommended_action", "assessment"]}


@lru_cache(maxsize=1)
def _provider_key():
    arn = os.environ.get("GEMINI_KEY_SECRET_ARN", "")
    if not arn:
        return ""
    import boto3
    from botocore.config import Config
    return boto3.client("secretsmanager", config=Config(
        connect_timeout=2, read_timeout=2, retries={"total_max_attempts": 1}
    )).get_secret_value(SecretId=arn)["SecretString"]


def generate(question, result, history):
    if os.environ.get("AI_ENABLED", "false").lower() != "true":
        return {}
    try:
        key = _provider_key()
    except Exception:
        return {}
    model = os.environ.get("GEMINI_MODEL", "gemini-3.5-flash-lite")
    if not key or not model.replace("-", "").replace(".", "").isalnum():
        return {}
    context = {"question": question,
               "correct_answer": question["options"][result["correct_option"]],
               "selected_answer": question["options"][result["choice"]],
               "result": result, "recent_attempts": history[-10:]}
    prompt = ("The explanation field is learner-facing anatomy feedback. Name the correct answer and explain why it is correct "
              "in two or three short plain sentences. If the selected answer is wrong, briefly explain the distinction. "
              "Do not discuss the learner, mastery, history, streaks, progress, difficulty, or performance in the explanation. "
              "Put progress analysis only in assessment. The supplied correctness and mastery are authoritative. "
              "Do not invent learner history. A positive model delta alone does not require harder questions. "
              "Recommend increase, maintain, or decrease relative to the answered question. "
              "History contains only recent attempts; an empty list is insufficient historical evidence.\n"
              + json.dumps(context, separators=(",", ":")))
    payload = {"contents": [{"parts": [{"text": prompt}]}], "generationConfig": {
        "responseMimeType": "application/json", "responseJsonSchema": SCHEMA, "maxOutputTokens": 320}}
    connection = http.client.HTTPSConnection("generativelanguage.googleapis.com", timeout=5)
    started = time.monotonic()
    deadline = started + 5
    logger = logging.getLogger(__name__)
    try:
        connection.connect()
        connection.sock.settimeout(max(0.001, deadline - time.monotonic()))
        connection.request("POST", "/v1beta/models/" + model + ":generateContent",
                           body=json.dumps(payload).encode(),
                           headers={"Content-Type": "application/json", "x-goog-api-key": key})
        connection.sock.settimeout(max(0.001, deadline - time.monotonic()))
        response = connection.getresponse()
        if response.status != 200:
            logger.warning("AI feedback fallback: provider_http_%s", response.status)
            return {}
        response_socket = connection.sock or response.fp.raw._sock
        chunks = []
        size = 0
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError()
            response_socket.settimeout(remaining)
            chunk = response.read(min(4096, 16385 - size))
            if not chunk:
                break
            chunks.append(chunk)
            size += len(chunk)
            if size > 16384:
                raise ValueError("oversized response")
        raw = b"".join(chunks)
        reply = json.loads(raw)
        logger.info("AI feedback duration_ms=%d total_tokens=%s", int((time.monotonic() - started) * 1000),
                    (reply.get("usageMetadata") or {}).get("totalTokenCount", "unknown"))
        content = reply["candidates"][0]["content"]["parts"][0]["text"]
        value = json.loads(content)
        return value if isinstance(value, dict) else {}
    except (OSError, ValueError, KeyError, IndexError, TypeError, AttributeError) as error:
        logger.warning("AI feedback fallback: %s", type(error).__name__)
        return {}
    finally:
        connection.close()
