using System;

namespace RuleGhost.Anomalies
{
    [Serializable]
    public struct ActionRequirement
    {
        public TargetRef Target;
        public ActionTag Action;

        public ActionRequirement(TargetRef target, ActionTag action)
        {
            Target = target;
            Action = action;
        }
    }
}
