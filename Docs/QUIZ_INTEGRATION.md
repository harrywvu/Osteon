# Practice quiz integration

The account-aware Lambda passed a two-learner API test on the earlier quiz
deployment. On 2026-11-06 the owner reported deploying a feedback fix and
enabling Gemini adaptation. The 2026-11-10
[deployment record](../Backend/educator-portal/README.md#deployment-record-and-remaining-work)
includes DynamoDB question-bank integration. Provider output, full explanation
coverage, live recommendations, and full platform acceptance remain open.

The [AI pipeline diagram](QUIZ_AI_PIPELINE.md) shows the per-answer path,
fallbacks, and difficulty flags. The [content audit](QUIZ_CONTENT_AUDIT_2026-11-06.md)
records the question review and its open items.

## Runtime components

```text
Quest or WebGL quiz panel
    -> HTTPS / API Gateway HTTP API
    -> Python Lambda: authentication, question selection, answer checking, ONNX inference
    -> DynamoDB: session, mastery, history

S3 deployment bucket -> Lambda code updates only (not a per-question hop)
```

Lambda runs the packaged ONNX model. It reads from the versioned DynamoDB
bank when configured and uses the packaged CSV in other environments. Learners
take quizzes in Unity. Educators manage the bank through a separate
[portal](EDUCATOR_PORTAL.md).

| Piece | Location | Job |
|---|---|---|
| Unity UI | `Assets/Scripts/OsteonQuizDemo.cs` | Render the station and questions; make authenticated HTTPS calls |
| Quiz API | `Backend/quiz-api/handler.py` | Authenticate, route, and return JSON |
| Session rules | `quiz_service.py` | Start/resume, score once, advance, handle conflicts |
| Model adapter | `quiz_engine.py` | Select a question and run CPU ONNX inference |
| Persistence | `dynamodb_store.py` | Read and conditionally write learner state |
| Deployment package | `Build-LambdaZip.ps1` | Bundle code, CSV, model, and Linux dependencies |

## Model and question bank

The source is the teammate's
[Osteon ML Model repository](https://github.com/Anthony77-fool/Osteon_ML_Model).
The package contains `assets/questions_datasets.csv` (50 questions: 15 Easy, 20
Medium, 15 Hard; 20 Axial, 30 Appendicular) and
`assets/random_forest_mastery_model.onnx`, which predicts the **change** in
mastery for one answer. The model SHA-256 is
`18e69421e1e811eb12eee6f4f47675731c4613a6cb27e073432122ab8a4c45b9`.

Mastery starts at `0.50`. Below `0.50` the server prefers Easy, `0.50`–`0.75`
Medium, and above `0.75` Hard; it takes the first unasked question in that tier,
falls back to the first unasked question, and does not randomize. For each
answer it sends six `float32` features to ONNX Runtime:

1. CSV `difficulty_score`
2. Response time in seconds
3. Correctness (`1` or `0`)
4. Previous mastery
5. Appendicular flag (`1` or `0`)
6. Difficulty code (`0` Easy, `1` Medium, `2` Hard)

The adapter forces a negative delta for a wrong answer and a positive delta for a
correct answer, then clamps mastery to `[0, 1]`. This is a learning-score
mechanic, not a validated educational or medical assessment. Responses omit the
correct option until submission. With the DynamoDB bank configured, new runs
pin the published version and existing runs keep their original bank. Legacy
content hashes are resolved through `LEGACY_BANK_MAP`. In CSV-only mode, a
content-version change starts a new run while retaining mastery.

## API contract

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/v1/sessions` | Start or resume the learner's current run |
| `GET` | `/v1/sessions/current` | Fetch the current question and mastery |
| `POST` | `/v1/sessions/{session_id}/answers` | Submit an answer and receive feedback/next question |

All protected requests use `Authorization: Bearer <account-token>`. The quiz
Lambda verifies the token through the Accounts API `/v1/auth/me` on every
request and uses only the returned learner ID. Missing or invalid tokens return
HTTP 401 with `{"error":"authentication_required"}`. Responses use
`Cache-Control: no-store`.

An answer body is JSON with `question_id`, `choice` (`A`–`D`), and `attempt_id`
(a UUID generated once per answer and reused on retry). The server calculates
time and prior mastery; clients cannot supply them. A repeated `attempt_id`
replays the saved result without double-scoring. A stale question or changed
session returns HTTP 409. A conditional write prevents concurrent answers from
overwriting each other.

## AWS resources

All resources are in **Asia Pacific (Sydney), `ap-southeast-2`**. A school-owned
deployment should use its own names and infrastructure-as-code.

| Service | Current configuration |
|---|---|
| S3 | Private `osteon-quiz-deploy-bucket`, object `osteon-quiz-lambda.zip`; deployment only |
| Lambda | `osteon-quiz-demo`; Python 3.12, x86-64, 1024 MB, 30 s timeout, handler `handler.lambda_handler` |
| Lambda environment | `QUIZ_TABLE_NAME=osteon-quiz-demo`, `ACCOUNTS_API_ORIGIN`, `QUESTION_BANK_TABLE_NAME`, `LEGACY_BANK_MAP`, AI flags, Gemini settings |
| DynamoDB | `osteon-quiz-demo`; String partition key `learner_id` |
| IAM | Execution role with quiz table read/write, question-bank reads, and CloudWatch Logs |
| API Gateway | HTTP API `osteon-quiz-demo-api`, `$default` stage with auto-deploy |
| Secrets Manager | Separate Gemini key secret, readable only by the quiz Lambda role |

The role policy's `Resource` must be the DynamoDB **table ARN**, not the S3
object ARN. API Gateway relies on the Lambda's bearer-token check, not a gateway
JWT authorizer.

## Client configuration and flags

- `Assets/Resources/QuizServiceConfig.json` holds the public quiz base URL. The
  client attaches the current account token.
- `AI_ENABLED` and `AI_ADAPTATION_ENABLED` default off.
- `GEMINI_KEY_SECRET_ARN` points to a Secrets Manager secret containing only the
  provider key; the quiz Lambda needs read permission.
- `GEMINI_MODEL` defaults to `gemini-3.5-flash-lite`.
- The Gemini key must never be bundled in the Unity player or docs.
- Quiz API CORS must allow the exact web origin, GET/POST,
  `authorization`/`content-type`/`x-unity-version`, and OPTIONS without
  credentials.

With AI disabled, feedback falls back to the correct option and answer text.
With adaptation enabled, Gemini may suggest increase/maintain/decrease only after
at least five recorded answers; the server bounds the result to an adjacent tier
and records both the recommendation and the selected tier. Gemini cannot change
correctness, the answer key, or mastery.

## Build and deploy an update

1. From the repository root, run `./Backend/quiz-api/Build-LambdaZip.ps1`. It
   installs pinned `numpy==1.26.4` and `onnxruntime==1.22.1` Linux x86-64 wheels
   for Python 3.12 and writes `Backend/quiz-api/dist/osteon-quiz-lambda.zip`
   (about 62 MiB). Generated files are not committed.
2. Upload the ZIP to the private S3 bucket. In Lambda, choose
   **Code → Upload from → Amazon S3 location**, paste the object URL, and update.
   Uploading to S3 alone does not update the running code; the 50 MB direct
   upload limit is why S3 is used.
3. Confirm runtime, architecture, handler, memory, timeout, environment variable
   names, and execution role.
4. Test start and answer in the Lambda console, then the three HTTPS routes.
   Check CloudWatch logs on failure. A deployed ZIP is not proof that native
   model inference works until an answer has been scored.
5. Keep a known-good ZIP for rollback. Re-deploy that ZIP through the Lambda code
   update; changing the S3 object does not roll Lambda back.

Do not put tokens or credentials in this document, screenshots, or logs.

## Remaining acceptance

- Review all 50 keyed questions and all 50 generated explanations, including
  correct and wrong answers.
- Inspect live recommendations for sparse, weak, improving, and mixed histories
  before accepting `AI_ADAPTATION_ENABLED=true`.
- Measure cold/warm latency, provider failures, and forced fallback.
- Verify account isolation, CORS success/error paths, account expiry, and
  cross-device resume in WebGL and on Quest.
- Move resources to an institution-owned account; define billing, monitoring,
  retention, and rollback ownership.
