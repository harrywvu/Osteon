"""Reject educator self-sign-up outside the MMSU email domain."""


def lambda_handler(event, _context):
    if event.get("triggerSource") == "PreSignUp_SignUp":
        email = event.get("request", {}).get("userAttributes", {}).get("email", "")
        local, separator, domain = email.rpartition("@")
        if not local or not separator or domain.lower() != "mmsu.edu.ph":
            raise ValueError("Use an @mmsu.edu.ph email address to sign up.")
    return event
