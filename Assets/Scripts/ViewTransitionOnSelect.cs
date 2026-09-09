using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ViewTransitionOnSelect : MonoBehaviour
{
    [SerializeField] private AnatomyNavigationController navigation;
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
        interactables = GetComponentsInChildren<XRBaseInteractable>(true);

        foreach (var interactable in interactables)
            interactable.selectEntered.AddListener(OnSelected);
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
