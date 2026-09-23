using System.IO;
using RuleGhost.Anomalies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Forces a couple of anomalies through AnomalyRuntimeApplier directly (bypassing the RNG in
    // PatrolGenerator) and renders the result, so the texture-swap/flip/door-angle/humidity
    // logic can be checked without having to get lucky in Play mode first.
    public static class AnomalyApplierSmokeTest
    {
        private const string ScenePath = "Assets/Scenes/Lobby_Graybox.unity";
        private const string OutDir = "RawAssets/Architecture/RoomCheck";

        [MenuItem("RuleGhost/Tests/Anomaly Applier Smoke Test")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var bindingsGO = GameObject.Find("PatrolSceneBindings");
            var bindings = bindingsGO.GetComponent<PatrolSceneBindings>();

            var eyesOpen = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>("Assets/Data/Anomalies/Anomaly_EyesOpenPortrait.asset");
            var flipped = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>("Assets/Data/Anomalies/Anomaly_FlippedPainting.asset");
            var wideOpen = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>("Assets/Data/Anomalies/Anomaly_InspectionDoorWideOpen.asset");
            var highHumidity = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>("Assets/Data/Anomalies/Anomaly_HighHumidity.asset");

            var eyesOpenResolved = new ResolvedAnomaly(eyesOpen,
                new System.Collections.Generic.List<ActionRequirement>(),
                new System.Collections.Generic.List<ActionRequirement> { new ActionRequirement(TargetRef.Painting(PaintingWall.North, 2), ActionTag.MakeEyeContact) });

            var flippedResolved = new ResolvedAnomaly(flipped,
                new System.Collections.Generic.List<ActionRequirement> { new ActionRequirement(TargetRef.Painting(PaintingWall.East, 1), ActionTag.FlipPainting) },
                new System.Collections.Generic.List<ActionRequirement> { new ActionRequirement(TargetRef.Painting(PaintingWall.West, 1), ActionTag.ModifyOriginalPainting) });

            var wideOpenResolved = new ResolvedAnomaly(wideOpen,
                new System.Collections.Generic.List<ActionRequirement> { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                new System.Collections.Generic.List<ActionRequirement>());

            var humidityResolved = new ResolvedAnomaly(highHumidity,
                new System.Collections.Generic.List<ActionRequirement>(),
                new System.Collections.Generic.List<ActionRequirement> { new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.AdjustThermostat) });

            var applier = new AnomalyRuntimeApplier();
            applier.ResetAll(bindings); // idempotent re-run: undo any state left from a previous run of this test first
            applier.Apply(bindings, new[] { eyesOpenResolved, flippedResolved, wideOpenResolved, humidityResolved });

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutDir));
            Capture(new Vector3(0f, 1.6f, 8.3f), new Vector3(0f, 2.3f, 9.95f), "smoke_north_eyesopen");
            Capture(new Vector3(-2f, 1.6f, 2f), new Vector3(-6.95f, 2.3f, 0f), "smoke_west_flipped");
            Capture(new Vector3(3f, 1.8f, 7f), new Vector3(6.9f, 1.3f, 9f), "smoke_door_wideopen");
            Capture(new Vector3(-4f, 2f, 8.5f), new Vector3(-6f, 2.2f, 9.9f), "smoke_humidity");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[AnomalyApplierSmokeTest] Applied 4 anomalies and rendered checks to {OutDir}. Scene saved WITH anomalies still applied -- run ResetAll before real play.");
        }

        [MenuItem("RuleGhost/Tests/Anomaly Applier Smoke Test Reset")]
        public static void ResetOnly()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var bindingsGO = GameObject.Find("PatrolSceneBindings");
            var bindings = bindingsGO.GetComponent<PatrolSceneBindings>();
            new AnomalyRuntimeApplier().ResetAll(bindings);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AnomalyApplierSmokeTest] Reset applied. Scene saved.");
        }

        private static void Capture(Vector3 eyePos, Vector3 lookTarget, string outName)
        {
            var camGO = new GameObject("TestCam");
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = eyePos;
            camGO.transform.LookAt(lookTarget);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

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
        }
    }
}
