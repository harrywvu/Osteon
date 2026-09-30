using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>A ray-grabbable bar that moves a world-space panel without covering its UI.</summary>
public static class PanelMoveHandle
{
    public static void Configure(Transform movableRoot, RectTransform panel, TMP_FontAsset font,
        Vector2 position, Vector2 size)
    {
        Transform existing = panel.Find("Move handle");
        GameObject handle = existing != null ? existing.gameObject :
            new GameObject("Move handle", typeof(RectTransform));
        handle.transform.SetParent(panel, false);
        handle.layer = 0;
        var rect = (RectTransform)handle.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = new Vector3(position.x, position.y, 0);
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        var image = handle.GetComponent<Image>();
        if (image == null) image = handle.AddComponent<Image>();
        image.color = new Color(0.07f, 0.27f, 0.39f, 1f);
        image.raycastTarget = false;

        Transform labelTransform = handle.transform.Find("Move label");
        GameObject labelObject = labelTransform != null ? labelTransform.gameObject :
            new GameObject("Move label", typeof(RectTransform));
        labelObject.transform.SetParent(handle.transform, false);
        labelObject.layer = handle.layer;
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition3D = Vector3.zero;
        labelRect.sizeDelta = size - new Vector2(20, 8);
        labelRect.localRotation = Quaternion.identity;
        labelRect.localScale = Vector3.one;
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        if (label == null) label = labelObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = "Move — hold select to reposition";
        label.fontSize = 23;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.94f, 0.97f, 1f, 1f);
        label.raycastTarget = false;

        var collider = handle.GetComponent<BoxCollider>();
        if (collider == null) collider = handle.AddComponent<BoxCollider>();
        collider.size = new Vector3(size.x, size.y, 40f);
        collider.center = Vector3.zero;
        collider.isTrigger = false;

        var body = movableRoot.GetComponent<Rigidbody>();
        if (body == null) body = movableRoot.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        var grab = movableRoot.GetComponent<XRGrabInteractable>();
        if (grab == null) grab = movableRoot.gameObject.AddComponent<XRGrabInteractable>();
        grab.colliders.Clear();
        grab.colliders.Add(collider);
        grab.selectMode = InteractableSelectMode.Single;
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.throwOnDetach = false;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
    }
}
