# Individual-bone inspection

The vertebral-column pilot extends the enabled controller-migration scene from
G2 to G3. The selectable entries are C1–C7, T1–T12, L1–L5, Sacrum, and Coccyx.
The model's five `Disk` meshes remain visible context and are not selectable bones.

## Interaction

- In G2, point at a bone to highlight it and preview its name on the information panel.
- Select using the existing XRI selection binding to open a centered inspection copy.
- A stationary reference column remains beside the inspected bone and highlights its location.
- The information panel shows a breadcrumb, name, introductory description, and controls.
- Back returns one level: bone → vertebral column → axial division → whole skeleton.
  The previous view's position, rotation, and scale are restored.

| G3 control | Action |
|---|---|
| Right thumbstick sideways | Turn the inspected bone at up to 90°/second |
| Right thumbstick vertically | Tilt at up to 60°/second, limited to ±90° |
| Right A button | Reset turning independently |
| Right B button | Reset tilt independently |
| Panel Back | Return to the vertebral column |

Each bone starts in the model's authored anatomical orientation. The controller
must return to neutral on entry or reconnection before inspection movement starts.
Selection must be released between navigation steps. Earlier views retain their
existing right-thumbstick yaw behavior.

## Implementation

`AnatomyNavigationController` owns view history and selection state. Existing
`DivisionSelection` and `ViewTransitionOnSelect` instances delegate to it only
when their navigation reference is assigned. Historical scenes keep their legacy
behavior and existing script GUIDs.

Each selectable mesh has serialized `BonePartInfo`, `BoneSelection`,
`XRSimpleInteractable`, renderer references, and a fitted non-convex `MeshCollider`.
The mesh collider is used for ray selection; these bones have no grab rigidbody.

`BoneInspectionDisplay` constructs visual-only copies sharing imported meshes.
The original group stays intact and inactive during G3. The inspected bone fits
inside a 30 cm bounding volume; the reference column is 45 cm high. Both use
editable scene anchors. There are no interactables, scripts, or colliders on the
copies, so they do not capture UI rays or run duplicate navigation code.

`AnatomyInputReservation` temporarily overrides conflicting right-controller
thumbstick and face-button bindings with an empty path. Tracking, selection, and
UI press bindings remain available. Prior binding overrides and affected action
enabled states are restored on exit or disable. This also prevents the template
input mediator from reviving a conflicting binding by re-enabling its action.

The active scene's anatomy Back button belongs only to the navigation controller.
Its old coaching-card callbacks are removed, and the unused scene `StepManager`
is disabled. The controller now presents a scene-owned world-space panel beside
the display. Imported template scripts and historical scenes are unchanged.
Anatomy colliders have explicit, unique interactable owners, including in earlier
views. G2's old whole-column highlighter is disabled in favor of per-bone hover.

## Authoring and validation

Use **Anatomy → Configure vertebral inspection** in Unity 6000.3.2f1 to rebuild
the serialized bone bindings and navigation references. Save your current work
first: the command opens and saves the enabled scene. It reuses existing
components and rejects missing or duplicate source mesh names.

The editor-only `VertebralBoneCatalog` supplies the initial descriptions. Once
configured, runtime information is serialized on each bone's `BonePartInfo`.
Running setup again replaces those descriptions with the catalog versions.

Run automated Play Mode checks in a closed project or a temporary project copy:

```powershell
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Pilot
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Capture
```

The first command configures and validates C1. The second configures all 26
entries and captures a desktop rendering when a graphics device is available.
The runner writes `Logs/G3-validation.json` and its Unity log; failures return
a nonzero exit. It checks navigation history, held-selection gating, independent
resets, tilt limits, two-ray hover, copied geometry and bounds, information,
input restoration, appendicular compatibility, repeated entry, and scene reload.
Selection tests invoke XRI manager events through the scene's real selection
callbacks; Back tests invoke the existing button's navigation callback.

`AnatomyBuild.BuildQuest` is the batch entry point for an Android development APK.
It requires the pinned ARM64, IL2CPP, and minimum SDK 32 settings and writes
`Builds/Anatomy-G3-development.apk` plus `Logs/G3-build.txt`.

### Quest acceptance pass

1. Open the controller-migration scene or install its development APK. Select
   Axial, then the vertebral column, then C1. Release selection between steps.
2. Read the C1 information and target both small and large bones from a normal
   standing position. Confirm the reference highlights only the selected bone.
3. Move the right stick diagonally, reach both tilt limits, and test A then B
   after combined movement. Confirm the panel/reference stay stationary and
   the XR origin does not turn, teleport, or jump from those inputs.
4. Use Back through G2, G1, and G0. Repeat the same path and inspect the same
   bone again; confirm the saved G2 orientation and information return.
5. Hold selection during a transition and try both controller selections at
   once. Confirm one transition per fresh press and no stuck hover material.
6. Disconnect/reconnect the right controller during G3. Return its stick and
   A/B to neutral, then verify controls resume. Leave G3 and confirm the prior
   locomotion/manipulation bindings work again. Repeat with all 26 entries.

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

## Verification record

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
