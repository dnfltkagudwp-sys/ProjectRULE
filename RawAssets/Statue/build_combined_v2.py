"""Headless Blender script: combines the VARGO body (Figure1_test_v1.glb)
with the NEW VARGO head (Head2_closed_v1.glb, the fully-closed veiled-head
test that came out clean with no broken geometry), instead of the
hand-modeled Blender hood. Both pieces keep their own VARGO-baked textures
since they were generated with matching prompts (pale grey-white stone,
neutral studio lighting) and should already read as one material family
without needing to be stripped and replaced.
Run with: blender.exe --background --python build_combined_v2.py
"""
import bpy
import bmesh
import math
from mathutils import Vector

OUT_DIR = "C:/projectclone/ProjectRULE/RawAssets/Statue"

for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)

# --- import body, trim off its own broken head geometry ---
bpy.ops.import_scene.gltf(filepath=OUT_DIR + "/Figure1_test_v1.glb")
body = [o for o in bpy.data.objects if o.type == "MESH"][0]
body.name = "Body"

mw = body.matrix_world
bbox = [mw @ Vector(c) for c in body.bound_box]
min_z = min(v.z for v in bbox)
max_z = max(v.z for v in bbox)
center_x = sum(v.x for v in bbox) / 8
center_y = sum(v.y for v in bbox) / 8
body_height_full = max_z - min_z
cut_z = min_z + body_height_full * 0.85
print(f"BODY min_z={min_z:.4f} max_z={max_z:.4f} height={body_height_full:.4f} cut_z={cut_z:.4f}")

band_lo, band_hi = cut_z - body_height_full * 0.05, cut_z
band_verts = [mw @ v.co for v in body.data.vertices if band_lo <= (mw @ v.co).z <= band_hi]
neck_radius = max(((v.x - center_x) ** 2 + (v.y - center_y) ** 2) ** 0.5 for v in band_verts) if band_verts else 0.15
print(f"neck_radius measured: {neck_radius:.4f} from {len(band_verts)} verts")

bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode="EDIT")
bm_body = bmesh.from_edit_mesh(body.data)
bmesh.ops.bisect_plane(
    bm_body, geom=list(bm_body.verts) + list(bm_body.edges) + list(bm_body.faces),
    plane_co=(0, 0, cut_z), plane_no=(0, 0, 1), clear_outer=True,
)
bmesh.update_edit_mesh(body.data)
bpy.ops.object.mode_set(mode="OBJECT")
neckline_z = cut_z

# --- import the new closed-hood head, measure its own bottom radius ---
bpy.ops.import_scene.gltf(filepath=OUT_DIR + "/Head2_closed_v1.glb")
head = [o for o in bpy.data.objects if o.type == "MESH" and o.name != body.name][0]
head.name = "Head"

hmw = head.matrix_world
hbbox = [hmw @ Vector(c) for c in head.bound_box]
h_min_z = min(v.z for v in hbbox)
h_max_z = max(v.z for v in hbbox)
h_center_x = sum(v.x for v in hbbox) / 8
h_center_y = sum(v.y for v in hbbox) / 8
head_height = h_max_z - h_min_z
print(f"HEAD min_z={h_min_z:.4f} max_z={h_max_z:.4f} height={head_height:.4f}")

# radius of the head mesh's own bottom rim (where it was cropped in the
# reference photo), measured the same way as the body's neckline
h_band_lo, h_band_hi = h_min_z, h_min_z + head_height * 0.05
h_band_verts = [hmw @ v.co for v in head.data.vertices if h_band_lo <= (hmw @ v.co).z <= h_band_hi]
head_bottom_radius = max(((v.x - h_center_x) ** 2 + (v.y - h_center_y) ** 2) ** 0.5 for v in h_band_verts) if h_band_verts else 0.15
print(f"head_bottom_radius measured: {head_bottom_radius:.4f} from {len(h_band_verts)} verts")

# Scale the head uniformly so its own bottom radius matches the body's
# measured neck radius (with a small margin so it overlaps rather than
# leaving a gap), then place it so that scaled bottom sits just below the
# neckline for a solid overlap.
scale_factor = (neck_radius * 1.12) / head_bottom_radius
print(f"scale_factor: {scale_factor:.4f}")

head.scale = (scale_factor, scale_factor, scale_factor)
bpy.context.view_layer.update()

hmw2 = head.matrix_world
hbbox2 = [hmw2 @ Vector(c) for c in head.bound_box]
new_h_min_z = min(v.z for v in hbbox2)
new_h_center_x = sum(v.x for v in hbbox2) / 8
new_h_center_y = sum(v.y for v in hbbox2) / 8

overlap = (h_max_z - h_min_z) * scale_factor * 0.12
target_bottom_z = neckline_z - overlap
delta_z = target_bottom_z - new_h_min_z
delta_x = center_x - new_h_center_x
delta_y = center_y - new_h_center_y

head.location.x += delta_x
head.location.y += delta_y
head.location.z += delta_z
bpy.context.view_layer.update()

light_data = bpy.data.lights.new("KeyLight", type="SUN")
light_data.energy = 3.0
light_obj = bpy.data.objects.new("KeyLight", light_data)
bpy.context.collection.objects.link(light_obj)
light_obj.rotation_euler = (math.radians(55), 0, math.radians(35))

bpy.ops.export_scene.gltf(
    filepath=OUT_DIR + "/Combined_v2.glb",
    export_format="GLB",
    use_selection=False,
    export_apply=True,
    export_yup=True,
)
bpy.ops.wm.save_as_mainfile(filepath=OUT_DIR + "/Combined_v2.blend")
print("DONE: exported Combined_v2.glb")
