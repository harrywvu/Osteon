using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Moves the existing G2 skull bones while keeping their ray targets attached.</summary>
[DisallowMultipleComponent]
public sealed class SkullExplosionController : MonoBehaviour
{
    [SerializeField] private BoneSelection[] bones;
    [SerializeField, Min(0.01f)] private float spreadInSkullHeights = 0.8f;
    [SerializeField, Min(0.01f)] private float transitionSeconds = 0.35f;

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

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        if (bones == null || bones.Length != 29)
            throw new InvalidOperationException("Skull explosion requires the 29 authored bone selections.");

        Bounds bounds = BoneInspectionDisplay.LocalBounds(transform);
        if (bounds.size.y <= 0f) throw new InvalidOperationException("The skull has no visible bounds.");
        float distance = bounds.size.y * spreadInSkullHeights;
        boneTransforms = new Transform[bones.Length];
        assembledPositions = new Vector3[bones.Length];
        offsets = new Vector3[bones.Length];
        var seen = new HashSet<Transform>();
        for (int i = 0; i < bones.Length; i++)
        {
            BoneSelection bone = bones[i];
            if (bone == null || !bone.transform.IsChildOf(transform) || !seen.Add(bone.transform))
                throw new InvalidOperationException("Skull explosion has a missing or duplicate bone.");
            Transform part = bone.transform;
            boneTransforms[i] = part;
            assembledPositions[i] = part.localPosition;
            Renderer renderer = bone.Renderers[0];
            MeshFilter mesh = renderer.GetComponent<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null)
                throw new InvalidOperationException("Skull explosion bone has no visual mesh: " + bone.name);
            Vector3 visualCenter = transform.InverseTransformPoint(
                renderer.transform.TransformPoint(mesh.sharedMesh.bounds.center));
            Vector3 direction = DirectionFor(bone.name, visualCenter - bounds.center);
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

    private static Vector3 DirectionFor(string name, Vector3 radial)
    {
        // Bone centers near the skull center need distinct directions to expose them.
        switch (name)
        {
            case "Ethmoid": return new Vector3(0f, 0.7f, 1f);
            case "Sphenoid": return new Vector3(0f, -0.3f, -1f);
            case "Vomer": return new Vector3(0f, -0.8f, 1f);
            case "Left Malleus": return new Vector3(-1f, 0.4f, -0.4f);
            case "Left Incus": return new Vector3(-1f, 0.9f, 0f);
            case "Left Stapes": return new Vector3(-1f, 0.1f, 0.7f);
            case "Right Malleus": return new Vector3(1f, 0.4f, -0.4f);
            case "Right Incus": return new Vector3(1f, 0.9f, 0f);
            case "Right Stapes": return new Vector3(1f, 0.1f, 0.7f);
            default: return radial.sqrMagnitude > 0.000001f ? radial : Vector3.up;
        }
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

    private void Update()
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
