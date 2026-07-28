using UnityEngine;
using TMPro;

public class InfoBoardController : MonoBehaviour
{
    private static InfoBoardController instance;

    [Header("UI References")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }

    public static void ShowInfo(string title, string description)
    {
        if (instance == null)
        {
            return;
        }

        if (instance.titleText != null)
        {
            instance.titleText.text = title;
        }

        if (instance.descriptionText != null)
        {
            instance.descriptionText.text = description;
        }

        if (instance.infoPanel != null)
        {
            instance.infoPanel.SetActive(true);
        }
    }

    public static void HideInfo()
    {
        if (instance == null)
        {
            return;
        }

        if (instance.infoPanel != null)
        {
            instance.infoPanel.SetActive(false);
        }
    }
}
