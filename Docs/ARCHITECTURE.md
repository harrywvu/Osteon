# Architecture Overview

> Last verified against scripts, package configuration, and the enabled scene
> on 2026-09-08.

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
| **G2 — Major bone group** | A major group or region within the selected division—for example skull, vertebral column, thoracic cage, shoulder/upper-limb grouping, lower-limb grouping, or pelvis | Explore/examine the group and select it for a closer view | Partially implemented: the axial view and vertebral-column transition are wired; the complete group set is not verified |
| **G3 — Individual bone** | One named bone | Explore/examine the bone and display its anatomical information | Not wired into the enabled scene; legacy `BonePartGrabRelease` and `BonePartInfo` provide part of the intended behavior in older recovery scenes |

The intended navigation direction is `G0 -> G1 -> G2 -> G3` as the user makes
increasingly specific selections. Back navigation should move toward the
previous, less granular view. The current Back-button behavior across all model
transitions is still awaiting Play Mode verification.

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

G3: Individual-bone view and information are not connected in this scene.
```

`DivisionSelection` and `ViewTransitionOnSelect` listen to selection events
from child `XRBaseInteractable` components. The enabled scene currently uses
`XRSimpleInteractable` components rather than `XRGrabInteractable` components.

`InfoBoardController` owns the visible division roots and the world-space
TextMesh Pro panel. It starts with "Human Skeleton / Select a division to
start", isolates the selected division, updates the panel, and exposes
`ReturnToDivisionSelection` to the Back button.

`SkeletonYawRotator` reads the right XR controller's `primary2DAxis` directly
through `UnityEngine.XR.InputDevices`. Horizontal thumbstick input rotates the
`SkeletonRotationPivot` GameObject around the world Y axis. The full low-poly
skeleton is parented to that pivot; the axial and vertebral drill-down roots are
not.

## Project-owned scripts

| Script | Responsibility | Status in current scene |
|---|---|---|
| `Assets/Scripts/InfoBoardController.cs` | Singleton-style controller for division visibility, title/description text, panel visibility, and Back-button state | Attached once to `CoachingCardRoot` |
| `Assets/Scripts/DivisionSelection.cs` | Listens to all child interactables; on first selection, delegates division isolation and information display to `InfoBoardController` | Attached to the axial and appendicular roots |
| `Assets/Scripts/ViewTransitionOnSelect.cs` | Activates a configured next view and disables the current view after the first child selection | Attached to the axial division and axial detail flow |
| `Assets/Scripts/BoneGroupHoverHighlighter.cs` | Caches child render materials and replaces them with one highlight material while any child is hovered | Attached three times; one serialized instance has no material assigned |
| `Assets/Scripts/SkeletonYawRotator.cs` | Rotates its pivot with the right thumbstick and exposes an orientation reset method | Attached to `SkeletonRotationPivot`, which parents the full low-poly view |
| `Assets/BonePartGrabRelease.cs` | Shows `BonePartInfo` on grab, hides it on release, then lerps the object back to its original parent/local transform | Not referenced by the current scene; used by older recovery scenes |
| `Assets/Scripts/BonePartInfo.cs` | Stores a serialized part name and anatomical description | Not referenced by the current scene; used by older recovery scenes |
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
| Information UI | `CoachingCardRoot` in the current scene, using TextMesh Pro |
| Hover material | `Assets/HighlightMst.mat` |

The current scene also depends on Unity VR template prefabs and the template's
`StepManager` for coaching-card navigation. Treat `Assets/VRTemplateAssets/` as
an imported dependency: project code may reference it, but changes there are
harder to carry across a template/package refresh.

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

1. `InfoBoardController.Awake` establishes the singleton instance and calls
   `ReturnToDivisionSelection`.
2. `DivisionSelection.Awake` subscribes to `selectEntered` on every child
   interactable, including inactive children.
3. The first child selection calls `InfoBoardController.SelectDivision`, which
   keeps only the chosen division active, updates the information panel, and
   shows the Back button.
4. Where configured, `ViewTransitionOnSelect` handles the same selection event
   and swaps the whole model view.
5. The Back button invokes the template `StepManager.PreviousStep` and
   `InfoBoardController.ReturnToDivisionSelection`.
6. All listener-owning project scripts unregister their listeners in
   `OnDestroy`.

## Features that are not implemented in the current scene

- Complete G2 coverage for all major axial and appendicular bone groups
- G3 per-bone grab and return-to-origin behavior
- G3 per-bone title/description display
- Socket-based assembly or correctness matching
- A project-owned scoring, progress, or persistence system
- Automated project tests

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
