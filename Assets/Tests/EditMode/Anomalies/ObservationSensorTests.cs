using System.Collections.Generic;
using NUnit.Framework;
using RuleGhost.Anomalies;
using UnityEngine;

namespace RuleGhost.Anomalies.Tests
{
    public class ObservationSensorTests
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

        private Camera MakeCamera(Vector3 position, float yaw)
        {
            var go = Spawn("TestCamera", position);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go.AddComponent<Camera>();
        }

        [Test]
        public void FacingSensor_TargetDirectlyAhead_AngleIsZero()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var target = Spawn("Target", new Vector3(0f, 0f, 5f));

            float angle = FacingSensor.HorizontalAngleToTarget(camera, target.transform);

            Assert.AreEqual(0f, angle, 0.01f);
        }

        [Test]
        public void FacingSensor_TargetBehind_AngleIsAroundOneEighty()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var target = Spawn("Target", new Vector3(0f, 0f, -5f));

            float angle = FacingSensor.HorizontalAngleToTarget(camera, target.transform);

            Assert.AreEqual(180f, angle, 0.01f);
        }

        [Test]
        public void FacingSensor_TargetToTheSide_AngleIsAroundNinety()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var target = Spawn("Target", new Vector3(5f, 0f, 0f));

            float angle = FacingSensor.HorizontalAngleToTarget(camera, target.transform);

            Assert.AreEqual(90f, angle, 0.01f);
        }

        [Test]
        public void FacingSensor_IgnoresVerticalOffset()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var target = Spawn("Target", new Vector3(0f, 10f, 5f)); // way above, but same horizontal direction

            float angle = FacingSensor.HorizontalAngleToTarget(camera, target.transform);

            Assert.AreEqual(0f, angle, 0.01f);
        }

        [Test]
        public void FacingSensor_ClearLineOfSight_WithinRange_ReturnsTrue()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var target = Spawn("Target", new Vector3(0f, 0f, 5f));

            bool result = FacingSensor.HasClearLineOfSight(camera, target.transform, playerRoot: null, maxDistance: 15f);

            Assert.IsTrue(result);
        }

        [Test]
        public void FacingSensor_BeyondMaxDistance_ReturnsFalse()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var target = Spawn("Target", new Vector3(0f, 0f, 20f));

            bool result = FacingSensor.HasClearLineOfSight(camera, target.transform, playerRoot: null, maxDistance: 15f);

            Assert.IsFalse(result);
        }

        [Test]
        public void FacingSensor_BlockedByWall_ReturnsFalse()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var target = Spawn("Target", new Vector3(0f, 0f, 5f));

            var wall = Spawn("Wall", new Vector3(0f, 0f, 2.5f));
            wall.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 0.2f);

            bool result = FacingSensor.HasClearLineOfSight(camera, target.transform, playerRoot: null, maxDistance: 15f);

            Assert.IsFalse(result);
        }

        [Test]
        public void FacingSensor_BlockedByPlayerCollider_StillReturnsTrue()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var target = Spawn("Target", new Vector3(0f, 0f, 5f));

            var playerRoot = Spawn("PlayerRoot", new Vector3(0f, 0f, 2.5f));
            playerRoot.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 0.2f);

            bool result = FacingSensor.HasClearLineOfSight(camera, target.transform, playerRoot: playerRoot.transform, maxDistance: 15f);

            Assert.IsTrue(result);
        }

        [Test]
        public void GazeSensor_WithinConeAndRangeAndUnoccluded_ReturnsTrue()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var painting = Spawn("Painting", new Vector3(0f, 0f, 3f));
            var anchor = Spawn("EyeAnchor", new Vector3(0f, 0f, 3f));
            anchor.transform.SetParent(painting.transform, true);

            bool result = GazeSensor.IsWithinGazeCone(camera, painting.transform, anchor.transform,
                playerRoot: null, maxDistance: 4.5f, coneHalfAngleDegrees: 10f);

            Assert.IsTrue(result);
        }

        [Test]
        public void GazeSensor_BeyondMaxDistance_ReturnsFalse()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var painting = Spawn("Painting", new Vector3(0f, 0f, 10f));
            var anchor = Spawn("EyeAnchor", new Vector3(0f, 0f, 10f));
            anchor.transform.SetParent(painting.transform, true);

            bool result = GazeSensor.IsWithinGazeCone(camera, painting.transform, anchor.transform,
                playerRoot: null, maxDistance: 4.5f, coneHalfAngleDegrees: 10f);

            Assert.IsFalse(result);
        }

        [Test]
        public void GazeSensor_OutsideConeAngle_ReturnsFalse()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var painting = Spawn("Painting", new Vector3(2f, 0f, 3f)); // well off to the side
            var anchor = Spawn("EyeAnchor", new Vector3(2f, 0f, 3f));
            anchor.transform.SetParent(painting.transform, true);

            bool result = GazeSensor.IsWithinGazeCone(camera, painting.transform, anchor.transform,
                playerRoot: null, maxDistance: 4.5f, coneHalfAngleDegrees: 10f);

            Assert.IsFalse(result);
        }

        [Test]
        public void GazeSensor_BlockedByUnrelatedWall_ReturnsFalse()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var painting = Spawn("Painting", new Vector3(0f, 0f, 3f));
            var anchor = Spawn("EyeAnchor", new Vector3(0f, 0f, 3f));
            anchor.transform.SetParent(painting.transform, true);

            var wall = Spawn("Wall", new Vector3(0f, 0f, 1.5f));
            wall.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 0.2f);

            bool result = GazeSensor.IsWithinGazeCone(camera, painting.transform, anchor.transform,
                playerRoot: null, maxDistance: 4.5f, coneHalfAngleDegrees: 10f);

            Assert.IsFalse(result);
        }

        [Test]
        public void GazeSensor_OnlyHitsOwnPaintingCollider_StillReturnsTrue()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var painting = Spawn("Painting", new Vector3(0f, 0f, 3f));
            painting.AddComponent<BoxCollider>().size = new Vector3(1.2f, 1.6f, 0.5f); // spans z 2.75..3.25
            // Physics.Linecast never registers a hit for a ray that starts inside a collider, so to
            // actually exercise the self-hit exclusion branch the anchor has to sit outside the box
            // (here, tucked behind it) so the ray genuinely crosses the box's own surface on its way
            // to the camera -- not how a real anchor would be placed (that's on the front face), but
            // it's what's needed to force a real hit against the painting's own collider.
            var anchor = Spawn("EyeAnchor", new Vector3(0f, 0f, 3.4f));
            anchor.transform.SetParent(painting.transform, true);

            bool result = GazeSensor.IsWithinGazeCone(camera, painting.transform, anchor.transform,
                playerRoot: null, maxDistance: 4.5f, coneHalfAngleDegrees: 10f);

            Assert.IsTrue(result);
        }

        [Test]
        public void GazeSensor_OnlyHitsPlayerCollider_StillReturnsTrue()
        {
            var camera = MakeCamera(Vector3.zero, 0f);
            var painting = Spawn("Painting", new Vector3(0f, 0f, 3f));
            var anchor = Spawn("EyeAnchor", new Vector3(0f, 0f, 3f));
            anchor.transform.SetParent(painting.transform, true);

            var playerRoot = Spawn("PlayerRoot", new Vector3(0f, 0f, 1.5f));
            playerRoot.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 0.2f);

            bool result = GazeSensor.IsWithinGazeCone(camera, painting.transform, anchor.transform,
                playerRoot: playerRoot.transform, maxDistance: 4.5f, coneHalfAngleDegrees: 10f);

            Assert.IsTrue(result);
        }
    }
}
