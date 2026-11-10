# Osteon

Osteon is a Unity anatomy explorer for Meta Quest and desktop WebGL. Users move
from the whole skeleton (G0) through divisions (G1) and bone groups (G2) to
inspection of 207 individual entries (G3). The enabled scene also includes an in-world information
panel, group highlighting, model rotation, G2 Spread, movable panels,
a first-run Field Guide, Settings, and an optional practice quiz.

## Get started

Developers should begin with the [developer manual](Docs/DEVELOPER_MANUAL.md)
and [documentation map](Docs/README.md).

1. Clone with Git LFS and run `git lfs pull`. The project depends on LFS-managed
   models and media; a ZIP download is insufficient.
2. Install **Unity 6000.3.2f1** with Android Build Support and **Blender 4.5.x**
   with `.blend` file association.
3. Open the project in Unity, then open
   `Assets/Scenes/Osteon.unity`. It is the only enabled build
   scene.
4. For a Quest build, use **File → Build Profiles → Meta Quest**. The target is
   Android ARM64, IL2CPP, minimum SDK 32.

For desktop browsers, install Unity's Web Build Support module and use
**File → Build Profiles → Web - Desktop - Development → Build and Run**. See
[Desktop WebGL](Docs/WEB_DESKTOP.md) for mouse and keyboard controls.

See [Setup](Docs/SETUP.md) for the full import, Play Mode, and device workflow.

## Project status

The project owner declared Osteon complete on 2026-11-09. See the
[project handoff](Docs/RELEASE_HANDOFF.md) for the delivered scope and recorded
verification boundary.

- G3 covers the skull, vertebral column, ribcage, both lower limbs, both upper
  limbs, both pectoral sides, and the pelvic model. Selection and Back are
  connected in the enabled scene.
- The latest documented G3 Android development APK built successfully with
  skull Spread. Later shared Spread, Settings, and Field Guide work still needs
  recorded Play Mode and physical Quest interaction checks.
- The same scene has desktop WebGL input, account-aware quiz requests, and
  bundled narration. Some room and quiz interactions were observed or reported;
  the complete browser acceptance checklist remains open. WebGL has no speech
  fallback for text without a recording.
- Per-bone grabbing and socket assembly are not part of the enabled scene.

## Developer docs

| Document | Use it for |
|---|---|
| [Architecture](Docs/ARCHITECTURE.md) | G0–G3 flow, scene wiring, and script map |
| [Developer manual](Docs/DEVELOPER_MANUAL.md) | Source map, change workflow, build paths, and verification |
| [Project handoff](Docs/RELEASE_HANDOFF.md) | Completion status, delivered scope, and verification boundary |
| [G3 inspection](Docs/G3_INSPECTION.md) | Controls, authoring, and verification record |
| [Desktop WebGL](Docs/WEB_DESKTOP.md) | Browser build, controls, and acceptance checklist |
| [Known issues](Docs/KNOWN_ISSUES.md) | Open risks and troubleshooting history |
| [Quiz integration](Docs/QUIZ_INTEGRATION.md) | Optional quiz API and demo configuration |
| [Educator portal](Backend/educator-portal/README.md) | Local implementation of educator login, question publishing, and learner review; AWS rollout pending |
| [Accounts](Docs/ACCOUNTS.md) | Device and release access |
| [Plans and features](Plans%20and%20Features/README.md) | Proposed work |

## Ownership and license

This project belongs to **Mariano Marcos State University, College of Computing
and Information Sciences (MMSU CCIS)**. It is not open source. All rights are
reserved by MMSU CCIS; reuse or distribution outside MMSU-affiliated academic
work requires the university's written permission.

Current point of contact: **Queenee R. Vidad**.
