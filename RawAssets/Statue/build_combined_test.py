"""Headless Blender script: tests whether the VARGO-generated body
(Figure1_test_v1.glb) and the Blender-authored hood can read as ONE coherent
sculpture once both are stripped of their own materials and given the exact
same plain stone shader -- directly testing the concern that a photoreal
AI-baked body texture next to a flat procedural head will look like two
different things glued together, rather than just further polishing the
head shape in isolation.
Run with: blender.exe --background --python build_combined_test.py
"""
import bpy
import bmesh
import math
import random

OUT_DIR = "C:/projectclone/ProjectRULE/RawAssets/Statue"


def make_normal_map(name, size=128, strength=2.2):
    """A baked (real pixel) noise normal map -- Blender's procedural Noise/Bump
    nodes are NOT supported by glTF export (glTF only carries actual image
    textures), so a purely procedural rough-stone shader looks perfectly
    smooth once exported to GLB. Baking real pixels here is what actually
    survives into the .glb (and later into Unity)."""
    img = bpy.data.images.new(name, width=size, height=size, alpha=False)
    height = [[random.random() for _ in range(size)] for _ in range(size)]

    def blur(h):
        out = [[0.0] * size for _ in range(size)]
        for y in range(size):
            for x in range(size):
                s = 0.0
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        s += h[(y + dy) % size][(x + dx) % size]
                out[y][x] = s / 9.0
        return out

    for _ in range(2):
        height = blur(height)

    pixels = [0.0] * (size * size * 4)
    for y in range(size):
        for x in range(size):
            hL = height[y][(x - 1) % size]
            hR = height[y][(x + 1) % size]
            hD = height[(y - 1) % size][x]
            hU = height[(y + 1) % size][x]
            nx = (hL - hR) * strength
            ny = (hD - hU) * strength
            nz = 1.0
            length = (nx * nx + ny * ny + nz * nz) ** 0.5
            nx, ny, nz = nx / length, ny / length, nz / length
            idx = (y * size + x) * 4
            pixels[idx + 0] = nx * 0.5 + 0.5
            pixels[idx + 1] = ny * 0.5 + 0.5
            pixels[idx + 2] = nz * 0.5 + 0.5
            pixels[idx + 3] = 1.0
    img.pixels = pixels
    img.update()
    img.pack()
    return img

for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
for mat in list(bpy.data.materials):
    bpy.data.materials.remove(mat)

# Shared stone material -- the whole point of this test is that BOTH pieces
# use this one material instead of the body keeping its own AI-baked texture.
stone_mat = bpy.data.materials.new("M_Statue_Stone")
stone_mat.use_nodes = True
bsdf = stone_mat.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Base Color"].default_value = (0.78, 0.77, 0.74, 1.0)
bsdf.inputs["Roughness"].default_value = 0.85

normal_img = make_normal_map("StoneNormal")

tex_coord = stone_mat.node_tree.nodes.new("ShaderNodeTexCoord")
mapping = stone_mat.node_tree.nodes.new("ShaderNodeMapping")
mapping.inputs["Scale"].default_value = (40.0, 40.0, 40.0)
img_tex = stone_mat.node_tree.nodes.new("ShaderNodeTexImage")
img_tex.image = normal_img
img_tex.interpolation = "Cubic"
try:
    img_tex.image.colorspace_settings.name = "Non-Color"
except Exception:
    pass
normal_map_node = stone_mat.node_tree.nodes.new("ShaderNodeNormalMap")
normal_map_node.inputs["Strength"].default_value = 1.4

stone_mat.node_tree.links.new(tex_coord.outputs["UV"], mapping.inputs["Vector"])
stone_mat.node_tree.links.new(mapping.outputs["Vector"], img_tex.inputs["Vector"])
stone_mat.node_tree.links.new(img_tex.outputs["Color"], normal_map_node.inputs["Color"])
stone_mat.node_tree.links.new(normal_map_node.outputs["Normal"], bsdf.inputs["Normal"])

# --- import the VARGO body and strip its own baked material ---
bpy.ops.import_scene.gltf(filepath=OUT_DIR + "/Figure1_test_v1.glb")
body_objs = [o for o in bpy.context.selected_objects if o.type == "MESH"]
if not body_objs:
    body_objs = [o for o in bpy.data.objects if o.type == "MESH"]
body = body_objs[0]
body.name = "Body"
body.data.materials.clear()
body.data.materials.append(stone_mat)

print("BODY bounds:", body.dimensions, "loc:", body.location)
bbox = [body.matrix_world @ __import__("mathutils").Vector(c) for c in body.bound_box]
min_z = min(v.z for v in bbox)
max_z = max(v.z for v in bbox)
center_x = sum(v.x for v in bbox) / 8
center_y = sum(v.y for v in bbox) / 8
print("BODY min_z:", min_z, "max_z:", max_z, "center_x:", center_x, "center_y:", center_y)

# The imported mesh still includes VARGO's own broken hood/head geometry on
# top of the clean body drape. Bisect it off at 85% of body height (removing
# only the top ~15%, which is where the broken geometry actually lives per
# visual inspection) so the new Blender hood attaches to the actual
# neckline/shoulder line instead of floating above -- or cutting into -- the
# old broken mass.
body_height_full = max_z - min_z
cut_z = min_z + body_height_full * 0.85

# Measure the body's actual radius just below the cut line so the new hood's
# hem can be scaled to fully cover it -- a fixed hood/body scale ratio left a
# visible flat rim of the trimmed body sticking out past a too-narrow hem.
mw = body.matrix_world
band_lo, band_hi = cut_z - body_height_full * 0.05, cut_z
band_verts = [mw @ v.co for v in body.data.vertices if band_lo <= (mw @ v.co).z <= band_hi]
neck_radius = max(((v.x - center_x) ** 2 + (v.y - center_y) ** 2) ** 0.5 for v in band_verts) if band_verts else 0.15
print("neck_radius measured:", neck_radius, "from", len(band_verts), "verts")

bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode="EDIT")
bm_body = bmesh.from_edit_mesh(body.data)
bmesh.ops.bisect_plane(
    bm_body, geom=list(bm_body.verts) + list(bm_body.edges) + list(bm_body.faces),
    plane_co=(0, 0, cut_z), plane_no=(0, 0, 1), clear_outer=True,
)
bmesh.update_edit_mesh(body.data)
bpy.ops.object.mode_set(mode="OBJECT")

max_z = cut_z
print("BODY trimmed, new max_z (neckline):", max_z)

# --- build the hood profile (same as build_hood.py), sized so its own hem
# radius (0.26 in local units) matches the measured neck radius with a 15%
# margin so it fully covers the trimmed edge, and scaled taller/shorter
# independently via a separate height factor ---
body_height = max_z - min_z
hood_hem_local_radius = 0.26
hood_scale_xy = (neck_radius * 1.15) / hood_hem_local_radius
hood_scale_z = body_height_full * 0.40
print("hood_scale_xy:", hood_scale_xy, "hood_scale_z:", hood_scale_z)

profile = [
    (0.0, 0.62), (0.05, 0.615), (0.10, 0.59), (0.145, 0.53), (0.17, 0.45),
    (0.175, 0.36), (0.16, 0.28), (0.15, 0.20), (0.165, 0.12), (0.21, 0.04),
    (0.26, 0.0),
]
mesh = bpy.data.meshes.new("Hood")
hood = bpy.data.objects.new("Hood", mesh)
bpy.context.collection.objects.link(hood)

bm = bmesh.new()
profile_verts = [bm.verts.new((x, 0.0, z)) for x, z in profile]
for a, b in zip(profile_verts, profile_verts[1:]):
    bm.edges.new((a, b))
bmesh.ops.spin(bm, geom=list(bm.verts) + list(bm.edges), axis=(0, 0, 1),
                cent=(0, 0, 0), angle=math.radians(360), steps=40, use_duplicate=False)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(mesh)
bm.free()

bpy.context.view_layer.objects.active = hood
hood.select_set(True)

bend = hood.modifiers.new("Bend", "SIMPLE_DEFORM")
bend.deform_method = "BEND"
bend.deform_axis = "X"
bend.angle = math.radians(20)
bend.limits = (0.55, 1.0)
bpy.ops.object.modifier_apply(modifier="Bend")

bevel = hood.modifiers.new("Bevel", "BEVEL")
bevel.width = 0.008
bevel.segments = 3
bpy.ops.object.modifier_apply(modifier="Bevel")

subsurf = hood.modifiers.new("Subsurf", "SUBSURF")
subsurf.levels = 2
subsurf.render_levels = 2
bpy.ops.object.modifier_apply(modifier="Subsurf")

# Rounder face-cavity cutter (sphere-ish, not a flat-faced cube) so the gap
# reads as a soft shadowed hollow instead of a cut window.
bpy.ops.mesh.primitive_uv_sphere_add(
    radius=0.14, segments=20, ring_count=14,
    location=(0, -0.16, 0.24)
)
cutter = bpy.context.active_object
cutter.scale = (1.0, 0.85, 1.35)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
cutter.name = "FaceCavityCutter"

boolean = hood.modifiers.new("FaceCavity", "BOOLEAN")
boolean.operation = "DIFFERENCE"
boolean.object = cutter
bpy.context.view_layer.objects.active = hood
bpy.ops.object.modifier_apply(modifier="FaceCavity")
bpy.data.objects.remove(cutter, do_unlink=True)

bpy.ops.object.mode_set(mode="EDIT")
bm = bmesh.from_edit_mesh(hood.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.update_edit_mesh(hood.data)
bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.shade_smooth()

# The hood mesh was built from scratch via bmesh -- it has no UV map yet,
# and an image-textured material (needed so the normal map survives glTF
# export) requires one to sample from.
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project()
bpy.ops.object.mode_set(mode="OBJECT")

hood.data.materials.append(stone_mat)

if not body.data.uv_layers:
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project()
    bpy.ops.object.mode_set(mode="OBJECT")

# Scale and place the hood so its hem sits right at (and fully covers) the
# body's trimmed neckline.
hood.scale = (hood_scale_xy, hood_scale_xy, hood_scale_z)
hood.location = (center_x, center_y, max_z - hood_scale_z * 0.12)
bpy.context.view_layer.update()

light_data = bpy.data.lights.new("KeyLight", type="SUN")
light_data.energy = 3.0
light_obj = bpy.data.objects.new("KeyLight", light_data)
bpy.context.collection.objects.link(light_obj)
light_obj.rotation_euler = (math.radians(55), 0, math.radians(35))

bpy.ops.export_scene.gltf(
    filepath=OUT_DIR + "/Combined_test_v1.glb",
    export_format="GLB",
    use_selection=False,
    export_apply=True,
    export_yup=True,
)
bpy.ops.wm.save_as_mainfile(filepath=OUT_DIR + "/Combined_test_v1.blend")
print("DONE: exported Combined_test_v1.glb")
