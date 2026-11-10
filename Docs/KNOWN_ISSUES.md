# Known Issues and Incidents

> Current-state update: 2026-11-06. This page lists open risks, incident lessons,
> and the checks still required. It is not a test record.

## Open issues

- **Physical Quest acceptance is incomplete.** Android development APKs build
  with zero errors, but the latest source has no recorded headset pass for
  targeting, grabbing, panel movement, Settings, Spread, tutorial, narration, or
  comfort. A 2026-11-05 build also emitted 352 warnings.
- **Batch validation is unreliable.** Several Unity batch attempts exited during
  licensing startup before loading the scene. The 2026-11-05 `-Current` run
  started and passed 915 assertions, then failed because C1 information text
  overflowed its panel (measured 319.5). Fix the layout and rerun.
- **AI quiz feedback is only sampled.** The owner deployed a prompt and
  validation fix; WebGL screenshots show anatomy-focused explanations after
  wrong answers. Provider source, all 50 explanations, latency, and live
  recommendation behavior remain unread or untested. The owner reports
  `AI_ADAPTATION_ENABLED=true` since 2026-11-06, but the change was not read back
  and recommendation quality is not yet acceptable. See
  [Quiz integration](QUIZ_INTEGRATION.md).
- **Browser acceptance remains partial.** Optimized WebGL builds load the room,
  and the owner reports sign-in, quiz feedback, and cross-device resume.
  Anatomy navigation, panels, Settings, narration, CORS error paths, and
  performance still need a recorded pass. See [Desktop WebGL](WEB_DESKTOP.md).
- **First-run tutorial is unverified.** The Field Guide, glowing arrows, Skip,
  replay, large-text layout, controller-ray buttons, and Quest comfort need Play
  Mode and headset checks. Desktop prompts have no matching recordings.
- **Narration playback is unverified.** Bundled clips and the Android offline
  voice fallback compile, but Play Mode, WebGL, and Quest playback and the
  availability of an installed English voice are unconfirmed. See
  [Panel narration](TTS.md).
- **Account UI is unverified in-engine.** Live HTTPS account tests passed, but
  Play Mode UI, physical Quest input, and a rebuilt browser pass are not
  recorded. See [User accounts](USER_ACCOUNTS.md).
- **The quiz service is not production-ready.** It runs in a maintainer's
  personal AWS account. Before a wider release, move it to a school account,
  add proper per-user authorization at the gateway, configure browser CORS
  deliberately, automate deployment and rollback, add monitoring and cost
  alerts, and define learner-data retention.
- **Content rights and accuracy are undocumented.** The question bank and ONNX
  model came from a teammate's repository. Educational accuracy, data rights,
  and permission for wider distribution need written confirmation. The local
  content audit corrected two ambiguous stems; that package is not deployed.
- **Legacy script cleanup.** `AxialDivisionSelection.cs` is unreferenced and
  overlaps `DivisionSelection`. `BonePartGrabRelease.cs` is used only by older
  recovery scenes. Remove them only after confirming nothing references them.
- **Not implemented:** per-bone grab and return, socket assembly, stationary
  viewing, smooth viewpoint turning, complete G2 coverage, and G3 outside the
  configured groups. Do not describe these as current features.

## Incident lessons

### Appendicular selection and Back (2026-10-01)

- **Symptom:** Appendicular bones highlighted, but selection did not reach G3;
  panel Back and left X appeared inert.
- **Cause:** The appendicular division root's `DivisionSelection.navigation`
  referenced the controller, but its sibling
  `ViewTransitionOnSelect.navigation` was null. The legacy transition path could
  activate a view without recording a navigation frame, so the visible view and
  the controller's level disagree. The old test called `SelectDivision`
  directly and missed the broken XRI callback.
- **Repair:** Assign both components on each G0 division root to the same
  `AnatomyNavigationController`. `ViewTransitionOnSelect.Prepare` now adopts its
  sibling's navigation reference when its own is missing. Validation drives the
  real XRI selection callback.
- **If it recurs:** Reopen the scene from disk after external edits, check both
  navigation fields on the appendicular root, save, and run
  `Invoke-AnatomyValidation.ps1 -Current` in a temporary copy. Rebuild and
  reinstall the APK; an older headset build still contains the old scene. The
  Anatomy setup menu can rewrite references but replaces catalog descriptions,
  so save authored content first.

### Quest rays missed G1 groups (2026-10-01)

Controller rays passed through the ribcage and appendicular G1 groups on Quest.
Those groups created mesh colliders at runtime from imported meshes with
Read/Write disabled; PC Play Mode callbacks did not exercise physics ray hits.
The scene now serializes 140 G1 mesh targets plus 30 skull G1 targets and 29 G2
bone targets, and Quest build profiles prebake collision meshes. Keep authored
colliders and **Prebake Collision Meshes** enabled for Android.

### Rotators used offset origins (2026-10-06)

Lower-limb, upper-limb, and pectoral G2 views revolved around offset object
origins. Their yaw rotators now use the visible model bounds center. Physical
Quest confirmation is pending.

### Blender-dependent import (2026-07-28)

After a full `Library` rebuild, skeleton and room models rendered invisible even
though assets were present. Unity needed Blender installed and associated with
`.blend` files; without it, reimport failed without a prominent missing-asset
error. Installing Blender 4.5 and forcing reimport restored the models. Exporting
runtime models to FBX/GLB would remove this machine-level dependency.

### Meta package version drift (2026-07-27)

Meta XR Core/Simulator version drift caused `CS0122` errors in Simulator editor
code. A Meta XR `AndroidExternalToolsSettings` error was caused by missing or
mismatched Android Build Support and was resolved by reinstalling the module for
the exact editor.

## Package and XR risks

- `com.meta.xr.sdk.interaction`, `com.meta.xr.sdk.interaction.ovr`, and Meta XR
  Core resolve to 83.0.0, while `com.meta.xr.simulator` is 81.0.0. This gap has
  caused compilation failures and remains unresolved.
- Both `com.unity.ai.assistant` and deprecated `com.unity.ai.generators` are
  present. A cleanup reportedly removed Generators, but it has returned. Confirm
  whether either is required before changing them.
- `com.unity.xr.oculus` 4.5.2 remains installed even though Android XR
  Management loads OpenXR. Confirm whether the legacy provider is needed.
- Hand-tracking packages and controller profiles are both enabled. The
  right-stick rotation script is controller-based; end-to-end hand and
  controller paths are unverified on current hardware.
- Batch Play Mode emits an `ArgumentOutOfRangeException` from imported
  `XRDeviceSimulatorUI.Initialize`, another from Editor Search indexing, duplicate
  CodeAnalysis assembly versions from Bezi/Unity AI packages, and an unavailable
  editor-integration WebSocket. Anatomy checks pass separately; these imported
  package issues have not been repaired.

## Build and release configuration

- The product name is `VRSKULL`; the committed company and package identity are
  still `DefaultCompany` / `com.DefaultCompany.VRTemplate`.
- Two Android profiles exist: `Meta Quest` and `Meta Quest 1`. They serialize the
  same core settings; consolidate once ownership of the duplicate is known.
- Build profiles serialize Android target SDK 32, while the custom manifest
  declares Horizon OS minimum 60 and target 83. Validate the merged manifest and
  current Meta store requirements before release.
- Release signing, keystore custody, and backup ownership are undocumented. Do
  not distribute a release build until those have an explicit owner and secure
  storage location. See [Accounts and access](ACCOUNTS.md).
- An older automated build failed during APK packaging with Gradle's
  `Unable to establish loopback connection` and a missing
  `xrmanifest.androidlib` intermediate. Later manual and automated builds
  succeeded; treat packaging as environment-sensitive.

## Asset pipeline

- Eleven live `.blend` files are imported project-wide. The scene directly
  depends on `hospital room.blend`, `Skeleton_axial.blend`, and
  `VERTEBRAL COLUMN.blend`.
- Blender 4.5.x and correct `.blend` association are required for a reliable
  clean import.
- Git LFS must resolve models and media before Unity opens. A pointer file can
  look present in Explorer while containing no binary asset.

## Verification still required

- [x] Automated G0–G3 Play Mode flow across all 207 entries
- [x] G2 limb and pectoral rotation-center checks
- [x] Back callbacks across implemented transitions
- [x] Two-ray highlighting and material restoration
- [ ] Manual Play Mode controller input and targeting
- [ ] Controller rays and right-stick rotation on Quest 2 and Quest 3
- [ ] Hand-tracking selection on a physical headset
- [ ] Clean Android/Quest build from a fresh clone
- [ ] Installation, launch, and interaction on supported Quest hardware
- [ ] Quiz station placement and full menu/answer/error flow in Play Mode and on Quest
- [ ] C1 text-overflow fix and a complete `-Current` validation rerun
