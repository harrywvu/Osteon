using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Local appearance preferences and the ray-selectable wall settings station.</summary>
public sealed class OsteonSettings : MonoBehaviour
{
    private const string RoomKey = "Osteon.RoomColor.v1";
    private const string ThemeKey = "Osteon.PanelTheme.v1";
    private const string TextKey = "Osteon.TextSize.v1";
    private const string HighlightKey = "Osteon.HighlightColor.v1";
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly string[] RoomNames = { "Original", "Cool blue", "Sage", "Warm" };
    private static readonly Color[] RoomColors =
    {
        Color.white,
        new Color(0.47f, 0.69f, 0.94f),
        new Color(0.55f, 0.77f, 0.62f),
        new Color(0.93f, 0.71f, 0.53f)
    };
    private static readonly string[] ThemeNames = { "Default dark", "Light", "High contrast" };
    private static readonly string[] TextNames = { "Small", "Standard", "Large" };
    private static readonly float[] TextScales = { 0.9f, 1f, 1.2f };
    private static readonly string[] HighlightNames = { "Original blue", "Amber", "Violet", "Lime" };
    private static readonly Color[] HighlightColors =
    {
        new Color(0f, 0.23814607f, 1f),
        new Color(1f, 0.45f, 0.02f),
        new Color(0.55f, 0.15f, 1f),
        new Color(0.35f, 0.9f, 0.05f)
    };

    private struct Palette
    {
        public Color background, inset, button, selected, text, buttonText, track, fill, handle;
    }

    private static readonly Palette[] Palettes =
    {
        new Palette { background = new Color(0.035f, 0.055f, 0.075f, 0.98f),
            inset = new Color(0.055f, 0.11f, 0.15f), button = new Color(0.09f, 0.27f, 0.38f),
            selected = new Color(0.18f, 0.58f, 0.72f), text = new Color(0.94f, 0.97f, 1f),
            buttonText = Color.white, track = new Color(0.16f, 0.30f, 0.38f),
            fill = new Color(0.18f, 0.68f, 0.87f), handle = new Color(0.75f, 0.94f, 1f) },
        new Palette { background = new Color(0.92f, 0.95f, 0.97f),
            inset = new Color(0.78f, 0.85f, 0.89f), button = new Color(0.12f, 0.31f, 0.45f),
            selected = new Color(0.12f, 0.49f, 0.39f), text = new Color(0.08f, 0.14f, 0.20f),
            buttonText = Color.white, track = new Color(0.45f, 0.57f, 0.64f),
            fill = new Color(0.10f, 0.46f, 0.64f), handle = Color.white },
        new Palette { background = Color.black, inset = new Color(0.12f, 0.12f, 0.12f),
            button = new Color(0.18f, 0.18f, 0.18f), selected = new Color(0.24f, 0.39f, 0.02f),
            text = Color.white, buttonText = Color.white, track = new Color(0.48f, 0.48f, 0.48f),
            fill = new Color(0.95f, 0.85f, 0.12f), handle = Color.white }
    };

    private readonly Dictionary<Renderer, Material[]> originalRendererMaterials = new Dictionary<Renderer, Material[]>();
    private readonly Dictionary<Material, Material> roomCopies = new Dictionary<Material, Material>();
    private readonly Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
    private readonly List<Image> roomButtons = new List<Image>();
    private readonly List<Image> themeButtons = new List<Image>();
    private readonly List<Image> textButtons = new List<Image>();
    private readonly List<Image> highlightButtons = new List<Image>();
    private readonly Dictionary<TextMeshProUGUI, float> baseFontSizes = new Dictionary<TextMeshProUGUI, float>();
    private readonly Dictionary<TextMeshProUGUI, bool> baseAutoSizing = new Dictionary<TextMeshProUGUI, bool>();
    private GameObject page;
    private GameObject launcher;
    private Material highlightCopy;
    private Color originalHighlightBase;
    private Color originalHighlightEmission;
    private int roomChoice;
    private int themeChoice;
    private int textChoice;
    private int highlightChoice;

    public static OsteonSettings Instance { get; private set; }
    public int RoomChoice => roomChoice;
    public int ThemeChoice => themeChoice;
    public int TextChoice => textChoice;
    public int HighlightChoice => highlightChoice;
    public Material HighlightMaterial => highlightCopy;
    public bool IsOpen => page != null && page.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "CONTROLLERS MIGRATION" ||
            FindFirstObjectByType<OsteonSettings>() != null) return;
        new GameObject("Osteon settings").AddComponent<OsteonSettings>();
    }

    private void Awake()
    {
        Instance = this;
        roomChoice = SafeChoice(PlayerPrefs.GetInt(RoomKey, 0), RoomNames.Length);
        themeChoice = SafeChoice(PlayerPrefs.GetInt(ThemeKey, 0), ThemeNames.Length);
        textChoice = SafeChoice(PlayerPrefs.GetInt(TextKey, 1), TextNames.Length, 1);
        highlightChoice = SafeChoice(PlayerPrefs.GetInt(HighlightKey, 0), HighlightNames.Length);
    }

    private IEnumerator Start()
    {
        var room = GameObject.Find("hospital room");
        var panel = GameObject.Find("Anatomy information panel");
        if (room == null || panel == null)
        {
            Debug.LogError("Osteon settings need the hospital room and anatomy information panel.");
            yield break;
        }

        CaptureRoom(room.transform);
        BindHighlightMaterial();
        ApplyHighlightColor();
        Camera viewer = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        while (viewer == null)
        {
            yield return null;
            viewer = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }
        BuildPage((RectTransform)panel.transform, viewer);
        var description = panel.transform.Find("Anatomy description")?.GetComponent<TextMeshProUGUI>();
        if (description != null) MakeScrollable(description);
        ApplyRoom();
        ApplyTheme();
        ApplyTextSize();
    }

    private static int SafeChoice(int choice, int count, int fallback = 0) =>
        choice >= 0 && choice < count ? choice : fallback;

    public void SetRoomChoice(int choice)
    {
        roomChoice = SafeChoice(choice, RoomNames.Length);
        PlayerPrefs.SetInt(RoomKey, roomChoice);
        PlayerPrefs.Save();
        ApplyRoom();
        UpdateRoomButtons();
    }

    public void Open()
    {
        if (page == null || launcher == null) return;
        launcher.SetActive(false);
        page.SetActive(true);
    }

    public void Close()
    {
        if (page == null || launcher == null) return;
        page.SetActive(false);
        launcher.SetActive(true);
    }

    public void SetThemeChoice(int choice)
    {
        themeChoice = SafeChoice(choice, ThemeNames.Length);
        PlayerPrefs.SetInt(ThemeKey, themeChoice);
        PlayerPrefs.Save();
        ApplyTheme();
    }

    public void SetTextChoice(int choice)
    {
        textChoice = SafeChoice(choice, TextNames.Length, 1);
        PlayerPrefs.SetInt(TextKey, textChoice);
        PlayerPrefs.Save();
        ApplyTextSize();
    }

    public void SetHighlightChoice(int choice)
    {
        highlightChoice = SafeChoice(choice, HighlightNames.Length);
        PlayerPrefs.SetInt(HighlightKey, highlightChoice);
        PlayerPrefs.Save();
        ApplyHighlightColor();
        UpdateHighlightButtons();
    }

    public void ResetToDefaults()
    {
        SetRoomChoice(0);
        SetThemeChoice(0);
        SetTextChoice(1);
        SetHighlightChoice(0);
    }

    private void BindHighlightMaterial()
    {
        var navigation = FindFirstObjectByType<AnatomyNavigationController>();
        Material source = navigation != null ? navigation.GroupHighlightMaterial : null;
        if (source == null)
        {
            Debug.LogError("Osteon settings could not find the anatomy highlight material.");
            return;
        }
        highlightCopy = new Material(source) { name = source.name + " (Osteon highlight color)" };
        originalHighlightBase = source.HasProperty(BaseColor) ? source.GetColor(BaseColor) : Color.white;
        originalHighlightEmission = source.HasProperty(EmissionColor)
            ? source.GetColor(EmissionColor) : HighlightColors[0];
        foreach (var highlighter in FindObjectsByType<BoneGroupHoverHighlighter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)) highlighter.Configure(highlightCopy);
        foreach (var transition in FindObjectsByType<ViewTransitionOnSelect>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)) transition.ConfigureHighlight(highlightCopy);
        foreach (var selection in FindObjectsByType<BoneSelection>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)) selection.ConfigureHighlight(highlightCopy);
        if (navigation.InspectionDisplay != null)
            navigation.InspectionDisplay.ConfigureHighlight(highlightCopy);
    }

    private void ApplyHighlightColor()
    {
        if (highlightCopy == null) return;
        Color emission = highlightChoice == 0
            ? originalHighlightEmission : HighlightColors[highlightChoice];
        Color baseTint = highlightChoice == 0
            ? originalHighlightBase : Color.Lerp(Color.white, emission, 0.35f);
        if (highlightCopy.HasProperty(BaseColor)) highlightCopy.SetColor(BaseColor, baseTint);
        if (highlightCopy.HasProperty(EmissionColor)) highlightCopy.SetColor(EmissionColor, emission);
    }

    private void CaptureRoom(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var originals = renderer.sharedMaterials;
            var replacements = (Material[])originals.Clone();
            bool changed = false;
            for (int i = 0; i < originals.Length; i++)
            {
                var source = originals[i];
                if (source == null || !source.HasProperty(BaseColor) || source.renderQueue > 2500) continue;
                if (!roomCopies.TryGetValue(source, out var copy))
                {
                    copy = new Material(source) { name = source.name + " (Osteon room color)" };
                    roomCopies.Add(source, copy);
                    originalColors.Add(source, source.GetColor(BaseColor));
                }
                replacements[i] = copy;
                changed = true;
            }
            if (!changed) continue;
            originalRendererMaterials.Add(renderer, originals);
            renderer.sharedMaterials = replacements;
        }
    }

    private void ApplyRoom()
    {
        foreach (var pair in roomCopies)
        {
            Color original = originalColors[pair.Key];
            if (roomChoice == 0)
            {
                pair.Value.SetColor(BaseColor, original);
                continue;
            }
            float brightness = original.grayscale;
            Color target = RoomColors[roomChoice] * Mathf.Lerp(0.4f, 1f, brightness);
            Color color = Color.Lerp(original, target, 0.7f);
            color.a = original.a;
            pair.Value.SetColor(BaseColor, color);
        }
    }

    private void BuildPage(RectTransform panel, Camera viewer)
    {
        var font = panel.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        var existing = panel.Find("Settings button");
        if (existing != null) Destroy(existing.gameObject);
        var back = panel.Find("Back Button") as RectTransform;
        if (back != null)
        {
            back.anchoredPosition = new Vector2(0, -352);
            back.sizeDelta = new Vector2(508, 56);
            var backLabel = back.Find("Anatomy back label") as RectTransform;
            if (backLabel != null) backLabel.sizeDelta = new Vector2(484, 52);
        }

        Transform station = new GameObject("Settings station").transform;
        station.SetPositionAndRotation(new Vector3(4.54f, 1.4f, 1.9f), Quaternion.Euler(0f, 90f, 0f));
        var launcherCanvas = MakeCanvas(station, "Settings launcher", viewer);
        launcher = launcherCanvas.gameObject;
        var launcherRect = (RectTransform)launcher.transform;
        launcherRect.sizeDelta = new Vector2(360, 100);
        launcherRect.localScale = Vector3.one * 0.001f;
        var launcherImage = launcher.GetComponent<Image>();
        launcherImage.color = new Color(0.09f, 0.27f, 0.38f, 1f);
        var openButton = launcher.AddComponent<Button>();
        openButton.targetGraphic = launcherImage;
        openButton.onClick.AddListener(Open);
        MakeText(launcherRect, "Settings", Vector2.zero, new Vector2(350, 90),
            30, font, TextAlignmentOptions.Center);

        page = MakeCanvas(station, "Settings panel", viewer).gameObject;
        var pageRect = (RectTransform)page.transform;
        pageRect.sizeDelta = new Vector2(560, 980);
        pageRect.localScale = Vector3.one * 0.001f;
        var background = page.GetComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.075f, 1f);
        background.raycastTarget = true;
        MakeText(pageRect, "Appearance settings", new Vector2(-58, 420), new Vector2(360, 65),
            30, font, TextAlignmentOptions.Left);
        MakeButton(pageRect, "Close settings", "Close", new Vector2(205, 420),
            new Vector2(110, 58), font, Close);
        MakeText(pageRect, "Hospital color", new Vector2(0, 310), new Vector2(460, 48),
            26, font, TextAlignmentOptions.Left);
        for (int i = 0; i < RoomNames.Length; i++)
        {
            int choice = i;
            var button = MakeButton(pageRect, "Room " + RoomNames[i], RoomNames[i],
                new Vector2(-187 + 125 * i, 238), new Vector2(115, 74), font,
                () => SetRoomChoice(choice));
            roomButtons.Add(button.GetComponent<Image>());
        }
        MakeText(pageRect, "Changes apply to the hospital room surfaces.",
            new Vector2(0, 150), new Vector2(470, 60), 22, font, TextAlignmentOptions.Center);
        MakeText(pageRect, "Panel theme", new Vector2(0, 80), new Vector2(460, 45),
            26, font, TextAlignmentOptions.Left);
        for (int i = 0; i < ThemeNames.Length; i++)
        {
            int choice = i;
            var button = MakeButton(pageRect, "Theme " + ThemeNames[i], ThemeNames[i],
                new Vector2(-160 + 160 * i, 15), new Vector2(150, 66), font,
                () => SetThemeChoice(choice));
            themeButtons.Add(button.GetComponent<Image>());
        }
        MakeText(pageRect, "Text size", new Vector2(0, -95), new Vector2(460, 45),
            26, font, TextAlignmentOptions.Left);
        for (int i = 0; i < TextNames.Length; i++)
        {
            int choice = i;
            var button = MakeButton(pageRect, "Text " + TextNames[i], TextNames[i],
                new Vector2(-160 + 160 * i, -165), new Vector2(150, 66), font,
                () => SetTextChoice(choice));
            textButtons.Add(button.GetComponent<Image>());
        }
        MakeText(pageRect, "Highlight color", new Vector2(0, -260), new Vector2(460, 45),
            26, font, TextAlignmentOptions.Left);
        for (int i = 0; i < HighlightNames.Length; i++)
        {
            int choice = i;
            var button = MakeButton(pageRect, "Highlight " + HighlightNames[i], HighlightNames[i],
                new Vector2(-187 + 125 * i, -330), new Vector2(115, 74), font,
                () => SetHighlightChoice(choice));
            highlightButtons.Add(button.GetComponent<Image>());
        }
        MakeButton(pageRect, "Reset settings", "Reset to defaults", new Vector2(0, -420),
            new Vector2(300, 66), font, ResetToDefaults);
        UpdateRoomButtons();
        UpdateThemeButtons();
        UpdateTextButtons();
        UpdateHighlightButtons();
        PanelMoveHandle.Configure(station, pageRect, font,
            new Vector2(0, -590), new Vector2(508, 58));
        PanelNarration.Attach(pageRect, NarrationContent, font);
        page.SetActive(false);
    }

    private IEnumerable<string> NarrationContent()
    {
        yield return "Appearance settings";
        yield return "Hospital color";
        yield return RoomNames[roomChoice];
        yield return "Panel theme";
        yield return ThemeNames[themeChoice];
        yield return "Text size";
        yield return TextNames[textChoice];
        yield return "Highlight color";
        yield return HighlightNames[highlightChoice];
    }

    private static Canvas MakeCanvas(Transform station, string name, Camera viewer)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas),
            typeof(TrackedDeviceGraphicRaycaster), typeof(Image));
        int layer = LayerMask.NameToLayer("UI");
        if (layer >= 0) obj.layer = layer;
        obj.transform.SetParent(station, false);
        var canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = viewer;
        return canvas;
    }

    private void UpdateRoomButtons()
    {
        Palette palette = Palettes[themeChoice];
        for (int i = 0; i < roomButtons.Count; i++)
            roomButtons[i].color = i == roomChoice ? palette.selected : palette.button;
    }

    private void UpdateThemeButtons()
    {
        Palette palette = Palettes[themeChoice];
        for (int i = 0; i < themeButtons.Count; i++)
            themeButtons[i].color = i == themeChoice ? palette.selected : palette.button;
    }

    private void UpdateTextButtons()
    {
        Palette palette = Palettes[themeChoice];
        for (int i = 0; i < textButtons.Count; i++)
            textButtons[i].color = i == textChoice ? palette.selected : palette.button;
    }

    private void UpdateHighlightButtons()
    {
        Palette palette = Palettes[themeChoice];
        for (int i = 0; i < highlightButtons.Count; i++)
            highlightButtons[i].color = i == highlightChoice ? palette.selected : palette.button;
    }

    public void ApplyTheme()
    {
        var anatomy = GameObject.Find("Anatomy information panel");
        if (anatomy != null) ApplyThemeTo(anatomy.transform);
        var tutorial = GameObject.Find("Tutorial station");
        if (tutorial != null) ApplyThemeTo(tutorial.transform);
        var quiz = GameObject.Find("Quiz station");
        if (quiz != null) ApplyThemeTo(quiz.transform);
        var settings = GameObject.Find("Settings station");
        if (settings != null) ApplyThemeTo(settings.transform);
        UpdateRoomButtons();
        UpdateThemeButtons();
        UpdateTextButtons();
        UpdateHighlightButtons();
    }

    public void PrepareQuizText(params TextMeshProUGUI[] regions)
    {
        foreach (var region in regions)
            if (region != null) MakeScrollable(region);
        ApplyTextSize();
    }

    public void ReleaseDynamicText(Transform root)
    {
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            baseFontSizes.Remove(label);
            baseAutoSizing.Remove(label);
        }
    }

    public static void ScrollToTop(TextMeshProUGUI label)
    {
        if (label == null) return;
        var scroll = label.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    public void ApplyTextSize()
    {
        ApplyTextSizeTo(GameObject.Find("Anatomy information panel"));
        ApplyTextSizeTo(GameObject.Find("Tutorial station"));
        ApplyTextSizeTo(GameObject.Find("Quiz station"));
        ApplyTextSizeTo(GameObject.Find("Settings station"));
        UpdateTextButtons();
    }

    private void ApplyTextSizeTo(GameObject root)
    {
        if (root == null) return;
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (!baseFontSizes.TryGetValue(label, out float baseline))
            {
                baseline = label.fontSize;
                baseFontSizes.Add(label, baseline);
                baseAutoSizing.Add(label, label.enableAutoSizing);
            }
            label.fontSize = baseline * TextScales[textChoice];
            // A selected size must remain the displayed size; long reading areas scroll.
            label.enableAutoSizing = textChoice == 1 && baseAutoSizing[label];
        }
    }

    private static void MakeScrollable(TextMeshProUGUI label)
    {
        if (label.GetComponentInParent<ScrollRect>() != null) return;
        var original = label.rectTransform;
        Transform parent = original.parent;
        int sibling = original.GetSiblingIndex();
        var viewport = MakeRect(parent, label.name + " scroll viewport",
            original.anchoredPosition, original.sizeDelta);
        viewport.SetSiblingIndex(sibling);
        var image = viewport.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = viewport;
        original.SetParent(viewport, false);
        original.anchorMin = new Vector2(0f, 1f);
        original.anchorMax = Vector2.one;
        original.pivot = new Vector2(0.5f, 1f);
        original.anchoredPosition = Vector2.zero;
        original.sizeDelta = new Vector2(0f, original.sizeDelta.y);
        var fitter = label.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = original;
        scroll.verticalNormalizedPosition = 1f;
    }

    private void ApplyThemeTo(Transform root)
    {
        Palette palette = Palettes[themeChoice];
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            var button = image.GetComponent<Button>();
            if (button != null)
            {
                image.color = palette.button;
                var states = button.colors;
                states.normalColor = Color.white;
                states.highlightedColor = new Color(0.8f, 0.95f, 1f);
                states.pressedColor = new Color(0.62f, 0.75f, 0.8f);
                states.disabledColor = new Color(0.43f, 0.43f, 0.43f);
                button.colors = states;
            }
            else if (image.name == "Anatomy information panel" || image.name == "Tutorial panel" ||
                image.name == "Settings page" ||
                image.name == "Quiz panel" || image.name == "Quiz launcher" || image.name == "Account panel" ||
                image.name == "Settings panel" || image.name == "Settings launcher")
                image.color = palette.background;
            else if (image.name == "Group spread control") image.color = palette.inset;
            else if (image.name == "Move handle") image.color = palette.button;
            else if (image.name == "Track") image.color = palette.track;
            else if (image.name == "Fill") image.color = palette.fill;
            else if (image.name == "Handle") image.color = palette.handle;
            else if (image.name == "Spread slider")
                image.color = new Color(palette.inset.r, palette.inset.g, palette.inset.b, 0.35f);
        }
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            label.color = label.GetComponentInParent<Button>() != null
                ? palette.buttonText : palette.text;
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

    private static TextMeshProUGUI MakeText(Transform parent, string value, Vector2 position,
        Vector2 size, float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        var rect = MakeRect(parent, value, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = new Color(0.94f, 0.97f, 1f);
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Button MakeButton(Transform parent, string name, string label,
        Vector2 position, Vector2 size, TMP_FontAsset font, UnityEngine.Events.UnityAction click)
    {
        var rect = MakeRect(parent, name, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.09f, 0.27f, 0.38f, 1f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(click);
        MakeText(rect, label, Vector2.zero, size - new Vector2(10, 8), 21, font,
            TextAlignmentOptions.Center);
        return button;
    }

    private void OnDestroy()
    {
        foreach (var pair in originalRendererMaterials)
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        foreach (var copy in roomCopies.Values)
            if (copy != null) Destroy(copy);
        if (highlightCopy != null) Destroy(highlightCopy);
        if (Instance == this) Instance = null;
    }
}
