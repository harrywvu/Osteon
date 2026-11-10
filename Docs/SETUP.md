# Setup Guide

## 1. Install the required tools

| Tool | Required version or configuration | Why |
|---|---|---|
| Unity Editor | **6000.3.2f1** | Pinned in `ProjectSettings/ProjectVersion.txt`; XR and Meta packages are version-sensitive. |
| Android Build Support | Module for Unity 6000.3.2f1, including SDK/NDK Tools and OpenJDK | Required for the Android/Quest IL2CPP build. |
| Web Build Support | Module for Unity 6000.3.2f1 | Required for the desktop WebGL player. |
| Blender | **4.5.x** with `.blend` file association | Unity invokes Blender to import the live `.blend` assets. Without it, models can appear invisible. |
| Git LFS | Current stable version | Models, textures, audio, and other large binaries are LFS-managed. |
| ADB | Installed with Unity's Android SDK or Android platform tools | Installs and inspects APKs on a Quest. |

## 2. Clone correctly

```powershell
git lfs install
git clone <repo-url>
cd Osteon
git lfs pull
```

Do not work from a ZIP archive; it can contain LFS pointer files instead of the
actual models and textures. Spot-check before opening Unity:

```powershell
git lfs status
git lfs ls-files
```

If an LFS file is a small text file beginning with
`version https://git-lfs.github.com/spec/v1`, run `git lfs pull` again.

## 3. Open the project

1. In Unity Hub, choose **Open** and select the repository root.
2. Confirm Unity Hub uses **6000.3.2f1**.
3. Let the first import and script compilation finish before entering Play Mode.
4. Open `Assets/Scenes/Osteon.unity` if Unity does not restore it automatically.

`Osteon.unity` is the only enabled scene in `EditorBuildSettings.asset`.
Numbered scenes under `Assets/_Recovery/` are historical snapshots.

## 4. Verify the editor configuration

The committed configuration should resolve to:

- Android build target
- ARM64 (`AndroidTargetArchitectures: 2`)
- IL2CPP (`scriptingBackend.Android: 1`)
- minimum Android SDK 32
- Unity Input System
- OpenXR loader for Android
- URP 17.3.0

Unity 6 uses **File → Build Profiles**. The repository contains `Meta Quest`,
the duplicate-looking `Meta Quest 1`, and `Web - Desktop - Development`. All
inherit the global scene list. Prefer `Meta Quest` for Android and the Web
profile for desktop builds.

## 5. Play Mode smoke test

The scene follows the G0–G3 granularity model in
[Architecture](ARCHITECTURE.md). G3 covers the skull, vertebral column, ribcage,
both lower limbs, both upper limbs, both pectoral sides, and the pelvic hip bone.

- [ ] **G0:** The hospital room and complete low-poly skeleton render correctly.
- [ ] The XR origin initializes and controller rays can select objects.
- [ ] **First-run tutorial:** On a fresh preference profile, the Field Guide and
      glowing arrows show the division choices, then follow a hovered group or
      bone. Check a division → group → bone route, right-stick turn and tilt,
      and Back through every level. Skip collapses the guide; Replay from G3
      returns to the whole skeleton. Confirm the card stays collapsed after a
      restart.
- [ ] **G1:** Hovering the axial or appendicular division applies group
      highlighting. Selecting it hides the other division and updates the
      information panel.
- [ ] **G2:** Selecting the axial division opens its detailed view; Skull,
      vertebral column, and Ribcage open their G2 views. Back returns to axial G1.
- [ ] **G2:** Left lower limb, Right lower limb, both upper limbs, and Pelvic
      girdle open their G2 models. Back returns to appendicular G1.
- [ ] **G2:** Right pectoral girdle opens its model; left Y switches to and from
      the left model.
- [ ] Moving the right thumbstick horizontally rotates the full-skeleton view.
- [ ] **G3:** Pointing at a configured bone highlights only that bone and
      previews its name. Selecting opens an enlarged bone and a stationary
      reference group.
- [ ] **G2 Spread:** In the nine multi-bone views, drag Spread from assembled to
      full spread, turn with the right stick, and select a small or interior
      bone. Back restores that group's spread amount; reopening starts assembled.
      The one-bone pelvic view has no Spread slider.
- [ ] **G3:** Right-stick sideways turns the bone; up/down tilts it. A resets
      turning and B resets tilt independently.
- [ ] The name, breadcrumb, description, and controls are readable.
- [ ] **Panels:** Aim at each blue Move bar, hold select/grab, reposition the
      anatomy, quiz, and Settings panels, then release. Launchers stay with their
      moved panels, and nearby buttons remain selectable.
- [ ] **Settings:** Change room surface colors, panel theme, text size, and
      anatomy highlight color. Confirm large text scrolls, Reset restores
      defaults, and choices survive a restart.
- [ ] Panel Back and left X each follow bone → G2 group → division → whole
      skeleton, restoring the previous transform and information.
- [ ] In the XR Device Simulator, select Left Controller and press B for X;
      holding X returns only one level until released and pressed again.
- [ ] Holding selection does not skip levels. G3 input does not move the XR
      origin or scroll the panel.

The account-aware quiz needs the corresponding Lambda deployed first. Sign in
through **Accounts** at the quiz station on the right wall, then start/resume,
answer, stop, hide, reopen, and confirm the station stays put until grabbed.
Backend setup and the open device checks are in
[Quiz integration](QUIZ_INTEGRATION.md).

Automated checks:

```powershell
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Pilot
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>'
```

Run these from a temporary project copy or worktree while the main project is
closed or open elsewhere. Add `-Capture` when a desktop rendering is required.

## 6. Build and explore in a desktop browser

With Web Build Support installed, select `Web - Desktop - Development` in
**File → Build Profiles**, then choose **Build and Run**. For an optimized build,
switch to WebGL and use **Tools → Osteon → Build Optimized WebGL**; serve the
output with `./Tools/Start-WebDesktop.ps1`.

`AnatomyDesktopInput` runs in a WebGL player only, so the Play Mode smoke test
exercises the XR path, not desktop controls. See
[Desktop WebGL](WEB_DESKTOP.md) for controls and the browser acceptance
checklist.

## 7. Build and run on Quest

1. Enable Developer Mode for the headset in the Meta Horizon mobile app.
2. Connect the Quest by USB and accept the USB-debugging prompt.
3. In Unity, open **File → Build Profiles** and select the `Meta Quest` profile.
4. Confirm `Assets/Scenes/Osteon.unity` is the only enabled scene.
5. Confirm **Prebake Collision Meshes** is enabled. Reopen the scene from disk if
   scene files changed externally.
6. Choose **Build and Run**, or install an APK manually:

```powershell
adb devices
adb install -r <path-to-apk>
```

The committed application identifier is still `com.DefaultCompany.VRTemplate`.
Replace the company name, package identifier, version, and release signing
configuration before distributing the app.

## 8. Troubleshooting the first import

**Many compile errors**

1. Enable **Collapse** in the Console and identify the first distinct error.
2. Separate project errors under `Assets/` from package errors under
   `Library/PackageCache/`.
3. Confirm Android Build Support belongs to Unity 6000.3.2f1.
4. Review package risks in [Known issues](KNOWN_ISSUES.md) before changing XR
   packages.

**Appendicular selection or Back does not work**

Reopen the enabled scene from disk after external edits, check that both
navigation references on the appendicular division root are assigned, and run
the saved-scene `-Current` validation before rebuilding. See the appendicular
incident in [Known issues](KNOWN_ISSUES.md).

**Models are missing or invisible**

1. Confirm Git LFS downloaded the actual binary files.
2. Confirm Blender 4.5.x is installed and associated with `.blend` files.
3. Reimport the affected `.blend` assets.

Eleven `.blend` files are imported project-wide. The enabled scene directly
references `hospital room.blend`, `Skeleton_axial.blend`, and
`VERTEBRAL COLUMN.blend`.

**Suspected cache corruption**

Close Unity and delete only regenerable folders: `Library/`, `Library_*`,
`Temp/`, `obj/`, and `Logs/`. Never delete `Assets/`, `Packages/`,
`ProjectSettings/`, or `.meta` files as a cache repair.

## 9. Before handing off a change

- [ ] Reopen the entry scene with no project-owned compile errors.
- [ ] Run the Play Mode smoke test above.
- [ ] Test on a Quest for input or XR configuration changes.
- [ ] For build changes, produce and install a clean APK.
- [ ] Update [Known issues](KNOWN_ISSUES.md) with failures another developer
      could otherwise rediscover.
