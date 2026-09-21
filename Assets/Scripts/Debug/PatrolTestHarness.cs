using System.Collections.Generic;
using RuleGhost.Anomalies;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = System.Random;

namespace RuleGhost.Debugging
{
    // Graybox-only tool: press 1-6 to generate that patrol round and tag whatever it touched
    // with a color + floating label. Pressing a key again (same round or a different one)
    // always clears the previous roll first and re-rolls, so a compound round (5/6) can be
    // hammered dozens of times to shake out bad Validator output quickly.
    //
    // None of this — colors, labels, number-key bindings, verbose console dump — belongs in
    // the real game. PatrolSceneBindings is the only part of this feature that is not debug-only.
    public class PatrolTestHarness : MonoBehaviour
    {
        [SerializeField] private PatrolSceneBindings bindings;
        [SerializeField] private CombinationRuleSet ruleSet;
        [SerializeField] private PatrolProfile[] profiles = new PatrolProfile[6]; // index 0 = patrol 1

        private readonly Random rng = new Random();
        private readonly List<GameObject> spawnedDebugObjects = new();
        private readonly List<Renderer> tintedRenderers = new();
        private readonly Dictionary<Transform, Quaternion> rotationOverrides = new();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) Roll(0);
            if (keyboard.digit2Key.wasPressedThisFrame) Roll(1);
            if (keyboard.digit3Key.wasPressedThisFrame) Roll(2);
            if (keyboard.digit4Key.wasPressedThisFrame) Roll(3);
            if (keyboard.digit5Key.wasPressedThisFrame) Roll(4);
            if (keyboard.digit6Key.wasPressedThisFrame) Roll(5);
        }

        private void Roll(int profileIndex)
        {
            ClearVisuals();

            if (bindings == null || ruleSet == null || profileIndex >= profiles.Length || profiles[profileIndex] == null)
            {
                Debug.LogWarning($"[PatrolTestHarness] Patrol {profileIndex + 1} isn't wired up (rebuild the graybox scene).");
                return;
            }

            var profile = profiles[profileIndex];
            var result = PatrolGenerator.Generate(profile, ruleSet, rng);

            Debug.Log($"[PatrolTestHarness] DEBUG ROLL (tint + label only, does not touch real game state/textures) -- " +
                      $"Patrol {profile.PatrolIndex} ({profile.Slot}, {profile.Difficulty}) -> {result.Anomalies.Count} anomaly(s). " +
                      "For the real effect (texture swap, door angle, etc.), play through PatrolRuntimeController instead.");

            foreach (var anomaly in result.Anomalies)
            {
                LogAnomaly(anomaly);
                Visualize(anomaly);
            }
        }

        private void LogAnomaly(ResolvedAnomaly anomaly)
        {
            Debug.Log($"  - {anomaly.Id} | Terminal={anomaly.Source.IsTerminal} | " +
                      $"Required=[{Describe(anomaly.RequiredActions)}] | Forbidden=[{Describe(anomaly.ForbiddenActions)}]");
        }

        private static string Describe(IReadOnlyList<ActionRequirement> reqs)
        {
            var parts = new List<string>();
            foreach (var r in reqs)
            {
                parts.Add($"{r.Action}@{r.Target}");
            }
            return string.Join(", ", parts);
        }

        private void Visualize(ResolvedAnomaly anomaly)
        {
            switch (anomaly.Id)
            {
                case "EyesOpenPortrait":
                    TagTarget(FirstTarget(anomaly.ForbiddenActions), Color.yellow, "눈뜬 초상화");
                    break;
                case "PersonInLandscape":
                    TagTarget(FirstTarget(anomaly.RequiredActions), new Color(1f, 0.5f, 0f), "사람 풍경화");
                    break;
                case "FlippedPainting":
                    var mirrorTarget = FirstTarget(anomaly.RequiredActions);
                    var discoveredTarget = FirstTarget(anomaly.ForbiddenActions);
                    TagTarget(mirrorTarget, Color.magenta, "뒤집기");
                    FlipPainting(mirrorTarget);
                    TagTarget(discoveredTarget, Color.gray, "원본 - 손대지 마시오");
                    break;
                case "HighHumidity":
                    TagTarget(TargetRef.Simple(TargetKind.Thermometer), Color.red, "습도 70%+");
                    break;
                case "InspectionDoorAjar":
                    TagTarget(TargetRef.Simple(TargetKind.InspectionDoor), new Color(1f, 0.5f, 0f), "점검문 반개방");
                    break;
                case "InspectionDoorWideOpen":
                    TagTarget(TargetRef.Simple(TargetKind.InspectionDoor), Color.red, "점검문 완전개방 (TERMINAL)");
                    break;
                case "SoundFromExhibit":
                    TagTarget(FirstTarget(anomaly.RequiredActions), Color.cyan, "소리 발생");
                    break;
                case "KnockOnDoor":
                    TagTarget(TargetRef.Simple(TargetKind.EntranceDoor), Color.blue, "출입문 노크");
                    break;
            }
        }

        private static TargetRef FirstTarget(IReadOnlyList<ActionRequirement> reqs)
        {
            return reqs.Count > 0 ? reqs[0].Target : default;
        }

        private void TagTarget(TargetRef target, Color color, string label)
        {
            var t = bindings.Resolve(target);
            if (t == null)
            {
                return;
            }

            var renderer = t.GetComponent<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(block);
                tintedRenderers.Add(renderer);
            }

            var labelGO = new GameObject($"DebugLabel_{label}");
            labelGO.transform.SetParent(t, false);
            labelGO.transform.localPosition = Vector3.up * 1f;
            // Paintings/checkpoints are non-uniformly scaled (e.g. 1.2 x 1.6 x 0.05 for a thin
            // frame), which the label would otherwise inherit and render squashed/unreadable.
            // Counteract the parent's scale so the label always renders at a normal, even size.
            var parentScale = t.lossyScale;
            labelGO.transform.localScale = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f);
            var mesh = labelGO.AddComponent<TextMesh>();
            mesh.text = label;
            mesh.characterSize = 0.2f;
            mesh.fontSize = 48;
            mesh.color = Color.white;
            mesh.anchor = TextAnchor.LowerCenter;
            spawnedDebugObjects.Add(labelGO);
        }

        private void FlipPainting(TargetRef target)
        {
            var t = bindings.Resolve(target);
            if (t == null)
            {
                return;
            }

            if (!rotationOverrides.ContainsKey(t))
            {
                rotationOverrides[t] = t.localRotation;
            }
            t.localRotation *= Quaternion.Euler(0, 0, 180);
        }

        private void ClearVisuals()
        {
            foreach (var go in spawnedDebugObjects)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }
            spawnedDebugObjects.Clear();

            foreach (var renderer in tintedRenderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(null);
                }
            }
            tintedRenderers.Clear();

            foreach (var kvp in rotationOverrides)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localRotation = kvp.Value;
                }
            }
            rotationOverrides.Clear();
        }

#if UNITY_EDITOR
        public void EditorConfigure(PatrolSceneBindings sceneBindings, CombinationRuleSet rules, PatrolProfile[] patrolProfiles)
        {
            bindings = sceneBindings;
            ruleSet = rules;
            profiles = patrolProfiles;
        }
#endif
    }
}
