# Osteon quiz API

The 2026-11-10 [deployment record](../educator-portal/README.md#deployment-record-and-remaining-work)
includes the DynamoDB question-bank adapter and `LEGACY_BANK_MAP`, which maps
earlier content hashes to imported bank versions. When
`QUESTION_BANK_TABLE_NAME` is set, new runs pin the active immutable bank
version and active runs keep their pinned version. New sessions record
`model_version` separately. Publication acceptance remains open.

The quiz runs server-side for Quest and WebGL. The package includes the
exported ONNX model and a fallback CSV in `assets/` for environments without
a configured question-bank table. The notebook is used for training.

## Configuration

The Unity client reads the public quiz URL from
`Assets/Resources/QuizServiceConfig.json` and sends the in-memory account bearer
token. Set `ACCOUNTS_API_ORIGIN` on the quiz Lambda to the fixed HTTPS account
origin. Every quiz request calls `/v1/auth/me`; invalid sessions return 401 and
verifier outages return 503. Remove `QUIZ_DEMO_TOKEN` from the environment.

`AI_ENABLED` enables explanations. `AI_ADAPTATION_ENABLED` enables bounded
Gemini-guided difficulty selection. Both default off. `GEMINI_KEY_SECRET_ARN`
must point to a separate Secrets Manager secret containing only the key; grant
the quiz Lambda `secretsmanager:GetSecretValue` on that ARN. `GEMINI_MODEL`
defaults to `gemini-3.5-flash-lite`. The owner reports enabling adaptation on
2026-11-06; recommendation quality is still unverified. See the
[AI pipeline](../../Docs/QUIZ_AI_PIPELINE.md) and
[content audit](../../Docs/QUIZ_CONTENT_AUDIT_2026-11-06.md).

Run local tests from the repository root:

```powershell
python -m unittest discover -s Backend/quiz-api -p 'test_*.py' -q
```

For a direct Gemini review, run `python Tools/Audit-QuizGemini.py --full`. It
prompts for the key without echoing it, makes 112 provider requests, and writes a
report without account tokens or learner data. Resume an interrupted run with
`--resume`; use `--recommendations-only` to target the 12 synthetic history
cases. It does not measure Lambda latency or exercise Quest/WebGL.

## HTTP contract

The Lambda entry point is `handler.lambda_handler`. It expects API Gateway HTTP
API payload format 2.0 and `Authorization: Bearer <account-session-token>` on
protected routes. The verifier calls the accounts API; the request body cannot
choose a learner ID. All responses are JSON with `Cache-Control: no-store`.

| Method and path | Purpose |
|---|---|
| `POST /v1/sessions` | Start or resume the authenticated learner's session |
| `GET /v1/sessions/current` | Read the current question and mastery |
| `POST /v1/sessions/{session_id}/answers` | Score the current question, return feedback and the next question |

An answer body has `question_id`, `choice` (`A`–`D`), and `attempt_id` (a UUID
generated once by the client and reused for retries). The server derives
correctness, prior mastery, and elapsed time. Responses never contain the answer
key before submission. Repeating the latest `attempt_id` returns the saved result
without scoring twice; stale questions return HTTP 409.

## Storage contract

Set `QUIZ_TABLE_NAME` to a DynamoDB table with a String partition key named
`learner_id`. Each learner has one active session in a JSON `payload` attribute
and a Number `revision` attribute. Conditional writes prevent simultaneous
answers from overwriting each other. Mastery persists across new runs; a run
restarts when the packaged model or question bank changes.

The execution role needs only `dynamodb:GetItem`, `dynamodb:PutItem`, and
`dynamodb:UpdateItem` on this table plus CloudWatch Logs. Configure
`ACCOUNTS_API_ORIGIN` and allow only the intended browser origins in API Gateway
CORS. Keep request authentication in the Lambda verifier and verify the live
deployment before release.

## Build and deploy

`Build-LambdaZip.ps1` packages the service and Linux x86-64 native wheels for
Python 3.12. It requires Python/pip and PyPI access and writes
`dist/osteon-quiz-lambda.zip` (about 62 MiB). The size exceeds Lambda's 50 MB
direct-upload limit, so upload the ZIP to a private S3 bucket in the same Region
and update Lambda from that object. Uploading to S3 alone does not update code.

The full deployment, environment, rollback, and acceptance procedures are in
[Quiz integration](../../Docs/QUIZ_INTEGRATION.md).

## Verification

The local suite uses fakes and an in-memory store; it does not replace deployed
API, WebGL, or headset acceptance. See
[Quiz integration](../../Docs/QUIZ_INTEGRATION.md) for the rollout and remaining
checks.
