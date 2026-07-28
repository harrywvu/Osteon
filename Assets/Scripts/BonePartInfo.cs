using UnityEngine;

public class BonePartInfo : MonoBehaviour
{
    [Header("Part Information")]
    [SerializeField] private string partName;
    [TextArea(3, 6)]
    [SerializeField] private string partDescription;

    public string PartName => partName;
    public string PartDescription => partDescription;
}
