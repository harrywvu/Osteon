using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>On-demand narration shared by the world-space panels.</summary>
public sealed class PanelNarration : MonoBehaviour
{
    private static PanelNarration activePanel;
    private Func<IEnumerable<string>> content;
    private Func<bool> exactClipRequired;
    private TextMeshProUGUI label;
    private string spokenContent;
    private AudioSource audioSource;

    public static PanelNarration Attach(RectTransform panel,
        Func<IEnumerable<string>> contentProvider, TMP_FontAsset font, Func<bool> exactClipRequired = null)
    {
        var narration = panel.GetComponent<PanelNarration>();
        if (narration == null) narration = panel.gameObject.AddComponent<PanelNarration>();
        narration.content = contentProvider;
        narration.exactClipRequired = exactClipRequired;
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
        label.enableAutoSizing = true;
        label.fontSizeMin = 14;
        label.fontSizeMax = 24;
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
        if (exactClipRequired != null && exactClipRequired() && NarrationClips.Find(spokenContent) == null)
        {
            SetLabel("Audio unavailable");
            return;
        }
        activePanel = this;
        SetLabel("Loading");
        StartCoroutine(Speak(spokenContent));
    }

    private IEnumerator Speak(string text)
    {
        // A full clip takes precedence. Settings and quiz feedback can mix separate phrases.
        string[] lines = text.Split('\n');
        var parts = NarrationClips.Find(text) == null &&
            Array.Exists(lines, line => NarrationClips.Find(line) != null) ? lines : new[] { text };
        foreach (string part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var clip = NarrationClips.Find(part);
            if (clip != null)
            {
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                }
                audioSource.clip = clip;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                float deadline = Time.realtimeSinceStartup + 10f;
                while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (clip.loadState == AudioDataLoadState.Loaded)
                {
                    SetLabel("Stop");
                    audioSource.Play();
                    yield return null;
                    while (audioSource.isPlaying) yield return null;
                    ReleaseClip();
                    continue;
                }
                ReleaseClip();
            }

            yield return SpeakFallback(part);
            if (activePanel != this) yield break;
        }
        FinishPlayback();
    }

    private IEnumerator SpeakFallback(string text)
    {
        OnDeviceSpeech.Initialize();
        float deadline = Time.realtimeSinceStartup + 10f;
        while (OnDeviceSpeech.State == 0 && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (OnDeviceSpeech.State != 1 || !OnDeviceSpeech.Speak(text))
        {
            FinishPlayback("Audio unavailable");
            yield break;
        }

        SetLabel("Stop");
        deadline = Time.realtimeSinceStartup + 10f;
        while (activePanel == this)
        {
            int state = OnDeviceSpeech.PlaybackState;
            if (state == 2) yield break;
            if (state < 0 || (state == 0 && Time.realtimeSinceStartup >= deadline))
            {
                FinishPlayback("Audio unavailable");
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
        StopAllCoroutines();
        FinishPlayback();
    }

    private void FinishPlayback(string finalLabel = "Read")
    {
        if (activePanel == this)
        {
            ReleaseClip();
            OnDeviceSpeech.Stop();
            activePanel = null;
        }
        SetLabel(finalLabel);
    }

    private void ReleaseClip()
    {
        if (audioSource == null || audioSource.clip == null) return;
        var clip = audioSource.clip;
        audioSource.Stop();
        audioSource.clip = null;
        if (!clip.preloadAudioData && clip.loadState == AudioDataLoadState.Loaded)
            clip.UnloadAudioData();
    }

    private void OnDisable() { if (activePanel == this) StopPlayback(); }
    private void OnDestroy() { if (activePanel == this) StopPlayback(); }
    private void OnApplicationQuit() { OnDeviceSpeech.Shutdown(); }
    private void SetLabel(string value) { if (label != null) label.text = value; }
}
