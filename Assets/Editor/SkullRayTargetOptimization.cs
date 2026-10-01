using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Assigns low-detail collision meshes without changing the rendered skull.</summary>
public static class SkullRayTargetOptimization
{
    public const string ColliderModelPath =
        "Assets/Art/Models/Skeleton/Mid Poly/Axial Bone Groups/skull_ray_colliders.fbx";

    public static Dictionary<string, Mesh> LoadMeshes()
    {
        var meshes = AssetDatabase.LoadAllAssetsAtPath(ColliderModelPath)
            .OfType<Mesh>().ToDictionary(mesh => mesh.name);
        var expected = SkullBoneCatalog.All().Select(entry => entry.meshName).ToArray();
        if (meshes.Count != expected.Length || expected.Any(name => !meshes.ContainsKey(name)))
            throw new InvalidOperationException("The skull ray collider FBX must contain exactly the 29 named bone meshes.");
        return meshes;
    }

    public static int Apply(GameObject skullView)
    {
        var meshes = LoadMeshes();
        var bones = skullView.GetComponentsInChildren<BoneSelection>(true)
            .ToDictionary(bone => bone.name);
        foreach (var entry in SkullBoneCatalog.All())
        {
            if (!bones.TryGetValue(entry.meshName, out BoneSelection bone))
                throw new InvalidOperationException("Missing skull bone: " + entry.meshName);
            var collider = bone.GetComponent<MeshCollider>();
            if (collider == null)
                throw new InvalidOperationException("Missing skull ray collider: " + entry.meshName);
            Mesh visual = bone.GetComponent<MeshFilter>().sharedMesh;
            Mesh ray = meshes[entry.meshName];
            if (visual == null)
                throw new InvalidOperationException("Missing rendered skull mesh: " + entry.meshName);
            if (ray.vertexCount >= visual.vertexCount ||
                Vector3.Distance(visual.bounds.center, ray.bounds.center) > 0.003f ||
                Vector3.Distance(visual.bounds.extents, ray.bounds.extents) > 0.003f)
                throw new InvalidOperationException($"Skull ray mesh is misaligned or too detailed: {entry.meshName}; " +
                    $"visual {visual.vertexCount} verts at {AssetDatabase.GetAssetPath(visual)}, center {visual.bounds.center.ToString("F6")}, " +
                    $"extents {visual.bounds.extents.ToString("F6")}, object scale {bone.transform.lossyScale.ToString("F6")}; " +
                    $"ray {ray.vertexCount} verts, center {ray.bounds.center.ToString("F6")}, " +
                    $"extents {ray.bounds.extents.ToString("F6")}");
            collider.sharedMesh = ray;
            collider.convex = false;
            EditorUtility.SetDirty(collider);
        }
        return meshes.Count;
    }

    [MenuItem("Anatomy/Optimize skull ray targets")]
    public static void ApplyToScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before optimizing skull ray targets.");
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your scene changes before optimizing skull ray targets.");
        Scene scene = EditorSceneManager.OpenScene(AnatomySceneSetup.ScenePath, OpenSceneMode.Single);
        var skullView = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(transform => transform.name == "G2_skull").gameObject;
        int count = Apply(skullView);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Assigned {count} low-detail skull ray meshes. Rendered and G3 meshes are unchanged.");
    }
}
