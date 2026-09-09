using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class AnatomySceneSetup
{
    public const string ScenePath = "Assets/_Recovery/CONTROLLERS MIGRATION.unity";

    [MenuItem("Anatomy/Configure vertebral inspection")]
    public static void ConfigureAll() => Configure(false);
    public static void ConfigurePilot() => Configure(true);

    private static T Add<T>(GameObject target) where T : Component
    {
        // Unity's missing-component objects can be fake-null in the Editor; ?? bypasses that check.
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    public static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetText(UnityEngine.Object target, string name, string value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetArray(UnityEngine.Object target, string name, UnityEngine.Object[] values)
    {
        var serialized = new SerializedObject(target);
        var array = serialized.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static T Reference<T>(UnityEngine.Object target, string name) where T : UnityEngine.Object =>
        new SerializedObject(target).FindProperty(name).objectReferenceValue as T;

    private static void Configure(bool pilot)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before configuring anatomy.");
        Scene active = SceneManager.GetActiveScene();
        if (active.isDirty) throw new InvalidOperationException("Save your scene changes before configuring anatomy.");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).ToArray();
        GameObject overview = objects.Single(o => o.name == "low-poly-skeleton-prefab");
        GameObject axial = objects.Single(o => o.name == "Skeleton_axial");
        GameObject vertebral = objects.Single(o => o.name == "VERTEBRAL COLUMN");
        InfoBoardController board = objects.Select(o => o.GetComponent<InfoBoardController>()).Single(c => c != null);
        GameObject axialDivision = overview.GetComponentsInChildren<DivisionSelection>(true)
            .Single(c => c.GetComponent<ViewTransitionOnSelect>() != null &&
                Reference<GameObject>(c.GetComponent<ViewTransitionOnSelect>(), "nextView") == axial).gameObject;
        GameObject appendicular = overview.GetComponentsInChildren<DivisionSelection>(true)
            .Single(c => c.gameObject != axialDivision).gameObject;
        Material highlight = AssetDatabase.LoadAssetAtPath<Material>("Assets/HighlightMst.mat");
        if (highlight == null) throw new InvalidOperationException("Highlight material was not imported.");

        GameObject owner = objects.FirstOrDefault(o => o.name == "AnatomyNavigation") ?? new GameObject("AnatomyNavigation");
        var navigation = Add<AnatomyNavigationController>(owner);
        var display = Add<BoneInspectionDisplay>(owner);
        var reservation = Add<AnatomyInputReservation>(owner);
        Transform boneAnchor = Anchor(owner.transform, "Bone inspection anchor");
        Transform referenceAnchor = Anchor(owner.transform, "Column reference anchor");
        Bounds bounds = BoneInspectionDisplay.LocalBounds(vertebral.transform);
        Vector3 center = vertebral.transform.TransformPoint(bounds.center);
        Camera viewer = objects.Select(o => o.GetComponent<Camera>()).FirstOrDefault(c => c != null);
        Vector3 towardDisplay = center - (viewer != null ? viewer.transform.position : Vector3.zero);
        towardDisplay.y = 0f;
        if (towardDisplay.sqrMagnitude < 0.01f) towardDisplay = Vector3.forward;
        Quaternion facing = Quaternion.LookRotation(towardDisplay.normalized, Vector3.up);
        center -= towardDisplay.normalized * 0.35f;
        center.y = Mathf.Max(center.y, 1.35f);
        boneAnchor.SetPositionAndRotation(center, facing);
        referenceAnchor.SetPositionAndRotation(center - facing * Vector3.right * 0.42f, facing);
        Set(display, "boneAnchor", boneAnchor);
        Set(display, "referenceAnchor", referenceAnchor);
        Set(display, "highlightMaterial", highlight);
        Set(navigation, "overviewRoot", overview);
        Set(navigation, "axialDivisionRoot", axialDivision);
        Set(navigation, "appendicularDivisionRoot", appendicular);
        Set(navigation, "axialView", axial);
        Set(navigation, "vertebralView", vertebral);
        Set(navigation, "infoBoard", board);
        Set(navigation, "inspectionDisplay", display);
        Set(navigation, "inputReservation", reservation);
        SetArray(reservation, "actionManagers", objects.Select(o => o.GetComponent<InputActionManager>())
            .Where(c => c != null).Cast<UnityEngine.Object>().ToArray());
        foreach (var selection in overview.GetComponentsInChildren<DivisionSelection>(true)) Set(selection, "navigation", navigation);
        foreach (var transition in objects.Select(o => o.GetComponent<ViewTransitionOnSelect>()).Where(c => c != null))
            Set(transition, "navigation", navigation);

        var entries = VertebralBoneCatalog.All().Where(e => !pilot || e.meshName == "C1").ToArray();
        var selections = new BoneSelection[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var mesh = vertebral.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == entry.meshName);
            if (mesh.sharedMesh == null) throw new InvalidOperationException($"Missing imported mesh: {entry.meshName}");
            var info = Add<BonePartInfo>(mesh.gameObject);
            SetText(info, "partName", entry.title);
            SetText(info, "partDescription", entry.description);
            var collider = Add<MeshCollider>(mesh.gameObject);
            collider.sharedMesh = mesh.sharedMesh;
            collider.convex = false;
            var interactable = Add<XRSimpleInteractable>(mesh.gameObject);
            interactable.colliders.Clear();
            interactable.colliders.Add(collider);
            interactable.selectMode = InteractableSelectMode.Single;
            var selection = Add<BoneSelection>(mesh.gameObject);
            Set(selection, "navigation", navigation);
            Set(selection, "highlightMaterial", highlight);
            SetArray(selection, "boneRenderers", new UnityEngine.Object[] { mesh.GetComponent<MeshRenderer>() });
            selections[i] = selection;
        }
        SetArray(navigation, "bones", selections);
        foreach (var highlighter in vertebral.GetComponentsInChildren<BoneGroupHoverHighlighter>(true))
            highlighter.enabled = false;
        // Empty collider lists make XRI collect descendants, including another interactable's
        // colliders. Assign each collider to its nearest interactable so targeting has one owner.
        foreach (GameObject root in new[] { overview, axial, vertebral })
            foreach (var interactable in root.GetComponentsInChildren<XRBaseInteractable>(true))
            {
                var owned = interactable.GetComponentsInChildren<Collider>(true)
                    .Where(c => c.GetComponentInParent<XRBaseInteractable>(true) == interactable).ToArray();
                interactable.colliders.Clear();
                interactable.colliders.AddRange(owned);
                interactable.enabled = owned.Length > 0;
            }

        Set(board, "navigation", navigation);
        GameObject backObject = Reference<GameObject>(board, "backButton");
        Button back = backObject.GetComponent<Button>();
        while (back.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(back.onClick, 0);
        UnityEventTools.AddPersistentListener(back.onClick, navigation.GoBack);
        foreach (var step in objects.Select(o => o.GetComponent<StepManager>()).Where(c => c != null))
        {
            step.enabled = false;
            if (step.nextButton != null) step.nextButton.gameObject.SetActive(false);
            foreach (var card in step.cards) if (card != null) card.SetActive(false);
        }
        ConfigurePanel(owner.transform, board, back, viewer, center, facing);
        board.ShowNavigationInfo("Human Skeleton", "Select a division to start.", "Back to skeleton", false);
        overview.SetActive(true);
        axialDivision.SetActive(true);
        appendicular.SetActive(true);
        axial.SetActive(false);
        vertebral.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"G3 scene configured with {entries.Length} bones. Display center: {center}.");
    }

    private static Transform Anchor(Transform owner, string name)
    {
        Transform found = owner.Find(name);
        if (found != null) return found;
        var anchor = new GameObject(name).transform;
        anchor.SetParent(owner, false);
        return anchor;
    }

    private static void ConfigurePanel(Transform owner, InfoBoardController board, Button back,
        Camera viewer, Vector3 center, Quaternion facing)
    {
        TMP_FontAsset font = Reference<TextMeshProUGUI>(board, "titleText").font;
        Transform existing = owner.Find("Anatomy information panel");
        GameObject panel = existing != null ? existing.gameObject :
            new GameObject("Anatomy information panel", typeof(RectTransform));
        panel.transform.SetParent(owner, false);
        panel.transform.SetPositionAndRotation(center + facing * Vector3.right * 0.64f, facing);
        panel.transform.localScale = Vector3.one * 0.001f;
        var panelRect = (RectTransform)panel.transform;
        panelRect.sizeDelta = new Vector2(560, 680);
        var canvas = Add<Canvas>(panel);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = viewer;
        Add<TrackedDeviceGraphicRaycaster>(panel);
        Add<Image>(panel).color = new Color(0.035f, 0.055f, 0.075f, 0.98f);
        TextMeshProUGUI title = Text(panelRect, "Anatomy title", font, 32, FontStyles.Bold);
        Layout(title.rectTransform, new Vector2(0, 276), new Vector2(508, 70));
        TextMeshProUGUI description = Text(panelRect, "Anatomy description", font, 24, FontStyles.Normal);
        Layout(description.rectTransform, new Vector2(0, -4), new Vector2(508, 472));
        description.enableAutoSizing = true;
        description.fontSizeMin = 20;
        description.fontSizeMax = 24;
        description.alignment = TextAlignmentOptions.TopLeft;

        // Reuse the existing Back button, with presentation owned by this scene.
        back.transform.SetParent(panelRect, false);
        Layout((RectTransform)back.transform, new Vector2(0, -292), new Vector2(508, 56));
        foreach (Transform child in back.transform) child.gameObject.SetActive(false);
        foreach (var collider in back.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var interactable in back.GetComponentsInChildren<XRBaseInteractable>(true)) interactable.enabled = false;
        Image buttonImage = Add<Image>(back.gameObject);
        buttonImage.enabled = true;
        buttonImage.sprite = null;
        buttonImage.raycastTarget = true;
        buttonImage.color = new Color(0.10f, 0.30f, 0.42f, 1);
        back.targetGraphic = buttonImage;
        var colors = back.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.7f, 0.9f, 1, 1);
        colors.pressedColor = new Color(0.5f, 0.75f, 0.9f, 1);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 1);
        back.colors = colors;
        TextMeshProUGUI label = Text((RectTransform)back.transform, "Anatomy back label", font, 24, FontStyles.Normal);
        Layout(label.rectTransform, Vector2.zero, new Vector2(484, 52));
        label.alignment = TextAlignmentOptions.Center;
        label.gameObject.SetActive(true);
        foreach (Transform oldCard in board.transform) oldCard.gameObject.SetActive(false);
        Set(board, "infoPanel", panel);
        Set(board, "titleText", title);
        Set(board, "descriptionText", description);
        Set(board, "backLabel", label);
        panel.SetActive(true);
    }

    private static TextMeshProUGUI Text(RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var text = Add<TextMeshProUGUI>(obj);
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = new Color(0.94f, 0.97f, 1, 1);
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static void Layout(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition3D = new Vector3(position.x, position.y, 0);
        rect.sizeDelta = size;
    }
}
