using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
[RequireComponent(typeof(BonePartInfo), typeof(XRSimpleInteractable))]
public sealed class BoneSelection : MonoBehaviour
{
    [SerializeField] private AnatomyNavigationController navigation;
    [SerializeField] private Renderer[] boneRenderers;
    [SerializeField] private Material highlightMaterial;

    private XRSimpleInteractable interactable;
    private Material[][] originals;
    private bool desktopHovered;
    public BonePartInfo Info { get; private set; }
    public Renderer[] Renderers => boneRenderers;
    public Material HighlightMaterial => highlightMaterial;
    public bool IsHovered => interactable != null && interactable.isHovered;

    public void SetDesktopHover(bool hovered)
    {
        if (navigation == null) return;
        desktopHovered = hovered;
        if (hovered && navigation.Level == AnatomyLevel.Group)
        {
            if (highlightMaterial != null) ApplyHighlight(highlightMaterial);
            navigation.Preview(this);
        }
        else
        {
            ClearHighlight();
            navigation.EndPreview(this);
        }
    }

    public void ConfigureHighlight(Material material)
    {
        highlightMaterial = material;
        if ((IsHovered || desktopHovered) && originals != null)
            ApplyHighlight(material);
    }

    private void Awake()
    {
        Info = GetComponent<BonePartInfo>();
        interactable = GetComponent<XRSimpleInteractable>();
        originals = new Material[boneRenderers.Length][];
        for (int i = 0; i < boneRenderers.Length; i++)
            originals[i] = boneRenderers[i].sharedMaterials;
    }

    private void OnEnable()
    {
        interactable.firstHoverEntered.AddListener(OnHover);
        interactable.lastHoverExited.AddListener(OnUnhover);
        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        desktopHovered = false;
        interactable.firstHoverEntered.RemoveListener(OnHover);
        interactable.lastHoverExited.RemoveListener(OnUnhover);
        interactable.selectEntered.RemoveListener(OnSelected);
        ClearHighlight();
        if (navigation != null)
            navigation.EndPreview(this);
    }

    private void OnHover(HoverEnterEventArgs args)
    {
        if (navigation == null || navigation.Level != AnatomyLevel.Group) return;
        if (highlightMaterial != null)
            ApplyHighlight(highlightMaterial);
        navigation.Preview(this);
    }

    private void ApplyHighlight(Material material)
    {
        for (int i = 0; i < boneRenderers.Length; i++)
        {
            var materials = new Material[originals[i].Length];
            for (int j = 0; j < materials.Length; j++) materials[j] = material;
            boneRenderers[i].sharedMaterials = materials;
        }
    }

    private void OnUnhover(HoverExitEventArgs args)
    {
        ClearHighlight();
        if (navigation != null) navigation.EndPreview(this);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (navigation != null) navigation.InspectBone(this);
    }

    public void ClearHighlight()
    {
        if (originals == null) return;
        for (int i = 0; i < boneRenderers.Length; i++)
            if (boneRenderers[i] != null) boneRenderers[i].sharedMaterials = originals[i];
    }
}
