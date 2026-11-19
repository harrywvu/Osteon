# Osteon project handoff

The project owner declared Osteon complete on 2026-11-09. This handoff includes
the Quest 1.2 and educator portal follow-up records from 2026-11-10 and the
build configuration reviewed on 2026-11-19.

## Delivered application

Osteon supports Meta Quest Android and desktop WebGL. The application contains
G0-G3 anatomy exploration with 207 selectable
G3 entries, G2 Spread for nine multi-bone views, group highlighting, information
and Back navigation, movable world-space panels, Settings, the Field Guide,
account-aware practice quiz, and on-demand panel narration. Quest uses XR
controllers and desktop WebGL uses a mouse and keyboard. The account and quiz
services provide learner sessions, scoring, persistence, and optional AI
feedback. The [educator portal](EDUCATOR_PORTAL.md) supports learner review and
question-bank publication.

Start with the [developer manual](DEVELOPER_MANUAL.md) and
[documentation map](README.md). The [architecture](ARCHITECTURE.md) describes
runtime ownership; [setup](SETUP.md) gives build and test steps.

## Verification record

| Area | Recorded evidence | Open acceptance |
|---|---|---|
| Editor anatomy | Earlier saved-scene pass covered 207 entries and 10,571 assertions. The 2026-11-05 `-Current` run stopped after 915 assertions on C1 text overflow. | Resolve the overflow and rerun the [saved-scene checks](G3_INSPECTION.md). |
| Quest | The 2026-11-05 scripted development APK built with zero errors. The local Unity Editor log records a successful 1.2 APK build and install on a Quest 3S on 2026-11-10. | Launch and interact with the 1.2 APK on a headset. Follow the [Quest checklist](VR_1_2_FOLLOWUP.md#test-the-installed-build). |
| Desktop WebGL | Optimized builds loaded the room. The owner reported sign-in, quiz feedback, and cross-device resume. | Run the [browser checklist](WEB_DESKTOP.md#browser-acceptance-pass) against a build identified by source revision. |
| Accounts and quiz | Earlier live HTTP checks covered learner sessions and account-aware quiz. The 2026-11-10 deployment record includes named-profile routes and question-bank integration. | Verify the refreshed Unity client against those routes, playback on both platforms, and the [AI feedback and recommendation checks](QUIZ_INTEGRATION.md). |
| Educator portal | The 2026-11-10 deployment record includes HTTPS, route authorization, and sign-up guard checks. | Complete [educator browser acceptance](EDUCATOR_PORTAL.md#open-acceptance), including school email, MFA, publication, and learner access boundaries. |

The 2026-11-19 check of the working tree based on `6b319bf` passed 17 account
tests, 38 quiz tests, 10 educator portal tests, the portal JavaScript syntax
check, 34 simulated account assertions, and the Windows speech process cleanup
check. These checks exercise local code and mocks. The platform and live
service results appear in the table above.

For each new acceptance run, record the source revision, build artifact, scene,
date, headset or browser version, checks performed, and failures or skipped
checks. For service or content changes, include the deployment version or
question-bank version and link the supporting evidence.

## Scope excluded from the delivered experience

Per-bone grabbing and socket assembly are not wired into the enabled scene.
Stationary viewing, smooth viewpoint turning, and further group expansion are
documented as plans under [Plans and Features](../Plans%20and%20Features/README.md).
Do not present those as implemented features.

## Ownership and source of truth

The repository [README](../README.md) records ownership and license. The
[scene and build map](SETUP.md#scene-and-build-map) identifies the source for
each build. [Accounts and access](ACCOUNTS.md) records responsibilities that
still need owners. Use the service runbooks for deployment and rollback, and
keep account codes, tokens, signing material, and cloud secrets out of test
records and shared archives.
