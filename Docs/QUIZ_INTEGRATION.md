# Practice quiz integration and AWS demo runbook

> Status: 2026-09-30. This records the one-learner demonstration built in a
> personal AWS account, not a school-owned production deployment. AWS settings
> and smoke-test outcomes below were reported and exercised during setup; they
> have not been independently audited from the AWS console. Do not put tokens,
> personal credentials, or a real API URL in this repository.

## What runs where

```text
Quest quiz panel (future: web client)
    -> HTTPS / API Gateway HTTP API
    -> Python Lambda: question selection, answer checking, ONNX inference
    -> DynamoDB: current session and mastery for one demo learner

S3 deployment bucket -> Lambda code updates only (not a per-question hop)
```

The training notebook is **not hosted**. It was used to produce the ONNX model;
Lambda serves the exported model and approved question CSV packaged with the
Python code. Both the Quest and a future web client can call the same JSON API.
The current web interface does not exist yet. The UI is separate from the
model, so a web release does not require running a second copy of the notebook.

| Piece | Location | Job |
|---|---|---|
| Quest UI | `Assets/Scripts/OsteonQuizDemo.cs` | Render the in-world menu and questions; make authenticated HTTPS calls. |
| Quiz API | `Backend/quiz-api/handler.py` | Authenticate, route requests, and return JSON. |
| Session rules | `quiz_service.py` | Start/resume, score once, advance, and handle concurrent changes. |
| Model adapter | `quiz_engine.py` | Select a question and run CPU ONNX inference. |
| Persistence | `dynamodb_store.py` | Read and conditionally write the learner session. |
| Deployment package | `Build-LambdaZip.ps1` | Bundle code, CSV, model, and Linux Python dependencies. |

## Model and question bank

The source is the teammate's [Osteon ML Model repository](https://github.com/Anthony77-fool/Osteon_ML_Model).
The local integration includes `assets/questions_datasets.csv` (50 questions:
15 Easy, 20 Medium, 15 Hard; 20 Axial and 30 Appendicular) and
`assets/random_forest_mastery_model.onnx` (a Random Forest model that predicts
the **change** in mastery for one answer). The model file's SHA-256 is
`18e69421e1e811eb12eee6f4f47675731c4613a6cb27e073432122ab8a4c45b9`;
the CSV's is
`1f01b40c3146c5d4f4015aebea68ef84f07c890433d5dc4cbad1679a17257f93`.
These hashes identify the files currently integrated; they do not establish
training quality or content accuracy. Review question correctness and model
ownership/permission with the school before any wider distribution.

The server starts mastery at `0.50`. Below `0.50` it prefers Easy questions;
from `0.50` through `0.75`, Medium; above `0.75`, Hard. It chooses the first
unasked question in the matching tier, falling back to the first unasked
question in the bank if that tier is empty. There is no randomization.
For each answer, the server determines correctness from the CSV, measures
elapsed time since it issued the question, then sends these six `float32`
features to ONNX Runtime in this order:

1. CSV `difficulty_score`
2. Response time in seconds
3. Correctness (`1` or `0`)
4. Previous mastery
5. Appendicular flag (`1` for Appendicular, otherwise `0`)
6. Difficulty code (`0` Easy, `1` Medium, `2` Hard)

The model predicts a delta. The adapter forces a negative delta for a wrong
answer or a positive delta for a correct answer if the prediction contradicts
the result, then clamps updated mastery to `[0, 1]`. This is a learning-score
mechanic, not a validated educational assessment or medical decision tool.
Question responses omit the correct option until after an answer is submitted.
Changing the packaged CSV or model changes a content hash; starting a session
then creates a new run while retaining the previous mastery value.

## AWS resources used for the demo

All resources are in **Asia Pacific (Sydney), `ap-southeast-2`**. Resource names
below describe the current demo; a future school account should use its own
names and infrastructure-as-code.

| Service | Current configuration | Why |
|---|---|---|
| S3 | Private bucket `osteon-quiz-deploy-bucket`, object `osteon-quiz-lambda.zip` | Holds the deployment ZIP for Lambda updates. It is not the model API or the learner database. |
| Lambda | `osteon-quiz-demo`; Python 3.12, x86-64, 1024 MB, 30-second timeout; ZIP handler `handler.lambda_handler` | Runs the Python API and packaged ONNX model on demand. |
| Lambda environment | `QUIZ_TABLE_NAME=osteon-quiz-demo`; `QUIZ_DEMO_TOKEN` set to a private, temporary token | Names the table and enables the one-learner demo identity. Never record the token value here. |
| DynamoDB | `osteon-quiz-demo`; String partition key `learner_id` | Stores one current JSON session, mastery, and revision per learner. |
| IAM | Lambda execution role `osteon-quiz-demo-role-ebn9cp1b` with table-scoped read/write policy and normal CloudWatch Logs rights | Lets Lambda access only its progress table. |
| API Gateway | HTTP API `osteon-quiz-demo-api`; `$default` stage with auto-deploy; Lambda integration | Gives Quest and future web clients an HTTPS endpoint. |

The API Gateway routes are:

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/v1/sessions` | Start or resume the demo learner's current run. |
| `GET` | `/v1/sessions/current` | Fetch its current question and mastery. |
| `POST` | `/v1/sessions/{session_id}/answers` | Submit an answer and receive feedback/next question. |

The role policy's `Resource` must be the **DynamoDB table ARN**, not the S3 ZIP
object ARN. The initial `AccessDeniedException` for `dynamodb:GetItem` came
from using the S3 object ARN in that policy; replacing it with the table ARN
fixed the Lambda call. Use the actual account ID when applying this example:

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Sid": "QuizProgressTable",
    "Effect": "Allow",
    "Action": ["dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:UpdateItem"],
    "Resource": "arn:aws:dynamodb:ap-southeast-2:<account-id>:table/osteon-quiz-demo"
  }]
}
```

This is an **identity policy on the Lambda execution role**. It does not grant
the headset direct DynamoDB access. API Gateway routes currently rely on the
Lambda's temporary bearer-token check, not an API Gateway JWT authorizer.
Without a valid token, the Lambda returns HTTP 401 with
`{"error":"authentication_required"}`.

## Build and deploy an update

1. From the repository root, run
   `./Backend/quiz-api/Build-LambdaZip.ps1`. The script installs pinned
   `numpy==1.26.4` and `onnxruntime==1.22.1` Linux x86-64 wheels for
   Python 3.12, then creates
   `Backend/quiz-api/dist/osteon-quiz-lambda.zip` (about 62 MiB). The
   generated ZIP and package scratch files are not committed.
2. Upload the ZIP to the private Sydney S3 bucket as
   `osteon-quiz-lambda.zip`. Then open Lambda **Code → Upload from → Amazon S3
   location**, paste that object's S3 URL, and choose **Update**. Uploading a
   new S3 object alone does **not** update the running Lambda code. The ZIP
   exceeds Lambda's 50 MB direct-upload limit, which is why S3 is used.
3. Confirm the runtime, architecture, handler, memory, timeout, environment
   variable **names**, and execution role above. Do not reveal the token in
   screenshots or logs. Leave the ZIP as the source of truth instead of editing
   the Lambda console's starter `lambda_function.py`.
4. Run a Lambda test for start and answer, then test the three HTTPS routes.
   Check CloudWatch Logs if a request fails. A successful ZIP deployment is
   not proof that the packaged native model runtime works until an answer has
   been scored. If an update fails, deploy a previously retained known-good
   ZIP from S3 and repeat the Lambda code update; simply changing the S3
   object does not roll Lambda back. Confirm a rollback artifact exists before
   updating a live demonstration.

[AWS Lambda's Python ZIP guide](https://docs.aws.amazon.com/lambda/latest/dg/python-package.html)
describes the same-Region S3 upload route and ZIP layout; the
[HTTP API guide](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api.html)
describes the API Gateway-to-Lambda integration.

## API contract and client configuration

Each request uses `Authorization: Bearer <temporary-demo-token>`. A valid demo
token maps every client to the same fixed `osteon-demo-learner` ID. Do **not**
use this mode for multiple real learners: they would share progress. In a
future release, replace it with verified per-learner identity and enforce
authorization on the API routes.

An answer body is JSON with `question_id`, `choice` (`A`–`D`), and `attempt_id`
(a UUID generated once for that answer and reused if the request is retried).
The backend calculates the time and prior mastery; a client cannot supply
them. The latest repeated `attempt_id` returns the saved result rather than
double-scoring. A stale question or changed session returns HTTP 409. All
responses are JSON with `Cache-Control: no-store`.

The Unity client reads an ignored local file:

```text
Docs/QuizDemoConfig.example.json -> copy to Assets/Resources/QuizDemoConfig.json
```

Find the HTTPS **Invoke URL** in the API Gateway console and use it as the
base URL (without `/v1/...`), along with the private demo token. The JSON and
its `.meta` are ignored by Git. Because Unity
`Resources` are packaged into the build, this token can be extracted from an
APK; it is acceptable only for this controlled demonstration, not public
distribution. Do not paste a real token into documentation or chat.

For a safe smoke test, first call `POST /v1/sessions` without authorization
and expect HTTP 401. With the private bearer token, call that route again and
expect a session ID, initial mastery of `0.5` for a new learner, and a question
without `correct_option`. `GET /v1/sessions/current` should return the same
session. Then submit one answer with a fresh UUID `attempt_id`; expect
correctness, updated mastery, and either a next question or completion. Reuse
the same `attempt_id` only to verify retry behavior: it should replay the
saved result, not advance twice. Answering changes the demo learner's stored
progress, so do not use a shared live session as disposable test data.

In the enabled `CONTROLLERS MIGRATION` scene, `OsteonQuizDemo` creates a
stationary world-space quiz station beside `Anatomy information panel`. The
menu initially shows **Start / Resume Practice**. **Stop** returns to the
menu without erasing saved server progress. **Hide** collapses the menu to a
small launcher at the same scene position; selecting it reopens the menu.
The station does not follow the headset and is not saved as a hand-authored
scene object. If the anatomy panel cannot be found, it uses a fixed position
computed once from the camera at startup. The web client is not implemented;
it needs its own UI, allowed CORS origin, and release-grade authentication.

## Verification record and remaining work

| Check | Observed result |
|---|---|
| Python unit tests | 12 passed on 2026-09-30 using fake ONNX and in-memory stores. |
| Real ONNX inference on Windows | Passed locally using ONNX Runtime 1.22.1. |
| Lambda direct tests | Start and answer succeeded after the IAM table-ARN fix, exercising DynamoDB and model inference. |
| API Gateway smoke test | Unauthenticated request returned `authentication_required`; authorized `POST /v1/sessions` returned a session, mastery `0.5`, and a question. An answer request also succeeded. |
| Unity editor | Quiz UI was reported visible and interactive. The latest stationary placement change has not yet been rechecked in Play Mode or on a Quest. |
| Physical Quest / APK | Not verified for this quiz integration. The existing project also has a separately recorded Android packaging failure. |

Before a wider or production release: move the resources to an institution-
owned AWS account; replace the shared token with per-user authentication and
an API Gateway authorizer; configure web CORS deliberately; rotate/remove the
demo token; automate deployment and rollback; add monitoring, cost alerts,
and a backup/retention policy for learner data; and verify the model/data
rights and educational quality. None of these are implied by the working
single-learner demo.
