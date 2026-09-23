using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Resolves a TargetRef to the actual Transform it refers to in the lobby scene. This is the
    // permanent bridge between the anomaly data layer and real scene objects — the patrol system
    // (and later, the real interaction/rule-checking system) will depend on it, not just debug
    // tooling. Wired up by LobbyGrayboxBuilder when the scene is (re)built.
    public class PatrolSceneBindings : MonoBehaviour
    {
        [SerializeField] private Transform[] northPaintings = new Transform[3];
        [SerializeField] private Transform[] westPaintings = new Transform[3];
        [SerializeField] private Transform[] eastPaintings = new Transform[3];
        [SerializeField] private Transform thermometer;
        [SerializeField] private Transform inspectionDoor;
        [SerializeField] private Transform entranceMarker;
        [SerializeField] private Transform guardRoomReturn;

        public void Configure(Transform[] north, Transform[] west, Transform[] east, Transform thermometerTransform,
            Transform inspectionDoorTransform, Transform entranceMarkerTransform)
        {
            northPaintings = north;
            westPaintings = west;
            eastPaintings = east;
            thermometer = thermometerTransform;
            inspectionDoor = inspectionDoorTransform;
            entranceMarker = entranceMarkerTransform;
        }

        // Separate from Configure (rather than extending its parameter list) since
        // GuardRoomReturnTrigger is wired up later, by AttachGuardRoomReturnTrigger, on top of a
        // scene Configure already built -- matches this project's convention of layering
        // additional wiring onto an existing scene instead of re-running the original builder.
        public void ConfigureGuardRoomReturn(Transform guardRoomReturnTransform)
        {
            guardRoomReturn = guardRoomReturnTransform;
        }

        public Transform ResolvePainting(PaintingWall wall, int index)
        {
            var array = wall switch
            {
                PaintingWall.North => northPaintings,
                PaintingWall.West => westPaintings,
                PaintingWall.East => eastPaintings,
                _ => null
            };

            if (array == null || index < 1 || index > array.Length)
            {
                return null;
            }

            return array[index - 1];
        }

        // Returns null for TargetKind.WholePatrol — that target has no single scene object.
        public Transform Resolve(TargetRef target)
        {
            return target.Kind switch
            {
                TargetKind.SpecificPainting => ResolvePainting(target.Wall, target.Index),
                TargetKind.Thermometer => thermometer,
                TargetKind.InspectionDoor => inspectionDoor,
                TargetKind.EntranceDoor => entranceMarker,
                TargetKind.GuardRoomReturn => guardRoomReturn,
                _ => null
            };
        }
    }
}
