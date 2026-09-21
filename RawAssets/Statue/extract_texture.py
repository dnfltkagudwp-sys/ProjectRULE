"""Extracts the baked base-color texture from the original VARGO GLB material
so it can be applied as a real Unity Material -- the FBX round-trip through
Blender apparently didn't carry the texture through cleanly (Unity shows a
flat white material with no surface detail at all).
Run with: blender.exe --background --python extract_texture.py
"""
import bpy

OUT_DIR = "C:/projectclone/ProjectRULE/RawAssets/Statue"

for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
for img in list(bpy.data.images):
    bpy.data.images.remove(img)

bpy.ops.import_scene.gltf(filepath=OUT_DIR + "/FullFigure_closed_v1.glb")

found = False
for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    for node in mat.node_tree.nodes:
        if node.type == "TEX_IMAGE" and node.image is not None:
            img = node.image
            safe_node_name = node.name.replace(".", "_").replace(" ", "_")
            out_path = OUT_DIR + f"/Figure1_Standing_{safe_node_name}.png"
            img.filepath_raw = out_path
            img.file_format = "PNG"
            img.save()
            print(f"DONE: extracted texture from material '{mat.name}' node '{node.name}' -> {out_path} "
                  f"({img.size[0]}x{img.size[1]})")
            found = True

if not found:
    print("NO_TEXTURE_FOUND: no image texture node discovered in any material")
