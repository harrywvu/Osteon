"""Resolve an opaque account session at the fixed accounts service origin."""

import json
import os
import urllib.error
import urllib.request
from urllib.parse import urlparse


class VerifierUnavailable(Exception):
    pass


class InvalidCredential(Exception):
    pass


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        return None


def verify(token):
    origin = os.environ.get("ACCOUNTS_API_ORIGIN", "")
    parsed = urlparse(origin)
    if parsed.scheme != "https" or not parsed.netloc or parsed.path not in ("", "/") or parsed.query or parsed.fragment or parsed.username:
        raise VerifierUnavailable()
    if not token or any(char.isspace() for char in token):
        raise InvalidCredential()
    request = urllib.request.Request(origin.rstrip("/") + "/v1/auth/me",
                                     headers={"Authorization": "Bearer " + token})
    try:
        with urllib.request.build_opener(NoRedirect).open(request, timeout=3) as response:
            if response.status != 200:
                raise VerifierUnavailable()
            data = json.loads(response.read(4097))
    except urllib.error.HTTPError as error:
        if error.code == 401:
            raise InvalidCredential() from None
        raise VerifierUnavailable() from error
    except (OSError, ValueError) as error:
        raise VerifierUnavailable() from error
    if not isinstance(data, dict) or not isinstance(data.get("learner_id"), str) or not data["learner_id"] or len(data["learner_id"]) > 128:
        raise VerifierUnavailable()
    return data["learner_id"]
