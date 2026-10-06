# Individual-bone inspection

The enabled controller-migration scene has G2-to-G3 inspection for the axial
skull, vertebral column, and ribcage, plus both appendicular lower limbs, both upper
limbs, both pectoral sides, and the pelvic girdle. The vertebral
column has C1–C7, T1–T12, L1–L5, Sacrum, and Coccyx. The ribcage has 24
individual ribs and the sternum. The left lower limb has 30 selectable bones:
femur, patella, tibia, fibula, 7 tarsals, 5 metatarsals, and 14 toe phalanges.
Each pectoral side has a clavicle and scapula. The pelvic model has one selectable
left hip bone; its sacrum, coccyx, and obturator-foramen meshes remain context.
The right lower-limb model has the standard 30 bones plus two sesamoids. Each
upper limb has 30 selectable bones.
The G2 skull model has 29 named selectable bone meshes: the 22 skull bones,
six middle-ear ossicles, and the hyoid in the upper neck. Its two teeth meshes
remain visible context and are not selectable bones.
Anatomical grouping follows [OpenStax, *The Skull*](https://openstax.org/books/anatomy-and-physiology-2e/pages/7-2-the-skull).
The vertebral model's five
`Disk` meshes and the rib model's cartilage stay visible as context and are not
selectable bones.

## Interaction

- In G2, point at a bone to highlight it and preview its name on the information panel.
- In Skull G2, drag **Spread skull** on the anatomy panel to move all 29 bones
  outward or return them to their assembled positions. Right-stick sideways
  turns the skull around its assembled center so interior bones can be targeted.
- Select using the existing XRI selection binding to open a centered inspection copy.
- A stationary reference group remains beside the inspected bone and highlights its location.
- The information panel shows a breadcrumb, name, introductory description, and controls.
- The bone inspection anchor is at world X **2.477991**, Z **-1.173756**;
  the reference group keeps its 42 cm offset and both retain their height.
- The anatomy and quiz panels have a blue **Move** bar below their controls.
  Aim at a bar, hold the controller's select/grab input while positioning the
  panel, then release. The quiz launcher moves with its panel.
- The panel Back button or left X returns one level: bone → its G2 group → axial or appendicular division → whole skeleton.
  The previous view's position, rotation, and scale are restored.
  Returning from a skull bone also restores its spread amount; leaving Skull G2
  and opening it again starts assembled.
  Release X before pressing again. In the XR Device Simulator, select **Left Controller**
  and press **B** for X.
- In pectoral G2, press left Y to switch between the right and left models without
  adding a history level. Release Y before switching again.
  In the XR Device Simulator, select **Left Controller** and press **N** for Y.

| G3 control | Action |
|---|---|
| Right thumbstick sideways | Turn the inspected bone at up to 90°/second |
| Right thumbstick vertically | Tilt at up to 60°/second, limited to ±90° |
| Right A button | Reset turning independently |
| Right B button | Reset tilt independently |
| Panel Back or left X | Return one G level |
| Left Y in pectoral G2 | Switch between right and left pectoral models |
| Skull G2 panel slider | Spread or assemble the 29 skull bones |
| Right thumbstick sideways in Skull G2 | Turn the assembled or spread skull |

Each bone starts in the model's authored anatomical orientation. The controller
must return to neutral on entry or reconnection before inspection movement starts.
Selection must be released between navigation steps. Earlier views retain their
existing right-thumbstick yaw behavior.

## Implementation

`AnatomyNavigationController` owns view history and selection state. Existing
`DivisionSelection` and `ViewTransitionOnSelect` instances delegate to it.
In the enabled scene, both components on a division root must use the same
navigation controller. `ViewTransitionOnSelect.Prepare` recovers a missing
reference from its sibling `DivisionSelection`; historical scenes without a
controller keep their legacy behavior and existing script GUIDs. A direct
view switch bypasses navigation history and leaves G3 and Back out of sync.

Each selectable skull, vertebral, ribcage, limb, or girdle mesh has serialized
`BonePartInfo`, `BoneSelection`, `XRSimpleInteractable`, renderer references,
and a fitted non-convex `MeshCollider`.
The mesh collider is used for ray selection; these bones have no grab rigidbody.
The G2 skull uses separate low-detail meshes from `skull_ray_colliders.fbx` for
its 29 ray colliders. The visible `skull.fbx` remains full detail for G2 and G3.
`SkullExplosionController` moves each bone object, which keeps its visual mesh,
authored ray collider, and XRI interactable aligned. Its directions are based on
the assembled skull with explicit directions for central bones and ear ossicles.
The two teeth context renderers hide while spread and return when assembled.
The G3 reference copy uses assembled source positions while the G2 spread amount
is held for Back navigation.
If the skull FBX changes, regenerate the collider asset and run
`Tools/Build-SkullRayColliders.py` with Blender 4.5, then run
**Anatomy → Optimize skull ray targets** before building.

`BoneInspectionDisplay` constructs visual-only copies sharing imported meshes.
The original group stays intact and inactive during G3. The inspected bone fits
inside a 30 cm bounding volume; the reference group is 45 cm high. Both use
editable scene anchors. There are no interactables, scripts, or colliders on the
copies, so they do not capture UI rays or run duplicate navigation code.

`AnatomyInputReservation` temporarily overrides conflicting right-controller
thumbstick and face-button bindings with an empty path. Tracking, selection, and
UI press bindings remain available. Prior binding overrides and affected action
enabled states are restored on exit or disable. This also prevents the template
input mediator from reviving a conflicting binding by re-enabling its action.

The active scene's anatomy Back button and left X input call the navigation controller.
Its old coaching-card callbacks are removed, and the unused scene `StepManager`
is disabled. The controller now presents a scene-owned world-space panel beside
the display. The panel Canvas sits inside a movable station; only the Move bar
has a grab collider, so the Back button remains a UI target. Imported template
scripts and historical scenes are unchanged.
Anatomy colliders have explicit, unique interactable owners, including in earlier
views. G2's old whole-column highlighter is disabled in favor of per-bone hover.

## Authoring and validation

Use **Anatomy → Configure vertebral inspection** in Unity 6000.3.2f1 to rebuild
the serialized bone bindings and navigation references. Save your current work
first: the command opens and saves the enabled scene. It reuses existing
components and rejects missing or duplicate source mesh names.

Use **Anatomy → Configure rib inspection** to rebuild only the ribcage G3
bindings. It selects 24 imported rib meshes and the sternum and leaves cartilage
as context. The `RibBoneCatalog` supplies initial descriptions; rerunning setup
replaces descriptions on those entries.

Use **Anatomy → Configure skull inspection** to connect the G1 skull group to
the imported `G2_skull` model and bind its 29 named bones for G3. It authors
the skull's G1 and G2 collider targets in the scene for Quest builds and
replaces descriptions from `SkullBoneCatalog` when rerun.
Use **Anatomy → Configure skull explosion** to bind the 29 existing selections,
add the centered G2 yaw control, and add the conditional anatomy-panel slider
without replacing the skull models or colliders.

Use **Anatomy → Configure left lower limb inspection** to rebuild only the 30
left lower-limb G3 bindings. `LeftLowerLimbBoneCatalog` supplies introductory
descriptions; rerunning setup replaces descriptions on those entries.

Use **Anatomy → Configure girdle inspection** to rebuild both pectoral sides
and the pelvic G3 binding. `GirdleBoneCatalog` supplies their introductory
descriptions; rerunning setup replaces them.

Use **Anatomy → Configure remaining limb inspection** to rebuild the right
lower limb and both upper limbs. `AppendicularLimbBoneCatalog` supplies their
introductory descriptions; rerunning setup replaces them.

The editor-only `VertebralBoneCatalog` supplies the initial descriptions. Once
configured, runtime information is serialized on each bone's `BonePartInfo`.
Running setup again replaces those descriptions with the catalog versions.

Run automated Play Mode checks in a closed project or a temporary project copy:

```powershell
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Pilot
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>'
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Capture
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Current
```

The first command configures C1, all 29 skull entries, all 25 ribcage entries, both lower limbs,
both upper limbs, and the five girdle entries. The second configures and
validates all 207 entries without graphics.
The third captures a desktop rendering when a graphics device is available.
`-Current` validates the already
configured scene without replacing its serialized bone descriptions.
The runner writes `Logs/G3-validation.json` and its Unity log; failures return
a nonzero exit. It checks navigation history, held-selection gating, independent
resets, tilt limits, two-ray hover, copied geometry and bounds, information,
input restoration, appendicular compatibility, repeated entry, and scene reload.
Selection tests invoke XRI manager events through the scene's real selection
callbacks, including the appendicular G0 division target; Back tests invoke
the existing button's navigation callback. See the appendicular incident in
[`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) for its negative control and repair steps.

`AnatomyBuild.BuildQuest` is the batch entry point for an Android development APK.
It requires the pinned ARM64, IL2CPP, minimum SDK 32, and prebaked collision
mesh settings and writes
`Builds/Anatomy-G3-development.apk` plus `Logs/G3-build.txt`.

### Quest acceptance pass

1. Open the controller-migration scene or install its development APK. Select
   Axial, then Skull and a visible skull bone; repeat through the vertebral
   column and C1, then through Ribcage with a rib
   and the sternum, then through Appendicular → Left lower limb with a limb bone.
   Inspect both pectoral sides using left Y, then the pelvic hip bone.
   Repeat through the right lower limb and each upper limb.
   Release selection between steps.
2. Read each bone's information and target both small and large bones from a
   normal standing position. Confirm the reference highlights only the selected bone.
3. Move the right stick diagonally, reach both tilt limits, and test A then B
   after combined movement. Confirm the panel/reference stay stationary and
   the XR origin does not turn, teleport, or jump from those inputs.
4. Use Back through G2, G1, and G0. Repeat the same path and inspect the same
   bone again; confirm the saved G2 orientation and information return.
5. Hold selection during a transition and try both controller selections at
   once. Confirm one transition per fresh press and no stuck hover material.
6. Disconnect/reconnect the right controller during G3. Return its stick and
   A/B to neutral, then verify controls resume. Leave G3 and confirm the prior
   locomotion/manipulation bindings work again. Repeat with all 207 entries.

## Content references

Descriptions are short project-authored factual summaries. They describe regional
features without claiming that every vertebra has a unique shape or that all
fine landmarks are represented faithfully in this model. Review their teaching
level and the model's anatomical fidelity before classroom use.

- [OpenStax, Anatomy and Physiology 2e, The Vertebral Column](https://openstax.org/books/anatomy-and-physiology-2e/pages/7-3-the-vertebral-column): regional ordering and atlas/axis overview.
- [NCBI Bookshelf, Cervical Vertebrae](https://ncbi.nlm.nih.gov/sites/books/NBK459200/): cervical features.
- [NCBI Bookshelf, Thoracic Vertebrae](https://www.ncbi.nlm.nih.gov/books/NBK459153/): rib articulation and lower thoracic exceptions.
- [NCBI Bookshelf, Lumbar Vertebrae](https://ncbi.nlm.nih.gov/books/NBK459278/): lumbar features.
- [NCBI Bookshelf, Sacral Vertebrae](https://www.ncbi.nlm.nih.gov/books/NBK551653/): sacral structure and connections.
- [OpenStax, Anatomy and Physiology 2e, The Thoracic Cage](https://openstax.org/books/anatomy-and-physiology-2e/pages/7-4-the-thoracic-cage): rib numbering, true/false/floating classification, costal cartilage, and sternum.
- [OpenStax, Anatomy and Physiology 2e, Bones of the Lower Limb](https://openstax.org/books/anatomy-and-physiology-2e/pages/8-4-bones-of-the-lower-limb): lower-limb bone names, location, and regional grouping.
- [OpenStax, Anatomy and Physiology 2e, Bones of the Upper Limb](https://openstax.org/books/anatomy-and-physiology-2e/pages/8-2-bones-of-the-upper-limb): arm, wrist, hand, and digit grouping.
- [OpenStax, Anatomy and Physiology 2e, The Pectoral Girdle](https://openstax.org/books/anatomy-and-physiology-2e/pages/8-1-the-pectoral-girdle): clavicle and scapula anatomy.
- [OpenStax, Anatomy and Physiology 2e, The Pelvic Girdle and Pelvis](https://openstax.org/books/anatomy-and-physiology-2e/pages/8-3-the-pelvic-girdle-and-pelvis): hip bone and pelvic context.

## Verification record

On 2026-10-06, Skull G2 gained the Spread skull slider, centered yaw, 29-bone
outward motion, and exact reassembly. G3 retains the spread setting for Back
while its reference copy uses assembled positions. The saved enabled scene
passed **10,571 Play Mode assertions across all 207 G3 entries** in an isolated
Unity 6000.3.2f1 project copy. The added checks exercised the slider callback,
all 29 bone movements, slider sizing, teeth context visibility, a spread interior-bone ray
target and selection, assembled G3 reference pose, Back restoration, and full
reassembly on exit. Physical Quest targeting, UI dragging, performance, and
comfort remain unverified.
The ARM64 IL2CPP Android development build completed with zero errors and
produced `Builds/Anatomy-skull-explosion-Quest.apk` (SHA-256
`F3178079BBBB2816C3E72AF4E10DB553A00113C876F0390CA9AE47D5E270B673`).
ADB showed no connected Quest, so installation and headset interaction were
not checked in this run.

On 2026-10-01, the appendicular division's transition was connected to
navigation and given a runtime fallback to its sibling `DivisionSelection`.
The previous null-reference state failed the real XRI callback test at G0→G1;
the saved scene and the fallback against that old scene each passed **10,535
assertions across 207 G3 entries**. A fresh Android development APK built with
zero errors at `Builds/Anatomy-G3-navigation-fix.apk`. Physical Quest
interaction with this APK remains unverified. The full diagnosis and repair
procedure are in [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md).

On 2026-10-01, the new `G2_skull` model was connected to the G1 skull group.
The 29 named bone meshes have serialized G3 selection targets; the two teeth
meshes remain context. The G1 skull group has 30 authored collider targets,
and both Meta Quest build profiles prebake collision meshes. Unity 6000.3.2f1
Play Mode passed **10,376 assertions across 207 G3 entries**, including the
skull's G1 raycast, hover, selection, G3 display, and Back path. Physical Quest
interaction remains to be checked. The ARM64 IL2CPP Android development build
completed with zero errors and produced `Builds/Anatomy-G3-skull-Quest.apk`.

On 2026-10-01, Quest testing found that controller rays passed through the
ribcage and appendicular G1 groups. Their collider targets were being created
at runtime from non-readable imported meshes. The scene now stores 140 G1 ray
targets, and Quest build settings prebake collision meshes. Unity 6000.3.2f1
Play Mode passed **9,947 assertions across 178 G3 entries**, including physics
raycasts for the G1 groups. The rebuilt APK still needs a headset check.

On 2026-10-01, the anatomy Canvas grab component moved to a parent station
and left X gained a Back binding. Unity 6000.3.2f1 Play Mode passed **9,783
assertions across 178 G3 entries**, including simulated X navigation, a held
button staying at one level, and a separate Back UI target. Physical
Quest targeting and panel movement remain unverified.

On 2026-10-01, the G3 display moved to X 2.477991, Z -1.173756, with the
reference offset preserved. The anatomy and quiz panels gained separate XR
grab handles below their controls. Unity 6000.3.2f1 Play Mode passed **9,780
assertions across 178 G3 entries**, including anchor spacing and both handle
configurations. Physical Quest grabbing and panel comfort remain unverified.

On 2026-10-01, the active scene gained G3 inspection for the right lower limb
(32 imported meshes, including two sesamoids) and both upper limbs (30 bones
each). Unity 6000.3.2f1 Play Mode passed **9,774 assertions across 178 entries**.
The new checks cover G1→G2 XRI selection, per-bone hover and G3 selection,
information fit, reference geometry and highlighting, and Back restoration.
Physical Quest targeting and APK packaging remain unverified.

On 2026-10-01, the active scene gained G3 inspection for each pectoral
clavicle and scapula and the pelvic model's left hip bone. The appendicular
Right pectoral G2 view switches between sides with left Y, without changing the
Back depth. Unity 6000.3.2f1 Play Mode passed **8,197 assertions across 86
entries**, including the new XRI hover/selection, reference geometry, bone
highlighting, information fit, side switching, and Back paths. Controller Y
feel, targeting, and APK packaging remain unverified on Quest.

On 2026-10-01, the pectoral switch gained an Input System binding so the XR
Device Simulator's left-controller **N** key drives Y. A simulated left Y press
switched the G2 model during the full 178-entry Play Mode run, which passed
9,774 assertions. Physical Quest input still needs verification.

On 2026-09-30, the enabled scene gained G3 inspection for all 30 bones in the
left lower-limb model. The `-Current` Play Mode suite in Unity 6000.3.2f1
passed **8,100 assertions across 81 entries**. It exercised each new bone's
XRI hover and selection, information fit, inspection and reference geometry,
reference highlighting, Back history, and G2 transform restoration. Physical
Quest targeting and a packaged APK remain unverified.

On 2026-09-30, the active scene gained G3 inspection for the 24 ribs and
sternum, sharing the vertebral path's hover preview, enlarged inspection,
stationary highlighted reference, right-stick turning/tilt, A/B resets, input
reservation, and Back history. Unity 6000.3.2f1 compiled the changes and the
`-Current` Play Mode suite passed **7,153 assertions across 51 entries**. The
checks exercised every ribcage entry's XRI hover/selection callback, reference
highlighting, information fit, geometry, transform restoration, and Back path.
Physical Quest targeting and a packaged APK remain unverified.

On 2026-09-24, the G3 commits were recovered from the pushed
`codex/g3-bone-inspection` branch and fast-forwarded onto local and remote
`master`. Before recovery, the default-branch scene contained no per-bone
`BoneSelection` components or `AnatomyNavigationController`, which explained
the missing vertebral hover and selection behavior on a new laptop.

The full validation was repeated in an isolated worktree with Unity 6000.3.2f1.
It again passed **6,604 assertions across all 26 entries**, including actual XRI
selection callbacks, two-ray hover and material restoration, Back navigation,
input restoration, and scene reload. Project assemblies compiled without C#
errors; the known package assembly-version and editor-integration warnings were
still present.

On 2026-09-09, C1 passed the initial Play Mode pilot. The final all-bone run in
Unity 6000.3.2f1 passed **6,604 assertions across 26 entries**, including actual
XRI selection callbacks, two-ray hover, clearing both hovers during selection,
Back-button callbacks, input override/state restoration, and scene reload.
No anatomy-script runtime errors were recorded. The generated scene was copied
back from an isolated validation project; the open working editor was not closed.

The desktop C1 rendering was visually reviewed and the panel checked for text
overflow for all 26 descriptions. The display was raised to a 1.35 m center and
the information panel placed alongside it. Local artifacts are
`Logs/G3-validation.json`, `Logs/G3-validation-editor.log`, and `Logs/G3-C1.png`.
These logs and renders are intentionally outside version control.

The batch editor also emitted exceptions from the imported XR Device Simulator
UI (no resolved keyboard control) and Unity Editor Search, plus a disconnected
editor-integration WebSocket and package assembly-version warnings. Those are
outside the anatomy checks and remain documented in `KNOWN_ISSUES.md`.

Physical Quest targeting, panel comfort, control feel, controller reconnection,
and anatomical-content review still require separate verification. ADB reported
no connected devices. Desktop checks do not establish headset comfort or
complete hand-tracking support.

The automated Android development build completed ARM64 native compilation but
failed during APK packaging. Gradle reported `Unable to establish loopback
connection`; the Meta manifest callback also reported a missing generated
`xrmanifest.androidlib` intermediate manifest. No successful APK or device
installation was verified in that run. Details remain in the local validation
copy's `Logs/G3-android.log` and `Logs/G3-build.txt`. No further build was run
for the commit and documentation update.

## User review and next steps — 2026-09-09

The user reported that the result matched the expected output but needed
substantial polish. Priorities are stationary use, viewpoint turning, consistent
model placement, and controller grabbing. The chosen future behavior is smooth
viewpoint turning, controller-based grabbing, and bones staying where released.
These are planned changes, not features of the current G3 implementation.

See [Plans and Features](../Plans%20and%20Features/README.md) for the dated plans
and feature status. The user's review does not establish a complete headset
acceptance pass or a successful automated APK build.
