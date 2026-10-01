# Setup Guide

> Vertebral G3 was revalidated on 2026-09-24. The vertebral and ribcage G3
> paths passed 7,153 Play Mode assertions on 2026-09-30. The expanded suite
> with the left lower limb passed 8,100 assertions across 81 entries.
> The pectoral and pelvic expansion passed 8,197 assertions across 86 entries
> on 2026-10-01.
> The expanded limb suite passed 9,774 assertions across 178 entries on 2026-10-01.
> With the inspection placement and panel handles, 9,780 assertions passed
> across 178 entries on 2026-10-01.
> After Quest testing exposed missing G1 ray hits, baked G1 targets and
> collider raycasts passed 9,947 assertions across 178 entries on 2026-10-01.
> The appendicular callback repair passed 10,535 assertions across all 207
> entries; a new Android development APK built with zero errors on 2026-10-01.

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
cd Osteon
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
defined in `Docs/ARCHITECTURE.md`. G3 covers the skull, vertebral column, ribcage,
both lower limbs, both upper limbs, both pectoral sides, and the pelvic hip bone:

- [ ] **G0:** The hospital room and complete low-poly skeleton render correctly.
- [ ] The XR origin initializes and controller rays can select objects.
- [ ] **G1:** Hovering the axial or appendicular division applies group
      highlighting.
- [ ] **G1:** Selecting a division hides the other division and updates the
      title and description on the in-world information panel.
- [ ] **G2 (partial):** Selecting the axial division opens its detailed view;
      selecting Skull, vertebral column, or Ribcage opens its G2 view. Back
      returns from either group to axial G1.
- [ ] **G2 (partial):** Selecting Left lower limb from appendicular G1 opens its
      G2 model. Back returns to appendicular G1.
- [ ] **G2 (partial):** Selecting Right pectoral girdle opens its G2 model;
      left Y switches to and from the left model. Selecting Pelvic girdle opens
      its G2 model. Back returns to appendicular G1.
- [ ] **G2 (partial):** Selecting Right lower limb or either upper limb opens
      its G2 model. Back returns to appendicular G1.
- [ ] Moving the right thumbstick horizontally rotates the full-skeleton
      selection view.
- [ ] **G3:** Pointing at a skull bone, vertebral entry, rib, sternum, limb bone,
      pectoral bone, or pelvic hip bone highlights only that
      bone and previews its name. Selecting opens an enlarged bone and a
      stationary reference group.
- [ ] **G3:** Right-stick sideways turns the bone; up/down tilts it. A resets
      turning without changing tilt, and B resets tilt without changing turning.
- [ ] **G3:** The name, breadcrumb, introductory description, and controls are readable.
- [ ] **Panels:** Aim at each blue Move bar, hold select/grab, reposition the
      anatomy and quiz panels independently, then release. Quiz Hide/Open keeps
      the launcher with the moved panel. Confirm the anatomy Back button can be
      selected without grabbing the Move bar.
- [ ] Panel Back and left X each follow bone → its G2 group → its division → whole skeleton,
      restoring the previous view's transform and information.
- [ ] In the XR Device Simulator, select Left Controller and press B for X;
      holding X returns only one level until it is released and pressed again.
- [ ] Holding the selection input does not skip levels. Repeat the complete path twice.
- [ ] G3 input does not also move/jump/turn the XR origin or scroll the panel.

See `Docs/G3_INSPECTION.md` and `Tools/Invoke-AnatomyValidation.ps1` for automated
Play Mode regression checks. Grabbing and assembly are excluded from this smoke test.

The optional one-learner practice quiz uses an AWS API. To test it, copy
`Docs/QuizDemoConfig.example.json` to the ignored
`Assets/Resources/QuizDemoConfig.json` and fill in the private HTTPS API base
URL and temporary demo token. In Play Mode, find the quiz station on the room's
right wall. Start/resume, answer, stop, hide, reopen, and turn
your head to confirm the station stays in its current room position until
grabbed. Do not commit the filled config or distribute an APK containing its
token. The quiz's XR grab interaction and physical Quest input still need
device verification. Full backend and AWS setup is in
[`QUIZ_INTEGRATION.md`](QUIZ_INTEGRATION.md).

Run a focused vertebral pilot or the complete 178-entry suite from a temporary project copy
or worktree while the main project is closed or open elsewhere:

```powershell
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Pilot
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>'
```

Add `-Capture` to the complete run when a desktop rendering is also required.

## 6. Build and run on Quest

1. Enable Developer Mode for the headset in the Meta Horizon mobile app.
2. Connect the Quest by USB and accept the headset's USB-debugging prompt.
3. In Unity, open **File -> Build Profiles** and select the `Meta Quest`
   Android profile.
4. Confirm `Assets/_Recovery/CONTROLLERS MIGRATION.unity` is the only enabled
   scene.
5. Confirm **Prebake Collision Meshes** is enabled. Reopen the scene from disk
   if it was open while scene files were updated externally.
6. Choose **Build and Run**, or build an APK and install it manually:

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

If appendicular bones highlight but selection, panel Back, and left X do not
advance or return, follow the appendicular incident in
[`KNOWN_ISSUES.md`](KNOWN_ISSUES.md). Reopen the enabled scene from disk after
external edits, check both navigation references on the appendicular division,
and run the saved-scene `-Current` validation before rebuilding the Quest APK.

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
