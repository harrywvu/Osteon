# Educator dashboard

**Superseded local prototype.** Use the source in
[`Backend/educator-portal/`](../educator-portal/README.md) for PLAN-008. This
read-only server uses developer AWS credentials and must not be exposed publicly.
The portal is implemented locally but not deployed.

Read-only local browser view for reviewing the 50 packaged quiz questions,
answer keys, difficulty levels and scores, and learner profile and quiz data.
The question bank is read from `Backend/quiz-api/assets/questions_datasets.csv`.
The dashboard reads `PROFILE#` records from `osteon-accounts` and saved quiz
sessions from `osteon-quiz-demo` in DynamoDB. S3 holds the Lambda deployment ZIP;
it is not the question or learner database.

## Run

Install `boto3` in a Python environment, then sign in to an AWS CLI profile
granted read-only access to these two tables. From the repository root:

```powershell
python Backend/educator-dashboard/app.py --profile educator
```

Open `http://127.0.0.1:8765/questions`. The server listens only on the local
computer. It never writes to AWS. The educator's AWS identity is the access
control; do not expose this server through a proxy or share AWS credentials.
For least privilege, grant `dynamodb:Scan` on only the two table ARNs. No S3,
Secrets Manager, quiz API, or learner access code permission is needed.

The Learners page shows profile label, ID, status, creation time, current saved
mastery, and answer count for the current run. A learner detail page shows the
current question and up to 10 recent answers because the quiz service retains
only 10 history entries. The account profile stores no name, email, or other
personal details. The dashboard does not show access codes or session tokens.

The question view reflects the local CSV, which may differ from the deployed
Lambda package until an updated ZIP is deployed. This is a review interface;
question edits still happen in the CSV and require a deliberate backend
deployment. Changing the bank changes its content version and starts a fresh
quiz run for each learner on the next start.

Run checks with:

```powershell
python -m unittest discover -s Backend/educator-dashboard -p 'test_*.py' -q
```
