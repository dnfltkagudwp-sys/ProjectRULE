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
    // own text) has no separate generation event in this slice; it is folded into Duty AM1's
    // baseline InspectAllPaintings requirement. If a standalone TiltedPainting anomaly is wanted
    // later, add it as its own AnomalyDefinition + pool entry — nothing else needs to change.
    public static class AnomalyDataBuilder
    {
        private const string DataFolder = "Assets/Data/Anomalies";

        [MenuItem("RuleGhost/Anomalies/Build Anomaly Data Set")]
        public static void Build()
        {
            EnsureFolder(DataFolder);

            var dutyAm1 = CreateDuty("Duty_AM1", TimeSlot.AM1,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings) },
                forbidden: System.Array.Empty<ActionRequirement>());

            var dutyAm5 = CreateDuty("Duty_AM5", TimeSlot.AM5,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.CheckEntranceDoorClosed) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) });

            var eyesOpenPortrait = CreateAnomaly("EyesOpenPortrait", "눈뜬 초상화",
                slots: new[] { TimeSlot.AM1 }, terminal: false, mirror: false, paintingTarget: true,
                required: System.Array.Empty<ActionRequirement>(),
                forbidden: new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.MakeEyeContact) });

            var personInLandscape = CreateAnomaly("PersonInLandscape", "사람이 나타난 풍경화",
                slots: new[] { TimeSlot.AM1 }, terminal: false, mirror: false, paintingTarget: true,
                required: new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.TurnAwayFromExhibit) },
                forbidden: new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.RecheckExhibit) });

            var flippedPainting = CreateAnomaly("FlippedPainting", "뒤집힌 그림",
                slots: new[] { TimeSlot.AM1, TimeSlot.AM5 }, terminal: false, mirror: true, paintingTarget: false,
                required: new[] { new ActionRequirement(TargetPlaceholder.MirrorTarget, ActionTag.FlipPainting) },
                forbidden: new[] { new ActionRequirement(TargetPlaceholder.MirrorDiscovered, ActionTag.ModifyOriginalPainting) });

            var highHumidity = CreateAnomaly("HighHumidity", "습도 70% 이상",
                slots: new[] { TimeSlot.AM5 }, terminal: false, mirror: false, paintingTarget: false,
                required: System.Array.Empty<ActionRequirement>(),
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.TouchThermostat) });

            var doorAjar = CreateAnomaly("InspectionDoorAjar", "점검문 반개방",
                slots: new[] { TimeSlot.AM5 }, terminal: false, mirror: false, paintingTarget: false,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.InspectionDoor), ActionTag.CloseInspectionDoorFully) },
                forbidden: System.Array.Empty<ActionRequirement>());

            var doorWideOpen = CreateAnomaly("InspectionDoorWideOpen", "점검문 완전개방",
                slots: new[] { TimeSlot.AM5 }, terminal: true, mirror: false, paintingTarget: false,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ContinuePatrol) });

            var soundFromExhibit = CreateAnomaly("SoundFromExhibit", "전시물에서 발생하는 소리",
                slots: new[] { TimeSlot.AM5 }, terminal: false, mirror: false, paintingTarget: true,
                required: new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.FaceExhibit) },
                forbidden: new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.ShowBackToExhibit) });

            var knockOnDoor = CreateAnomaly("KnockOnDoor", "출입문 노크",
                slots: new[] { TimeSlot.AM5 }, terminal: false, mirror: false, paintingTarget: false,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.KeepDistanceAndWait) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.OperateEntranceDoor) });

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
            SaveAsset(ruleSet, "CombinationRuleSet");

            var am1Pool = new List<AnomalyDefinition> { eyesOpenPortrait, personInLandscape, flippedPainting };
            var am5Pool = new List<AnomalyDefinition> { highHumidity, flippedPainting, doorAjar, doorWideOpen, soundFromExhibit, knockOnDoor };

            CreateProfile(1, TimeSlot.AM1, dutyAm1, new List<AnomalyDefinition>(), DifficultyRule.None);
            CreateProfile(2, TimeSlot.AM5, dutyAm5, am5Pool, DifficultyRule.SingleZeroOrOne);
            CreateProfile(3, TimeSlot.AM1, dutyAm1, am1Pool, DifficultyRule.SingleOne);
            CreateProfile(4, TimeSlot.AM5, dutyAm5, am5Pool, DifficultyRule.SingleOne);
            CreateProfile(5, TimeSlot.AM1, dutyAm1, am1Pool, DifficultyRule.CompoundOnePlusOptionalSingle);
            CreateProfile(6, TimeSlot.AM5, dutyAm5, am5Pool, DifficultyRule.CompoundOnePlusOptionalSingle);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AnomalyDataBuilder] Anomaly data set built under " + DataFolder);
        }

        private static PatrolDuty CreateDuty(string id, TimeSlot slot, ActionRequirement[] required, ActionRequirement[] forbidden)
        {
            var duty = ScriptableObject.CreateInstance<PatrolDuty>();
            duty.EditorInitialize(id, slot, required, forbidden);
            SaveAsset(duty, id);
            return duty;
        }

        private static AnomalyDefinition CreateAnomaly(string id, string displayName, TimeSlot[] slots, bool terminal,
            bool mirror, bool paintingTarget, ActionRequirement[] required, ActionRequirement[] forbidden)
        {
            var anomaly = ScriptableObject.CreateInstance<AnomalyDefinition>();
            anomaly.EditorInitialize(id, displayName, slots, terminal, mirror, paintingTarget, required, forbidden);
            SaveAsset(anomaly, "Anomaly_" + id);
            return anomaly;
        }

        private static void CreateProfile(int index, TimeSlot slot, PatrolDuty duty, List<AnomalyDefinition> pool,
            DifficultyRule difficulty)
        {
            var profile = ScriptableObject.CreateInstance<PatrolProfile>();
            profile.EditorInitialize(index, slot, duty, pool, difficulty);
            SaveAsset(profile, $"Patrol_{index:00}_{slot}");
        }

        private static void SaveAsset(Object asset, string name)
        {
            string path = $"{DataFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(asset, path);
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
