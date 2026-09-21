using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Attaches a flashlight-style spotlight to the player's camera so the
    // room can go genuinely darker overall without leaving the player
    // unable to see -- a classic horror-game move. The existing painting/
    // statue accent spots stay as fixed "points of interest" that light up
    // as the player's own beam or proximity crosses them, rather than
    // carrying the room's base visibility on their own.
    public static class AddPlayerFlashlight
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Graybox/Add Player Flashlight")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var camGO = GameObject.Find("Player_TestController/Main Camera");
            if (camGO == null)
            {
                Debug.LogError("[AddPlayerFlashlight] Could not find Player_TestController/Main Camera.");
                return;
            }

            var existing = camGO.transform.Find("Flashlight");
            GameObject flashGO = existing != null ? existing.gameObject : new GameObject("Flashlight");
            flashGO.transform.SetParent(camGO.transform, false);
            flashGO.transform.localPosition = Vector3.zero;
            flashGO.transform.localRotation = Quaternion.identity;

            var light = flashGO.GetComponent<Light>();
            if (light == null) light = flashGO.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.92f, 0.95f, 1f);
            light.intensity = 13f;
            light.range = 10f;
            light.spotAngle = 40f;
            light.innerSpotAngle = 20f;
            light.shadows = LightShadows.Soft;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AddPlayerFlashlight] Flashlight attached to player camera. Scene saved.");
        }
    }
}
