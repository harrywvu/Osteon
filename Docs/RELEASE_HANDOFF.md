# Osteon project handoff

**Project status:** Complete, as declared by the project owner on 2026-11-09.
This is the final developer documentation set for the delivered source. The
status records project completion; it does not turn unperformed tests into
verified results or extend the implemented feature scope.

## Delivered application

The enabled `Assets/Scenes/Osteon.unity` scene supports Meta Quest Android and
desktop WebGL builds. It contains G0–G3 anatomy exploration with 207 selectable
G3 entries, G2 Spread for nine multi-bone views, group highlighting, information
and Back navigation, movable world-space panels, Settings, the Field Guide,
account-aware practice quiz, and on-demand panel narration. Quest uses XR
controller input; WebGL uses mouse and keyboard input. The two Python services
provide account sessions and quiz scoring, persistence, and optional AI feedback.

Start with the [developer manual](DEVELOPER_MANUAL.md) and
[documentation map](README.md). The [architecture](ARCHITECTURE.md) describes
runtime ownership; [setup](SETUP.md) gives build and test steps.

## Verification boundary at handoff

- Saved-scene anatomy validation previously passed 10,571 assertions across
  207 entries for skull Spread. A later 2026-11-05 `-Current` run stopped after
  915 assertions on C1 information-panel text overflow. See
  [Known issues](KNOWN_ISSUES.md) and [G3 inspection](G3_INSPECTION.md).
- A 2026-11-05 Android development APK built with zero errors. Physical Quest
  interaction for the latest source is not recorded as accepted.
- Optimized WebGL builds loaded the room, and the owner reported working
  account sign-in, quiz feedback, and cross-device resume. The full browser
  [acceptance checklist](WEB_DESKTOP.md#browser-acceptance-pass) remains open.
- The account and quiz services have local and selected live HTTP checks. The
  latest AI recommendation behavior, the full explanation set, and narration
  playback need the checks recorded in [Quiz integration](QUIZ_INTEGRATION.md),
  the [content audit](QUIZ_CONTENT_AUDIT_2026-11-06.md), and
  [Panel narration](TTS.md).

These are handoff facts, not new test results from the documentation packaging.
The dated records retain the exact evidence and historical context.

## Scope excluded from the delivered experience

Per-bone grabbing and socket assembly are not wired into the enabled scene.
Stationary viewing, smooth viewpoint turning, and further group expansion are
documented as plans under [Plans and Features](../Plans%20and%20Features/README.md).
Do not present those as implemented features.

## Ownership and source of truth

The repository [README](../README.md) records ownership and license. The
enabled scene, `ProjectSettings/EditorBuildSettings.asset`, and current source
are authoritative for behavior. Build outputs, deployment ZIPs, and local
credentials are not part of this documentation package. Keep account codes,
tokens, signing material, and cloud secrets out of shared archives.
