using RuleGhost.Anomalies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuleGhost.EditorTools
{
    // Adds (or reuses) the Lobby scene's AnomalyVisualAssets and assigns every runtime-swapped
    // texture/material to it from their asset paths -- the one place those paths still live, now
    // at edit time instead of at runtime. Safe to re-run; it just reassigns.
    public static class WireAnomalyVisualAssets
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string Paintings = "Assets/Art/Paintings/";
        private const string Hygrometer = "Assets/Art/Checkpoints/Hygrometer/";

        [MenuItem("RuleGhost/Anomalies/Wire Anomaly Visual Assets")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Wire(scene);
        }

        // Returns false (and logs which) if any asset is missing; still saves what it found.
        public static bool Wire(Scene scene)
        {
            AnomalyVisualAssets assets = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                assets = root.GetComponentInChildren<AnomalyVisualAssets>(true);
                if (assets != null)
                {
                    break;
                }
            }

            if (assets == null)
            {
                var go = new GameObject("AnomalyVisualAssets");
                SceneManager.MoveGameObjectToScene(go, scene);
                assets = go.AddComponent<AnomalyVisualAssets>();
            }

            bool ok = true;
            Texture2D Tex(string path)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t == null)
                {
                    Debug.LogError($"[WireAnomalyVisualAssets] Missing texture: {path}");
                    ok = false;
                }
                return t;
            }
            Material Mat(string path)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    Debug.LogError($"[WireAnomalyVisualAssets] Missing material: {path}");
                    ok = false;
                }
                return m;
            }

            // Portrait folders: the first has no number suffix. Landscape3's person variant is _v1,
            // the other two _v2 -- a naming quirk from how each was approved.
            var eyesOpen = new[]
            {
                Tex(Paintings + "Portrait/Portrait_eyesopen_v1.png"),
                Tex(Paintings + "Portrait2/Portrait2_eyesopen_v1.png"),
                Tex(Paintings + "Portrait3/Portrait3_eyesopen_v1.png"),
            };
            var person = new[]
            {
                Tex(Paintings + "Landscape1/Landscape1_person_v2.png"),
                Tex(Paintings + "Landscape2/Landscape2_person_v2.png"),
                Tex(Paintings + "Landscape3/Landscape3_person_v1.png"),
            };
            var portraitDeath = new[]
            {
                Tex(Paintings + "Portrait/Portrait_death_v1.png"),
                Tex(Paintings + "Portrait2/Portrait2_death_v1.png"),
                Tex(Paintings + "Portrait3/Portrait3_death_v1.png"),
            };
            var landscapeDeath = new[]
            {
                Tex(Paintings + "Landscape1/Landscape1_death_v1.png"),
                Tex(Paintings + "Landscape2/Landscape2_death_v1.png"),
                Tex(Paintings + "Landscape3/Landscape3_death_v1.png"),
            };

            Undo.RecordObject(assets, "Wire Anomaly Visual Assets");
            assets.EditorConfigure(eyesOpen, person, portraitDeath, landscapeDeath,
                Mat(Hygrometer + "M_Hygrometer_Normal.mat"),
                Mat(Hygrometer + "M_Hygrometer_Routine.mat"),
                Mat(Hygrometer + "M_Hygrometer_Anomaly.mat"));
            EditorUtility.SetDirty(assets);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[WireAnomalyVisualAssets] AnomalyVisualAssets wired in {scene.name}{(ok ? "" : " (with missing assets, see errors)")}. Scene saved.");
            return ok;
        }
    }
}
