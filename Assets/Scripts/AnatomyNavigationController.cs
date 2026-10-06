using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.UI;

public enum AnatomyLevel { Whole, Division, Group, Bone }

[DisallowMultipleComponent]
public sealed class AnatomyNavigationController : MonoBehaviour
{
    [SerializeField] private GameObject overviewRoot;
    [SerializeField] private GameObject axialDivisionRoot;
    [SerializeField] private GameObject appendicularDivisionRoot;
    [SerializeField] private GameObject axialView;
    [SerializeField] private GameObject appendicularView;
    [SerializeField] private GameObject vertebralView;
    [SerializeField] private GameObject skullView;
    [SerializeField] private GameObject ribView;
    [SerializeField] private GameObject leftLowerLimbView;
    [SerializeField] private GameObject rightPectoralView;
    [SerializeField] private GameObject leftPectoralView;
    [SerializeField] private GameObject pelvicView;
    [SerializeField] private GameObject rightLowerLimbView;
    [SerializeField] private GameObject leftUpperLimbView;
    [SerializeField] private GameObject rightUpperLimbView;
    [SerializeField] private Material groupHighlightMaterial;
    [SerializeField] private InfoBoardController infoBoard;
    [SerializeField] private BoneInspectionDisplay inspectionDisplay;
    [SerializeField] private AnatomyInputReservation inputReservation;
    [SerializeField] private BoneSelection[] bones;
    [SerializeField] private BoneSelection[] skullBones;
    [SerializeField] private SkullExplosionController skullExplosion;
    [SerializeField] private Slider skullSpreadSlider;
    [SerializeField] private BoneSelection[] ribBones;
    [SerializeField] private BoneSelection[] leftLowerLimbBones;
    [SerializeField] private BoneSelection[] rightPectoralBones;
    [SerializeField] private BoneSelection[] leftPectoralBones;
    [SerializeField] private BoneSelection[] pelvicBones;
    [SerializeField] private BoneSelection[] rightLowerLimbBones;
    [SerializeField] private BoneSelection[] leftUpperLimbBones;
    [SerializeField] private BoneSelection[] rightUpperLimbBones;

    private readonly Stack<ViewFrame> history = new Stack<ViewFrame>();
    private readonly AnatomySelectionGate selectionGate = new AnatomySelectionGate();
    private readonly BoneInspectionPose inspectionPose = new BoneInspectionPose();
    private readonly List<XRBaseInteractable> filtered = new List<XRBaseInteractable>();
    private readonly List<ViewTransitionOnSelect> groupTransitions = new List<ViewTransitionOnSelect>();
    private readonly List<GameObject> groupViews = new List<GameObject>();
    private XRSelectFilterDelegate selectFilter;
    private ViewFrame current;
    private BoneSelection previewBone;
    private Quaternion authoredGroupOrientation;
    private Quaternion authoredSkullOrientation;
    private Quaternion authoredRibOrientation;
    private Quaternion authoredLeftLowerLimbOrientation;
    private Quaternion authoredRightPectoralOrientation;
    private Quaternion authoredLeftPectoralOrientation;
    private Quaternion authoredPelvicOrientation;
    private Quaternion authoredRightLowerLimbOrientation;
    private Quaternion authoredLeftUpperLimbOrientation;
    private Quaternion authoredRightUpperLimbOrientation;
    private InputDevice rightController;
    private InputDevice leftController;
    private UnityEngine.InputSystem.InputAction pectoralSwitchAction;
    private UnityEngine.InputSystem.InputAction backAction;
    private int pectoralSwitchControlCount;
    private int backControlCount;
    private bool controlsReady;
    private bool pectoralSwitchReady;
    private bool backInputReady;
    private bool lastBack;
    private bool lastY;
    private bool lastA;
    private bool lastB;
    private bool initialized;

    public AnatomyLevel Level => current != null ? current.level : AnatomyLevel.Whole;
    public BoneSelection SelectedBone { get; private set; }
    public GameObject CurrentView => current?.root;
    public int HistoryCount => history.Count;
    public bool CanNavigate => initialized && isActiveAndEnabled && selectionGate.CanSelect;
    public BoneInspectionPose InspectionPose => inspectionPose;
    public BoneInspectionDisplay InspectionDisplay => inspectionDisplay;
    public BoneSelection[] Bones => bones;
    public BoneSelection[] SkullBones => skullBones;
    public SkullExplosionController SkullExplosion => skullExplosion;
    public Slider SkullSpreadSlider => skullSpreadSlider;
    public BoneSelection[] RibBones => ribBones;
    public BoneSelection[] LeftLowerLimbBones => leftLowerLimbBones;
    public BoneSelection[] RightPectoralBones => rightPectoralBones;
    public BoneSelection[] LeftPectoralBones => leftPectoralBones;
    public BoneSelection[] PelvicBones => pelvicBones;
    public BoneSelection[] RightLowerLimbBones => rightLowerLimbBones;
    public BoneSelection[] LeftUpperLimbBones => leftUpperLimbBones;
    public BoneSelection[] RightUpperLimbBones => rightUpperLimbBones;
    public Material GroupHighlightMaterial => groupHighlightMaterial;

    private sealed class ViewFrame
    {
        public AnatomyLevel level;
        public GameObject root;
        public GameObject division;
        public string title;
        public string description;
        public string breadcrumb;
        private Vector3 position;
        private Quaternion rotation;
        private Vector3 scale;

        public void Capture()
        {
            if (root == null) return;
            position = root.transform.localPosition;
            rotation = root.transform.localRotation;
            scale = root.transform.localScale;
        }

        public void Restore()
        {
            if (root == null) return;
            root.transform.localPosition = position;
            root.transform.localRotation = rotation;
            root.transform.localScale = scale;
        }
    }

    private void Awake()
    {
        RegisterGroupTransitions(axialView);
        RegisterGroupTransitions(appendicularView);
    }

    private void RegisterGroupTransitions(GameObject divisionView)
    {
        if (divisionView == null) return;
        foreach (var transition in divisionView.GetComponentsInChildren<ViewTransitionOnSelect>(true))
        {
            if (!transition.enabled || transition.NextView == null || groupTransitions.Contains(transition)) continue;
            groupTransitions.Add(transition);
            if (transition.NextView != vertebralView && !groupViews.Contains(transition.NextView))
                groupViews.Add(transition.NextView);
            transition.Prepare(this);
        }
    }

    private void Start() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        if (overviewRoot == null || axialView == null || vertebralView == null ||
            infoBoard == null || inspectionDisplay == null || inputReservation == null ||
            bones == null || bones.Length == 0)
        {
            Debug.LogError("Anatomy navigation is missing scene references. Run Anatomy/Configure vertebral inspection.", this);
            enabled = false;
            return;
        }
        authoredGroupOrientation = vertebralView.transform.rotation;
        if (skullView != null) authoredSkullOrientation = skullView.transform.rotation;
        if (ribView != null) authoredRibOrientation = ribView.transform.rotation;
        if (leftLowerLimbView != null)
            authoredLeftLowerLimbOrientation = leftLowerLimbView.transform.rotation;
        if (rightPectoralView != null)
            authoredRightPectoralOrientation = rightPectoralView.transform.rotation;
        if (leftPectoralView != null)
            authoredLeftPectoralOrientation = leftPectoralView.transform.rotation;
        if (pelvicView != null) authoredPelvicOrientation = pelvicView.transform.rotation;
        if (rightLowerLimbView != null)
            authoredRightLowerLimbOrientation = rightLowerLimbView.transform.rotation;
        if (leftUpperLimbView != null)
            authoredLeftUpperLimbOrientation = leftUpperLimbView.transform.rotation;
        if (rightUpperLimbView != null)
            authoredRightUpperLimbOrientation = rightUpperLimbView.transform.rotation;
        pectoralSwitchAction = new UnityEngine.InputSystem.InputAction("Pectoral side switch",
            UnityEngine.InputSystem.InputActionType.Button,
            "<XRController>{LeftHand}/{SecondaryButton}");
        pectoralSwitchAction.Enable();
        backAction = new UnityEngine.InputSystem.InputAction("Anatomy Back",
            UnityEngine.InputSystem.InputActionType.Button,
            "<XRController>{LeftHand}/{PrimaryButton}");
        backAction.Enable();
        selectFilter = new XRSelectFilterDelegate((interactor, interactable) => CanNavigate);
        foreach (GameObject root in SelectionRoots())
        {
            if (root == null) continue;
            foreach (XRBaseInteractable interactable in root.GetComponentsInChildren<XRBaseInteractable>(true))
                if (!filtered.Contains(interactable))
                {
                    filtered.Add(interactable);
                    interactable.selectFilters.Add(selectFilter);
                }
        }
        initialized = true;
        current = new ViewFrame { level = AnatomyLevel.Whole, root = overviewRoot,
            title = infoBoard.DefaultTitle, description = infoBoard.DefaultDescription, breadcrumb = "" };
        current.Capture();
        ShowFrame(current);
        selectionGate.Block(Time.frameCount);
    }

    private void Update()
    {
        if (!initialized) return;
        bool pressed = inputReservation.IsSelectionPressed() || IsControllerPressed(XRNode.LeftHand) ||
                       IsControllerPressed(XRNode.RightHand);
        selectionGate.Poll(pressed, Time.frameCount);
        infoBoard.SetNavigationBackInteractable(CanNavigate);
        if (Level == AnatomyLevel.Bone) UpdateInspectionInput();
        UpdatePectoralSwitchInput();
        UpdateBackInput();
    }

    private void UpdateBackInput()
    {
        var connected = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        int controlCount = backAction != null ? backAction.controls.Count : 0;
        if (controlCount != backControlCount)
        {
            backInputReady = false;
            backControlCount = controlCount;
        }
        bool legacyX = connected.isValid &&
            connected.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed) && pressed;
        bool x = legacyX || (backAction != null && backAction.IsPressed());
        if (!backInputReady)
        {
            backInputReady = !x;
            lastBack = x;
            return;
        }
        if (x && !lastBack && history.Count > 0) GoBack();
        lastBack = x;
    }

    private void UpdatePectoralSwitchInput()
    {
        if (Level != AnatomyLevel.Group ||
            (current.root != rightPectoralView && current.root != leftPectoralView))
        {
            pectoralSwitchReady = false;
            return;
        }
        var connected = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        int controlCount = pectoralSwitchAction != null ? pectoralSwitchAction.controls.Count : 0;
        if (controlCount != pectoralSwitchControlCount)
        {
            pectoralSwitchReady = false;
            pectoralSwitchControlCount = controlCount;
        }
        if (connected.isValid && (!leftController.isValid || !leftController.Equals(connected)))
        {
            leftController = connected;
            pectoralSwitchReady = false;
        }
        else if (!connected.isValid && leftController.isValid)
        {
            leftController = default;
            pectoralSwitchReady = false;
        }
        bool legacyY = connected.isValid &&
            connected.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed) && pressed;
        bool y = legacyY || (pectoralSwitchAction != null && pectoralSwitchAction.IsPressed());
        if (!pectoralSwitchReady)
        {
            pectoralSwitchReady = !y;
            lastY = y;
            return;
        }
        if (y && !lastY) SwitchPectoralSide();
        lastY = y;
    }

    public bool SwitchPectoralSide()
    {
        if (!CanNavigate || Level != AnatomyLevel.Group ||
            (current.root != rightPectoralView && current.root != leftPectoralView)) return false;
        GameObject nextRoot = current.root == rightPectoralView ? leftPectoralView : rightPectoralView;
        if (nextRoot == null) return false;
        string title = nextRoot == leftPectoralView ? "Left pectoral girdle" : "Right pectoral girdle";
        string description = nextRoot == leftPectoralView
            ? "The left clavicle and scapula connect the upper limb to the trunk. They position the shoulder and support arm movement."
            : "The right clavicle and scapula connect the upper limb to the trunk. They position the shoulder and support arm movement.";
        var next = new ViewFrame { level = AnatomyLevel.Group, root = nextRoot, title = title,
            description = description, breadcrumb = "Appendicular → " + title };
        next.Capture();
        ShowFrame(next);
        selectionGate.Block(Time.frameCount);
        return true;
    }

    public void SetSkullSpread(float value)
    {
        if (!initialized || Level != AnatomyLevel.Group || current.root != skullView ||
            skullExplosion == null) return;
        skullExplosion.SetSpread(value);
    }

    private static bool IsControllerPressed(XRNode hand)
    {
        var device = InputDevices.GetDeviceAtXRNode(hand);
        return (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger) && trigger) ||
               (device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip) && grip);
    }

    private void UpdateInspectionInput()
    {
        var connected = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!connected.isValid)
        {
            controlsReady = false;
            rightController = default;
            return;
        }
        if (!rightController.isValid || !rightController.Equals(connected))
        {
            controlsReady = false;
            rightController = connected;
        }
        if (!rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return;
        rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool a);
        rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool b);
        if (!controlsReady)
        {
            controlsReady = stick.magnitude <= 0.15f && !a && !b;
            lastA = a;
            lastB = b;
            return;
        }
        inspectionPose.Apply(stick, Time.deltaTime, a && !lastA, b && !lastB);
        inspectionDisplay.SetRotation(inspectionPose.Rotation);
        lastA = a;
        lastB = b;
    }

    public bool SelectDivision(GameObject division, string title, string description)
    {
        if (!CanNavigate || Level != AnatomyLevel.Whole ||
            (division != axialDivisionRoot && division != appendicularDivisionRoot)) return false;
        var next = new ViewFrame { level = AnatomyLevel.Division, division = division,
            root = division == axialDivisionRoot ? axialView : appendicularView != null ? appendicularView : overviewRoot,
            title = title, description = description,
            breadcrumb = division == axialDivisionRoot ? "Axial" : "Appendicular" };
        Push(next);
        return true;
    }

    public bool EnterGroup(GameObject group, string title, string description)
    {
        if (!CanNavigate || Level != AnatomyLevel.Division || group == null) return false;
        bool belongsToCurrentDivision = false;
        foreach (var transition in groupTransitions)
            if (transition != null && transition.NextView == group &&
                transition.transform.IsChildOf(current.root.transform))
            {
                belongsToCurrentDivision = true;
                break;
            }
        if (!belongsToCurrentDivision) return false;
        Push(new ViewFrame { level = AnatomyLevel.Group, root = group, title = title,
            description = description, breadcrumb = current.breadcrumb + " → " + title });
        return true;
    }

    private IEnumerable<GameObject> SelectionRoots()
    {
        yield return overviewRoot;
        yield return axialView;
        yield return appendicularView;
        yield return vertebralView;
        yield return skullView;
        yield return ribView;
        yield return leftLowerLimbView;
        yield return rightPectoralView;
        yield return leftPectoralView;
        yield return pelvicView;
        yield return rightLowerLimbView;
        yield return leftUpperLimbView;
        yield return rightUpperLimbView;
        foreach (var view in groupViews) yield return view;
    }

    private IEnumerable<BoneSelection> AllBones()
    {
        foreach (var bone in bones) yield return bone;
        if (skullBones != null)
            foreach (var bone in skullBones) yield return bone;
        if (ribBones != null)
            foreach (var bone in ribBones) yield return bone;
        if (leftLowerLimbBones != null)
            foreach (var bone in leftLowerLimbBones) yield return bone;
        if (rightPectoralBones != null)
            foreach (var bone in rightPectoralBones) yield return bone;
        if (leftPectoralBones != null)
            foreach (var bone in leftPectoralBones) yield return bone;
        if (pelvicBones != null)
            foreach (var bone in pelvicBones) yield return bone;
        if (rightLowerLimbBones != null)
            foreach (var bone in rightLowerLimbBones) yield return bone;
        if (leftUpperLimbBones != null)
            foreach (var bone in leftUpperLimbBones) yield return bone;
        if (rightUpperLimbBones != null)
            foreach (var bone in rightUpperLimbBones) yield return bone;
    }

    public bool InspectBone(BoneSelection bone)
    {
        if (!CanNavigate || Level != AnatomyLevel.Group || bone == null || bone.Info == null) return false;
        GameObject group = current.root;
        BoneSelection[] entries = group == vertebralView ? bones :
            group == skullView ? skullBones :
            group == ribView ? ribBones : group == leftLowerLimbView ? leftLowerLimbBones :
            group == rightPectoralView ? rightPectoralBones :
            group == leftPectoralView ? leftPectoralBones : group == pelvicView ? pelvicBones :
            group == rightLowerLimbView ? rightLowerLimbBones :
            group == leftUpperLimbView ? leftUpperLimbBones :
            group == rightUpperLimbView ? rightUpperLimbBones : null;
        if (entries == null || System.Array.IndexOf(entries, bone) < 0 ||
            !bone.transform.IsChildOf(group.transform)) return false;
        ClearHighlights();
        Quaternion orientation = group == vertebralView ? authoredGroupOrientation :
            group == skullView ? authoredSkullOrientation :
            group == ribView ? authoredRibOrientation :
            group == leftLowerLimbView ? authoredLeftLowerLimbOrientation :
            group == rightPectoralView ? authoredRightPectoralOrientation :
            group == leftPectoralView ? authoredLeftPectoralOrientation :
            group == pelvicView ? authoredPelvicOrientation :
            group == rightLowerLimbView ? authoredRightLowerLimbOrientation :
            group == leftUpperLimbView ? authoredLeftUpperLimbOrientation :
            authoredRightUpperLimbOrientation;
        SkullExplosionController reference = group == skullView ? skullExplosion : null;
        if (reference != null) reference.CompleteTransition();
        try { inspectionDisplay.Show(bone, group.transform, orientation, reference); }
        catch (System.Exception error)
        {
            Debug.LogError($"Unable to inspect {bone.name}: {error.Message}", bone);
            return false;
        }
        current.Capture();
        history.Push(current);
        SelectedBone = bone;
        current = new ViewFrame { level = AnatomyLevel.Bone, title = bone.Info.PartName,
            description = bone.Info.PartDescription,
            breadcrumb = current.breadcrumb + " → " + bone.Info.PartName };
        group.SetActive(false);
        inspectionPose.Reset();
        inspectionDisplay.SetRotation(Quaternion.identity);
        controlsReady = false;
        inputReservation.Reserve();
        selectionGate.Block(Time.frameCount);
        Present();
        return true;
    }

    private void Push(ViewFrame next)
    {
        current.Capture();
        history.Push(current);
        next.Capture();
        ShowFrame(next);
        selectionGate.Block(Time.frameCount);
    }

    public void GoBack()
    {
        if (!CanNavigate || history.Count == 0) return;
        ExitInspection();
        ShowFrame(history.Pop());
        selectionGate.Block(Time.frameCount);
    }

    public void ReturnToOverview()
    {
        if (!initialized) return;
        ExitInspection();
        while (history.Count > 0) current = history.Pop();
        ShowFrame(current);
        selectionGate.Block(Time.frameCount);
    }

    private void ShowFrame(ViewFrame frame)
    {
        if (skullExplosion != null && frame.root != skullView && frame.level != AnatomyLevel.Bone)
            skullExplosion.ResetImmediate();
        ClearHighlights();
        overviewRoot.SetActive(false);
        axialView.SetActive(false);
        if (appendicularView != null) appendicularView.SetActive(false);
        vertebralView.SetActive(false);
        if (skullView != null) skullView.SetActive(false);
        if (ribView != null) ribView.SetActive(false);
        if (leftLowerLimbView != null) leftLowerLimbView.SetActive(false);
        if (rightPectoralView != null) rightPectoralView.SetActive(false);
        if (leftPectoralView != null) leftPectoralView.SetActive(false);
        if (pelvicView != null) pelvicView.SetActive(false);
        if (rightLowerLimbView != null) rightLowerLimbView.SetActive(false);
        if (leftUpperLimbView != null) leftUpperLimbView.SetActive(false);
        if (rightUpperLimbView != null) rightUpperLimbView.SetActive(false);
        foreach (var group in groupViews)
            if (group != null) group.SetActive(false);
        axialDivisionRoot.SetActive(frame.level == AnatomyLevel.Whole || frame.division == axialDivisionRoot);
        appendicularDivisionRoot.SetActive(frame.level == AnatomyLevel.Whole || frame.division == appendicularDivisionRoot);
        current = frame;
        pectoralSwitchReady = false;
        frame.Restore();
        frame.root.SetActive(true);
        Present();
    }

    private void ExitInspection()
    {
        if (inspectionDisplay != null) inspectionDisplay.Clear();
        if (inputReservation != null) inputReservation.Release();
        SelectedBone = null;
        controlsReady = false;
    }

    private void ClearHighlights()
    {
        previewBone = null;
        foreach (var bone in AllBones()) if (bone != null) bone.ClearHighlight();
        foreach (GameObject root in SelectionRoots())
        {
            if (root == null) continue;
            foreach (var highlighter in root.GetComponentsInChildren<BoneGroupHoverHighlighter>(true))
                highlighter.ClearHighlight();
        }
    }

    public void Preview(BoneSelection bone)
    {
        if (Level != AnatomyLevel.Group) return;
        previewBone = bone;
        Present();
    }

    public void EndPreview(BoneSelection bone)
    {
        if (previewBone != bone) return;
        previewBone = null;
        if (Level == AnatomyLevel.Group)
            foreach (var other in AllBones())
                if (other != null && other != bone && other.IsHovered) { previewBone = other; break; }
        Present();
    }

    private void Present()
    {
        if (current == null || infoBoard == null) return;
        if (skullSpreadSlider != null)
        {
            bool showSpread = Level == AnatomyLevel.Group && current.root == skullView;
            if (skullSpreadSlider.transform.parent.gameObject.activeSelf != showSpread)
                skullSpreadSlider.transform.parent.gameObject.SetActive(showSpread);
            if (showSpread && skullExplosion != null)
                skullSpreadSlider.SetValueWithoutNotify(skullExplosion.Spread);
        }
        string body = current.description;
        if (!string.IsNullOrEmpty(current.breadcrumb)) body = current.breadcrumb + "\n\n" + body;
        if (previewBone != null && previewBone.Info != null)
            body += "\n\nPointing at: " + previewBone.Info.PartName + "\nSelect to inspect.";
        if (Level == AnatomyLevel.Group &&
            (current.root == rightPectoralView || current.root == leftPectoralView))
            body += "\n\nLeft Y: switch pectoral side";
        if (Level == AnatomyLevel.Bone)
            body += "\n\nRight stick: turn / tilt\nA: reset turn    B: reset tilt";
        string back = Level == AnatomyLevel.Bone && history.Count > 0 ? "Back to " + history.Peek().title :
            Level == AnatomyLevel.Group && history.Count > 0 ? "Back to " + history.Peek().breadcrumb + " division" :
            "Back to skeleton";
        infoBoard.ShowNavigationInfo(current.title, body, back, history.Count > 0);
    }

    private void OnDisable()
    {
        pectoralSwitchAction?.Disable();
        backAction?.Disable();
        if (!initialized) return;
        ExitInspection();
        ClearHighlights();
    }

    private void OnEnable()
    {
        if (!initialized) return;
        pectoralSwitchAction?.Enable();
        backAction?.Enable();
        backInputReady = false;
        if (Level == AnatomyLevel.Bone && history.Count > 0) ShowFrame(history.Pop());
        selectionGate.Block(Time.frameCount);
    }

    private void OnDestroy()
    {
        pectoralSwitchAction?.Dispose();
        backAction?.Dispose();
        foreach (var interactable in filtered)
            if (interactable != null) interactable.selectFilters.Remove(selectFilter);
    }
}
