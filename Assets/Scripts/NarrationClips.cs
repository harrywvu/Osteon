using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class NarrationClips : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        public string text;
        public AudioClip clip;
    }

    [SerializeField] private Entry[] entries;
    private static Dictionary<string, AudioClip> byText;

    internal static AudioClip Find(string text)
    {
        if (byText == null)
        {
            byText = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            var catalog = Resources.Load<NarrationClips>("NarrationClips");
            if (catalog != null)
                foreach (var entry in catalog.entries)
                    if (entry.clip != null) byText.Add(entry.text, entry.clip);
        }

        byText.TryGetValue(text, out var clip);
        return clip;
    }
}
