# Osteon

*Greek root for bone*

Osteon (Human Skeletal VR) is a Unity-based anatomy viewer for Meta Quest. The current
build presents a skeleton in a hospital-room environment, lets the user select
the axial or appendicular division, updates an in-world information panel, and
supports drill-down views of the axial skeleton and individual-bone inspection
within the vertebral column.

> G3 implementation updated on 2026-09-09; see the verification record in
> [`Docs/G3_INSPECTION.md`](Docs/G3_INSPECTION.md).

## Current experience

The only enabled build scene is:

`Assets/_Recovery/CONTROLLERS MIGRATION.unity`

The experience is designed around four anatomical granularity levels: **G0**
whole skeleton, **G1** axial/appendicular division, **G2** major bone group or
region, and **G3** individual bone. See the implementation matrix in
[`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md#anatomical-granularity-levels-g0-g3).

In that scene:

- XR Interaction Toolkit `XRSimpleInteractable` components drive selection.
- Hovering a configured division highlights the whole group.
- Selecting a division isolates it and updates the TextMesh Pro information
  panel.
- Selecting the axial division transitions from the full low-poly skeleton to
  `Skeleton_axial.blend`; a further selection transitions to the vertebral
  column view.
- The right controller thumbstick rotates the whole-skeleton and detail views.
- Selecting one of the 26 vertebral-column bones opens an enlarged inspection
  copy beside a stationary reference column. Right-stick sideways turns the
  bone; up/down tilts it. A resets turning and B resets tilt independently.
- Back restores the previous anatomical view and its orientation.

`BonePartInfo` supplies the G3 information. `BonePartGrabRelease` remains a
legacy script in historical scenes; grabbing and assembly are not part of the
current bone-inspection experience. See [`Docs/G3_INSPECTION.md`](Docs/G3_INSPECTION.md)
for controls, authoring, automated checks, and remaining headset verification.

The 2026-09-09 Play Mode run passed 6,604 assertions across all 26 entries.
Automated APK packaging failed with a Gradle loopback-connection error, and
headset acceptance remains pending. Planned stationary viewing, controller
grabbing, and expansion are tracked in
[`Plans and Features`](Plans%20and%20Features/README.md).

## Technical baseline

| Area | Current value |
|---|---|
| Unity | **6000.3.2f1** |
| Render pipeline | URP 17.3.0 |
| XR runtime | OpenXR 1.16.0 on Android |
| Interaction | XR Interaction Toolkit 3.2.1 |
| Meta packages | Meta XR Interaction/Core 83.0.0 |
| Hand tracking package | XR Hands 1.7.1 |
| Target | Android / Meta Quest, ARM64, IL2CPP, minimum SDK 32 |
| Input handling | Unity Input System |
| UI text | TextMesh Pro |

The project contains live `.blend` assets and currently requires Blender 4.5.x
for reliable Unity imports. Large models, textures, and media are tracked with
Git LFS.

## Start here

1. Read [`Docs/SETUP.md`](Docs/SETUP.md) before opening the project on a new
   machine.
2. Read [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) for the scene flow and
   script map.
3. Check [`Docs/KNOWN_ISSUES.md`](Docs/KNOWN_ISSUES.md) before changing package,
   XR, or build settings.
4. Use [`Docs/ACCOUNTS.md`](Docs/ACCOUNTS.md) when preparing a headset or a
   distributable build.

## Repository map

| Path | Purpose |
|---|---|
| `Assets/_Recovery/` | Working scene plus historical recovery snapshots |
| `Assets/Scripts/` | Project-owned interaction and UI scripts |
| `Assets/BonePartGrabRelease.cs` | Legacy per-bone grab/return behavior |
| `Assets/Art/Models/Skeleton/` | Low- and mid-poly anatomy models |
| `Assets/VRTemplateAssets/` | Unity VR template content used by the XR rig and coaching UI |
| `Assets/Samples/` | Imported package samples; do not edit for project behavior |
| `Assets/Settings/Build Profiles/` | Unity 6 Android/Quest build profiles |
| `Packages/manifest.json` | Direct package dependencies |
| `ProjectSettings/` | Unity, Android, XR, and global scene settings |
| `Docs/` | Setup, architecture, accounts, and issue history |

## Ownership

This project is hosted on my personal GitHub for development convenience, but
it is **owned by Mariano Marcos State University, College of Computing and
Information Sciences (MMSU CCIS)**. It was developed as part of an academic
research/practicum initiative, not as personal or independent work. Any reuse,
distribution, or continuation of this project should go through the
university, not just this repository.

## License

This project is **not open source**. All rights are reserved by MMSU CCIS.

No license is granted to copy, modify, distribute, or use this code or its
assets outside of MMSU-affiliated academic work, except with explicit written
permission from the university.

## Handing this off to the next maintainer

If you're inheriting this project:

1. **Get repo access first.** Ask the current maintainer or MMSU CCIS OJT/practicum coordinator to either transfer this repository to you, add you as a collaborator, or point you to wherever the university wants it hosted long-term (a personal account shouldn't be the permanent home of an institution-owned project — flag this if it hasn't already been addressed).
2. **Read `Docs/` in this order:** `SETUP.md` → `KNOWN_ISSUES.md` → `ARCHITECTURE.md`. Don't skip `KNOWN_ISSUES.md` — it exists specifically so you don't re-diagnose problems that already have known fixes.
3. **Do a clean clone test before assuming anything's broken.** Clone fresh, follow `Installation`, and see what actually happens on your machine before troubleshooting — half of past "the project is broken" moments were stale local cache, not real bugs.
4. **Add to `KNOWN_ISSUES.md` as you go**, don't just fix things quietly. The point of these docs is that they compound — every maintainer who documents what they hit makes it faster for the next one.
5. **When you eventually hand it off yourself**, do the same: update ownership contacts below, tag a known-good commit, and don't leave it in a state where the next person has to reverse-engineer what "working" even looks like.

**Current point of contact:** *Queenee R. Vidad*
