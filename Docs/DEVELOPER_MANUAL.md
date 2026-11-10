# Osteon developer manual

This is the starting point for engineers working on the Unity application and its
two Python services. It describes the source in this checkout. Test and deployment
claims are recorded separately because code presence does not establish device or
browser acceptance.

## 1. Repository and runtime

Osteon is a single-scene anatomy explorer for Meta Quest Android and desktop
WebGL. `Assets/Scenes/Osteon.unity` is the only enabled scene in
`ProjectSettings/EditorBuildSettings.asset`. `Assets/Editor/AnatomySceneSetup.cs`
also names this scene, and the Quest and optimized WebGL build scripts use that
constant. `Assets/_Recovery/` contains historical scenes, not the build entry.

| Area | Source of truth | Purpose |
|---|---|---|
| Unity runtime | `Assets/Scripts/` and `Assets/Scenes/Osteon.unity` | Anatomy, input, world-space panels, accounts, quiz client, and narration |
| Unity editor | `Assets/Editor/` | Scene authoring, validation, and build recipes |
| Quest settings | `ProjectSettings/`, `Assets/Settings/Build Profiles/` | Android, XR, and build configuration |
| WebGL runner | `Tools/Start-WebDesktop.ps1` | Serves the optimized local build on loopback |
| Accounts API | `Backend/accounts-api/` | Profile codes, bearer sessions, and account persistence |
| Quiz API | `Backend/quiz-api/` | Questions, scoring, mastery, optional AI feedback, and persistence |
| Content | `Assets/Art/`, `Assets/Ai Voice/`, `Assets/Resources/` | Models, recordings, and runtime configuration |

The Unity version is **6000.3.2f1** (`ProjectSettings/ProjectVersion.txt`).
URP, OpenXR, Meta XR, and XRI versions are pinned in `Packages/manifest.json`.
Install Blender 4.5.x before importing the live `.blend` assets, and resolve Git
LFS objects before opening Unity. Follow [Setup](SETUP.md) for the exact tools,
clone steps, import checks, and Quest build settings.

## 2. Anatomy architecture

`G0` through `G3` are anatomical *granularity* levels, not graphics LODs:

```text
G0 whole skeleton → G1 axial or appendicular division
                 → G2 bone group → G3 individual bone inspection
```

`AnatomyNavigationController` owns the active view, history stack, Back, and G3
input. `DivisionSelection` routes G0 division selection to it;
`ViewTransitionOnSelect` routes G1 group selection. Each G0 division root must
have **both** components pointing to the same navigation controller. Otherwise
a legacy direct transition can display a model without recording history.
`BoneSelection` handles G3 ray hover and selection. The inspection display
creates an enlarged copy and a stationary reference group. Returning from G3
restores the preceding G2 transform and Spread value.

The configured G3 set has 207 entries across the skull, vertebral column,
ribcage, both lower limbs, both upper limbs, both pectoral sides, and pelvis.
Nine multi-bone G2 views have Spread; the single-bone pelvic view does not.
G1 Quest ray targets must be authored in the scene, with Android **Prebake
Collision Meshes** enabled. Runtime mesh collider creation was insufficient on
the tested Quest build. See [Architecture](ARCHITECTURE.md),
[G3 inspection](G3_INSPECTION.md), and [Adding bone groups](ADDING_BONE_GROUPS.md)
before changing this path.

`SkeletonYawRotator` reads controller input for G0–G2. G3 uses separate turn and
tilt values, with A and B resetting them independently. On WebGL,
`AnatomyDesktopInput` is created after scene load and supplies mouse and
keyboard input through the same navigation components. It does not run in
Windows Editor Play Mode. See [Desktop WebGL](WEB_DESKTOP.md) for controls.

## 3. Panels, preferences, and narration

`InfoBoardController` presents anatomy text and Back. The quiz station,
Settings, and Field Guide are created at runtime by `OsteonQuizDemo`,
`OsteonSettings`, and `AnatomyTutorial`. `PanelMoveHandle` supplies XR Move bars;
the desktop input script supports dragging them with the mouse. Settings saves
room color, panel theme, text size, and highlight color locally. The first-run
guide stores completion or skip state in local preferences.

`PanelNarration` adds a Read / Stop tab to the anatomy, quiz, Settings, and
Field Guide panels. It first matches current text to `NarrationClips` recordings
from `Assets/Resources/NarrationClips.asset`. Missing clips use offline device
speech on Android or a Windows Editor voice; WebGL has no speech fallback.
Content changes stop playback. For the clip map, validation menu, and remaining
audio coverage, see [Panel narration](TTS.md).

## 4. Accounts and quiz data flow

The runtime account panel uses `HttpLearnerAccounts` by default. It sends HTTPS
requests to the accounts API and retains the bearer token in memory only. The
quiz client reads its public base URL from
`Assets/Resources/QuizServiceConfig.json`, attaches the current account token,
and calls the quiz API. The quiz Lambda verifies that token with the accounts
API on each protected request, then reads or writes that learner's DynamoDB
session. The account code and token are credentials: keep them out of source,
docs, screenshots, and build assets.

The quiz backend packages a 50-question CSV and an ONNX mastery model. It scores
on the server, stores answer attempts for retry safety, and can request Gemini
explanations and bounded difficulty advice when separately enabled. The Gemini
key belongs in AWS Secrets Manager, not the Unity player. The directory
`Osteon_Agentic_AI_Pipeline/` is a local prototype, not the deployed service.
See [User accounts](USER_ACCOUNTS.md), [Quiz integration](QUIZ_INTEGRATION.md),
the [AI pipeline](QUIZ_AI_PIPELINE.md), and each backend README for contracts and
deployment details. `Docs/ACCOUNTS.md` covers human device and release access.

## 5. Make a change safely

1. Start from a clean or intentionally reviewed worktree. Preserve `.meta`
   files when moving Unity assets. Open the enabled scene and wait for import
   and compilation.
2. Change project-owned code under `Assets/Scripts/` or `Assets/Editor/`.
   Avoid editing `Assets/Samples/` and `Assets/VRTemplateAssets/` for app behavior.
3. For anatomy changes, wire scene references and colliders, then save the
   scene. The Anatomy setup menu can replace bone descriptions and save the
   active scene, so save other work first.
4. Run the relevant checks below. Record what ran and what remains unverified;
   a successful compile or APK build is not a Quest interaction pass.
5. Update the appropriate task page, [Known issues](KNOWN_ISSUES.md), and this
   manual if behavior or the supported workflow changes.

### Verification commands

Run Unity validation from a temporary project copy or worktree while the
original project is closed or open elsewhere. `-Current` validates the saved
scene; `-Pilot` is a focused anatomy run. The script requires the pinned Editor.

```powershell
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>' -Current
./Tools/Invoke-AnatomyValidation.ps1 -ProjectPath '<validation-project>'
python -m unittest discover -s Backend/quiz-api -p 'test_*.py' -q
```

For account backend test dependencies and its test command, follow
[`Backend/accounts-api/README.md`](../Backend/accounts-api/README.md). The
Windows account client and narration checks are
`./Tools/Test-LearnerAccounts.ps1` and `./Tools/Test-SpeechCleanup.ps1`.
**Tools → Osteon → Validate Narration Clips** checks imported catalog links;
it does not listen to the audio. Use the [Setup smoke test](SETUP.md#5-play-mode-smoke-test)
for Play Mode and a physical Quest for XR interaction changes. Use the
[WebGL acceptance checklist](WEB_DESKTOP.md#browser-acceptance-pass) for desktop
input and browser service calls.

### Build paths

| Target | Build path | Output and acceptance |
|---|---|---|
| Quest | `File → Build Profiles → Meta Quest`, then Build and Run | Android ARM64/IL2CPP, minimum SDK 32; install and test on a headset |
| Desktop debug | `File → Build Profiles → Web - Desktop - Development`, then Build and Run | Development WebGL player for debugging |
| Desktop optimized | Switch to WebGL, then `Tools → Osteon → Build Optimized WebGL` | `Builds/Web Desktop Optimized/`; serve with `./Tools/Start-WebDesktop.ps1` |

The optimized WebGL recipe temporarily removes the old private demo config
from `Assets/Resources/` during packaging and restores it afterward. The live
account path does not require that config. Do not infer a completed browser
acceptance pass from a successful build.

## 6. Status and limits

The project owner declared the delivered project complete on 2026-11-09. The
[project handoff](RELEASE_HANDOFF.md) records the scope and evidence at closure.

The latest documented G3 Quest development APK built successfully with skull
Spread. Later shared Spread, Settings, tutorial, accounts, quiz, and narration
changes have separate evidence and gaps in their respective docs. Some WebGL
room and quiz interactions and cross-device resume have owner-reported checks;
full browser controls, panel behavior, narration, and physical Quest input still
need recorded acceptance. See [Known issues](KNOWN_ISSUES.md) for the current
verification boundary and incident history.

Per-bone grabbing, socket assembly, stationary viewing, and smooth viewpoint
turning are not implemented in the enabled scene. Plans under
[`Plans and Features/`](../Plans%20and%20Features/README.md) are proposals or
work records; use the enabled scene and test results to determine current
functionality.
