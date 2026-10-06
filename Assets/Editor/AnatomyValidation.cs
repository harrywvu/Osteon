using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>Batch Play Mode regression checks without moving legacy scripts into a new assembly.</summary>
[InitializeOnLoad]
public static class AnatomyValidation
{
    private const string RunningKey = "Anatomy.Validation.Running";
    private const string CountKey = "Anatomy.Validation.BoneCount";
    private static IEnumerator checks;
    private static int assertions;
    private static int startedFrame;
    private static readonly List<string> projectErrors = new List<string>();

    static AnatomyValidation()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
    }

    public static void RunPilot()
    {
        AnatomySceneSetup.ConfigurePilot();
        Begin(1);
    }

    public static void RunAll()
    {
        AnatomySceneSetup.ConfigureAll();
        Begin(26);
    }

    public static void RunCurrent()
    {
        EditorSceneManager.OpenScene(AnatomySceneSetup.ScenePath, OpenSceneMode.Single);
        Begin(26);
    }

    private static void Begin(int count)
    {
        SessionState.SetInt(CountKey, count);
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string condition, string stack, LogType type)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
            (stack.Contains("Assets/Scripts/") || stack.Contains("Assets\\Scripts\\") ||
             condition.Contains("Anatomy") || condition.Contains("Unable to inspect")))
            projectErrors.Add(condition + "\n" + stack);
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (Time.frameCount < 3) return;
        try
        {
            if (checks == null)
            {
                startedFrame = Time.frameCount;
                checks = CheckScene();
            }
            if (Time.frameCount == startedFrame) return;
            startedFrame = Time.frameCount;
            if (!checks.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckCore()
    {
        var gate = new AnatomySelectionGate();
        gate.Block(10);
        gate.Poll(false, 10);
        Require(!gate.CanSelect, "Selection must stay blocked in the transition frame.");
        gate.Poll(true, 11);
        Require(!gate.CanSelect, "A held trigger must not advance another view.");
        gate.Poll(false, 12);
        Require(gate.CanSelect, "Releasing the trigger must rearm selection.");
        var pose = new BoneInspectionPose();
        pose.Apply(new Vector2(1, 1), 0.5f, false, false);
        Require(Mathf.Approximately(pose.Turning, -45) && Mathf.Approximately(pose.Tilt, 30), "Stick axis mapping or speed changed.");
        pose.Apply(Vector2.zero, 0, true, false);
        Require(pose.Turning == 0 && pose.Tilt == 30, "A must reset only turning.");
        pose.Apply(new Vector2(-1, 0), 0.5f, false, false);
        pose.Apply(Vector2.zero, 0, false, true);
        Require(pose.Turning == 45 && pose.Tilt == 0, "B must reset only tilt.");
        pose.Apply(new Vector2(0, 1), 100, false, false);
        Require(pose.Tilt == 90, "Positive tilt limit failed.");
        pose.Apply(new Vector2(0, -1), 100, false, false);
        Require(pose.Tilt == -90, "Negative tilt limit failed.");
        pose.Reset();
        pose.Apply(new Vector2(0.1f, -0.1f), 10, false, false);
        Require(pose.Rotation == Quaternion.identity, "Dead zone should not move the bone.");
    }

    private static IEnumerator CheckScene()
    {
        CheckCore();
        CheckInputReservation();
        var nav = UnityEngine.Object.FindFirstObjectByType<AnatomyNavigationController>();
        Require(nav != null && nav.CanNavigate, "Navigation did not initialize.");
        Require(nav.Level == AnatomyLevel.Whole && nav.HistoryCount == 0, "Initial view must be G0.");
        Require(nav.Bones.Length == SessionState.GetInt(CountKey, 26), "Unexpected bone coverage.");
        Require(nav.Bones.Select(b => b.name).Distinct().Count() == nav.Bones.Length, "Duplicate bone bindings.");
        Require(nav.SkullBones != null && nav.SkullBones.Length == 29 &&
                nav.SkullBones.Select(b => b.name).Distinct().Count() == 29,
            "Expected 29 distinct selectable skull bones.");
        Require(nav.RibBones.Length == 25, "Expected 24 selectable ribs and the sternum.");
        Require(nav.RibBones.Select(b => b.name).Distinct().Count() == 25, "Duplicate rib bindings.");
        Require(nav.LeftLowerLimbBones.Length == 30, "Expected 30 selectable left lower-limb bones.");
        Require(nav.LeftLowerLimbBones.Select(b => b.name).Distinct().Count() == 30,
            "Duplicate left lower-limb bindings.");
        Require(nav.RightPectoralBones.Length == 2 && nav.LeftPectoralBones.Length == 2,
            "Expected a clavicle and scapula on each pectoral side.");
        Require(nav.PelvicBones.Length == 1 && nav.PelvicBones[0].name == "Hip_L_2",
            "Expected the imported left hip bone as the pelvic G3 entry.");
        Require(nav.RightLowerLimbBones.Length == 32 &&
                nav.RightLowerLimbBones.Select(b => b.name).Distinct().Count() == 32,
            "Expected 32 right lower-limb meshes, including two sesamoids.");
        Require(nav.LeftUpperLimbBones.Length == 30 && nav.RightUpperLimbBones.Length == 30,
            "Expected 30 selectable bones per upper limb.");
        Require(nav.LeftUpperLimbBones.Select(b => b.name).Distinct().Count() == 30 &&
                nav.RightUpperLimbBones.Select(b => b.name).Distinct().Count() == 30,
            "Duplicate upper-limb bindings.");
        Require(PlayerSettings.bakeCollisionMeshes, "Quest build must prebake collision meshes.");
        var overview = nav.CurrentView;
        foreach (var divisionView in new[] {
            AnatomySceneSetup.Reference<GameObject>(nav, "axialView"),
            AnatomySceneSetup.Reference<GameObject>(nav, "appendicularView") })
        {
            if (divisionView == null) continue;
            foreach (var transition in divisionView.GetComponentsInChildren<ViewTransitionOnSelect>(true))
            {
                if (transition.NextView == null) continue;
                Require(!string.IsNullOrWhiteSpace(new SerializedObject(transition).FindProperty("nextTitle").stringValue),
                    $"Missing navigation title: {transition.name}.");
                Require(!string.IsNullOrWhiteSpace(new SerializedObject(transition).FindProperty("nextDescription").stringValue),
                    $"Missing navigation description: {transition.name}.");
                Require(!new SerializedObject(transition).FindProperty("autoCreateMeshTargets").boolValue,
                    $"G1 group {transition.name} still creates ray colliders at runtime.");
                if (transition.NextView == AnatomySceneSetup.Reference<GameObject>(nav, "vertebralView")) continue;
                Transform root = AnatomySceneSetup.Reference<Transform>(transition, "interactableRoot") ?? transition.transform;
                var meshes = root.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => filter.sharedMesh != null && filter.GetComponent<Renderer>() != null).ToArray();
                Require(meshes.Length > 0, $"G1 group {transition.name} has no visible ray targets.");
                foreach (var mesh in meshes)
                {
                    var collider = mesh.GetComponent<Collider>();
                    var target = mesh.GetComponent<XRBaseInteractable>();
                    Require(collider != null && collider.enabled && target != null && target.enabled &&
                            target.colliders.Contains(collider),
                        $"G1 group {transition.name} has an unbaked ray target: {mesh.name}.");
                }
            }
        }
        var axialDivision = AnatomySceneSetup.Reference<GameObject>(nav, "axialDivisionRoot");
        var appendicular = AnatomySceneSetup.Reference<GameObject>(nav, "appendicularDivisionRoot");
        var group = AnatomySceneSetup.Reference<GameObject>(nav, "vertebralView");
        var skullView = AnatomySceneSetup.Reference<GameObject>(nav, "skullView");
        Require(skullView != null && !skullView.activeSelf, "Skull G2 view must start hidden.");
        var ribView = AnatomySceneSetup.Reference<GameObject>(nav, "ribView");
        Require(ribView != null, "Ribcage G3 view is not assigned.");
        var leftLowerLimbView = AnatomySceneSetup.Reference<GameObject>(nav, "leftLowerLimbView");
        Require(leftLowerLimbView != null, "Left lower-limb G3 view is not assigned.");
        var rightPectoralView = AnatomySceneSetup.Reference<GameObject>(nav, "rightPectoralView");
        var leftPectoralView = AnatomySceneSetup.Reference<GameObject>(nav, "leftPectoralView");
        var pelvicView = AnatomySceneSetup.Reference<GameObject>(nav, "pelvicView");
        Require(rightPectoralView != null && leftPectoralView != null && pelvicView != null,
            "Girdle G3 views are not assigned.");
        var rightLowerLimbView = AnatomySceneSetup.Reference<GameObject>(nav, "rightLowerLimbView");
        var leftUpperLimbView = AnatomySceneSetup.Reference<GameObject>(nav, "leftUpperLimbView");
        var rightUpperLimbView = AnatomySceneSetup.Reference<GameObject>(nav, "rightUpperLimbView");
        Require(rightLowerLimbView != null && leftUpperLimbView != null && rightUpperLimbView != null,
            "Remaining limb G3 views are not assigned.");
        foreach (var view in new[] { leftLowerLimbView, rightLowerLimbView,
                     leftUpperLimbView, rightUpperLimbView, leftPectoralView, rightPectoralView })
        {
            var rotator = view.GetComponent<SkeletonYawRotator>();
            Require(rotator != null, $"G2 view has no yaw rotator: {view.name}.");
            Vector3 center = WorldModelCenter(view);
            Vector3 position = view.transform.localPosition;
            Quaternion rotation = view.transform.localRotation;
            rotator.RotateYaw(90f);
            Vector3 rotatedCenter = WorldModelCenter(view);
            Require(Vector2.Distance(new Vector2(center.x, center.z),
                    new Vector2(rotatedCenter.x, rotatedCenter.z)) < 0.001f,
                $"G2 view revolves around an offset pivot: {view.name}.");
            view.transform.localPosition = position;
            view.transform.localRotation = rotation;
        }
        var board = AnatomySceneSetup.Reference<InfoBoardController>(nav, "infoBoard");
        var title = AnatomySceneSetup.Reference<TMPro.TextMeshProUGUI>(board, "titleText");
        var description = AnatomySceneSetup.Reference<TMPro.TextMeshProUGUI>(board, "descriptionText");
        var back = AnatomySceneSetup.Reference<GameObject>(board, "backButton").GetComponent<UnityEngine.UI.Button>();
        var manager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
        Require(manager != null, "XR interaction manager missing.");
        var ray1 = new GameObject("Validation ray 1").AddComponent<XRRayInteractor>();
        var ray2 = new GameObject("Validation ray 2").AddComponent<XRRayInteractor>();
        ray1.transform.position = ray2.transform.position = Vector3.one * 1000;
        ray1.interactionManager = ray2.interactionManager = manager;
        foreach (var root in new[] { overview, AnatomySceneSetup.Reference<GameObject>(nav, "axialView"),
                     AnatomySceneSetup.Reference<GameObject>(nav, "appendicularView"), group, skullView, ribView,
                     leftLowerLimbView, rightPectoralView, leftPectoralView, pelvicView,
                     rightLowerLimbView, leftUpperLimbView, rightUpperLimbView })
        {
            var colliders = new HashSet<Collider>();
            foreach (var target in root.GetComponentsInChildren<XRBaseInteractable>(true).Where(i => i.enabled))
                foreach (var collider in target.colliders)
                    Require(colliders.Add(collider), "Two anatomy interactables own the same collider.");
        }
        var divisionTarget = axialDivision.GetComponentsInChildren<XRSimpleInteractable>().First(i => i.enabled);
        manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)divisionTarget);
        Require(nav.Level == AnatomyLevel.Division && nav.HistoryCount == 1, "Division callbacks must enter exactly G1.");
        Require(!nav.EnterGroup(group, "Vertebral column", "Group overview."), "One press skipped G1.");
        yield return null; yield return null;
        CheckGroupHighlights(nav.CurrentView, manager, ray1, ray2);
        var groupTransition = nav.CurrentView.GetComponentsInChildren<ViewTransitionOnSelect>(true)
            .Single(t => AnatomySceneSetup.Reference<GameObject>(t, "nextView") == group);
        var transitionRoot = AnatomySceneSetup.Reference<Transform>(groupTransition, "interactableRoot");
        Require(transitionRoot != null && transitionRoot.name == "VertabralColumn",
            "The vertebral transition is not scoped to the axial vertebral group.");
        var groupTarget = transitionRoot.GetComponentsInChildren<XRSimpleInteractable>(true).First(i => i.enabled);
        Require(manager.IsSelectPossible((IXRSelectInteractor)ray2, (IXRSelectInteractable)groupTarget),
            "Selection did not rearm after the transition input was released.");
        yield return null; yield return null;
        manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)groupTarget);
        Require(nav.Level == AnatomyLevel.Group && nav.HistoryCount == 2, "Group selection callbacks did not enter G2.");
        yield return null; yield return null;
        group.transform.Rotate(Vector3.up, 37, Space.World);
        Vector3 savedPosition = group.transform.localPosition;
        Quaternion savedRotation = group.transform.localRotation;
        Vector3 savedScale = group.transform.localScale;
        var reservation = AnatomySceneSetup.Reference<AnatomyInputReservation>(nav, "inputReservation");

        foreach (BoneSelection bone in nav.Bones)
        {
            Require(bone.Info != null && !string.IsNullOrWhiteSpace(bone.Info.PartDescription), $"Missing information: {bone.name}");
            Require(bone.GetComponent<MeshCollider>().sharedMesh != null, $"Missing collider: {bone.name}");
            var interactable = bone.GetComponent<XRSimpleInteractable>();
            var renderer = bone.Renderers[0];
            var original = renderer.sharedMaterials;
            manager.HoverEnter((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            manager.HoverEnter((IXRHoverInteractor)ray2, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials[0] != original[0], $"Hover did not highlight {bone.name}.");
            manager.HoverExit((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials[0] != original[0], "One ray exiting cleared the other ray's highlight.");
            manager.HoverExit((IXRHoverInteractor)ray2, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials.SequenceEqual(original), "Last hover did not restore materials.");

            var bindingStates = reservation.Assets.SelectMany(a => a.actionMaps).SelectMany(m => m.actions)
                .SelectMany(a => a.bindings.Select((b, i) => (action: a, index: i, binding: b))).ToArray();
            manager.HoverEnter((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            manager.HoverEnter((IXRHoverInteractor)ray2, (IXRHoverInteractable)interactable);
            manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)interactable);
            Require(nav.Level == AnatomyLevel.Bone && nav.SelectedBone == bone && !group.activeSelf, "Incorrect G3 state.");
            Require(title.text == bone.Info.PartName && title.gameObject.activeInHierarchy, "Bone title is incorrect or hidden.");
            description.ForceMeshUpdate(true, true);
            Require(description.preferredHeight <= description.rectTransform.rect.height + 1,
                $"Information overflows the panel for {bone.name}: {description.preferredHeight}.");
            Require(back.onClick.GetPersistentEventCount() == 1 && back.onClick.GetPersistentMethodName(0) == "GoBack",
                "Anatomy Back must have one navigation callback.");
            var boneVisual = nav.InspectionDisplay.BoneVisual;
            var context = nav.InspectionDisplay.ReferenceVisual;
            Require(boneVisual.GetComponentsInChildren<MeshFilter>().Length == bone.Renderers.Length, "Wrong isolated geometry.");
            Require(boneVisual.GetComponentInChildren<Renderer>().sharedMaterials.SequenceEqual(original),
                "A hover material leaked into the inspected bone.");
            Require(context.GetComponentsInChildren<MeshFilter>().Length == group.GetComponentsInChildren<MeshFilter>(true).Length, "Incomplete reference column.");
            Require(boneVisual.GetComponentsInChildren<Collider>().Length == 0 && context.GetComponentsInChildren<Collider>().Length == 0,
                "Inspection visuals must not capture controller rays.");
            Require(BoneInspectionDisplay.LocalBounds(boneVisual).center.magnitude < 0.001f, "Bone is not centered.");
            Require(Mathf.Abs(BoneInspectionDisplay.LocalBounds(boneVisual).size.magnitude - 0.30f) < 0.001f, "Bone inspection size changed.");
            Require(Mathf.Abs(BoneInspectionDisplay.LocalBounds(context).size.y - 0.45f) < 0.001f, "Reference height changed.");
            var contextRotation = context.rotation;
            nav.InspectionPose.Apply(new Vector2(1, 1), 0.5f, false, false);
            nav.InspectionDisplay.SetRotation(nav.InspectionPose.Rotation);
            Require(context.rotation == contextRotation, "Reference column rotated with the inspected bone.");
            nav.InspectionPose.Reset();
            nav.InspectionDisplay.SetRotation(Quaternion.identity);
            foreach (var asset in reservation.Assets)
            {
                var turn = asset.FindAction("XRI Right Locomotion/Turn", false);
                if (turn != null) Require(turn.bindings.All(b => string.IsNullOrEmpty(b.effectivePath)), "Right turning binding still active in G3.");
                var select = asset.FindAction("XRI Right Interaction/Select", false);
                if (select != null) Require(select.bindings.Any(b => !string.IsNullOrEmpty(b.effectivePath)), "Ray selection binding was muted.");
            }
            Require(!nav.InspectBone(bone), "Repeated selection should not push duplicate G3 frames.");
            yield return null; yield return null;
            if (bone.name == "C1") Capture(nav);
            back.onClick.Invoke();
            Require(nav.Level == AnatomyLevel.Group && nav.SelectedBone == null && group.activeSelf, "Back did not restore G2.");
            Require(group.transform.localPosition == savedPosition && group.transform.localRotation == savedRotation && group.transform.localScale == savedScale,
                "G2 transform changed across inspection.");
            Require(renderer.sharedMaterials.SequenceEqual(original), "Material leaked across Back.");
            foreach (var item in bindingStates)
            {
                var restored = item.action.bindings[item.index];
                Require(restored.overridePath == item.binding.overridePath && restored.overrideProcessors == item.binding.overrideProcessors &&
                    restored.overrideInteractions == item.binding.overrideInteractions, "A pre-existing binding override was not restored.");
            }
            yield return null; yield return null;
        }
        Require(group.GetComponentsInChildren<BoneSelection>(true).All(b => !b.name.StartsWith("Disk")), "Disc became a bone selection.");
        nav.GoBack();
        Require(nav.Level == AnatomyLevel.Division, "G2 Back must restore G1.");
        yield return null; yield return null;
        var skullTransition = nav.CurrentView.GetComponentsInChildren<ViewTransitionOnSelect>(true)
            .Single(t => t.NextView == skullView);
        Require(skullTransition.name == "Skull", "Skull transition is not on the G1 skull group.");
        var skullTargets = skullTransition.GetComponentsInChildren<XRSimpleInteractable>(true)
            .Where(i => i.enabled).ToArray();
        Require(skullTargets.Length == 30 && skullTargets.Any(i =>
                i.colliders.Any(CanRaycastCollider)),
            "G1 skull lacks its authored Quest ray targets.");
        manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)skullTargets[0]);
        Require(nav.Level == AnatomyLevel.Group && nav.CurrentView == skullView &&
                skullView.activeSelf && nav.HistoryCount == 2,
            "G1 skull selection did not open G2.");
        yield return null; yield return null;
        Require(skullView.GetComponentsInChildren<BoneSelection>(true).Length == 29 &&
                skullView.GetComponentsInChildren<MeshFilter>(true).Length == 31 &&
                skullView.GetComponentsInChildren<MeshFilter>(true).Count(m =>
                    m.name.StartsWith("Context -")) == 2,
            "Skull bone selection or teeth context is incomplete.");
        foreach (BoneSelection bone in nav.SkullBones)
        {
            Require(bone.Info != null && !string.IsNullOrWhiteSpace(bone.Info.PartDescription),
                $"Missing skull information: {bone.name}");
            var interactable = bone.GetComponent<XRSimpleInteractable>();
            var collider = bone.GetComponent<MeshCollider>();
            var renderer = bone.Renderers[0];
            var original = renderer.sharedMaterials;
            Require(interactable.enabled && collider.enabled && collider.sharedMesh != null &&
                    interactable.colliders.Contains(collider),
                $"Skull bone lacks an authored ray target: {bone.name}");
            Require(AssetDatabase.GetAssetPath(collider.sharedMesh) == SkullRayTargetOptimization.ColliderModelPath &&
                    collider.sharedMesh != bone.GetComponent<MeshFilter>().sharedMesh &&
                    CanRaycastCollider(collider),
                $"Skull bone does not use a hittable low-detail ray collider: {bone.name}");
            manager.HoverEnter((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials[0] != original[0] &&
                    description.text.Contains("Pointing at: " + bone.Info.PartName),
                $"Skull hover did not highlight and preview {bone.name}");
            manager.HoverExit((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials.SequenceEqual(original),
                $"Skull hover material leaked: {bone.name}");
            manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)interactable);
            Require(nav.Level == AnatomyLevel.Bone && nav.SelectedBone == bone &&
                    !skullView.activeSelf && nav.HistoryCount == 3,
                $"Skull selection did not enter G3: {bone.name}");
            Require(title.text == bone.Info.PartName && description.text.Contains("Axial → Skull"),
                $"Skull G3 information is missing: {bone.name}");
            description.ForceMeshUpdate(true, true);
            Require(description.preferredHeight <= description.rectTransform.rect.height + 1,
                $"Skull information overflows the panel: {bone.name}");
            var isolated = nav.InspectionDisplay.BoneVisual;
            var reference = nav.InspectionDisplay.ReferenceVisual;
            Require(isolated.GetComponentsInChildren<MeshFilter>().Length == 1 &&
                    reference.GetComponentsInChildren<MeshFilter>().Length == 31 &&
                    isolated.GetComponentsInChildren<Collider>().Length == 0 &&
                    reference.GetComponentsInChildren<Collider>().Length == 0,
                $"Skull inspection geometry is incomplete: {bone.name}");
            yield return null; yield return null;
            back.onClick.Invoke();
            Require(nav.Level == AnatomyLevel.Group && nav.CurrentView == skullView &&
                    skullView.activeSelf && nav.SelectedBone == null,
                $"Skull G3 Back did not restore G2: {bone.name}");
            yield return null; yield return null;
        }
        nav.GoBack();
        Require(nav.Level == AnatomyLevel.Division && !skullView.activeSelf,
            "Skull G2 Back did not restore axial G1.");
        yield return null; yield return null;
        var ribcageTransition = nav.CurrentView.GetComponentsInChildren<ViewTransitionOnSelect>(true)
            .Single(t => t.name == "Ribcage");
        var ribsView = AnatomySceneSetup.Reference<GameObject>(ribcageTransition, "nextView");
        Require(ribsView != null, "Ribcage has no G2 view.");
        var ribTargets = ribcageTransition.GetComponentsInChildren<XRSimpleInteractable>(true)
            .Where(i => i.enabled).ToArray();
        Require(ribTargets.Length > 0, "Ribcage automatic ray targets were not created.");
        manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)ribTargets[0]);
        Require(nav.Level == AnatomyLevel.Group && nav.CurrentView == ribsView && ribsView.activeSelf,
            "Ribcage selection did not open its G2 view.");
        yield return null; yield return null;
        Require(ribsView == ribView, "Ribcage G2 and G3 use different roots.");
        Require(ribView.GetComponentsInChildren<BoneSelection>(true).Length == 25,
            "Ribcage selection components are incomplete.");
        Require(ribView.GetComponentsInChildren<BoneSelection>(true).All(b =>
                b.name.IndexOf("rib (", StringComparison.Ordinal) >= 0 || b.name == "Sternum (Breastbone)"),
            "A cartilage mesh became selectable.");
        ribView.transform.Rotate(Vector3.up, 31, Space.World);
        Vector3 ribPosition = ribView.transform.localPosition;
        Quaternion ribRotation = ribView.transform.localRotation;
        Vector3 ribScale = ribView.transform.localScale;
        foreach (BoneSelection bone in nav.RibBones)
        {
            Require(bone.Info != null && !string.IsNullOrWhiteSpace(bone.Info.PartDescription),
                $"Missing rib information: {bone.name}");
            var interactable = bone.GetComponent<XRSimpleInteractable>();
            var renderer = bone.Renderers[0];
            var original = renderer.sharedMaterials;
            Require(interactable.enabled && bone.GetComponent<MeshCollider>().sharedMesh != null,
                $"Rib cannot be selected: {bone.name}");
            manager.HoverEnter((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            manager.HoverEnter((IXRHoverInteractor)ray2, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials[0] != original[0], $"Rib hover failed: {bone.name}");
            manager.HoverExit((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials[0] != original[0], "One ray cleared the other rib hover.");
            manager.HoverExit((IXRHoverInteractor)ray2, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials.SequenceEqual(original), "Rib hover material was not restored.");
            manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)interactable);
            Require(nav.Level == AnatomyLevel.Bone && nav.SelectedBone == bone && !ribView.activeSelf,
                $"Rib selection did not enter G3: {bone.name}");
            Require(title.text == bone.Info.PartName && description.text.Contains("Rib cage"),
                "Rib information or breadcrumb is missing.");
            description.ForceMeshUpdate(true, true);
            Require(description.preferredHeight <= description.rectTransform.rect.height + 1,
                $"Rib information overflows the panel: {bone.name}");
            var isolated = nav.InspectionDisplay.BoneVisual;
            var reference = nav.InspectionDisplay.ReferenceVisual;
            Require(isolated != null && reference != null &&
                    isolated.GetComponentsInChildren<MeshFilter>().Length == bone.Renderers.Length &&
                    reference.GetComponentsInChildren<MeshFilter>().Length == ribView.GetComponentsInChildren<MeshFilter>(true).Length,
                "Rib inspection or reference geometry is incomplete.");
            var referenceBone = reference.GetComponentsInChildren<MeshRenderer>()
                .Single(r => r.name == bone.name);
            Require(referenceBone.sharedMaterials.All(m => m == nav.GroupHighlightMaterial),
                "The reference cage does not highlight the inspected rib bone.");
            Require(isolated.GetComponentsInChildren<Collider>().Length == 0 &&
                    reference.GetComponentsInChildren<Collider>().Length == 0,
                "Rib inspection visuals captured controller rays.");
            var referenceRotation = reference.rotation;
            nav.InspectionPose.Apply(new Vector2(1, 1), 0.5f, false, false);
            nav.InspectionDisplay.SetRotation(nav.InspectionPose.Rotation);
            Require(reference.rotation == referenceRotation, "Rib reference moved with the inspected bone.");
            yield return null; yield return null;
            back.onClick.Invoke();
            Require(nav.Level == AnatomyLevel.Group && nav.SelectedBone == null && ribView.activeSelf,
                "Rib Back did not restore G2.");
            Require(ribView.transform.localPosition == ribPosition &&
                    ribView.transform.localRotation == ribRotation && ribView.transform.localScale == ribScale,
                "Rib G2 transform changed across inspection.");
            Require(renderer.sharedMaterials.SequenceEqual(original), "Rib hover material leaked across Back.");
            yield return null; yield return null;
        }
        nav.GoBack();
        Require(nav.Level == AnatomyLevel.Division && !ribsView.activeSelf,
            "Ribcage Back did not restore G1.");
        yield return null; yield return null;
        nav.GoBack();
        Require(nav.Level == AnatomyLevel.Whole && overview.activeSelf && axialDivision.activeSelf && appendicular.activeSelf, "G1 Back must restore G0.");
        yield return null; yield return null;
        var appendicularTransition = appendicular.GetComponent<ViewTransitionOnSelect>();
        Require(appendicularTransition != null &&
                AnatomySceneSetup.Reference<AnatomyNavigationController>(appendicularTransition, "navigation") == nav,
            "Appendicular division transition is not connected to navigation.");
        var appendicularTarget = appendicular.GetComponentsInChildren<XRSimpleInteractable>()
            .First(i => i.enabled);
        manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)appendicularTarget);
        Require(nav.Level == AnatomyLevel.Division && nav.HistoryCount == 1,
            "Appendicular division callbacks did not enter G1.");
        var appendicularView = AnatomySceneSetup.Reference<GameObject>(nav, "appendicularView");
        Require(!axialDivision.activeSelf && nav.CurrentView == appendicularView && appendicularView.activeSelf,
            "Appendicular isolation regressed.");
        yield return null; yield return null;
        CheckGroupHighlights(appendicularView, manager, ray1, ray2);
        var leftLowerTransition = appendicularView.GetComponentsInChildren<ViewTransitionOnSelect>(true)
            .Single(t => t.NextView == leftLowerLimbView);
        var leftLowerTarget = leftLowerTransition.GetComponentsInChildren<XRSimpleInteractable>(true)
            .First(i => i.enabled);
        manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)leftLowerTarget);
        Require(nav.Level == AnatomyLevel.Group && nav.CurrentView == leftLowerLimbView &&
                leftLowerLimbView.activeSelf && nav.HistoryCount == 2,
            "Left lower-limb selection did not open its G2 view.");
        yield return null; yield return null;
        Require(!ray1.hasSelection,
            "The G1 target kept the controller ray selected after opening appendicular G2.");
        Require(manager.IsSelectPossible((IXRSelectInteractor)ray1,
                (IXRSelectInteractable)nav.LeftLowerLimbBones[0].GetComponent<XRSimpleInteractable>()),
            "The selecting ray cannot select a G2 bone after leaving the G1 target.");
        Require(leftLowerLimbView.GetComponentsInChildren<BoneSelection>(true).Length == 30,
            "Left lower-limb selection components are incomplete.");
        leftLowerLimbView.transform.Rotate(Vector3.up, 29, Space.World);
        Vector3 limbPosition = leftLowerLimbView.transform.localPosition;
        Quaternion limbRotation = leftLowerLimbView.transform.localRotation;
        Vector3 limbScale = leftLowerLimbView.transform.localScale;
        foreach (BoneSelection bone in nav.LeftLowerLimbBones)
        {
            Require(bone.Info != null && !string.IsNullOrWhiteSpace(bone.Info.PartDescription),
                $"Missing lower-limb information: {bone.name}");
            var interactable = bone.GetComponent<XRSimpleInteractable>();
            var renderer = bone.Renderers[0];
            var original = renderer.sharedMaterials;
            Require(interactable.enabled && bone.GetComponent<MeshCollider>().sharedMesh != null,
                $"Lower-limb bone cannot be selected: {bone.name}");
            manager.HoverEnter((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            manager.HoverEnter((IXRHoverInteractor)ray2, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials[0] != original[0] &&
                    description.text.Contains("Pointing at: " + bone.Info.PartName),
                $"Lower-limb hover preview failed: {bone.name}");
            manager.HoverExit((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials[0] != original[0], "One ray cleared the other lower-limb hover.");
            manager.HoverExit((IXRHoverInteractor)ray2, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials.SequenceEqual(original),
                "Lower-limb hover material was not restored.");
            Require(manager.IsSelectPossible((IXRSelectInteractor)ray2, (IXRSelectInteractable)interactable),
                $"Lower-limb bone is highlighted but blocked from selection: {bone.name}");
            manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)interactable);
            Require(nav.Level == AnatomyLevel.Bone && nav.SelectedBone == bone &&
                    !leftLowerLimbView.activeSelf && nav.HistoryCount == 3,
                $"Lower-limb selection did not enter G3: {bone.name}");
            Require(title.text == bone.Info.PartName &&
                    description.text.Contains("Appendicular → Left lower limb"),
                "Lower-limb information or breadcrumb is missing.");
            description.ForceMeshUpdate(true, true);
            Require(description.preferredHeight <= description.rectTransform.rect.height + 1,
                $"Lower-limb information overflows the panel: {bone.name}");
            var isolated = nav.InspectionDisplay.BoneVisual;
            var reference = nav.InspectionDisplay.ReferenceVisual;
            Require(isolated != null && reference != null &&
                    isolated.GetComponentsInChildren<MeshFilter>().Length == bone.Renderers.Length &&
                    reference.GetComponentsInChildren<MeshFilter>().Length ==
                    leftLowerLimbView.GetComponentsInChildren<MeshFilter>(true).Length,
                "Lower-limb inspection or reference geometry is incomplete.");
            var referenceBone = reference.GetComponentsInChildren<MeshRenderer>()
                .Single(r => r.name == bone.name);
            Require(referenceBone.sharedMaterials.All(m => m == nav.GroupHighlightMaterial),
                "The reference limb does not highlight the inspected bone.");
            Require(isolated.GetComponentsInChildren<Collider>().Length == 0 &&
                    reference.GetComponentsInChildren<Collider>().Length == 0,
                "Lower-limb inspection visuals captured controller rays.");
            Require(Mathf.Abs(BoneInspectionDisplay.LocalBounds(isolated).size.magnitude - 0.30f) < 0.001f &&
                    Mathf.Abs(BoneInspectionDisplay.LocalBounds(reference).size.y - 0.45f) < 0.001f,
                "Lower-limb inspection sizing changed.");
            var referenceRotation = reference.rotation;
            nav.InspectionPose.Apply(new Vector2(1, 1), 0.5f, false, false);
            nav.InspectionDisplay.SetRotation(nav.InspectionPose.Rotation);
            Require(reference.rotation == referenceRotation, "Lower-limb reference moved with the inspected bone.");
            Require(!nav.InspectBone(bone), "Repeated selection pushed a duplicate lower-limb G3 frame.");
            yield return null; yield return null;
            back.onClick.Invoke();
            Require(nav.Level == AnatomyLevel.Group && nav.SelectedBone == null &&
                    leftLowerLimbView.activeSelf && nav.HistoryCount == 2,
                "Lower-limb Back did not restore G2.");
            Require(leftLowerLimbView.transform.localPosition == limbPosition &&
                    leftLowerLimbView.transform.localRotation == limbRotation &&
                    leftLowerLimbView.transform.localScale == limbScale,
                "Lower-limb G2 transform changed across inspection.");
            Require(renderer.sharedMaterials.SequenceEqual(original),
                "Lower-limb hover material leaked across Back.");
            yield return null; yield return null;
        }
        nav.GoBack();
        Require(nav.Level == AnatomyLevel.Division && !leftLowerLimbView.activeSelf,
            "Left lower-limb Back did not restore appendicular G1.");
        yield return null; yield return null;
        var rightPectoralTransition = appendicularView.GetComponentsInChildren<ViewTransitionOnSelect>(true)
            .Single(t => t.NextView == rightPectoralView);
        var rightPectoralTarget = rightPectoralTransition.GetComponentsInChildren<XRSimpleInteractable>(true)
            .First(i => i.enabled);
        manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)rightPectoralTarget);
        Require(nav.Level == AnatomyLevel.Group && nav.CurrentView == rightPectoralView &&
                description.text.Contains("Left Y: switch pectoral side"),
            "Right pectoral G2 or switch hint is missing.");
        yield return null; yield return null;
        var rightChecks = CheckGirdleBones(nav, rightPectoralView, nav.RightPectoralBones,
            "Right pectoral girdle", manager, ray1, ray2, title, description, back);
        while (rightChecks.MoveNext()) yield return rightChecks.Current;
        var simulatedLeft = UnityEngine.InputSystem.InputSystem.AddDevice<
            UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedController>();
        UnityEngine.InputSystem.InputSystem.SetDeviceUsage(simulatedLeft,
            UnityEngine.InputSystem.CommonUsages.LeftHand);
        yield return null; yield return null;
        var pressedY = new UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedControllerState()
            .WithButton(UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.ControllerButton.SecondaryButton);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(simulatedLeft, pressedY);
        yield return null; yield return null;
        Require(nav.CurrentView == leftPectoralView &&
                !rightPectoralView.activeSelf && nav.HistoryCount == 2,
            "Simulated left Y did not show the left pectoral G2 model.");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(simulatedLeft,
            new UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedControllerState());
        yield return null;
        UnityEngine.InputSystem.InputSystem.RemoveDevice(simulatedLeft);
        yield return null; yield return null;
        var leftChecks = CheckGirdleBones(nav, leftPectoralView, nav.LeftPectoralBones,
            "Left pectoral girdle", manager, ray1, ray2, title, description, back);
        while (leftChecks.MoveNext()) yield return leftChecks.Current;
        Require(nav.SwitchPectoralSide() && nav.CurrentView == rightPectoralView &&
                !leftPectoralView.activeSelf && nav.HistoryCount == 2,
            "Pectoral side switch did not return to the right G2 model.");
        yield return null; yield return null;
        var simulatedBack = UnityEngine.InputSystem.InputSystem.AddDevice<
            UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedController>();
        UnityEngine.InputSystem.InputSystem.SetDeviceUsage(simulatedBack,
            UnityEngine.InputSystem.CommonUsages.LeftHand);
        yield return null; yield return null;
        var pressedX = new UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedControllerState()
            .WithButton(UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.ControllerButton.PrimaryButton);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(simulatedBack, pressedX);
        yield return null; yield return null;
        Require(nav.Level == AnatomyLevel.Division && nav.CurrentView == appendicularView &&
                !rightPectoralView.activeSelf && !leftPectoralView.activeSelf,
            "Left X Back did not restore appendicular G1.");
        Require(nav.HistoryCount == 1, "Holding left X skipped more than one G level.");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(simulatedBack,
            new UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedControllerState());
        yield return null;
        UnityEngine.InputSystem.InputSystem.RemoveDevice(simulatedBack);
        yield return null; yield return null;
        var pelvicTransition = appendicularView.GetComponentsInChildren<ViewTransitionOnSelect>(true)
            .Single(t => t.NextView == pelvicView);
        var pelvicTarget = pelvicTransition.GetComponentsInChildren<XRSimpleInteractable>(true)
            .First(i => i.enabled);
        manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)pelvicTarget);
        Require(nav.Level == AnatomyLevel.Group && nav.CurrentView == pelvicView && pelvicView.activeSelf,
            "Pelvic selection did not open its G2 view.");
        yield return null; yield return null;
        var pelvicChecks = CheckGirdleBones(nav, pelvicView, nav.PelvicBones,
            "Pelvic girdle", manager, ray1, ray2, title, description, back);
        while (pelvicChecks.MoveNext()) yield return pelvicChecks.Current;
        nav.GoBack();
        Require(nav.Level == AnatomyLevel.Division && nav.CurrentView == appendicularView,
            "Pelvic Back did not restore appendicular G1.");
        yield return null; yield return null;
        var remainingViews = new[] { rightLowerLimbView, leftUpperLimbView, rightUpperLimbView };
        var remainingBones = new[] { nav.RightLowerLimbBones, nav.LeftUpperLimbBones, nav.RightUpperLimbBones };
        var remainingTitles = new[] { "Right lower limb", "Left upper limb", "Right upper limb" };
        for (int i = 0; i < remainingViews.Length; i++)
        {
            GameObject view = remainingViews[i];
            var transition = appendicularView.GetComponentsInChildren<ViewTransitionOnSelect>(true)
                .Single(t => t.NextView == view);
            var target = transition.GetComponentsInChildren<XRSimpleInteractable>(true)
                .First(t => t.enabled);
            manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)target);
            Require(nav.Level == AnatomyLevel.Group && nav.CurrentView == view && view.activeSelf,
                $"Remaining limb selection did not open G2: {remainingTitles[i]}");
            yield return null; yield return null;
            var limbChecks = CheckGirdleBones(nav, view, remainingBones[i], remainingTitles[i],
                manager, ray1, ray2, title, description, back);
            while (limbChecks.MoveNext()) yield return limbChecks.Current;
            nav.GoBack();
            Require(nav.Level == AnatomyLevel.Division && nav.CurrentView == appendicularView,
                $"Remaining limb Back did not restore appendicular G1: {remainingTitles[i]}");
            yield return null; yield return null;
        }
        nav.GoBack();
        Require(nav.Level == AnatomyLevel.Whole, "Appendicular Back did not restore G0.");
        yield return null; yield return null;
        Require(nav.SelectDivision(axialDivision, "Axial", "Axial overview."), "Cannot re-enter axial division.");
        yield return null; yield return null;
        Require(nav.EnterGroup(group, "Vertebral column", "Group overview."), "Cannot re-enter vertebral group.");
        yield return null; yield return null;
        Require(nav.InspectBone(nav.Bones[0]), "Cannot re-inspect the same bone.");
        yield return null; yield return null;
        nav.GoBack();
        CheckPanelMovementSetup(nav);
        UnityEngine.Object.Destroy(ray1.gameObject);
        UnityEngine.Object.Destroy(ray2.gameObject);
        yield return null; yield return null;
        UnityEngine.SceneManagement.SceneManager.LoadScene(AnatomySceneSetup.ScenePath);
        yield return null; yield return null; yield return null;
        var reloaded = UnityEngine.Object.FindFirstObjectByType<AnatomyNavigationController>();
        Require(reloaded != null && reloaded.Level == AnatomyLevel.Whole, "Scene reload did not restart at G0.");
        Require(projectErrors.Count == 0, "Project runtime errors: " + string.Join("\n", projectErrors));
    }

    private static Vector3 WorldModelCenter(GameObject view)
    {
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (var mesh in view.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null || mesh.GetComponent<MeshRenderer>() == null) continue;
            Bounds local = mesh.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1,
                    (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                Vector3 point = mesh.transform.TransformPoint(
                    local.center + Vector3.Scale(local.extents, sign));
                if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(point);
            }
        }
        Require(hasBounds, $"G2 view has no rendered geometry: {view.name}.");
        return bounds.center;
    }

    private static void CheckPanelMovementSetup(AnatomyNavigationController nav)
    {
        var display = nav.InspectionDisplay;
        var boneAnchor = AnatomySceneSetup.Reference<Transform>(display, "boneAnchor");
        var referenceAnchor = AnatomySceneSetup.Reference<Transform>(display, "referenceAnchor");
        Require(Mathf.Abs(boneAnchor.position.x - 2.477991f) < 0.0001f &&
                Mathf.Abs(boneAnchor.position.z + 1.173756f) < 0.0001f &&
                Mathf.Abs(boneAnchor.position.y - referenceAnchor.position.y) < 0.0001f &&
                Mathf.Abs(Vector3.Distance(boneAnchor.position, referenceAnchor.position) - 0.42f) < 0.001f,
            "G3 inspection placement or reference spacing is incorrect.");
        var board = AnatomySceneSetup.Reference<InfoBoardController>(nav, "infoBoard");
        var anatomyPanel = AnatomySceneSetup.Reference<GameObject>(board, "infoPanel");
        var quizStation = GameObject.Find("Quiz station");
        Require(anatomyPanel != null && quizStation != null, "Anatomy or quiz panel is missing.");
        Transform anatomyStation = anatomyPanel.transform.parent;
        Require(anatomyStation != null && anatomyStation.name == "Anatomy panel station" &&
                anatomyPanel.GetComponent<Rigidbody>() == null &&
                anatomyPanel.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() == null,
            "Anatomy Canvas still owns the grab interaction.");
        var backObject = AnatomySceneSetup.Reference<GameObject>(board, "backButton");
        var back = backObject != null ? backObject.GetComponent<UnityEngine.UI.Button>() : null;
        var backImage = back != null ? back.GetComponent<UnityEngine.UI.Image>() : null;
        Require(back != null && back.gameObject.activeInHierarchy &&
                backImage != null && backImage.raycastTarget,
            "Anatomy Back is not a clickable UI button.");
        CheckPanelGrabHandle(anatomyStation, anatomyPanel.transform);
        CheckPanelGrabHandle(quizStation.transform, quizStation.transform.Find("Quiz panel"));
    }

    private static void CheckPanelGrabHandle(Transform movable, Transform panel)
    {
        Require(panel != null, $"Movable panel is missing under {movable.name}.");
        var handle = panel.Find("Move handle");
        var grab = movable.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        var body = movable.GetComponent<Rigidbody>();
        var collider = handle != null ? handle.GetComponent<BoxCollider>() : null;
        var label = handle != null ? handle.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
        Require(handle != null && collider != null && label != null && label.text.Contains("Move") &&
                handle.localPosition.y < -0.5f * ((RectTransform)panel).sizeDelta.y &&
                body != null && body.isKinematic && grab != null && grab.enabled &&
                grab.colliders.Count == 1 && grab.colliders[0] == collider,
            $"Move handle is not configured for XR grabbing: {movable.name}");
    }

    private static IEnumerator CheckGirdleBones(AnatomyNavigationController nav, GameObject view,
        BoneSelection[] bones, string groupTitle, XRInteractionManager manager,
        XRRayInteractor ray1, XRRayInteractor ray2, TMPro.TextMeshProUGUI title,
        TMPro.TextMeshProUGUI description, UnityEngine.UI.Button back)
    {
        Require(view.activeSelf && view.GetComponentsInChildren<BoneSelection>(true).Length == bones.Length,
            $"Girdle selection components are incomplete: {groupTitle}");
        Vector3 position = view.transform.localPosition;
        Quaternion rotation = view.transform.localRotation;
        Vector3 scale = view.transform.localScale;
        foreach (BoneSelection bone in bones)
        {
            Require(bone.Info != null && !string.IsNullOrWhiteSpace(bone.Info.PartDescription),
                $"Missing girdle information: {bone.name}");
            var interactable = bone.GetComponent<XRSimpleInteractable>();
            var renderer = bone.Renderers[0];
            var original = renderer.sharedMaterials;
            Require(interactable.enabled && bone.GetComponent<MeshCollider>().sharedMesh != null,
                $"Girdle bone cannot be selected: {bone.name}");
            manager.HoverEnter((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            manager.HoverEnter((IXRHoverInteractor)ray2, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials[0] != original[0] &&
                    description.text.Contains("Pointing at: " + bone.Info.PartName),
                $"Girdle hover preview failed: {bone.name}");
            manager.HoverExit((IXRHoverInteractor)ray1, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials[0] != original[0], "One ray cleared the other girdle hover.");
            manager.HoverExit((IXRHoverInteractor)ray2, (IXRHoverInteractable)interactable);
            Require(renderer.sharedMaterials.SequenceEqual(original), "Girdle hover material was not restored.");
            Require(manager.IsSelectPossible((IXRSelectInteractor)ray2, (IXRSelectInteractable)interactable),
                $"Appendicular bone is highlighted but blocked from selection: {bone.name}");
            manager.SelectEnter((IXRSelectInteractor)ray1, (IXRSelectInteractable)interactable);
            Require(nav.Level == AnatomyLevel.Bone && nav.SelectedBone == bone &&
                    !view.activeSelf && nav.HistoryCount == 3,
                $"Girdle selection did not enter G3: {bone.name}");
            Require(title.text == bone.Info.PartName &&
                    description.text.Contains("Appendicular → " + groupTitle),
                $"Girdle information or breadcrumb is missing: {bone.name}");
            description.ForceMeshUpdate(true, true);
            Require(description.preferredHeight <= description.rectTransform.rect.height + 1,
                $"Girdle information overflows the panel: {bone.name}");
            var isolated = nav.InspectionDisplay.BoneVisual;
            var reference = nav.InspectionDisplay.ReferenceVisual;
            Require(isolated != null && reference != null &&
                    isolated.GetComponentsInChildren<MeshFilter>().Length == bone.Renderers.Length &&
                    reference.GetComponentsInChildren<MeshFilter>().Length ==
                    view.GetComponentsInChildren<MeshFilter>(true).Length,
                $"Girdle inspection or reference geometry is incomplete: {bone.name}");
            var referenceBone = reference.GetComponentsInChildren<MeshRenderer>()
                .Single(r => r.name == bone.name);
            Require(referenceBone.sharedMaterials.All(m => m == nav.GroupHighlightMaterial),
                $"Reference girdle does not highlight the inspected bone: {bone.name}");
            Require(isolated.GetComponentsInChildren<Collider>().Length == 0 &&
                    reference.GetComponentsInChildren<Collider>().Length == 0,
                "Girdle inspection visuals captured controller rays.");
            var referenceRotation = reference.rotation;
            nav.InspectionPose.Apply(new Vector2(1, 1), 0.5f, false, false);
            nav.InspectionDisplay.SetRotation(nav.InspectionPose.Rotation);
            Require(reference.rotation == referenceRotation, "Girdle reference moved with the inspected bone.");
            Require(!nav.SwitchPectoralSide(), "Pectoral switching should be disabled in G3.");
            yield return null; yield return null;
            back.onClick.Invoke();
            Require(nav.Level == AnatomyLevel.Group && nav.SelectedBone == null &&
                    nav.CurrentView == view && view.activeSelf && nav.HistoryCount == 2,
                $"Girdle Back did not restore G2: {bone.name}");
            Require(view.transform.localPosition == position &&
                    view.transform.localRotation == rotation && view.transform.localScale == scale,
                $"Girdle G2 transform changed across inspection: {bone.name}");
            Require(renderer.sharedMaterials.SequenceEqual(original),
                $"Girdle hover material leaked across Back: {bone.name}");
            yield return null; yield return null;
        }
    }

    private static void CheckGroupHighlights(GameObject axial, XRInteractionManager manager,
        XRRayInteractor ray1, XRRayInteractor ray2)
    {
        Physics.SyncTransforms();
        var groups = axial.GetComponentsInChildren<BoneGroupHoverHighlighter>(true)
            .Where(highlighter => highlighter.enabled)
            .Select(highlighter => highlighter.gameObject).ToArray();
        Require(groups.Length >= 2, "Division groups have no hover highlighters.");

        foreach (GameObject group in groups)
        {
            var highlighter = group.GetComponent<BoneGroupHoverHighlighter>();
            Require(highlighter != null && highlighter.enabled, $"Missing G2 highlighter: {group.name}");
            var target = group.GetComponentsInChildren<XRSimpleInteractable>(true).FirstOrDefault(i => i.enabled);
            Require(target != null, $"G2 group has no hover target: {group.name}");
            Require(group.GetComponentsInChildren<XRBaseInteractable>(true)
                    .Where(interactable => interactable.enabled)
                    .SelectMany(interactable => interactable.colliders)
                    .Any(CanRaycastCollider),
                $"A physical ray cannot hit G1 group {group.name}.");
            var groupRenderers = group.GetComponentsInChildren<Renderer>(true);
            Require(groupRenderers.Length > 0, $"G2 group has no renderers: {group.name}");
            var originals = groupRenderers.Select(renderer => renderer.sharedMaterials).ToArray();
            GameObject other = groups.First(candidate => candidate != group);
            Renderer otherRenderer = other.GetComponentsInChildren<Renderer>(true).First();
            Material[] otherOriginal = otherRenderer.sharedMaterials;

            manager.HoverEnter((IXRHoverInteractor)ray1, (IXRHoverInteractable)target);
            manager.HoverEnter((IXRHoverInteractor)ray2, (IXRHoverInteractable)target);
            for (int i = 0; i < groupRenderers.Length; i++)
                Require(!groupRenderers[i].sharedMaterials.SequenceEqual(originals[i]),
                    $"Hover did not highlight renderer {groupRenderers[i].name} in {group.name}.");
            Require(otherRenderer.sharedMaterials.SequenceEqual(otherOriginal),
                $"Hovering {group.name} changed {other.name}.");
            manager.HoverExit((IXRHoverInteractor)ray1, (IXRHoverInteractable)target);
            Require(!groupRenderers[0].sharedMaterials.SequenceEqual(originals[0]),
                $"One ray exiting cleared the other ray on {group.name}.");
            manager.HoverExit((IXRHoverInteractor)ray2, (IXRHoverInteractable)target);
            for (int i = 0; i < groupRenderers.Length; i++)
                Require(groupRenderers[i].sharedMaterials.SequenceEqual(originals[i]),
                    $"Last hover did not restore {group.name} materials.");
        }
    }

    private static bool CanRaycastCollider(Collider collider)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) return false;
        Bounds bounds = collider.bounds;
        foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
        {
            float distance = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(axis.x),
                Mathf.Abs(axis.y), Mathf.Abs(axis.z))) + 1f;
            int axisIndex = axis.x > 0 ? 0 : axis.y > 0 ? 1 : 2;
            int first = (axisIndex + 1) % 3;
            int second = (axisIndex + 2) % 3;
            for (int u = -1; u <= 1; u++)
                for (int v = -1; v <= 1; v++)
                {
                    Vector3 origin = bounds.center + axis * distance;
                    origin[first] += u * bounds.extents[first] * 0.6f;
                    origin[second] += v * bounds.extents[second] * 0.6f;
                    if (collider.Raycast(new Ray(origin, -axis), out _, distance * 2f))
                        return true;
                }
        }
        return false;
    }

    private static void CheckInputReservation()
    {
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();
        var right = asset.AddActionMap("XRI Right Locomotion");
        var turn = right.AddAction("Turn", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
        var jump = right.AddAction("Jump", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        var left = asset.AddActionMap("XRI Left Locomotion").AddAction("Move", InputActionType.Value,
            "<XRController>{LeftHand}/primary2DAxis");
        var click = asset.AddActionMap("XRI Right Interaction").AddAction("UI Press", InputActionType.Button,
            "<XRController>{RightHand}/triggerPressed");
        turn.ApplyBindingOverride(0, new InputBinding { overridePath = "<XRController>{RightHand}/thumbstick",
            overrideProcessors = "scaleVector2(x=0.5,y=0.5)" });
        var before = turn.bindings[0];
        asset.Enable();
        jump.Disable();
        var owner = new GameObject("Input reservation validation");
        var manager = owner.AddComponent<InputActionManager>();
        manager.actionAssets = new List<InputActionAsset> { asset };
        var reservation = owner.AddComponent<AnatomyInputReservation>();
        AnatomySceneSetup.SetArray(reservation, "actionManagers", new UnityEngine.Object[] { manager });
        reservation.Reserve();
        Require(turn.bindings[0].effectivePath == "" && jump.bindings[0].effectivePath == "", "G3 did not reserve turning and A.");
        Require(left.bindings[0].effectivePath.Contains("LeftHand") && click.bindings[0].effectivePath.Contains("triggerPressed"),
            "G3 changed left-hand movement or UI clicks.");
        jump.Enable();
        turn.ApplyBindingOverride(0, "<XRController>{RightHand}/thumbstick");
        Require(turn.bindings[0].effectivePath == "", "A mediator/rebind revived a reserved binding.");
        reservation.enabled = false;
        Require(turn.bindings[0].overridePath == before.overridePath && turn.bindings[0].overrideProcessors == before.overrideProcessors,
            "G3 failed to restore an existing binding override.");
        Require(turn.enabled && !jump.enabled, "G3 failed to restore action enabled states on disable.");
        UnityEngine.Object.Destroy(owner);
        UnityEngine.Object.Destroy(asset);
    }

    private static void Capture(AnatomyNavigationController nav)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Transform display = nav.InspectionDisplay.BoneVisual;
        Transform reference = nav.InspectionDisplay.ReferenceVisual;
        var cameraObject = new GameObject("Validation capture camera");
        var camera = cameraObject.AddComponent<Camera>();
        Vector3 center = display.position + display.parent.right * 0.18f;
        camera.transform.SetPositionAndRotation(center - display.parent.forward * 1.6f, display.parent.rotation);
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 20f;
        camera.fieldOfView = 48f;
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        var texture = new RenderTexture(1400, 1000, 24);
        camera.targetTexture = texture;
        camera.Render();
        var prior = RenderTexture.active;
        RenderTexture.active = texture;
        var image = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/G3-C1.png", image.EncodeToPNG());
        RenderTexture.active = prior;
        camera.targetTexture = null;
        UnityEngine.Object.Destroy(texture);
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(cameraObject);
    }

    [Serializable]
    private class Report
    {
        public bool passed;
        public int assertions;
        public int bones;
        public int vertebralBones;
        public int skullBones;
        public int ribcageBones;
        public int leftLowerLimbBones;
        public int pectoralBones;
        public int pelvicBones;
        public int rightLowerLimbBones;
        public int upperLimbBones;
        public string error;
    }

    private static void Finish(string error)
    {
        SessionState.SetBool(RunningKey, false);
        Directory.CreateDirectory("Logs");
        int vertebralCount = SessionState.GetInt(CountKey, 0);
        var report = new Report { passed = error == null, assertions = assertions,
            bones = vertebralCount + 29 + 25 + 30 + 4 + 1 + 32 + 60,
            vertebralBones = vertebralCount, skullBones = 29,
            ribcageBones = 25, leftLowerLimbBones = 30, pectoralBones = 4, pelvicBones = 1,
            rightLowerLimbBones = 32, upperLimbBones = 60,
            error = error };
        File.WriteAllText("Logs/G3-validation.json", JsonUtility.ToJson(report, true));
        Debug.Log($"G3 validation: {(report.passed ? "PASS" : "FAIL")}, {assertions} assertions. {error}");
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
