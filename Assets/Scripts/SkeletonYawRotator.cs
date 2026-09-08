using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class SkeletonYawRotator : MonoBehaviour
{
    public enum RotationCenter
    {
        ObjectOrigin,
        ModelCenter,
        CustomOffset
    }

    [Header("Rotation Center")]
    [SerializeField]
    private RotationCenter centerMode =
        RotationCenter.ObjectOrigin;

    [Tooltip("Rotation center relative to this object's origin.")]
    [SerializeField] private Vector3 customLocalCenter;

    [Header("Rotation")]
    [Min(0f)]
    [SerializeField] private float degreesPerSecond = 90f;

    [Range(0f, 1f)]
    [SerializeField] private float deadZone = 0.15f;

    [Tooltip("Reverse the existing thumbstick rotation direction.")]
    [SerializeField] private bool invertDirection;

    private InputDevice rightController;
    private Vector3 cachedLocalCenter;
    private Vector3 startingLocalPosition;
    private Quaternion startingLocalRotation;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
            return;

        startingLocalPosition = transform.localPosition;
        startingLocalRotation = transform.localRotation;
        cachedLocalCenter = CalculateLocalCenter();
        initialized = true;
    }

    private void Update()
    {
        if (!rightController.isValid)
        {
            rightController =
                InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        if (!rightController.TryGetFeatureValue(
                CommonUsages.primary2DAxis,
                out Vector2 thumbstick))
        {
            return;
        }

        float input = thumbstick.x;

        if (Mathf.Abs(input) <= deadZone)
            return;

        float direction = invertDirection ? 1f : -1f;
        float angle =
            input * direction * degreesPerSecond * Time.deltaTime;

        RotateYaw(angle);
    }

    // Independent of controller input, so other controls can reuse it.
    public void RotateYaw(float degrees)
    {
        Initialize();

        Vector3 worldCenter =
            transform.TransformPoint(cachedLocalCenter);

        transform.RotateAround(worldCenter, Vector3.up, degrees);
    }

    public void ResetOrientation()
    {
        Initialize();

        // An offset pivot changes position as well as rotation.
        transform.localPosition = startingLocalPosition;
        transform.localRotation = startingLocalRotation;
    }

    private Vector3 CalculateLocalCenter()
    {
        switch (centerMode)
        {
            case RotationCenter.ModelCenter:
                return CalculateModelCenter();

            case RotationCenter.CustomOffset:
                return customLocalCenter;

            default:
                return Vector3.zero;
        }
    }

    private Vector3 CalculateModelCenter()
    {
        // Include inactive anatomy so hiding a division does not
        // change which geometry contributes to the center.
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(true);

        Bounds combinedBounds = default;
        bool hasBounds = false;

        foreach (Renderer childRenderer in renderers)
        {
            // Only use model geometry.
            if (!(childRenderer is MeshRenderer) &&
                !(childRenderer is SkinnedMeshRenderer))
            {
                continue;
            }

            // Transform local bounds explicitly so inactive model
            // objects can contribute without relying on world bounds.
            Bounds localBounds = childRenderer.localBounds;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f);

                Vector3 localCorner = localBounds.center +
                    Vector3.Scale(localBounds.extents, offset);

                Vector3 worldCorner =
                    childRenderer.transform.TransformPoint(localCorner);

                if (!hasBounds)
                {
                    combinedBounds =
                        new Bounds(worldCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(worldCorner);
                }
            }
        }

        return hasBounds
            ? transform.InverseTransformPoint(combinedBounds.center)
            : Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 localCenter = Application.isPlaying && initialized
            ? cachedLocalCenter
            : CalculateLocalCenter();

        Vector3 worldCenter = transform.TransformPoint(localCenter);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(worldCenter, 0.025f);
        Gizmos.DrawLine(
            worldCenter - Vector3.up * 0.2f,
            worldCenter + Vector3.up * 0.2f);
    }
}