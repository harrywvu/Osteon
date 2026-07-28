# Known Issues / History

Dated entries. Add to this file instead of just fixing things silently — the
whole point is that the next person doesn't have to rediscover what you
already figured out.

## 2026-07-27 — Initial handoff diagnosis

- Project inherited via flash drive with `Library/`, `Temp/`, `obj/`,
  `Logs/` copied over (all regenerable, all should be deleted and rebuilt
  fresh on a new machine — see SETUP.md).
- Deprecated packages found installed and removed: `com.unity.ai.generators`
  (Unity's own deprecation notice: superseded by `com.unity.ai.assistant`).
- `com.meta.xr.sdk.core` and `com.meta.xr.simulator` had a version mismatch
  causing `CS0122` (inaccessible member) errors in Simulator's editor code.
  **If you don't have physical Quest hardware for testing, resolve the
  version match properly instead of removing Simulator. If you do have
  hardware, Simulator can likely be removed entirely.**
- `AndroidExternalToolsSettings` compile error in Meta XR Core traced to
  missing/mismatched Android Build Support module — reinstalling the module
  matching the exact Unity Editor version resolved it.
- `Oculus XR Plugin` (legacy, `com.unity.xr.oculus`) shows as deprecated /
  "no longer supported on this editor version" — evaluate whether it's still
  needed alongside the modern Meta XR Core SDK, or if it's leftover cruft
  from before the Meta SDK rename.

## 2026-07-28 — Visual recovery

- After a full `Library` cache rebuild, the skeleton and hospital-room
  models rendered invisible (not "missing," just not visible — assets were
  confirmed present).
- Root cause: `.blend` source files (`hospital room.blend`, `ribs.blend`,
  `SpinalColumn.blend`, `wholebodydraft.blend`) require **Blender itself
  installed and associated with `.blend` files** for Unity to invoke as its
  import pipeline. No Blender install = silent failed reimport = invisible
  meshes, with no loud Console error to point at the cause.
- Fixed by installing Blender 4.5 (matching the source files' version) and
  associating it, then forcing Unity to reimport just the four affected
  model files.
- **Follow-up planned, not yet done:** export these four models to `.fbx`
  and point Unity at the FBX instead of the live `.blend` file. This removes
  the "must have Blender installed" dependency entirely for anyone who only
  needs to run/build the project rather than edit the 3D art. Keep the
  `.blend` files in the repo (via Git LFS) as the editable source — just stop
  making Unity depend on Blender being present at import time.

## Still unverified as of last entry

- [ ] Bone grab/release return-to-origin behavior in Play Mode
- [ ] Info panel populating correctly on bone select
- [ ] Socket/assembly matching logic
- [ ] Full VR input path (hands vs controllers) on an actual headset
- [ ] A clean Android/Quest build from a fresh clone (not just the
      recovered local working copy)

## Template for new entries

```
## YYYY-MM-DD — short title

- What broke / what you were trying to do
- Root cause (be specific — package name, file, exact error text)
- Fix applied
- Anything still unresolved or worth double-checking later
```
