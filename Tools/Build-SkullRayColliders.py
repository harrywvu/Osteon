"""Run with Blender 4.5 in background mode to rebuild skull_ray_colliders.fbx."""

from pathlib import Path

import bpy


model_dir = (
    Path(__file__).resolve().parents[1]
    / "Assets/Art/Models/Skeleton/Mid Poly/Axial Bone Groups"
)
source = model_dir / "skull.fbx"
output = model_dir / "skull_ray_colliders.fbx"

bpy.ops.import_scene.fbx(filepath=str(source))
bones = [
    obj for obj in bpy.data.objects
    if obj.type == "MESH" and obj.name != "Cube"
    and not obj.name.startswith("Context -")
]
if len(bones) != 29:
    raise RuntimeError(f"Expected 29 skull bones, found {len(bones)}")

for obj in list(bpy.data.objects):
    if obj not in bones:
        bpy.data.objects.remove(obj, do_unlink=True)

for bone in bones:
    triangles = len(bone.data.loop_triangles)
    target = min(triangles, max(450, min(4000, round(triangles * 0.05))))
    if target < triangles:
        modifier = bone.modifiers.new("Quest ray collision decimation", "DECIMATE")
        modifier.ratio = target / triangles
        bpy.ops.object.select_all(action="DESELECT")
        bone.select_set(True)
        bpy.context.view_layer.objects.active = bone
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    # Unity imports the original FBX's local mesh coordinates at 1/100 of
    # Blender's reimported coordinates. Scene transforms supply the other scale.
    for vertex in bone.data.vertices:
        vertex.co *= 0.01
    bone.data.materials.clear()
    bone.data.update()

bpy.ops.object.select_all(action="DESELECT")
for bone in bones:
    bone.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=str(output),
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    bake_anim=False,
    add_leaf_bones=False,
)
print(f"Wrote {len(bones)} skull ray meshes to {output}")
