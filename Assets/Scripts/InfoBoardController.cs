using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class InfoBoardController : MonoBehaviour
{
    private static InfoBoardController instance;
    [SerializeField] private AnatomyNavigationController navigation;
    [SerializeField] private TextMeshProUGUI backLabel;

    [Header("UI References")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private GameObject backButton;

    [Header("Divisions")]
    [SerializeField] private GameObject[] divisionRoots;

    [Header("Default Info")]
    [SerializeField] private string defaultTitle = "Human Skeleton";
    [TextArea(2, 4)]
    [SerializeField] private string defaultDescription = "Select a division to start";
    private PanelNarration narration;
    private string narrationTitle;
    private string narrationDescription;

    public string DefaultTitle => defaultTitle;
    public string DefaultDescription => defaultDescription;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (infoPanel != null && infoPanel.TryGetComponent<RectTransform>(out var panel))
            narration = PanelNarration.Attach(panel, NarrationContent,
                titleText != null ? titleText.font : null);
        narrationTitle = titleText != null ? titleText.text : defaultTitle;
        narrationDescription = descriptionText != null ? descriptionText.text : defaultDescription;

        if (navigation == null) ReturnToDivisionSelection();
    }

    public static void SelectDivision(
        GameObject selectedDivision,
        string title,
        string description)
    {
        if (instance == null) return;

        foreach (var division in instance.divisionRoots)
        {
            if (division != null)
                division.SetActive(division == selectedDivision);
        }

        instance.Show(title, description);

        if (instance.backButton != null)
            instance.backButton.SetActive(true);
    }

    public void ReturnToDivisionSelection()
    {
        if (navigation != null)
        {
            navigation.ReturnToOverview();
            return;
        }
        foreach (var division in divisionRoots)
        {
            if (division == null) continue;

            division.SetActive(true);

            var selection = division.GetComponent<DivisionSelection>();
            if (selection != null)
                selection.ResetSelection();
        }

        Show(defaultTitle, defaultDescription);

        if (backButton != null)
            backButton.SetActive(false);
    }

    public static void ShowInfo(string title, string description)
    {
        if (instance != null)
            instance.Show(title, description);
    }

    public static void HideInfo()
    {
        if (instance != null && instance.infoPanel != null)
            instance.infoPanel.SetActive(false);
    }

    public void ShowNavigationInfo(string title, string description, string backText,
        bool canGoBack, string spokenDescription = null)
    {
        Show(title, description);
        if (spokenDescription != null)
        {
            narrationDescription = spokenDescription;
            narration?.ContentChanged();
        }
        if (backLabel != null) backLabel.text = backText;
        if (backButton != null) backButton.SetActive(canGoBack);
    }

    public void SetNavigationBackInteractable(bool value)
    {
        if (backButton != null && backButton.TryGetComponent<Button>(out var button))
            button.interactable = value;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Show(string title, string description)
    {
        narrationTitle = title;
        narrationDescription = description;
        narration?.ContentChanged();
        if (titleText != null)
            titleText.text = title;

        if (descriptionText != null)
        {
            descriptionText.text = description;
            OsteonSettings.ScrollToTop(descriptionText);
        }

        if (infoPanel != null)
            infoPanel.SetActive(true);
    }

    private IEnumerable<string> NarrationContent()
    {
        yield return narrationTitle;
        yield return narrationDescription;
    }
}
