using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

public enum AnatomyTutorialStep { Division, Group, Bone, Turn, Tilt, Back, Complete }

/// <summary>Tracks one real G0-to-G3 inspection and the return path.</summary>
public sealed class AnatomyTutorialProgress
{
    public AnatomyTutorialStep Step { get; private set; }
    private int backProgress;

    public void Restart()
    {
        Step = AnatomyTutorialStep.Division;
        backProgress = 0;
    }

    public void Observe(AnatomyLevel level, float turning, float tilt)
    {
        if (Step == AnatomyTutorialStep.Complete) return;
        if (Step == AnatomyTutorialStep.Back)
        {
            if (level == AnatomyLevel.Bone) backProgress = 0;
            else if (level == AnatomyLevel.Group) backProgress = 1;
            else if (level == AnatomyLevel.Division)
            {
                if (backProgress == 1) backProgress = 2;
                else Step = AnatomyTutorialStep.Group;
            }
            else if (backProgress == 2) Step = AnatomyTutorialStep.Complete;
            else Step = AnatomyTutorialStep.Division;
            return;
        }

        // Backing out before the return step resumes at the action available here.
        if (level == AnatomyLevel.Whole) { Step = AnatomyTutorialStep.Division; return; }
        if (level == AnatomyLevel.Division && Step > AnatomyTutorialStep.Group)
            Step = AnatomyTutorialStep.Group;
        if (level == AnatomyLevel.Group && Step > AnatomyTutorialStep.Bone)
            Step = AnatomyTutorialStep.Bone;

        if (Step == AnatomyTutorialStep.Division && level >= AnatomyLevel.Division)
            Step = AnatomyTutorialStep.Group;
        else if (Step == AnatomyTutorialStep.Group && level >= AnatomyLevel.Group)
            Step = AnatomyTutorialStep.Bone;
        else if (Step == AnatomyTutorialStep.Bone && level == AnatomyLevel.Bone)
            Step = AnatomyTutorialStep.Turn;
        else if (Step == AnatomyTutorialStep.Turn && level == AnatomyLevel.Bone &&
                 Mathf.Abs(turning) >= 10f)
            Step = AnatomyTutorialStep.Tilt;
        else if (Step == AnatomyTutorialStep.Tilt && level == AnatomyLevel.Bone &&
                 Mathf.Abs(tilt) >= 8f)
        {
            Step = AnatomyTutorialStep.Back;
            backProgress = 0;
        }
    }
}

/// <summary>First-run guidance in a separate station with non-interactive world arrows.</summary>
public sealed class AnatomyTutorial : MonoBehaviour
{
    private const string SeenKey = "Osteon.TutorialSeen.v1";
    private static readonly string[] Prompts =
    {
        "Aim your controller ray at the skeleton and press Select. Choose Axial or Appendicular.",
        "Select a highlighted bone group to look closer.",
        "Point at a bone to see its name, then select it.",
        "Move the right stick sideways to turn the bone.",
        "Move the right stick up or down to tilt it.",
        "Press left X or use Back on the panel. Keep going until you reach the whole skeleton.",
        "You're ready to explore. A resets turn; B resets tilt. Use Spread in multi-bone groups."
    };
    private static readonly string[] DesktopPrompts =
    {
        "Click a division. WASD moves; F toggles mouse look. Tab or Esc frees the cursor.",
        "Click a highlighted bone group to look closer.",
        "Hover over a bone to see its name, then click it.",
        "Right drag sideways or use the left/right arrows to turn the bone.",
        "Right drag vertically or use the up/down arrows to tilt it.",
        "Press Backspace or click Back on the panel until you reach the whole skeleton.",
        "You're ready to explore. R resets turn; T resets tilt. Use Spread in multi-bone groups."
    };

    private readonly AnatomyTutorialProgress progress = new AnatomyTutorialProgress();
    private readonly List<Component> candidates = new List<Component>();
    private AnatomyNavigationController navigation;
    private RectTransform station;
    private RectTransform anatomyPanel;
    private Camera viewer;
    private GameObject card;
    private GameObject launcher;
    private TextMeshProUGUI heading;
    private TextMeshProUGUI prompt;
    private TextMeshProUGUI finishLabel;
    private Image accent;
    private Image[] progressMarkers;
    private GuideArrow[] guides;
    private GuideArrow motionGuide;
    private Material guideMaterial;
    private bool expanded;
    private float nextGuideUpdate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "CONTROLLERS MIGRATION" ||
            FindFirstObjectByType<AnatomyTutorial>() != null) return;
        new GameObject("Anatomy tutorial").AddComponent<AnatomyTutorial>();
    }

    private IEnumerator Start()
    {
        navigation = FindFirstObjectByType<AnatomyNavigationController>();
        var panelObject = GameObject.Find("Anatomy information panel");
        while (navigation == null || !navigation.CanNavigate || panelObject == null)
        {
            yield return null;
            if (navigation == null) navigation = FindFirstObjectByType<AnatomyNavigationController>();
            if (panelObject == null) panelObject = GameObject.Find("Anatomy information panel");
        }

        anatomyPanel = panelObject.GetComponent<RectTransform>();
        if (anatomyPanel == null) yield break;
        viewer = anatomyPanel.GetComponent<Canvas>()?.worldCamera;
        if (viewer == null) viewer = Camera.main;
        Build(anatomyPanel);
        progress.Restart();
        SetExpanded(PlayerPrefs.GetInt(SeenKey, 0) == 0);
        OsteonSettings.Instance?.ApplyTheme();
        OsteonSettings.Instance?.ApplyTextSize();
    }

    private void Update()
    {
        if (!expanded || navigation == null) return;
        AnatomyTutorialStep before = progress.Step;
        progress.Observe(navigation.Level, navigation.InspectionPose.Turning,
            navigation.InspectionPose.Tilt);
        if (progress.Step == before) return;
        if (progress.Step == AnatomyTutorialStep.Complete) SaveSeen();
        Render();
    }

    private void LateUpdate()
    {
        if (!expanded || guides == null) return;
        accent.color = new Color(0.14f, 0.87f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.time * 2.7f));
        if (Time.unscaledTime < nextGuideUpdate) return;
        nextGuideUpdate = Time.unscaledTime + 0.05f;
        UpdateGuides();
    }

    private void Replay()
    {
        navigation.ReturnToOverview();
        progress.Restart();
        SetExpanded(true);
    }

    private void Dismiss()
    {
        SaveSeen();
        SetExpanded(false);
    }

    private static void SaveSeen()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    private void SetExpanded(bool value)
    {
        expanded = value;
        card.SetActive(value);
        launcher.SetActive(!value);
        if (!value && guides != null)
        {
            foreach (var guide in guides) guide.Hide();
            motionGuide.Hide();
        }
        if (value) Render();
    }

    private void Render()
    {
        int index = (int)progress.Step;
        heading.text = progress.Step == AnatomyTutorialStep.Complete
            ? "Ready to explore" : $"Tutorial {index + 1}/6";
        prompt.text = Application.platform == RuntimePlatform.WebGLPlayer
            ? DesktopPrompts[index] : Prompts[index];
        finishLabel.text = progress.Step == AnatomyTutorialStep.Complete ? "Done" : "Skip";
        for (int i = 0; i < progressMarkers.Length; i++)
            progressMarkers[i].color = i <= index
                ? new Color(0.13f, 0.85f, 1f, 1f) : new Color(0.13f, 0.85f, 1f, 0.18f);
    }

    private void Build(RectTransform panel)
    {
        TMP_FontAsset font = panel.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        var stationObject = new GameObject("Tutorial station", typeof(RectTransform),
            typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
        stationObject.transform.SetParent(transform, false);
        station = (RectTransform)stationObject.transform;
        station.SetPositionAndRotation(panel.position - panel.right * 1.28f + panel.up * 0.08f,
            panel.rotation);
        station.localScale = Vector3.one * 0.001f;
        station.sizeDelta = new Vector2(520, 310);
        stationObject.layer = panel.gameObject.layer;
        var canvas = stationObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = viewer;

        var cardRect = MakeRect(station, "Tutorial panel", Vector2.zero, station.sizeDelta);
        card = cardRect.gameObject;
        var background = card.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.075f, 0.98f);
        accent = MakeDecor(cardRect, "Guide accent", new Vector2(0, 151),
            new Vector2(520, 8), new Color(0.14f, 0.87f, 1f));
        MakeDecor(cardRect, "Guide rail", new Vector2(-257, 0),
            new Vector2(6, 300), new Color(0.14f, 0.87f, 1f, 0.7f));
        MakeText(cardRect, "Guide identity", new Vector2(0, 113),
            new Vector2(470, 48), 28, font, TextAlignmentOptions.Center).text = "OSTEON  /  FIELD GUIDE";
        heading = MakeText(cardRect, "Tutorial heading", new Vector2(0, 68),
            new Vector2(470, 34), 20, font, TextAlignmentOptions.Center);
        prompt = MakeText(cardRect, "Tutorial prompt", new Vector2(0, -16),
            new Vector2(464, 126), 23, font, TextAlignmentOptions.Center);
        progressMarkers = new Image[6];
        for (int i = 0; i < progressMarkers.Length; i++)
            progressMarkers[i] = MakeDecor(cardRect, "Guide progress " + (i + 1),
                new Vector2(-180 + i * 55, -110), new Vector2(42, 8), Color.clear);
        var dismiss = MakeButton(cardRect, "Tutorial dismiss", new Vector2(197, -125),
            new Vector2(90, 44), font, Dismiss);
        finishLabel = dismiss.GetComponentInChildren<TextMeshProUGUI>();

        var launcherRect = MakeRect(station, "Tutorial launcher", Vector2.zero,
            new Vector2(250, 76));
        launcher = launcherRect.gameObject;
        var launcherImage = launcher.AddComponent<Image>();
        launcherImage.color = new Color(0.05f, 0.20f, 0.30f, 0.98f);
        var launcherButton = launcher.AddComponent<Button>();
        launcherButton.targetGraphic = launcherImage;
        launcherButton.onClick.AddListener(Replay);
        MakeDecor(launcherRect, "Guide tab accent", new Vector2(0, 35),
            new Vector2(250, 6), new Color(0.14f, 0.87f, 1f));
        MakeText(launcherRect, "Tutorial", Vector2.zero, new Vector2(230, 56),
            25, font, TextAlignmentOptions.Center);

        Shader shader = Resources.Load<Shader>("TutorialGlow");
        if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("Tutorial guide lines need a transparent shader.", this);
            return;
        }
        guideMaterial = new Material(shader) { name = "Tutorial guide glow" };
        guides = new[] { new GuideArrow(transform, guideMaterial, "Guide arrow 1"),
            new GuideArrow(transform, guideMaterial, "Guide arrow 2") };
        motionGuide = new GuideArrow(transform, guideMaterial, "Stick motion arrow");
    }

    private static Image MakeDecor(Transform parent, string name, Vector2 position,
        Vector2 size, Color color)
    {
        var rect = MakeRect(parent, name, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void UpdateGuides()
    {
        foreach (var guide in guides) guide.Hide();
        motionGuide.Hide();
        if (progress.Step == AnatomyTutorialStep.Complete) return;

        if (progress.Step == AnatomyTutorialStep.Turn ||
            progress.Step == AnatomyTutorialStep.Tilt)
        {
            Transform bone = navigation.InspectionDisplay?.BoneVisual;
            if (bone == null || !TryCenter(bone, out Vector3 center)) return;
            ShowGuide(0, center, true);
            Vector3 axis = progress.Step == AnatomyTutorialStep.Turn ? station.right : station.up;
            Vector3 front = TowardViewer(center) * 0.08f;
            motionGuide.Show(center - axis * 0.16f + front,
                center + axis * 0.16f + front, viewer, 0f, 1f);
            return;
        }
        if (progress.Step == AnatomyTutorialStep.Back)
        {
            Transform back = anatomyPanel.Find("Back Button");
            if (back != null) ShowGuide(0, back.position, true);
            return;
        }

        candidates.Clear();
        if (navigation.CurrentView == null) return;
        if (progress.Step == AnatomyTutorialStep.Division)
        {
            foreach (var division in navigation.CurrentView.GetComponentsInChildren<DivisionSelection>())
                if (division.isActiveAndEnabled) candidates.Add(division);
        }
        else if (progress.Step == AnatomyTutorialStep.Group)
        {
            foreach (var group in navigation.CurrentView.GetComponentsInChildren<ViewTransitionOnSelect>())
                if (group.isActiveAndEnabled && group.NextView != null &&
                    group.GetComponent<DivisionSelection>() == null) candidates.Add(group);
        }
        else if (progress.Step == AnatomyTutorialStep.Bone)
        {
            foreach (var bone in navigation.CurrentView.GetComponentsInChildren<BoneSelection>())
                if (bone.isActiveAndEnabled) candidates.Add(bone);
        }
        if (candidates.Count == 0) return;

        Component hovered = null;
        foreach (var candidate in candidates)
            if (IsHovered(candidate)) { hovered = candidate; break; }
        if (hovered != null)
        {
            if (TryTargetCenter(hovered, out Vector3 center)) ShowGuide(0, center, true);
            if (progress.Step != AnatomyTutorialStep.Division) return;
        }
        if (progress.Step == AnatomyTutorialStep.Bone)
        {
            if (hovered != null) return;
            BoneSelection largest = null;
            float size = -1f;
            foreach (BoneSelection bone in candidates)
            {
                float candidateSize = 0f;
                foreach (Renderer renderer in bone.Renderers)
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        candidateSize += renderer.bounds.size.sqrMagnitude;
                if (candidateSize <= size) continue;
                largest = bone;
                size = candidateSize;
            }
            if (largest != null && TryTargetCenter(largest, out Vector3 boneCenter))
                ShowGuide(0, boneCenter, true);
            return;
        }

        int shown = 0;
        foreach (var candidate in candidates)
        {
            if (candidate == hovered || !TryTargetCenter(candidate, out Vector3 center)) continue;
            if (hovered != null && shown == 0) shown = 1;
            if (shown >= guides.Length) break;
            ShowGuide(shown++, center, hovered == null);
        }
    }

    private void ShowGuide(int index, Vector3 center, bool bright)
    {
        Vector3 start = station.position + station.right * 0.29f - station.forward * 0.02f +
            station.up * (index == 0 ? 0.04f : -0.04f);
        guides[index].Show(start, center + TowardViewer(center) * 0.045f, viewer,
            0.07f, (bright ? 1f : 0.55f) * (0.82f + 0.18f * Mathf.Sin(Time.time * 2.7f)));
    }

    private Vector3 TowardViewer(Vector3 point)
    {
        Vector3 direction = viewer != null ? viewer.transform.position - point : -station.forward;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : -station.forward;
    }

    private static bool IsHovered(Component target)
    {
        if (target is BoneSelection bone) return bone.IsHovered;
        foreach (var interactable in target.GetComponentsInChildren<XRBaseInteractable>())
            if (interactable.isHovered) return true;
        return false;
    }

    private static bool TryTargetCenter(Component target, out Vector3 center)
    {
        if (target is BoneSelection) return TryCenter(target.transform, out center);
        Collider best = null;
        float bestSize = -1f;
        foreach (var interactable in target.GetComponentsInChildren<XRBaseInteractable>())
        {
            if (!interactable.isActiveAndEnabled) continue;
            foreach (var collider in interactable.colliders)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    continue;
                float size = collider.bounds.size.sqrMagnitude;
                if (interactable.isHovered) { center = collider.bounds.center; return true; }
                if (size <= bestSize) continue;
                best = collider;
                bestSize = size;
            }
        }
        if (best != null) { center = best.bounds.center; return true; }
        return TryCenter(target.transform, out center);
    }

    private static bool TryCenter(Transform root, out Vector3 center)
    {
        Bounds bounds = default;
        bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found)
            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
        center = found ? bounds.center : root.position;
        return root.gameObject.activeInHierarchy;
    }

    private void OnDestroy()
    {
        if (guideMaterial != null) Destroy(guideMaterial);
    }

    private sealed class GuideArrow
    {
        private readonly GameObject root;
        private readonly LineRenderer halo;
        private readonly LineRenderer trail;
        private readonly LineRenderer headHalo;
        private readonly LineRenderer head;

        public GuideArrow(Transform owner, Material material, string name)
        {
            root = new GameObject(name);
            root.transform.SetParent(owner, false);
            halo = MakeLine(root.transform, "Soft glow", material, 0.036f, 3);
            trail = MakeLine(root.transform, "Bright line", material, 0.009f, 3);
            headHalo = MakeLine(root.transform, "Arrow glow", material, 0.042f, 3);
            head = MakeLine(root.transform, "Arrow tip", material, 0.011f, 3);
            Hide();
        }

        public void Hide() => root.SetActive(false);

        public void Show(Vector3 from, Vector3 to, Camera camera, float bend, float strength)
        {
            if ((to - from).sqrMagnitude < 0.0064f) { Hide(); return; }
            root.SetActive(true);
            Vector3 mid = Vector3.Lerp(from, to, 0.58f) + Vector3.up * bend;
            Vector3 direction = (to - mid).normalized;
            Vector3 cameraForward = camera != null ? camera.transform.forward : Vector3.forward;
            Vector3 side = Vector3.Cross(direction, cameraForward).normalized;
            if (side.sqrMagnitude < 0.01f) side = camera != null ? camera.transform.right : Vector3.right;
            Vector3 left = to - direction * 0.065f + side * 0.034f;
            Vector3 right = to - direction * 0.065f - side * 0.034f;
            SetPoints(halo, from, mid, to);
            SetPoints(trail, from, mid, to);
            SetPoints(headHalo, left, to, right);
            SetPoints(head, left, to, right);
            Color color = new Color(0.13f, 0.88f, 1f, 0.9f * strength);
            Color glow = new Color(0.13f, 0.88f, 1f, 0.2f * strength);
            SetColor(trail, color);
            SetColor(head, color);
            SetColor(halo, glow);
            SetColor(headHalo, glow);
        }

        private static LineRenderer MakeLine(Transform parent, string name, Material material,
            float width, int points)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void SetColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        private static void SetPoints(LineRenderer line, Vector3 first, Vector3 middle, Vector3 last)
        {
            line.SetPosition(0, first);
            line.SetPosition(1, middle);
            line.SetPosition(2, last);
        }
    }

    private static RectTransform MakeRect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 position,
        Vector2 size, float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        var rect = MakeRect(parent, name, position, size);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = fontSize;
        label.color = new Color(0.94f, 0.97f, 1f);
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Button MakeButton(Transform parent, string name, Vector2 position,
        Vector2 size, TMP_FontAsset font, UnityEngine.Events.UnityAction click)
    {
        var rect = MakeRect(parent, name, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.09f, 0.27f, 0.38f, 1f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(click);
        MakeText(rect, "Tutorial action", Vector2.zero, size - new Vector2(8, 6),
            22, font, TextAlignmentOptions.Center);
        return button;
    }
}
