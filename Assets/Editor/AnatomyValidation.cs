using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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
        var overview = nav.CurrentView;
        var axialDivision = AnatomySceneSetup.Reference<GameObject>(nav, "axialDivisionRoot");
        var appendicular = AnatomySceneSetup.Reference<GameObject>(nav, "appendicularDivisionRoot");
        var group = AnatomySceneSetup.Reference<GameObject>(nav, "vertebralView");
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
        foreach (var root in new[] { overview, AnatomySceneSetup.Reference<GameObject>(nav, "axialView"), group })
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
        var groupTransition = nav.CurrentView.GetComponentsInChildren<ViewTransitionOnSelect>(true)
            .Single(t => AnatomySceneSetup.Reference<GameObject>(t, "nextView") == group);
        var groupTarget = groupTransition.GetComponentsInChildren<XRSimpleInteractable>(true).First(i => i.enabled);
        Require(!manager.IsSelectPossible((IXRSelectInteractor)ray2, (IXRSelectInteractable)groupTarget),
            "The second controller can select through the transition gate.");
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
        nav.GoBack();
        Require(nav.Level == AnatomyLevel.Whole && overview.activeSelf && axialDivision.activeSelf && appendicular.activeSelf, "G1 Back must restore G0.");
        yield return null; yield return null;
        Require(nav.SelectDivision(appendicular, "Appendicular", "Appendicular overview."), "Appendicular selection regressed.");
        Require(!axialDivision.activeSelf && appendicular.activeInHierarchy, "Appendicular isolation regressed.");
        yield return null; yield return null;
        nav.GoBack();
        yield return null; yield return null;
        Require(nav.SelectDivision(axialDivision, "Axial", "Axial overview."), "Cannot re-enter axial division.");
        yield return null; yield return null;
        Require(nav.EnterGroup(group, "Vertebral column", "Group overview."), "Cannot re-enter vertebral group.");
        yield return null; yield return null;
        Require(nav.InspectBone(nav.Bones[0]), "Cannot re-inspect the same bone.");
        yield return null; yield return null;
        nav.GoBack();
        UnityEngine.Object.Destroy(ray1.gameObject);
        UnityEngine.Object.Destroy(ray2.gameObject);
        yield return null; yield return null;
        UnityEngine.SceneManagement.SceneManager.LoadScene(AnatomySceneSetup.ScenePath);
        yield return null; yield return null; yield return null;
        var reloaded = UnityEngine.Object.FindFirstObjectByType<AnatomyNavigationController>();
        Require(reloaded != null && reloaded.Level == AnatomyLevel.Whole, "Scene reload did not restart at G0.");
        Require(projectErrors.Count == 0, "Project runtime errors: " + string.Join("\n", projectErrors));
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
    private class Report { public bool passed; public int assertions; public int bones; public string error; }

    private static void Finish(string error)
    {
        SessionState.SetBool(RunningKey, false);
        Directory.CreateDirectory("Logs");
        var report = new Report { passed = error == null, assertions = assertions,
            bones = SessionState.GetInt(CountKey, 0), error = error };
        File.WriteAllText("Logs/G3-validation.json", JsonUtility.ToJson(report, true));
        Debug.Log($"G3 validation: {(report.passed ? "PASS" : "FAIL")}, {assertions} assertions. {error}");
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
