import unittest

from signup_guard import lambda_handler


class SignupGuardTests(unittest.TestCase):
    def event(self, email, source="PreSignUp_SignUp"):
        return {"triggerSource": source, "request": {"userAttributes": {"email": email}}, "response": {}}

    def test_school_address_allowed_without_skipping_confirmation(self):
        event = self.event("professor@MMSU.EDU.PH")
        self.assertIs(event, lambda_handler(event, None))
        self.assertEqual({}, event["response"])

    def test_other_and_deceptive_domains_rejected(self):
        for email in ("person@gmail.com", "person@mmsu.edu.ph.example", "person@sub.mmsu.edu.ph", "@mmsu.edu.ph", "person@mmsu.edu.ph@evil.test", ""):
            with self.subTest(email=email), self.assertRaises(ValueError):
                lambda_handler(self.event(email), None)

    def test_existing_admin_creation_unaffected(self):
        event = self.event("existing@gmail.com", "PreSignUp_AdminCreateUser")
        self.assertIs(event, lambda_handler(event, None))
