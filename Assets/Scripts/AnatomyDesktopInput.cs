using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Mouse and keyboard input for the existing anatomy scene in a Web player.</summary>
public sealed class AnatomyDesktopInput : MonoBehaviour
{
    private readonly RaycastHit[] hits = new RaycastHit[128];
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private AnatomyNavigationController navigation;
    private Camera viewer;
    private Transform cameraRig;
    private XRUIInputModule uiInput;
    private PointerEventData pointer;
    private Component hovered;
    private Transform draggedPanel;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private bool rightDragStartedOverUi;
    private bool middleDragStartedOverUi;
    private float nextCanvasScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer ||
            SceneManager.GetActiveScene().name != "CONTROLLERS MIGRATION") return;
        new GameObject("Anatomy desktop input").AddComponent<AnatomyDesktopInput>();
    }

    private IEnumerator Start()
    {
        while (navigation == null || !navigation.CanNavigate || viewer == null || EventSystem.current == null)
        {
            navigation = FindFirstObjectByType<AnatomyNavigationController>();
            viewer = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            yield return null;
        }

        uiInput = EventSystem.current.GetComponent<XRUIInputModule>();
        if (uiInput == null)
        {
            Debug.LogError("Web input needs the scene XR UI input module.", this);
            enabled = false;
            yield break;
        }
        uiInput.enableMouseInput = true;
        uiInput.enableXRInput = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.stickyCursorLock = false;
#endif
        XROrigin origin = viewer.GetComponentInParent<XROrigin>();
        cameraRig = origin != null ? origin.transform : viewer.transform.root;
        pointer = new PointerEventData(EventSystem.current);
        AddScreenRaycasters();
        if (FindFirstObjectByType<GraphicRaycaster>() == null)
            Debug.LogError("Web input found no mouse-raycastable anatomy panel.", this);
    }

    private void Update()
    {
        if (pointer == null || navigation == null || viewer == null) return;
        var mouse = Mouse.current;
        if (mouse == null) return;

        Keyboard keys = Keyboard.current;
        bool accountInput = LearnerAccountPanel.CapturesDesktopInput;
        if (accountInput && Cursor.lockState == CursorLockMode.Locked) ReleaseMouse();
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            if (keys != null && (keys.fKey.wasPressedThisFrame ||
                                 keys.tabKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame))
            {
                ReleaseMouse();
                return;
            }
            HoverAt(null);
            HandleKeyboard();
            LookCamera(mouse.delta.ReadValue());
            return;
        }
        Cursor.visible = true;
        if (!uiInput.enableMouseInput) ReleaseMouse();
        if (!accountInput && keys != null && keys.fKey.wasPressedThisFrame)
        {
            CaptureMouse();
            return;
        }

        if (Time.unscaledTime >= nextCanvasScan)
        {
            AddScreenRaycasters();
            nextCanvasScan = Time.unscaledTime + 1f;
        }

        Vector2 position = mouse.position.ReadValue();
        bool overUi = IsOverUi(position);
        if (mouse.rightButton.wasPressedThisFrame) rightDragStartedOverUi = overUi;
        if (mouse.middleButton.wasPressedThisFrame) middleDragStartedOverUi = overUi;
        if (mouse.leftButton.wasReleasedThisFrame) draggedPanel = null;

        if (draggedPanel != null)
            DragPanel(position);
        else if (mouse.leftButton.wasPressedThisFrame && !overUi)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (!TryBeginPanelDrag(position))
            {
                Component target = TargetAt(position);
                if (target != null) SelectTarget(target);
                else if (!accountInput)
                {
                    CaptureMouse();
                    return;
                }
            }
        }

        if (draggedPanel == null && !mouse.leftButton.isPressed)
            HoverAt(overUi ? null : TargetAt(position));
        else
            HoverAt(null);

        if (!accountInput)
        {
            HandleKeyboard();
            HandleMouseMotion(mouse, overUi);
        }
    }

    private void CaptureMouse()
    {
        EventSystem.current.SetSelectedGameObject(null);
        HoverAt(null);
        uiInput.enableMouseInput = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ReleaseMouse()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (uiInput != null) uiInput.enableMouseInput = true;
    }

    private void LookCamera(Vector2 delta)
    {
        const float sensitivity = 0.15f;
        cameraRig.Rotate(Vector3.up, delta.x * sensitivity, Space.World);
        float pitch = Mathf.Asin(Mathf.Clamp(viewer.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        float nextPitch = Mathf.Clamp(pitch + delta.y * sensitivity, -80f, 80f);
        cameraRig.Rotate(viewer.transform.right, pitch - nextPitch, Space.World);
    }

    private void AddScreenRaycasters()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (canvas.renderMode == RenderMode.WorldSpace &&
                canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private bool IsOverUi(Vector2 position)
    {
        pointer.Reset();
        pointer.position = position;
        uiHits.Clear();
        EventSystem.current.RaycastAll(pointer, uiHits);
        foreach (RaycastResult hit in uiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    private Component TargetAt(Vector2 position)
    {
        if (!navigation.CanNavigate || navigation.Level == AnatomyLevel.Bone) return null;
        int count = Physics.RaycastNonAlloc(viewer.ScreenPointToRay(position), hits, 100f);
        Component nearest = null;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || hits[i].distance >= distance) continue;
            Component candidate = null;
            if (navigation.Level == AnatomyLevel.Whole)
                candidate = collider.GetComponentInParent<DivisionSelection>();
            else if (navigation.Level == AnatomyLevel.Division)
            {
                var group = collider.GetComponentInParent<ViewTransitionOnSelect>();
                if (group != null && group.NextView != null &&
                    group.transform.IsChildOf(navigation.CurrentView.transform))
                    candidate = group;
            }
            else if (navigation.Level == AnatomyLevel.Group)
            {
                var bone = collider.GetComponentInParent<BoneSelection>();
                if (bone != null && bone.transform.IsChildOf(navigation.CurrentView.transform))
                    candidate = bone;
            }
            if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
            nearest = candidate;
            distance = hits[i].distance;
        }
        return nearest;
    }

    private void SelectTarget(Component target)
    {
        if (target is DivisionSelection division) division.SelectFromDesktop();
        else if (target is ViewTransitionOnSelect group) group.SelectFromDesktop();
        else if (target is BoneSelection bone) navigation.InspectBone(bone);
        HoverAt(null);
    }

    private void HoverAt(Component target)
    {
        if (hovered == target) return;
        if (hovered is BoneSelection oldBone) oldBone.SetDesktopHover(false);
        else if (hovered != null)
            hovered.GetComponent<BoneGroupHoverHighlighter>()?.SetDesktopHover(false);
        hovered = target;
        if (target is BoneSelection bone) bone.SetDesktopHover(true);
        else if (target != null)
            target.GetComponent<BoneGroupHoverHighlighter>()?.SetDesktopHover(true);
    }

    private bool TryBeginPanelDrag(Vector2 position)
    {
        int count = Physics.RaycastNonAlloc(viewer.ScreenPointToRay(position), hits, 100f);
        float nearest = float.PositiveInfinity;
        Transform panel = null;
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider.name != "Move handle" || hits[i].distance >= nearest)
                continue;
            var grab = collider.GetComponentInParent<XRGrabInteractable>();
            if (grab == null) continue;
            nearest = hits[i].distance;
            panel = grab.transform;
        }
        if (panel == null) return false;
        draggedPanel = panel;
        dragPlane = new Plane(-viewer.transform.forward, panel.position);
        Ray ray = viewer.ScreenPointToRay(position);
        if (dragPlane.Raycast(ray, out float distance))
            dragOffset = panel.position - ray.GetPoint(distance);
        else
            dragOffset = Vector3.zero;
        return true;
    }

    private void DragPanel(Vector2 position)
    {
        Ray ray = viewer.ScreenPointToRay(position);
        if (!dragPlane.Raycast(ray, out float distance)) return;
        draggedPanel.position = ray.GetPoint(distance) + dragOffset;
        Physics.SyncTransforms();
    }

    private void HandleKeyboard()
    {
        Keyboard keys = Keyboard.current;
        if (keys == null) return;
        if (keys.backspaceKey.wasPressedThisFrame) navigation.GoBack();
        if (keys.yKey.wasPressedThisFrame) navigation.SwitchPectoralSide();
        MoveCamera(keys);
        bool uiSelected = EventSystem.current.currentSelectedGameObject != null;

        if (navigation.Level == AnatomyLevel.Bone)
        {
            Vector2 direction = uiSelected ? Vector2.zero : new Vector2(
                (keys.rightArrowKey.isPressed ? 1f : 0f) - (keys.leftArrowKey.isPressed ? 1f : 0f),
                (keys.upArrowKey.isPressed ? 1f : 0f) - (keys.downArrowKey.isPressed ? 1f : 0f));
            navigation.ApplyDesktopInspection(direction, Time.deltaTime,
                keys.rKey.wasPressedThisFrame, keys.tKey.wasPressedThisFrame);
        }
        else if (!uiSelected)
        {
            float direction = (keys.rightArrowKey.isPressed ? 1f : 0f) -
                              (keys.leftArrowKey.isPressed ? 1f : 0f);
            RotateModel(-direction * 90f * Time.deltaTime);
        }
    }

    private void MoveCamera(Keyboard keys)
    {
        Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = forward * ((keys.wKey.isPressed ? 1f : 0f) -
                                       (keys.sKey.isPressed ? 1f : 0f)) +
                            right * ((keys.dKey.isPressed ? 1f : 0f) -
                                     (keys.aKey.isPressed ? 1f : 0f)) +
                            Vector3.up * ((keys.spaceKey.isPressed ? 1f : 0f) -
                                          (keys.cKey.isPressed ? 1f : 0f));
        if (direction.sqrMagnitude > 1f) direction.Normalize();
        float speed = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed ? 4f : 2f;
        cameraRig.position += direction * speed * Time.deltaTime;
    }

    private void HandleMouseMotion(Mouse mouse, bool overUi)
    {
        Vector2 delta = mouse.delta.ReadValue();
        Keyboard keys = Keyboard.current;
        bool shift = keys != null && (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed);
        if (draggedPanel == null && mouse.rightButton.isPressed && !rightDragStartedOverUi)
        {
            if (shift) OrbitCamera(delta);
            else if (navigation.Level == AnatomyLevel.Bone)
                navigation.ApplyDesktopInspection(delta * 0.005f, 1f);
            else
                RotateModel(-delta.x * 0.35f);
        }
        if (draggedPanel == null && mouse.middleButton.isPressed && !middleDragStartedOverUi)
            cameraRig.position -= (viewer.transform.right * delta.x +
                viewer.transform.up * delta.y) * 0.003f;
        float scroll = mouse.scroll.ReadValue().y;
        if (draggedPanel == null && !overUi && Mathf.Abs(scroll) > 0f)
        {
            float distance = Vector3.Distance(viewer.transform.position, ViewCenter());
            float step = Mathf.Clamp(scroll * 0.002f * distance, -0.5f, 0.5f);
            if (distance - step > 0.3f) cameraRig.position += viewer.transform.forward * step;
        }
    }

    private void RotateModel(float angle)
    {
        if (Mathf.Approximately(angle, 0f) || navigation.CurrentView == null) return;
        navigation.CurrentView.GetComponent<SkeletonYawRotator>()?.RotateYaw(angle);
    }

    private void OrbitCamera(Vector2 delta)
    {
        Vector3 center = ViewCenter();
        cameraRig.RotateAround(center, Vector3.up, delta.x * 0.2f);
        cameraRig.RotateAround(center, viewer.transform.right, -delta.y * 0.2f);
    }

    private Vector3 ViewCenter()
    {
        Transform root = navigation.Level == AnatomyLevel.Bone
            ? navigation.InspectionDisplay?.BoneVisual : navigation.CurrentView?.transform;
        if (root == null) return viewer.transform.position + viewer.transform.forward * 2f;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = default;
        bool found = false;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found ? bounds.center : root.position;
    }

    private void OnDisable()
    {
        HoverAt(null);
        ReleaseMouse();
    }
}
