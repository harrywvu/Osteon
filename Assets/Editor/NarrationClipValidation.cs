using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class NarrationClipValidation
{
    [MenuItem("Tools/Osteon/Validate Narration Clips")]
    public static void Validate()
    {
        var catalog = Resources.Load<NarrationClips>("NarrationClips");
        if (catalog == null) throw new InvalidOperationException("Missing narration catalog.");

        var entries = new SerializedObject(catalog).FindProperty("entries");
        var texts = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            string text = entry.FindPropertyRelative("text").stringValue;
            var clip = entry.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
            if (string.IsNullOrWhiteSpace(text) || !texts.Add(text) || clip == null || clip.samples <= 0)
                throw new InvalidOperationException($"Invalid narration entry {i}: {text}");
        }

        if (texts.Count == 0) throw new InvalidOperationException("Narration catalog is empty.");
        Debug.Log($"Validated {texts.Count} narration entries and imported audio clips.");
    }
}
