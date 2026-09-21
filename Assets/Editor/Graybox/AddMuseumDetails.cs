using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Cheap primitive-only detail pass to sell "museum" beyond lighting
    // alone: baseboard/crown trim so the room reads as built rather than a
    // bare box, small brass placards under each painting, and a velvet-rope
    // stanchion line along the pilgrim pedestal (the classic "do not
    // approach" museum cue, which also reads well for this game's tone).
    // No VARGO assets -- everything here is primitives + flat materials.
    public static class AddMuseumDetails
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const float HalfWidth = 7f;
        private const float HalfDepth = 10f;
        private const float Height = 5f;
        private const float WallThickness = 0.2f;

        [MenuItem("RuleGhost/Graybox/Add Museum Details")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = GameObject.Find("MuseumDetails");
            if (existing != null) Object.DestroyImmediate(existing);
            var root = new GameObject("MuseumDetails");

            var trimMat = GetOrCreateMaterial("M_Trim", new Color(0.14f, 0.1f, 0.08f), smoothness: 0.35f);
            // Brass read too close to the paintings' own dark tones and
            // disappeared -- a pale, matte placard reads as an actual
            // museum label by contrast instead.
            var placardMat = GetOrCreateMaterial("M_Placard", new Color(0.85f, 0.85f, 0.82f), smoothness: 0.15f);
            var stanchionMat = GetOrCreateMaterial("M_Stanchion", new Color(0.08f, 0.08f, 0.08f), smoothness: 0.6f);
            var ropeMat = GetOrCreateMaterial("M_Rope", new Color(0.35f, 0.03f, 0.04f), smoothness: 0.3f);
            var floorTrimMat = GetOrCreateMaterial("M_FloorTrim", new Color(0.08f, 0.08f, 0.09f), smoothness: 0.25f);

            AddTrim(root.transform, trimMat);
            AddPlacards(root.transform, placardMat);
            AddStanchions(root.transform, stanchionMat, ropeMat);
            AddCeilingFrame(root.transform, trimMat);
            AddFloorDelineation(root.transform, floorTrimMat);
            AddWallPilasters(root.transform, trimMat);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AddMuseumDetails] Trim, placards, stanchions, ceiling frame, floor delineation and wall pilasters added. Scene saved.");
        }

        private static void AddWallPilasters(Transform root, Material mat)
        {
            // Shallow vertical pilaster between each pair of adjacent
            // paintings on a wall -- breaks the wall into panels instead of
            // one continuous plane, without touching painting position/size.
            // Reuses the trim material so it reads as part of the same
            // "dark architectural accent" language as the baseboard/frames,
            // rather than a same-color wall relief that would barely show.
            const float thickness = 0.05f;
            const float protrusion = 0.02f;
            const float bottomY = 0.13f; // baseboard top
            const float topY = Height - 0.07f; // ceiling molding bottom
            float midY = (bottomY + topY) / 2f;
            float spanY = topY - bottomY;

            var group = new GameObject("WallPilasters");
            group.transform.SetParent(root, false);

            float northZ = HalfDepth - WallThickness / 2f - protrusion / 2f;
            float westX = -HalfWidth + WallThickness / 2f + protrusion / 2f;
            float eastX = HalfWidth - WallThickness / 2f - protrusion / 2f;

            // Paintings sit at x -4/0/4 (north) and z -5/0/5 (west/east) --
            // pilasters go at the midpoints between them.
            foreach (float x in new[] { -2f, 2f })
                Block(group.transform, $"Pilaster_North_{x}", mat, new Vector3(x, midY, northZ), new Vector3(thickness, spanY, protrusion));
            foreach (float z in new[] { -2.5f, 2.5f })
            {
                Block(group.transform, $"Pilaster_West_{z}", mat, new Vector3(westX, midY, z), new Vector3(protrusion, spanY, thickness));
                Block(group.transform, $"Pilaster_East_{z}", mat, new Vector3(eastX, midY, z), new Vector3(protrusion, spanY, thickness));
            }
        }

        private static void AddCeilingFrame(Transform root, Material mat)
        {
            // A shallow picture-frame-style border set in from the ceiling
            // edge, separate from the light rails -- breaks up the ceiling
            // as one huge flat plane without any coffering complexity.
            const float inset = 0.3f;
            const float drop = 0.15f;
            const float frameH = 0.06f;
            const float frameD = 0.08f;
            float y = Height - drop;
            float innerHalfW = HalfWidth - inset;
            float innerHalfD = HalfDepth - inset;

            var group = new GameObject("CeilingFrame");
            group.transform.SetParent(root, false);

            Block(group.transform, "CeilingFrame_West", mat, new Vector3(-innerHalfW, y, 0f), new Vector3(frameD, frameH, innerHalfD * 2f));
            Block(group.transform, "CeilingFrame_East", mat, new Vector3(innerHalfW, y, 0f), new Vector3(frameD, frameH, innerHalfD * 2f));
            Block(group.transform, "CeilingFrame_North", mat, new Vector3(0f, y, innerHalfD), new Vector3(innerHalfW * 2f, frameH, frameD));
            Block(group.transform, "CeilingFrame_South", mat, new Vector3(0f, y, -innerHalfD), new Vector3(innerHalfW * 2f, frameH, frameD));
        }

        private static void AddFloorDelineation(Transform root, Material trimMat)
        {
            // Floor-hugging perimeter border, reads as a continuation of the
            // baseboard. (A same-material aisle tint strip was tried here
            // too, but a raised patch of the exact same floor stone just
            // looked like an unexplained rug-shaped step rather than a
            // subtle zone cue -- the rope stanchions and the accent lighting
            // already separate "aisle" from "exhibit" well enough.)
            const float borderW = 0.18f;
            const float borderH = 0.015f;
            float y = borderH / 2f + 0.001f;

            var group = new GameObject("FloorDelineation");
            group.transform.SetParent(root, false);

            float westX = -HalfWidth + WallThickness + borderW / 2f;
            float eastX = HalfWidth - WallThickness - borderW / 2f;
            float northZ = HalfDepth - WallThickness - borderW / 2f;
            float southZ = -HalfDepth + WallThickness + borderW / 2f;
            Block(group.transform, "FloorBorder_West", trimMat, new Vector3(westX, y, 0f), new Vector3(borderW, borderH, HalfDepth * 2f - borderW * 2f));
            Block(group.transform, "FloorBorder_East", trimMat, new Vector3(eastX, y, 0f), new Vector3(borderW, borderH, HalfDepth * 2f - borderW * 2f));
            Block(group.transform, "FloorBorder_North", trimMat, new Vector3(0f, y, northZ), new Vector3(HalfWidth * 2f, borderH, borderW));
            Block(group.transform, "FloorBorder_South", trimMat, new Vector3(0f, y, southZ), new Vector3(HalfWidth * 2f, borderH, borderW));
        }

        private static void AddTrim(Transform root, Material mat)
        {
            // Baseboard noticeably heavier than the ceiling molding -- a
            // 20x14m room read as a bare box with the old 6cm/4cm trim on
            // both edges; a real baseboard carries visual weight at the
            // floor, while the ceiling edge stays light so it doesn't feel
            // like a lowered drop-ceiling.
            const float baseH = 0.13f, baseD = 0.05f;
            const float ceilH = 0.07f, ceilD = 0.04f;
            var group = new GameObject("Trim");
            group.transform.SetParent(root, false);

            AddTrimRing(group.transform, mat, "Baseboard", baseH / 2f, baseH, baseD);
            AddTrimRing(group.transform, mat, "CeilingMolding", Height - ceilH / 2f, ceilH, ceilD);
        }

        private static void AddTrimRing(Transform parent, Material mat, string label, float y, float trimH, float trimD)
        {
            float westX = -HalfWidth + WallThickness / 2f + trimD / 2f;
            float eastX = HalfWidth - WallThickness / 2f - trimD / 2f;
            float northZ = HalfDepth - WallThickness / 2f - trimD / 2f;
            float southZ = -HalfDepth + WallThickness / 2f + trimD / 2f;

            Block(parent, $"{label}_West", mat, new Vector3(westX, y, 0f), new Vector3(trimD, trimH, HalfDepth * 2f));
            Block(parent, $"{label}_East", mat, new Vector3(eastX, y, 0f), new Vector3(trimD, trimH, HalfDepth * 2f));
            Block(parent, $"{label}_North", mat, new Vector3(0f, y, northZ), new Vector3(HalfWidth * 2f, trimH, trimD));
            Block(parent, $"{label}_South", mat, new Vector3(0f, y, southZ), new Vector3(HalfWidth * 2f, trimH, trimD));
        }

        private static void AddPlacards(Transform root, Material mat)
        {
            var group = new GameObject("Placards");
            group.transform.SetParent(root, false);
            const float y = 1.5f;
            const float w = 0.35f, h = 0.15f, t = 0.02f;

            float[] northX = { -4f, 0f, 4f };
            foreach (float x in northX)
                Block(group.transform, $"Placard_North_{x}", mat, new Vector3(x, y, HalfDepth - 0.03f), new Vector3(w, h, t));

            float[] sideZ = { -5f, 0f, 5f };
            foreach (float z in sideZ)
            {
                Block(group.transform, $"Placard_West_{z}", mat, new Vector3(-HalfWidth + 0.03f, y, z), new Vector3(t, h, w));
                Block(group.transform, $"Placard_East_{z}", mat, new Vector3(HalfWidth - 0.03f, y, z), new Vector3(t, h, w));
            }
        }

        private static void AddStanchions(Transform root, Material postMat, Material ropeMat)
        {
            var group = new GameObject("PedestalStanchions");
            group.transform.SetParent(root, false);

            // Pedestal spans roughly x -1.5..1.5, z -7..7 at floor level --
            // a line of posts a short distance out from each long edge,
            // connected by a rope segment between each consecutive pair.
            const float offset = 0.5f;
            const float postHeight = 0.9f;
            const float postRadius = 0.04f;
            float[] xSides = { -1.5f - offset, 1.5f + offset };
            float[] zPositions = { -7f, -5f, -3f, -1f, 1f, 3f, 5f, 7f };

            foreach (float x in xSides)
            {
                Transform prevPost = null;
                foreach (float z in zPositions)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = $"Stanchion_Post_{x}_{z}";
                    Object.DestroyImmediate(post.GetComponent<Collider>());
                    post.transform.SetParent(group.transform, false);
                    post.transform.position = new Vector3(x, postHeight / 2f, z);
                    post.transform.localScale = new Vector3(postRadius * 2f, postHeight / 2f, postRadius * 2f);
                    post.GetComponent<Renderer>().sharedMaterial = postMat;

                    if (prevPost != null)
                    {
                        Vector3 a = prevPost.position;
                        Vector3 b = post.transform.position;
                        Vector3 mid = (a + b) / 2f + Vector3.up * (postHeight * 0.9f - postHeight / 2f);
                        float len = Vector3.Distance(a, b);

                        var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        rope.name = $"Stanchion_Rope_{x}_{z}";
                        Object.DestroyImmediate(rope.GetComponent<Collider>());
                        rope.transform.SetParent(group.transform, false);
                        rope.transform.position = new Vector3(x, postHeight * 0.9f, mid.z);
                        rope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        rope.transform.localScale = new Vector3(postRadius * 1.2f, len / 2f, postRadius * 1.2f);
                        rope.GetComponent<Renderer>().sharedMaterial = ropeMat;
                    }
                    prevPost = post.transform;
                }
            }
        }

        private static GameObject Block(Transform parent, string name, Material mat, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness)
        {
            string path = $"Assets/Art/Architecture/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
