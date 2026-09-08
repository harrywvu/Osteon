using UnityEngine;
using TMPro;

public class InfoBoardController : MonoBehaviour
{
    private static InfoBoardController instance;

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

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        ReturnToDivisionSelection();
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

    private void Show(string title, string description)
    {
        if (titleText != null)
            titleText.text = title;

        if (descriptionText != null)
            descriptionText.text = description;

        if (infoPanel != null)
            infoPanel.SetActive(true);
    }
}