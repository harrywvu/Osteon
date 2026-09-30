# Add a G1 bone group

The G1-to-G2 setup lives on the G1 group object's `ViewTransitionOnSelect`
component. Navigation discovers these components under the axial and
appendicular G1 views at startup. Ribcage is a working example in the enabled
scene.

1. Put the new G2 model in the scene as a separate, inactive root. Keep it
   outside the G1 division hierarchy so navigation can show and hide it
   independently.
2. On the matching G1 group object, add `ViewTransitionOnSelect`. Assign
   **Next View** to that G2 root. Set **Next Title** and **Next Description** for
   the information panel. The title defaults to the G1 group object's name.
3. Leave **Interactable Root** empty to use the group object and leave **Auto
   Create Mesh Targets** enabled. At runtime, rendered mesh children receive
   mesh colliders and `XRSimpleInteractable` ray targets if needed. The group
   also receives hover highlighting. The highlight material comes from
   `AnatomyNavigationController`; **Highlight Material** can override it for
   one group.
4. Enter Play Mode. Point at the G1 group: all its rendered meshes should
   highlight. Select it: its G2 model and information should appear. Use Back
   to restore the G1 division, then Back again for the whole skeleton.

Existing authored ray targets can be retained: turn off **Auto Create Mesh
Targets** and set **Interactable Root** to their hierarchy. The vertebral
column uses this option. The group needs at least one enabled XRI interactable
with a collider to receive selection.

No per-mesh collider setup, separate highlighter component, or navigation view
array edit is needed for a new group. G3 individual-bone inspection currently
applies to the vertebral column, ribcage, both lower limbs, both upper limbs,
both pectoral sides, and pelvic girdle. Adding a G2 view does not create
G3 entries: each selectable bone needs `BonePartInfo`, `BoneSelection`, an
`XRSimpleInteractable`, a mesh collider, and a navigation binding.
