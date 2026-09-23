using System.Collections.Generic;

namespace RuleGhost.Anomalies
{
    // Builds the 규칙서 UI's display text: the full 근무수칙 1-9, always, regardless of which
    // Duty/Anomalies are active this round -- a real work manual isn't filtered down to "today's
    // relevant pages," and the player is expected to recognize which rule applies themselves.
    // Kept separate from RulebookUI (a MonoBehaviour) so it can be unit tested without a scene, the
    // same split PatrolEvaluator/PatrolRuntimeController already use.
    public static class RulebookTextBuilder
    {
        public static string BuildAll(IEnumerable<PatrolProfile> profiles)
        {
            var byNumber = new SortedDictionary<int, string>();

            void AddIfPresent(int number, string text)
            {
                if (number > 0 && !string.IsNullOrEmpty(text) && !byNumber.ContainsKey(number))
                {
                    byNumber[number] = text;
                }
            }

            if (profiles != null)
            {
                foreach (var profile in profiles)
                {
                    if (profile == null)
                    {
                        continue;
                    }

                    if (profile.Duty != null)
                    {
                        AddIfPresent(profile.Duty.RuleNumber, profile.Duty.RuleText);
                    }

                    foreach (var anomaly in profile.AnomalyPool)
                    {
                        if (anomaly != null)
                        {
                            AddIfPresent(anomaly.RuleNumber, anomaly.RuleText);
                        }
                    }
                }
            }

            return string.Join("\n\n", byNumber.Values);
        }
    }
}
