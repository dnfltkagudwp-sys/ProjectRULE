using RuleGhost.Anomalies;
using RuleGhost.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Adds the SoundBank (every sound slot, filled in the Inspector) and the player's footstep
    // component to the Lobby scene. Never overwrites an existing SoundBank -- re-running it keeps
    // whatever clips have already been assigned. The Start scene's own SoundBank is created by
    // BuildStartScene.
    public static class BuildSoundBank
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";

        [MenuItem("RuleGhost/Audio/Build Sound Bank (Lobby)")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var bank = Object.FindFirstObjectByType<SoundBank>();
            if (bank == null)
            {
                new GameObject("SoundBank").AddComponent<SoundBank>();
            }

            var player = GameObject.Find("Player_TestController");
            if (player == null)
            {
                Debug.LogError("[BuildSoundBank] Player_TestController not found.");
            }
            else if (player.GetComponent<PlayerFootsteps>() == null)
            {
                player.AddComponent<PlayerFootsteps>();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[BuildSoundBank] SoundBank + PlayerFootsteps in the Lobby scene. Scene saved.");
        }
    }
}
