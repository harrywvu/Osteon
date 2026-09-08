using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class AxialDivisionSelection : MonoBehaviour
{
    [Header("Division objects")]

//  The Division to be hidden
    [SerializeField] private GameObject appendicularDivision; 

    [Header("Information shown after selection")]
    [SerializeField] private string divisionTitle = "Axial Skeleton";

    [TextArea(4, 8)]
    [SerializeField]
    private string divisionDescription =
        "The axial skeleton forms the central axis of the body. It includes the skull, vertebral column, ribs, and sternum.";

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
        if (selected) return;

        selected = true;

        if (appendicularDivision != null)
            appendicularDivision.SetActive(false);

        InfoBoardController.ShowInfo(divisionTitle, divisionDescription);
    }
}