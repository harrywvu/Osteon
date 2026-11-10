# Osteon quiz API

PLAN-008 local source adds an optional DynamoDB question-bank adapter. When
`QUESTION_BANK_TABLE_NAME` is set, new runs pin the active immutable bank
version; active runs continue with their pinned version. Legacy active runs
require `LEGACY_CONTENT_VERSION` and `LEGACY_BANK_VERSION` from importing the
actual deployed ZIP. `model_version` is recorded separately on new sessions.
The bank adapter and Lambda package are not deployed; see
[educator portal rollout](../educator-portal/README.md).

This directory contains the server-side practice quiz for Quest and WebGL.
The notebook is a training tool; the deployed
service uses the approved question CSV and exported ONNX model in `assets/`.

## Account-aware Quest and WebGL presentation

The Unity client reads the public quiz URL from `Assets/Resources/QuizServiceConfig.json`
and uses the current in-memory account bearer token. Set `ACCOUNTS_API_ORIGIN`
on the quiz Lambda to the fixed HTTPS account service origin. Every quiz
request calls `/v1/auth/me`; invalid sessions return 401 and verifier outages
return 503. Remove `QUIZ_DEMO_TOKEN` from the Lambda environment.

`AI_ENABLED` enables explanations. `AI_ADAPTATION_ENABLED` enables bounded
Gemini-guided difficulty selection. Both default off in source. Set `GEMINI_KEY_SECRET_ARN`
to a separate Secrets Manager secret containing only the Gemini key, and grant
the quiz Lambda `secretsmanager:GetSecretValue` for that ARN. `GEMINI_MODEL`
defaults to `gemini-3.5-flash-lite`.
The owner reports setting `AI_ADAPTATION_ENABLED=true` in the live Lambda on
2026-11-06. Its recommendation quality and post-change live behavior remain
unverified; see the audit before treating it as accepted.
See [the AI pipeline diagram](../../Docs/QUIZ_AI_PIPELINE.md) for the request
path and [the content audit](../../Docs/QUIZ_CONTENT_AUDIT_2026-11-06.md) for
current validation and the undeployed wording update.

Run local backend tests with `python -m unittest discover -s Backend/quiz-api
-p 'test_*.py' -q` from the repository root. For a direct Gemini content
review, run `python Tools/Audit-QuizGemini.py --full`. This prompts for the key
without echoing it, makes 112 provider requests, and writes a review report
without account tokens or learner data. It does not exercise Lambda latency or
the Quest/WebGL interfaces.
If a provider limit or outage interrupts the audit, run
`python Tools/Audit-QuizGemini.py --full --resume` after it clears. This keeps
successful cases and stops after three consecutive empty responses.
To review difficulty advice first, add `--recommendations-only`; that targets
the 12 synthetic learner-history cases and preserves explanation results.
The previous demo instructions below describe the old deployment.

## Historical one-learner Quest presentation (superseded)

The instructions in this section describe the former demo-token deployment.
Do not use them to configure the current account-aware client or Lambda.

The Unity scene creates a world-space quiz station on the room's right
wall. It stays in place when the learner turns their head, but can be moved
with its XR grab bar. **Start / Resume Practice** begins or resumes the saved quiz, **Stop**
returns to the menu, and **Hide** collapses it to a small **Practice Quiz**
launcher at that same location. Selecting the launcher reopens the menu. The
station uses this API to show feedback and mastery in the headset. Physical
Quest input is not yet verified. Before building an APK, copy
`Docs/QuizDemoConfig.example.json` to `Assets/Resources/QuizDemoConfig.json`,
then enter the deployed HTTPS API base URL and the same temporary demo token
set as `QUIZ_DEMO_TOKEN` in Lambda. The JSON file and its Unity `.meta` are
ignored by Git. Do not distribute this APK publicly: its token is embedded in
the build. The web version can use the same API contract but needs its own UI
and authentication before public release.

`Build-LambdaZip.ps1` packages the service and Linux x86-64 native wheels for
Python 3.12. It requires Python/pip and internet access to PyPI. The generated
file is `dist/osteon-quiz-lambda.zip`. The Lambda handler is
`handler.lambda_handler`. The Python 3.12/x86-64 ZIP has been exercised in
the Sydney Lambda function with successful start and answer requests, including
model inference. The current ZIP is about 62 MiB, so upload it to a private S3
bucket in the same Region as Lambda rather than using Lambda's 50 MB direct
upload. See [the quiz integration runbook](../../Docs/QUIZ_INTEGRATION.md) for
the deployed demo configuration and verification record.

## HTTP contract

The current service verifies bearer tokens through the accounts API on each
request and derives the learner ID from that response. The demo-token and JWT
authorizer statements below are historical alternatives, not the active
authentication path. Use [Quiz integration](../../Docs/QUIZ_INTEGRATION.md) for
the active deployment contract.

The Lambda entry point is `handler.lambda_handler`. It expects API Gateway HTTP
API payload format 2.0 and `Authorization: Bearer <account-session-token>` on
protected routes. The verifier calls the accounts API; the request body cannot
choose a learner ID. All responses are JSON with `Cache-Control: no-store`.

| Method and path | Purpose |
|---|---|
| `POST /v1/sessions` | Start or resume the authenticated learner's practice session. |
| `GET /v1/sessions/current` | Read that learner's current question and mastery. |
| `POST /v1/sessions/{session_id}/answers` | Score the current question and return feedback plus the next question. |

An answer body has `question_id`, `choice` (`A`–`D`), and `attempt_id` (a UUID
generated once by the client and reused for retries). The server derives
correctness, prior mastery, and elapsed time from its own data. A question
response never contains the answer key. Feedback includes the correct option
after submission. Repeating the latest `attempt_id` returns the saved result
without scoring twice; stale questions return HTTP 409.

## Storage contract

Set `QUIZ_TABLE_NAME` to a DynamoDB table with a String partition key named
`learner_id`. Each learner has one active session in a JSON `payload` attribute
and a Number `revision` attribute. Conditional writes prevent simultaneous
answers from overwriting each other. The session retains mastery across a new
practice run and restarts a run if the packaged model or question bank changes.

The Lambda execution role needs only `dynamodb:GetItem`, `dynamodb:PutItem`,
and `dynamodb:UpdateItem` on this table, plus normal CloudWatch Logs rights.
Configure `ACCOUNTS_API_ORIGIN` for the fixed account service origin and allow
only the intended browser origins in API Gateway CORS. Keep request authentication
in the Lambda verifier and verify its live deployment before a release. See
[Quiz integration](../../Docs/QUIZ_INTEGRATION.md) for the rollout record.

## Verification

Run `python -m unittest discover -s Backend/quiz-api -p 'test_*.py' -q` from the
repository root. The local suite uses fakes and an in-memory store; it does not
replace deployed API, WebGL, or headset acceptance. See the dated results and
remaining checks in [Quiz integration](../../Docs/QUIZ_INTEGRATION.md) and the
[content audit](../../Docs/QUIZ_CONTENT_AUDIT_2026-11-06.md).
