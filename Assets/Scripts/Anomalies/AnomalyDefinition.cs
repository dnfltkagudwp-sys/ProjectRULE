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
        [SerializeField] private List<ActionRequirement> requiredActions = new();
        [SerializeField] private List<ActionRequirement> forbiddenActions = new();

        public string Id => id;
        public string DisplayName => displayName;
        public bool IsTerminal => isTerminal;

        // True for Rule 6 (FlippedPainting): target is resolved via MirrorPairTable at generation time.
        public bool UsesMirrorPairing => usesMirrorPairing;

        // True for anomalies whose target is "some painting" decided only at generation time (3, 4, 8).
        public bool NeedsPaintingTarget => needsPaintingTarget;

        public IReadOnlyList<TimeSlot> AllowedSlots => allowedSlots;
        public IReadOnlyList<ActionRequirement> RequiredActions => requiredActions;
        public IReadOnlyList<ActionRequirement> ForbiddenActions => forbiddenActions;

#if UNITY_EDITOR
        public void EditorInitialize(string newId, string newDisplayName, IEnumerable<TimeSlot> slots,
            bool terminal, bool mirrorPairing, bool paintingTarget,
            IEnumerable<ActionRequirement> required, IEnumerable<ActionRequirement> forbidden)
        {
            id = newId;
            displayName = newDisplayName;
            allowedSlots = new List<TimeSlot>(slots);
            isTerminal = terminal;
            usesMirrorPairing = mirrorPairing;
            needsPaintingTarget = paintingTarget;
            requiredActions = new List<ActionRequirement>(required);
            forbiddenActions = new List<ActionRequirement>(forbidden);
        }
#endif
    }
}
