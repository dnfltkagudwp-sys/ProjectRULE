"""Headless Blender script: procedurally models a bowed, hollow-faced hood to
attach onto the VARGO-generated draped body (Figure1_test_v1.glb), since
Generate3D's image-to-3D pass produced broken/torn geometry for the hood
specifically (confirmed by direct visual inspection), while the body drape
came out clean. Run with: blender.exe --background --python build_hood.py
"""
import bpy
import bmesh
import math

OUT_DIR = "C:/projectclone/ProjectRULE/RawAssets/Statue"

for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)

# Build the hood by revolving a hand-authored silhouette profile (in the X-Z
# half-plane, X = radius from the vertical axis, Z = height) around the
# vertical axis. This gives direct control over the shape -- a rounded dome
# top that necks in slightly then flares out to a wide draped hem -- instead
# of guessing cone+modifier parameters blindly.
profile = [
    (0.0, 0.62),
    (0.05, 0.615),
    (0.10, 0.59),
    (0.145, 0.53),
    (0.17, 0.45),
    (0.175, 0.36),
    (0.16, 0.28),
    (0.15, 0.20),
    (0.165, 0.12),
    (0.21, 0.04),
    (0.26, 0.0),
]

mesh = bpy.data.meshes.new("Hood")
hood = bpy.data.objects.new("Hood", mesh)
bpy.context.collection.objects.link(hood)

bm = bmesh.new()
profile_verts = [bm.verts.new((x, 0.0, z)) for x, z in profile]
for a, b in zip(profile_verts, profile_verts[1:]):
    bm.edges.new((a, b))

spin_result = bmesh.ops.spin(
    bm, geom=list(bm.verts) + list(bm.edges), axis=(0, 0, 1),
    cent=(0, 0, 0), angle=math.radians(360), steps=40, use_duplicate=False,
)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

bm.to_mesh(mesh)
bm.free()

bpy.context.view_layer.objects.active = hood
hood.select_set(True)

# Only the upper ~40% bends forward (like a cowl hanging straight from the
# shoulders that curls forward over a bowed head) -- limiting the deform
# range keeps the hem vertical instead of tipping the whole shape sideways.
bend = hood.modifiers.new("Bend", "SIMPLE_DEFORM")
bend.deform_method = "BEND"
bend.deform_axis = "Y"
bend.angle = math.radians(22)
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

# Face-cavity cutter: a wedge-shaped cube poking into the lower-front of the
# now-bowed hood, angled to match the forward bend, sized to be a clearly
# visible dark gap (roughly a third of the hood's front face) rather than a
# faint dimple.
bpy.ops.mesh.primitive_cube_add(
    size=1.0, location=(0, -0.13, 0.22),
    rotation=(math.radians(60), 0, 0)
)
cutter = bpy.context.active_object
cutter.scale = (0.11, 0.09, 0.26)
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

mat = bpy.data.materials.new("M_Statue_Stone")
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Base Color"].default_value = (0.78, 0.77, 0.74, 1.0)
bsdf.inputs["Roughness"].default_value = 0.85
hood.data.materials.append(mat)

light_data = bpy.data.lights.new("KeyLight", type="SUN")
light_data.energy = 3.0
light_obj = bpy.data.objects.new("KeyLight", light_data)
bpy.context.collection.objects.link(light_obj)
light_obj.rotation_euler = (math.radians(55), 0, math.radians(35))

print("PRE-EXPORT bounds:", hood.dimensions, "loc:", hood.location)

bpy.ops.export_scene.gltf(
    filepath=OUT_DIR + "/Hood_test_v1.glb",
    export_format="GLB",
    use_selection=False,
    export_apply=True,
    export_yup=True,
)

bpy.ops.wm.save_as_mainfile(filepath=OUT_DIR + "/Hood_test_v1.blend")

print("DONE: exported Hood_test_v1.glb")
