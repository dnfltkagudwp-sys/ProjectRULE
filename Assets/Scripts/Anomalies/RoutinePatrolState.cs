using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace RuleGhost.Anomalies
{
    // Ordinary, non-anomalous night-guard conditions that can show up on a round: a painting
    // that's simply been bumped crooked (AM1 only -- rule 1's own text), or a humidity reading
    // that's drifted out of the comfortable range but isn't yet the 70%+ Anomaly (either slot,
    // since both duties check the thermometer). Deliberately kept separate from
    // AnomalyDefinition/ConflictValidator -- a Routine condition never competes for a round's
    // anomaly slot and is never subject to the combination matrix, since it's not a rule the
    // player is being tested on, just ordinary wear the job includes.
    //
    // Owns both the decision (what's rolled this round) and its scene-visible presentation (the
    // painting's tilt, the humidity readout) -- the same shape AnomalyRuntimeApplier already uses
    // for its own domain. PatrolRuntimeController owns one instance and calls RollForRound/
    // ResetVisuals around each round exactly like it already does with anomalyApplier.
    public class RoutinePatrolState
    {
        public const float TiltAngleMinDegrees = 8f;
        public const float TiltAngleMaxDegrees = 15f;
        // "0~1 정도" for a normal AM1 round; the very first AM1 round (Patrol 1, the tutorial)
        // guarantees one so the player is shown the rule in practice at least once.
        public const float TiltChance = 0.5f;
        public const int TutorialPatrolIndex = 1;

        public const int NormalHumidityMin = 45;
        public const int NormalHumidityMax = 55;
        public const int RoutineHumidityMin = 56;
        public const int RoutineHumidityMax = 69;
        // Not every round needs a humidity task -- purely a feel/pacing knob, tune freely.
        public const float RoutineHumidityChance = 0.4f;

        private static readonly PaintingWall[] AllWalls = { PaintingWall.North, PaintingWall.West, PaintingWall.East };

        private readonly Dictionary<Transform, Quaternion> baseRotations = new();
        private GameObject humidityLabel;
        private TextMesh humidityTextMesh;

        public TargetRef? TiltedPainting { get; private set; }
        public bool HumidityNeedsAdjustment { get; private set; }

        public void ResetVisuals(PatrolSceneBindings bindings)
        {
            foreach (var kvp in baseRotations)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localRotation = kvp.Value;
                }
            }
            baseRotations.Clear();
            TiltedPainting = null;

            HumidityNeedsAdjustment = false;
            if (humidityLabel != null)
            {
                humidityLabel.SetActive(false);
            }
        }

        // Decides this round's Routine conditions and immediately applies their visuals.
        // `anomalies` is only consulted to defer to an active HighHumidity Anomaly -- Routine
        // humidity never competes with it (see class comment).
        public void RollForRound(PatrolProfile profile, IReadOnlyList<ResolvedAnomaly> anomalies,
            Random rng, PatrolSceneBindings bindings)
        {
            if (profile.Slot == TimeSlot.AM1)
            {
                bool tiltThisRound = profile.PatrolIndex == TutorialPatrolIndex || rng.NextDouble() < TiltChance;
                if (tiltThisRound)
                {
                    var wall = AllWalls[rng.Next(AllWalls.Length)];
                    int index = rng.Next(1, 4);
                    ApplyTilt(TargetRef.Painting(wall, index), rng, bindings);
                }
            }

            bool highHumidityActive = false;
            if (anomalies != null)
            {
                foreach (var a in anomalies)
                {
                    if (a.Id == "HighHumidity")
                    {
                        highHumidityActive = true;
                        break;
                    }
                }
            }

            if (!highHumidityActive && rng.NextDouble() < RoutineHumidityChance)
            {
                HumidityNeedsAdjustment = true;
                int value = rng.Next(RoutineHumidityMin, RoutineHumidityMax + 1);
                ShowHumidity(bindings, value, needsAdjustment: true);
            }
        }

        private void ApplyTilt(TargetRef target, Random rng, PatrolSceneBindings bindings)
        {
            var t = bindings?.Resolve(target);
            if (t == null)
            {
                return;
            }

            baseRotations[t] = t.localRotation;
            float magnitude = TiltAngleMinDegrees + (float)rng.NextDouble() * (TiltAngleMaxDegrees - TiltAngleMinDegrees);
            float signedAngle = rng.Next(2) == 0 ? magnitude : -magnitude;
            t.localRotation *= Quaternion.AngleAxis(signedAngle, PaintingOrientation.DepthAxis(target.Wall));
            TiltedPainting = target;
        }

        // Called by PatrolRuntimeController when the player E-key's a painting that's currently
        // tilted -- straightening is the only thing E does to a painting during AM1 (observing is
        // gaze-only, via PaintingObservationTracker).
        public void Straighten(PatrolSceneBindings bindings)
        {
            if (TiltedPainting is not TargetRef target)
            {
                return;
            }

            var t = bindings?.Resolve(target);
            if (t != null && baseRotations.TryGetValue(t, out var baseRotation))
            {
                t.localRotation = baseRotation;
            }
            TiltedPainting = null;
        }

        public void AdjustHumidity(PatrolSceneBindings bindings)
        {
            if (!HumidityNeedsAdjustment)
            {
                return;
            }

            HumidityNeedsAdjustment = false;
            int value = UnityEngine.Random.Range(NormalHumidityMin, NormalHumidityMax + 1);
            ShowHumidity(bindings, value, needsAdjustment: false);
        }

        // Separate GameObject from AnomalyRuntimeApplier's own "HumidityReadout" -- the two never
        // show at once (HighHumidity active suppresses the Routine roll, see RollForRound), but
        // keeping them fully independent avoids either system needing to know about the other's
        // internal state, matching the Routine/Anomaly separation this class exists for.
#if UNITY_EDITOR
        // Test/editor-only direct state injection -- RollForRound's own Random roll decides which
        // painting (if any) tilts and whether humidity needs adjusting, which a test of
        // PatrolEvaluator's *consumption* of this state (not RollForRound's randomness itself)
        // shouldn't have to depend on. No scene/visual side effects, unlike the real roll.
        public void EditorForceState(TargetRef? tiltedPainting, bool humidityNeedsAdjustment)
        {
            TiltedPainting = tiltedPainting;
            HumidityNeedsAdjustment = humidityNeedsAdjustment;
        }
#endif

        private void ShowHumidity(PatrolSceneBindings bindings, int value, bool needsAdjustment)
        {
            var t = bindings?.Resolve(TargetRef.Simple(TargetKind.Thermometer));
            if (t == null)
            {
                return;
            }

            if (humidityLabel == null)
            {
                humidityLabel = new GameObject("RoutineHumidityReadout");
                humidityLabel.transform.SetParent(t, false);
                humidityLabel.transform.localPosition = Vector3.up * 0.5f;
                humidityTextMesh = humidityLabel.AddComponent<TextMesh>();
                humidityTextMesh.characterSize = 0.15f;
                humidityTextMesh.fontSize = 48;
                humidityTextMesh.anchor = TextAnchor.LowerCenter;
            }

            humidityLabel.SetActive(true);
            humidityTextMesh.text = $"{value}%";
            humidityTextMesh.color = needsAdjustment ? new Color(1f, 0.7f, 0.1f) : Color.white;
        }
    }
}
