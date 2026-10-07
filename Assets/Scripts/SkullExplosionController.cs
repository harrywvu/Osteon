using UnityEngine;

/// <summary>Skull-specific directions for central bones and middle-ear ossicles.</summary>
[DisallowMultipleComponent]
public sealed class SkullExplosionController : BoneGroupSpreadController
{
    protected override Vector3 DirectionFor(string name, int index, Vector3 radial)
    {
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
}
