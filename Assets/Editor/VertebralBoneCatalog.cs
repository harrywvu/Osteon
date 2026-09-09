using System.Collections.Generic;

/// <summary>Introductory descriptions; sources and editorial boundaries are in Docs/G3_INSPECTION.md.</summary>
public static class VertebralBoneCatalog
{
    public struct Entry
    {
        public string meshName;
        public string title;
        public string description;
        public Entry(string mesh, string name, string text)
        { meshName = mesh; title = name; description = text; }
    }

    public static IEnumerable<Entry> All()
    {
        yield return new Entry("C1", "C1 — Atlas",
            "Location: top of the neck, between the skull and C2.\nRole: supports the head and contributes to nodding.\nLook for: the bony ring and broad lateral masses; there is no vertebral body.");
        yield return new Entry("C2", "C2 — Axis",
            "Location: below C1 in the upper neck.\nRole: provides the pivot for turning the atlas and head.\nLook for: the upward projection called the dens.");
        for (int i = 3; i <= 6; i++)
            yield return new Entry($"C{i}", $"C{i} — Cervical vertebra",
                $"Location: neck, between C{i - 1} and C{i + 1}.\nRole: supports the neck and allows movement.\nLook for: a small body and openings in the transverse processes.");
        yield return new Entry("C7", "C7 — Cervical vertebra",
            "Location: base of the neck, between C6 and T1.\nRole: joins the neck to the thoracic region.\nLook for: its long spinous process.");
        for (int i = 1; i <= 12; i++)
        {
            string above = i == 1 ? "C7" : $"T{i - 1}";
            string below = i == 12 ? "L1" : $"T{i + 1}";
            string feature = i >= 11 ? "rib articulation on the body; no transverse costal facet." :
                "rib articulation surfaces on the body and transverse processes.";
            yield return new Entry($"T{i}", $"T{i} — Thoracic vertebra",
                $"Location: thoracic region, between {above} and {below}.\nRole: supports the trunk and articulates with ribs.\nLook for: {feature}");
        }
        for (int i = 1; i <= 5; i++)
        {
            string above = i == 1 ? "T12" : $"L{i - 1}";
            string below = i == 5 ? "the sacrum" : $"L{i + 1}";
            yield return new Entry($"L{i}", $"L{i} — Lumbar vertebra",
                $"Location: lower back, between {above} and {below}.\nRole: carries substantial body weight and supports trunk movement.\nLook for: the large vertebral body and broad spinous process.");
        }
        yield return new Entry("Sacrum", "Sacrum",
            "Location: between L5 and the coccyx, at the back of the pelvis.\nRole: transfers weight from the spine to the pelvic girdle.\nLook for: its triangular shape and paired sacral foramina; five vertebrae fuse to form this bone.");
        yield return new Entry("Coccyx", "Coccyx — Tailbone",
            "Location: below the sacrum at the end of the column.\nRole: anchors pelvic-floor muscles and ligaments.\nLook for: the small tapering series of segments. The number and fusion of these segments vary.");
    }
}
