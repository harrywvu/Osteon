using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Local appearance preferences and the ray-selectable anatomy settings page.</summary>
public sealed class OsteonSettings : MonoBehaviour
{
    private const string RoomKey = "Osteon.RoomColor.v1";
    private const string ThemeKey = "Osteon.PanelTheme.v1";
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly string[] RoomNames = { "Original", "Cool blue", "Sage", "Warm" };
    private static readonly Color[] RoomColors =
    {
        Color.white,
        new Color(0.47f, 0.69f, 0.94f),
        new Color(0.55f, 0.77f, 0.62f),
        new Color(0.93f, 0.71f, 0.53f)
    };
    private static readonly string[] ThemeNames = { "Default dark", "Light", "High contrast" };

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
    private GameObject page;
    private int roomChoice;
    private int themeChoice;

    public static OsteonSettings Instance { get; private set; }
    public int RoomChoice => roomChoice;
    public int ThemeChoice => themeChoice;
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
    }

    private void Start()
    {
        var room = GameObject.Find("hospital room");
        var panel = GameObject.Find("Anatomy information panel");
        if (room == null || panel == null)
        {
            Debug.LogError("Osteon settings need the hospital room and anatomy information panel.");
            return;
        }

        CaptureRoom(room.transform);
        BuildPage((RectTransform)panel.transform);
        ApplyRoom();
        ApplyTheme();
    }

    private static int SafeChoice(int choice, int count) => choice >= 0 && choice < count ? choice : 0;

    public void SetRoomChoice(int choice)
    {
        roomChoice = SafeChoice(choice, RoomNames.Length);
        PlayerPrefs.SetInt(RoomKey, roomChoice);
        PlayerPrefs.Save();
        ApplyRoom();
        UpdateRoomButtons();
    }

    public void Open() { if (page != null) page.SetActive(true); }
    public void Close() { if (page != null) page.SetActive(false); }

    public void SetThemeChoice(int choice)
    {
        themeChoice = SafeChoice(choice, ThemeNames.Length);
        PlayerPrefs.SetInt(ThemeKey, themeChoice);
        PlayerPrefs.Save();
        ApplyTheme();
    }

    public void ResetToDefaults()
    {
        SetRoomChoice(0);
        SetThemeChoice(0);
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

    private void BuildPage(RectTransform panel)
    {
        var font = panel.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        var existing = panel.Find("Settings button");
        if (existing != null) Destroy(existing.gameObject);
        var settings = MakeButton(panel, "Settings button", "Settings", new Vector2(201, 336),
            new Vector2(110, 58), font, Open);
        settings.GetComponent<Image>().color = new Color(0.10f, 0.30f, 0.42f, 1f);

        var title = panel.Find("Anatomy title") as RectTransform;
        if (title != null)
        {
            title.anchoredPosition = new Vector2(-74, 336);
            title.sizeDelta = new Vector2(354, 70);
            var text = title.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = 24;
                text.fontSizeMax = 32;
            }
        }

        page = MakeRect(panel, "Settings page", Vector2.zero, panel.sizeDelta).gameObject;
        var background = page.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.075f, 1f);
        background.raycastTarget = true;
        var pageRect = (RectTransform)page.transform;
        MakeText(pageRect, "Appearance settings", new Vector2(-58, 330), new Vector2(360, 65),
            30, font, TextAlignmentOptions.Left);
        MakeButton(pageRect, "Close settings", "Close", new Vector2(205, 330),
            new Vector2(110, 58), font, Close);
        MakeText(pageRect, "Hospital color", new Vector2(0, 220), new Vector2(460, 48),
            26, font, TextAlignmentOptions.Left);
        for (int i = 0; i < RoomNames.Length; i++)
        {
            int choice = i;
            var button = MakeButton(pageRect, "Room " + RoomNames[i], RoomNames[i],
                new Vector2(-187 + 125 * i, 145), new Vector2(115, 74), font,
                () => SetRoomChoice(choice));
            roomButtons.Add(button.GetComponent<Image>());
        }
        MakeText(pageRect, "Changes apply to the hospital room surfaces.",
            new Vector2(0, 55), new Vector2(470, 80), 22, font, TextAlignmentOptions.Center);
        MakeText(pageRect, "Panel theme", new Vector2(0, -22), new Vector2(460, 45),
            26, font, TextAlignmentOptions.Left);
        for (int i = 0; i < ThemeNames.Length; i++)
        {
            int choice = i;
            var button = MakeButton(pageRect, "Theme " + ThemeNames[i], ThemeNames[i],
                new Vector2(-160 + 160 * i, -92), new Vector2(150, 66), font,
                () => SetThemeChoice(choice));
            themeButtons.Add(button.GetComponent<Image>());
        }
        MakeButton(pageRect, "Reset settings", "Reset to defaults", new Vector2(0, -315),
            new Vector2(300, 66), font, ResetToDefaults);
        UpdateRoomButtons();
        UpdateThemeButtons();
        page.SetActive(false);
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

    public void ApplyTheme()
    {
        var anatomy = GameObject.Find("Anatomy information panel");
        if (anatomy != null) ApplyThemeTo(anatomy.transform);
        var quiz = GameObject.Find("Quiz station");
        if (quiz != null) ApplyThemeTo(quiz.transform);
        UpdateRoomButtons();
        UpdateThemeButtons();
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
            else if (image.name == "Anatomy information panel" || image.name == "Settings page" ||
                image.name == "Quiz panel" || image.name == "Quiz launcher") image.color = palette.background;
            else if (image.name == "Skull spread control") image.color = palette.inset;
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
        if (Instance == this) Instance = null;
    }
}
