using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DivisionSelection : MonoBehaviour
{
    [SerializeField] private AnatomyNavigationController navigation;
    [Header("Division information")]
    [SerializeField] private string divisionTitle;

    [TextArea(4, 8)]
    [SerializeField] private string divisionDescription;

    private XRBaseInteractable[] childInteractables;
    private bool selected;

    private void Awake()
    {
        childInteractables = GetComponentsInChildren<XRBaseInteractable>(true);

        foreach (var interactable in childInteractables)
            interactable.selectEntered.AddListener(OnChildSelected);
    }

    private void OnDestroy()
    {
        if (childInteractables == null) return;

        foreach (var interactable in childInteractables)
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnChildSelected);
        }
    }

    private void OnChildSelected(SelectEnterEventArgs args)
    {
        if (!isActiveAndEnabled) return;
        if (navigation != null)
        {
            navigation.SelectDivision(gameObject, divisionTitle, divisionDescription);
            return;
        }
        if (selected) return;

        selected = true;
        InfoBoardController.SelectDivision(gameObject, divisionTitle, divisionDescription);
    }

    public void ResetSelection()
    {
        selected = false;
    }
}
