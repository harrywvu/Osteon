# Human Skeletal VR

A VR anatomy training application for Meta Quest 2/3. Users grab individual
bones from a full skeleton in a hospital-room environment; each bone returns
to its starting position on release and can display a title + anatomical
description on an info panel. Colored sockets suggest a bone-assembly /
matching exercise.

This project is maintained by MMSU CCIS students as part of an ongoing
research/practicum handoff. **Read `Docs/SETUP.md` before opening this
project for the first time** — it will save you hours.

## Quick facts

- **Engine:** Unity — exact version pinned in `ProjectSettings/ProjectVersion.txt`
  (verify there; do not assume from memory, this project has been through
  version drift before)
- **Render pipeline:** URP
- **XR stack:** XR Interaction Toolkit, XR Hands, Meta XR SDK, OpenXR
- **Target:** Android (Meta Quest), ARM64, IL2CPP, min SDK 32
- **3D source assets:** Blender 4.5.x (`.blend` files) — see note below
- **UI/Text:** TextMesh Pro

## Entry point

Open scene: `Assets/_Recovery/0 (8).unity`

(Yes, that scene name is unusual — it's the recovered/working scene from a
past maintenance session. Consider renaming it to something clearer once
you're confident it's stable — see `Docs/KNOWN_ISSUES.md`.)

## Before you touch anything

1. Read `Docs/SETUP.md` — exact tool versions and install steps.
2. Read `Docs/ARCHITECTURE.md` — what the main scripts do.
3. Read `Docs/KNOWN_ISSUES.md` — active warnings, unresolved package
   mismatches, and things that look broken but are actually fine (or vice versa).

## Why these docs exist

This project was previously handed off with no setup documentation, an 8GB
working copy full of regenerable cache folders, mismatched package versions,
and a hard dependency on having Blender installed just to *see* the models.
Every fix above exists so the next person doesn't have to rediscover all of
this from scratch.
