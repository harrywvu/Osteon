# AGENTS.md

## Project Overview

VR anatomy training app for Meta Quest 2/3. Users grab individual bones from a full skeleton in a hospital-room environment; bones return to origin on release and display an info panel (title + anatomical description). Colored sockets suggest a bone-assembly matching exercise.

- **Engine:** Unity 6000.3.2f1 (pinned — do not guess)
- **Render pipeline:** URP (com.unity.render-pipelines.universal 17.3.0)
- **XR stack:** OpenXR + Meta XR SDK (com.meta.xr.sdk.interaction 83.0.0), XR Interaction Toolkit 3.2.1, XR Hands 1.7.1
- **Target:** Android, ARM64, IL2CPP, min SDK 32
- **3D source assets:** Blender 4.5.x (.blend files — requires Blender installed for import)
- **Entry scene:** `Assets/_Recovery/0 (8).unity`

## Key Scripts

| Script | Role |
|---|---|
| `Assets/BonePartGrabRelease.cs` | XR grab behavior + return-to-origin on release (coroutine-based lerp) |
| `Assets/Scripts/BonePartInfo.cs` | Per-bone data: partName, partDescription |
| `Assets/Scripts/InfoBoardController.cs` | Singleton that shows/hides an in-world TMP info panel |

## Build & Test

1. Open project in Unity Hub (Unity 6000.3.2f1 with Android Build Support module)
2. File → Build Settings → Android (ARM64, IL2CPP, min SDK 32)
3. Enable Developer Mode on Quest via Meta Horizon app
4. Connect Quest via USB, accept USB debugging prompt
5. Build & Run

Or build APK and sideload: `adb install <apk>`

## Critical Gotchas

- **Blender dependency:** The four `.blend` files (`hospital room.blend`, `ribs.blend`, `SpinalColumn.blend`, `wholebodydraft.blend`) require Blender 4.5.x installed and associated with `.blend` files. Without it, meshes render invisible with no loud error. Fix: install Blender 4.5, force reimport.
- **Package version mismatches:** `com.meta.xr.sdk.interaction` (83.0.0) and `com.meta.xr.simulator` (81.0.0) have a version gap. Deprecated `com.unity.ai.generators` also present.
- **Legacy Oculus plugin:** `com.unity.xr.oculus` (4.5.2) is deprecated on this editor version — may be leftover cruft.
- **Scene naming:** Entry scene has an unusual name (`0 (8)`). Rename once stable per `Docs/KNOWN_ISSUES.md`.
- **First-open errors:** If compile errors flood the console on first open, collapse them and check if errors are in `Library/PackageCache/` (package version mismatch) vs `Assets/` (real problem). Safe to delete `Library/`, `Temp/`, `obj/`, `Logs/` to regenerate.

## Repo Structure

- `Assets/Scripts/` — project code (2 files: BonePartInfo.cs, InfoBoardController.cs)
- `Assets/BonePartGrabRelease.cs` — project code (at root Assets)
- `Assets/_Recovery/` — scene recovery history (snapshots of the working scene)
- `Assets/Scenes/` — sample/basic scenes
- `Assets/Samples/` — XR Interaction Toolkit, XR Hands sample content (do not edit)
- `Assets/VRTemplateAssets/` — VR template sample scripts (do not edit)
- `Docs/` — SETUP.md, ARCHITECTURE.md, KNOWN_ISSUES.md (read these first)
- `ProjectSettings/` — Unity project config (build targets, XR settings)
- `Packages/manifest.json` — package dependencies

## Unverified (as of last note)

- Socket/assembly matching logic location (not yet identified in scripts)
- Hand tracking vs controller input path on actual headset
- Clean Android build from a fresh clone