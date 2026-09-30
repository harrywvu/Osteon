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
    private static readonly Vector2 InspectionXZ = new Vector2(2.477991f, -1.173756f);

    [MenuItem("Anatomy/Configure vertebral inspection")]
    public static void ConfigureAll() => Configure(false);
    public static void ConfigurePilot() => Configure(true);

    [MenuItem("Anatomy/Configure rib inspection")]
    public static void ConfigureRibInspection()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before configuring anatomy.");
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your scene changes before configuring anatomy.");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).ToArray();
        var navigation = objects.Select(o => o.GetComponent<AnatomyNavigationController>()).Single(c => c != null);
        GameObject ribView = Named(objects, "G2_ribs", "G2_RIBCAGE");
        Material highlight = AssetDatabase.LoadAssetAtPath<Material>("Assets/HighlightMst.mat");
        if (highlight == null) throw new InvalidOperationException("Highlight material was not imported.");
        ConfigureRibBones(navigation, ribView, highlight);
        ribView.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Ribcage G3 configured with 24 ribs and the sternum.");
    }

    [MenuItem("Anatomy/Configure left lower limb inspection")]
    public static void ConfigureLeftLowerLimbInspection()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before configuring anatomy.");
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your scene changes before configuring anatomy.");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).ToArray();
        var navigation = objects.Select(o => o.GetComponent<AnatomyNavigationController>()).Single(c => c != null);
        GameObject view = Named(objects, "G2_LeftLowerLimb", "G2_LEFT_LOWER_LIMB");
        Material highlight = AssetDatabase.LoadAssetAtPath<Material>("Assets/HighlightMst.mat");
        if (highlight == null) throw new InvalidOperationException("Highlight material was not imported.");
        ConfigureLeftLowerLimbBones(navigation, view, highlight);
        view.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Left lower limb G3 configured with 30 bones.");
    }

    [MenuItem("Anatomy/Configure girdle inspection")]
    public static void ConfigureGirdleInspection()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before configuring anatomy.");
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your scene changes before configuring anatomy.");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).ToArray();
        var navigation = objects.Select(o => o.GetComponent<AnatomyNavigationController>()).Single(c => c != null);
        Material highlight = AssetDatabase.LoadAssetAtPath<Material>("Assets/HighlightMst.mat");
        if (highlight == null) throw new InvalidOperationException("Highlight material was not imported.");
        ConfigureGirdles(navigation, objects, highlight);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Girdle G3 configured: two bones per pectoral side and one pelvic hip bone.");
    }

    [MenuItem("Anatomy/Configure remaining limb inspection")]
    public static void ConfigureRemainingLimbInspection()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before configuring anatomy.");
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your scene changes before configuring anatomy.");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).ToArray();
        var navigation = objects.Select(o => o.GetComponent<AnatomyNavigationController>()).Single(c => c != null);
        Material highlight = AssetDatabase.LoadAssetAtPath<Material>("Assets/HighlightMst.mat");
        if (highlight == null) throw new InvalidOperationException("Highlight material was not imported.");
        ConfigureRemainingLimbs(navigation, objects, highlight);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Right lower and both upper-limb G3 views configured with 92 selectable meshes.");
    }

    [MenuItem("Anatomy/Configure inspection placement and panel movement")]
    public static void ConfigureInspectionPlacementAndPanels()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before configuring anatomy.");
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your scene changes before configuring anatomy.");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).ToArray();
        Transform owner = Named(objects, "AnatomyNavigation").transform;
        Transform boneAnchor = owner.Find("Bone inspection anchor");
        Transform referenceAnchor = owner.Find("Column reference anchor");
        if (boneAnchor == null || referenceAnchor == null)
            throw new InvalidOperationException("Inspection anchors are missing.");
        MoveInspectionAnchors(boneAnchor, referenceAnchor);
        var panel = owner.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(rect => rect.name == "Anatomy information panel");
        if (panel == null) throw new InvalidOperationException("Anatomy information panel is missing.");
        TMP_FontAsset font = panel.Find("Anatomy title").GetComponent<TextMeshProUGUI>().font;
        Transform station = EnsureAnatomyPanelStation(owner, panel);
        PanelMoveHandle.Configure(station, panel, font, new Vector2(0, -385), new Vector2(508, 58));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("G3 inspection moved to the requested X/Z and anatomy panel movement configured.");
    }

    private static void MoveInspectionAnchors(Transform boneAnchor, Transform referenceAnchor)
    {
        Vector3 position = boneAnchor.position;
        Vector3 delta = new Vector3(InspectionXZ.x - position.x, 0f, InspectionXZ.y - position.z);
        boneAnchor.position += delta;
        referenceAnchor.position += delta;
    }

    [MenuItem("Anatomy/Configure appendicular G1 UI")]
    public static void ConfigureAppendicularG1Ui()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before configuring anatomy.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).ToArray();
        GameObject appendicularView = Named(objects, "G1_Skeleton_appendicular", "Skeleton_appendicular");
        AnatomyNavigationController navigation = objects.Select(o => o.GetComponent<AnatomyNavigationController>())
            .Single(c => c != null);
        InfoBoardController board = objects.Select(o => o.GetComponent<InfoBoardController>()).Single(c => c != null);

        Set(navigation, "appendicularView", appendicularView);
        Set(board, "navigation", navigation);
        GameObject backObject = Reference<GameObject>(board, "backButton");
        Button back = backObject.GetComponent<Button>();
        while (back.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(back.onClick, 0);
        UnityEventTools.AddPersistentListener(back.onClick, navigation.GoBack);
        appendicularView.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Appendicular G1 now uses the anatomy information panel and Back navigation.");
    }

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

    private static GameObject Named(GameObject[] objects, params string[] names) =>
        objects.Single(o => names.Contains(o.name));

    private static void Configure(bool pilot)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before configuring anatomy.");
        Scene active = SceneManager.GetActiveScene();
        if (active.isDirty) throw new InvalidOperationException("Save your scene changes before configuring anatomy.");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).ToArray();
        GameObject overview = Named(objects, "G0_low-poly-skeleton-prefab", "low-poly-skeleton-prefab");
        GameObject axial = Named(objects, "G1_Skeleton_axial", "Skeleton_axial");
        GameObject appendicularView = Named(objects, "G1_Skeleton_appendicular", "Skeleton_appendicular");
        GameObject vertebral = Named(objects, "G2_VertebralColumn", "G2_VERTEBRAL COLUMN", "VERTEBRAL COLUMN");
        GameObject ribView = Named(objects, "G2_ribs", "G2_RIBCAGE");
        GameObject leftLowerLimbView = Named(objects, "G2_LeftLowerLimb", "G2_LEFT_LOWER_LIMB");
        GameObject rightPectoralView = Named(objects, "G2_RightPectoralGirdle");
        GameObject leftPectoralView = Named(objects, "G2_LeftPectoralGirdle");
        GameObject pelvicView = Named(objects, "G2_Pelvic Girdle", "G2_PELVIC_GIRDLE");
        GameObject rightLowerLimbView = Named(objects, "G2_RightLowerLimb");
        GameObject leftUpperLimbView = Named(objects, "G2_LeftUpperLimb");
        GameObject rightUpperLimbView = Named(objects, "G2_RightUpperLimb");
        InfoBoardController board = objects.Select(o => o.GetComponent<InfoBoardController>()).Single(c => c != null);
        GameObject axialDivision = overview.GetComponentsInChildren<DivisionSelection>(true)
            .Single(c => c.GetComponent<ViewTransitionOnSelect>() != null &&
                Reference<GameObject>(c.GetComponent<ViewTransitionOnSelect>(), "nextView") == axial).gameObject;
        GameObject appendicular = overview.GetComponentsInChildren<DivisionSelection>(true)
            .Single(c => c.gameObject != axialDivision).gameObject;
        Material highlight = AssetDatabase.LoadAssetAtPath<Material>("Assets/HighlightMst.mat");
        if (highlight == null) throw new InvalidOperationException("Highlight material was not imported.");
        var vertebralTransition = axial.GetComponentsInChildren<ViewTransitionOnSelect>(true)
            .Single(transition => Reference<GameObject>(transition, "nextView") == vertebral);
        Transform axialVertebralGroup = vertebralTransition.transform;

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
        MoveInspectionAnchors(boneAnchor, referenceAnchor);
        Set(display, "boneAnchor", boneAnchor);
        Set(display, "referenceAnchor", referenceAnchor);
        Set(display, "highlightMaterial", highlight);
        Set(navigation, "overviewRoot", overview);
        Set(navigation, "axialDivisionRoot", axialDivision);
        Set(navigation, "appendicularDivisionRoot", appendicular);
        Set(navigation, "axialView", axial);
        Set(navigation, "appendicularView", appendicularView);
        Set(navigation, "vertebralView", vertebral);
        Set(navigation, "ribView", ribView);
        Set(navigation, "leftLowerLimbView", leftLowerLimbView);
        Set(navigation, "rightPectoralView", rightPectoralView);
        Set(navigation, "leftPectoralView", leftPectoralView);
        Set(navigation, "pelvicView", pelvicView);
        Set(navigation, "rightLowerLimbView", rightLowerLimbView);
        Set(navigation, "leftUpperLimbView", leftUpperLimbView);
        Set(navigation, "rightUpperLimbView", rightUpperLimbView);
        Set(navigation, "groupHighlightMaterial", highlight);
        Set(navigation, "infoBoard", board);
        Set(navigation, "inspectionDisplay", display);
        Set(navigation, "inputReservation", reservation);
        SetArray(reservation, "actionManagers", objects.Select(o => o.GetComponent<InputActionManager>())
            .Where(c => c != null).Cast<UnityEngine.Object>().ToArray());
        foreach (var selection in overview.GetComponentsInChildren<DivisionSelection>(true)) Set(selection, "navigation", navigation);
        foreach (var transition in objects.Select(o => o.GetComponent<ViewTransitionOnSelect>()).Where(c => c != null))
        {
            Set(transition, "navigation", navigation);
            if (Reference<GameObject>(transition, "nextView") == vertebral)
                Set(transition, "interactableRoot", axialVertebralGroup);
        }

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
        ConfigureRibBones(navigation, ribView, highlight);
        ConfigureLeftLowerLimbBones(navigation, leftLowerLimbView, highlight);
        ConfigureGirdles(navigation, objects, highlight);
        ConfigureRemainingLimbs(navigation, objects, highlight);
        foreach (var highlighter in vertebral.GetComponentsInChildren<BoneGroupHoverHighlighter>(true))
            highlighter.enabled = false;
        // Empty collider lists make XRI collect descendants, including another interactable's
        // colliders. Assign each collider to its nearest interactable so targeting has one owner.
        foreach (GameObject root in new[] { overview, axial, appendicularView, vertebral, ribView,
                     leftLowerLimbView, rightPectoralView, leftPectoralView, pelvicView,
                     rightLowerLimbView, leftUpperLimbView, rightUpperLimbView })
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
        board.ShowNavigationInfo(board.DefaultTitle, board.DefaultDescription, "Back to skeleton", false);
        overview.SetActive(true);
        axialDivision.SetActive(true);
        appendicular.SetActive(true);
        axial.SetActive(false);
        appendicularView.SetActive(false);
        vertebral.SetActive(false);
        ribView.SetActive(false);
        leftLowerLimbView.SetActive(false);
        rightPectoralView.SetActive(false);
        leftPectoralView.SetActive(false);
        pelvicView.SetActive(false);
        rightLowerLimbView.SetActive(false);
        leftUpperLimbView.SetActive(false);
        rightUpperLimbView.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"G3 scene configured with {entries.Length} bones. Display center: {center}.");
    }

    private static void ConfigureRibBones(AnatomyNavigationController navigation, GameObject ribView,
        Material highlight)
    {
        var entries = RibBoneCatalog.All().ToArray();
        var meshes = ribView.GetComponentsInChildren<MeshFilter>(true);
        var selections = new BoneSelection[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var mesh = meshes.Single(m => m.name == entry.meshName);
            if (mesh.sharedMesh == null || mesh.GetComponent<MeshRenderer>() == null)
                throw new InvalidOperationException($"Missing rendered rib mesh: {entry.meshName}");
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
            var oldHighlighter = mesh.GetComponent<BoneGroupHoverHighlighter>();
            if (oldHighlighter != null) oldHighlighter.enabled = false;
            selections[i] = selection;
        }
        Set(navigation, "ribView", ribView);
        SetArray(navigation, "ribBones", selections);
    }

    private static void ConfigureLeftLowerLimbBones(AnatomyNavigationController navigation,
        GameObject view, Material highlight)
    {
        var entries = LeftLowerLimbBoneCatalog.All().ToArray();
        if (entries.Length != 30) throw new InvalidOperationException("Expected 30 left lower-limb entries.");
        var meshes = view.GetComponentsInChildren<MeshFilter>(true);
        var selections = new BoneSelection[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var mesh = meshes.Single(m => m.name == entry.meshName);
            if (mesh.sharedMesh == null || mesh.GetComponent<MeshRenderer>() == null)
                throw new InvalidOperationException($"Missing rendered left lower-limb mesh: {entry.meshName}");
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
            var oldHighlighter = mesh.GetComponent<BoneGroupHoverHighlighter>();
            if (oldHighlighter != null) oldHighlighter.enabled = false;
            selections[i] = selection;
        }
        Set(navigation, "leftLowerLimbView", view);
        SetArray(navigation, "leftLowerLimbBones", selections);
    }

    private static void ConfigureGirdles(AnatomyNavigationController navigation,
        GameObject[] objects, Material highlight)
    {
        GameObject right = Named(objects, "G2_RightPectoralGirdle");
        GameObject left = Named(objects, "G2_LeftPectoralGirdle");
        GameObject pelvic = Named(objects, "G2_Pelvic Girdle", "G2_PELVIC_GIRDLE");
        Set(navigation, "rightPectoralView", right);
        Set(navigation, "leftPectoralView", left);
        Set(navigation, "pelvicView", pelvic);
        SetArray(navigation, "rightPectoralBones",
            ConfigureGirdleBones(navigation, right, highlight, GirdleBoneCatalog.Pectoral("Right").ToArray()));
        SetArray(navigation, "leftPectoralBones",
            ConfigureGirdleBones(navigation, left, highlight, GirdleBoneCatalog.Pectoral("Left").ToArray()));
        SetArray(navigation, "pelvicBones",
            ConfigureGirdleBones(navigation, pelvic, highlight, GirdleBoneCatalog.Pelvic().ToArray()));
        right.SetActive(false);
        left.SetActive(false);
        pelvic.SetActive(false);
    }

    private static BoneSelection[] ConfigureGirdleBones(AnatomyNavigationController navigation,
        GameObject view, Material highlight, GirdleBoneCatalog.Entry[] entries)
    {
        var meshes = view.GetComponentsInChildren<MeshFilter>(true);
        var selections = new BoneSelection[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var mesh = meshes.Single(m => m.name == entry.meshName);
            if (mesh.sharedMesh == null || mesh.GetComponent<MeshRenderer>() == null)
                throw new InvalidOperationException($"Missing rendered girdle mesh: {view.name}/{entry.meshName}");
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
            var oldHighlighter = mesh.GetComponent<BoneGroupHoverHighlighter>();
            if (oldHighlighter != null) oldHighlighter.enabled = false;
            selections[i] = selection;
        }
        return selections;
    }

    private static void ConfigureRemainingLimbs(AnatomyNavigationController navigation,
        GameObject[] objects, Material highlight)
    {
        GameObject rightLower = Named(objects, "G2_RightLowerLimb");
        GameObject leftUpper = Named(objects, "G2_LeftUpperLimb");
        GameObject rightUpper = Named(objects, "G2_RightUpperLimb");
        Set(navigation, "rightLowerLimbView", rightLower);
        Set(navigation, "leftUpperLimbView", leftUpper);
        Set(navigation, "rightUpperLimbView", rightUpper);
        SetArray(navigation, "rightLowerLimbBones", ConfigureLimbBones(navigation, rightLower,
            highlight, AppendicularLimbBoneCatalog.RightLower().ToArray()));
        SetArray(navigation, "leftUpperLimbBones", ConfigureLimbBones(navigation, leftUpper,
            highlight, AppendicularLimbBoneCatalog.Upper("Left").ToArray()));
        SetArray(navigation, "rightUpperLimbBones", ConfigureLimbBones(navigation, rightUpper,
            highlight, AppendicularLimbBoneCatalog.Upper("Right").ToArray()));
        rightLower.SetActive(false);
        leftUpper.SetActive(false);
        rightUpper.SetActive(false);
    }

    private static BoneSelection[] ConfigureLimbBones(AnatomyNavigationController navigation,
        GameObject view, Material highlight, AppendicularLimbBoneCatalog.Entry[] entries)
    {
        var meshes = view.GetComponentsInChildren<MeshFilter>(true);
        var selections = new BoneSelection[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var mesh = meshes.Single(m => m.name == entry.meshName);
            if (mesh.sharedMesh == null || mesh.GetComponent<MeshRenderer>() == null)
                throw new InvalidOperationException($"Missing rendered limb mesh: {view.name}/{entry.meshName}");
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
            var oldHighlighter = mesh.GetComponent<BoneGroupHoverHighlighter>();
            if (oldHighlighter != null) oldHighlighter.enabled = false;
            selections[i] = selection;
        }
        return selections;
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
        Transform existing = owner.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(rect => rect.name == "Anatomy information panel");
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
        Transform station = EnsureAnatomyPanelStation(owner, panelRect);
        PanelMoveHandle.Configure(station, panelRect, font,
            new Vector2(0, -385), new Vector2(508, 58));
    }

    private static Transform EnsureAnatomyPanelStation(Transform owner, RectTransform panel)
    {
        var oldGrab = panel.GetComponent<XRGrabInteractable>();
        if (oldGrab != null) UnityEngine.Object.DestroyImmediate(oldGrab);
        var oldBody = panel.GetComponent<Rigidbody>();
        if (oldBody != null) UnityEngine.Object.DestroyImmediate(oldBody);

        Transform station = owner.Find("Anatomy panel station");
        if (station == null)
        {
            station = new GameObject("Anatomy panel station").transform;
            station.SetParent(owner, false);
        }
        panel.SetParent(station, true);
        return station;
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
