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
history bound. Additional tests cover the Accounts API verification boundary,
revoked-token replay, a second client's conflicting answer, and content-version
reset. The 32 backend tests passed locally on 2026-11-06, including two audit
runner checks. These tests
verify the server's handling of recommendations, not the educational quality or
consistency of Gemini's recommendations.

`Tools/Audit-QuizGemini.py` prepares a direct-provider audit without learner
accounts or DynamoDB writes. `python Tools/Audit-QuizGemini.py --full` requests
an API key at a hidden terminal prompt and runs 100 explanation cases (correct
and wrong choices for all 50 questions) plus 12 repeated recommendation cases
across sparse, weak, improving, and mixed histories. It writes a JSON report
under `Backend/quiz-api/dist/`, with answer anchoring and wording flags for
human review. The key is not written to the report. The runner has been updated
to save after each call, pause between calls, stop after repeated failures, and
resume only missing cases with `python Tools/Audit-QuizGemini.py --full --resume`.
It measures direct-provider request time, not Lambda cold starts or full API
latency.

## First direct Gemini audit run

The owner ran all 112 calls on 2026-11-06. The local
`Backend/quiz-api/dist/gemini-audit.json` contains 17 generated explanations
covering 14 distinct questions. The other 95 calls returned no usable output:
75 HTTP 429, 13 HTTP 503, and 7 timeouts, according to the supplied terminal
log. None of the 12 recommendation scenarios returned an action. This run does
not complete the 50-question explanation review or recommendation review.
The 17 successful calls took 1.062–4.828 seconds each; these are direct-provider
times, not Lambda end-to-end or cold-start measurements.

The automatic screen marked 96 cases for review: 95 empty results and one
answer-text mismatch. Human review found two potentially misleading rib
explanations among the 17 successes. `rib_03` says false ribs attach indirectly,
without accounting for floating ribs 11–12, which have no anterior attachment.
One `rib_07` answer contrasts floating ribs with false ribs even though floating
ribs are commonly classified as a subset of false ribs. See
[OpenStax thoracic cage anatomy](https://openstax.org/books/anatomy-and-physiology/pages/7-4-the-thoracic-cage).
The keyword screen did not catch both issues, so it cannot certify anatomy
accuracy. The provider limit or outage should clear before resuming; the
updated runner stops after three consecutive empty responses rather than
continuing through all remaining cases.

**First resume:** The owner reran with `--full --resume`. Two cases succeeded,
then `rib_03` correct and both `rib_04` cases returned HTTP 503, so the runner
stopped. The report now has 19 usable explanations across 14 distinct
questions, 93 missing cases, and 117 total provider attempts. The two new
explanations (`rib_01` correct and `rib_02` wrong) accurately identified the
thoracic cavity and heart/lungs respectively. No recommendation fixture has
yet produced output. Another immediate run is unlikely to establish the
missing content while the provider is returning 503.

The first audit ran explanation cases before recommendation cases, so all
recommendation attempts came after the provider errors had become frequent.
The runner now prioritizes recommendations. To focus only on the missing 12
advice cases while preserving the report, use
`python Tools/Audit-QuizGemini.py --full --resume --recommendations-only`
after the provider is accepting requests again. This still cannot guarantee
responses when Gemini returns HTTP 503.

**Recommendation-focused resume:** The owner ran the 12 advice cases next.
Ten returned actions and two returned HTTP 503. The three sparse-history runs
all recommended increasing from Medium after one correct answer; the server's
five-answer gate would ignore these. Two weak-history runs recommended
maintaining Medium despite four wrong answers in the preceding five and an
updated mastery near 0.33, where the existing mastery selector prefers Easy.
The two improving-history runs recommended increasing to Hard at mastery near
0.52. Mixed-history runs recommended maintain, maintain, then increase for the
same inputs. This is insufficiently conservative and consistent to validate
Gemini-driven selection. The report now contains 29 usable responses in total
(19 explanation cases, 10 recommendation cases) and 83 missing cases. The
runner now also records the provider's assessment and fixture mastery in
future responses; earlier assessment text was not retained.

The owner reports setting `AI_ADAPTATION_ENABLED=true` in Lambda on
2026-11-06. The environment change has not been read back and live selection
under that flag has not been tested. The concerns above remain open. To
validate the enabled behavior, inspect real recommendations
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
