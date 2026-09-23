using System.Collections.Generic;
using UnityEngine;

namespace RuleGhost.Anomalies
{
    [CreateAssetMenu(menuName = "RuleGhost/Anomalies/Anomaly Definition", fileName = "Anomaly_")]
    public class AnomalyDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private List<TimeSlot> allowedSlots = new();
        [SerializeField] private bool isTerminal;
        [SerializeField] private bool usesMirrorPairing;
        [SerializeField] private bool needsPaintingTarget;
        // Wall subject matter is fixed: North = portraits, West = landscapes, East = abstracts
        // (confirmed 2026-09-18). Empty means "any wall" (e.g. SoundFromExhibit isn't tied to a
        // specific painting subject).
        [SerializeField] private List<PaintingWall> paintingWallOptions = new();
        [SerializeField] private List<ActionRequirement> requiredActions = new();
        [SerializeField] private List<ActionRequirement> forbiddenActions = new();
        // Player-facing rule sentence, verbatim from the design doc (e.g. "풍경화에 사람이 보인다면...")
        // -- for the 규칙서 UI. Distinct from DisplayName, which is only a short label ("사람이 나타난 풍경화").
        [SerializeField, TextArea] private string ruleText;
        // The design doc's own rule number (3-9) -- lets the 규칙서 UI show every rule in the
        // original 1-9 order. InspectionDoorAjar/InspectionDoorWideOpen share number 7 (one rule,
        // two anomaly states).
        [SerializeField] private int ruleNumber;

        public string Id => id;
        public string DisplayName => displayName;
        public string RuleText => ruleText;
        public int RuleNumber => ruleNumber;
        public bool IsTerminal => isTerminal;

        // True for Rule 6 (FlippedPainting): target is resolved via MirrorPairTable at generation time.
        public bool UsesMirrorPairing => usesMirrorPairing;

        // True for anomalies whose target is "some painting" decided only at generation time (3, 4, 8).
        public bool NeedsPaintingTarget => needsPaintingTarget;

        public IReadOnlyList<PaintingWall> PaintingWallOptions => paintingWallOptions;
        public IReadOnlyList<TimeSlot> AllowedSlots => allowedSlots;
        public IReadOnlyList<ActionRequirement> RequiredActions => requiredActions;
        public IReadOnlyList<ActionRequirement> ForbiddenActions => forbiddenActions;

#if UNITY_EDITOR
        public void EditorInitialize(string newId, string newDisplayName, IEnumerable<TimeSlot> slots,
            bool terminal, bool mirrorPairing, bool paintingTarget,
            IEnumerable<ActionRequirement> required, IEnumerable<ActionRequirement> forbidden,
            IEnumerable<PaintingWall> wallOptions = null, string newRuleText = "", int newRuleNumber = 0)
        {
            id = newId;
            displayName = newDisplayName;
            allowedSlots = new List<TimeSlot>(slots);
            isTerminal = terminal;
            usesMirrorPairing = mirrorPairing;
            needsPaintingTarget = paintingTarget;
            requiredActions = new List<ActionRequirement>(required);
            forbiddenActions = new List<ActionRequirement>(forbidden);
            paintingWallOptions = wallOptions != null ? new List<PaintingWall>(wallOptions) : new List<PaintingWall>();
            ruleText = newRuleText;
            ruleNumber = newRuleNumber;
        }
#endif
    }
}
