# Educator portal

> Status: the AWS backend and website were deployed on 2026-11-10 in
> `ap-southeast-2`. Complete browser sign-up, first educator sign-in, question
> publication, and learner/Quest/WebGL acceptance remain open. Operational
> detail and live resource identifiers are in
> [`Backend/educator-portal/README.md`](../Backend/educator-portal/README.md).

The educator portal is a separate web application for reviewing learner progress
and editing or publishing the quiz question bank. It does not replace the Unity
quiz station or the learner account panel. Publication changes the bank used by
new quiz sessions without redeploying Lambda or Unity; existing sessions stay
pinned to the version they started with.

## Architecture

```text
Educator browser
  -> CloudFront in front of a private S3 site
  -> Cognito hosted sign-in (authorization code + PKCE, required TOTP MFA)
  -> API Gateway HTTP API with JWT authorizer
  -> Educator Lambda
       -> question-bank DynamoDB table (read and publish)
       -> accounts and quiz tables (read-only projections)
```

| Component | Source | Role |
|---|---|---|
| Static site | `Backend/educator-portal/web/` | Login, Questions, and Learners pages; plain HTML/CSS/JS, no bundler |
| Educator API | `Backend/educator-portal/api.py` | Protected routes, learner projections, question publication |
| Sign-up guard | `Backend/educator-portal/signup_guard.py` | Pre-sign-up Lambda that restricts self-registration to `@mmsu.edu.ph` |
| Infrastructure | `Backend/educator-portal/infrastructure.yaml` | Cognito pool, CloudFront/S3 site, API, Lambda, IAM, versioned bank table |
| Bank import | `Backend/educator-portal/import_live_bank.py` | Imports deployed and historical question-bank versions |
| Shared bank logic | `Backend/quiz-api/question_bank.py` | Immutable versions, completion manifest, conditional active pointer |
| Program catalog | `Backend/accounts-api/program_catalog.py` | Shared program IDs and names |

The existing accounts and quiz Lambdas remain separate deployments. Editing a
shared module locally does not change those live functions until each is
packaged and deployed.

## Authentication and access

- The portal uses its own Cognito user pool, not learner accounts. Cognito
  confirms email ownership and requires software TOTP MFA.
- Self-registration accepts an exact `@mmsu.edu.ph` address through the pre-sign-up
  guard. Anyone controlling an MMSU address can register, so keep the site link
  private until a stronger role check exists.
- The site keeps Cognito access tokens in memory only; a page reload requires a
  new sign-in. Logout ends the Cognito hosted session. An issued token remains
  valid until its 15-minute expiry, so disable the user to stop future sign-ins.
- Every API route requires the API Gateway JWT authorizer. The Lambda also checks
  the Cognito app-client ID and token type.
- Learner identity and educator identity are separate. The portal reads learner
  profiles and quiz state but cannot edit profiles or enter a learner session.

## Data behavior

- The question bank stores each published version as immutable question rows,
  a completion manifest, and a conditional active pointer. Publishing a stale
  base returns HTTP 409; a repeated request UUID is idempotent.
- New quiz sessions read the active pointer. Sessions already running stay
  pinned to their original bank, so never delete old versions.
- Learner responses exclude access codes, session tokens, rate records, and
  encrypted creation replays. The detail view shows only the current run and up
  to 10 retained answers; its breakdown is a recent-window observation, not a
  lifetime grade.
- Learner search scans projected profile records and returns 20 results per page.
  This is intended for the initial small cohort; revisit indexing and pagination
  for a larger one.
- The shared program catalog still needs the release review in
  [MMSU program catalog](MMSU_PROGRAM_CATALOG.md).

## API routes

All routes require an educator access token and the Lambda client/token-type
check.

| Method and path | Purpose |
|---|---|
| `GET /v1/questions` | Read the current question bank |
| `POST /v1/questions/publish` | Publish a full bank against a base version and request UUID |
| `GET /v1/programs` | List program IDs and display names |
| `GET /v1/learners` | Search projected learner profiles, 20 per page |
| `GET /v1/learners/{learner_id}` | Read one learner's profile, mastery, current run, and recent answers |

## Local workflow

Work from the repository root with Python 3.12, Node.js, and PowerShell. Install
the [Python test dependencies](../Backend/accounts-api/README.md#test-and-package)
first. Use the AWS CLI for deployments.

```powershell
$env:PYTHONPATH = (Resolve-Path 'Backend/accounts-api/.test-deps').Path
python -m unittest discover -s Backend/accounts-api -p 'test_*.py' -q
python -m unittest discover -s Backend/quiz-api -p 'test_*.py' -q
python -m unittest discover -s Backend/educator-portal -p 'test_*.py' -q
node --check Backend/educator-portal/web/app.js
./Backend/educator-portal/Build-LambdaZip.ps1
```

`web/preview.html` contains sample data for layout work and must never be
uploaded as the live site. `web/index.html` loads `config.js` before `app.js`.
Copy `web/config.example.js` to the ignored `web/config.js` and fill in the
public stack outputs; the redirect URI must exactly match the Cognito app-client
callback URL.

| Change | Files to start with |
|---|---|
| Questions or Learners UI | `web/index.html`, `web/app.js`, `web/style.css` |
| Educator API or learner analysis | `api.py` |
| Bank validation, publication, or session pinning | `Backend/quiz-api/question_bank.py`, `quiz_service.py` |
| Program choices or learner profile fields | `Backend/accounts-api/program_catalog.py`, `lambda_function.py`, Unity `LearnerAccountPanel.cs` |
| AWS resources or permissions | `infrastructure.yaml` |

## Release

**Website:** upload only `index.html`, `style.css`, `app.js`, and `config.js` to
the private site bucket, then invalidate CloudFront. Check the bucket and
distribution against stack outputs. A static UI change does not require a Unity
build.

**API:** `Build-LambdaZip.ps1` writes `dist/osteon-educator-lambda.zip` with the
API, sign-up guard, question-bank module, and program catalog. Upload a new
immutable ZIP key, inspect a CloudFormation change set with `EducatorZipKey`
changed, and preserve `ArtifactBucket`, `AccountsTableName`, `QuizTableName`,
and `CognitoDomainPrefix`. Confirm `UPDATE_COMPLETE`, then test authenticated
routes. Do not upload the ZIP to the website bucket.

The educator Lambda reads `QUESTION_BANK_TABLE_NAME`, `ACCOUNTS_TABLE_NAME`,
`QUIZ_TABLE_NAME`, and `EDUCATOR_CLIENT_ID` from CloudFormation. The quiz Lambda
separately needs the bank table and legacy hash mapping. See the
[rollback runbook](../Backend/educator-portal/README.md#rollback) for site, API,
and bank recovery.

## Open acceptance

- [ ] Complete a real MMSU email confirmation and TOTP sign-up.
- [ ] Sign in and exercise the Questions and Learners pages.
- [ ] Publish a question edit and confirm new sessions use it while old sessions
      stay pinned.
- [ ] Confirm unauthenticated and learner tokens cannot read educator data or
      publish.
- [ ] Verify CORS, conflict, retry, and rollback behavior.
- [ ] Record Quest and WebGL named-profile acceptance separately.
- [ ] Review and reconcile the program catalog.

## See also

- [Educator portal operations](../Backend/educator-portal/README.md)
- [PLAN-008](../Plans%20and%20Features/2026-11-09_PLAN-008_Educator-Portal.md)
- [Quiz integration](QUIZ_INTEGRATION.md)
- [User accounts](USER_ACCOUNTS.md)
