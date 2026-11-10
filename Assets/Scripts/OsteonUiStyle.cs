using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared shapes and pictograms for the project-owned world-space UI.</summary>
public static class OsteonUiStyle
{
    private static Sprite rounded;

    public static void Surface(Image image, bool sliced = true)
    {
        if (image == null) return;
        if (rounded == null) rounded = Resources.Load<Sprite>("OsteonUI/rounded");
        if (rounded == null) return;
        image.sprite = rounded;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
    }

    public static void Decorate(Image image, Color accent, Color iconColor)
    {
        if (image == null) return;
        string name = image.name;
        var rect = image.transform as RectTransform;
        if (rect == null) return;

        if (IsPanel(name))
        {
            Surface(image);
            var rail = EnsureImage(rect, "UI accent", "rounded", new Vector2(0, rect.rect.height * 0.5f - 5f),
                new Vector2(Mathf.Max(0, rect.rect.width - 24f), 6f));
            rail.color = accent;
            return;
        }

        var button = image.GetComponent<Button>();
        if (button != null || name == "Move handle" || name == "Group spread control") Surface(image);
        else return;

        string icon = IconFor(name, image.GetComponentInChildren<TextMeshProUGUI>(true)?.text);
        if (icon == null) return;
        float width = rect.rect.width;
        if (width < 190f && name != "Narration tab") return;
        float size = Mathf.Min(width < 190f ? 25f : 31f, rect.rect.height * 0.46f);
        float x = -width * 0.5f + (width < 190f ? 19f : 34f);
        var symbol = EnsureImage(rect, "UI icon", icon, new Vector2(x, 0), new Vector2(size, size));
        symbol.color = iconColor;
        var label = image.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null && label.transform.parent == rect)
        {
            if (label.rectTransform.anchorMin.x != label.rectTransform.anchorMax.x)
            {
                label.rectTransform.offsetMin = new Vector2(43f, label.rectTransform.offsetMin.y);
                label.rectTransform.offsetMax = new Vector2(-6f, label.rectTransform.offsetMax.y);
            }
            else
            {
                label.rectTransform.sizeDelta = new Vector2(width - 72f, label.rectTransform.sizeDelta.y);
                label.rectTransform.anchoredPosition = new Vector2(22f, 0);
            }
        }
    }

    private static bool IsPanel(string name) =>
        name == "Anatomy information panel" || name == "Tutorial panel" ||
        name == "Settings panel" || name == "Quiz panel" || name == "Account panel";

    private static string IconFor(string name, string label)
    {
        string value = (name + " " + label).ToLowerInvariant();
        if (value.Contains("move handle")) return "move";
        if (value.Contains("narration tab")) return "audio";
        if (value.Contains("settings launcher")) return "settings";
        if (value.Contains("quiz launcher")) return "quiz";
        if (value.Contains("tutorial launcher")) return "guide";
        if (value.Contains("back") || value.Contains("cancel")) return "back";
        if (value.Contains("close") || value.Contains("hide") || value.Contains("sign out")) return "close";
        if (value.Contains("reset") || value.Contains("clear") || value.Contains("retry")) return "reset";
        if (value.Contains("account") || value.Contains("learner") || value.Contains("code")) return "account";
        if (value.Contains("guide") || value.Contains("tutorial")) return "guide";
        if (value.Contains("quiz") || value.Contains("practice")) return "quiz";
        if (value.Contains("settings")) return "settings";
        if (value.Contains("hospital color") || value.Contains("highlight color")) return "color";
        if (value.Contains("panel theme")) return "theme";
        if (value.Contains("text size")) return "text";
        if (value.Contains("saved") || value.Contains("continue") || value.Contains("finish")) return "check";
        if (value.Contains("spread")) return "move";
        if (int.TryParse(label, out _)) return null;
        if (value.StartsWith("button ") || value.Contains("explore")) return "next";
        return null;
    }

    public static void Heading(Transform root, string heading, string icon, Color tint)
    {
        RectTransform label = null;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (text.name == heading) { label = text.rectTransform; break; }
        if (label == null) return;
        var symbol = EnsureImage(label, "UI heading icon", icon,
            new Vector2(-label.rect.width * 0.5f - 13f, 0), new Vector2(25, 25));
        symbol.color = tint;
    }

    public static void ThemePreview(Image button, Color tint)
    {
        var rect = (RectTransform)button.transform;
        var swatch = EnsureImage(rect, "UI theme swatch", "rounded",
            new Vector2(0, rect.rect.height * 0.5f - 5f), new Vector2(rect.rect.width - 16f, 5f));
        swatch.color = tint;
    }

    private static Image EnsureImage(RectTransform parent, string name, string spriteName,
        Vector2 position, Vector2 size)
    {
        var child = parent.Find(name) as RectTransform;
        if (child == null)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.layer = parent.gameObject.layer;
            child = (RectTransform)obj.transform;
            child.SetParent(parent, false);
        }
        child.anchorMin = child.anchorMax = child.pivot = new Vector2(0.5f, 0.5f);
        child.anchoredPosition = position;
        child.sizeDelta = size;
        var image = child.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("OsteonUI/" + spriteName);
        image.raycastTarget = false;
        return image;
    }
}
