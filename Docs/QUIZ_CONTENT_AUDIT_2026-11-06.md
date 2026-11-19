# Quiz content and adaptation audit — 2026-11-06

## Answer-key review

Reviewed the question text, four choices, and keyed answer for all 50 questions
in `Backend/quiz-api/assets/questions_datasets.csv` (`rib_01`–`10`,
`skull_01`–`10`, `pelvis_01`–`10`, `arm_01`–`10`, `leg_01`–`10`). No keyed answer
contradicted its question after the corrections below. This is a static review,
not a review of 50 live Gemini explanations.

| ID | Finding | Change |
|---|---|---|
| `pelvis_10` | The greater sciatic foramen uses both the sacrospinous and sacrotuberous ligaments, so asking for one alone was imprecise. | The question names the sacrotuberous ligament and asks for its partner; the key remains sacrospinous. |
| `arm_06` | "Make up the wrist joint" could mean only the radiocarpal articulation, though all eight carpals are in the wrist. | The question asks how many carpal bones are in one wrist; the key remains eight. |

References: [NCBI sciatic foramina](https://www.ncbi.nlm.nih.gov/books/NBK507801/),
[NCBI carpal bones](https://www.ncbi.nlm.nih.gov/books/NBK535382/), and
[NCBI MeSH wrist joint](https://www.ncbi.nlm.nih.gov/mesh/D014955).

These edits change the content version. Deploying the CSV starts a new quiz
session for existing learners while retaining mastery. The validated package is
`Backend/quiz-api/dist/osteon-quiz-content-review-20261106.zip` (SHA-256
`7c4b5c9dc920dd111f4f9c3b9a4c9bd6a9a579b74cf8ba1a707293ac18770b19`); it is not
deployed. Do not deploy during an active learner session.

## Automated checks

Local backend tests cover provider timeout, deterministic explanation fallback,
retry without duplicate scoring, the five-answer adaptation gate, bounded
adjacent-tier selection, a ten-entry history bound, Accounts API verification,
revoked-token replay, conflicting answers, and content-version reset. The 32
backend tests passed locally on 2026-11-06. They verify server behavior, not the
educational quality of Gemini's output.

`Tools/Audit-QuizGemini.py` runs a direct-provider audit without learner
accounts or DynamoDB writes. `--full` prompts for a key and requests 100
explanation cases (correct and wrong choices for all 50 questions) plus 12
repeated recommendation cases across sparse, weak, improving, and mixed
histories. It writes a JSON report under `Backend/quiz-api/dist/` with
answer-anchoring and wording flags. It saves after each call, pauses between
calls, stops after repeated failures, and resumes missing cases with `--resume`.
It measures direct-provider time, not Lambda latency.

## Audit run

The owner ran all 112 calls. The report contained 17 generated explanations
covering 14 questions; 75 calls returned HTTP 429, 13 HTTP 503, and 7 timed out.
No recommendation scenario returned an action. A resume added two explanations,
then stopped on repeated 503s; a recommendation-focused resume returned 10
actions and two 503s.

The 17 successes included two potentially misleading rib explanations:
`rib_03` omits that floating ribs 11–12 have no anterior attachment, and one
`rib_07` answer contrasts floating and false ribs although floating ribs are a
subset of false ribs. The keyword screen missed both, so it cannot certify
anatomy accuracy.

The 10 advice cases were insufficiently conservative and consistent: sparse
histories recommended increasing from Medium after one correct answer, weak
histories recommended maintaining Medium despite four recent wrong answers, and
mixed histories varied for the same inputs. This does not validate
Gemini-driven selection.

## Open items

- Resume the explanation audit for all 50 questions when the provider is stable.
- Inspect live recommendations for weak, improving, mixed, and sparse histories,
  including repeated runs, before accepting `AI_ADAPTATION_ENABLED=true`.
- Measure cold/warm latency, provider use, and forced fallback.
- Record WebGL and VR interaction, cross-device isolation, account expiry, slow
  API recovery, and Firefox coverage.
