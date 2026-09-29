using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ViewTransitionOnSelect : MonoBehaviour
{
    [SerializeField] private AnatomyNavigationController navigation;
    [Tooltip("Optional child hierarchy that owns the interactables for this transition.")]
    [SerializeField] private Transform interactableRoot;
    [SerializeField] private string nextTitle = "Vertebral column";
    [SerializeField, TextArea(3, 6)] private string nextDescription =
        "Explore the vertebral column. Point at an individual bone and select it for inspection.";
    [Header("View transition")]
    [SerializeField] private GameObject currentView;
    [SerializeField] private GameObject nextView;

    private XRBaseInteractable[] interactables;
    private bool hasTransitioned;

    private void Awake()
    {
        ConfigureAxialGroupHighlights();
        Transform source = interactableRoot != null ? interactableRoot : transform;
        interactables = source.GetComponentsInChildren<XRBaseInteractable>(true);

        foreach (var interactable in interactables)
            interactable.selectEntered.AddListener(OnSelected);
    }

    private void ConfigureAxialGroupHighlights()
    {
        if (nextView == null || nextView.name != "VERTEBRAL COLUMN") return;

        string[] names = { "Skull", "Ribcage", "VertabralColumn" };
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        var groups = new Transform[names.Length];
        for (int i = 0; i < names.Length; i++)
            foreach (Transform candidate in descendants)
                if (candidate.name == names[i])
                {
                    groups[i] = candidate;
                    break;
                }

        if (System.Array.Exists(groups, group => group == null)) return;
        interactableRoot = interactableRoot != null ? interactableRoot : groups[2];

        Material material = null;
        foreach (Transform group in groups)
        {
            var existing = group.GetComponent<BoneGroupHoverHighlighter>();
            if (existing != null && existing.HighlightMaterial != null)
            {
                material = existing.HighlightMaterial;
                break;
            }
        }
        if (material == null) return;

        foreach (Transform group in groups)
        {
            var highlighter = group.GetComponent<BoneGroupHoverHighlighter>();
            if (highlighter == null) highlighter = group.gameObject.AddComponent<BoneGroupHoverHighlighter>();
            highlighter.Configure(material);
            highlighter.enabled = true;
        }
    }

    private void OnDestroy()
    {
        if (interactables == null)
            return;

        foreach (var interactable in interactables)
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnSelected);
        }
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (!isActiveAndEnabled) return;
        if (navigation != null)
        {
            // DivisionSelection owns division entry; do not advance twice for the same event.
            if (GetComponent<DivisionSelection>() == null)
                navigation.EnterGroup(nextView, nextTitle, nextDescription);
            return;
        }
        if (hasTransitioned || nextView == null)
            return;

        hasTransitioned = true;

        nextView.SetActive(true);

        if (currentView != null)
            currentView.SetActive(false);
    }
}
