using System;
using NUnit.Framework;
using RuleGhost.Anomalies;
using UnityEngine;

namespace RuleGhost.Anomalies.Tests
{
    public class PatrolEvaluatorTests
    {
        private static PatrolDuty MakeDuty(ActionRequirement[] required, ActionRequirement[] forbidden = null)
        {
            var duty = ScriptableObject.CreateInstance<PatrolDuty>();
            duty.EditorInitialize("Duty_Test", TimeSlot.AM1, required, forbidden ?? Array.Empty<ActionRequirement>());
            return duty;
        }

        private static ResolvedAnomaly MakeAnomaly(string id, ActionRequirement[] required = null,
            ActionRequirement[] forbidden = null, bool isTerminal = false)
        {
            var def = ScriptableObject.CreateInstance<AnomalyDefinition>();
            def.EditorInitialize(id, id, Array.Empty<TimeSlot>(), isTerminal, false, false,
                required ?? Array.Empty<ActionRequirement>(), forbidden ?? Array.Empty<ActionRequirement>());
            return new ResolvedAnomaly(def, required ?? Array.Empty<ActionRequirement>(),
                forbidden ?? Array.Empty<ActionRequirement>());
        }

        [Test]
        public void InspectAllPaintings_AllNineChecked_EntranceLast_Succeeds()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });

            var progress = new PatrolProgress();
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    progress.RecordVisit(TargetRef.Painting(wall, i));
                }
            }
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress);

            Assert.IsTrue(result.Success);
            Assert.IsEmpty(result.MissingTargets);
            Assert.IsFalse(result.EntranceNotLast);
        }

        [Test]
        public void MissingPainting_FailsAndListsIt()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });

            var progress = new PatrolProgress();
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    if (wall == PaintingWall.East && i == 3) continue; // deliberately skip one
                    progress.RecordVisit(TargetRef.Painting(wall, i));
                }
            }
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress);

            Assert.IsFalse(result.Success);
            Assert.Contains(TargetRef.Painting(PaintingWall.East, 3), (System.Collections.ICollection)result.MissingTargets);
        }

        [Test]
        public void EntranceCheckedButNotLast_Fails()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.TouchThermostat)
            });

            var progress = new PatrolProgress();
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));
            progress.RecordVisit(TargetRef.Simple(TargetKind.Thermometer));

            var result = PatrolEvaluator.Evaluate(duty, progress);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.EntranceNotLast);
            Assert.IsEmpty(result.MissingTargets);
        }

        [Test]
        public void EntranceNeverChecked_ListedAsMissing()
        {
            var duty = MakeDuty(Array.Empty<ActionRequirement>());
            var progress = new PatrolProgress();

            var result = PatrolEvaluator.Evaluate(duty, progress);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.EntranceNotLast);
            Assert.Contains(TargetRef.Simple(TargetKind.EntranceDoor), (System.Collections.ICollection)result.MissingTargets);
        }

        [Test]
        public void HighHumidity_ThermostatTouched_FailsAsForbiddenAction()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var highHumidity = MakeAnomaly("HighHumidity", forbidden: new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.TouchThermostat)
            });

            var progress = new PatrolProgress();
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    progress.RecordVisit(TargetRef.Painting(wall, i));
                }
            }
            progress.RecordVisit(TargetRef.Simple(TargetKind.Thermometer));
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { highHumidity });

            Assert.IsFalse(result.Success);
            Assert.Contains("HighHumidity", result.ForbiddenAnomalyActions);
        }

        [Test]
        public void HighHumidity_ThermostatNotTouched_DoesNotRequireVisitingIt()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var highHumidity = MakeAnomaly("HighHumidity", forbidden: new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.TouchThermostat)
            });

            var progress = new PatrolProgress();
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    progress.RecordVisit(TargetRef.Painting(wall, i));
                }
            }
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { highHumidity });

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void InspectionDoorAjar_RequiresVisitingDoorEvenThoughBaseDutyDoesNot()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.CheckEntranceDoorClosed)
            });
            var doorAjar = MakeAnomaly("InspectionDoorAjar", required: new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.InspectionDoor), ActionTag.CloseInspectionDoorFully)
            });

            var progress = new PatrolProgress();
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { doorAjar });

            Assert.IsFalse(result.Success);
            Assert.Contains(TargetRef.Simple(TargetKind.InspectionDoor), (System.Collections.ICollection)result.MissingTargets);
        }

        [Test]
        public void TerminalAnomaly_GoesStraightToEntrance_Succeeds()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var doorWideOpen = MakeAnomaly("InspectionDoorWideOpen", isTerminal: true,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ContinuePatrol) });

            var progress = new PatrolProgress();
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { doorWideOpen });

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void TerminalAnomaly_ChecksTheOpenDoorItselfFirst_StillSucceeds()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var doorWideOpen = MakeAnomaly("InspectionDoorWideOpen", isTerminal: true,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ContinuePatrol) });

            var progress = new PatrolProgress();
            progress.RecordVisit(TargetRef.Simple(TargetKind.InspectionDoor));
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { doorWideOpen });

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void TerminalAnomaly_ContinuesPatrolInstead_Fails()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var doorWideOpen = MakeAnomaly("InspectionDoorWideOpen", isTerminal: true,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ContinuePatrol) });

            var progress = new PatrolProgress();
            progress.RecordVisit(TargetRef.Painting(PaintingWall.North, 1));
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { doorWideOpen });

            Assert.IsFalse(result.Success);
            Assert.Contains("InspectionDoorWideOpen", result.ForbiddenAnomalyActions);
        }
    }
}
