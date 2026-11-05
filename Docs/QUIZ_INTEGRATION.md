# Practice quiz integration and AWS demo runbook

> Status: 2026-11-06. The account-aware Lambda is deployed in the personal
> AWS account and has passed a two-learner live API test. The owner deployed the
> feedback fix and supplied WebGL screenshots of two anatomy-focused explanations
> after wrong answers. The owner also reports the quiz works in both WebGL and
> VR. Provider source and the detailed platform acceptance cases remain
> unverified. Do not put tokens or credentials here.

See the [current AI pipeline diagram](QUIZ_AI_PIPELINE.md) for the answer path,
fallback, and difficulty flags. The older demo instructions below are kept as
a deployment history.

## Historical one-learner demo (superseded)

The setup details below record the previous shared-token demonstration. The
current deployment and API contract are described under **API contract and
client configuration**.

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
the original demo CSV's was
`1f01b40c3146c5d4f4015aebea68ef84f07c890433d5dc4cbad1679a17257f93`.
The reordered local CSV's is
`0e5705197381040e11c408d04af6b8d47aac624a3803e7163afa8b6a619593ca`.
The later wording-corrected local CSV's is
`f3ef318418e6fa406049e86b36bf06346329383a7a017fa2bb8bb697dba4f92f`;
it is packaged separately and has not been deployed.
These hashes identify the files involved; they do not establish
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

**2026-11-05 source update:** The new client and Lambda source use account
bearer tokens. Deploy the quiz Lambda with `ACCOUNTS_API_ORIGIN` set to the
fixed HTTPS accounts origin, `QUIZ_DEMO_TOKEN` removed, and the ZIP containing
`account_verifier.py` and `ai_feedback.py`. The public quiz URL is in
`Assets/Resources/QuizServiceConfig.json`. `AI_ENABLED` and
`AI_ADAPTATION_ENABLED` default off. Set `GEMINI_KEY_SECRET_ARN` to a separate
Secrets Manager secret containing only the provider key, with quiz Lambda read
permission; `GEMINI_MODEL` defaults to `gemini-3.5-flash-lite`. Quiz API CORS must allow the exact web
origin, GET/POST, authorization/content-type/x-unity-version, and OPTIONS
without credentials. Platform acceptance remains unrecorded.
The 2026-11-05 local `osteon-quiz-account-aware.zip` was assembled from the
previous dependency ZIP because PyPI was unavailable. ZIP integrity, source
contents, and live Lambda execution were checked.
Review all 50 explanations before enabling adaptation. The next-question
timer still includes feedback reading and idle time.

**2026-11-05 live rollout:** The previous quiz ZIP was copied to
`s3://osteon-quiz-deploy-bucket/backup-plan007/osteon-quiz-lambda.zip`.
`osteon-quiz-account-aware.zip` was uploaded under a separate key and deployed
to `osteon-quiz-demo`; the Lambda SHA-256 matched the local ZIP. The Lambda
environment now has the fixed accounts API origin and both AI flags set to
`false`, with the shared demo token removed. The quiz HTTP API allows
`http://localhost:8000`, GET/POST, and authorization/content-type/x-unity-version
without browser credentials; `$default` automatic deployment is enabled.
Live OPTIONS returned 204 with these headers, and unauthenticated POST returned
401 with the localhost CORS origin. An unlisted origin received no allow-origin
header. Two fresh account profiles then passed live quiz start, separate
progress, one answer each, saved answer retry, same-learner resume using a new
login, fallback explanation with AI disabled, and success-response CORS. Three
test sessions were signed out. AI, Unity, and device checks remain pending.

**Gemini test on 2026-11-05:** Secrets Manager contains
`osteon/quiz/gemini-api-key` as a raw string. The quiz Lambda role has an
inline policy allowing only `secretsmanager:GetSecretValue` on that secret's
exact ARN. `GEMINI_KEY_SECRET_ARN` is set, `AI_ENABLED=true`, and
`AI_ADAPTATION_ENABLED=false`. A fourth temporary learner submitted one answer
and signed out; its response was saved with `explanation_source=fallback` in
1.11 seconds. CloudWatch logged `provider_http_404`. A direct provider model
listing with the same key succeeded and included `gemini-2.5-flash`. Diagnosis
and any further AWS changes are paused because the AWS console browser origin
is blocked by a saved or managed permission. At that point, the quiz remained
usable through its deterministic fallback; later screenshots are recorded below.
Google's [deprecation guidance](https://aistudio.google.com/docs/deprecations#gemini-2.5-flash-models)
limits Gemini 2.5 access to prior active users and recommends
`gemini-3.5-flash-lite`. The source default was updated, but the
deployed ZIP still uses the old default until redeployed; setting the Lambda
`GEMINI_MODEL` environment variable can select the new model in the meantime.

**Follow-up:** The account owner reports setting `GEMINI_MODEL` to
`gemini-3.5-flash-lite` and restoring `AI_ENABLED=true`. This has not been read
back through the AWS console because browser access remains blocked. A fresh
Linux Lambda ZIP was built locally as
`Backend/quiz-api/dist/osteon-quiz-account-aware-20261105.zip`
and passed archive integrity and entry-point checks; SHA-256 is
`a7d346692f1bef5eef20fed61f1abb2473a9d89d753c193b832bd5e69d20ecf5`.
The owner reported deploying it on 2026-11-06. Static question-bank review
found that all 30 pelvis,
arm, and leg questions placed the correct choice at A. Local CSV choices were
reordered to balance answer positions (A 12, B 13, C 12, D 13) without changing
any question's answer text or option set. This changes the content version and
starts a fresh quiz run when deployed.

The refreshed optimized WebGL player built with zero errors and 386 warnings;
its artifact contains both public service URLs and no demo-token setting or
Gemini secret name. The Android development APK also built with zero errors
and 352 warnings; its package has a valid ZIP directory and Android manifest.
The user confirmed that the saved learner code signs in on WebGL and that wrong
quiz answers show explanations. A later screenshot showed both feedback styles;
no response `explanation_source` or CloudWatch readback was available. Broader
browser interaction, physical Quest input, cross-device
resume, and full question-bank explanation quality still require live acceptance.
The 2026-11-05 scene
validation stopped on an unrelated C1 anatomy-panel text overflow after 915
assertions.

**2026-11-06 browser check:** After the owner uploaded the updated Lambda ZIP,
screenshots showed a fresh quiz run advancing from two to four answered
questions. One response used the exact deterministic fallback text; two later
responses showed longer generated feedback. This establishes that both feedback
paths reach the WebGL panel, although `explanation_source` was not read from the
network response. The generated feedback described the learner's streak and
mastery rather than the anatomical reason for the correct answer, so the
explanation-quality check failed. The source now supplies the correct and
selected answer text, explicitly separates anatomy explanation from progress
assessment, and rejects progress-only text for fallback. The owner deployed
this fix and supplied WebGL screenshots from two wrong answers. The floating-ribs
response explains their lack of anterior sternal attachment and posterior
articulation; the skull response names the frontal bone and distinguishes the
occipital bone. Neither discusses mastery or streaks. This passes the sampled
explanation-content check, but the screenshots do not prove `explanation_source`
or all-question quality. Adaptation remains off.
The owner subsequently reported that the quiz works in both WebGL and VR and
confirmed same-account progress resumed across those devices. Exact VR
interactions and the remaining cross-device cases were not recorded.
The [2026-11-06 content audit](QUIZ_CONTENT_AUDIT_2026-11-06.md) reviewed all
50 keyed answers, corrected two ambiguous question stems locally, and recorded
the remaining live recommendation, latency, and platform checks. The revised
question-bank package is separate and has not been deployed because its content
version change would restart active quiz sessions.
The replacement package is
`Backend/quiz-api/dist/osteon-quiz-feedback-fix-20261106.zip`, SHA-256
`83d4c1de6d0eddbf0bd014a5c250c337c04529a4217a29d6f2371929d4f2ce07`.

### Historical demo-token notes (superseded)

The following demo-token contract describes the superseded version.
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
world-space quiz station on the room's right wall. The
menu initially shows **Start / Resume Practice**. **Stop** returns to the
menu without erasing saved server progress. **Hide** collapses the menu to a
small launcher at the station's current position; selecting it reopens the menu.
The blue Move bar below the quiz panel lets the user grab and reposition the
station, including its launcher. It does not follow the headset and is not
saved as a hand-authored scene object. Its initial wall position is configured
in the Unity script.
The desktop WebGL player now reuses this Unity quiz station, but quiz requests
from a browser have not been verified. Browser use needs a deliberately allowed
CORS origin and release-grade authentication; the shared demo token must not be
published in a WebGL build. A separate browser-native quiz UI is not implemented.

## Historical demo verification (superseded)

| Check | Observed result |
|---|---|
| Python unit tests | 12 passed on 2026-09-30 using fake ONNX and in-memory stores. |
| Real ONNX inference on Windows | Passed locally using ONNX Runtime 1.22.1. |
| Lambda direct tests | Start and answer succeeded after the IAM table-ARN fix, exercising DynamoDB and model inference. |
| API Gateway smoke test | Unauthenticated request returned `authentication_required`; authorized `POST /v1/sessions` returned a session, mastery `0.5`, and a question. An answer request also succeeded. |
| Unity editor | Quiz UI was reported visible and interactive. The movable station and XR grab components passed automated Play Mode checks on 2026-10-01; physical grab feel remains unverified. |
| Physical Quest / APK | Not verified for this quiz integration. The existing project also has a separately recorded Android packaging failure. |

Before a wider or production release: move the resources to an institution-
owned AWS account; replace the shared token with per-user authentication and
an API Gateway authorizer; configure web CORS deliberately; rotate/remove the
demo token; automate deployment and rollback; add monitoring, cost alerts,
and a backup/retention policy for learner data; and verify the model/data
rights and educational quality. None of these are implied by the working
single-learner demo.
