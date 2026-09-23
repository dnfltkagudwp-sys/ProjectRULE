using System;
using System.Collections.Generic;
using NUnit.Framework;
using RuleGhost.Anomalies;
using UnityEngine;

namespace RuleGhost.Anomalies.Tests
{
    public class RulebookTextBuilderTests
    {
        private static PatrolDuty MakeDuty(string ruleText, int ruleNumber, TimeSlot slot = TimeSlot.AM1)
        {
            var duty = ScriptableObject.CreateInstance<PatrolDuty>();
            duty.EditorInitialize("Duty_Test", slot, Array.Empty<ActionRequirement>(),
                Array.Empty<ActionRequirement>(), ruleText, ruleNumber);
            return duty;
        }

        private static AnomalyDefinition MakeAnomaly(string id, string ruleText, int ruleNumber)
        {
            var def = ScriptableObject.CreateInstance<AnomalyDefinition>();
            def.EditorInitialize(id, id, Array.Empty<TimeSlot>(), false, false, false,
                Array.Empty<ActionRequirement>(), Array.Empty<ActionRequirement>(), null, ruleText, ruleNumber);
            return def;
        }

        private static PatrolProfile MakeProfile(PatrolDuty duty, params AnomalyDefinition[] pool)
        {
            var profile = ScriptableObject.CreateInstance<PatrolProfile>();
            profile.EditorInitialize(1, duty.Slot, duty, new List<AnomalyDefinition>(pool), DifficultyRule.None);
            return profile;
        }

        [Test]
        public void SingleProfile_DutyAndAnomalies_SortedByRuleNumber()
        {
            var duty = MakeDuty("규칙 1", 1);
            var a = MakeAnomaly("A", "규칙 3", 3);
            var b = MakeAnomaly("B", "규칙 2", 2); // out of insertion order on purpose
            var profile = MakeProfile(duty, a, b);

            var result = RulebookTextBuilder.BuildAll(new[] { profile });

            Assert.AreEqual("규칙 1\n\n규칙 2\n\n규칙 3", result);
        }

        [Test]
        public void MultipleProfiles_ShowsRulesFromAllOfThem()
        {
            var dutyAm1 = MakeDuty("규칙 1", 1, TimeSlot.AM1);
            var dutyAm5 = MakeDuty("규칙 2", 2, TimeSlot.AM5);
            var anomaly = MakeAnomaly("C", "규칙 5", 5);
            var profile1 = MakeProfile(dutyAm1);
            var profile2 = MakeProfile(dutyAm5, anomaly);

            var result = RulebookTextBuilder.BuildAll(new[] { profile1, profile2 });

            Assert.AreEqual("규칙 1\n\n규칙 2\n\n규칙 5", result);
        }

        [Test]
        public void AnomalySharedAcrossProfiles_AppearsOnlyOnce()
        {
            // Mirrors InspectionDoorAjar/InspectionDoorWideOpen sharing rule 7's text, and
            // FlippedPainting appearing in both the AM1 and AM5 pools.
            var duty = MakeDuty("규칙 1", 1);
            var shared = MakeAnomaly("Shared", "규칙 6", 6);
            var profile1 = MakeProfile(duty, shared);
            var profile2 = MakeProfile(duty, shared);

            var result = RulebookTextBuilder.BuildAll(new[] { profile1, profile2 });

            Assert.AreEqual("규칙 1\n\n규칙 6", result);
        }

        [Test]
        public void AnomalyWithNoRuleNumber_IsSkipped()
        {
            var duty = MakeDuty("규칙 1", 1);
            var noNumber = MakeAnomaly("NoNumber", "이 텍스트는 안 보여야 함", 0);
            var profile = MakeProfile(duty, noNumber);

            var result = RulebookTextBuilder.BuildAll(new[] { profile });

            Assert.AreEqual("규칙 1", result);
        }

        [Test]
        public void NullProfiles_ReturnsEmptyString()
        {
            var result = RulebookTextBuilder.BuildAll(null);

            Assert.AreEqual(string.Empty, result);
        }
    }
}
