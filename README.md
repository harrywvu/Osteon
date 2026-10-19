# Osteon

Osteon is a Unity VR anatomy explorer for Meta Quest. Users move from the whole
skeleton (G0) through divisions (G1) and bone groups (G2) to inspection of 207
individual entries (G3). The enabled scene also includes an in-world information
panel, group highlighting, controller-based rotation, G2 Spread, movable panels,
a first-run Field Guide, Settings, and an optional practice quiz.

## Get started

1. Clone with Git LFS and run `git lfs pull`. The project depends on LFS-managed
   models and media; a ZIP download is insufficient.
2. Install **Unity 6000.3.2f1** with Android Build Support and **Blender 4.5.x**
   with `.blend` file association.
3. Open the project in Unity, then open
   `Assets/_Recovery/CONTROLLERS MIGRATION.unity`. It is the only enabled build
   scene.
4. For a Quest build, use **File → Build Profiles → Meta Quest**. The target is
   Android ARM64, IL2CPP, minimum SDK 32.

See [Setup](Docs/SETUP.md) for the full import, Play Mode, and device workflow.

## Current status

- G3 covers the skull, vertebral column, ribcage, both lower limbs, both upper
  limbs, both pectoral sides, and the pelvic model. Selection and Back are
  connected in the enabled scene.
- The latest G3 Android development APK built successfully with skull Spread.
  Shared G2 Spread, Settings, and the Field Guide compile but still need Play
  Mode and physical Quest interaction checks. Controller and hand paths on
  Quest also remain unverified.
- Per-bone grabbing and socket assembly are not part of the enabled scene.

## Developer docs

| Document | Use it for |
|---|---|
| [Architecture](Docs/ARCHITECTURE.md) | G0–G3 flow, scene wiring, and script map |
| [G3 inspection](Docs/G3_INSPECTION.md) | Controls, authoring, and verification record |
| [Known issues](Docs/KNOWN_ISSUES.md) | Open risks and troubleshooting history |
| [Quiz integration](Docs/QUIZ_INTEGRATION.md) | Optional quiz API and demo configuration |
| [Accounts](Docs/ACCOUNTS.md) | Device and release access |
| [Plans and features](Plans%20and%20Features/README.md) | Proposed work |

## Ownership and license

This project belongs to **Mariano Marcos State University, College of Computing
and Information Sciences (MMSU CCIS)**. It is not open source. All rights are
reserved by MMSU CCIS; reuse or distribution outside MMSU-affiliated academic
work requires the university's written permission.

Current point of contact: **Queenee R. Vidad**.
