using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
    [SerializeField] private GameObject ribView;
    [SerializeField] private Material groupHighlightMaterial;
    [SerializeField] private InfoBoardController infoBoard;
    [SerializeField] private BoneInspectionDisplay inspectionDisplay;
    [SerializeField] private AnatomyInputReservation inputReservation;
    [SerializeField] private BoneSelection[] bones;
    [SerializeField] private BoneSelection[] ribBones;

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
    private Quaternion authoredRibOrientation;
    private InputDevice rightController;
    private bool controlsReady;
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
    public BoneSelection[] RibBones => ribBones;
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
        if (ribView != null) authoredRibOrientation = ribView.transform.rotation;
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
        yield return ribView;
        foreach (var view in groupViews) yield return view;
    }

    private IEnumerable<BoneSelection> AllBones()
    {
        foreach (var bone in bones) yield return bone;
        if (ribBones != null)
            foreach (var bone in ribBones) yield return bone;
    }

    public bool InspectBone(BoneSelection bone)
    {
        if (!CanNavigate || Level != AnatomyLevel.Group || bone == null || bone.Info == null) return false;
        GameObject group = current.root;
        BoneSelection[] entries = group == vertebralView ? bones : group == ribView ? ribBones : null;
        if (entries == null || System.Array.IndexOf(entries, bone) < 0 ||
            !bone.transform.IsChildOf(group.transform)) return false;
        ClearHighlights();
        Quaternion orientation = group == vertebralView ? authoredGroupOrientation : authoredRibOrientation;
        try { inspectionDisplay.Show(bone, group.transform, orientation); }
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
        ClearHighlights();
        overviewRoot.SetActive(false);
        axialView.SetActive(false);
        if (appendicularView != null) appendicularView.SetActive(false);
        vertebralView.SetActive(false);
        if (ribView != null) ribView.SetActive(false);
        foreach (var group in groupViews)
            if (group != null) group.SetActive(false);
        axialDivisionRoot.SetActive(frame.level == AnatomyLevel.Whole || frame.division == axialDivisionRoot);
        appendicularDivisionRoot.SetActive(frame.level == AnatomyLevel.Whole || frame.division == appendicularDivisionRoot);
        current = frame;
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
        string body = current.description;
        if (!string.IsNullOrEmpty(current.breadcrumb)) body = current.breadcrumb + "\n\n" + body;
        if (previewBone != null && previewBone.Info != null)
            body += "\n\nPointing at: " + previewBone.Info.PartName + "\nSelect to inspect.";
        if (Level == AnatomyLevel.Bone)
            body += "\n\nRight stick: turn / tilt\nA: reset turn    B: reset tilt";
        string back = Level == AnatomyLevel.Bone && history.Count > 0 ? "Back to " + history.Peek().title :
            Level == AnatomyLevel.Group && history.Count > 0 ? "Back to " + history.Peek().breadcrumb + " division" :
            "Back to skeleton";
        infoBoard.ShowNavigationInfo(current.title, body, back, history.Count > 0);
    }

    private void OnDisable()
    {
        if (!initialized) return;
        ExitInspection();
        ClearHighlights();
    }

    private void OnEnable()
    {
        if (!initialized) return;
        if (Level == AnatomyLevel.Bone && history.Count > 0) ShowFrame(history.Pop());
        selectionGate.Block(Time.frameCount);
    }

    private void OnDestroy()
    {
        foreach (var interactable in filtered)
            if (interactable != null) interactable.selectFilters.Remove(selectFilter);
    }
}
