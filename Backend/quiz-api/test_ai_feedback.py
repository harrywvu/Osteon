import json
import unittest
from unittest.mock import patch

import ai_feedback


class Socket:
    def settimeout(self, value):
        assert value > 0


class Response:
    status = 200
    def __init__(self):
        self.chunks = [json.dumps({"candidates": [{"content": {"parts": [{"text":
            json.dumps({"explanation": "A bone answer.", "recommended_action": "maintain", "assessment": "mixed"})
        }]}}]}).encode(), b""]
    def read(self, amount):
        return self.chunks.pop(0)


class Connection:
    calls = []
    def __init__(self, host, timeout):
        self.sock = Socket()
    def connect(self):
        pass
    def request(self, method, path, body, headers):
        self.calls.append((method, path, body, headers))
    def getresponse(self):
        return Response()
    def close(self):
        pass


class AIFeedbackTests(unittest.TestCase):
    def test_one_structured_request_omits_identity(self):
        Connection.calls.clear()
        with patch.dict("os.environ", {"AI_ENABLED": "true"}), \
             patch.object(ai_feedback, "_provider_key", return_value="secret"), \
             patch.object(ai_feedback.http.client, "HTTPSConnection", Connection):
            result = ai_feedback.generate({"question_id": "q1", "options": {"A": "Bone"}},
                                          {"correct": True, "correct_option": "A", "choice": "A"}, [])
        self.assertEqual(result["recommended_action"], "maintain")
        self.assertEqual(len(Connection.calls), 1)
        self.assertEqual(Connection.calls[0][1], "/v1beta/models/gemini-3.5-flash-lite:generateContent")
        body = json.loads(Connection.calls[0][2])
        self.assertEqual(body["generationConfig"]["responseMimeType"], "application/json")
        self.assertNotIn("learner_id", json.dumps(body))
        self.assertIn('"correct_answer":"Bone"', body["contents"][0]["parts"][0]["text"])

    def test_provider_timeout_returns_empty_feedback(self):
        class TimedOutConnection(Connection):
            def getresponse(self):
                raise TimeoutError("provider timed out")

        with patch.dict("os.environ", {"AI_ENABLED": "true"}), \
             patch.object(ai_feedback, "_provider_key", return_value="secret"), \
             patch.object(ai_feedback.http.client, "HTTPSConnection", TimedOutConnection):
            result = ai_feedback.generate({"options": {"A": "Bone"}},
                                          {"correct": True, "correct_option": "A", "choice": "A"}, [])
        self.assertEqual(result, {})


if __name__ == "__main__":
    unittest.main()
