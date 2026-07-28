using UnityEngine;

public class AutoAddBoxColliders : MonoBehaviour
{
    void Start()
    {
        int added = 0;

        // Detect both MeshFilter and MeshRenderer
        foreach (var mesh in GetComponentsInChildren<Transform>())
        {
            // Only add if visible mesh exists
            if (mesh.GetComponent<MeshRenderer>() || mesh.GetComponent<SkinnedMeshRenderer>())
            {
                if (mesh.GetComponent<Collider>() == null)
                {
                    mesh.gameObject.AddComponent<BoxCollider>();
                    added++;
                }
            }
        }

        Debug.Log($"✅ Added {added} BoxColliders automatically!");
    }
}
