using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RuleGhost.Anomalies;

namespace RuleGhost.EditorTools
{
    // One-off visual check for SoundFromExhibit's new flicker-light cue (added alongside the
    // existing placeholder audio cue in AnomalyRuntimeApplier.PlaySoundCue) -- renders the same
    // painting with the cue off and on, from a distance and with no flashlight pointed at it, to
    // confirm the flicker actually reads in a dark room instead of just trusting the numbers.
    public static class SoundCuePreviewTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";
        private const string TargetPaintingName = "Painting_West_1";

        [MenuItem("RuleGhost/Tests/Sound Cue Preview")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));

            var painting = GameObject.Find(TargetPaintingName);
            if (painting == null)
            {
                Debug.LogError($"[SoundCuePreviewTest] Could not find {TargetPaintingName}.");
                return;
            }

            Vector3 eyePos = painting.transform.position + new Vector3(4f, -0.2f, 3f);
            Capture(eyePos, painting.transform.position, "sound_cue_off", painting.transform, lightOn: false, flickerIntensity: 0f);
            Capture(eyePos, painting.transform.position, "sound_cue_low", painting.transform, lightOn: true, flickerIntensity: 1.75f);
            Capture(eyePos, painting.transform.position, "sound_cue_high", painting.transform, lightOn: true, flickerIntensity: 3.25f);

            Debug.Log($"[SoundCuePreviewTest] Renders written to {OutDir}");
        }

        private static void Capture(Vector3 eyePos, Vector3 lookTarget, string outName, Transform paintingTransform, bool lightOn, float flickerIntensity)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(lookTarget);
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.05f;

            GameObject cueLightGO = null;
            if (lightOn)
            {
                cueLightGO = new GameObject("PreviewSoundCueLight");
                cueLightGO.transform.SetParent(paintingTransform, false);
                cueLightGO.transform.localPosition = Vector3.zero;
                var light = cueLightGO.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.15f, 0.1f);
                light.range = 3f;
                light.intensity = flickerIntensity;
            }

            const int width = 900, height = 700;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();

            RenderTexture.active = rt;
            var outputTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            outputTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            outputTex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();

            string fullDir = Path.Combine(Application.dataPath, "..", OutDir);
            File.WriteAllBytes(Path.Combine(fullDir, outName + ".png"), outputTex.EncodeToPNG());

            Object.DestroyImmediate(outputTex);
            Object.DestroyImmediate(camGO);
            if (cueLightGO != null) Object.DestroyImmediate(cueLightGO);
        }
    }
}
