"""Converts the two final statue GLBs to FBX -- Unity has no built-in glTF
importer (no glTFast/UnityGLTF package installed in this project), so .glb
files import as generic DefaultImporter blobs with no mesh at all. Matches
the same GLB->FBX path already used for Frame_Blender_v1.
Run with: blender.exe --background --python convert_to_fbx.py
"""
import bpy

OUT_DIR = "C:/projectclone/ProjectRULE/RawAssets/Statue"

jobs = [
    (OUT_DIR + "/FullFigure_closed_v1.glb", OUT_DIR + "/Figure1_Standing.fbx"),
    (OUT_DIR + "/FullFigure_turnedLeft_v1.glb", OUT_DIR + "/Figure1_TurnedLeft.fbx"),
]

for src, dst in jobs:
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)

    bpy.ops.import_scene.gltf(filepath=src)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=dst,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        embed_textures=True,
        path_mode='COPY',
    )
    print(f"DONE: {src} -> {dst}")
