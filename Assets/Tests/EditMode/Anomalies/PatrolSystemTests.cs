using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuleGhost.Anomalies;
using UnityEngine;
using Random = System.Random;

namespace RuleGhost.Anomalies.Tests
{
    public class PatrolSystemTests
    {
        // Mirrors AnomalyDataBuilder's data shape in-memory, so these tests don't depend on
        // the actual project assets existing / being up to date.
        private class Fixture
        {
            public PatrolDuty DutyAm1, DutyAm5;
            public AnomalyDefinition EyesOpenPortrait, PersonInLandscape, FlippedPainting, HighHumidity,
                DoorAjar, DoorWideOpen, SoundFromExhibit, KnockOnDoor;
            public CombinationRuleSet RuleSet;
            public List<AnomalyDefinition> Am1Pool, Am5Pool;
            public PatrolProfile[] Profiles;
        }

        private static AnomalyDefinition MakeAnomaly(string id, TimeSlot[] slots, bool terminal, bool mirror,
            bool paintingTarget, ActionRequirement[] required, ActionRequirement[] forbidden)
        {
            var def = ScriptableObject.CreateInstance<AnomalyDefinition>();
            def.EditorInitialize(id, id, slots, terminal, mirror, paintingTarget, required, forbidden);
            return def;
        }

        private static PatrolDuty MakeDuty(string id, TimeSlot slot, ActionRequirement[] required, ActionRequirement[] forbidden)
        {
            var duty = ScriptableObject.CreateInstance<PatrolDuty>();
            duty.EditorInitialize(id, slot, required, forbidden);
            return duty;
        }

        private static Fixture BuildFixture()
        {
            var f = new Fixture();

            f.DutyAm1 = MakeDuty("Duty_AM1", TimeSlot.AM1,
                new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings) },
                Array.Empty<ActionRequirement>());

            f.DutyAm5 = MakeDuty("Duty_AM5", TimeSlot.AM5,
                new[] { new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.CheckEntranceDoorClosed) },
                new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) });

            f.EyesOpenPortrait = MakeAnomaly("EyesOpenPortrait", new[] { TimeSlot.AM1 }, false, false, true,
                Array.Empty<ActionRequirement>(),
                new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.MakeEyeContact) });

            f.PersonInLandscape = MakeAnomaly("PersonInLandscape", new[] { TimeSlot.AM1 }, false, false, true,
                new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.TurnAwayFromExhibit) },
                new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.RecheckExhibit) });

            f.FlippedPainting = MakeAnomaly("FlippedPainting", new[] { TimeSlot.AM1, TimeSlot.AM5 }, false, true, false,
                new[] { new ActionRequirement(TargetPlaceholder.MirrorTarget, ActionTag.FlipPainting) },
                new[] { new ActionRequirement(TargetPlaceholder.MirrorDiscovered, ActionTag.ModifyOriginalPainting) });

            f.HighHumidity = MakeAnomaly("HighHumidity", new[] { TimeSlot.AM5 }, false, false, false,
                Array.Empty<ActionRequirement>(),
                new[] { new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.TouchThermostat) });

            f.DoorAjar = MakeAnomaly("InspectionDoorAjar", new[] { TimeSlot.AM5 }, false, false, false,
                new[] { new ActionRequirement(TargetRef.Simple(TargetKind.InspectionDoor), ActionTag.CloseInspectionDoorFully) },
                Array.Empty<ActionRequirement>());

            f.DoorWideOpen = MakeAnomaly("InspectionDoorWideOpen", new[] { TimeSlot.AM5 }, true, false, false,
                new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ContinuePatrol) });

            f.SoundFromExhibit = MakeAnomaly("SoundFromExhibit", new[] { TimeSlot.AM5 }, false, false, true,
                new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.FaceExhibit) },
                new[] { new ActionRequirement(TargetPlaceholder.AnyPainting, ActionTag.ShowBackToExhibit) });

            f.KnockOnDoor = MakeAnomaly("KnockOnDoor", new[] { TimeSlot.AM5 }, false, false, false,
                new[] { new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.KeepDistanceAndWait) },
                new[] { new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.OperateEntranceDoor) });

            f.RuleSet = ScriptableObject.CreateInstance<CombinationRuleSet>();
            f.RuleSet.EditorSetRules(new[]
            {
                new CombinationRule { AnomalyA = f.DoorAjar, AnomalyB = f.DoorWideOpen, State = CombinationState.Deny },
                new CombinationRule { AnomalyA = f.EyesOpenPortrait, AnomalyB = f.SoundFromExhibit, State = CombinationState.Allow }
            });

            f.Am1Pool = new List<AnomalyDefinition> { f.EyesOpenPortrait, f.PersonInLandscape, f.FlippedPainting };
            f.Am5Pool = new List<AnomalyDefinition>
                { f.HighHumidity, f.FlippedPainting, f.DoorAjar, f.DoorWideOpen, f.SoundFromExhibit, f.KnockOnDoor };

            PatrolProfile Profile(int index, TimeSlot slot, PatrolDuty duty, List<AnomalyDefinition> pool, DifficultyRule rule)
            {
                var p = ScriptableObject.CreateInstance<PatrolProfile>();
                p.EditorInitialize(index, slot, duty, pool, rule);
                return p;
            }

            f.Profiles = new[]
            {
                Profile(1, TimeSlot.AM1, f.DutyAm1, new List<AnomalyDefinition>(), DifficultyRule.None),
                Profile(2, TimeSlot.AM5, f.DutyAm5, f.Am5Pool, DifficultyRule.SingleZeroOrOne),
                Profile(3, TimeSlot.AM1, f.DutyAm1, f.Am1Pool, DifficultyRule.SingleOne),
                Profile(4, TimeSlot.AM5, f.DutyAm5, f.Am5Pool, DifficultyRule.SingleOne),
                Profile(5, TimeSlot.AM1, f.DutyAm1, f.Am1Pool, DifficultyRule.CompoundOnePlusOptionalSingle),
                Profile(6, TimeSlot.AM5, f.DutyAm5, f.Am5Pool, DifficultyRule.CompoundOnePlusOptionalSingle)
            };

            return f;
        }

        [Test]
        public void MirrorPairTable_ReturnsFixedPairs()
        {
            Assert.AreEqual(TargetRef.Painting(PaintingWall.East, 2),
                MirrorPairTable.GetMirror(TargetRef.Painting(PaintingWall.West, 2)));
            Assert.AreEqual(TargetRef.Painting(PaintingWall.West, 3),
                MirrorPairTable.GetMirror(TargetRef.Painting(PaintingWall.East, 3)));
        }

        [Test]
        public void MirrorPairTable_ThrowsForNorthWall()
        {
            Assert.Throws<ArgumentException>(() => MirrorPairTable.GetMirror(TargetRef.Painting(PaintingWall.North, 1)));
        }

        [Test]
        public void CombinationRuleSet_DefaultsToAllow()
        {
            var f = BuildFixture();
            Assert.AreEqual(CombinationState.Allow, f.RuleSet.GetState(f.HighHumidity, f.KnockOnDoor));
        }

        [Test]
        public void CombinationRuleSet_ExplicitAllow_IsRecorded()
        {
            var f = BuildFixture();
            Assert.AreEqual(CombinationState.Allow, f.RuleSet.GetState(f.EyesOpenPortrait, f.SoundFromExhibit));
        }

        [Test]
        public void ConflictValidator_MatrixDeny_BlocksCombination()
        {
            var f = BuildFixture();
            var rng = new Random(1);
            var ajar = AnomalyTargetResolver.Resolve(f.DoorAjar, rng);
            var wideOpen = AnomalyTargetResolver.Resolve(f.DoorWideOpen, rng);

            bool canAdd = ConflictValidator.CanAdd(wideOpen, new List<IActionConstraint> { f.DutyAm5, ajar }, f.RuleSet, out var reason);

            Assert.IsFalse(canAdd);
            StringAssert.Contains("matrix DENY", reason);
        }

        [Test]
        public void ConflictValidator_TerminalExclusion_BlocksAnomalyWithRequiredAction()
        {
            var f = BuildFixture();
            var rng = new Random(2);
            var wideOpen = AnomalyTargetResolver.Resolve(f.DoorWideOpen, rng);
            var personInLandscape = AnomalyTargetResolver.Resolve(f.PersonInLandscape, rng);

            bool canAdd = ConflictValidator.CanAdd(wideOpen, new List<IActionConstraint> { f.DutyAm5, personInLandscape }, f.RuleSet, out var reason);

            Assert.IsFalse(canAdd);
            StringAssert.Contains("terminal exclusion", reason);
        }

        [Test]
        public void ConflictValidator_TerminalCandidate_OverridesPatrolDuty()
        {
            // DoorWideOpen requires ReturnToGuardRoom on WholePatrol; Duty_AM5 forbids exactly
            // that same (target, action) pair before the entrance check is done. A Terminal
            // anomaly must still be addable — the work rules say an override instruction wins.
            var f = BuildFixture();
            var rng = new Random(3);
            var wideOpen = AnomalyTargetResolver.Resolve(f.DoorWideOpen, rng);

            bool canAdd = ConflictValidator.CanAdd(wideOpen, new List<IActionConstraint> { f.DutyAm5 }, f.RuleSet, out var reason);

            Assert.IsTrue(canAdd, reason);
        }

        [Test]
        public void ConflictValidator_SameTargetCompatibleActions_Allowed()
        {
            // Design-doc example: eyes-open portrait + sound-from-exhibit on the SAME painting
            // must be allowed since "keep facing" and "no eye contact" don't actually conflict.
            var f = BuildFixture();
            var sharedTarget = TargetRef.Painting(PaintingWall.North, 1);

            var eyesOpen = new ResolvedAnomaly(f.EyesOpenPortrait, Array.Empty<ActionRequirement>(),
                new[] { new ActionRequirement(sharedTarget, ActionTag.MakeEyeContact) });
            var sound = new ResolvedAnomaly(f.SoundFromExhibit,
                new[] { new ActionRequirement(sharedTarget, ActionTag.FaceExhibit) },
                new[] { new ActionRequirement(sharedTarget, ActionTag.ShowBackToExhibit) });

            bool canAdd = ConflictValidator.CanAdd(sound, new List<IActionConstraint> { f.DutyAm5, eyesOpen }, f.RuleSet, out var reason);

            Assert.IsTrue(canAdd, reason);
        }

        [Test]
        public void ConflictValidator_SameTargetConflictingActions_Denied()
        {
            var sharedTarget = TargetRef.Painting(PaintingWall.West, 1);
            var f = BuildFixture();

            var requiresFlip = new ResolvedAnomaly(f.FlippedPainting,
                new[] { new ActionRequirement(sharedTarget, ActionTag.FlipPainting) }, Array.Empty<ActionRequirement>());
            var forbidsFlip = new ResolvedAnomaly(f.PersonInLandscape, Array.Empty<ActionRequirement>(),
                new[] { new ActionRequirement(sharedTarget, ActionTag.FlipPainting) });

            bool canAdd = ConflictValidator.CanAdd(forbidsFlip, new List<IActionConstraint> { f.DutyAm1, requiresFlip }, f.RuleSet, out var reason);

            Assert.IsFalse(canAdd);
            StringAssert.Contains("both requires and forbids", reason);
        }

        [Test]
        public void PatrolGenerator_Day1AM1_NeverProducesAnomalies()
        {
            var f = BuildFixture();
            var rng = new Random(42);
            var result = PatrolGenerator.Generate(f.Profiles[0], f.RuleSet, rng);
            Assert.IsEmpty(result.Anomalies);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void PatrolGenerator_SingleOneProfiles_AlwaysProduceExactlyOne(int seed)
        {
            var f = BuildFixture();
            var rng = new Random(seed);
            var result = PatrolGenerator.Generate(f.Profiles[2], f.RuleSet, rng); // day2 AM1, SingleOne
            Assert.AreEqual(1, result.Anomalies.Count);
        }

        [Test]
        public void PatrolGenerator_AllProfiles_RepeatedRuns_NeverThrowAndRespectDifficultyBounds()
        {
            var f = BuildFixture();
            var rng = new Random(1234);

            foreach (var profile in f.Profiles)
            {
                for (int i = 0; i < 200; i++)
                {
                    PatrolGenerationResult result = null;
                    Assert.DoesNotThrow(() => result = PatrolGenerator.Generate(profile, f.RuleSet, rng));

                    int max = profile.Difficulty switch
                    {
                        DifficultyRule.None => 0,
                        DifficultyRule.SingleZeroOrOne => 1,
                        DifficultyRule.SingleOne => 1,
                        DifficultyRule.CompoundOnePlusOptionalSingle => 3,
                        _ => 0
                    };
                    int min = profile.Difficulty == DifficultyRule.SingleOne ? 1
                        : profile.Difficulty == DifficultyRule.CompoundOnePlusOptionalSingle ? 2
                        : 0;

                    Assert.GreaterOrEqual(result.Anomalies.Count, min);
                    Assert.LessOrEqual(result.Anomalies.Count, max);

                    // No two identical AnomalyDefinition entries in the same round.
                    var distinctSources = result.Anomalies.Select(a => a.Source).Distinct().Count();
                    Assert.AreEqual(result.Anomalies.Count, distinctSources);
                }
            }
        }
    }
}
