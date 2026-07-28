using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StepManager : MonoBehaviour
{
    [Header("UI Elements")]
    public List<GameObject> cards;       // Each bone info card
    public Button nextButton;
    public Button backButton;

    [Header("Bones")]
    public List<GameObject> bones;       // Each bone in order
    public Material highlightMaterial;   // Material for highlighting

    private int currentIndex = 0;
    private List<List<Material>> originalMaterials = new List<List<Material>>();

    void Start()
    {
        // Store original materials for each bone and its children
        foreach (var bone in bones)
        {
            var rends = bone.GetComponentsInChildren<Renderer>();
            var boneMats = new List<Material>();

            foreach (var rend in rends)
            {
                boneMats.Add(rend.material);
            }

            originalMaterials.Add(boneMats);
        }

        // Add button listeners
        nextButton.onClick.AddListener(NextCard);
        backButton.onClick.AddListener(PreviousCard);

        // Start with the first card
        ShowCard(0);
    }

    public void ShowCard(int index)
    {
        if (cards.Count == 0 || bones.Count == 0) return;

        // Hide all cards first
        foreach (var card in cards)
            card.SetActive(false);

        // Reset all bone highlights
        for (int i = 0; i < bones.Count; i++)
        {
            var rends = bones[i].GetComponentsInChildren<Renderer>();
            for (int j = 0; j < rends.Length; j++)
            {
                rends[j].material = originalMaterials[i][j];
            }
        }

        // Clamp index
        currentIndex = Mathf.Clamp(index, 0, cards.Count - 1);

        // Show the current card
        cards[currentIndex].SetActive(true);

        // Highlight the corresponding bone
        if (currentIndex < bones.Count)
        {
            var rends = bones[currentIndex].GetComponentsInChildren<Renderer>();
            foreach (var rend in rends)
            {
                rend.material = highlightMaterial;
            }
        }
    }

    public void NextCard()
    {
        int nextIndex = currentIndex + 1;
        if (nextIndex >= cards.Count)
            nextIndex = 0; // Loop back to first card
        ShowCard(nextIndex);
    }

    public void PreviousCard()
    {
        int prevIndex = currentIndex - 1;
        if (prevIndex < 0)
            prevIndex = cards.Count - 1; // Loop to last card
        ShowCard(prevIndex);
    }
}
