# AGENTS.md

## Project Overview

VR anatomy exploration app for Meta Quest. The current enabled experience uses
selectable skeleton divisions, an in-world anatomical information panel,
hover-group highlighting, axial drill-down views, and controller-based yaw
rotation.

The conceptual navigation model uses anatomical granularity levels: G0 whole
skeleton, G1 axial/appendicular division, G2 major bone group, and G3 individual
bone. These are semantic scopes, not rendering LODs. See
`Docs/ARCHITECTURE.md` for the implementation matrix.

Legacy per-bone grab/return scripts remain in older recovery scenes, but they
are not attached to the enabled build scene. Do not describe per-bone grabbing
or G3/socket assembly as current functionality without implementing and
verifying it first.

- **Engine:** Unity 6000.3.2f1 (pinned — do not guess)
- **Render pipeline:** URP (`com.unity.render-pipelines.universal` 17.3.0)
- **XR stack:** OpenXR 1.16.0, Meta XR Interaction/Core 83.0.0, XR Interaction
  Toolkit 3.2.1, XR Hands 1.7.1
- **Target:** Android, ARM64, IL2CPP, minimum SDK 32
- **3D source assets:** Blender 4.5.x (`.blend` import requires Blender)
- **Entry scene:** `Assets/_Recovery/CONTROLLERS MIGRATION.unity`

## Read First

1. `Docs/SETUP.md`
2. `Docs/ARCHITECTURE.md`
3. `Docs/KNOWN_ISSUES.md`
4. `Docs/ACCOUNTS.md` for device or release work

## Project-Owned Scripts

| Script | Role | Current-scene status |
|---|---|---|
| `Assets/Scripts/InfoBoardController.cs` | Controls division visibility, information text, panel state, and Back-button state | Active |
| `Assets/Scripts/DivisionSelection.cs` | Selects/isolates axial or appendicular divisions using child XRI interactables | Active |
| `Assets/Scripts/ViewTransitionOnSelect.cs` | Swaps from the full model to configured drill-down views | Active |
| `Assets/Scripts/BoneGroupHoverHighlighter.cs` | Applies one highlight material to all renderers in a hovered group | Active |
| `Assets/Scripts/SkeletonYawRotator.cs` | Rotates `SkeletonRotationPivot` from the right controller thumbstick | Active |
| `Assets/BonePartGrabRelease.cs` | Legacy XR grab, info display, and return-to-origin coroutine | Older recovery scenes only |
| `Assets/Scripts/BonePartInfo.cs` | Legacy per-bone title and description data | Older recovery scenes only |
| `Assets/Scripts/AxialDivisionSelection.cs` | Superseded axial-only selection behavior | Unreferenced |

## Build and Test

1. Open the project in Unity Hub with Unity 6000.3.2f1 and its Android Build
   Support module.
2. Open `Assets/_Recovery/CONTROLLERS MIGRATION.unity`.
3. Use **File -> Build Profiles** and prefer the `Meta Quest` Android profile.
4. Confirm the global scene list contains only the controller-migration scene
   as enabled.
5. Confirm ARM64, IL2CPP, and minimum SDK 32.
6. Enable Developer Mode on Quest through the Meta Horizon app.
7. Connect by USB, accept USB debugging, then use Build and Run.

Manual installation:

```powershell
adb install -r <path-to-apk>
```

The current Play Mode smoke test is division selection, information-panel
updates, drill-down transitions, group highlighting, and full-skeleton
right-thumbstick rotation. Per-bone grabbing is not a current-scene test.

## Critical Gotchas

- **Blender dependency:** The project contains 11 live `.blend` assets. The
  current scene directly references `hospital room.blend`,
  `Skeleton_axial.blend`, and `VERTEBRAL COLUMN.blend`. Without Blender 4.5.x
  and `.blend` file association, Unity may render imported meshes invisible
  without a prominent error.
- **Git LFS:** Models, textures, and media use LFS. Resolve LFS files before
  opening Unity; do not work from a ZIP containing pointer files.
- **Package version gap:** Meta XR Interaction/Core resolve to 83.0.0, while
  Meta XR Simulator is 81.0.0.
- **Deprecated/redundant packages:** `com.unity.ai.generators` is present beside
  `com.unity.ai.assistant`; `com.unity.xr.oculus` remains installed even though
  Android XR Management loads OpenXR.
- **Scene naming:** The enabled scene has the recovery name
  `CONTROLLERS MIGRATION`. Rename it only when stable, and update the global
  scene list, both build profiles, README, and Docs together.
- **Build identity:** `DefaultCompany` and
  `com.DefaultCompany.VRTemplate` are still committed defaults.
- **Detail-view rotation:** Only the full low-poly model is parented to
  `SkeletonRotationPivot`; drill-down roots do not currently rotate with it.
- **Highlight reference:** One vertebral-view `BoneGroupHoverHighlighter`
  instance has no highlight material assigned.
- **First-open errors:** Collapse Console errors and separate
  `Library/PackageCache/` failures from project errors under `Assets/`. Only
  `Library/`, `Library_*`, `Temp/`, `obj/`, and `Logs/` are safe cache folders
  to regenerate.

## Repository Structure

- `Assets/Scripts/` — project-owned selection, UI, highlighting, and rotation
  code
- `Assets/BonePartGrabRelease.cs` — legacy per-bone grab behavior
- `Assets/_Recovery/` — current scene and historical recovery snapshots
- `Assets/Art/Models/Skeleton/` — low- and mid-poly skeleton assets
- `Assets/VRTemplateAssets/` — imported VR template dependency used by the
  active XR rig and coaching UI; avoid editing
- `Assets/Samples/` — package sample content; do not edit
- `Assets/Settings/Build Profiles/` — Unity 6 Android build profiles
- `Docs/` — maintained project documentation
- `ProjectSettings/EditorBuildSettings.asset` — authoritative enabled-scene
  list
- `Packages/manifest.json` — direct package dependencies

## Unverified

- Play Mode smoke test after the latest scene migration
- Back-button behavior across every drill-down transition
- Controller and hand paths on physical Quest hardware
- Clean Android build from a fresh clone
- Socket/assembly behavior (no implementation was found)
