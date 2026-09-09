# Architecture Overview

> G3 implementation updated on 2026-09-09. See `Docs/G3_INSPECTION.md` for the
> validation record and remaining physical-headset checks.

## Runtime entry point

`Assets/_Recovery/CONTROLLERS MIGRATION.unity` is the only enabled scene in
`ProjectSettings/EditorBuildSettings.asset`. It is also the scene most recently
opened in the local Unity workspace.

The scene name reflects its recovery history; it is the current source of truth
despite the unusual name. `Assets/_Recovery/0 (8).unity` and the other numbered
scenes are historical snapshots.

## Anatomical granularity levels (G0-G3)

The `G` prefix means **granularity**: how narrowly the anatomy is grouped for
viewing and interaction. It is a semantic navigation model, not a mesh
level-of-detail (LOD), graphics-quality, or difficulty setting.

| Level | Anatomical scope | Intended user action | Current implementation |
|---|---|---|---|
| **G0 — Whole skeleton** | The complete skeleton as the overview/entry view | View the complete structure and select a top-level division | Present through `low-poly-skeleton-prefab.fbx` |
| **G1 — Skeletal division** | Axial or appendicular skeleton | Select a division to isolate it and show its overview information | Present through the two `DivisionSelection` roots |
| **G2 — Major bone group** | A major group or region within the selected division—for example skull, vertebral column, thoracic cage, shoulder/upper-limb grouping, lower-limb grouping, or pelvis | Explore the group and select one of its bones | Implemented for the vertebral column; other major groups are not connected to individual-bone inspection |
| **G3 — Individual bone** | One named bone | Select, turn, and tilt an enlarged inspection copy with information and a reference column | Implemented for C1–C7, T1–T12, L1–L5, Sacrum, and Coccyx; other groups remain outside the pilot |

The intended navigation direction is `G0 -> G1 -> G2 -> G3` as the user makes
increasingly specific selections. `AnatomyNavigationController` owns Back
navigation and restores the previous view, transform, and information from history.

The level identifies the anatomical scope even when two levels share a scene or
reuse the same model hierarchy. A feature should not be called complete merely
because an asset exists: its selection, transition, information, and Back path
must all work in the enabled scene.

## Current interaction flow

```text
G0: Full low-poly skeleton
  |-- select Appendicular -> G1: isolate division + show division information
  `-- select Axial -------> G1: isolate division + show division information
                              `-> activate the detailed axial model
                                   `-> select the vertebral-column group
                                        -> G2: activate its group view
                                             `-> select a named bone
                                                  -> G3: inspection + reference column
```

`DivisionSelection` and `ViewTransitionOnSelect` listen to selection events
from child `XRBaseInteractable` components. The enabled scene currently uses
`XRSimpleInteractable` components rather than `XRGrabInteractable` components.

`AnatomyNavigationController` owns model visibility and history in the enabled
scene. `InfoBoardController` presents its title, description, breadcrumb, and
Back label. Division selection and view-transition scripts delegate to navigation
when their serialized navigation reference is assigned; their legacy behavior
remains available in older scenes.

`SkeletonYawRotator` reads the right XR controller's `primary2DAxis` directly
through `UnityEngine.XR.InputDevices`. Instances on the full low-poly, axial,
and vertebral roots rotate those views about their model centers. During G3,
those roots are inactive. Separate turning and tilt values rotate only the
inspection copy; the reference column stays stationary. A and B reset the axes
independently. The controller must return to neutral after entry or reconnection.

## Project-owned scripts

| Script | Responsibility | Status in current scene |
|---|---|---|
| `Assets/Scripts/AnatomyNavigationController.cs` | View history, model visibility, bone selection, information, and G3 controller input | Attached to `AnatomyNavigation` |
| `Assets/Scripts/BoneSelection.cs` | Per-bone selection and first/last-ray hover highlighting | Attached to 26 vertebral-column entries |
| `Assets/Scripts/BoneInspectionDisplay.cs` | Builds centered visual-only inspection and reference copies | Attached to `AnatomyNavigation` |
| `Assets/Scripts/AnatomyInputReservation.cs` | Temporarily reserves conflicting right-controller bindings and restores them on exit | Attached to `AnatomyNavigation` |
| `Assets/Scripts/InfoBoardController.cs` | Information-panel presentation; legacy division behavior when navigation is unassigned | Attached once to `CoachingCardRoot` |
| `Assets/Scripts/DivisionSelection.cs` | Routes child selection to navigation; retains legacy isolation when navigation is unassigned | Attached to the axial and appendicular roots |
| `Assets/Scripts/ViewTransitionOnSelect.cs` | Routes group selection to navigation; retains legacy view swapping when navigation is unassigned | Attached to the axial division and axial detail flow |
| `Assets/Scripts/BoneGroupHoverHighlighter.cs` | Highlights a hovered division/group and restores shared materials on exit or disable | Used before individual-bone selection |
| `Assets/Scripts/SkeletonYawRotator.cs` | Rotates a model about its center with the right thumbstick | Attached to the full, axial, and vertebral model roots |
| `Assets/BonePartGrabRelease.cs` | Shows `BonePartInfo` on grab, hides it on release, then lerps the object back to its original parent/local transform | Not referenced by the current scene; used by older recovery scenes |
| `Assets/Scripts/BonePartInfo.cs` | Stores a serialized part name and anatomical description | Used by G3 and older recovery scenes |
| `Assets/Scripts/AxialDivisionSelection.cs` | Older axial-only selection implementation that hides a configured appendicular object | Not referenced by any scene or prefab |

Project scripts are currently compiled into the default `Assembly-CSharp`
assembly; no project `.asmdef` files are present.

## Scene and asset composition

| Runtime element | Source |
|---|---|
| Full skeleton selection view | `Assets/Art/Models/Skeleton/Low Poly/low-poly-skeleton-prefab.fbx` |
| Axial drill-down view | `Assets/Art/Models/Skeleton/Mid Poly/Skeleton_axial.blend` |
| Vertebral-column drill-down | `Assets/Art/Models/Skeleton/Mid Poly/Axial Bone Groups/VERTEBRAL COLUMN.blend` |
| Room model | `Assets/hospital room.blend` |
| XR rig | `Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab` |
| Information UI | `AnatomyNavigation/Anatomy information panel`, using TextMesh Pro and the existing Back button; controlled by `InfoBoardController` on `CoachingCardRoot` |
| Hover material | `Assets/HighlightMst.mat` |

The current scene depends on Unity VR template prefabs. The obsolete scene
`StepManager` is disabled and its anatomy Back callbacks are removed. Treat
`Assets/VRTemplateAssets/` as an imported dependency; the implementation modifies
scene instances rather than imported template scripts.

## XR and input configuration

- Android XR Management loads `Open XR Loader`.
- OpenXR's Android configuration enables Meta Quest support and Oculus/Meta
  Touch controller profiles.
- Hand Tracking Subsystem and Meta Hand Tracking Aim are enabled in the OpenXR
  settings, and XR Hands 1.7.1 is installed.
- The active scene uses the Unity VR template XR Origin and XRI interactables.
- `SkeletonYawRotator` is explicitly controller-oriented. Installed hand
  packages and enabled features do not, by themselves, prove that every action
  has a working hand-tracking binding.

Controller and hand behavior still require verification on a physical headset.

## UI and selection lifecycle

1. Navigation starts in G0 and installs selection filters on the model interactables.
2. `DivisionSelection` requests G1; the axial detail model represents axial G1.
3. The configured vertebral-group transition requests G2. A division transition
   does not also advance for the same selection event.
4. `BoneSelection` highlights a single bone and requests G3. Navigation records
   G2's transform, clears highlighting, hides the group, and presents mesh copies.
5. G3 reserves conflicting controller bindings while retaining ray/UI selection.
6. Back restores the previous frame and releases G3 input reservations. Selection
   is blocked until the input has been released after every transition.

`Assets/Editor/AnatomySceneSetup.cs` authors explicit bone references and colliders;
`AnatomyValidation` runs automated Play Mode checks. See `Docs/G3_INSPECTION.md`.

## Features that are not implemented in the current scene

- Complete G2 coverage for all major axial and appendicular bone groups
- G3 per-bone grab and return-to-origin behavior
- Socket-based assembly or correctness matching
- A project-owned scoring, progress, or persistence system
- G3 inspection beyond the vertebral-column pilot

Colored socket materials and older grab scripts remain in the repository, but
there is no project-owned socket-matching script and no `XRSocketInteractor` in
the enabled scene. Do not describe assembly matching as a current feature until
it is implemented and verified.

## Maintenance boundaries

- Add project behavior under `Assets/Scripts/` rather than editing
  `Assets/Samples/`.
- Avoid editing `Assets/VRTemplateAssets/` unless a change truly belongs to the
  imported template dependency.
- Once the scene and package set stabilize, consider introducing project-owned
  assemblies such as `Anatomy.Core`, `Anatomy.XR`, and `Anatomy.Editor`.
- When the recovery scene is renamed, update the global build scene list, both
  build profiles, this document, `README.md`, and `Docs/SETUP.md` together.
