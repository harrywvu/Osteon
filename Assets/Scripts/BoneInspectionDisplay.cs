using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoneInspectionDisplay : MonoBehaviour
{
    [SerializeField] private Transform boneAnchor;
    [SerializeField] private Transform referenceAnchor;
    [SerializeField] private Material highlightMaterial;
    [SerializeField, Min(0.01f)] private float boneDiameter = 0.30f;
    [SerializeField, Min(0.01f)] private float referenceHeight = 0.45f;

    private GameObject boneVisual;
    private GameObject referenceVisual;
    public Transform BoneVisual => boneVisual != null ? boneVisual.transform : null;
    public Transform ReferenceVisual => referenceVisual != null ? referenceVisual.transform : null;

    public void Show(BoneSelection bone, Transform group, Quaternion authoredOrientation)
    {
        Clear();
        try
        {
            boneVisual = CreateVisual("Inspected bone", bone.Renderers, group, authoredOrientation,
                boneAnchor, null);
            Fit(boneVisual.transform, boneDiameter, false);
            referenceVisual = CreateVisual("Reference column", group.GetComponentsInChildren<Renderer>(true),
                group, authoredOrientation, referenceAnchor, new HashSet<Renderer>(bone.Renderers));
            Fit(referenceVisual.transform, referenceHeight, true);
        }
        catch
        {
            Clear();
            throw;
        }
    }

    private GameObject CreateVisual(string label, Renderer[] renderers, Transform sourceRoot,
        Quaternion orientation, Transform anchor, HashSet<Renderer> selected)
    {
        var root = new GameObject(label);
        root.transform.SetParent(anchor, false);
        var geometry = new GameObject("Geometry").transform;
        geometry.SetParent(root.transform, false);
        geometry.localRotation = Quaternion.Inverse(anchor.rotation) * orientation;
        int count = 0;
        foreach (Renderer source in renderers)
        {
            if (!(source is MeshRenderer)) continue;
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var part = new GameObject(source.name);
            part.transform.SetParent(geometry, false);
            Matrix4x4 relative = sourceRoot.worldToLocalMatrix * source.transform.localToWorldMatrix;
            part.transform.localPosition = relative.GetColumn(3);
            part.transform.localRotation = relative.rotation;
            part.transform.localScale = relative.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = part.AddComponent<MeshRenderer>();
            var materials = source.sharedMaterials;
            if (selected != null && selected.Contains(source) && highlightMaterial != null)
                for (int j = 0; j < materials.Length; j++) materials[j] = highlightMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            count++;
        }
        if (count == 0)
        {
            Destroy(root);
            throw new InvalidOperationException("The selected anatomy has no imported static mesh geometry.");
        }
        return root;
    }

    public static Bounds LocalBounds(Transform root)
    {
        Bounds bounds = default;
        bool first = true;
        foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null) continue;
            Bounds local = mesh.sharedMesh.bounds;
            Matrix4x4 matrix = root.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1,
                    (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }

    private static void Fit(Transform root, float size, bool byHeight)
    {
        Bounds bounds = LocalBounds(root);
        float extent = byHeight ? bounds.size.y : bounds.size.magnitude;
        if (extent < 0.00001f) throw new InvalidOperationException("Anatomy bounds are empty.");
        float scale = size / extent;
        Transform geometry = root.GetChild(0);
        geometry.localPosition = -bounds.center * scale;
        geometry.localScale = Vector3.one * scale;
    }

    public void SetRotation(Quaternion rotation)
    {
        if (boneVisual != null) boneVisual.transform.localRotation = rotation;
    }

    public void Clear()
    {
        // Deactivate immediately: deferred destruction must not leave two displays visible.
        if (boneVisual != null) { boneVisual.SetActive(false); Destroy(boneVisual); }
        if (referenceVisual != null) { referenceVisual.SetActive(false); Destroy(referenceVisual); }
        boneVisual = null;
        referenceVisual = null;
    }

    private void OnDisable() => Clear();
}
