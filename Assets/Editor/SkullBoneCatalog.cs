using System.Collections.Generic;

/// <summary>The named bones in the imported skull FBX; teeth are context meshes.</summary>
public static class SkullBoneCatalog
{
    public struct Entry
    {
        public readonly string meshName;
        public readonly string title;
        public readonly string description;

        public Entry(string meshName, string title, string description)
        {
            this.meshName = meshName;
            this.title = title;
            this.description = description;
        }
    }

    public static IEnumerable<Entry> All()
    {
        yield return Bone("Ethmoid", "between the eyes, behind the nasal cavity", "forms part of the nasal roof and the eye sockets");
        yield return Bone("Frontal", "the forehead and front of the cranial vault", "protects the frontal lobes and forms the upper eye sockets");
        yield return Bone("Hyoid", "the front of the neck below the mandible", "supports the tongue and larynx without joining another bone directly");
        foreach (string side in new[] { "Left", "Right" })
        {
            yield return Bone(side + " Incus", "the middle ear", "passes sound vibrations from the malleus to the stapes");
            yield return Bone(side + " Inferior Nasal Concha", "the lower side wall of the nasal cavity", "helps warm and humidify inhaled air");
            yield return Bone(side + " Lacrimal", "the inner wall of the eye socket", "provides a channel for tear drainage");
            yield return Bone(side + " Malleus", "the middle ear", "passes sound vibrations from the eardrum to the incus");
            yield return Bone(side + " Maxilla", "the upper jaw", "holds the upper teeth and forms part of the hard palate and eye socket");
            yield return Bone(side + " Nasal", "the bridge of the nose", "supports the upper nose");
            yield return Bone(side + " Palatine", "the back of the hard palate", "separates the mouth from the nasal cavity");
            yield return Bone(side + " Parietal", "the upper side of the cranial vault", "protects the brain");
            yield return Bone(side + " Stapes", "the middle ear", "transmits vibrations into the inner ear");
            yield return Bone(side + " Temporal", "the side and base of the skull", "houses the ear structures and forms part of the jaw joint");
            yield return Bone(side + " Zygomatic", "the cheek and outer eye socket", "forms the cheekbone and supports the eye socket");
        }
        yield return Bone("Mandible", "the lower jaw", "holds the lower teeth and moves during chewing and speech");
        yield return Bone("Occipital", "the back and base of the skull", "protects the rear of the brain and surrounds the opening for the spinal cord");
        yield return Bone("Sphenoid", "the central skull base behind the eyes", "links many skull bones and supports the pituitary gland");
        yield return Bone("Vomer", "the lower part of the nasal septum", "divides the left and right nasal cavities");
    }

    private static Entry Bone(string name, string location, string role) =>
        new Entry(name, name + " bone", "Location: " + location + ".\nRole: " + role + ".");
}
