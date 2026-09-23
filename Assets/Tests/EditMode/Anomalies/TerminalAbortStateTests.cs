using System.Collections.Generic;
using NUnit.Framework;
using RuleGhost.Anomalies;
using UnityEngine;

namespace RuleGhost.Anomalies.Tests
{
    public class TerminalAbortStateTests
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

        private PatrolSceneBindings MakeBindings(Transform door)
        {
            var go = new GameObject("Bindings");
            spawned.Add(go);
            var bindings = go.AddComponent<PatrolSceneBindings>();
            var empty = new Transform[3];
            bindings.Configure(empty, empty, empty, null, door, null);
            return bindings;
        }

        private Camera MakeCamera(Vector3 position, float yaw)
        {
            var go = Spawn("TestCamera", position);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go.AddComponent<Camera>();
        }

        [Test]
        public void NotDiscovered_UntilGazeDwellReached()
        {
            var door = Spawn("Door", new Vector3(0f, 0f, 3f));
            var bindings = MakeBindings(door.transform);
            var camera = MakeCamera(Vector3.zero, 0f);
            var state = new TerminalAbortState();

            state.Tick(TerminalAbortState.DiscoveryDwellSeconds - 0.05f, camera, null, bindings);

            Assert.IsFalse(state.Discovered);
        }

        [Test]
        public void Discovered_AfterSustainedGaze()
        {
            var door = Spawn("Door", new Vector3(0f, 0f, 3f));
            var bindings = MakeBindings(door.transform);
            var camera = MakeCamera(Vector3.zero, 0f);
            var state = new TerminalAbortState();

            state.Tick(TerminalAbortState.DiscoveryDwellSeconds + 0.05f, camera, null, bindings);

            Assert.IsTrue(state.Discovered);
        }

        [Test]
        public void GazeInterrupted_ResetsDwell()
        {
            var door = Spawn("Door", new Vector3(0f, 0f, 3f));
            var bindings = MakeBindings(door.transform);
            var lookingAway = MakeCamera(Vector3.zero, 180f);
            var state = new TerminalAbortState();

            // Almost there, then look away, then almost there again -- should never accumulate
            // past a single partial dwell.
            state.Tick(TerminalAbortState.DiscoveryDwellSeconds - 0.05f,
                MakeCamera(Vector3.zero, 0f), null, bindings);
            state.Tick(1f, lookingAway, null, bindings);
            state.Tick(TerminalAbortState.DiscoveryDwellSeconds - 0.05f,
                MakeCamera(Vector3.zero, 0f), null, bindings);

            Assert.IsFalse(state.Discovered);
        }

        [Test]
        public void BeforeDiscovery_VisitingOtherTargetsIsNotAViolation()
        {
            var state = new TerminalAbortState();

            state.NotifyVisit(TargetRef.Simple(TargetKind.Thermometer));

            Assert.IsFalse(state.Violated);
        }

        [Test]
        public void AfterDiscovery_VisitingInspectionDoorOrGuardRoomIsFine()
        {
            var door = Spawn("Door", new Vector3(0f, 0f, 3f));
            var bindings = MakeBindings(door.transform);
            var camera = MakeCamera(Vector3.zero, 0f);
            var state = new TerminalAbortState();
            state.Tick(TerminalAbortState.DiscoveryDwellSeconds + 0.05f, camera, null, bindings);
            Assert.IsTrue(state.Discovered);

            state.NotifyVisit(TargetRef.Simple(TargetKind.InspectionDoor));
            state.NotifyVisit(TargetRef.Simple(TargetKind.GuardRoomReturn));

            Assert.IsFalse(state.Violated);
        }

        [Test]
        public void AfterDiscovery_VisitingAnythingElseViolates()
        {
            var door = Spawn("Door", new Vector3(0f, 0f, 3f));
            var bindings = MakeBindings(door.transform);
            var camera = MakeCamera(Vector3.zero, 0f);
            var state = new TerminalAbortState();
            state.Tick(TerminalAbortState.DiscoveryDwellSeconds + 0.05f, camera, null, bindings);

            state.NotifyVisit(TargetRef.Simple(TargetKind.Thermometer));

            Assert.IsTrue(state.Violated);
        }
    }
}
