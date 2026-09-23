using System;
using System.Linq;
using NUnit.Framework;
using RuleGhost.Anomalies;
using UnityEngine;

namespace RuleGhost.Anomalies.Tests
{
    public class PatrolEvaluatorTests
    {
        // Slotted AM5 rather than AM1: most tests here are about generic judgment (anomalies,
        // recheck, observation) and just need "the entrance is required and must be last," which
        // is now an AM5-specific structural rule (see PatrolEvaluator.RequiresEntranceLast) --
        // AM1's own closing rule is exercised separately by the AM1_* tests below via MakeAm1Duty.
        private static PatrolDuty MakeDuty(ActionRequirement[] required, ActionRequirement[] forbidden = null)
        {
            var duty = ScriptableObject.CreateInstance<PatrolDuty>();
            duty.EditorInitialize("Duty_Test", TimeSlot.AM5, required, forbidden ?? Array.Empty<ActionRequirement>());
            return duty;
        }

        private static PatrolDuty MakeAm1Duty(ActionRequirement[] required, ActionRequirement[] forbidden = null)
        {
            var duty = ScriptableObject.CreateInstance<PatrolDuty>();
            duty.EditorInitialize("Duty_AM1_Test", TimeSlot.AM1, required, forbidden ?? Array.Empty<ActionRequirement>());
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

        private static TargetRef[] AllPaintingTargets()
        {
            var list = new System.Collections.Generic.List<TargetRef>();
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    list.Add(TargetRef.Painting(wall, i));
                }
            }
            return list.ToArray();
        }

        // Mirrors AnomalyDataBuilder's real Duty_AM1 shape (9 ObservePainting + facility checks +
        // the tautological GuardRoomReturn entry) so these tests exercise the actual production
        // structure, not a simplified stand-in.
        private static ActionRequirement[] Am1BaselineRequired()
        {
            var list = new System.Collections.Generic.List<ActionRequirement>
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.InspectThermometer),
                new ActionRequirement(TargetRef.Simple(TargetKind.InspectionDoor), ActionTag.InspectInspectionDoor),
                new ActionRequirement(TargetRef.Simple(TargetKind.GuardRoomReturn), ActionTag.ReturnToGuardRoom),
            };
            foreach (var target in AllPaintingTargets())
            {
                list.Add(new ActionRequirement(target, ActionTag.ObservePainting));
            }
            return list.ToArray();
        }

        private static ActionRequirement[] Am5BaselineRequired() => new[]
        {
            new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.InspectThermometer),
            new ActionRequirement(TargetRef.Simple(TargetKind.InspectionDoor), ActionTag.InspectInspectionDoor),
            new ActionRequirement(TargetRef.Simple(TargetKind.EntranceDoor), ActionTag.CheckEntranceDoorClosed),
        };

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
                new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.AdjustThermostat)
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
                new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.AdjustThermostat)
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
                new ActionRequirement(TargetRef.Simple(TargetKind.Thermometer), ActionTag.AdjustThermostat)
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
        public void TerminalAnomaly_ReachedGuardRoomWithoutContinuing_Succeeds()
        {
            // terminalAbortViolated defaults to false -- TerminalAbortState only ever sets it once
            // the player has both discovered the wide-open door (a live gaze dwell) AND then
            // visited something else, neither of which happened here, so PatrolProgress content is
            // irrelevant to this verdict (Evaluate is only ever reached once the player has already
            // arrived at the guard room in the first place -- see PatrolEvaluator.EvaluateTerminalAbort).
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var doorWideOpen = MakeAnomaly("InspectionDoorWideOpen", isTerminal: true,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ContinuePatrol) });

            var result = PatrolEvaluator.Evaluate(duty, new PatrolProgress(), new[] { doorWideOpen }, terminalAbortViolated: false);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void EyesOpenPortrait_EyeContactViolated_Fails()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var eyesOpen = MakeAnomaly("EyesOpenPortrait", forbidden: new[]
            {
                new ActionRequirement(TargetRef.Painting(PaintingWall.North, 1), ActionTag.MakeEyeContact)
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

            var observation = new ObservationReport
            {
                ViolatedForbiddenAnomalyIds = new System.Collections.Generic.HashSet<string> { "EyesOpenPortrait" }
            };

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { eyesOpen }, observation);

            Assert.IsFalse(result.Success);
            Assert.Contains("EyesOpenPortrait", result.ForbiddenAnomalyActions);
        }

        [Test]
        public void EyesOpenPortrait_NoEyeContact_Succeeds()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var eyesOpen = MakeAnomaly("EyesOpenPortrait", forbidden: new[]
            {
                new ActionRequirement(TargetRef.Painting(PaintingWall.North, 1), ActionTag.MakeEyeContact)
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

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { eyesOpen }, ObservationReport.Empty);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void PersonInLandscape_NeverTurnedAway_MissingObservation()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var personInLandscape = MakeAnomaly("PersonInLandscape", required: new[]
            {
                new ActionRequirement(TargetRef.Painting(PaintingWall.West, 1), ActionTag.TurnAwayFromExhibit)
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

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { personInLandscape }, ObservationReport.Empty);

            Assert.IsFalse(result.Success);
            Assert.Contains("PersonInLandscape", result.MissingObservations);
        }

        [Test]
        public void PersonInLandscape_TurnedAway_Succeeds()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var personInLandscape = MakeAnomaly("PersonInLandscape", required: new[]
            {
                new ActionRequirement(TargetRef.Painting(PaintingWall.West, 1), ActionTag.TurnAwayFromExhibit)
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

            var observation = new ObservationReport
            {
                SatisfiedRequiredAnomalyIds = new System.Collections.Generic.HashSet<string> { "PersonInLandscape" }
            };

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { personInLandscape }, observation);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void PersonInLandscape_RecheckedAfterTurningAway_FailsAsForbiddenAction()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var target = TargetRef.Painting(PaintingWall.West, 1);
            var personInLandscape = MakeAnomaly("PersonInLandscape",
                required: new[] { new ActionRequirement(target, ActionTag.TurnAwayFromExhibit) },
                forbidden: new[] { new ActionRequirement(target, ActionTag.RecheckExhibit) });

            var progress = new PatrolProgress();
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    progress.RecordVisit(TargetRef.Painting(wall, i));
                }
            }
            progress.RecordVisit(target); // went back for a second look
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var observation = new ObservationReport
            {
                SatisfiedRequiredAnomalyIds = new System.Collections.Generic.HashSet<string> { "PersonInLandscape" }
            };

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { personInLandscape }, observation);

            Assert.IsFalse(result.Success);
            Assert.Contains("PersonInLandscape", result.ForbiddenAnomalyActions);
        }

        [Test]
        public void PersonInLandscape_NotRechecked_SucceedsEvenWithForbiddenRecheckDefined()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var target = TargetRef.Painting(PaintingWall.West, 1);
            var personInLandscape = MakeAnomaly("PersonInLandscape",
                required: new[] { new ActionRequirement(target, ActionTag.TurnAwayFromExhibit) },
                forbidden: new[] { new ActionRequirement(target, ActionTag.RecheckExhibit) });

            var progress = new PatrolProgress();
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    progress.RecordVisit(TargetRef.Painting(wall, i));
                }
            }
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var observation = new ObservationReport
            {
                SatisfiedRequiredAnomalyIds = new System.Collections.Generic.HashSet<string> { "PersonInLandscape" }
            };

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { personInLandscape }, observation);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void RequiredRecheck_NeverDone_ListedAsMissingRecheck()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var target = TargetRef.Painting(PaintingWall.East, 2);
            var needsRecheck = MakeAnomaly("NeedsRecheck", required: new[]
            {
                new ActionRequirement(target, ActionTag.RecheckExhibit)
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

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { needsRecheck });

            Assert.IsFalse(result.Success);
            Assert.Contains("NeedsRecheck", result.MissingRechecks);
        }

        [Test]
        public void RequiredRecheck_DoneTwice_Succeeds()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var target = TargetRef.Painting(PaintingWall.East, 2);
            var needsRecheck = MakeAnomaly("NeedsRecheck", required: new[]
            {
                new ActionRequirement(target, ActionTag.RecheckExhibit)
            });

            var progress = new PatrolProgress();
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    progress.RecordVisit(TargetRef.Painting(wall, i));
                }
            }
            progress.RecordVisit(target); // the recheck
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { needsRecheck });

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void SoundFromExhibit_ShowedBack_FailsAsForbiddenAction()
        {
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var soundFromExhibit = MakeAnomaly("SoundFromExhibit",
                required: new[] { new ActionRequirement(TargetRef.Painting(PaintingWall.East, 2), ActionTag.FaceExhibit) },
                forbidden: new[] { new ActionRequirement(TargetRef.Painting(PaintingWall.East, 2), ActionTag.ShowBackToExhibit) });

            var progress = new PatrolProgress();
            foreach (var wall in new[] { PaintingWall.North, PaintingWall.West, PaintingWall.East })
            {
                for (int i = 1; i <= 3; i++)
                {
                    progress.RecordVisit(TargetRef.Painting(wall, i));
                }
            }
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var observation = new ObservationReport
            {
                SatisfiedRequiredAnomalyIds = new System.Collections.Generic.HashSet<string> { "SoundFromExhibit" },
                ViolatedForbiddenAnomalyIds = new System.Collections.Generic.HashSet<string> { "SoundFromExhibit" }
            };

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { soundFromExhibit }, observation);

            Assert.IsFalse(result.Success);
            Assert.Contains("SoundFromExhibit", result.ForbiddenAnomalyActions);
        }

        [Test]
        public void TerminalAnomaly_ContinuesPatrolInstead_Fails()
        {
            // Whether the player "continued the patrol instead" is decided live by
            // TerminalAbortState (discovery-gated), not re-derived from PatrolProgress.VisitOrder
            // here -- so this simulates the live tracker having already caught that via the
            // terminalAbortViolated flag, the same way PatrolRuntimeController would pass it.
            var duty = MakeDuty(new[]
            {
                new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.InspectAllPaintings)
            });
            var doorWideOpen = MakeAnomaly("InspectionDoorWideOpen", isTerminal: true,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ContinuePatrol) });

            var progress = new PatrolProgress();

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { doorWideOpen }, terminalAbortViolated: true);

            Assert.IsFalse(result.Success);
            Assert.Contains("InspectionDoorWideOpen", result.ForbiddenAnomalyActions);
        }

        // --- AM1: paintings, the inspection door AND the thermometer are all observed (gaze), not
        // E-key visited -- pressing E on the door or thermometer means a real action instead
        // (CloseInspectionDoorFully / AdjustThermostat/AdjustHumidity), so their baseline "확인"
        // must never be E-key based -- closes on GuardRoomReturn ---

        // The 9 paintings plus the inspection door and thermometer -- everything
        // InspectInspectionDoor/InspectThermometer/ObservePainting require via
        // LooseObservationTracker in both Am1BaselineRequired and Am5BaselineRequired.
        private static TargetRef[] AllLooseObservationTargets()
        {
            var list = new System.Collections.Generic.List<TargetRef>(AllPaintingTargets())
            {
                TargetRef.Simple(TargetKind.InspectionDoor),
                TargetRef.Simple(TargetKind.Thermometer)
            };
            return list.ToArray();
        }

        [Test]
        public void AM1_AllPaintingsObservedAndFacilitiesChecked_Succeeds()
        {
            var duty = MakeAm1Duty(Am1BaselineRequired());
            var progress = new PatrolProgress();
            // Nothing is ever E-key visited here -- only gaze-observed -- which is exactly the
            // point: none of the baseline checks need an E-key press any more.

            var result = PatrolEvaluator.Evaluate(duty, progress, observedTargets: AllLooseObservationTargets());

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.EntranceNotLast); // AM1 has no entrance-last rule at all
        }

        [Test]
        public void AM1_ReturningWithoutObservingEveryPainting_Fails()
        {
            var duty = MakeAm1Duty(Am1BaselineRequired());
            var progress = new PatrolProgress();

            var observed = AllLooseObservationTargets().Where(t => !(t.Wall == PaintingWall.East && t.Index == 3)).ToArray();

            var result = PatrolEvaluator.Evaluate(duty, progress, observedTargets: observed);

            Assert.IsFalse(result.Success);
            Assert.Contains(TargetRef.Painting(PaintingWall.East, 3), (System.Collections.ICollection)result.MissingTargets);
        }

        [Test]
        public void AM1_ThermometerNotObservedAndDoorNotObserved_FailsAndListsBoth()
        {
            var duty = MakeAm1Duty(Am1BaselineRequired());
            var progress = new PatrolProgress();

            var result = PatrolEvaluator.Evaluate(duty, progress, observedTargets: AllPaintingTargets());

            Assert.IsFalse(result.Success);
            Assert.Contains(TargetRef.Simple(TargetKind.Thermometer), (System.Collections.ICollection)result.MissingTargets);
            Assert.Contains(TargetRef.Simple(TargetKind.InspectionDoor), (System.Collections.ICollection)result.MissingTargets);
        }

        [Test]
        public void AM1_TiltedPaintingLeftUnfixed_Fails()
        {
            var duty = MakeAm1Duty(Am1BaselineRequired());
            var progress = new PatrolProgress();

            var routine = new RoutinePatrolState();
            routine.EditorForceState(TargetRef.Painting(PaintingWall.North, 2), humidityNeedsAdjustment: false);

            var result = PatrolEvaluator.Evaluate(duty, progress, observedTargets: AllLooseObservationTargets(), routine: routine);

            Assert.IsFalse(result.Success);
            Assert.Contains("TiltedPainting", result.MissingRoutineTasks);
        }

        [Test]
        public void AM1_TiltedPaintingStraightened_Succeeds()
        {
            var duty = MakeAm1Duty(Am1BaselineRequired());
            var tilted = TargetRef.Painting(PaintingWall.North, 2);
            var progress = new PatrolProgress();
            progress.RecordAction(tilted, ActionTag.StraightenPainting);

            var routine = new RoutinePatrolState();
            routine.EditorForceState(tilted, humidityNeedsAdjustment: false);

            var result = PatrolEvaluator.Evaluate(duty, progress, observedTargets: AllLooseObservationTargets(), routine: routine);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void RoutineHumidity_LeftUnadjusted_Fails()
        {
            var duty = MakeAm1Duty(Am1BaselineRequired());
            var progress = new PatrolProgress(); // observed the thermometer, never adjusted it

            var routine = new RoutinePatrolState();
            routine.EditorForceState(null, humidityNeedsAdjustment: true);

            var result = PatrolEvaluator.Evaluate(duty, progress, observedTargets: AllLooseObservationTargets(), routine: routine);

            Assert.IsFalse(result.Success);
            Assert.Contains("RoutineHumidity", result.MissingRoutineTasks);
        }

        [Test]
        public void RoutineHumidity_Adjusted_Succeeds()
        {
            var duty = MakeAm1Duty(Am1BaselineRequired());
            var progress = new PatrolProgress();
            progress.RecordAction(TargetRef.Simple(TargetKind.Thermometer), ActionTag.AdjustHumidity);

            var routine = new RoutinePatrolState();
            routine.EditorForceState(null, humidityNeedsAdjustment: true);

            var result = PatrolEvaluator.Evaluate(duty, progress, observedTargets: AllLooseObservationTargets(), routine: routine);

            Assert.IsTrue(result.Success);
        }

        // --- AM5: thermometer/door are required every round; entrance must come last ---

        [Test]
        public void AM5_ThermometerNotChecked_Fails()
        {
            var duty = MakeDuty(Am5BaselineRequired());
            var progress = new PatrolProgress();
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress,
                observedTargets: new[] { TargetRef.Simple(TargetKind.InspectionDoor) });

            Assert.IsFalse(result.Success);
            Assert.Contains(TargetRef.Simple(TargetKind.Thermometer), (System.Collections.ICollection)result.MissingTargets);
        }

        [Test]
        public void AM5_InspectionDoorNotObserved_Fails()
        {
            var duty = MakeDuty(Am5BaselineRequired());
            var progress = new PatrolProgress();
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress,
                observedTargets: new[] { TargetRef.Simple(TargetKind.Thermometer) });

            Assert.IsFalse(result.Success);
            Assert.Contains(TargetRef.Simple(TargetKind.InspectionDoor), (System.Collections.ICollection)result.MissingTargets);
        }

        [Test]
        public void AM5_ThermometerAndDoorObserved_EntranceLast_Succeeds()
        {
            var duty = MakeDuty(Am5BaselineRequired());
            var progress = new PatrolProgress();
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress,
                observedTargets: new[] { TargetRef.Simple(TargetKind.InspectionDoor), TargetRef.Simple(TargetKind.Thermometer) });

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void AM5_EntranceCheckedFirst_Fails()
        {
            var duty = MakeDuty(Am5BaselineRequired());
            var progress = new PatrolProgress();
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));
            progress.RecordVisit(TargetRef.Simple(TargetKind.Thermometer)); // any later E-key action just needs to come after

            var result = PatrolEvaluator.Evaluate(duty, progress,
                observedTargets: new[] { TargetRef.Simple(TargetKind.InspectionDoor), TargetRef.Simple(TargetKind.Thermometer) });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.EntranceNotLast);
        }

        [Test]
        public void AM5_WideOpenTerminal_ExemptsNormalDutyRequirements()
        {
            var duty = MakeDuty(Am5BaselineRequired());
            var doorWideOpen = MakeAnomaly("InspectionDoorWideOpen", isTerminal: true,
                required: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ReturnToGuardRoom) },
                forbidden: new[] { new ActionRequirement(TargetRef.Simple(TargetKind.WholePatrol), ActionTag.ContinuePatrol) });

            var progress = new PatrolProgress();
            // Never visits Thermometer or observes the InspectionDoor -- would fail a normal AM5
            // round, but the terminal override means the normal Duty requirements aren't asked
            // for at all.
            progress.RecordVisit(TargetRef.Simple(TargetKind.EntranceDoor));

            var result = PatrolEvaluator.Evaluate(duty, progress, new[] { doorWideOpen });

            Assert.IsTrue(result.Success);
        }
    }
}
