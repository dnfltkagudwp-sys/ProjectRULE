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
        public const int DefaultRulesPerPage = 3;

        public const string Header =
            "<size=40>야간 경비 근무수칙</size>\n" +
            "문서번호 제03호 · 경비실 비치용\n\n" +
            "귀하의 근무를 환영합니다. 아래 수칙은 무사히 근무를 마치기 위한 것입니다.\n" +
            "반드시 숙지하시고, 근무 중에도 언제든 다시 확인하십시오.";

        public const string Footer = "본 수칙에 기재되지 않은 지시를 받았다면 따르지 마십시오.";

        public static string BuildAll(IEnumerable<PatrolProfile> profiles)
        {
            return string.Join("\n\n", Collect(profiles).Values);
        }

        // The physical rulebook: rules grouped a few per page, the document header on the first page
        // and the closing notice on the last. Always at least one page, even with no rules.
        public static List<string> BuildPages(IEnumerable<PatrolProfile> profiles, int rulesPerPage = DefaultRulesPerPage)
        {
            if (rulesPerPage < 1)
            {
                rulesPerPage = 1;
            }

            var rules = new List<string>(Collect(profiles).Values);
            var pages = new List<string>();

            for (int i = 0; i < rules.Count; i += rulesPerPage)
            {
                int count = System.Math.Min(rulesPerPage, rules.Count - i);
                pages.Add(string.Join("\n\n", rules.GetRange(i, count)));
            }

            if (pages.Count == 0)
            {
                pages.Add(string.Empty);
            }

            pages[0] = Header + "\n\n" + pages[0];
            pages[pages.Count - 1] = pages[pages.Count - 1] + "\n\n\n" + Footer;
            return pages;
        }

        private static SortedDictionary<int, string> Collect(IEnumerable<PatrolProfile> profiles)
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

            return byNumber;
        }
    }
}
