using System.Collections.Generic;

/// <summary>Introductory information for the 30 bones in the imported left lower-limb model.</summary>
public static class LeftLowerLimbBoneCatalog
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
        yield return new Entry("Femur", "Left femur — Thigh bone",
            "Location: thigh, between the hip and knee.\nRole: supports body weight and forms the hip and knee joints.\nLook for: the rounded head and broad lower end.");
        yield return new Entry("Patella", "Left patella — Kneecap",
            "Location: front of the knee.\nRole: protects the tendon at the knee and helps the thigh muscles extend the leg.\nLook for: its small, roughly triangular shape.");
        yield return new Entry("Tibia", "Left tibia — Shinbone",
            "Location: medial side of the leg, between knee and ankle.\nRole: bears most of the leg's weight.\nLook for: its broad upper end and inner ankle projection.");
        yield return new Entry("Fibula", "Left fibula",
            "Location: lateral side of the leg, beside the tibia.\nRole: helps stabilize the ankle and provides muscle attachments.\nLook for: its slender shaft and outer ankle projection.");

        yield return new Entry("Talus", "Left talus",
            "Location: upper rear foot, below the tibia and fibula.\nRole: forms the ankle joint and passes weight toward the heel.\nLook for: its joint surfaces above the calcaneus.");
        yield return new Entry("Calcaneus", "Left calcaneus — Heel bone",
            "Location: back and underside of the foot.\nRole: forms the heel and supports the talus.\nLook for: the large heel projection.");
        yield return new Entry("Navicular", "Left navicular",
            "Location: medial midfoot, in front of the talus.\nRole: links the talus to the cuneiform bones.\nLook for: its curved, boat-like outline.");
        yield return new Entry("Cuboid", "Left cuboid",
            "Location: lateral midfoot, in front of the calcaneus.\nRole: links the heel region to the outer metatarsals.\nLook for: its block-like shape.");
        foreach (string position in new[] { "Medial", "Intermediate", "Lateral" })
            yield return new Entry($"{position} cuneiform", $"Left {position.ToLowerInvariant()} cuneiform",
                $"Location: {position.ToLowerInvariant()} part of the midfoot, between the navicular and metatarsals.\n" +
                "Role: helps connect the rear foot to the forefoot and supports the foot's arches.");

        for (int number = 1; number <= 5; number++)
            yield return new Entry($"Metatarsal {number}", $"Left metatarsal {number}",
                $"Location: forefoot, in line with toe {number}; numbering begins at the big toe.\n" +
                "Role: links the tarsal bones to the toes and helps form the ball of the foot.");

        for (int toe = 1; toe <= 5; toe++)
        {
            foreach (string segment in toe == 1 ? new[] { "proximal", "distal" } :
                         new[] { "proximal", "middle", "distal" })
                yield return new Entry($"Toe {toe} {segment} phalanx",
                    $"Left toe {toe} — {segment} phalanx",
                    $"Location: {segment} segment of left toe {toe}.\n" +
                    "Role: forms part of the toe and helps the foot balance and push off during movement.");
        }
    }
}
