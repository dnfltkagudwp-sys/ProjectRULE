using System.Collections.Generic;
using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Makes a round's generated anomalies actually visible/tangible in the scene: swaps a
    // painting's texture, flips one 180 degrees, opens the inspection door, or shows a
    // humidity reading. Deliberately just the presentation layer -- it does not judge whether
    // the player corrected anything (that's a future extension of PatrolEvaluator) and does not
    // implement anything that needs new audio assets yet (SoundFromExhibit, KnockOnDoor are
    // logged only). Plain C# class (not a MonoBehaviour) so PatrolRuntimeController can own one
    // instance and call ResetAll()/Apply() around each round the same way it already owns a
    // PatrolProgress.
    //
    // Uses MaterialPropertyBlock for texture swaps rather than mutating the painting's real
    // material asset -- ApplyPaintingBaseTextures.cs already gave each painting its own material
    // holding the base texture, and a property block overlay is trivially revertible
    // (SetPropertyBlock(null)) without ever touching that asset at runtime.
    public class AnomalyRuntimeApplier
    {
        private const float DoorClosedAngle = 0f;
        private const float DoorAjarAngle = 35f;
        private const float DoorWideOpenAngle = 100f;

        private readonly List<Renderer> tintedRenderers = new();
        private readonly Dictionary<Transform, Quaternion> rotationOverrides = new();
        // Kept alive and just deactivated between rounds instead of Destroy()'d and recreated --
        // Object.Destroy() doesn't actually remove the GameObject until the end of the frame, so
        // a new round that also rolls HighHumidity could create its replacement before the old
        // one was gone, showing both labels stacked on top of each other for a frame (or longer,
        // if rounds advance faster than that).
        private GameObject humidityLabel;
        private TextMesh humidityTextMesh;

        public void ResetAll(PatrolSceneBindings bindings)
        {
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

            SetDoorAngle(DoorClosedAngle);

            if (humidityLabel != null)
            {
                humidityLabel.SetActive(false);
            }
        }

        public void Apply(PatrolSceneBindings bindings, IReadOnlyList<ResolvedAnomaly> anomalies)
        {
            foreach (var anomaly in anomalies)
            {
                switch (anomaly.Id)
                {
                    case "EyesOpenPortrait":
                        // Forbidden action's target is the resolved "any painting" placeholder --
                        // the one that must not be looked in the eye.
                        SwapTexture(bindings, FirstTarget(anomaly.ForbiddenActions), PortraitVariantPath);
                        break;

                    case "PersonInLandscape":
                        SwapTexture(bindings, FirstTarget(anomaly.RequiredActions), LandscapeVariantPath);
                        break;

                    case "FlippedPainting":
                        // Forbidden action's target is the resolved MirrorDiscovered placeholder --
                        // the painting the player actually finds already flipped. The required
                        // action's target (MirrorTarget, its counterpart) is left alone here; that
                        // is what the player is supposed to flip themselves.
                        FlipPainting(bindings, FirstTarget(anomaly.ForbiddenActions));
                        break;

                    case "HighHumidity":
                        ShowHumidity(bindings, isHigh: true);
                        break;

                    case "InspectionDoorAjar":
                        SetDoorAngle(DoorAjarAngle);
                        break;

                    case "InspectionDoorWideOpen":
                        SetDoorAngle(DoorWideOpenAngle);
                        break;

                    case "SoundFromExhibit":
                    case "KnockOnDoor":
                        Debug.Log($"[AnomalyRuntimeApplier] {anomaly.Id} is active but has no audio implementation yet " +
                                  "(needs real sound assets) -- data/generation only for now.");
                        break;

                    default:
                        Debug.LogWarning($"[AnomalyRuntimeApplier] No presentation handler for anomaly '{anomaly.Id}'.");
                        break;
                }
            }
        }

        private static TargetRef FirstTarget(IReadOnlyList<ActionRequirement> reqs) =>
            reqs.Count > 0 ? reqs[0].Target : default;

        private void SwapTexture(PatrolSceneBindings bindings, TargetRef target, System.Func<TargetRef, (string path, Vector4 st)?> lookup)
        {
            var variant = lookup(target);
            if (variant == null)
            {
                Debug.LogWarning($"[AnomalyRuntimeApplier] No texture variant known for {target} -- skipping.");
                return;
            }

            var t = bindings.Resolve(target);
            var renderer = t != null ? t.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                Debug.LogWarning($"[AnomalyRuntimeApplier] Could not resolve a renderer for {target}.");
                return;
            }

            var loaded = LoadTexture(variant.Value.path);
            if (loaded == null)
            {
                Debug.LogWarning($"[AnomalyRuntimeApplier] Texture not found at {variant.Value.path}.");
                return;
            }

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", loaded);
            block.SetTexture("_MainTex", loaded);
            var st = variant.Value.st;
            block.SetVector("_BaseMap_ST", st);
            block.SetVector("_MainTex_ST", st);
            renderer.SetPropertyBlock(block);
            tintedRenderers.Add(renderer);
        }

        private static Texture2D LoadTexture(string assetPath)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
#else
            return null;
#endif
        }

        // North wall Cubes need a V-flip, West wall Cubes use identity -- same convention as
        // ApplyPaintingBaseTextures.cs.
        private static readonly Vector4 NorthFlipST = new Vector4(1, -1, 0, 1);
        private static readonly Vector4 WestIdentityST = new Vector4(1, 1, 0, 0);

        private static (string path, Vector4 st)? PortraitVariantPath(TargetRef target)
        {
            if (target.Kind != TargetKind.SpecificPainting || target.Wall != PaintingWall.North)
            {
                return null;
            }
            string folder = target.Index switch
            {
                1 => "Portrait",
                2 => "Portrait2",
                3 => "Portrait3",
                _ => null
            };
            if (folder == null) return null;
            return ($"Assets/Art/Paintings/{folder}/{folder}_eyesopen_v1.png", NorthFlipST);
        }

        private static (string path, Vector4 st)? LandscapeVariantPath(TargetRef target)
        {
            if (target.Kind != TargetKind.SpecificPainting || target.Wall != PaintingWall.West)
            {
                return null;
            }
            // Landscape3's approved variant file is versioned _v1 (the others are _v2) -- just a
            // naming quirk from how each was originally generated/approved, not a pattern to fix here.
            string suffix = target.Index == 3 ? "v1" : "v2";
            if (target.Index < 1 || target.Index > 3) return null;
            return ($"Assets/Art/Paintings/Landscape{target.Index}/Landscape{target.Index}_person_{suffix}.png", WestIdentityST);
        }

        private void FlipPainting(PatrolSceneBindings bindings, TargetRef target)
        {
            var t = bindings.Resolve(target);
            if (t == null)
            {
                Debug.LogWarning($"[AnomalyRuntimeApplier] Could not resolve {target} to flip.");
                return;
            }

            if (!rotationOverrides.ContainsKey(t))
            {
                rotationOverrides[t] = t.localRotation;
            }
            t.localRotation *= Quaternion.Euler(0f, 0f, 180f);
        }

        private static void SetDoorAngle(float angle)
        {
            var hinge = GameObject.Find("CheckPoint_InspectionDoor_Hinge");
            if (hinge == null)
            {
                Debug.LogWarning("[AnomalyRuntimeApplier] Could not find CheckPoint_InspectionDoor_Hinge.");
                return;
            }
            hinge.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        private void ShowHumidity(PatrolSceneBindings bindings, bool isHigh)
        {
            var t = bindings.Resolve(TargetRef.Simple(TargetKind.Thermometer));
            if (t == null)
            {
                Debug.LogWarning("[AnomalyRuntimeApplier] Could not resolve Thermometer.");
                return;
            }

            int value = isHigh ? Random.Range(72, 96) : Random.Range(45, 56);

            if (humidityLabel == null)
            {
                humidityLabel = new GameObject("HumidityReadout");
                humidityLabel.transform.SetParent(t, false);
                humidityLabel.transform.localPosition = Vector3.up * 0.5f;
                humidityTextMesh = humidityLabel.AddComponent<TextMesh>();
                humidityTextMesh.characterSize = 0.15f;
                humidityTextMesh.fontSize = 48;
                humidityTextMesh.anchor = TextAnchor.LowerCenter;
            }

            humidityLabel.SetActive(true);
            humidityTextMesh.text = $"{value}%";
            humidityTextMesh.color = isHigh ? Color.red : Color.white;

            var renderer = t.GetComponent<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", isHigh ? Color.red : new Color(0.2f, 0.5f, 0.9f));
                renderer.SetPropertyBlock(block);
                tintedRenderers.Add(renderer);
            }
        }
    }
}
