import json
import os
import unittest
from unittest.mock import patch

import handler


class FakeService:
    def __init__(self):
        self.calls = []

    def start(self, learner_id):
        self.calls.append(("start", learner_id))
        return {"session_id": "session-1"}

    def current(self, learner_id):
        self.calls.append(("current", learner_id))
        return {"session_id": "session-1"}

    def answer(self, *args):
        self.calls.append(("answer", *args))
        return {"correct": True}


def event(method, path, body=None, authenticated=True):
    context = {"http": {"method": method}}
    if authenticated:
        context["authorizer"] = {"jwt": {"claims": {"sub": "learner-1"}}}
    return {
        "version": "2.0",
        "rawPath": path,
        "requestContext": context,
        "body": json.dumps(body) if body is not None else None,
    }


class HandlerTests(unittest.TestCase):
    def test_requires_verified_identity(self):
        with patch.dict(os.environ, {"QUIZ_DEMO_TOKEN": ""}), patch.object(handler, "_service") as service:
            result = handler.lambda_handler(event("POST", "/v1/sessions", authenticated=False), None)
        self.assertEqual(result["statusCode"], 401)
        service.assert_not_called()

    def test_single_learner_demo_key(self):
        service = FakeService()
        request = event("POST", "/v1/sessions", authenticated=False)
        request["headers"] = {"Authorization": "Bearer demo-secret"}
        with patch.dict(os.environ, {"QUIZ_DEMO_TOKEN": "demo-secret"}), \
             patch.object(handler, "_service", return_value=service):
            response = handler.lambda_handler(request, None)
        self.assertEqual(response["statusCode"], 200)
        self.assertEqual(service.calls, [("start", "osteon-demo-learner")])

    def test_routes_to_quiz_service(self):
        service = FakeService()
        with patch.object(handler, "_service", return_value=service):
            started = handler.lambda_handler(event("POST", "/v1/sessions"), None)
            current = handler.lambda_handler(event("GET", "/v1/sessions/current"), None)
            answered = handler.lambda_handler(event(
                "POST", "/v1/sessions/session-1/answers",
                {"question_id": "rib_04", "choice": "B", "attempt_id": "attempt-1"},
            ), None)
        self.assertEqual([started["statusCode"], current["statusCode"], answered["statusCode"]],
                         [200, 200, 200])
        self.assertEqual(service.calls[-1],
                         ("answer", "learner-1", "session-1", "rib_04", "B", "attempt-1"))

    def test_rejects_bad_answer_body(self):
        service = FakeService()
        with patch.object(handler, "_service", return_value=service):
            response = handler.lambda_handler(event(
                "POST", "/v1/sessions/session-1/answers", {"choice": "B"},
            ), None)
        self.assertEqual(response["statusCode"], 400)
        self.assertEqual(service.calls, [])


if __name__ == "__main__":
    unittest.main()
