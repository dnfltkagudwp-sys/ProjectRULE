using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Shared by AnomalyRuntimeApplier (Anomaly state) and RoutinePatrolState (Normal/Routine
    // states) -- both target the same CheckPoint_ThermoHygrometer renderer, so material loading
    // and lookup live in one place instead of being duplicated across the two callers.
    // Baked-texture display: each state is a fixed pre-rendered material (digital readout,
    // warning icon and all) swapped onto the hygrometer's own renderer, replacing the old
    // floating TextMesh + flat-color tint approach. Public so DeathSequenceUI's HighHumidity
    // glitch can cycle the same materials.
    public static class HygrometerDisplay
    {
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

        // Every state's material, in State order; entries are null wherever LoadMaterial can't load.
        public static Material[] LoadAllStates()
        {
            return new[] { LoadMaterial(State.Normal), LoadMaterial(State.Routine), LoadMaterial(State.Anomaly) };
        }

        // From AnomalyVisualAssets (a scene reference) so the build includes them -- the old
        // AssetDatabase path lookup only worked in the Editor.
        private static Material LoadMaterial(State state)
        {
            var assets = AnomalyVisualAssets.Instance;
            var mat = assets != null ? assets.Hygrometer(state) : null;
            if (mat == null)
            {
                Debug.LogError($"[HygrometerDisplay] No {state} material -- is AnomalyVisualAssets in the scene and wired?");
            }
            return mat;
        }
    }
}
