# AGENTS.md

## Project Overview

VR anatomy exploration app for Meta Quest. The current enabled experience uses
selectable skeleton divisions, an in-world anatomical information panel,
hover-group highlighting, axial drill-down views, controller-based yaw rotation,
the Skull G2 spread slider and centered skull rotation,
movable anatomy and quiz panels, and G3 inspection of 207 entries across the skull, vertebral column, ribcage,
both lower limbs, both upper limbs, both pectoral sides, and the pelvic model. See
`Docs/G3_INSPECTION.md` for controls, setup, and the verification record.
`Plans and Features/README.md` indexes dated future work. Stationary viewing,
smooth viewpoint turning, controller grabbing, and further group expansion are planned;
do not describe them as implemented. The latest G3 Android development APK
built successfully with skull explosion; physical Quest interaction is still unverified. See
`Docs/KNOWN_ISSUES.md`.

The conceptual navigation model uses anatomical granularity levels: G0 whole
skeleton, G1 axial/appendicular division, G2 major bone group, and G3 individual
bone. These are semantic scopes, not rendering LODs. See
`Docs/ARCHITECTURE.md` for the implementation matrix.

Legacy per-bone grab/return scripts remain in older recovery scenes, but they
are not attached to the enabled build scene. Do not describe per-bone grabbing
or socket assembly as current functionality without implementing and
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
| `Assets/Scripts/SkeletonYawRotator.cs` | Rotates the full, axial, vertebral, and ribcage models from the right thumbstick | Active |
| `Assets/Scripts/AnatomyNavigationController.cs` | Owns view history, Back, G3 selection and controller input | Active |
| `Assets/Scripts/BoneSelection.cs` | Per-bone selection and hover highlighting | Active on 207 entries across ten group models |
| `Assets/Scripts/SkullExplosionController.cs` | Moves 29 Skull G2 bones and restores their assembled poses | Active on Skull G2 |
| `Assets/Scripts/BoneInspectionDisplay.cs` | Centered inspection copy and stationary reference column | Active |
| `Assets/Scripts/PanelMoveHandle.cs` | Adds XR grab bars below the anatomy and quiz panels | Active |
| `Assets/Scripts/AnatomyInputReservation.cs` | Reserves and restores competing G3 input bindings | Active |
| `Assets/BonePartGrabRelease.cs` | Legacy XR grab, info display, and return-to-origin coroutine | Older recovery scenes only |
| `Assets/Scripts/BonePartInfo.cs` | Per-bone title and description data | G3 and older recovery scenes |
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
updates, drill-down transitions, group highlighting, full-skeleton
right-thumbstick rotation, and G3 inspection. In G3, right-stick sideways turns,
up/down tilts, A resets turning, and B resets tilt. Panel Back or left X restores
the previous view. Per-bone grabbing is not a current-scene test. Automated checks are in
`Assets/Editor/AnatomyValidation.cs` and `Tools/Invoke-AnatomyValidation.ps1`.
In Skull G2, drag the Spread skull slider, turn the spread view with the right
stick, inspect an interior bone, and verify Back retains the spread setting.

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
- **G3 input:** Keep the anatomy Back button independent of coaching-card
  callbacks. Preserve prior binding overrides when reserving the right stick/A/B.
- **Division navigation:** On each G0 division root, both `DivisionSelection`
  and `ViewTransitionOnSelect` must point to the same navigation controller.
  A missing transition reference can show a view without history, leaving
  appendicular G3 selection and Back inert. Validate the real XRI callback;
  see `Docs/KNOWN_ISSUES.md`.
- **Quest G1 ray targets:** Keep rib and appendicular group colliders authored
  in the scene, including the skull, and Prebake Collision Meshes enabled for Android. Runtime mesh
  collider creation from non-readable imports worked in PC Play Mode but did
  not give controller rays a hit target in the tested Quest build.
- **Scene authoring:** The Anatomy setup menu saves the active build scene and
  replaces per-bone descriptions from the matching vertebral, rib, lower-limb,
  or girdle catalog; save work first.
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

- Manual Play Mode input and headset comfort (automated G0–G3 navigation,
  Back, hover, and input-reservation checks passed on 2026-09-09 and again
  after default-branch recovery on 2026-09-24; the expanded 81-entry suite
  passed on 2026-09-30; the 178-entry suite passed 9,780 assertions after
  panel placement and handle configuration on 2026-10-01)
- Controller and hand paths on physical Quest hardware
- Clean Android build from a fresh clone
- Socket/assembly behavior (no implementation was found)
