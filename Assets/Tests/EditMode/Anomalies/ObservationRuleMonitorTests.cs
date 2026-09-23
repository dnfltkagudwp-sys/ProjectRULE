using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuleGhost.Anomalies;
using UnityEngine;

namespace RuleGhost.Anomalies.Tests
{
    public class ObservationRuleMonitorTests
    {
        private readonly List<GameObject> spawned = new();

        private GameObject Spawn(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            spawned.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            spawned.Clear();
        }

        private PatrolSceneBindings MakeBindingsWithWestPainting(Transform painting)
        {
            var go = new GameObject("Bindings");
            spawned.Add(go);
            var bindings = go.AddComponent<PatrolSceneBindings>();
            var north = new Transform[3];
            var west = new Transform[3];
            var east = new Transform[3];
            west[0] = painting;
            bindings.Configure(north, west, east, null, null, null);
            return bindings;
        }

        private Camera MakeCamera(Vector3 position, float yaw)
        {
            var go = Spawn("TestCamera", position);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go.AddComponent<Camera>();
        }

        private static ResolvedAnomaly MakePersonInLandscape(TargetRef target)
        {
            var def = ScriptableObject.CreateInstance<AnomalyDefinition>();
            var required = new[] { new ActionRequirement(target, ActionTag.TurnAwayFromExhibit) };
            var forbidden = new[]
            {
                new ActionRequirement(target, ActionTag.RecheckExhibit),
                new ActionRequirement(target, ActionTag.FaceExhibit)
            };
            def.EditorInitialize("PersonInLandscape", "PersonInLandscape", System.Array.Empty<TimeSlot>(),
                false, false, true, required, forbidden);
            return new ResolvedAnomaly(def, required, forbidden);
        }

        [Test]
        public void KeepFacingToward_ShortOfDwell_NotYetViolated()
        {
            var painting = Spawn("Painting", new Vector3(0f, 0f, 5f));
            var bindings = MakeBindingsWithWestPainting(painting.transform);
            var camera = MakeCamera(Vector3.zero, 0f); // facing directly at the painting
            var anomaly = MakePersonInLandscape(TargetRef.Painting(PaintingWall.West, 1));
            var monitor = new ObservationRuleMonitor();

            monitor.Tick(ObservationRuleMonitor.KeepFacingForbiddenDwellSeconds - 0.1f, camera, null, bindings,
                new[] { anomaly });

            Assert.IsFalse(monitor.ViolatedForbiddenIds.Contains("PersonInLandscape"));
        }

        [Test]
        public void KeepFacingToward_PastDwell_ViolatesForbidden()
        {
            var painting = Spawn("Painting", new Vector3(0f, 0f, 5f));
            var bindings = MakeBindingsWithWestPainting(painting.transform);
            var camera = MakeCamera(Vector3.zero, 0f);
            var anomaly = MakePersonInLandscape(TargetRef.Painting(PaintingWall.West, 1));
            var monitor = new ObservationRuleMonitor();

            monitor.Tick(ObservationRuleMonitor.KeepFacingForbiddenDwellSeconds + 0.1f, camera, null, bindings,
                new[] { anomaly });

            Assert.IsTrue(monitor.ViolatedForbiddenIds.Contains("PersonInLandscape"));
        }

        [Test]
        public void TurningAway_NeverViolatesTheKeepFacingForbidden()
        {
            var painting = Spawn("Painting", new Vector3(0f, 0f, 5f));
            var bindings = MakeBindingsWithWestPainting(painting.transform);
            var camera = MakeCamera(Vector3.zero, 180f); // facing away
            var anomaly = MakePersonInLandscape(TargetRef.Painting(PaintingWall.West, 1));
            var monitor = new ObservationRuleMonitor();

            monitor.Tick(ObservationRuleMonitor.KeepFacingForbiddenDwellSeconds + 1f, camera, null, bindings,
                new[] { anomaly });

            Assert.IsFalse(monitor.ViolatedForbiddenIds.Contains("PersonInLandscape"));
            Assert.IsTrue(monitor.SatisfiedRequiredIds.Contains("PersonInLandscape"));
        }

        [Test]
        public void KeepFacingToward_BlockedByWall_DoesNotAccumulate()
        {
            var painting = Spawn("Painting", new Vector3(0f, 0f, 5f));
            var bindings = MakeBindingsWithWestPainting(painting.transform);
            var camera = MakeCamera(Vector3.zero, 0f);
            var wall = Spawn("Wall", new Vector3(0f, 0f, 2.5f));
            wall.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 0.2f);
            var anomaly = MakePersonInLandscape(TargetRef.Painting(PaintingWall.West, 1));
            var monitor = new ObservationRuleMonitor();

            monitor.Tick(ObservationRuleMonitor.KeepFacingForbiddenDwellSeconds + 1f, camera, null, bindings,
                new[] { anomaly });

            Assert.IsFalse(monitor.ViolatedForbiddenIds.Contains("PersonInLandscape"));
        }
    }
}
