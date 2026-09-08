using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BoneGroupHoverHighlighter : MonoBehaviour
{
    [SerializeField] private Material highlightMaterial;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private XRBaseInteractable[] interactables;
    private int hoverCount;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        originalMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].sharedMaterials;

        // Finds every grabbable/hoverable bone under Axial or Appendicular.
        interactables = GetComponentsInChildren<XRBaseInteractable>(true);

        foreach (var interactable in interactables)
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }
    }

    private void OnDestroy()
    {
        if (interactables == null) return;

        foreach (var interactable in interactables)
        {
            if (interactable == null) continue;
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        hoverCount++;

        foreach (var renderer in renderers)
        {
            var highlighted = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < highlighted.Length; i++)
                highlighted[i] = highlightMaterial;

            renderer.materials = highlighted;
        }
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        hoverCount = Mathf.Max(0, hoverCount - 1);

        if (hoverCount > 0) return;

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].materials = originalMaterials[i];
    }
}