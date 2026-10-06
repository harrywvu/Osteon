using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BoneGroupHoverHighlighter : MonoBehaviour
{
    [SerializeField] private Material highlightMaterial;

    public Material HighlightMaterial => highlightMaterial;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private Material[][] highlightMaterials;
    private readonly List<XRBaseInteractable> interactables = new List<XRBaseInteractable>();
    private int hoverCount;

    public void Configure(Material material)
    {
        highlightMaterial = material;
        if (renderers != null) CacheHighlightMaterials();
    }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        originalMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].sharedMaterials;

        CacheHighlightMaterials();
        RefreshInteractables();
    }

    private void CacheHighlightMaterials()
    {
        highlightMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            highlightMaterials[i] = new Material[originalMaterials[i].Length];
            for (int j = 0; j < highlightMaterials[i].Length; j++)
                highlightMaterials[i][j] = highlightMaterial;
        }
    }

    public void RefreshInteractables()
    {
        foreach (var interactable in GetComponentsInChildren<XRBaseInteractable>(true))
        {
            if (interactables.Contains(interactable)) continue;
            interactables.Add(interactable);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }
    }

    private void OnDestroy()
    {
        foreach (var interactable in interactables)
        {
            if (interactable == null) continue;
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!isActiveAndEnabled || highlightMaterial == null) return;
        hoverCount++;
        if (hoverCount > 1) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sharedMaterials = highlightMaterials[i];
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (hoverCount == 0) return;
        hoverCount--;

        if (hoverCount > 0) return;

        ClearHighlight();
    }

    public void ClearHighlight()
    {
        hoverCount = 0;
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sharedMaterials = originalMaterials[i];
    }

    private void OnDisable() => ClearHighlight();
}
