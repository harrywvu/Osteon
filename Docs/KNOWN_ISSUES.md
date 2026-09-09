# Known Issues and Project History

> Current-state update: 2026-09-09. Historical entries are retained below so
> previous recovery work is not lost.

## Current open issues

### Current scene and incomplete feature migration

- The only enabled scene is
  `Assets/_Recovery/CONTROLLERS MIGRATION.unity`. Earlier documentation named
  `0 (8).unity`; that is now a historical recovery snapshot.
- The enabled scene has G3 selection-based inspection for the 26 vertebral-column
  entries. `BonePartInfo` supplies their descriptions. Grabbing, physical return,
  and assembly remain outside the current experience.
- G0 whole skeleton, G1 axial/appendicular divisions, and G2 vertebral column
  are connected in the enabled scene. Other major groups remain outside the
  implemented G2–G3 flow.
- No project-owned socket-matching logic was found, and the enabled scene does
  not contain an `XRSocketInteractor`. Socket materials are present, but an
  assembly/matching exercise is not currently implemented.
- `AxialDivisionSelection.cs` is unreferenced by every scene and prefab. It
  overlaps with the newer generic `DivisionSelection` behavior and is a
  cleanup candidate after confirming it is not needed.
- G3 display placement, controller feel/reconnection, targeting, and text
  readability still require physical Quest verification. See `G3_INSPECTION.md`.
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
- The full, axial, and vertebral views now each have a model-center yaw rotator.
  G3 uses independent turning/tilt on its inspection copy; the reference column
  stays stationary.
- Batch Play Mode emits an `ArgumentOutOfRangeException` in imported
  `XRDeviceSimulatorUI.Initialize` when its keyboard action has no resolved
  controls, and another in Unity Editor Search indexing. Bezi/Unity AI packages
  also report duplicate CodeAnalysis assembly versions and an unavailable
  editor-integration WebSocket. Anatomy checks pass separately; these package
  and editor issues have not been repaired by changing imported code.

### Build and release configuration

- The automated G3 Android development build on 2026-09-09 completed ARM64
  native compilation but failed to package an APK. Gradle reported
  `Unable to establish loopback connection`; a Meta manifest callback also
  failed on a missing generated `xrmanifest.androidlib` intermediate manifest
  in the isolated validation copy. Successful APK packaging and installation
  remain unverified. This is a recorded failure, not a confirmed diagnosis or fix.
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

- [x] Automated Play Mode G0–G3 flow: 6,604 assertions, all 26 entries
- [x] Back callbacks across the implemented model-view transitions
- [x] Two-ray bone highlighting and material restoration in the vertebral view
- [ ] Manual Play Mode controller input and targeting
- [ ] Controller rays and right-thumbstick rotation on Quest 2 and Quest 3
- [ ] Hand-tracking selection path on a physical headset
- [ ] Clean Android/Quest build from a fresh clone
- [ ] Installation and launch of that APK on supported Quest hardware

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
