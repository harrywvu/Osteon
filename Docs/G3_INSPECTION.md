# Individual-bone inspection (G3)

The enabled scene has G2-to-G3 inspection for the skull, vertebral column,
ribcage, both lower limbs, both upper limbs, both pectoral sides, and the pelvic
girdle: 207 entries in total.

- **Skull:** 29 named bones (22 skull bones, six middle-ear ossicles, hyoid).
  Two teeth meshes remain visible context; the G2 skull uses separate
  low-detail ray colliders.
- **Vertebral column:** C1–C7, T1–T12, L1–L5, sacrum, coccyx. Five `Disk` meshes
  remain visible context.
- **Ribcage:** 24 ribs and the sternum; cartilage remains context.
- **Left lower limb:** 30 bones; **right lower limb:** 30 bones plus two
  sesamoids.
- **Upper limbs:** 30 selectable bones each.
- **Pectoral girdles:** clavicle and scapula per side.
- **Pelvis:** one selectable left hip bone; sacrum, coccyx, and
  obturator-foramen meshes remain context.

Anatomical grouping follows
[OpenStax, *The Skull*](https://openstax.org/books/anatomy-and-physiology-2e/pages/7-2-the-skull).

## Interaction

- In G2, point at a bone to highlight it and preview its name.
- In any multi-bone G2 view, drag **Spread [group]** on the anatomy panel to
  separate or assemble the bones. The one-bone pelvic view has no Spread
  control. Right-stick sideways turns the group around its assembled center.
- Select a bone to open a centered inspection copy with a stationary,
  highlighted reference group beside it.
- The panel shows a breadcrumb, name, introductory description, and controls.
- The bone anchor is at world X **2.477991**, Z **-1.173756**; the reference
  group keeps its 42 cm offset.
- The anatomy, quiz, and Settings panels have a blue **Move** bar. Aim, hold the
  select/grab input, reposition, then release.
- The wall **Settings** launcher changes room surface colors, panel theme, text
  size, and highlight color.
- Panel Back or left X returns one level: bone → G2 group → division → skeleton,
  restoring the previous transform and information. Returning from a bone
  restores that group's spread amount; reopening the group starts assembled.
  Release X before pressing again.

| Control | Action |
|---|---|
| Right thumbstick sideways | Turn the inspected bone, up to 90°/s |
| Right thumbstick vertically | Tilt, up to 60°/s, limited to ±90° |
| Right A | Reset turning |
| Right B | Reset tilt |
| Panel Back or left X | Return one level |
| Left Y in pectoral G2 | Switch right/left pectoral model |
| G2 panel slider (except pelvis) | Spread or assemble the group |
| Right thumbstick sideways in G2 | Turn the assembled or spread group |

Bones start in their authored orientation. The controller must return to neutral
on entry or reconnection; selection must be released between navigation steps.

In the XR Device Simulator, use **Left Controller** + **B** for X and
**N** for Y.

## Implementation

`AnatomyNavigationController` owns view history and selection state.
`DivisionSelection` and `ViewTransitionOnSelect` delegate to it. Both components
on a division root must reference the same controller;
`ViewTransitionOnSelect.Prepare` recovers a missing reference from its sibling,
and scenes without a controller retain legacy behavior. A direct view switch
bypasses history and desynchronizes G3 and Back.

Each selectable mesh has serialized `BonePartInfo`, `BoneSelection`,
`XRSimpleInteractable`, renderer references, and a fitted non-convex
`MeshCollider`. The collider is used for ray selection; these bones have no grab
rigidbody. The G2 skull uses separate low-detail meshes from
`skull_ray_colliders.fbx` for its 29 ray colliders while keeping `skull.fbx` for
display.

`BoneGroupSpreadController` moves each bone object, keeping its mesh, authored
ray collider, and interactable aligned. Runtime group profiles set spread
distance and axis weights. `SkullExplosionController` preserves explicit
directions for central skull bones and ear ossicles. Teeth context hides while
spread and returns when assembled. The G3 reference copy uses assembled
positions while the G2 spread amount is retained for Back. Leaving a group
resets its spread; switching pectoral sides resets the side being left.

If the skull FBX changes, regenerate the collider asset with
`Tools/Build-SkullRayColliders.py` (Blender 4.5), then run
**Anatomy → Optimize skull ray targets** before building.

`BoneInspectionDisplay` builds visual-only copies that share imported meshes.
The original group stays intact and inactive during G3. The inspected bone fits
inside a 30 cm bounding volume; the reference group is 45 cm high. The copies
have no interactables, scripts, or colliders, so they do not capture UI rays or
run duplicate navigation code.

`AnatomyInputReservation` temporarily overrides conflicting right-controller
stick and face-button bindings with an empty path. Tracking, selection, and UI
press bindings remain available. Prior overrides and action states are restored
on exit or disable. The anatomy Back button and left X call the navigation
controller; old coaching-card callbacks are removed and `StepManager` is
disabled.

## Authoring commands

Save work first: these commands open and save the enabled scene and replace
descriptions when rerun.

| Command | Effect |
|---|---|
| **Anatomy → Configure vertebral inspection** | Rebuilds vertebral bindings and navigation references |
| **Anatomy → Configure rib inspection** | Rebuilds 24 ribs and sternum; cartilage stays context |
| **Anatomy → Configure skull inspection** | Connects `G2_skull` and binds 29 bones plus G1/G2 colliders |
| **Anatomy → Configure skull explosion** | Binds the 29 selections and adds centered yaw and slider |
| **Anatomy → Configure left lower limb inspection** | Rebuilds the 30 left lower-limb bindings |
| **Anatomy → Configure girdle inspection** | Rebuilds both pectoral sides and the pelvic binding |
| **Anatomy → Configure remaining limb inspection** | Rebuilds the right lower limb and both upper limbs |

Editor-only catalogs (`VertebralBoneCatalog`, `RibBoneCatalog`,
`SkullBoneCatalog`, `LeftLowerLimbBoneCatalog`, `GirdleBoneCatalog`,
`AppendicularLimbBoneCatalog`) supply initial descriptions. After configuration,
runtime information is serialized on each `BonePartInfo`; rerunning setup
replaces it with the catalog version.

## Validation

Run automated Play Mode checks from a closed project or temporary copy:

```powershell
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Pilot
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>'
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Capture
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Current
```

`-Pilot` configures a focused set; the default run configures and validates all
207 entries without graphics; `-Capture` adds a desktop rendering; `-Current`
validates the saved scene without replacing serialized descriptions. The runner
writes `Logs/G3-validation.json`, returns nonzero on failure, and checks
navigation history, held-selection gating, resets, tilt limits, two-ray hover,
copied geometry, information, input restoration, and scene reload.

`AnatomyBuild.BuildQuest` is the batch Android entry point. It requires ARM64,
IL2CPP, minimum SDK 32, and prebaked collision meshes, and writes
`Builds/Anatomy-G3-development.apk` plus `Logs/G3-build.txt`.

### Quest acceptance pass

1. Walk Axial → Skull, vertebral column (C1), Ribcage (a rib and the sternum),
   Appendicular → each limb, both pectoral sides via left Y, and the pelvic hip
   bone. Release selection between steps.
2. Read each bone's information and target both small and large bones from a
   normal standing position. Confirm the reference highlights only the selected
   bone.
3. Move the right stick diagonally, reach both tilt limits, and test A then B
   after combined movement. Confirm the panel and reference stay stationary and
   the XR origin does not move.
4. Use Back through G2, G1, and G0. Re-enter the same bone and confirm the saved
   G2 orientation and information return.
5. Hold selection during a transition and try both controller selections at
   once. Confirm one transition per fresh press and no stuck hover material.
6. Disconnect/reconnect the right controller during G3. Return stick and A/B to
   neutral, verify controls resume, then leave G3 and confirm locomotion and
   manipulation bindings work again.

## Verification status

The saved scene passed 10,571 Play Mode assertions across all 207 entries on
2026-10-06, covering skull Spread, reassembly, interior-bone targeting, and Back
restoration. A later `-Current` run stopped after 915 assertions on C1
information-panel text overflow; that UI issue still needs a rerun. Android
development APKs build with zero errors, but physical Quest targeting, panel
movement, performance, comfort, and narration remain unverified. Current risks
are in [Known issues](KNOWN_ISSUES.md).

## Content references

Descriptions are short project-authored summaries, not a validated anatomical
curriculum. Review teaching level and model fidelity before classroom use.

- [OpenStax, The Vertebral Column](https://openstax.org/books/anatomy-and-physiology-2e/pages/7-3-the-vertebral-column)
- [NCBI Bookshelf, Cervical Vertebrae](https://www.ncbi.nlm.nih.gov/books/NBK459200/)
- [NCBI Bookshelf, Thoracic Vertebrae](https://www.ncbi.nlm.nih.gov/books/NBK459153/)
- [NCBI Bookshelf, Lumbar Vertebrae](https://www.ncbi.nlm.nih.gov/books/NBK459278/)
- [NCBI Bookshelf, Sacral Vertebrae](https://www.ncbi.nlm.nih.gov/books/NBK551653/)
- [OpenStax, The Thoracic Cage](https://openstax.org/books/anatomy-and-physiology-2e/pages/7-4-the-thoracic-cage)
- [OpenStax, Bones of the Lower Limb](https://openstax.org/books/anatomy-and-physiology-2e/pages/8-4-bones-of-the-lower-limb)
- [OpenStax, Bones of the Upper Limb](https://openstax.org/books/anatomy-and-physiology-2e/pages/8-2-bones-of-the-upper-limb)
- [OpenStax, The Pectoral Girdle](https://openstax.org/books/anatomy-and-physiology-2e/pages/8-1-the-pectoral-girdle)
- [OpenStax, The Pelvic Girdle and Pelvis](https://openstax.org/books/anatomy-and-physiology-2e/pages/8-3-the-pelvic-girdle-and-pelvis)
