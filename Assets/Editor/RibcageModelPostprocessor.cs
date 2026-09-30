using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Gives G2 rib and sternum meshes their own hover targets.
/// Cartilage stays visible without interaction colliders.
/// </summary>
public sealed class RibcageModelPostprocessor : AssetPostprocessor
{
    private const string BlenderModelPath = "Assets/ribs.blend";
    private const string FbxModelPath = "Assets/Art/Models/Skeleton/Mid Poly/Axial Bone Groups/ribs.fbx";
    private const string HighlightMaterialPath = "Assets/HighlightMst.mat";

    private void OnPostprocessModel(GameObject root)
    {
        if (assetPath != BlenderModelPath && assetPath != FbxModelPath) return;

        var cage = root.GetComponentsInChildren<Transform>(true)
            .SingleOrDefault(child => child.name == "G2_RIBCAGE") ?? root.transform;

        var bones = cage.Cast<Transform>()
            .Where(child => child.name == "Sternum (Breastbone)" ||
                child.name.IndexOf(" rib (", StringComparison.Ordinal) >= 0)
            .ToArray();
        if (bones.Length != 25)
            throw new InvalidOperationException($"Expected 25 direct bone mesh children in {assetPath}; found {bones.Length}.");

        var highlight = AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
        if (highlight == null)
            throw new InvalidOperationException($"Missing highlight material: {HighlightMaterialPath}");

        foreach (Transform bone in bones)
        {
            var filter = bone.GetComponent<MeshFilter>();
            if (bone.childCount != 0 || filter == null || filter.sharedMesh == null ||
                bone.GetComponent<MeshRenderer>() == null)
                throw new InvalidOperationException($"{bone.name} must be a direct rendered mesh child of {assetPath}.");

            var collider = bone.GetComponent<MeshCollider>();
            if (collider == null) collider = bone.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;

            var interactable = bone.GetComponent<XRSimpleInteractable>();
            if (interactable == null) interactable = bone.gameObject.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Clear();
            interactable.colliders.Add(collider);

            var highlighter = bone.GetComponent<BoneGroupHoverHighlighter>();
            if (highlighter == null) highlighter = bone.gameObject.AddComponent<BoneGroupHoverHighlighter>();
            highlighter.Configure(highlight);
        }
    }

}
