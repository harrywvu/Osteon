# Osteon quiz API

This directory contains the server-side practice quiz for both the Quest and
future web versions of Osteon. The notebook is a training tool; the deployed
service uses the approved question CSV and exported ONNX model in `assets/`.

## One-learner Quest presentation

The current Unity scene creates a fixed, world-space quiz station beside the
anatomy information panel. It stays in the scene when the learner turns their
head. **Start / Resume Practice** begins or resumes the saved quiz, **Stop**
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

The Lambda entry point is `handler.lambda_handler`. It expects API Gateway HTTP
API payload format 2.0. In the full release, a verified JWT authorizer supplies
the learner ID from the token's `sub` claim. For the one-learner presentation,
setting `QUIZ_DEMO_TOKEN` enables `Authorization: Bearer <token>` and stores all
progress under `osteon-demo-learner`. Never commit the token, and remove this
mode before a public release. All responses are JSON with `Cache-Control: no-store`.

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
The production API Gateway routes must require a JWT authorizer. For the
one-learner demo, the Lambda validates the temporary bearer token. The identity
provider for the Quest and web clients, API Gateway CORS origins, and AWS
deployment template still need to be chosen before a public release.

## Verification

Run `python -m unittest discover -s Backend/quiz-api -p 'test_*.py'` from the
repository root. These tests use a fake ONNX session and an in-memory store.
Twelve tests passed locally on 2026-09-30. A separate real-model inference
check and a deployed DynamoDB/API Gateway smoke test also passed. The Quest
headset UI and a clean Android build still need verification.
