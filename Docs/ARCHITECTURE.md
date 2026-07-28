# Architecture Overview

> This is a starting map, not a complete one. Fill in details as you come to
> understand more of the codebase — future-you (or the next student) will
> thank present-you.

## Entry point

`Assets/_Recovery/0 (8).unity` — the working recovery scene as of the
visual-restoration fix (see project log / `KNOWN_ISSUES.md`).

## Known script roles (confirm and expand as you verify each one)

| Script | Believed responsibility |
|---|---|
| `BonePartInfo` | Holds per-bone data — title, anatomical description, probably the socket/matching ID |
| `BonePartGrabRelease` | Handles XR Interaction Toolkit grab behavior and the "return to origin on release" logic |
| `InfoBoardController` | Drives the in-world info panel (TextMesh Pro) — likely populated from whichever `BonePartInfo` was last grabbed/selected |

## Areas to map next

- [ ] Where socket/assembly matching logic lives (which script decides a bone
      is "correctly placed" in its colored socket)
- [ ] Scene flow — is there a menu/start scene before `0 (8).unity`, or is
      that the only scene?
- [ ] Which XR rig prefab is in use (XR Origin (XR Rig) variant — confirm
      hand tracking vs controller input path, since the project has both
      XR Hands and standard controller interaction packages installed)
- [ ] Whether `UI` hierarchy objects are world-space (VR-diegetic) or
      screen-space canvases used for anything (e.g. debug menus)

## Assembly definitions (recommended future work)

Currently the project likely compiles as one big `Assembly-CSharp`, which is
why a single broken XR sample package was able to cascade into 400+ unrelated
compile errors earlier in this project's history. Once the codebase is
stable, consider splitting into `.asmdef` files, e.g.:

- `Anatomy.Core` — bone data, info panel, socket logic (your actual game code)
- `Anatomy.XR` — XR Interaction Toolkit / Meta SDK integration glue
- `Anatomy.Editor` — editor-only tooling

This means a broken third-party package can only break the assembly that
depends on it, not the entire project's compilation.
