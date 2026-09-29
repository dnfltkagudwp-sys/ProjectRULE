using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Shared by AnomalyRuntimeApplier (Anomaly state) and RoutinePatrolState (Normal/Routine
    // states) -- both target the same CheckPoint_ThermoHygrometer renderer, so material loading
    // and lookup live in one place instead of being duplicated across the two callers.
    // Baked-texture display: each state is a fixed pre-rendered material (digital readout,
    // warning icon and all) swapped onto the hygrometer's own renderer, replacing the old
    // floating TextMesh + flat-color tint approach.
    internal static class HygrometerDisplay
    {
        private const string NormalMaterialPath = "Assets/Art/Checkpoints/Hygrometer/M_Hygrometer_Normal.mat";
        private const string RoutineMaterialPath = "Assets/Art/Checkpoints/Hygrometer/M_Hygrometer_Routine.mat";
        private const string AnomalyMaterialPath = "Assets/Art/Checkpoints/Hygrometer/M_Hygrometer_Anomaly.mat";

        public enum State
        {
            Normal,
            Routine,
            Anomaly
        }

        public static void Set(PatrolSceneBindings bindings, State state)
        {
            var t = bindings?.Resolve(TargetRef.Simple(TargetKind.Thermometer));
            var renderer = t != null ? t.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                return;
            }

            var mat = LoadMaterial(state);
            if (mat != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        private static Material LoadMaterial(State state)
        {
            string path = state switch
            {
                State.Routine => RoutineMaterialPath,
                State.Anomaly => AnomalyMaterialPath,
                _ => NormalMaterialPath
            };

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
#else
            // Same editor-only limitation as AnomalyRuntimeApplier's painting texture swaps --
            // moving these to Resources/Addressables is a project-wide change, not a per-prop one.
            return null;
#endif
        }
    }
}
