# Setup Guide

> Last verified against the repository on 2026-09-08.

## 1. Install the required tools

Install these before opening the project for the first time.

| Tool | Required version or configuration | Why |
|---|---|---|
| Unity Editor | **6000.3.2f1** | This exact editor is pinned in `ProjectSettings/ProjectVersion.txt`; XR and Meta packages are version-sensitive. |
| Android Build Support | Module for Unity 6000.3.2f1, including SDK/NDK Tools and OpenJDK | Required for the Android/Quest IL2CPP build. |
| Blender | **4.5.x** with `.blend` file association | Unity invokes Blender to import the live Blender source assets. Without it, models can appear invisible after an import or cache rebuild. |
| Git LFS | Current stable version | `.blend`, `.fbx`, textures, audio, video, and other large binary assets are LFS-managed. |
| ADB | Installed with Unity's Android SDK or Android platform tools | Used to install and inspect APKs on a Quest. |

## 2. Clone correctly

Run:

```powershell
git lfs install
git clone <repo-url>
cd "Human Skeletal VR - Backup"
git lfs pull
```

Do not work from a ZIP archive. A ZIP can contain Git LFS pointer files instead
of the actual models and textures.

Before opening Unity, spot-check LFS resolution:

```powershell
git lfs status
git lfs ls-files
```

If an LFS-managed model is only a small text file beginning with
`version https://git-lfs.github.com/spec/v1`, run `git lfs pull` again.

## 3. Open the project

1. In Unity Hub, choose **Open** and select the repository root.
2. Confirm Unity Hub uses **6000.3.2f1**.
3. Let the first import and script compilation finish before entering Play
   Mode. A clean import can take several minutes.
4. Open `Assets/_Recovery/CONTROLLERS MIGRATION.unity` if Unity does not restore
   it automatically.

The global build scene list also identifies `CONTROLLERS MIGRATION.unity` as
the only enabled scene. `0 (8).unity` is a historical recovery scene, not the
current entry scene.

## 4. Verify the editor configuration

The committed configuration should resolve to:

- Android build target
- ARM64 (`AndroidTargetArchitectures: 2`)
- IL2CPP (`scriptingBackend.Android: 1`)
- minimum Android SDK 32
- Unity Input System
- OpenXR loader for Android
- URP 17.3.0

Unity 6 uses **File -> Build Profiles**. The repository currently contains
`Meta Quest` and a duplicate-looking `Meta Quest 1` profile. Both inherit the
global scene list and serialize the same core Android values. Prefer
`Meta Quest` unless the team confirms a reason to use the duplicate.

## 5. Play Mode smoke test

The current scene is selection-based and follows the G0–G3 granularity model
defined in `Docs/ARCHITECTURE.md`. The present smoke test covers G0, G1, and the
implemented portion of G2:

- [ ] **G0:** The hospital room and complete low-poly skeleton render correctly.
- [ ] The XR origin initializes and controller rays can select objects.
- [ ] **G1:** Hovering the axial or appendicular division applies group
      highlighting.
- [ ] **G1:** Selecting a division hides the other division and updates the
      title and description on the in-world information panel.
- [ ] **G2 (partial):** Selecting the axial division opens its detailed view;
      selecting the configured vertebral-column group opens the group view.
- [ ] Moving the right thumbstick horizontally rotates the full-skeleton
      selection view.
- [ ] The Back button resets the division-selection information state.

**G3 is excluded from this smoke test.** Per-bone grabbing, return-to-origin,
and per-bone information are not wired into the enabled scene; the supporting
legacy scripts are present only in older recovery scenes.

## 6. Build and run on Quest

1. Enable Developer Mode for the headset in the Meta Horizon mobile app.
2. Connect the Quest by USB and accept the headset's USB-debugging prompt.
3. In Unity, open **File -> Build Profiles** and select the `Meta Quest`
   Android profile.
4. Confirm `Assets/_Recovery/CONTROLLERS MIGRATION.unity` is the only enabled
   scene.
5. Choose **Build and Run**, or build an APK and install it manually:

```powershell
adb devices
adb install -r <path-to-apk>
```

The committed Android application identifier is still
`com.DefaultCompany.VRTemplate`. Replace the company name, package identifier,
version, and release signing configuration before distributing the app.

## 7. Troubleshooting the first import

If Unity reports many compile errors:

1. Enable **Collapse** in the Console and identify the first distinct error.
2. Separate project errors under `Assets/` from package errors under
   `Library/PackageCache/`.
3. Confirm Android Build Support belongs to Unity 6000.3.2f1.
4. Review the package risks in `Docs/KNOWN_ISSUES.md` before upgrading or
   removing XR packages.

If models are missing or invisible:

1. Confirm Git LFS downloaded the actual binary files.
2. Confirm Blender 4.5.x is installed and associated with `.blend` files.
3. In Unity, reimport the affected `.blend` assets.

Unity imports every asset in the project, not only assets in the active scene.
There are currently 11 `.blend` files. The active scene directly references
`hospital room.blend`, `Skeleton_axial.blend`, and `VERTEBRAL COLUMN.blend`.

If cache corruption is suspected, close Unity and delete only regenerable
folders such as `Library/`, `Library_*`, `Temp/`, `obj/`, and `Logs/`. Never
delete `Assets/`, `Packages/`, `ProjectSettings/`, or `.meta` files as a cache
repair step.

## 8. Before handing off a change

- [ ] Reopen the current entry scene with no project-owned compile errors.
- [ ] Run the Play Mode smoke test above.
- [ ] Test on a Quest for input or XR configuration changes.
- [ ] For build changes, produce and install a clean APK.
- [ ] Update `Docs/KNOWN_ISSUES.md` with failures that another developer could
      otherwise rediscover.
