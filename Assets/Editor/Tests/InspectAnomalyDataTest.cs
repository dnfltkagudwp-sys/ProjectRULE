using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Prints what Unity actually deserialized for a few anomaly fields whose raw YAML looked
    // ambiguous when read as plain text -- ground truth beats guessing at the serialized format.
    public static class InspectAnomalyDataTest
    {
        [MenuItem("RuleGhost/Tests/Inspect Anomaly Data")]
        public static void Run()
        {
            Inspect("Assets/Data/Anomalies/Anomaly_EyesOpenPortrait.asset");
            Inspect("Assets/Data/Anomalies/Anomaly_PersonInLandscape.asset");
        }

        private static void Inspect(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<RuleGhost.Anomalies.AnomalyDefinition>(path);
            if (def == null)
            {
                Debug.LogError($"[InspectAnomalyDataTest] Could not load {path}");
                return;
            }

            Debug.Log($"[InspectAnomalyDataTest] {def.Id}: PaintingWallOptions.Count={def.PaintingWallOptions.Count} " +
                      $"[{string.Join(",", def.PaintingWallOptions)}] AllowedSlots.Count={def.AllowedSlots.Count} " +
                      $"[{string.Join(",", def.AllowedSlots)}]");
        }
    }
}
