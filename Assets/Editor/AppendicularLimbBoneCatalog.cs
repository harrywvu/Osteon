using System.Collections.Generic;

/// <summary>Information for the imported right lower and paired upper-limb models.</summary>
public static class AppendicularLimbBoneCatalog
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

    public static IEnumerable<Entry> RightLower()
    {
        foreach (var left in LeftLowerLimbBoneCatalog.All())
            yield return new Entry(left.meshName, left.title.Replace("Left", "Right"),
                left.description.Replace("left", "right"));
        foreach (string position in new[] { "Medial", "Lateral" })
            yield return new Entry(position + " sesamoid", "Right " + position.ToLowerInvariant() + " sesamoid",
                "Location: beneath the big-toe joint, near the first metatarsal.\n" +
                "Role: a small bone within a tendon that helps protect it and guide movement.\n" +
                "Look for: the small rounded shape under the forefoot.");
    }

    public static IEnumerable<Entry> Upper(string side)
    {
        yield return new Entry("Humerus", side + " humerus — Upper arm",
            "Location: upper arm, between shoulder and elbow.\n" +
            "Role: forms the shoulder and elbow joints.\nLook for: the rounded head and broad lower end.");
        yield return new Entry("Radius", side + " radius",
            "Location: thumb side of the forearm.\n" +
            "Role: rotates around the ulna and contributes to the wrist joint.\n" +
            "Look for: its small rounded head near the elbow.");
        yield return new Entry("Ulna", side + " ulna",
            "Location: little-finger side of the forearm.\n" +
            "Role: forms the main hinge of the elbow.\n" +
            "Look for: the prominent upper end that forms the elbow point.");
        foreach (string name in new[] { "Scaphoid", "Lunate", "Triquetrum", "Pisiform",
                     "Trapezium", "Trapezoid", "Capitate", "Hamate" })
            yield return new Entry(name, side + " " + name.ToLowerInvariant(),
                "Location: wrist, between the forearm and palm.\n" +
                "Role: one of eight carpal bones that give the wrist its flexibility.\n" +
                "Look for: its position among the two rows of wrist bones.");
        for (int number = 1; number <= 5; number++)
            yield return new Entry("Metacarpal " + number, side + " metacarpal " + number,
                "Location: palm, aligned with digit " + number + "; numbering begins at the thumb.\n" +
                "Role: links the wrist bones to the fingers and helps form the knuckles.");
        foreach (string digit in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            foreach (string segment in digit == "Thumb" ? new[] { "proximal", "distal" } :
                         new[] { "proximal", "middle", "distal" })
                yield return new Entry(digit + " " + segment + " phalanx",
                    side + " " + digit.ToLowerInvariant() + " — " + segment + " phalanx",
                    "Location: " + segment + " segment of the " + digit.ToLowerInvariant() + ".\n" +
                    "Role: forms part of the digit for grasping and fine hand movement.");
    }
}
