import io
import os
import unittest
import urllib.error
from unittest.mock import patch

import account_verifier


class Response:
    status = 200

    def __init__(self, payload):
        self.stream = io.BytesIO(payload)

    def __enter__(self):
        return self

    def __exit__(self, *_):
        return False

    def read(self, size):
        return self.stream.read(size)


class Opener:
    def __init__(self, response=None, error=None):
        self.response = response
        self.error = error
        self.request = None

    def open(self, request, timeout):
        self.request = request
        assert timeout == 3
        if self.error:
            raise self.error
        return self.response


class AccountVerifierTests(unittest.TestCase):
    origin = "https://accounts.example.test"

    def test_verified_identity_uses_fixed_origin_and_bearer_token(self):
        opener = Opener(Response(b'{"learner_id":"learner-7"}'))
        with patch.dict(os.environ, {"ACCOUNTS_API_ORIGIN": self.origin}), \
             patch.object(account_verifier.urllib.request, "build_opener", return_value=opener):
            learner = account_verifier.verify("short-lived-token")
        self.assertEqual(learner, "learner-7")
        self.assertEqual(opener.request.full_url, self.origin + "/v1/auth/me")
        self.assertEqual(opener.request.get_header("Authorization"), "Bearer short-lived-token")

    def test_revoked_token_and_service_outage_are_distinct(self):
        for status, expected in ((401, account_verifier.InvalidCredential),
                                 (503, account_verifier.VerifierUnavailable)):
            with self.subTest(status=status):
                error = urllib.error.HTTPError(self.origin, status, "failure", {}, None)
                with patch.dict(os.environ, {"ACCOUNTS_API_ORIGIN": self.origin}), \
                     patch.object(account_verifier.urllib.request, "build_opener",
                                  return_value=Opener(error=error)):
                    with self.assertRaises(expected):
                        account_verifier.verify("short-lived-token")

    def test_bad_origin_token_and_identity_fail_closed(self):
        for origin in ("http://accounts.example.test", "https://user@accounts.example.test",
                       "https://accounts.example.test/other"):
            with self.subTest(origin=origin), patch.dict(os.environ, {"ACCOUNTS_API_ORIGIN": origin}), \
                 patch.object(account_verifier.urllib.request, "build_opener") as opener:
                with self.assertRaises(account_verifier.VerifierUnavailable):
                    account_verifier.verify("token")
                opener.assert_not_called()
        with patch.dict(os.environ, {"ACCOUNTS_API_ORIGIN": self.origin}):
            with self.assertRaises(account_verifier.InvalidCredential):
                account_verifier.verify("two words")
        for payload in (b'{}', b'{"learner_id":""}', b'{"learner_id":7}', b'not-json'):
            with self.subTest(payload=payload), patch.dict(os.environ, {"ACCOUNTS_API_ORIGIN": self.origin}), \
                 patch.object(account_verifier.urllib.request, "build_opener",
                              return_value=Opener(Response(payload))):
                with self.assertRaises(account_verifier.VerifierUnavailable):
                    account_verifier.verify("token")


if __name__ == "__main__":
    unittest.main()
