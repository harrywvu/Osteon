# Setup Guide

## 1. Required tools (install these BEFORE opening the project)

| Tool | Version | Why |
|---|---|---|
| Unity Editor | **Check `ProjectSettings/ProjectVersion.txt` for the exact version** — do not guess. Install that *exact* version via Unity Hub. | XR packages and Meta SDKs are version-sensitive; a close-but-different version can throw compile errors that look like corruption but aren't. |
| Blender | 4.5.x | The skeleton and hospital-room models (`hospital room.blend`, `ribs.blend`, `SpinalColumn.blend`, `wholebodydraft.blend`) are referenced as **live Blender source files**, not pre-baked exports. Unity silently fails to import them (models go invisible, not "missing") if Blender isn't installed and associated with `.blend` files on the machine. |
| Android Build Support module (via Unity Hub → Installs → gear icon → Add Modules) | Matching your Unity version | Required for Quest/Android builds. Must include Android SDK & NDK Tools and OpenJDK sub-modules. |
| Git LFS | latest | Large binary assets (`.blend`, `.fbx`, textures, audio) are tracked via LFS. Run `git lfs install` once per machine before cloning. |

## 2. Cloning and opening

```bash
git lfs install
git clone <repo-url>
```

Open the cloned folder via **Unity Hub → Open → select folder**. Do not use
"Add project from disk" pointed at a zip extraction — always clone properly
so Git LFS pointers resolve correctly, or your `.blend`/texture files will be
tiny placeholder pointer files instead of the real assets.

**First open will be slow** — Unity has to import every asset and compile
every script fresh. This is normal. If it takes an unusually long time
(30+ min with no progress) or spikes into hundreds of console errors, see
`Docs/KNOWN_ISSUES.md` before assuming something is broken.

## 3. Entry scene

`Assets/_Recovery/0 (8).unity`

## 4. Building for Quest

1. **File → Build Settings** → confirm platform is **Android**
2. Confirm: ARM64, IL2CPP scripting backend, min SDK 32 (these should already
   be set in `ProjectSettings/` — if they're not, something didn't come
   through on your machine's install correctly)
3. Connect a Quest via USB with Developer Mode enabled, or build an APK and
   sideload it
4. Build & Run

## 5. If Unity throws a wall of compile errors on first open

This has happened before. Before panicking:

1. Check if the errors are in `Assets/` (your actual project code — rare and
   serious) vs `Library/PackageCache/` (package-level — common and usually
   just a version mismatch, fixable via Package Manager)
2. Use Console **Collapse** mode to see if hundreds of errors are actually a
   handful of repeated root causes
3. See `Docs/KNOWN_ISSUES.md` for the specific packages that have caused
   trouble in the past (Meta XR Core/Simulator version sync, deprecated
   Unity AI packages)
4. Do **not** delete `Assets/`, `ProjectSettings/`, or any `.meta` files while
   troubleshooting — only `Library/`, `Temp/`, `obj/`, `Logs/` are safe to
   delete and regenerate

## 6. Verifying your setup worked

A clean clone + setup should let you:
- [ ] Open the project with zero errors in Safe Mode
- [ ] See the skeleton and hospital room render correctly (not invisible/pink)
- [ ] Enter Play Mode and grab a bone, release it, and see it return
- [ ] See a bone's info panel populate with title + description on select
- [ ] Build successfully to Android/Quest

If any of these fail on a fresh clone, the docs are incomplete — please
update `KNOWN_ISSUES.md` rather than just fixing it locally and moving on.
