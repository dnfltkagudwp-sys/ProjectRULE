"""Prints the body mesh's horizontal extent at several height slices, to find
where the neck/shoulder line actually is instead of guessing a flat height
fraction (the last combined test cut the trim plane wrong -- either through
the natural shoulder taper or leaving the broken head mostly intact)."""
import bpy

for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)

bpy.ops.import_scene.gltf(filepath="C:/projectclone/ProjectRULE/RawAssets/Statue/Figure1_test_v1.glb")
body = [o for o in bpy.data.objects if o.type == "MESH"][0]

mw = body.matrix_world
verts_world = [mw @ v.co for v in body.data.vertices]
min_z = min(v.z for v in verts_world)
max_z = max(v.z for v in verts_world)
height = max_z - min_z
print(f"min_z={min_z:.4f} max_z={max_z:.4f} height={height:.4f}")

for frac in [round(0.60 + 0.02 * i, 2) for i in range(21)]:
    z = min_z + frac * height
    band = [v for v in verts_world if abs(v.z - z) < height * 0.01]
    if not band:
        print(f"frac={frac:.2f} z={z:.4f} -> no verts in band")
        continue
    xs = [v.x for v in band]
    ys = [v.y for v in band]
    print(f"frac={frac:.2f} z={z:.4f} -> width_x={max(xs)-min(xs):.4f} width_y={max(ys)-min(ys):.4f} n={len(band)}")
