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
    [SerializeField] private GameObject vertebralView;
    [SerializeField] private InfoBoardController infoBoard;
    [SerializeField] private BoneInspectionDisplay inspectionDisplay;
    [SerializeField] private AnatomyInputReservation inputReservation;
    [SerializeField] private BoneSelection[] bones;

    private readonly Stack<ViewFrame> history = new Stack<ViewFrame>();
    private readonly AnatomySelectionGate selectionGate = new AnatomySelectionGate();
    private readonly BoneInspectionPose inspectionPose = new BoneInspectionPose();
    private readonly List<XRBaseInteractable> filtered = new List<XRBaseInteractable>();
    private XRSelectFilterDelegate selectFilter;
    private ViewFrame current;
    private BoneSelection previewBone;
    private Quaternion authoredGroupOrientation;
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

    private void Start() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        if (overviewRoot == null || axialView == null || vertebralView == null || infoBoard == null ||
            inspectionDisplay == null || inputReservation == null || bones == null || bones.Length == 0)
        {
            Debug.LogError("Anatomy navigation is missing scene references. Run Anatomy/Configure vertebral inspection.", this);
            enabled = false;
            return;
        }
        authoredGroupOrientation = vertebralView.transform.rotation;
        selectFilter = new XRSelectFilterDelegate((interactor, interactable) => CanNavigate);
        foreach (GameObject root in new[] { overviewRoot, axialView, vertebralView })
            foreach (XRBaseInteractable interactable in root.GetComponentsInChildren<XRBaseInteractable>(true))
                if (!filtered.Contains(interactable))
                {
                    filtered.Add(interactable);
                    interactable.selectFilters.Add(selectFilter);
                }
        initialized = true;
        current = new ViewFrame { level = AnatomyLevel.Whole, root = overviewRoot,
            title = "Human Skeleton", description = "Select a division to start.", breadcrumb = "" };
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
            root = division == axialDivisionRoot ? axialView : overviewRoot,
            title = title, description = description,
            breadcrumb = division == axialDivisionRoot ? "Axial" : "Appendicular" };
        Push(next);
        return true;
    }

    public bool EnterGroup(GameObject group, string title, string description)
    {
        if (!CanNavigate || Level != AnatomyLevel.Division || current.division != axialDivisionRoot ||
            group != vertebralView) return false;
        Push(new ViewFrame { level = AnatomyLevel.Group, root = group, title = title,
            description = description, breadcrumb = "Axial → Vertebral column" });
        return true;
    }

    public bool InspectBone(BoneSelection bone)
    {
        if (!CanNavigate || Level != AnatomyLevel.Group || bone == null ||
            System.Array.IndexOf(bones, bone) < 0 || bone.Info == null) return false;
        ClearHighlights();
        try { inspectionDisplay.Show(bone, vertebralView.transform, authoredGroupOrientation); }
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
            breadcrumb = $"Axial → Vertebral column → {bone.name}" };
        vertebralView.SetActive(false);
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
        vertebralView.SetActive(false);
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
        foreach (var bone in bones) if (bone != null) bone.ClearHighlight();
        foreach (GameObject root in new[] { overviewRoot, axialView, vertebralView })
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
            foreach (var other in bones)
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
        string back = Level == AnatomyLevel.Bone ? "Back to vertebral column" :
            Level == AnatomyLevel.Group ? "Back to axial division" : "Back to skeleton";
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
