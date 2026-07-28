using UnityEngine;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BonePartGrabRelease : MonoBehaviour
{
    [Header("Auto Return Settings")]
    [SerializeField] private float returnDelay = 0.5f;
    [SerializeField] private float returnDuration = 1f;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Transform initialParent;
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private bool isReturning = false;
    private BonePartInfo partInfo;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        partInfo = GetComponent<BonePartInfo>();

        initialParent = transform.parent;
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
        else
        {
            Debug.LogWarning($"No XRGrabInteractable found on {gameObject.name}");
        }
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        if (isReturning)
        {
            StopAllCoroutines();
            isReturning = false;
        }

        if (partInfo != null)
        {
            InfoBoardController.ShowInfo(partInfo.PartName, partInfo.PartDescription);
        }
    }

    private void OnReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
    {
        InfoBoardController.HideInfo();
        StartCoroutine(ReturnToInitialPosition());
    }

    private IEnumerator ReturnToInitialPosition()
    {
        yield return new WaitForSeconds(returnDelay);

        if (transform.parent != initialParent)
        {
            transform.SetParent(initialParent);
        }

        isReturning = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        float elapsedTime = 0f;
        Vector3 startLocalPosition = transform.localPosition;
        Quaternion startLocalRotation = transform.localRotation;

        while (elapsedTime < returnDuration)
        {
            float t = elapsedTime / returnDuration;
            transform.localPosition = Vector3.Lerp(startLocalPosition, initialLocalPosition, t);
            transform.localRotation = Quaternion.Lerp(startLocalRotation, initialLocalRotation, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;

        isReturning = false;
    }
}
