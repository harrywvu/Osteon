# Known Issues and Project History

> Current-state update: 2026-10-06. Historical entries are retained below so
> previous recovery work is not lost.

## Current open issues

### Current scene and incomplete feature migration

- The only enabled scene is
  `Assets/_Recovery/CONTROLLERS MIGRATION.unity`. Earlier documentation named
  `0 (8).unity`; that is now a historical recovery snapshot.
- The enabled scene has G3 selection-based inspection for 26 vertebral-column
  entries, 29 skull entries, 25 ribcage entries (24 ribs and the sternum), 30 left lower-limb
  bones, 32 right lower-limb meshes, 60 upper-limb bones, 4 pectoral bones,
  and 1 pelvic hip bone. `BonePartInfo`
  supplies their descriptions. Grabbing, physical return, and assembly remain
  outside the current experience.
- G0 whole skeleton, G1 axial/appendicular divisions, and the G2 skull, vertebral
  column, Ribcage, limb, pectoral, and pelvic views are connected in
  the enabled scene. Left Y switches between the two pectoral G2 models.
  Other major groups remain outside the implemented G2 flow.
- A 2026-10-01 Quest build showed controller rays passing through the ribcage
  and appendicular G1 groups and highlighting the vertebral group behind them.
  Those groups had created colliders at runtime from imported meshes with
  Read/Write disabled; PC Play Mode callback tests did not exercise physics
  ray hits. The current scene now serializes 140 G1 mesh targets and the Quest
  build settings prebake collision meshes. The skull adds 30 authored G1
  collider targets and 29 authored G2 bone targets. Automated raycast and G0–G3 checks
  pass, and a new Android APK built with zero errors; headset confirmation is
  still pending.
- Appendicular selection and Back were repaired after the skull optimization,
  but the new APK has not yet been tried on a physical Quest. The cause, repair,
  and regression test are recorded under the appendicular navigation incident
  below.
- No project-owned socket-matching logic was found, and the enabled scene does
  not contain an `XRSocketInteractor`. Socket materials are present, but an
  assembly/matching exercise is not currently implemented.
- `AxialDivisionSelection.cs` is unreferenced by every scene and prefab. It
  overlaps with the newer generic `DivisionSelection` behavior and is a
  cleanup candidate after confirming it is not needed.
- G3 display placement, controller feel/reconnection, targeting, and text
  readability still require physical Quest verification. See `G3_INSPECTION.md`.
- The lower-limb, upper-limb, and pectoral G2 views previously revolved around
  offset object origins when turned with the right thumbstick. Their six yaw
  rotators now use the visible model bounds center. The current-scene Play Mode
  suite passed 10,559 assertions across 207 entries on 2026-10-06, including
  90-degree center checks for all six views. Physical Quest confirmation is pending.
- User review on 2026-09-09 found the expected inspection output but identified
  rough interaction and placement. Stationary use, smooth viewpoint turning,
  and controller grabbing are planned in
  [Plans and Features](../Plans%20and%20Features/README.md), not implemented.

### Package and XR configuration risks

- `com.meta.xr.sdk.interaction`, `com.meta.xr.sdk.interaction.ovr`, and the
  resolved Meta XR Core package are 83.0.0, while `com.meta.xr.simulator` is
  81.0.0. This version gap previously caused package compilation failures and
  remains unresolved in `Packages/manifest.json`.
- Both `com.unity.ai.assistant` and deprecated `com.unity.ai.generators` are
  present. A historical cleanup reportedly removed Generators, but it has since
  returned to the manifest. Confirm whether either package is required before
  changing them.
- `com.unity.xr.oculus` 4.5.2 remains installed even though Android XR
  Management currently loads OpenXR. Confirm whether the legacy Oculus provider
  is still needed before removal.
- Hand-tracking packages/features and controller profiles are enabled together.
  The right-thumbstick rotation script is explicitly controller-based, and the
  end-to-end hand/controller paths have not been verified on current hardware.
- The full, division, and G2 group views have yaw rotators. G3 uses
  independent turning/tilt on its inspection copy; the reference group stays
  stationary.
- Batch Play Mode emits an `ArgumentOutOfRangeException` in imported
  `XRDeviceSimulatorUI.Initialize` when its keyboard action has no resolved
  controls, and another in Unity Editor Search indexing. Bezi/Unity AI packages
  also report duplicate CodeAnalysis assembly versions and an unavailable
  editor-integration WebSocket. Anatomy checks pass separately; these package
  and editor issues have not been repaired by changing imported code.

### Practice quiz demo boundaries

- The shared quiz API runs in a maintainer's personal AWS account and currently
  maps every valid demo token to one learner ID. It is suitable for the
  controlled one-headset presentation, not multiple independent learners or a
  public Quest/web release. The token is extractable from a Unity build.
- The web UI, per-learner authentication, API Gateway authorizer, deliberate
  browser CORS configuration, automated AWS deployment/rollback, and school
  ownership of AWS billing and learner data are not implemented or verified.
- The quiz is a world-space station with an XR grab bar. Automated Play Mode
  checks confirm the station and grab components, while controller targeting
  and movement still need physical Quest acceptance.
- The model/CSV were integrated from the teammate's repository, but their
  educational accuracy, data rights, and approval for wider distribution have
  not been documented here. See [`QUIZ_INTEGRATION.md`](QUIZ_INTEGRATION.md).

### Build and release configuration

- The automated G3 Android development build on 2026-09-09 completed ARM64
  native compilation but failed to package an APK. Gradle reported
  `Unable to establish loopback connection`; a Meta manifest callback also
  failed on a missing generated `xrmanifest.androidlib` intermediate manifest
  in the isolated validation copy. A later APK was built and installed by the
  user for Quest testing, but that build exposed the G1 ray-target defect above.
  The earlier automated packaging failure remains a separate recorded issue.
- The product name is `VRSKULL`, but the committed company and Android package
  identity are still the Unity template defaults:
  `DefaultCompany` / `com.DefaultCompany.VRTemplate`.
- Two very similar Android build profiles exist: `Meta Quest` and
  `Meta Quest 1`. Both inherit the global scene list and serialize the same core
  Android settings. Consolidate them once ownership of the duplicate is known.
- The build profiles serialize Android target SDK 32, while the custom Android
  manifest declares Horizon OS SDK minimum 60 and target 83. Validate the final
  merged manifest and current Meta store requirements before release.
- Release signing and keystore ownership are not documented. Do not distribute
  a release build until those details have an explicit owner and secure storage
  location.

### Asset pipeline

- The project contains 11 live `.blend` files. Unity imports all project assets,
  and the current scene directly depends on `hospital room.blend`,
  `Skeleton_axial.blend`, and `VERTEBRAL COLUMN.blend`.
- Blender 4.5.x and correct `.blend` file association remain required for a
  reliable clean import. Exporting runtime models to FBX/GLB would remove this
  machine-level dependency while preserving `.blend` files as editable source.
- Git LFS must resolve models and media before Unity opens the project. A pointer
  file can look present in Explorer while containing none of the binary asset.

## Verification still required

- [x] Automated Play Mode G0–G3 flow: 10,535 assertions, all 207 entries on 2026-10-01
- [x] Automated G2 limb and pectoral rotation-center checks: 10,559-assertion
      current-scene suite on 2026-10-06
- [x] Repeat all-bone validation from the restored default branch on 2026-09-24
- [x] Back callbacks across the implemented model-view transitions
- [x] Two-ray bone highlighting and material restoration in both G2 views
- [ ] Manual Play Mode controller input and targeting
- [ ] Controller rays and right-thumbstick rotation on Quest 2 and Quest 3
- [ ] Hand-tracking selection path on a physical headset
- [ ] Clean Android/Quest build from a fresh clone
- [ ] Installation and launch of that APK on supported Quest hardware
- [ ] Quiz station stays in the scene, and its menu, hide/reopen, start/stop,
      answer flow, and error recovery work in Play Mode and on Quest

## 2026-10-01 — Appendicular selection and Back regression

- **Observed:** After the skull model and ray-target work, appendicular bones
  still highlighted, but selecting them did not reach G3. Both panel Back and
  left X appeared inert in Play Mode and the then-current Quest build.
- **Cause:** The appendicular division root's `DivisionSelection.navigation`
  referenced `AnatomyNavigationController`, while its sibling
  `ViewTransitionOnSelect.navigation` was null in the enabled scene. The
  transition's legacy path could activate a view directly without recording a
  navigation frame. A visible view could therefore disagree with the
  controller's level and history. Highlighting alone did not prove that the
  selection callback had entered G1 through navigation. The earlier automated
  test called `SelectDivision` directly and missed the broken XRI callback.
- **Repair:** Assign the appendicular `ViewTransitionOnSelect.navigation` to
  the same scene controller as `DivisionSelection.navigation`. At runtime,
  `ViewTransitionOnSelect.Prepare` now also adopts its sibling
  `DivisionSelection.Navigation` when its own reference is missing. The
  validation selects the appendicular G0 ray target through
  `XRInteractionManager.SelectEnter` and checks G1 history, G2/G3 selection,
  and Back. Changing the selection gate did not resolve this wiring defect.
- **If it recurs:** Leave Play Mode and reopen
  `Assets/_Recovery/CONTROLLERS MIGRATION.unity` from disk after external scene
  edits. On the appendicular division root, inspect both components and set
  their **Navigation** fields to the scene's `AnatomyNavigation` controller;
  save the scene. Run `Tools/Invoke-AnatomyValidation.ps1 -Current` in a
  temporary project copy so the test uses the saved scene. Confirm selecting
  appendicular from G0 records one G1 history frame, then test a limb bone and
  both Back inputs. Rebuild and install a new APK; an older Quest build still
  contains its original scene. The Anatomy setup menu can also rewrite scene
  references, but it replaces catalog descriptions, so save authored content
  before using it.
- **Evidence:** With the original null reference and no runtime fallback, the
  corrected callback test failed at appendicular G0→G1 after 8,024 assertions.
  The repaired scene passed **10,535 assertions across 207 entries**. The
  runtime fallback also passed all 10,535 against the old null-reference
  scene. A fresh ARM64 IL2CPP development build completed with zero errors and
  produced `Builds/Anatomy-G3-navigation-fix.apk`. No Quest was connected, so
  physical targeting, selection, and Back remain unverified for this APK.

## 2026-10-01 — Inspection placement and movable panels

- Moved the G3 bone anchor to X 2.477991, Z -1.173756 and kept its reference
  offset. Added separate XR grab bars below the anatomy and quiz panels.
- Unity 6000.3.2f1 Play Mode passed 9,780 assertions across 178 entries,
  including anchor and grab-component checks. Quest grabbing remains unverified.

## 2026-10-01 — Right lower and upper-limb G3 inspection

- Added 32 selectable right lower-limb meshes (including two sesamoids) and
  30 bones in each upper-limb view.
- Unity 6000.3.2f1 Play Mode passed 9,774 assertions across 178 entries.
  Physical Quest controls and an APK remain unverified.

## 2026-10-01 — Pectoral and pelvic G3 inspection

- Added G3 inspection for the left and right clavicles and scapulae and the
  pelvic model's left hip bone. Left Y switches pectoral sides at G2.
- Unity 6000.3.2f1 Play Mode passed 8,197 assertions across 86 entries.
  Physical Quest controls and an APK remain unverified.

## 2026-09-30 — Left lower-limb G3 inspection

- Added individual selection and inspection for 30 bones in the appendicular
  left lower-limb model, using the existing G3 controls and reference display.
- Unity 6000.3.2f1 Play Mode passed 8,100 assertions across all 81 G3 entries.
  Physical Quest targeting and a packaged APK remain unverified.

## 2026-09-30 — Ribcage G3 inspection

- Connected 24 individual rib meshes and the sternum to the same inspection
  behavior as the vertebral column. Cartilage remains visible context.
- Added rib descriptions, scene bindings, per-bone hover, a highlighted
  reference cage, and Back restoration. Existing G2 ribcage selection remains.
- Unity 6000.3.2f1 Play Mode passed 7,153 assertions across all 51 G3 entries.
  No physical Quest or Android APK test was completed for this change.

## 2026-09-24 — Default-branch G3 recovery

- The validated G3 implementation had been pushed to
  `codex/g3-bone-inspection` but had not been merged into the default `master`
  branch. A clone of `master` therefore contained no `BoneSelection` components
  or `AnatomyNavigationController` in the enabled scene, so individual vertebrae
  could not emit hover or selection events.
- Rebuilt the missing local Git index, preserved the unrelated Unity preload
  settings, and fast-forwarded local and remote `master` through commits
  `a045b5c` and `b2edeeb`.
- Confirmed the enabled scene contains 26 `BoneSelection` components, one
  `AnatomyNavigationController`, 71 `XRSimpleInteractable` components, and 73
  mesh colliders.
- Repeated the full isolated Unity 6000.3.2f1 validation on this laptop. All
  6,604 assertions passed across 26 entries, including XRI selection callbacks,
  two-ray hover, material restoration, Back navigation, and scene reload.
- Physical Quest targeting and Android APK packaging remain unverified.

## 2026-09-09 — Vertebral-column G3 implementation

- Implemented inspection for all 26 entries with independent turning/tilt,
  a stationary reference column, information, and restored Back history.
- Automated Play Mode validation passed 6,604 assertions, including XRI
  selection callbacks, two-ray hover, input restoration, and scene reload.
- The automated Android build failed at packaging; no successful APK was
  verified. Detailed physical-headset acceptance remains pending.
- Recorded user feedback and dated plans for stationary viewing, controller
  grabbing, and reusable bone-group expansion. These improvements remain planned.

## 2026-09-08 — Documentation and repository audit

- Corrected the entry scene to `CONTROLLERS MIGRATION.unity`, the only enabled
  scene in `EditorBuildSettings.asset`.
- Replaced speculative script descriptions with a map of all eight
  project-owned scripts and their current-scene reference status.
- Documented the active low-poly -> axial -> vertebral selection flow, OpenXR
  Android loader, right-controller rotation, current package versions, build
  defaults, and unresolved legacy scripts.
- Defined G0–G3 as anatomical granularity levels and recorded the implementation
  boundary: G0/G1 represented, G2 partial, and G3 not connected.
- This was a static repository audit. It did not establish that Play Mode,
  headset input, or a clean Android build currently passes.

## 2026-07-28 — Visual recovery

- After a full `Library` rebuild, the skeleton and hospital-room models rendered
  invisible even though the assets were present.
- Root cause: Unity needed Blender installed and associated with `.blend` files
  to invoke its import pipeline. Without Blender, the reimport failed without a
  prominent missing-asset error.
- Installing Blender 4.5, associating `.blend` files, and forcing reimport
  restored the affected models.
- Follow-up remains open: use exported runtime assets instead of live Blender
  files, while retaining the `.blend` sources under Git LFS.

## 2026-07-27 — Initial handoff diagnosis

- The project arrived with regenerable `Library/`, `Temp/`, `obj/`, and `Logs/`
  folders from another machine.
- Meta XR Core/Simulator version drift caused `CS0122` errors in Simulator
  editor code.
- A Meta XR `AndroidExternalToolsSettings` error was traced to missing or
  mismatched Android Build Support; reinstalling the module for the exact Unity
  editor resolved that instance.
- The legacy Oculus XR Plugin was already reported as deprecated on the editor
  version in use.
- `com.unity.ai.generators` was reportedly removed during this cleanup, but the
  2026-09-08 audit confirms it is present again.

## Template for future entries

```text
## YYYY-MM-DD — short title

- What broke or what was being tested
- Root cause, including the exact package/file/error where possible
- Fix applied
- Verification performed
- Anything still unresolved
```
