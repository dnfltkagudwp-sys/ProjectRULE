"""
Builds the shared museum picture-frame asset directly in Blender (bpy), run headlessly.

Closed box, no hole. The front face is split (via two inset passes) into three material
regions: outer wood border, thin gold trim, and a flat inner canvas face with clean 0-1 UVs
so a portrait/landscape/abstract painting texture can be dropped straight onto it later.

Modeled with Blender's native Z-up convention (X=width, Y=depth/thickness, Z=height) so the
glTF/FBX exporters' automatic Z-up -> Y-up conversion lands the frame upright and correctly
oriented once imported into Unity, matching the graybox painting placeholders
(scale = width x height x thickness).
"""

import bpy
import bmesh
import os

WIDTH = 1.2   # matches LobbyGrayboxBuilder paintW
HEIGHT = 1.6  # matches LobbyGrayboxBuilder paintH
DEPTH = 0.05  # matches LobbyGrayboxBuilder paintT
BORDER = 0.10  # outer wood border width
TRIM = 0.02    # thin inner gold trim width

OUT_DIR = os.path.dirname(os.path.abspath(__file__))


def make_material(name, color, roughness=0.6, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in list(bpy.data.materials):
        if block.users == 0:
            bpy.data.materials.remove(block)


def build_frame():
    clear_scene()

    mat_wood = make_material("M_Frame_Wood", (0.12, 0.07, 0.045), roughness=0.55, metallic=0.0)
    mat_trim = make_material("M_Frame_Trim", (0.62, 0.49, 0.18), roughness=0.3, metallic=0.8)
    mat_canvas = make_material("M_Frame_Canvas", (0.92, 0.92, 0.90), roughness=0.9, metallic=0.0)

    mesh = bpy.data.meshes.new("Frame")
    obj = bpy.data.objects.new("Frame", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat_wood)    # index 0
    obj.data.materials.append(mat_trim)    # index 1
    obj.data.materials.append(mat_canvas)  # index 2

    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, verts=bm.verts, vec=(WIDTH, DEPTH, HEIGHT))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    # Front face = the one facing +Y (depth axis), i.e. the face pointing away from the wall.
    front_face = max(bm.faces, key=lambda f: f.normal.y)

    outer = bmesh.ops.inset_region(bm, faces=[front_face], thickness=BORDER, use_even_offset=True)
    border_faces = outer['faces']

    trim = bmesh.ops.inset_region(bm, faces=[front_face], thickness=TRIM, use_even_offset=True)
    trim_faces = trim['faces']

    # front_face itself is now the shrunken canvas rectangle.
    for f in bm.faces:
        if f is front_face:
            f.material_index = 2
        elif f in border_faces or f in trim_faces:
            f.material_index = 1 if f in trim_faces else 0
        else:
            f.material_index = 0  # back + side faces reuse the wood material

    # Clean 0..1 UVs on the canvas face only (the only face a texture will ever go on).
    uv_layer = bm.loops.layers.uv.new("UVMap")
    half_w = WIDTH / 2.0 - BORDER - TRIM
    half_h = HEIGHT / 2.0 - BORDER - TRIM
    for loop in front_face.loops:
        v = loop.vert.co
        u = (v.x + half_w) / (2 * half_w)
        w = (v.z + half_h) / (2 * half_h)
        loop[uv_layer].uv = (u, w)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    return obj


def export(obj):
    glb_path = os.path.join(OUT_DIR, "Frame_Blender_v1.glb")
    fbx_path = os.path.join(OUT_DIR, "Frame_Blender_v1.fbx")
    blend_path = os.path.join(OUT_DIR, "Frame_Blender_v1.blend")

    bpy.ops.export_scene.gltf(filepath=glb_path, export_format='GLB', use_selection=True)
    bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, apply_unit_scale=True,
                              axis_forward='-Z', axis_up='Y')
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)

    print(f"[build_frame] Exported: {glb_path}")
    print(f"[build_frame] Exported: {fbx_path}")
    print(f"[build_frame] Saved: {blend_path}")


frame_obj = build_frame()
export(frame_obj)
