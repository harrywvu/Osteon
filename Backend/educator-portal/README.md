# Educator portal

This source implements PLAN-008's Questions and Learners pages, a protected
educator API, and deployment configuration. The AWS backend and website were
deployed in `ap-southeast-2` on 2026-11-10. Educator sign-in, question publication,
and learner/Quest/WebGL flows still need live acceptance. Do not expose the old
`educator-dashboard` prototype publicly.

The self-sign-up change below was deployed on 2026-11-10. A complete browser
sign-up still needs acceptance with a real MMSU mailbox.

## Live deployment

- Website: `https://d2s8nrl6zj2hte.cloudfront.net/`
- Educator API: `https://1ui7lb5mp5.execute-api.ap-southeast-2.amazonaws.com`
- CloudFormation stack: `osteon-educator-portal`
- Cognito user pool: `ap-southeast-2_lWeHr5IFO`; app client:
  `31ba83ge4g3hmtpo3n3vb31b3p`
- Question bank: `osteon-educator-portal-Bank-FM0CMOZ2JIBR`
- Active imported bank: `legacy-a7c9ad835911e17a`; historical bank:
  `legacy-52e1a4fbdb2a9589` (50 questions each)

The first maintainer-created educator account uses the owner-provided address
`johnny.vu2004@gmail.com` and belongs to `osteon-educators`. Its initial
password change and TOTP setup have not yet been accepted. Never put temporary
passwords in this repository.

Educators can select **Create educator account** on the website, enter an
`@mmsu.edu.ph` email address and password in Cognito, confirm the email, and
enroll authenticator MFA before signing in. The pre-sign-up Lambda rejects
other domains; no maintainer approves or assigns a group to new accounts.
Anyone who controls an MMSU address, including a student, can sign up and
access educator data. Keep the link private until a stronger role check exists.
The Questions page edits and publishes the existing 50 questions. Adding or
removing questions is not implemented.

In the Unity source, new learners choose **New learner** on the Quest or desktop
WebGL account panel, enter first/optional middle/last name, MMSU program, and
year level, then save the six-digit access code issued at creation. Existing
learners use their code to complete or edit those fields. The account API is
deployed, but the updated Unity builds and device/browser flows still require
acceptance testing.

## Components

- `web/` is a static website. `app.js` uses Cognito authorization code with
  PKCE. Access tokens stay in memory; a reload requires a new sign-in. Logout
  ends the Cognito hosted session. An already issued access token can remain
  accepted until its 15-minute expiry; disable the user to stop future sign-ins.
- `api.py` is a Python 3.12 Lambda behind an API Gateway HTTP JWT authorizer.
  It checks the Cognito access-token client ID and token type again. The dedicated
  educator pool is the current access boundary.
  Every data route requires the authorizer. Its DynamoDB projections exclude
  account credentials, sessions, rate records, and encrypted creation replays.
- `infrastructure.yaml` defines the separate educator pool, required TOTP MFA,
  the pre-sign-up domain check, private S3 site, CloudFront HTTPS distribution,
  API, Lambda role, and
  versioned question-bank table. The existing account and quiz services stay in
  their own deployments.
- `Backend/quiz-api/question_bank.py` stores immutable versions as individual
  question rows, a completion manifest, and a conditional active pointer.
  Request UUIDs make publication retries idempotent. Old sessions use their
  pinned bank; new runs read the current pointer.
- `Backend/accounts-api/program_catalog.py` is the single source for IDs and
  display names served by the account API. See
  [catalog review](../../Docs/MMSU_PROGRAM_CATALOG.md) before release.

## Developer workflow

Work from the repository root with Python 3.12, Node.js, PowerShell, and the
AWS CLI. Unity 6000.3.2f1 is needed only for the Quest/WebGL clients. The
portal website is plain HTML, CSS, and JavaScript; it has no bundler or npm
install step. `web/preview.html` contains sample data for layout work and must
never be uploaded as the live site. `web/index.html` loads `config.js` before
`app.js`. Copy `web/config.example.js` to the ignored `web/config.js` and fill
in the four public stack outputs for a deployed environment. The redirect URI
must exactly match the Cognito app client's callback URL.

The files to change for common tasks are:

| Task | Source | Also check |
|---|---|---|
| Questions or Learners interface | `web/index.html`, `web/app.js`, `web/style.css` | `test_api.py` and live browser flow |
| Educator API or learner analysis | `api.py` | `test_api.py`; Lambda package and API response shape |
| Bank validation, publication, or session pinning | `Backend/quiz-api/question_bank.py`, `quiz_service.py` | Quiz tests; old-session behavior and rollback |
| Program choices or learner profile fields | `Backend/accounts-api/program_catalog.py`, `lambda_function.py`; Unity `LearnerAccountPanel.cs` | Account tests; Quest and WebGL builds |
| AWS resources or permissions | `infrastructure.yaml` | CloudFormation change set, CORS, IAM, retained resources |

Run the tests below before packaging. For a local visual preview, open
`web/preview.html`; it does not call AWS. To test authentication, publication,
or learner records, use the HTTPS site and an approved educator account. Never
put an access token, learner code, or temporary password in a checked-in file.

### Website release

The site bucket is private and CloudFront serves it. After editing the static
files, upload only these four files, then invalidate CloudFront. From the repo
root in PowerShell (after `aws login --region ap-southeast-2`):

```powershell
$bucket = 'osteon-educator-portal-portalbucket-rqhueccpwbmn'
$site = 'Backend/educator-portal/web'
foreach ($name in @('index.html', 'style.css', 'app.js', 'config.js')) {
    aws s3 cp "$site/$name" "s3://$bucket/$name" --region ap-southeast-2
}
aws cloudfront create-invalidation --distribution-id E9L9HRC0IKDMD --paths '/*'
```

Review `config.js` before upload; it is environment-specific and ignored by
Git. Check the distribution ID and bucket against stack outputs if deploying
to another account or if the stack was replaced. An invalidation request is
asynchronous; wait for completion before judging a stale page. The portal is
independent of the Unity build, so a static UI change does not require Unity.

### API and infrastructure release

`Build-LambdaZip.ps1` writes `dist/osteon-educator-lambda.zip` with `api.py`,
the shared question-bank module, and the shared program catalog. `dist/` is
ignored. The existing CloudFormation stack points its Lambda at an S3 ZIP key.
For an API release, upload a new immutable ZIP key, then update the stack with
`EducatorZipKey` changed to that key. Preserve the existing values of
`ArtifactBucket`, `AccountsTableName`, `QuizTableName`, and
`CognitoDomainPrefix`; inspect a CloudFormation change set before executing
it. Do not upload the ZIP to the private website bucket. Confirm the stack
returns to `UPDATE_COMPLETE`, then test the authenticated routes. The quiz and
accounts Lambdas are separate deployments: editing their modules locally does
not change those live functions.

The educator Lambda reads `QUESTION_BANK_TABLE_NAME`, `ACCOUNTS_TABLE_NAME`,
`QUIZ_TABLE_NAME`, and `EDUCATOR_CLIENT_ID` from CloudFormation. The quiz
Lambda separately needs its bank table and legacy hash mapping. The account
API separately owns learner creation, login, catalog, and authenticated profile
updates. Do not remove old bank versions: existing quiz sessions may be pinned
to them. A question publication changes the active pointer for new sessions
without a Lambda or Unity deployment.

### Account administration and support

New educators self-register through Cognito's hosted sign-up page with an
`@mmsu.edu.ph` address. `signup_guard.py` checks the exact domain before account
creation. Cognito confirms email ownership and requires software TOTP MFA; no
maintainer action is needed. Existing maintainer-created accounts can still sign in and may need
to change their temporary password first. If sign-in fails, inspect Cognito
user status; `FORCE_CHANGE_PASSWORD` means that change is still pending.
For access removal, disable the user. Already-issued access tokens may remain
valid until expiry (configured at 15 minutes). An AWS account is not required
to use the portal. The legacy `osteon-educators` group remains in the stack
but is no longer an access condition.

Learners are separate from Cognito. The Unity account panel creates a learner
profile through the existing account API and displays a six-digit return code.
The portal reads the learner's profile and current quiz state but cannot edit
their profile. The learner list searches names and returns 20 results per page;
the current implementation scans projected profile records and is intended
for the initial small cohort. Revisit indexing and pagination for a large
cohort. The detail page shows only the current run and at most 10 retained
answers; its correctness breakdown is not a lifetime grade.

## Local checks

```powershell
$env:PYTHONPATH = (Resolve-Path 'Backend/accounts-api/.test-deps').Path
python -m unittest discover -s Backend/accounts-api -p 'test_*.py' -q
python -m unittest discover -s Backend/quiz-api -p 'test_*.py' -q
python -m unittest discover -s Backend/educator-portal -p 'test_*.py' -q
node --check Backend/educator-portal/web/app.js
./Backend/educator-portal/Build-LambdaZip.ps1
```

The account test environment uses Moto as listed in its requirements-test file.
The educator ZIP includes both the API and pre-sign-up Lambda handlers.
The frontend has no build dependency. `web/config.js` is generated for each
environment from `config.example.js`; it contains public endpoints and a public
app-client ID only. Set the four values to the deployed stack outputs and upload
`index.html`, `style.css`, `app.js`, and `config.js` to the private site bucket.

## Deployment record and remaining work

1. Record the AWS account, region, Lambda names, API IDs, and current
   configuration. Download and retain the **actual deployed quiz Lambda ZIP**
   and make backups of both existing DynamoDB tables. The local CSV has
   documented wording changes; do not use it as a substitute for the live ZIP.
2. Package the educator Lambda and upload its ZIP to a private artifact bucket.
   Validate and deploy `infrastructure.yaml` with that bucket/key, the two
   existing table names, and a unique Cognito domain prefix. The template
   retains its site bucket and question table on stack deletion. New educator
   users sign themselves up through the site and complete email/TOTP setup.
3. Run `import_live_bank.py --live-zip <backup.zip> --historical-zip <older.zip>
   --table <bank-table>` without `--apply` to inspect both content hashes and
   compare the local CSV. Repeat `--historical-zip` for each older active hash.
   The historical ZIP must contain the same ONNX model. Then run with `--apply`
   after confirming the backup. Optionally pass
   `--reviewed-csv Backend/quiz-api/assets/questions_datasets.csv` to publish
   reviewed local corrections as the next version. The command prints the
   `LEGACY_BANK_MAP` value. Local boto3 with `aws login` also requires the
   compatible `awscrt` package (`botocore[crt]`).
4. Deploy the updated quiz Lambda ZIP and grant its role `dynamodb:GetItem` and
   `dynamodb:Query` on the bank table. Set `QUESTION_BANK_TABLE_NAME` plus the
   `LEGACY_BANK_MAP` from step 3. Test both active legacy hashes
   run before publishing further edits. Keep the old ZIP for rollback.
5. Deploy the updated accounts Lambda ZIP. Add `GET /v1/catalog/programs` and
   `PUT /v1/auth/me/profile` to its existing HTTP API. Give the profile write
   route a no-cache learner-session Lambda authorizer and allow `PUT` in CORS
   for the hosted WebGL origin. Older clients may still create incomplete
   profiles; current clients collect required fields. Verify the current
   `GET /v1/auth/me` response and quiz account verification still work.
6. Build Quest and optimized WebGL with Unity 6000.3.2f1, then deploy the
   client builds. Configure the educator site with stack outputs, upload it,
   and verify HTTPS, private S3, MFA, Questions, Learners, CORS, conflict, retry,
   and narration behavior. Record Quest and browser acceptance separately.

Steps 1–5 were deployed on 2026-11-10. The actual deployed quiz ZIP and one
historical ZIP were imported without substituting the different local CSV.
Both old hash mappings are configured on the quiz Lambda. The account API's
profile-write route has a no-cache Lambda authorizer; unauthenticated requests
return 401. The educator API's five routes have JWT authorization and the
Lambda educator-client check. The CloudFront site serves HTTPS, and direct S3
access is denied. The quiz and account functions each have published rollback
version `1`; their original ZIPs and configuration snapshots are in ignored
`dist/backup-20261110/`. The account-table backups are:

- `arn:aws:dynamodb:ap-southeast-2:974187939148:table/osteon-accounts/backup/01794241641543-28b2c1ee`
- `arn:aws:dynamodb:ap-southeast-2:974187939148:table/osteon-quiz-demo/backup/01794241641561-f78b3de4`

The 2026-11-10 self-sign-up update completed with CloudFormation status
`UPDATE_COMPLETE`. The live pool has `AllowAdminCreateUserOnly: false`, email
auto-verification, required MFA, and the pre-sign-up trigger. Direct invocation
of the deployed trigger rejected `visitor@example.com` and returned an unchanged
response for `sample@mmsu.edu.ph`; neither invocation created an account.
CloudFront invalidation `I7VCCRZMWB3ACNA5BUC9JD9WG5` completed, and the
public page and script returned HTTP 200 with the new sign-up controls.

Step 6 remains open: complete a real MMSU email confirmation and MFA sign-up,
then check the browser Questions and Learners flows. The Unity Quest/WebGL builds and named-profile
acceptance are not recorded as complete. The MMSU program catalog also needs
the review in `Docs/MMSU_PROGRAM_CATALOG.md`. Do not publish the local CSV until
its differences from the exact live bank have been reviewed.

## API routes

All routes require an educator access token through API Gateway and the Lambda
client/token-type check: `GET /v1/questions`, `POST /v1/questions/publish`, `GET /v1/programs`,
`GET /v1/learners`, `GET /v1/learners/{learner_id}`. Publishing accepts the full
bank, its base version, and a UUID request ID. A stale base returns HTTP 409.
Learner responses include only public profile fields, model-estimated mastery,
current-run count/question, and at most 10 retained answers. The learner detail
also summarizes correctness by bone group and difficulty within that retained
window, and shows a mastery-estimate trace when every retained answer has the
needed values. These are recent-window observations, not a lifetime score.
Legacy answers without a known bank version do not receive guessed question,
choice, or answer text.

## Rollback

Keep the downloaded live quiz ZIP and all published bank versions. A maintainer
can conditionally set the `ACTIVE/META` pointer back to an earlier complete
version after confirming the current version and making a table backup. New
sessions then use that version; sessions already pinned to another version
continue unchanged. Do not delete old version rows. Rolling back the Lambda
code requires restoring its prior environment and ZIP together.
