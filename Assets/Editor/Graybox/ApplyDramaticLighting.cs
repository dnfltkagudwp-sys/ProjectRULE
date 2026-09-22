using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // First-pass "closed museum at night" lighting: the room was previously
    // lit by one soft directional light plus Unity's default skybox ambient,
    // which flooded every surface evenly -- there was no dark/lit contrast to
    // judge anything against (the reference mood image is mostly dark with a
    // few dramatic pools of light). This darkens the base ambient/directional
    // fill and adds spotlights over the pilgrim crowd and each painting wall,
    // plus a warm light inside the guard room so it glows against the dark
    // lobby. A first pass to react to and iterate on, not a final pass.
    public static class ApplyDramaticLighting
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        // Paintings warmer/brighter (the featured exhibits), statue crowd
        // cooler and dimmer (silhouette should read, but not compete with
        // the art) -- role separation by color temperature and intensity
        // rather than by touching the room's overall ambient/wall tone.
        private static readonly Color PaintingWarm = new Color(1f, 0.85f, 0.65f);
        private static readonly Color StatueCool = new Color(0.82f, 0.87f, 0.95f);
        // Was (1, 0.82, 0.55) -- a saturated orange glow that, combined with the guard room's own
        // (now-retired) yellow-tan walls, made the room read as a different building entirely.
        // Desaturated toward near-neutral so the room still feels like a slightly warmer, more
        // human space than the cold gallery without breaking the shared building's color language.
        private static readonly Color GuardWarm = new Color(0.88f, 0.86f, 0.8f);
        private static readonly Color UtilityWhite = new Color(0.82f, 0.87f, 0.92f);

        [MenuItem("RuleGhost/Graybox/Apply Dramatic Lighting")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Dark flat ambient instead of the default skybox -- a closed
            // museum at night has no sky contribution indoors.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // Darkened further now that the player carries their own
            // flashlight (see AddPlayerFlashlight.cs) -- the room no longer
            // needs to be self-sufficiently visible, so the accent spots can
            // stay as discovered "points of interest" instead of doing the
            // work of general illumination.
            RenderSettings.ambientLight = new Color(0.010f, 0.0113f, 0.0167f);

            // Ambient mode is Flat (doesn't sample the skybox for lighting),
            // but the skybox is still what's rendered wherever the camera
            // can see past the geometry -- the floor-to-ceiling entrance gap
            // and the guard room's doorway both looked straight out at
            // Unity's default procedural sky, reading as a light leak even
            // though nothing was actually lighting the room from there.
            // Forcing the player camera to a solid black clear color closes
            // that off regardless of what gaps exist, without having to hunt
            // down and wall off every opening (and sidesteps needing a
            // dedicated skybox shader/material).
            var playerCam = GameObject.Find("Player_TestController/Main Camera");
            if (playerCam != null)
            {
                var cam = playerCam.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                }
            }

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = 0.033f;
                    light.color = new Color(0.75f, 0.8f, 1f);
                }
            }

            var rig = GameObject.Find("LightingRig");
            if (rig != null) Object.DestroyImmediate(rig);
            rig = new GameObject("LightingRig");

            fixtureMat = BuildFixtureMaterial();

            // Statue crowd: three downlights along the spine (pedestal runs
            // roughly z -7..7 at x=0), warm, fairly tight cones for drama.
            foreach (float z in new[] { -4f, 0f, 4f })
            {
                CreateSpot(rig.transform, $"Spot_Statue_{z}", new Vector3(0f, 4.7f, z), new Vector3(90f, 0f, 0f),
                    StatueCool, intensity: 4.5f, range: 6.5f, spotAngle: 55f);
            }

            // Individual track-light spots per painting instead of one wide
            // wall wash -- a single wide light either blew out the nearest
            // painting or left the rest of the row dark depending on angle.
            // Real gallery lighting is per-artwork anyway.
            float[] northX = { -4f, 0f, 4f };
            foreach (float x in northX)
            {
                CreateSpotAt(rig.transform, $"Spot_Painting_North_{x}", new Vector3(x, 4.5f, 8.7f), new Vector3(x, 2.5f, 9.95f),
                    PaintingWarm, intensity: 7f, range: 4.5f, spotAngle: 45f);
            }
            float[] sideZ = { -5f, 0f, 5f };
            foreach (float z in sideZ)
            {
                CreateSpotAt(rig.transform, $"Spot_Painting_West_{z}", new Vector3(-5.5f, 4.5f, z), new Vector3(-6.95f, 2.5f, z),
                    PaintingWarm, intensity: 7f, range: 4.5f, spotAngle: 45f);
                CreateSpotAt(rig.transform, $"Spot_Painting_East_{z}", new Vector3(5.5f, 4.5f, z), new Vector3(6.95f, 2.5f, z),
                    PaintingWarm, intensity: 7f, range: 4.5f, spotAngle: 45f);
            }

            // The door and thermometer are maintenance fixtures, not
            // exhibits -- a real museum doesn't hang gallery track lighting
            // over a service door or a sensor. They still need to not be
            // pitch black (past the last east painting's reach), so give
            // them a plain, dim, cool utility point light with no dramatic
            // spot cone and no gallery-can housing, instead of a featured
            // spotlight.
            CreateUtilityPoint(rig.transform, "Utility_InspectionDoor", new Vector3(6f, 3.2f, 9f), intensity: 1.1f, range: 6f);
            CreateUtilityPoint(rig.transform, "Utility_Thermometer", new Vector3(-5.8f, 2.8f, 9.5f), intensity: 0.9f, range: 5f);

            // Guard room reads as a slightly brighter, calmer space than the gallery -- not a
            // saturated orange glow (that read as a different building; see GuardWarm's comment).
            var guardLightGO = new GameObject("Point_GuardRoom");
            guardLightGO.transform.SetParent(rig.transform, false);
            guardLightGO.transform.position = new Vector3(-8.7f, 2f, -8f);
            var guardLight = guardLightGO.AddComponent<Light>();
            guardLight.type = LightType.Point;
            guardLight.color = GuardWarm;
            guardLight.intensity = 5.5f;
            guardLight.range = 5f;
            AddHousing(guardLightGO.transform, PrimitiveType.Cube, new Vector3(0.25f, 0.15f, 0.25f));

            // Thin rail bars behind each row of gallery spots so they read
            // as lights mounted on a track system instead of separate dots
            // floating at the ceiling. Purely cosmetic -- doesn't touch the
            // lights themselves.
            var rails = new GameObject("LightRails");
            rails.transform.SetParent(rig.transform, false);
            AddRail(rails.transform, "Rail_Statue", new Vector3(0f, 4.82f, 0f), new Vector3(0.07f, 0.05f, 9.5f));
            AddRail(rails.transform, "Rail_North", new Vector3(0f, 4.62f, 8.5f), new Vector3(9f, 0.05f, 0.07f));
            AddRail(rails.transform, "Rail_West", new Vector3(-5.6f, 4.62f, 0f), new Vector3(0.07f, 0.05f, 11f));
            AddRail(rails.transform, "Rail_East", new Vector3(5.6f, 4.62f, 0f), new Vector3(0.07f, 0.05f, 11f));

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[ApplyDramaticLighting] Dramatic lighting rig applied. Scene saved.");
        }

        private static Material fixtureMat;

        private static void CreateSpot(Transform parent, string name, Vector3 pos, Vector3 eulerAngles, Color color, float intensity, float range, float spotAngle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.eulerAngles = eulerAngles;
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = spotAngle;
            AddSpotHousing(go.transform);
        }

        private static void CreateSpotAt(Transform parent, string name, Vector3 pos, Vector3 lookAt, Color color, float intensity, float range, float spotAngle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.LookAt(lookAt);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = spotAngle;
            AddSpotHousing(go.transform);
        }

        // Cheap proxy geometry so each light reads as an actual fixture
        // (a real gallery track-light can) instead of light with no visible
        // source -- a full custom fixture asset isn't worth the VARGO
        // cost/time for a graybox pass, but a primitive can + a mounting
        // arm reads as "there's a lamp there" from normal play distances.
        private static void AddSpotHousing(Transform lightTransform)
        {
            var can = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            can.name = "Fixture_Can";
            Object.DestroyImmediate(can.GetComponent<Collider>());
            can.transform.SetParent(lightTransform, false);
            can.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            can.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            can.transform.localScale = new Vector3(0.14f, 0.08f, 0.14f);
            can.GetComponent<Renderer>().sharedMaterial = fixtureMat;

            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Fixture_MountArm";
            Object.DestroyImmediate(arm.GetComponent<Collider>());
            arm.transform.SetParent(lightTransform, false);
            arm.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            arm.transform.localScale = new Vector3(0.04f, 0.04f, 0.28f);
            arm.GetComponent<Renderer>().sharedMaterial = fixtureMat;
        }

        private static void CreateUtilityPoint(Transform parent, string name, Vector3 pos, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = UtilityWhite;
            light.intensity = intensity;
            light.range = range;
        }

        private static void AddRail(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = name;
            Object.DestroyImmediate(rail.GetComponent<Collider>());
            rail.transform.SetParent(parent, false);
            rail.transform.position = pos;
            rail.transform.localScale = scale;
            rail.GetComponent<Renderer>().sharedMaterial = fixtureMat;
        }

        private static void AddHousing(Transform parent, PrimitiveType shape, Vector3 scale)
        {
            var housing = GameObject.CreatePrimitive(shape);
            housing.name = "Fixture_Housing";
            Object.DestroyImmediate(housing.GetComponent<Collider>());
            housing.transform.SetParent(parent, false);
            housing.transform.localScale = scale;
            housing.GetComponent<Renderer>().sharedMaterial = fixtureMat;
        }

        private static Material BuildFixtureMaterial()
        {
            const string path = "Assets/Art/Architecture/M_LightFixture.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            var dark = new Color(0.05f, 0.05f, 0.05f);
            mat.color = dark;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", dark);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.7f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
