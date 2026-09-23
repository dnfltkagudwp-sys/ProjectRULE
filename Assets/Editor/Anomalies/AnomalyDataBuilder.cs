using System.Collections.Generic;
using System.IO;
using RuleGhost.Anomalies;
using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Builds the Rule 1 (규칙 괴담) anomaly-combination data set exactly as confirmed in Notion:
    // Duty 1/2 (baseline patrol), Anomalies 3/4/5/6/7a/7b/8/9, the combination rule set, the
    // Rule-6 mirror pairing, and the 6 PatrolProfiles for the 3-day x 2-patrol structure.
    //
    // Rules 1 and 2 are NOT anomalies here — per the confirmed design they are PatrolDuty data,
    // always active for their slot rather than picked from a pool. "Tilted painting" (rule 1's
    // own text) and the ordinary 56-69% humidity case are ordinary Routine conditions, not
    // Anomalies -- see RoutinePatrolState, which rolls and applies them at runtime rather than
    // through any static data here.
    public static class AnomalyDataBuilder
    {
        private const string DataFolder = "Assets/Data/Anomalies";

        [MenuItem("RuleGhost/Anomalies/Build Anomaly Data Set")]
        public static void Build()
        {
            EnsureFolder(DataFolder);

            // Rule text is verbatim from the confirmed design doc (Notion, 근무수칙 초안) -- the
            // 규칙서 UI shows exactly this wording, not the short DisplayName/Id labels below.
            //
            // AM1's 9 paintings AND the inspection door are required via a loose gaze check (see
            // LooseObservationTracker/PatrolEvaluator) rather than an E-key visit -- rule 1 is a
            // visual scan, and the door's E-key interaction has a real side effect (its hinge still
            // carries a graybox DoorTestInteraction toggle -- see AttachPatrolRuntime). Only the
            // thermometer stays a plain E-key check. ReturnToGuardRoom/GuardRoomReturn is the
            // round's own closing action (tautologically satisfied by the time Evaluate runs at
            // all -- see PatrolEvaluator) kept in the data so the checklist reads complete.
            var am1Required = new List<ActionRequirement>
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.InspectThermometer),
                new ActionRequirement(TargetRef.Simple(TargetKind.InspectionDoor), ActionTag.InspectInspectionDoor),
                new ActionRequirement(TargetRef.Simple(TargetKind.GuardRoomReturn), ActionTag.ReturnToGuardRoom),
            };
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    am1Required.Add(new ActionRequirement(TargetRef.Painting(wall, i), ActionTag.ObservePainting));
                }
            }

            var dutyAm1 = CreateDuty("Duty_AM1", TimeSlot.AM1,
                required: am1Required.ToArray(),
                forbidden: System.Array.Empty<ActionRequirement>(),
                ruleText: "1. 오전 1시가 되면 로비를 순찰하며 그림 상태를 확인한다. 기울어진 그림은 바로잡되, 움직이는 그림은 " +
                          "기울어져 있어도 그대로 둔다. 점검을 마치면 점검문·온습도계 상태를 확인한 뒤 경비실로 복귀한다.",
                ruleNumber: 1);

            // Thermometer/InspectionDoor are required here too -- previously only the entrance was,
            // so a round with no active Anomaly silently skipped checking either of them.
            var dutyAm5 = CreateDuty("Duty_AM5", TimeSlot.AM5,
                required: new[]
                {
                    new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.InspectThermometer),
                    new ActionRequirement(TargetRef.Simple(TargetKind.InspectionDoor), ActionTag.InspectInspectionDoor),
                    new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.CheckEntranceDoorClosed),
                },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                ruleText: "2. 오전 5시가 되면 온습도계와 점검문을 확인한 뒤 마지막으로 로비 출입문이 닫혀 있는지 확인한다. " +
                          "단, 다른 수칙이 즉시 복귀를 지시하면 그 수칙을 우선한다.",
                ruleNumber: 2);

            // Wall subject matter is fixed (2026-09-18): North = portraits, West = landscapes,
            // East = abstracts. Portrait/landscape anomalies must resolve to their own wall only.
            var eyesOpenPortrait = CreateAnomaly("EyesOpenPortrait", "눈뜬 초상화",
                slots: new[] { TimeSlot.AM1 }, terminal: false, mirror: false, paintingTarget: true,
                required: System.Array.Empty<ActionRequirement>(),
                forbidden: new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.MakeEyeContact) },
                wallOptions: new[] { PaintingWall.North },
                ruleText: "3. 초상화가 눈을 뜨고 있다면 눈을 마주치지 않는다. 기울어져 있다면 눈을 마주치지 않은 상태에서 바로잡는다.",
                ruleNumber: 3);

            var personInLandscape = CreateAnomaly("PersonInLandscape", "사람이 나타난 풍경화",
                slots: new[] { TimeSlot.AM1 }, terminal: false, mirror: false, paintingTarget: true,
                required: new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.TurnAwayFromExhibit) },
                // FaceExhibit here means "kept looking at it instead of turning away" (see
                // ObservationRuleMonitor.TickForbiddenFacing) -- a longer dwell than the required
                // TurnAwayFromExhibit's own 2s, so the player has real time to notice and react
                // before it counts as ignoring the rule outright.
                forbidden: new[]
                {
                    new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.RecheckExhibit),
                    new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.FaceExhibit)
                },
                wallOptions: new[] { PaintingWall.West },
                ruleText: "4. 풍경화에 사람이 보인다면 즉시 등을 돌리고 잠시 기다린다.",
                ruleNumber: 4);

            var flippedPainting = CreateAnomaly("FlippedPainting", "뒤집힌 그림",
                slots: new[] { TimeSlot.AM1, TimeSlot.AM5 }, terminal: false, mirror: true, paintingTarget: false,
                required: new[] { new ActionRequirement(TargetPlaceholder.MirrorTarget, ActionTag.FlipPainting) },
                forbidden: new[] { new ActionRequirement(TargetPlaceholder.MirrorDiscovered, ActionTag.ModifyOriginalPainting) },
                ruleText: "6. 뒤집힌 그림을 발견하면 원래대로 돌리지 않고, 맞은편 그림을 대신 뒤집어 놓는다. " +
                          "처음 발견한 그림에는 손대지 않는다.",
                ruleNumber: 6);

            var highHumidity = CreateAnomaly("HighHumidity", "습도 70% 이상",
                slots: new[] { TimeSlot.AM5 }, terminal: false, mirror: false, paintingTarget: false,
                required: System.Array.Empty<ActionRequirement>(),
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.AdjustThermostat) },
                ruleText: "5. 로비의 적정 습도는 45~55%다. 정상 범위를 벗어났다면 온습도계를 조작해 맞추되, " +
                          "70% 이상이면 건드리지 않는다.",
                ruleNumber: 5);

            var doorAjar = CreateAnomaly("InspectionDoorAjar", "점검문 반개방",
                slots: new[] { TimeSlot.AM5 }, terminal: false, mirror: false, paintingTarget: false,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.InspectionDoor), ActionTag.CloseInspectionDoorFully) },
                forbidden: System.Array.Empty<ActionRequirement>(),
                ruleText: "7. 점검문이 반쯤 열려 있다면 완전히 닫는다. 활짝 열려 있다면 가까이 가지 말고 즉시 경비실로 복귀한다.",
                ruleNumber: 7);

            var doorWideOpen = CreateAnomaly("InspectionDoorWideOpen", "점검문 완전개방",
                slots: new[] { TimeSlot.AM5 }, terminal: true, mirror: false, paintingTarget: false,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ContinuePatrol) },
                ruleText: "7. 점검문이 반쯤 열려 있다면 완전히 닫는다. 활짝 열려 있다면 가까이 가지 말고 즉시 경비실로 복귀한다.",
                ruleNumber: 7);

            var soundFromExhibit = CreateAnomaly("SoundFromExhibit", "전시물에서 발생하는 소리",
                slots: new[] { TimeSlot.AM5 }, terminal: false, mirror: false, paintingTarget: true,
                required: new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.FaceExhibit) },
                forbidden: new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.ShowBackToExhibit) },
                ruleText: "8. 특정 전시물에서 목소리가 들린다면 순찰이 끝날 때까지 그 전시물에 등을 보이지 않는다.",
                ruleNumber: 8);

            var knockOnDoor = CreateAnomaly("KnockOnDoor", "출입문 노크",
                slots: new[] { TimeSlot.AM5 }, terminal: false, mirror: false, paintingTarget: false,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.KeepDistanceAndWait) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.OperateEntranceDoor) },
                ruleText: "9. 출입문 점검 중 노크 소리가 들리면 문을 열거나 잠금장치를 조작하지 않는다. " +
                          "물러나서 소리가 멎을 때까지 기다린 뒤 점검을 마친다.",
                ruleNumber: 9);

            var ruleSet = ScriptableObject.CreateInstance<CombinationRuleSet>();
            ruleSet.EditorSetRules(new[]
            {
                new CombinationRule
                {
                    AnomalyA = doorAjar, AnomalyB = doorWideOpen, State = CombinationState.Deny,
                    Note = "같은 점검문의 서로 다른 상태 — 물리적으로 동시 존재 불가."
                },
                new CombinationRule
                {
                    AnomalyA = eyesOpenPortrait, AnomalyB = soundFromExhibit, State = CombinationState.Allow,
                    Note = "설계 문서 7번 예시: 같은 그림에 걸려도 정면 유지 + 눈맞춤 회피가 동시에 가능해 검토 완료. " +
                           "기본값과 동일하지만 검토 기록용으로 명시적으로 남김."
                }
            });
            ruleSet = SaveAsset(ruleSet, "CombinationRuleSet");

            var am1Pool = new List<AnomalyDefinition> { eyesOpenPortrait, personInLandscape, flippedPainting };
            var am5Pool = new List<AnomalyDefinition> { highHumidity, flippedPainting, doorAjar, doorWideOpen, soundFromExhibit, knockOnDoor };

            CreateProfile(1, TimeSlot.AM1, dutyAm1, new List<AnomalyDefinition>(), DifficultyRule.None);
            CreateProfile(2, TimeSlot.AM5, dutyAm5, am5Pool, DifficultyRule.SingleZeroOrOne);
            CreateProfile(3, TimeSlot.AM1, dutyAm1, am1Pool, DifficultyRule.SingleOne);
            CreateProfile(4, TimeSlot.AM5, dutyAm5, am5Pool, DifficultyRule.SingleOne);
            CreateProfile(5, TimeSlot.AM1, dutyAm1, am1Pool, DifficultyRule.CompoundOnly);
            CreateProfile(6, TimeSlot.AM5, dutyAm5, am5Pool, DifficultyRule.CompoundOnly);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AnomalyDataBuilder] Anomaly data set built under " + DataFolder);
        }

        private static PatrolDuty CreateDuty(string id, TimeSlot slot, ActionRequirement[] required,
            ActionRequirement[] forbidden, string ruleText = "", int ruleNumber = 0)
        {
            var duty = ScriptableObject.CreateInstance<PatrolDuty>();
            duty.EditorInitialize(id, slot, required, forbidden, ruleText, ruleNumber);
            return SaveAsset(duty, id);
        }

        private static AnomalyDefinition CreateAnomaly(string id, string displayName, TimeSlot[] slots, bool terminal,
            bool mirror, bool paintingTarget, ActionRequirement[] required, ActionRequirement[] forbidden,
            PaintingWall[] wallOptions = null, string ruleText = "", int ruleNumber = 0)
        {
            var anomaly = ScriptableObject.CreateInstance<AnomalyDefinition>();
            anomaly.EditorInitialize(id, displayName, slots, terminal, mirror, paintingTarget, required, forbidden,
                wallOptions, ruleText, ruleNumber);
            return SaveAsset(anomaly, "Anomaly_" + id);
        }

        private static void CreateProfile(int index, TimeSlot slot, PatrolDuty duty, List<AnomalyDefinition> pool,
            DifficultyRule difficulty)
        {
            var profile = ScriptableObject.CreateInstance<PatrolProfile>();
            profile.EditorInitialize(index, slot, duty, pool, difficulty);
            SaveAsset(profile, $"Patrol_{index:00}_{slot}");
        }

        // Updates the existing asset's data in place (via CopySerialized) when one already
        // exists at this path, instead of delete+recreate — that would mint a fresh GUID every
        // run and silently break any scene/asset that already references the old one (e.g. the
        // graybox scene's PatrolTestHarness/PatrolSceneBindings wiring).
        private static T SaveAsset<T>(T asset, string name) where T : ScriptableObject
        {
            string path = $"{DataFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(asset);
                return existing;
            }

            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
