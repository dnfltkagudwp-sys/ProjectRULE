using System;
using System.Collections.Generic;
using NUnit.Framework;
using RuleGhost.Anomalies;
using UnityEngine;
using Random = System.Random;

namespace RuleGhost.Anomalies.Tests
{
    public class RoutinePatrolStateTests
    {
        private static PatrolSceneBindings BuildBindings(out GameObject root)
        {
            var rootGO = new GameObject("TestBindingsRoot");
            root = rootGO;
            var bindings = rootGO.AddComponent<PatrolSceneBindings>();

            Transform[] MakeWall(string prefix)
            {
                var arr = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject($"{prefix}_{i + 1}");
                    go.transform.SetParent(rootGO.transform);
                    arr[i] = go.transform;
                }
                return arr;
            }

            var north = MakeWall("North");
            var west = MakeWall("West");
            var east = MakeWall("East");
            var thermometer = new GameObject("Thermometer").transform;
            thermometer.SetParent(rootGO.transform);
            var door = new GameObject("Door").transform;
            door.SetParent(rootGO.transform);
            var entrance = new GameObject("Entrance").transform;
            entrance.SetParent(rootGO.transform);

            bindings.Configure(north, west, east, thermometer, door, entrance);
            return bindings;
        }

        private static PatrolProfile MakeProfile(int index, TimeSlot slot)
        {
            var duty = ScriptableObject.CreateInstance<PatrolDuty>();
            duty.EditorInitialize("Duty_Test", slot, Array.Empty<ActionRequirement>(), Array.Empty<ActionRequirement>());
            var profile = ScriptableObject.CreateInstance<PatrolProfile>();
            profile.EditorInitialize(index, slot, duty, new List<AnomalyDefinition>(), DifficultyRule.None);
            return profile;
        }

        [Test]
        public void RollForRound_TutorialAM1Patrol_AlwaysTiltsAPainting()
        {
            var bindings = BuildBindings(out var root);
            try
            {
                var profile = MakeProfile(RoutinePatrolState.TutorialPatrolIndex, TimeSlot.AM1);
                var state = new RoutinePatrolState();

                state.RollForRound(profile, Array.Empty<ResolvedAnomaly>(), new Random(1), bindings);

                Assert.IsTrue(state.TiltedPainting.HasValue);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RollForRound_AM5Profile_NeverTiltsAPainting()
        {
            var bindings = BuildBindings(out var root);
            try
            {
                var profile = MakeProfile(2, TimeSlot.AM5);
                var state = new RoutinePatrolState();

                for (int seed = 0; seed < 50; seed++)
                {
                    state.RollForRound(profile, Array.Empty<ResolvedAnomaly>(), new Random(seed), bindings);
                    Assert.IsFalse(state.TiltedPainting.HasValue, $"seed {seed}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Straighten_RevertsRotationAndClearsTiltedPainting()
        {
            var bindings = BuildBindings(out var root);
            try
            {
                var profile = MakeProfile(RoutinePatrolState.TutorialPatrolIndex, TimeSlot.AM1);
                var state = new RoutinePatrolState();
                state.RollForRound(profile, Array.Empty<ResolvedAnomaly>(), new Random(1), bindings);
                Assert.IsTrue(state.TiltedPainting.HasValue);
                var t = bindings.Resolve(state.TiltedPainting.Value);
                Assert.AreNotEqual(Quaternion.identity, t.localRotation);

                state.Straighten(bindings);

                Assert.IsNull(state.TiltedPainting);
                Assert.AreEqual(Quaternion.identity, t.localRotation);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResetVisuals_ClearsTiltAndHumidityState_BetweenRounds()
        {
            var bindings = BuildBindings(out var root);
            try
            {
                var profile = MakeProfile(RoutinePatrolState.TutorialPatrolIndex, TimeSlot.AM1);
                var state = new RoutinePatrolState();
                state.RollForRound(profile, Array.Empty<ResolvedAnomaly>(), new Random(1), bindings);
                var t = bindings.Resolve(state.TiltedPainting.Value);

                state.ResetVisuals(bindings);

                Assert.IsNull(state.TiltedPainting);
                Assert.IsFalse(state.HumidityNeedsAdjustment);
                Assert.AreEqual(Quaternion.identity, t.localRotation);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RollForRound_HighHumidityAnomalyActive_NeverRollsRoutineHumidity()
        {
            var bindings = BuildBindings(out var root);
            try
            {
                var profile = MakeProfile(2, TimeSlot.AM5);
                var highHumidityDef = ScriptableObject.CreateInstance<AnomalyDefinition>();
                highHumidityDef.EditorInitialize("HighHumidity", "HighHumidity", Array.Empty<TimeSlot>(),
                    false, false, false, Array.Empty<ActionRequirement>(), Array.Empty<ActionRequirement>());
                var highHumidity = new ResolvedAnomaly(highHumidityDef, Array.Empty<ActionRequirement>(),
                    Array.Empty<ActionRequirement>());
                var state = new RoutinePatrolState();

                for (int seed = 0; seed < 50; seed++)
                {
                    state.RollForRound(profile, new[] { highHumidity }, new Random(seed), bindings);
                    Assert.IsFalse(state.HumidityNeedsAdjustment, $"seed {seed}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
