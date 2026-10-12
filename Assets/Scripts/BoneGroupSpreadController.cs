using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Spreads authored G2 bones while keeping their ray targets attached.</summary>
[DisallowMultipleComponent]
public class BoneGroupSpreadController : MonoBehaviour
{
    [SerializeField] private BoneSelection[] bones;
    // Keep the field name so the authored skull component retains its tuning.
    [SerializeField, Min(0.01f)] private float spreadInSkullHeights = 0.8f;
    [SerializeField, Min(0.01f)] private float transitionSeconds = 0.35f;
    [SerializeField] private Vector3 directionWeights = Vector3.one;

    private readonly Dictionary<Renderer, Matrix4x4> assembledMatrices =
        new Dictionary<Renderer, Matrix4x4>();
    private Transform[] boneTransforms;
    private Vector3[] assembledPositions;
    private Vector3[] offsets;
    private Renderer[] contextRenderers;
    private bool initialized;
    private float currentSpread;
    private float targetSpread;

    public float Spread => targetSpread;
    public float VisibleSpread => currentSpread;
    public int BoneCount => boneTransforms?.Length ?? 0;

    protected virtual void Awake()
    {
        if (bones != null && bones.Length >= 2) Initialize();
    }

    public void Configure(BoneSelection[] entries, float spreadInGroupHeights, Vector3 weights)
    {
        if (initialized) throw new InvalidOperationException("Group spread cannot be reconfigured after capture.");
        bones = entries;
        spreadInSkullHeights = Mathf.Max(0.01f, spreadInGroupHeights);
        directionWeights = weights;
        Initialize();
    }

    private void Initialize()
    {
        if (initialized) return;
        if (bones == null || bones.Length < 2)
            throw new InvalidOperationException("Group spread requires at least two authored bone selections.");

        Bounds bounds = BoneInspectionDisplay.LocalBounds(transform);
        if (bounds.size.y <= 0f) throw new InvalidOperationException("The group has no visible bounds.");
        float distance = bounds.size.y * spreadInSkullHeights;
        boneTransforms = new Transform[bones.Length];
        assembledPositions = new Vector3[bones.Length];
        offsets = new Vector3[bones.Length];
        var seen = new HashSet<Transform>();
        for (int i = 0; i < bones.Length; i++)
        {
            BoneSelection bone = bones[i];
            if (bone == null || !bone.transform.IsChildOf(transform) || !seen.Add(bone.transform))
                throw new InvalidOperationException("Group spread has a missing or duplicate bone.");
            Transform part = bone.transform;
            boneTransforms[i] = part;
            assembledPositions[i] = part.localPosition;
            Renderer renderer = bone.Renderers[0];
            MeshFilter mesh = renderer.GetComponent<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null)
                throw new InvalidOperationException("Group spread bone has no visual mesh: " + bone.name);
            Vector3 visualCenter = transform.InverseTransformPoint(
                renderer.transform.TransformPoint(mesh.sharedMesh.bounds.center));
            Vector3 direction = DirectionFor(bone.name, i, visualCenter - bounds.center);
            Vector3 rootOffset = direction.normalized * distance;
            offsets[i] = part.parent.worldToLocalMatrix.MultiplyVector(
                transform.localToWorldMatrix.MultiplyVector(rootOffset));
        }

        var context = new List<Renderer>();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            assembledMatrices.Add(renderer,
                transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
            if (renderer.name.StartsWith("Context -", StringComparison.Ordinal)) context.Add(renderer);
        }
        contextRenderers = context.ToArray();
        initialized = true;
    }

    protected virtual Vector3 DirectionFor(string name, int index, Vector3 radial)
    {
        if (radial.sqrMagnitude > 0.000001f)
            return Vector3.Scale(radial, directionWeights);
        float angle = index * 2.39996323f;
        return new Vector3(Mathf.Cos(angle), (index % 3 - 1) * 0.3f, Mathf.Sin(angle));
    }

    public void SetSpread(float value)
    {
        Initialize();
        targetSpread = Mathf.Clamp01(value);
    }

    public void CompleteTransition()
    {
        Initialize();
        Apply(targetSpread);
    }

    public void ResetImmediate()
    {
        Initialize();
        targetSpread = 0f;
        Apply(0f);
    }

    protected virtual void Update()
    {
        if (!initialized || Mathf.Approximately(currentSpread, targetSpread)) return;
        Apply(Mathf.MoveTowards(currentSpread, targetSpread,
            Time.deltaTime / transitionSeconds));
    }

    private void Apply(float value)
    {
        currentSpread = value;
        for (int i = 0; i < boneTransforms.Length; i++)
            boneTransforms[i].localPosition = assembledPositions[i] + offsets[i] * value;
        bool showContext = value <= 0.001f;
        foreach (Renderer context in contextRenderers)
            if (context != null) context.enabled = showContext;
        if (gameObject.activeInHierarchy) Physics.SyncTransforms();
    }

    public Matrix4x4 AssembledRelativeMatrix(Renderer renderer)
    {
        Initialize();
        return assembledMatrices.TryGetValue(renderer, out Matrix4x4 matrix)
            ? matrix : transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
    }
}
