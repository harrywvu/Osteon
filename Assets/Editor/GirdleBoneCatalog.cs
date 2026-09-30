using System.Collections.Generic;

/// <summary>Introductory information for the selectable girdle meshes.</summary>
public static class GirdleBoneCatalog
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

    public static IEnumerable<Entry> Pectoral(string side)
    {
        yield return new Entry("Clavicle", side + " clavicle — Collarbone",
            "Location: across the upper chest, between the sternum and shoulder.\n" +
            "Role: braces the shoulder away from the trunk and transfers forces from the arm.\n" +
            "Look for: the slender curved shaft.");
        yield return new Entry("Scapula", side + " scapula — Shoulder blade",
            "Location: upper back, behind the rib cage.\n" +
            "Role: anchors shoulder muscles and forms the shoulder socket for the humerus.\n" +
            "Look for: the broad triangular blade and projecting spine.");
    }

    public static IEnumerable<Entry> Pelvic()
    {
        yield return new Entry("Hip_L_2", "Left hip bone — Os coxae",
            "Location: left side of the pelvis, between the sacrum and thigh.\n" +
            "Role: transfers body weight to the lower limb and forms the hip socket.\n" +
            "Look for: the broad iliac wing and cup-shaped acetabulum.");
    }
}
