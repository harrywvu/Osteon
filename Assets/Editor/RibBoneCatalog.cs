using System.Collections.Generic;

/// <summary>Introductory information for the 24 ribs and sternum in the imported ribcage model.</summary>
public static class RibBoneCatalog
{
    public struct Entry
    {
        public string meshName;
        public string title;
        public string description;

        public Entry(string meshName, string title, string description)
        {
            this.meshName = meshName;
            this.title = title;
            this.description = description;
        }
    }

    public static IEnumerable<Entry> All()
    {
        foreach (string side in new[] { "L", "R" })
        {
            string sideName = side == "L" ? "Left" : "Right";
            for (int number = 1; number <= 12; number++)
            {
                string ordinal = number == 1 ? "1st" : number == 2 ? "2nd" : number == 3 ? "3rd" :
                    number + "th";
                string category = number <= 7 ? "true" : number <= 10 ? "false" : "floating";
                string connection = number <= 7 ? "Its costal cartilage connects directly to the sternum." :
                    number <= 10 ? "Its costal cartilage connects indirectly to the sternum through the cartilage above." :
                    "Its anterior end does not attach to the sternum.";
                yield return new Entry($"{side} {ordinal} rib ({category})", $"{sideName} rib {number}",
                    $"Location: {sideName.ToLowerInvariant()} side of the chest, rib pair {number} of 12.\n" +
                    $"Role: part of the thoracic cage that helps protect the heart and lungs.\n" +
                    $"Connection: {connection}");
            }
        }
        yield return new Entry("Sternum (Breastbone)", "Sternum — Breastbone",
            "Location: center of the front of the chest.\n" +
            "Role: anchors the anterior thoracic cage and connects to the costal cartilages of the true ribs.\n" +
            "Look for: the broad upper manubrium, long body, and lower xiphoid process.");
    }
}
