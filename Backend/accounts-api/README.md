# Persistent account Lambda

PLAN-008 local source now adds optional named fields to creation, authenticated
profile completion/correction, and the MMSU program catalog. This code has not
been deployed. The catalog still needs the official-offerings and year-range
review in [Docs/MMSU_PROGRAM_CATALOG.md](../../Docs/MMSU_PROGRAM_CATALOG.md).

Python 3.12 / Linux x86-64 account service for API Gateway HTTP API payload v2.
Prepared on 2026-11-03 for the manually provisioned Sydney resources. Local tests
use Moto; deployment and live AWS acceptance must be recorded separately.

## Configured resources

- DynamoDB `osteon-accounts`: String partition key `pk`, no sort key, TTL `expires_at`.
- Secrets Manager `osteon/accounts/keys`: JSON fields `code_lookup_key` and
  `replay_encryption_key`, each a 64-character hex string generated from 32 random bytes.
- Lambda `osteon-accounts`: Python 3.12, x86-64, 256 MB, timeout 15 seconds.
- Handler `lambda_function.lambda_handler`.
- Environment `ACCOUNTS_TABLE_NAME=osteon-accounts` and
  `ACCOUNTS_SECRET_ID=osteon/accounts/keys`.
- Role `osteon-accounts-role`: basic CloudWatch logging, GetItem/PutItem/UpdateItem/
  DeleteItem/ConditionCheckItem on the exact account table ARN and GetSecretValue
  on the exact secret ARN. DynamoDB transactions use the underlying item permissions.

No quiz or AI dependencies are included. Clients must never access this table or
secret directly. The Unity client now uses `HttpLearnerAccounts` by default;
the in-memory simulator is an explicit development-only option. See
[`Docs/USER_ACCOUNTS.md`](../../Docs/USER_ACCOUNTS.md) for the client flow and
verification record.

## Test and package

From the repository root, in PowerShell:

```powershell
python -m pip install -r Backend/accounts-api/requirements-test.txt --target Backend/accounts-api/.test-deps
$env:PYTHONPATH = (Resolve-Path Backend/accounts-api/.test-deps).Path
python -m unittest discover -s Backend/accounts-api -p 'test_*.py' -v
./Backend/accounts-api/Build-LambdaZip.ps1
```

The build downloads Linux wheels for Python 3.12, not Windows bindings. It produces
`dist/osteon-accounts-lambda.zip` with the handler and pinned dependencies at its
root. Generated packages and local test dependencies are ignored by Git. Moto is
not included in the Lambda package.

## Upload and first check

1. In the Sydney Lambda console open `osteon-accounts`.
2. Under Code, select Upload from > .zip file, choose the generated ZIP, and Save.
3. Confirm Runtime settings > Handler is `lambda_function.lambda_handler`.
4. Create a **private** test event named `accounts-connectivity`, using
   `events/connectivity.json`, and run Test.
5. Expect HTTP statusCode 200 with body containing
   `{"database":"reachable","secret":"valid"}`.

This diagnostic loads and validates both secret keys and performs a GetItem on a
nonexistent diagnostic key. It does not create an account, modify records, expose
secret values, or prove write permissions. It works only with a direct Lambda
invocation; no public diagnostic route is provided. On 503, inspect CloudWatch for
the AWS operation and error code, which are logged without request data or secrets.

## Account API

| Method / route | Input | Result |
|---|---|---|
| POST /v1/auth/profiles | JSON request_id, a fresh UUID | learner_id, profile_label, access_code, access_token, expires_in |
| POST /v1/auth/login | JSON access_code, six ASCII digits | learner_id, profile_label, access_token, expires_in |
| GET /v1/auth/me | Authorization: Bearer token | learner_id, profile_label, remaining expires_in |
| POST /v1/auth/logout | Authorization: Bearer token | signed_out: true; idempotent for this token |
| GET /v1/catalog/programs | No token | Program IDs, display names, colleges, and configured year options |
| PUT /v1/auth/me/profile | Bearer token and first_name, middle_name, last_name, program_id, year_level | Updated own profile; learner ID comes from the token |

New client creation sends the five profile fields. The backend still accepts
the old request shape so existing installations can create an incomplete
profile until updated. `GET /v1/auth/me` and login return `profile_complete`
plus named fields, with missing legacy values represented as null. Names are
trimmed Unicode text, bounded to 80 characters, and not identity keys.

POST creation/login require application/json and the trusted API Gateway
requestContext.http.sourceIp. Explicit route mapping uses event.rawPath and
requestContext.http.method. No identity value in the request body is trusted.
All responses are JSON with Cache-Control: no-store. Invalid input returns 400;
invalid codes, inactive profiles, and expired sessions return generic 401;
throttling returns 429 with retry_after_seconds; unavailable resources return 503.

When testing creation directly in Lambda, generate a new UUID for your private
test event. Never publish a reusable request_id in an example or source file: it
authorizes replay of the creation result for 24 hours. Save the returned code and
token privately. Do not share test responses or paste them into chat.

Example event shape (replace the request_id placeholder before running):

```json
{
  "version": "2.0",
  "rawPath": "/v1/auth/profiles",
  "headers": {"content-type": "application/json"},
  "requestContext": {
    "http": {"method": "POST", "sourceIp": "203.0.113.10"}
  },
  "body": "{\"request_id\":\"GENERATE-A-NEW-UUID\"}"
}
```

The source IP is a documentation test address; actual API Gateway calls supply
their real source IP. Never derive rate-limit identity from caller-controlled
headers such as X-Forwarded-For. Use private test events because creation and
login responses contain account credentials.

## Storage and behavior

- PROFILE records store stable random identity, label, creation time, and status.
- CODE records map keyed HMAC digests to profiles; codes are never stored plainly
  in these records and cannot be recovered through scanning the table.
- SESSION records use SHA-256 digests of random 256-bit tokens, expire after one
  hour, and are checked for expiry before use even if DynamoDB TTL has not deleted
  them. Logout deletes only the presented session.
- CREATE records retain the issued result encrypted with AES-256-GCM for 24 hours,
  with a fresh nonce and request-specific authenticated data. A transaction reserves
  code, profile, session, and replay record together. Code collisions retry internally;
  duplicate creation requests replay the same result. A revoked/expired original
  session is not reactivated by replay; expires_in is zero and the saved code must
  be used for a new login.
- RATE records atomically count all requests across Lambda workers. Creation allows
  five per source IP per minute and 100 globally per hour. Login allows ten per IP
  per minute and 100 globally per minute. Fixed windows return their remaining
  duration on throttling. Successful logins count too. Rate records have cleanup TTL.
- Profiles and code reservations have no TTL. A code is not reassigned while its
  profile exists. Setting a PROFILE status to disabled rejects both code login and
  existing sessions. No account recovery or replacement-code endpoint exists.

Secrets are cached per warm Lambda worker. Do not rotate either protection key
without a migration design: replacing the lookup key breaks existing code lookup,
and replacing the replay key breaks outstanding creation retries.

Before exposing API Gateway, configure explicit HTTP API routes, browser CORS,
and gateway request throttles to limit invocation volume. Keep request-body logging
off. Do not enable a public Function URL. This short-code account model remains
limited-protection anonymous access, not proof of real student identity.

## Verification record

On 2026-11-03, 14 tests passed against Moto using the real SDK transaction adapter
and AES-GCM implementation. Coverage includes persistent lookup across workers,
code collisions and transaction rollback, idempotent creation, sessions on multiple
devices, disabled profiles, TTL-independent expiry, shared rate counters, encrypted
replay integrity, HTTP validation, and safe diagnostics.

On 2026-11-03, the user reported a successful direct invocation of the deployed
Sydney Lambda connectivity diagnostic: statusCode 200, database reachable,
secret valid. AWS's reported code SHA-256 matches the locally generated ZIP.
Duration was 1,580.27 ms with 800.73 ms initialization and 95 MB maximum memory
used out of 256 MB. This verifies deployment and read access, not account writes.
Live creation/login/logout, concurrent production load, API Gateway, and
Quest/WebGL account integration remain pending.
