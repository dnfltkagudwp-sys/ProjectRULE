using UnityEditor;
using UnityEngine;

namespace RuleGhost.EditorTools
{
    // Rewrites every rule's display text into the "근무수칙" document style (하십시오체, one
    // paragraph per rule, numbered heading line) agreed for the physical rulebook prop. Text only --
    // ruleNumber and all judgment data on these assets are untouched. Header/footer of the document
    // live in RulebookTextBuilder, not here. Safe to re-run.
    public static class ApplyRulebookRewrite
    {
        private const string Dir = "Assets/Data/Anomalies/";

        private static readonly (string asset, string text)[] Rules =
        {
            ("Duty_AM1",
                "1. 오전 1시 순찰\n" +
                "로비를 순찰하며 그림의 상태를 확인하십시오. 기울어진 그림은 바로잡되, 움직이는 그림만은 기울어져 있어도 그대로 두십시오. 순찰을 마쳤다면 점검문과 습도계를 확인하고 경비실로 복귀하십시오."),
            ("Duty_AM5",
                "2. 오전 5시 순찰\n" +
                "습도계와 점검문을 확인한 뒤, 마지막으로 로비 출입문이 닫혀 있는지 확인하십시오. 다른 수칙이 즉시 복귀를 지시한다면 그 수칙이 우선입니다."),
            ("Anomaly_EyesOpenPortrait",
                "3. 초상화\n" +
                "초상화가 눈을 뜨고 있다면 눈을 마주치지 마십시오. 기울어져 있다면, 눈을 마주치지 않은 채로 바로잡으십시오."),
            ("Anomaly_PersonInLandscape",
                "4. 풍경화\n" +
                "풍경화에 사람이 보인다면 즉시 등을 돌리고 잠시 기다리십시오. 그림에서 사람이 사라졌다면 순찰을 마저 하십시오. 사라진 그림은 다시 확인하지 마십시오."),
            ("Anomaly_HighHumidity",
                "5. 습도\n" +
                "로비의 적정 습도는 45~55%입니다. 범위를 벗어났다면 습도계를 조작해 맞추십시오. 단, 70% 이상이라면 절대 건드리지 마십시오."),
            ("Anomaly_FlippedPainting",
                "6. 뒤집힌 그림\n" +
                "뒤집힌 그림을 발견했다면 원래대로 돌리지 말고, 맞은편 그림을 대신 뒤집으십시오. 처음 발견한 그림에는 손대지 마십시오."),
            ("Anomaly_InspectionDoorAjar",
                "7. 점검문\n" +
                "점검문이 반쯤 열려 있다면 완전히 닫으십시오. 활짝 열려 있다면 가까이 가지 말고 즉시 경비실로 복귀하십시오."),
            ("Anomaly_InspectionDoorWideOpen",
                "7. 점검문\n" +
                "점검문이 반쯤 열려 있다면 완전히 닫으십시오. 활짝 열려 있다면 가까이 가지 말고 즉시 경비실로 복귀하십시오."),
            ("Anomaly_SoundFromExhibit",
                "8. 목소리\n" +
                "특정 전시물에서 목소리가 들린다면, 순찰이 끝날 때까지 그 전시물에 등을 보이지 마십시오."),
            ("Anomaly_KnockOnDoor",
                "9. 출입문 노크\n" +
                "출입문을 점검하던 중 노크 소리가 들린다면 문을 열거나 잠금장치를 조작하지 마십시오. 물러나서 소리가 멎을 때까지 기다린 뒤 점검을 마치십시오."),
        };

        [MenuItem("RuleGhost/Data/Apply Rulebook Rewrite")]
        public static void Run()
        {
            int updated = 0;
            foreach (var (asset, text) in Rules)
            {
                var path = $"{Dir}{asset}.asset";
                var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (obj == null)
                {
                    Debug.LogError($"[ApplyRulebookRewrite] Could not load {path}.");
                    continue;
                }

                var so = new SerializedObject(obj);
                var prop = so.FindProperty("ruleText");
                if (prop == null)
                {
                    Debug.LogError($"[ApplyRulebookRewrite] {asset} has no ruleText field.");
                    continue;
                }

                prop.stringValue = text;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
                updated++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ApplyRulebookRewrite] Updated ruleText on {updated}/{Rules.Length} assets.");
        }
    }
}
