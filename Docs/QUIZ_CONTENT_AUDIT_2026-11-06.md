# Quiz content and adaptation audit — 2026-11-06

## What was checked

Reviewed the question text, four choices, and keyed answer for all 50 questions
in `Backend/quiz-api/assets/questions_datasets.csv`: rib_01–10, skull_01–10,
pelvis_01–10, arm_01–10, and leg_01–10. No keyed answer contradicted the
question after the wording changes below. This is a static answer-key review,
not a review of 50 live Gemini explanations. The two WebGL explanations
captured by the owner after the feedback deployment were anatomically focused.

## Wording corrections in local source

| ID | Finding | Change |
|---|---|---|
| pelvis_10 | The greater sciatic foramen is formed with both the sacrospinous and sacrotuberous ligaments, so asking for one ligament as the sole converter was imprecise. | The question now explicitly names the sacrotuberous ligament and asks for its partner; the keyed answer remains sacrospinous. |
| arm_06 | All eight carpal bones are in the wrist, but “make up the wrist joint” could be read as the radiocarpal articulation specifically. | The question now asks how many carpal bones are in one wrist; the keyed answer remains eight. |

Anatomy references: [NCBI Bookshelf on the sciatic foramina](https://www.ncbi.nlm.nih.gov/books/NBK507801/),
[NCBI Bookshelf on carpal bones](https://www.ncbi.nlm.nih.gov/books/NBK535382/), and
[NCBI MeSH definition of the wrist joint](https://www.ncbi.nlm.nih.gov/mesh/D014955).

These edits change the question-bank hash used as the quiz `content_version`.
Deploying this CSV will start a new quiz session for existing learners while
retaining their saved mastery. Do not deploy it during an active learner session.
The separate, validated package is
`Backend/quiz-api/dist/osteon-quiz-content-review-20261106.zip` (64,057,498
bytes; SHA-256 `7c4b5c9dc920dd111f4f9c3b9a4c9bd6a9a579b74cf8ba1a707293ac18770b19`).
It is not deployed.

## Recommendation and fallback checks

Local automated tests now cover provider timeout, deterministic explanation
fallback, retry without duplicate scoring, the five-answer adaptation gate,
bounded adjacent-tier selection for increase/maintain/decrease, and a ten-entry
history bound. The 24 backend tests passed locally on 2026-11-06. These tests
verify the server's handling of recommendations, not the educational quality or
consistency of Gemini's recommendations.

Still needed before `AI_ADAPTATION_ENABLED=true`: inspect real recommendations
for weak, improving, mixed, and sparse histories, including repeated runs;
inspect generated explanations for all 50 questions with representative correct
and wrong answers; measure live cold/warm latency, provider use, and forced
fallback; and record the detailed platform and cross-device checks in PLAN-007.

## Platform evidence

The owner reports the quiz works in WebGL and VR. WebGL screenshots show
progress and two anatomy-focused wrong-answer explanations. The owner confirmed
that signing into the same learner account on WebGL and VR resumed progress.
Exact VR interactions, another learner's isolation across devices, account
expiry, slow API recovery, and Firefox coverage have not been recorded. The
earlier direct two-learner API test did verify separate
progress, retry, and same-learner resume through the HTTP service.
