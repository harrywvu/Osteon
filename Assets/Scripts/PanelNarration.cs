using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>On-demand narration shared by the three world-space panels.</summary>
public sealed class PanelNarration : MonoBehaviour
{
    private static PanelNarration activePanel;
    private Func<IEnumerable<string>> content;
    private TextMeshProUGUI label;
    private Coroutine playback;
    private string spokenContent;

    public static PanelNarration Attach(RectTransform panel,
        Func<IEnumerable<string>> contentProvider, TMP_FontAsset font)
    {
        var narration = panel.GetComponent<PanelNarration>();
        if (narration == null) narration = panel.gameObject.AddComponent<PanelNarration>();
        narration.content = contentProvider;
        if (narration.label == null) narration.BuildTab(panel, font);
        return narration;
    }

    public void ContentChanged()
    {
        if (activePanel == this) StopPlayback();
        else SetLabel("Read");
    }

    private void BuildTab(RectTransform panel, TMP_FontAsset font)
    {
        var tab = new GameObject("Narration tab", typeof(RectTransform), typeof(Image), typeof(Button));
        tab.layer = panel.gameObject.layer;
        tab.transform.SetParent(panel, false);
        var rect = (RectTransform)tab.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(156f, 82f);
        rect.anchoredPosition = new Vector2(panel.rect.width * 0.5f + 80f,
            panel.rect.height * 0.5f - 110f);
        var button = tab.GetComponent<Button>();
        button.targetGraphic = tab.GetComponent<Image>();
        button.onClick.AddListener(Toggle);
        var text = new GameObject("Narration label", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.layer = tab.layer;
        text.transform.SetParent(tab.transform, false);
        var textRect = (RectTransform)text.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6, 4);
        textRect.offsetMax = new Vector2(-6, -4);
        label = text.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        SetLabel("Read");
    }

    private void Toggle()
    {
        if (activePanel == this) { StopPlayback(); return; }
        if (activePanel != null) activePanel.StopPlayback();
        spokenContent = CurrentContent();
        if (string.IsNullOrWhiteSpace(spokenContent))
        {
            SetLabel("Audio unavailable");
            return;
        }
        activePanel = this;
        SetLabel("Loading");
        playback = StartCoroutine(Speak(spokenContent));
    }

    private IEnumerator Speak(string text)
    {
        OnDeviceSpeech.Initialize();
        float deadline = Time.realtimeSinceStartup + 10f;
        while (OnDeviceSpeech.State == 0 && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (OnDeviceSpeech.State != 1 || !OnDeviceSpeech.Speak(text))
        {
            FailPlayback();
            yield break;
        }

        SetLabel("Stop");
        deadline = Time.realtimeSinceStartup + 10f;
        while (activePanel == this)
        {
            if (spokenContent != CurrentContent()) { StopPlayback(); yield break; }
            int state = OnDeviceSpeech.PlaybackState;
            if (state == 2) { FinishPlayback(); yield break; }
            if (state < 0 || (state == 0 && Time.realtimeSinceStartup >= deadline))
            {
                FailPlayback();
                yield break;
            }
            yield return null;
        }
    }

    private string CurrentContent()
    {
        if (content == null) return "";
        var parts = new List<string>();
        foreach (string value in content())
            if (!string.IsNullOrWhiteSpace(value)) parts.Add(value.Trim());
        return string.Join("\n", parts);
    }

    private void LateUpdate()
    {
        if (activePanel == this && spokenContent != CurrentContent()) StopPlayback();
    }

    private void StopPlayback()
    {
        if (playback != null) { StopCoroutine(playback); playback = null; }
        FinishPlayback();
    }

    private void FailPlayback()
    {
        if (activePanel == this)
        {
            OnDeviceSpeech.Stop();
            activePanel = null;
        }
        playback = null;
        SetLabel("Audio unavailable");
    }

    private void FinishPlayback()
    {
        playback = null;
        if (activePanel == this)
        {
            OnDeviceSpeech.Stop();
            activePanel = null;
        }
        SetLabel("Read");
    }

    private void OnDisable() { if (activePanel == this) StopPlayback(); }
    private void OnDestroy() { if (activePanel == this) StopPlayback(); }
    private void OnApplicationQuit() { OnDeviceSpeech.Shutdown(); }
    private void SetLabel(string value) { if (label != null) label.text = value; }
}
