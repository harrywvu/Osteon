using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Local appearance preferences and the ray-selectable anatomy settings page.</summary>
public sealed class OsteonSettings : MonoBehaviour
{
    private const string RoomKey = "Osteon.RoomColor.v1";
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly string[] RoomNames = { "Original", "Cool blue", "Sage", "Warm" };
    private static readonly Color[] RoomColors =
    {
        Color.white,
        new Color(0.47f, 0.69f, 0.94f),
        new Color(0.55f, 0.77f, 0.62f),
        new Color(0.93f, 0.71f, 0.53f)
    };

    private readonly Dictionary<Renderer, Material[]> originalRendererMaterials = new Dictionary<Renderer, Material[]>();
    private readonly Dictionary<Material, Material> roomCopies = new Dictionary<Material, Material>();
    private readonly Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
    private readonly List<Image> roomButtons = new List<Image>();
    private GameObject page;
    private int roomChoice;

    public static OsteonSettings Instance { get; private set; }
    public int RoomChoice => roomChoice;
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

    public void ResetToDefaults()
    {
        SetRoomChoice(0);
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
        MakeButton(pageRect, "Reset settings", "Reset to defaults", new Vector2(0, -315),
            new Vector2(300, 66), font, ResetToDefaults);
        UpdateRoomButtons();
        page.SetActive(false);
    }

    private void UpdateRoomButtons()
    {
        for (int i = 0; i < roomButtons.Count; i++)
            roomButtons[i].color = i == roomChoice
                ? new Color(0.18f, 0.58f, 0.72f, 1f)
                : new Color(0.09f, 0.27f, 0.38f, 1f);
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
