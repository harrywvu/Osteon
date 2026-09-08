using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ViewTransitionOnSelect : MonoBehaviour
{
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
        if (hasTransitioned || nextView == null)
            return;

        hasTransitioned = true;

        nextView.SetActive(true);

        if (currentView != null)
            currentView.SetActive(false);
    }
}