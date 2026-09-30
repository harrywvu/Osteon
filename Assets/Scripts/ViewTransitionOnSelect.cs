using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ViewTransitionOnSelect : MonoBehaviour
{
    [SerializeField] private AnatomyNavigationController navigation;
    [Tooltip("Optional child hierarchy that owns the interactables for this transition.")]
    [SerializeField] private Transform interactableRoot;
    [SerializeField] private string nextTitle;
    [SerializeField, TextArea(3, 6)] private string nextDescription;
    [Header("View transition")]
    [SerializeField] private GameObject currentView;
    [SerializeField] private GameObject nextView;
    [Header("Group setup")]
    [Tooltip("Creates ray targets on rendered mesh objects under this group. Disable when targets are authored separately.")]
    [SerializeField] private bool autoCreateMeshTargets = true;
    [Tooltip("Optional override for the navigation controller's group highlight material.")]
    [SerializeField] private Material highlightMaterial;

    private XRBaseInteractable[] interactables;
    private bool hasTransitioned;

    public GameObject NextView => nextView;

    private void Awake() => Prepare();

    public void Prepare(AnatomyNavigationController owner = null)
    {
        if (navigation == null && owner != null) navigation = owner;
        if (interactableRoot == null) interactableRoot = transform;

        if (nextView != null && GetComponent<DivisionSelection>() == null)
        {
            if (autoCreateMeshTargets) EnsureMeshTargets();
            var highlighter = GetComponent<BoneGroupHoverHighlighter>();
            if (highlighter == null) highlighter = gameObject.AddComponent<BoneGroupHoverHighlighter>();
            Material material = highlightMaterial != null ? highlightMaterial :
                highlighter.HighlightMaterial != null ? highlighter.HighlightMaterial :
                navigation != null ? navigation.GroupHighlightMaterial : null;
            if (material != null) highlighter.Configure(material);
            highlighter.RefreshInteractables();
        }

        if (interactables != null) return;
        interactables = interactableRoot.GetComponentsInChildren<XRBaseInteractable>(true);
        if (nextView != null && GetComponent<DivisionSelection>() == null && interactables.Length == 0)
            Debug.LogWarning($"Bone group {name} has no ray targets. Add rendered meshes or authored interactables.", this);

        foreach (var interactable in interactables)
            interactable.selectEntered.AddListener(OnSelected);
    }

    private void EnsureMeshTargets()
    {
        foreach (var filter in interactableRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<Renderer>() == null) continue;
            var collider = filter.GetComponent<Collider>();
            if (collider is MeshCollider existingMeshCollider && existingMeshCollider.sharedMesh == null)
                existingMeshCollider.sharedMesh = filter.sharedMesh;
            if (collider == null)
            {
                var meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
                collider = meshCollider;
            }
            var interactable = filter.GetComponent<XRBaseInteractable>();
            if (interactable == null)
                interactable = filter.gameObject.AddComponent<XRSimpleInteractable>();
            if (!interactable.colliders.Contains(collider)) interactable.colliders.Add(collider);
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
                navigation.EnterGroup(nextView,
                    string.IsNullOrWhiteSpace(nextTitle) ? gameObject.name : nextTitle,
                    nextDescription);
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
