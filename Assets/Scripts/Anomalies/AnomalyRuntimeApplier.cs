using System.Collections.Generic;
using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Makes a round's generated anomalies actually visible/tangible in the scene: swaps a
    // painting's texture, flips one 180 degrees, opens the inspection door, or shows a
    // humidity reading. Deliberately just the presentation layer -- it does not judge whether
    // the player corrected anything (that's a future extension of PatrolEvaluator). Sounds come
    // from SoundBank's Inspector slots (an empty slot is silent). Plain C# class (not a MonoBehaviour) so PatrolRuntimeController can own one
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

        // Same persistent-object pattern as humidityLabel, for SoundFromExhibit's audio cue.
        private GameObject soundCueObject;
        private AudioSource soundCueSource;
        private static AudioClip placeholderCueClip;
        // The cue is set up at round start but held until the player first leaves the guard room
        // (StartPendingSoundCue) -- it shouldn't already be playing while they're still inside.
        private bool soundCuePending;

        // Non-audio counterpart to soundCueObject -- a flickering red point light so the anomaly
        // still reads to a player who can't hear (or has muted) the placeholder audio cue.
        private GameObject soundCueLightObject;
        // Internal so KnockOnDoorState's own non-audio cue uses the same red.
        internal static readonly Color SoundCueLightColor = new(1f, 0.15f, 0.1f);

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

            HygrometerDisplay.Set(bindings, HygrometerDisplay.State.Normal);

            soundCuePending = false;
            if (soundCueSource != null)
            {
                soundCueSource.Stop();
            }
            if (soundCueObject != null)
            {
                soundCueObject.SetActive(false);
            }
            if (soundCueLightObject != null)
            {
                soundCueLightObject.SetActive(false);
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
                        PlayInspectionDoorSound(SoundBank.Instance?.InspectionDoorOpen);
                        break;

                    case "InspectionDoorWideOpen":
                        SetDoorAngle(DoorWideOpenAngle);
                        PlayInspectionDoorSound(SoundBank.Instance?.InspectionDoorOpen);
                        break;

                    case "SoundFromExhibit":
                        // FaceExhibit/ShowBackToExhibit's facing window is just "this round" --
                        // the cue is set up here and starts once the player leaves the guard room
                        // (StartPendingSoundCue); ObservationRuleMonitor watches these facing
                        // rules for every active anomaly all round anyway.
                        PlaySoundCue(bindings, FirstTarget(anomaly.RequiredActions));
                        break;

                    case "KnockOnDoor":
                        // Nothing to show on round start -- the knock starts once the player nears
                        // the entrance door (KnockOnDoorState, ticked by PatrolRuntimeController).
                        break;

                    default:
                        Debug.LogWarning($"[AnomalyRuntimeApplier] No presentation handler for anomaly '{anomaly.Id}'.");
                        break;
                }
            }
        }

        private static TargetRef FirstTarget(IReadOnlyList<ActionRequirement> reqs) =>
            reqs.Count > 0 ? reqs[0].Target : default;

        // Reverts one specific painting's texture swap back to its base material -- unlike
        // ResetAll(), which reverts everything and is only ever called between rounds. Used for a
        // required action's own visual payoff (e.g. PersonInLandscape's person disappearing once
        // the player has turned away long enough) that has to happen mid-round, not at round end.
        // Safe to call on a renderer that was never tinted (SetPropertyBlock(null) is a no-op then).
        public void RevertTexture(PatrolSceneBindings bindings, TargetRef target)
        {
            var t = bindings.Resolve(target);
            var renderer = t != null ? t.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                return;
            }

            renderer.SetPropertyBlock(null);
            tintedRenderers.Remove(renderer);
        }

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

        // Public so PatrolRuntimeController can also call this for the player's OWN flip action
        // (FlippedPainting's required MirrorTarget) -- the same 180-degree rotation either way,
        // just a different target and a different reason. Flips again (back to original) if
        // called twice on the same target, so callers that can fire more than once per round (a
        // repeat E-key press) must guard against a second call themselves -- see
        // PatrolRuntimeController.HandleAnomalyAction, which only calls this once per target via
        // PatrolProgress.HasPerformedAction.
        public void FlipPainting(PatrolSceneBindings bindings, TargetRef target)
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
            t.localRotation *= Quaternion.AngleAxis(180f, PaintingOrientation.DepthAxis(target.Wall));
        }

        // Called when the player checks the inspection door while InspectionDoorAjar is active --
        // PatrolEvaluator's CloseInspectionDoorFully is satisfied by a plain visit (see its class
        // comment), so this is what actually moves the hinge back instead of leaving it visually
        // ajar after a round the player otherwise passed.
        public void CloseInspectionDoor()
        {
            SetDoorAngle(DoorClosedAngle);
            PlayInspectionDoorSound(SoundBank.Instance?.InspectionDoorClose);
        }

        // The squeak for the player's own hand on a frame (flipping FlippedPainting's mirror target,
        // straightening a Routine tilt) -- FlipPainting itself stays silent because it also runs
        // at round start to set up FlippedPainting, where a sound would give it away.
        public void PlayPaintingFlipSound(PatrolSceneBindings bindings, TargetRef target)
        {
            var t = bindings?.Resolve(target);
            if (t == null)
            {
                return;
            }

            SoundBank.PlayAt(SoundBank.Instance?.PaintingFlip, t.position);
        }

        private static void PlayInspectionDoorSound(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            var hinge = GameObject.Find("CheckPoint_InspectionDoor_Hinge");
            if (hinge != null)
            {
                SoundBank.PlayAt(clip, hinge.transform.position);
            }
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

        // No real horror sound asset exists yet -- this generates a short, cheap placeholder tone
        // in code (no audio asset needed) so the FaceExhibit/ShowBackToExhibit facing rules have a
        // real, in-scene moment to react to instead of being untestable until real audio arrives.
        // Swap PlaySoundCue's clip source for a real asset later; nothing else needs to change.
        private void PlaySoundCue(PatrolSceneBindings bindings, TargetRef target)
        {
            var t = bindings.Resolve(target);
            if (t == null)
            {
                Debug.LogWarning($"[AnomalyRuntimeApplier] Could not resolve {target} for SoundFromExhibit cue.");
                return;
            }

            if (soundCueObject == null)
            {
                soundCueObject = new GameObject("AnomalySoundCue");
                soundCueSource = soundCueObject.AddComponent<AudioSource>();
                soundCueSource.playOnAwake = false;
                soundCueSource.spatialBlend = 1f;
                // The rule hinges on telling WHICH painting is speaking, so distance has to read
                // clearly: logarithmic falloff (loud up close, dropping fast over the first few
                // metres) instead of the old flat linear ramp, plus a distance low-pass so a far
                // voice also sounds muffled, not just quieter.
                soundCueSource.rolloffMode = AudioRolloffMode.Logarithmic;
                soundCueSource.minDistance = 1f;
                soundCueSource.maxDistance = 15f;
                soundCueSource.dopplerLevel = 0f;
                var lowPass = soundCueObject.AddComponent<AudioLowPassFilter>();
                // x = distance / maxDistance, y = cutoff (1 = no filtering).
                lowPass.customCutoffCurve = new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(0.3f, 0.35f), new Keyframe(1f, 0.08f));
            }

            // A real clip from the SoundBank if one is assigned, otherwise the generated placeholder.
            // Set every time (not just on creation) so a slot filled/changed after the first round
            // takes effect.
            var bank = SoundBank.Instance;
            soundCueSource.clip = bank != null && bank.SoundFromExhibitCue != null
                ? bank.SoundFromExhibitCue
                : GetPlaceholderCueClip();
            soundCueSource.loop = bank != null && bank.SoundFromExhibitCueLoop;

            soundCueObject.transform.SetParent(t, false);
            soundCueObject.transform.localPosition = Vector3.zero;
            soundCueObject.SetActive(true);
            soundCuePending = true;

            if (soundCueLightObject == null)
            {
                soundCueLightObject = new GameObject("AnomalySoundCueLight");
                var light = soundCueLightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = SoundCueLightColor;
                light.range = 3f;
                soundCueLightObject.AddComponent<FlickerLight>();
            }

            soundCueLightObject.transform.SetParent(t, false);
            soundCueLightObject.transform.localPosition = Vector3.zero;
            soundCueLightObject.SetActive(true);
        }

        // Called by PatrolRuntimeController when the player first walks out of the guard room.
        // No-op on rounds without SoundFromExhibit, and after the first exit of a round.
        public void StartPendingSoundCue()
        {
            if (!soundCuePending || soundCueSource == null)
            {
                return;
            }

            soundCuePending = false;
            soundCueSource.Play();
        }

        private static AudioClip GetPlaceholderCueClip()
        {
            if (placeholderCueClip != null)
            {
                return placeholderCueClip;
            }

            const int sampleRate = 44100;
            const float duration = 0.8f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-3f * t); // quick decay so it doesn't end on an audible click
                data[i] = Mathf.Sin(2f * Mathf.PI * 220f * t) * envelope * 0.5f;
            }

            placeholderCueClip = AudioClip.Create("AnomalySoundCue_Placeholder", sampleCount, 1, sampleRate, false);
            placeholderCueClip.SetData(data, 0);
            return placeholderCueClip;
        }

        // Anomaly's own high-humidity state; Routine's own (mutually exclusive, see
        // RoutinePatrolState) needs-adjustment state goes through the same HygrometerDisplay.
        private void ShowHumidity(PatrolSceneBindings bindings, bool isHigh)
        {
            HygrometerDisplay.Set(bindings, isHigh ? HygrometerDisplay.State.Anomaly : HygrometerDisplay.State.Normal);
        }
    }
}
