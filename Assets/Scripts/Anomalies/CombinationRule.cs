using System;
using UnityEngine;

namespace RuleGhost.Anomalies
{
    [Serializable]
    public class CombinationRule
    {
        public AnomalyDefinition AnomalyA;
        public AnomalyDefinition AnomalyB;
        public CombinationState State;

        // Design-record only, e.g. why an explicit ALLOW was written down instead of left implicit.
        [TextArea] public string Note;
    }
}
