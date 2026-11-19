# Osteon

Osteon is a Unity anatomy explorer for Meta Quest and desktop WebGL. Users move
from the whole skeleton (G0) through divisions (G1) and bone groups (G2) to
inspection of 207 individual entries (G3). The scene also includes an in-world
information panel, group highlighting, model rotation, G2 Spread, movable
panels, a first-run Field Guide, Settings, on-demand narration, and a practice quiz.

## Get started

For app controls, start with the [learner guide](Docs/LEARNER_GUIDE.md).
For development, use the [setup guide](Docs/SETUP.md) and
[documentation map](Docs/README.md).
To browse the guides with search and sidebar navigation, run the
[documentation site](docs-site/README.md) locally.

1. Clone with Git LFS and run `git lfs pull`. Models and media are LFS-managed;
   a ZIP download is insufficient.
2. Install **Unity 6000.3.2f1** with Android Build Support and **Blender 4.5.x**
   with `.blend` file association.
3. Open the project in Unity, then open `Assets/Scenes/Osteon.unity` to author
   and test the scene in the Editor.
4. For Quest, use **File → Build Profiles → Meta Quest** with Android ARM64,
   IL2CPP, and minimum SDK 32. The profile builds `Osteon-VR.unity`.
5. For desktop browsers, install Web Build Support and use
   **File → Build Profiles → Web - Desktop - Development → Build and Run**.

See [Setup](Docs/SETUP.md) for the full import, Play Mode, and device workflow.

## Project status

The owner declared Osteon complete on 2026-11-09. The
[project handoff](Docs/RELEASE_HANDOFF.md) records delivered scope and the
verification boundary.

- G3 covers the skull, vertebral column, ribcage, both lower limbs, both upper
  limbs, both pectoral sides, and the pelvic model.
- A Quest 1.2 APK was built and installed on a Quest 3S. Headset interaction
  acceptance and full browser acceptance remain open.
- WebGL uses `Osteon.unity` with desktop input, account-aware quiz requests, and
  bundled narration.
- The separate educator portal is deployed, but browser sign-up, publication,
  and learner flows still need acceptance. See
  [Educator portal](Docs/EDUCATOR_PORTAL.md).

Open risks are in [Known issues](Docs/KNOWN_ISSUES.md).

## Ownership and license

This project belongs to **Mariano Marcos State University, College of Computing
and Information Sciences (MMSU CCIS)**. It is not open source. All rights are
reserved by MMSU CCIS; reuse or distribution outside MMSU-affiliated academic
work requires the university's written permission.

Current point of contact: **Queenee R. Vidad**.
