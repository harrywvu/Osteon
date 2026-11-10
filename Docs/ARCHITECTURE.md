# Architecture Overview

The [system diagram](SYSTEM_ARCHITECTURE.svg) maps the shared Unity scene, Quest
and WebGL clients, account and quiz services, and build flow.

## Runtime entry point

`Assets/Scenes/Osteon.unity` is the only enabled scene in
`ProjectSettings/EditorBuildSettings.asset`. The `Meta Quest` Android and
`Web - Desktop - Development` WebGL profiles inherit that global scene list.
Numbered scenes under `Assets/_Recovery/` are historical snapshots.

## Anatomical granularity levels (G0–G3)

`G` means **granularity**: how narrowly anatomy is grouped for viewing and
interaction. It is a semantic navigation model, not a graphics LOD or
difficulty setting.

| Level | Scope | User action | Implementation |
|---|---|---|---|
| **G0** | Whole skeleton | View the skeleton and pick a division | `low-poly-skeleton-prefab.fbx` |
| **G1** | Axial or appendicular division | Isolate the division and read its overview | Two `DivisionSelection` roots |
| **G2** | Major bone group | Explore the group and pick a bone | Skull, vertebral column, ribcage, limbs, pectoral girdles, and pelvis continue to G3; nine multi-bone views have Spread |
| **G3** | Individual bone | Turn and tilt an enlarged copy beside a reference group | 207 entries: 29 skull, 26 vertebral, 25 ribcage, 30 left lower limb, 32 right lower limb, 60 upper limb, 4 pectoral, 1 pelvic |

Navigation runs `G0 → G1 → G2 → G3`.
`AnatomyNavigationController` owns Back and restores the previous view,
transform, and information from history. A feature is not complete merely
because an asset exists: selection, transition, information, and Back must all
work in the enabled scene.

## Current interaction flow

```text
G0: full low-poly skeleton
  |-- Appendicular -> G1 division
  |     |-- Left lower limb / Right lower limb / upper limbs -> G2 -> bone -> G3
  |     |-- Right pectoral girdle -> G2 (left Y switches sides) -> bone -> G3
  |     `-- Pelvic girdle -> G2 -> hip bone -> G3
  `-- Axial -> G1 division + detailed axial model
        |-- Vertebral column -> G2 -> named bone -> G3
        |-- Skull -> G2 -> named bone -> G3
        `-- Ribcage -> G2 -> rib or sternum -> G3
```

`DivisionSelection` and `ViewTransitionOnSelect` listen to child
`XRBaseInteractable` selection events; the enabled scene uses
`XRSimpleInteractable`, not `XRGrabInteractable`.

G1 group ray targets are authored into the scene before Quest builds so their
colliders are cooked with the Android player. `ViewTransitionOnSelect` registers
those targets and group highlighting at runtime. Navigation discovers each G2
view from its transition component. See
[Adding bone groups](ADDING_BONE_GROUPS.md).

`AnatomyNavigationController` owns model visibility and history.
`InfoBoardController` presents title, description, breadcrumb, and Back label.
`SkeletonYawRotator` reads the right controller's `primary2DAxis` through
`UnityEngine.XR.InputDevices` for G0–G2; the lower limbs, upper limbs, and
pectoral girdles pivot on visible model bounds. In G3 those roots are inactive,
and separate turn/tilt values rotate only the inspection copy. A and B reset the
axes independently; the controller must return to neutral after entry or
reconnection.

In WebGL, `AnatomyDesktopInput` starts after scene load and uses the camera ray,
mouse, and keyboard for selection and movement through the same navigation
components. Its platform check leaves Windows Editor Play Mode and Quest input
unchanged. See [Desktop WebGL](WEB_DESKTOP.md).

## Project-owned scripts

| Script | Responsibility | Scene status |
|---|---|---|
| `AnatomyNavigationController.cs` | View history, model visibility, bone selection, information, G3 input | On `AnatomyNavigation` |
| `AnatomyDesktopInput.cs` | Mouse/keyboard selection, camera movement, panel dragging, G3 input | WebGL only, runtime-created |
| `BoneSelection.cs` | Per-bone selection and hover highlighting | 207 entries across ten group models |
| `SkullExplosionController.cs` | Skull G2 spread, reassembly, assembled reference poses | On `G2_skull` |
| `BoneGroupSpreadController.cs` | Shared G2 Spread motion and reference poses | Runtime-added to eight views |
| `BoneInspectionDisplay.cs` | Centered inspection copy and stationary reference copy | On `AnatomyNavigation` |
| `AnatomyInputReservation.cs` | Reserves conflicting right-controller bindings and restores them | On `AnatomyNavigation` |
| `InfoBoardController.cs` | Information panel; legacy division behavior when navigation is unassigned | On `CoachingCardRoot` |
| `DivisionSelection.cs` | Routes division selection to navigation | Axial and appendicular roots |
| `ViewTransitionOnSelect.cs` | Routes group selection to navigation | Axial and appendicular group flows |
| `BoneGroupHoverHighlighter.cs` | One highlight material for a hovered group | Before individual-bone selection |
| `SkeletonYawRotator.cs` | Rotates a model about its center with the right stick | Full, axial, vertebral, and ribcage roots |
| `PanelMoveHandle.cs` | Grab bars below anatomy, quiz, and Settings panels | Anatomy authored; quiz and Settings runtime |
| `OsteonSettings.cs` | Wall Settings station and saved preferences | Runtime-created |
| `AnatomyTutorial.cs` | First-run Field Guide, arrows, Skip, replay | Runtime-created |
| `LearnerAccountPanel.cs` | Account create, login, switch, sign-out | Runtime-created with quiz station |
| `HttpLearnerAccounts.cs` | Account requests to the HTTPS service | Default transport on Quest, WebGL, Editor |
| `OsteonQuizDemo.cs` | Practice quiz UI and account-aware API calls | Runtime-created |
| `PanelNarration.cs` | Read/Stop tabs; coordinates one speaking panel | Anatomy, quiz, Settings, Field Guide |
| `NarrationClips.cs` | Looks up bundled recordings by current text | `Assets/Resources/NarrationClips.asset` |
| `OnDeviceSpeech.cs` | Android and Windows Editor speech fallback | No WebGL fallback |
| `BonePartGrabRelease.cs` | Legacy grab, information, return-to-origin | Older recovery scenes only |
| `BonePartInfo.cs` | Serialized bone name and description | G3 and older scenes |
| `AxialDivisionSelection.cs` | Superseded axial-only selection | Unreferenced |

Project scripts compile into `Assembly-CSharp`; there are no project `.asmdef`
files.

## Scene and asset composition

| Runtime element | Source |
|---|---|
| Full skeleton | `Assets/Art/Models/Skeleton/Low Poly/low-poly-skeleton-prefab.fbx` |
| Axial drill-down | `Assets/Art/Models/Skeleton/Mid Poly/Skeleton_axial.blend` |
| Vertebral column | `Assets/Art/Models/Skeleton/Mid Poly/Axial Bone Groups/VERTEBRAL COLUMN.blend` |
| Ribcage | `Assets/Art/Models/Skeleton/Mid Poly/Axial Bone Groups/ribs.fbx` |
| Lower limbs | `G2_LeftLowerLimb.fbx` and `G2_RightLowerLimb.fbx` under `Appendicular Bone Groups/` |
| Upper limbs | `G2_LeftUpperLimb.fbx` and `G2_RightUpperLimb.fbx` |
| Pectoral girdles | `G2_RightPectoralGirdle.fbx` and `G2_LeftPectoralGirdle.fbx` |
| Pelvis | `G2_Pelvic Girdle.fbx` |
| Room | `Assets/hospital room.blend` |
| XR rig | `Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab` |
| Information UI | `AnatomyNavigation/Anatomy information panel` (TextMesh Pro + Back) |
| G3 placement | Bone anchor at world X 2.477991, Z -1.173756; reference offset 42 cm |
| Hover material | `Assets/HighlightMst.mat` |

The scene depends on Unity VR template prefabs. The obsolete scene `StepManager`
is disabled and its anatomy Back callbacks are removed. Treat
`Assets/VRTemplateAssets/` as an imported dependency; change scene instances,
not imported template scripts.

## XR and input configuration

- Android XR Management loads `Open XR Loader`.
- OpenXR Android enables Meta Quest support and Oculus/Meta Touch profiles.
- Hand Tracking Subsystem and Meta Hand Tracking Aim are enabled; XR Hands
  1.7.1 is installed.
- The scene uses the Unity VR template XR Origin and XRI interactables.
- WebGL uses the same camera and colliders with its own mouse and keyboard
  controller.
- `SkeletonYawRotator` is controller-oriented. Installed hand packages do not
  prove a working hand-tracking binding.

Controller and hand behavior require verification on a physical headset.

## Practice quiz service

The quiz menu hosts **Accounts** in all builds. `LearnerAccountPanel` provides
create/login/switch/sign-out screens, and `HttpLearnerAccounts` calls the Sydney
API Gateway/Lambda service backed by DynamoDB. Quest and WebGL share the
endpoint and six-digit account codes. Session tokens stay in memory in
`LearnerAccountState`. The quiz sends that token and verifies it through the
accounts API before each protected request. See
[User accounts](USER_ACCOUNTS.md).

The [AWS diagram](AWS_ARCHITECTURE.md) shows the runtime and deployment paths.
The scene creates an in-world quiz station on the right wall, not attached to
the headset. API Gateway forwards HTTPS requests to a Python Lambda that reads
the packaged question CSV and ONNX model, optionally requests Gemini feedback,
and persists the learner session in DynamoDB. S3 holds the deployment ZIP only.
The training notebook is a local artifact, not a hosted service. See the
[AI pipeline](QUIZ_AI_PIPELINE.md) and
[quiz integration](QUIZ_INTEGRATION.md) for contracts and limits.

## UI and selection lifecycle

1. Navigation starts in G0 and installs selection filters.
2. `DivisionSelection` requests G1; the axial detail model represents axial G1.
3. A group transition requests G2. On each G0 division root,
   `DivisionSelection` and `ViewTransitionOnSelect` must reference the same
   navigation controller; otherwise a legacy direct switch bypasses history.
4. `BoneSelection` highlights one bone and requests G3. Navigation records the
   G2 transform, clears highlighting, hides the group, and presents mesh copies.
5. G3 reserves conflicting controller bindings while keeping ray/UI selection.
6. Back restores the previous frame and releases G3 reservations. Selection is
   blocked until input is released after every transition.

`Assets/Editor/AnatomySceneSetup.cs` authors bone references and colliders;
`AnatomyValidation` runs automated Play Mode checks. See
[G3 inspection](G3_INSPECTION.md).

## Not implemented in the current scene

- Complete G2 coverage for all major bone groups
- Per-bone grab and return-to-origin
- Socket-based assembly or correctness matching
- G3 inspection outside the configured groups

Colored socket materials and older grab scripts remain in the repository, but
there is no socket-matching script and no `XRSocketInteractor` in the enabled
scene.

## Maintenance boundaries

- Add project behavior under `Assets/Scripts/`, not `Assets/Samples/`.
- Avoid editing `Assets/VRTemplateAssets/` unless the change belongs to the
  imported dependency.
- When the entry scene changes, update the build scene list,
  `AnatomySceneSetup.ScenePath`, both build paths, and the developer docs.
