using System;
using System.Collections;
using System.Collections.Generic;
using RuleGhost.Anomalies;
using UnityEngine;

namespace RuleGhost.UI
{
    // Plays the visual/audio sting for whichever rule ended the round, then leaves the screen
    // fully black -- PatrolRuntimeController.TriggerDeath resumes right after this coroutine
    // returns and calls StartPatrolAt(0), whose RoundIntroShow starts from that same already-black
    // ScreenFadeController, so there is no separate fade-out here and no seam between the two.
    //
    // Every sequence restores whatever it touched (camera offset, light intensities, readout text,
    // swapped painting, spawned objects) before returning -- all of it happens under full black, so
    // the revert is invisible -- and OnDisable repeats that restore, since a coroutine stopped
    // mid-flight (Play Mode exit, object disabled) never runs the code after its current yield.
    public class DeathSequenceUI : MonoBehaviour
    {
        [SerializeField] private ScreenFadeController fade;

        // Exposed rather than const because this one sequence is pure feel -- it gets tuned by
        // watching it, not by reasoning about it, and the Inspector is a far faster loop than a
        // recompile. Tune during Play Mode, then write the numbers back into these defaults.
        [Header("Eyes-open portrait death (seconds)")]
        [Tooltip("Input is already frozen; nothing happens yet.")]
        [SerializeField] private float portraitFreezeBeat = 0.1f;
        [Tooltip("Darkness before the face is swapped.")]
        [SerializeField] private float portraitDarkBeforeSwap = 0.18f;
        [Tooltip("Remaining darkness after the swap, before the lights come back.")]
        [SerializeField] private float portraitDarkAfterSwap = 0.07f;
        [Tooltip("How long the swapped face is on screen before the hard cut.")]
        [SerializeField] private float portraitExposure = 0.6f;
        [Tooltip("Silent black before the reset intro takes over.")]
        [SerializeField] private float portraitBlackHold = 0.9f;

        [Header("Person-in-landscape death (seconds)")]
        [Tooltip("Darkness before the figure is swapped closer.")]
        [SerializeField] private float landscapeDarkBeforeSwap = 0.25f;
        [Tooltip("How long the closer figure is on screen -- longer than the portrait's, since the horror here is recognising what moved rather than being startled.")]
        [SerializeField] private float landscapeExposure = 0.9f;
        [SerializeField] private float landscapeFadeOut = 0.25f;
        [SerializeField] private float landscapeBlackHold = 0.9f;

        [Header("Sound-from-exhibit death")]
        [Tooltip("Seconds of camera jolt after the sting.")]
        [SerializeField] private float soundShakeDuration = 0.28f;
        [Tooltip("Jolt distance in metres, not seconds.")]
        [SerializeField] private float soundShakeMagnitude = 0.1f;
        [SerializeField] private float soundFadeOut = 0.08f;
        [SerializeField] private float soundBlackHold = 0.9f;

        [Header("High-humidity death (seconds)")]
        [Tooltip("How long the readout garbles and the light stutters.")]
        [SerializeField] private float humidityGlitchDuration = 0.55f;
        [SerializeField] private float humidityFadeOut = 0.08f;
        [SerializeField] private float humidityBlackHold = 0.9f;

        [Header("Inspection-door terminal death")]
        [Tooltip("How far the view rolls off level, in degrees.")]
        [SerializeField] private float doorTiltDegrees = 28f;
        [Tooltip("Seconds the roll takes to reach that angle.")]
        [SerializeField] private float doorTiltDuration = 1.8f;
        [Tooltip("Seconds of tilting before the fade starts -- keep shorter than the roll so it's cut off mid-tilt.")]
        [SerializeField] private float doorFadeStartDelay = 1f;
        [SerializeField] private float doorFadeOut = 0.6f;
        [SerializeField] private float doorBlackHold = 0.9f;

        [Header("Patrol-failed death (seconds)")]
        [Tooltip("Quiet beat after stepping into the guard room, before the knock.")]
        [SerializeField] private float failedBeforeKnock = 0.5f;
        [Tooltip("Long enough for all three knocks to land.")]
        [SerializeField] private float failedAfterKnock = 1.1f;
        [Tooltip("Sitting in the unlit guard room before the fade.")]
        [SerializeField] private float failedDarkBeforeFade = 0.7f;
        [SerializeField] private float failedFadeOut = 0.5f;
        [SerializeField] private float failedBlackHold = 0.9f;

        private const float HardCutSeconds = 0.08f;
        private const float BlackHoldSeconds = 1.4f;
        private const float PlaceholderCutSeconds = 0.1f;

        // Scene objects other systems create by name: AnomalyRuntimeApplier's own cue/readout
        // objects and ApplyDramaticLighting's guard room lamp. Looked up by name (rather than
        // through PatrolSceneBindings) for the same reason AnomalyRuntimeApplier looks up the
        // inspection door hinge that way -- they're fixed, single-instance scene objects.
        private const string ExhibitCueObjectName = "AnomalySoundCue";
        private const string HumidityReadoutObjectName = "HumidityReadout";
        private const string GuardRoomLightObjectName = "Point_GuardRoom";

        // Same texture-orientation convention as AnomalyRuntimeApplier/ApplyPaintingBaseTextures:
        // the north wall's cubes need their V flipped, the west wall's don't.
        private static readonly Vector4 NorthFlipST = new(1, -1, 0, 1);
        private static readonly Vector4 WestIdentityST = new(1, 1, 0, 0);

        private AudioSource audioSource;

        private Transform shakenCamera;
        private Vector3 shakenCameraBaseLocalPos;
        private Transform tiltedCamera;
        private Quaternion tiltedCameraBaseRotation;
        private Transform aimedCamera;
        private Quaternion aimedCameraBaseLocalRotation;
        private readonly List<(Light light, float intensity)> dimmedLights = new();
        private TextMesh garbledText;
        private string garbledTextBase;
        private Color garbledTextBaseColor;
        private GameObject spawnedFlash;
        private Renderer swappedRenderer;
        private MaterialPropertyBlock swappedBlockBefore;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            // 2D: every one of these stings is meant to be at the player's ear, not somewhere in
            // the room -- and the player is standing next to each source anyway when it fires.
            audioSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            PatrolRuntimeController.PlayDeathSequence = Play;
        }

        private void OnDisable()
        {
            if (PatrolRuntimeController.PlayDeathSequence == (Func<string, IEnumerator>)Play)
            {
                PatrolRuntimeController.PlayDeathSequence = null;
            }
            RestoreAll();
        }

        public IEnumerator Play(string deathId)
        {
            switch (deathId)
            {
                case DeathSequenceIds.EyesOpenPortrait:
                    yield return EyesOpenPortraitDeath();
                    break;

                case DeathSequenceIds.PersonInLandscape:
                    yield return PersonInLandscapeDeath();
                    break;

                case DeathSequenceIds.SoundFromExhibit:
                    yield return SoundFromExhibitDeath();
                    break;

                case DeathSequenceIds.HighHumidity:
                    yield return HighHumidityDeath();
                    break;

                case DeathSequenceIds.PatrolFailed:
                    yield return PatrolFailedDeath();
                    break;

                case DeathSequenceIds.InspectionDoorWideOpen:
                    yield return InspectionDoorWideOpenDeath();
                    break;

                default:
                    Debug.LogWarning($"[DeathSequenceUI] No death visual for '{deathId}' -- falling back to a plain cut.");
                    yield return PlaceholderCut();
                    break;
            }

            RestoreAll();
        }

        // The flagship scare. The lights cutting has to read as a power blink rather than a
        // fade-to-black, the camera is snapped onto the painting and the face swapped while it's
        // dark (so neither is ever seen happening), and the cut at the end is instant, not a fade.
        //
        // No camera shake here on purpose: the painting is the whole point, and shaking the view
        // just splits attention away from it. See the serialized fields above for the timings.
        private IEnumerator EyesOpenPortraitDeath()
        {
            var target = ResolveAnomalyTarget(DeathSequenceIds.EyesOpenPortrait, useForbiddenTarget: true,
                previewWall: PaintingWall.North);

            yield return new WaitForSeconds(portraitFreezeBeat);

            DimAllLights();
            yield return new WaitForSeconds(portraitDarkBeforeSwap);

            AimCameraAt(target);
            SwapToDeathTexture(target, NorthFlipST);
            yield return new WaitForSeconds(portraitDarkAfterSwap);

            RestoreLights();
            audioSource.PlayOneShot(ProceduralSfx.HarshBurst);
            yield return new WaitForSeconds(portraitExposure);

            yield return fade.FadeTo(1f, 0f);
            yield return new WaitForSeconds(portraitBlackHold);
        }

        // Same painting, same figure -- except it covers half the canvas now. Held a beat longer
        // than the portrait, since the horror here is recognising what changed, not being startled.
        private IEnumerator PersonInLandscapeDeath()
        {
            var target = ResolveAnomalyTarget(DeathSequenceIds.PersonInLandscape, useForbiddenTarget: false,
                previewWall: PaintingWall.West);

            DimAllLights();
            yield return new WaitForSeconds(landscapeDarkBeforeSwap);
            // Same reason as the portrait: FaceExhibit allows up to 60 degrees off-centre, so the
            // painting can be well out toward the edge of the screen when the rule fires.
            AimCameraAt(target);
            SwapToDeathTexture(target, WestIdentityST);
            RestoreLights();

            yield return new WaitForSeconds(landscapeExposure);
            yield return fade.FadeTo(1f, landscapeFadeOut);
            yield return new WaitForSeconds(landscapeBlackHold);
        }

        // Deliberately shows nothing: the player has their back to the exhibit, so the horror is
        // that the sound they were walking away from is suddenly behind their head instead.
        private IEnumerator SoundFromExhibitDeath()
        {
            StopExhibitCue();
            audioSource.PlayOneShot(ProceduralSfx.HarshBurst);
            yield return Shake(soundShakeDuration, soundShakeMagnitude);
            yield return fade.FadeTo(1f, soundFadeOut);
            yield return new WaitForSeconds(soundBlackHold);
        }

        // Not an electrical accident -- the readout stops being a number at all, and the room is
        // lit for an instant by something far brighter than the gallery's own lamps.
        private IEnumerator HighHumidityDeath()
        {
            var readout = GameObject.Find(HumidityReadoutObjectName);
            var readoutText = readout != null ? readout.GetComponent<TextMesh>() : null;
            if (readoutText != null)
            {
                garbledText = readoutText;
                garbledTextBase = readoutText.text;
                garbledTextBaseColor = readoutText.color;
                readoutText.color = Color.red;
            }

            if (readout != null)
            {
                spawnedFlash = new GameObject("DeathFlash");
                spawnedFlash.transform.position = readout.transform.position;
                var flash = spawnedFlash.AddComponent<Light>();
                flash.type = LightType.Point;
                flash.color = new Color(0.85f, 0.9f, 1f);
                flash.range = 10f;
                flash.intensity = 0f;
            }

            audioSource.PlayOneShot(ProceduralSfx.Zap);

            var rng = new System.Random(20260928);
            float elapsed = 0f;
            float nextGarble = 0f;
            while (elapsed < humidityGlitchDuration)
            {
                elapsed += Time.deltaTime;
                if (garbledText != null && elapsed >= nextGarble)
                {
                    nextGarble = elapsed + 0.04f;
                    garbledText.text = RandomGlyphs(rng, garbledTextBase.Length);
                }
                if (spawnedFlash != null)
                {
                    // Stuttering, way past what any lamp in the room puts out.
                    var flash = spawnedFlash.GetComponent<Light>();
                    flash.intensity = rng.NextDouble() < 0.5 ? 0f : 45f;
                }
                yield return null;
            }

            yield return fade.FadeTo(1f, humidityFadeOut);
            yield return new WaitForSeconds(humidityBlackHold);
        }

        // No jolt and nothing to look at: the player knew the door was wide open and kept walking
        // the round anyway, so the world just quietly stops being level. The tilt keeps going
        // underneath the fade rather than settling first -- it's cut off, not finished.
        private IEnumerator InspectionDoorWideOpenDeath()
        {
            StartCoroutine(TiltCamera(doorTiltDegrees, doorTiltDuration));
            yield return new WaitForSeconds(doorFadeStartDelay);
            yield return fade.FadeTo(1f, doorFadeOut);
            yield return new WaitForSeconds(doorBlackHold);
        }

        // The quiet one: the player thinks the shift is over, and something knocks to be let in.
        private IEnumerator PatrolFailedDeath()
        {
            yield return new WaitForSeconds(failedBeforeKnock);
            audioSource.PlayOneShot(ProceduralSfx.Knock);
            yield return new WaitForSeconds(failedAfterKnock);

            DimLight(GuardRoomLightObjectName);
            yield return new WaitForSeconds(failedDarkBeforeFade);

            yield return fade.FadeTo(1f, failedFadeOut);
            yield return new WaitForSeconds(failedBlackHold);
        }

        private IEnumerator PlaceholderCut()
        {
            if (fade == null)
            {
                yield break;
            }

            yield return fade.FadeTo(1f, PlaceholderCutSeconds);
            yield return new WaitForSeconds(BlackHoldSeconds);
        }

        // Which painting the sting belongs to isn't in the death id -- it's whichever one this
        // round's anomaly resolved onto, so it's read back off the live anomaly the same way
        // AnomalyRuntimeApplier picks its own target (first forbidden/required entry).
        private TargetRef? ResolveAnomalyTarget(string anomalyId, bool useForbiddenTarget, PaintingWall previewWall)
        {
            var controller = PatrolRuntimeController.Instance;
            if (controller != null)
            {
                foreach (var anomaly in controller.CurrentAnomalies)
                {
                    if (anomaly.Id != anomalyId)
                    {
                        continue;
                    }

                    var actions = useForbiddenTarget ? anomaly.ForbiddenActions : anomaly.RequiredActions;
                    if (actions.Count > 0)
                    {
                        return actions[0].Target;
                    }
                }
            }

#if UNITY_EDITOR
            // DeathSequenceTestKeys can fire a sting for an anomaly that isn't actually running
            // this round, which would otherwise leave it with no painting to change and nothing to
            // look at. A real death always matches a live anomaly and never reaches this.
            return TargetRef.Painting(previewWall, 1);
#else
            return null;
#endif
        }

        private void SwapToDeathTexture(TargetRef? target, Vector4 st)
        {
            if (target == null)
            {
                return;
            }

            var controller = PatrolRuntimeController.Instance;
            var bindings = controller != null ? controller.SceneBindings : null;
            var t = bindings != null ? bindings.Resolve(target.Value) : null;
            var renderer = t != null ? t.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                return;
            }

            var texture = LoadDeathTexture(target.Value);
            if (texture == null)
            {
                Debug.LogWarning($"[DeathSequenceUI] No death texture for {target.Value} -- " +
                                 "run RuleGhost/Anomalies/Build Death Placeholder Textures.");
                return;
            }

            // Restores to whatever the anomaly had already put on this renderer, not to bare
            // material -- the round reset would clear it anyway, but leaving the world exactly as
            // found is what keeps this sequence's cleanup independent of what happens next.
            swappedRenderer = renderer;
            swappedBlockBefore = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(swappedBlockBefore);

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", texture);
            block.SetTexture("_MainTex", texture);
            block.SetVector("_BaseMap_ST", st);
            block.SetVector("_MainTex_ST", st);
            renderer.SetPropertyBlock(block);
        }

        private static Texture2D LoadDeathTexture(TargetRef target)
        {
            if (target.Kind != TargetKind.SpecificPainting || target.Index < 1 || target.Index > 3)
            {
                return null;
            }

            string path = target.Wall switch
            {
                // Portrait/Portrait2/Portrait3 -- the first folder has no suffix, matching how
                // AnomalyRuntimeApplier.PortraitVariantPath names them.
                PaintingWall.North => target.Index == 1
                    ? "Assets/Art/Paintings/Portrait/Portrait_death_v1.png"
                    : $"Assets/Art/Paintings/Portrait{target.Index}/Portrait{target.Index}_death_v1.png",
                PaintingWall.West => $"Assets/Art/Paintings/Landscape{target.Index}/Landscape{target.Index}_death_v1.png",
                _ => null
            };
            if (path == null)
            {
                return null;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#else
            // Same editor-only limitation as AnomalyRuntimeApplier's own texture swaps -- moving
            // these to Resources/Addressables is a project-wide change, not a per-sting one.
            return null;
#endif
        }

        private static string RandomGlyphs(System.Random rng, int length)
        {
            const string glyphs = "▓█▒░#%&@!?";
            var chars = new char[Mathf.Max(2, length)];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = glyphs[rng.Next(glyphs.Length)];
            }
            return new string(chars);
        }

        // The exhibit's cue is a one-shot placeholder tone today, so this is usually a no-op --
        // it's here so that the moment a real looping "voice from the painting" clip replaces it,
        // the sound cutting dead at the instant of death already works.
        private void StopExhibitCue()
        {
            var cue = GameObject.Find(ExhibitCueObjectName);
            var source = cue != null ? cue.GetComponent<AudioSource>() : null;
            if (source != null)
            {
                source.Stop();
            }
        }

        // Same lookup PatrolRuntimeController uses for its own gaze checks -- the player camera is
        // a child of whatever object carries the CharacterController.
        private static Transform ResolvePlayerCamera()
        {
            var controller = FindFirstObjectByType<CharacterController>();
            var camera = controller != null ? controller.GetComponentInChildren<Camera>() : null;
            return camera != null ? camera.transform : null;
        }

        // Points the view straight at the painting the sting is about. Freezing the player's input
        // alone isn't enough: the rules that trigger these deaths only require the exhibit to be
        // roughly in view (15 degrees for eye contact, 60 for facing), so without this the face can
        // change off in a corner of the screen. Snapped rather than turned smoothly, and always
        // called while the lights are out, so the correction itself is never visible.
        private void AimCameraAt(TargetRef? target)
        {
            if (target == null)
            {
                return;
            }

            var controller = PatrolRuntimeController.Instance;
            var bindings = controller != null ? controller.SceneBindings : null;
            var t = bindings != null ? bindings.Resolve(target.Value) : null;
            var camera = ResolvePlayerCamera();
            if (t == null || camera == null)
            {
                return;
            }

            Vector3 toTarget = t.position - camera.position;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            aimedCamera = camera;
            aimedCameraBaseLocalRotation = camera.localRotation;
            camera.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
        }

        // Rolls the view off level, easing in so it starts as something you're not sure you saw.
        // Safe to leave running when the caller stops waiting on it: RestoreAll puts the camera
        // back, and the player's own look control is disabled for the whole death anyway.
        private IEnumerator TiltCamera(float degrees, float duration)
        {
            var camera = ResolvePlayerCamera();
            if (camera == null)
            {
                yield break;
            }

            tiltedCamera = camera;
            tiltedCameraBaseRotation = camera.localRotation;

            float elapsed = 0f;
            while (elapsed < duration && tiltedCamera != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float angle = degrees * (t * t);
                tiltedCamera.localRotation = tiltedCameraBaseRotation * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
        }

        private IEnumerator Shake(float duration, float magnitude)
        {
            var camera = ResolvePlayerCamera();
            if (camera == null)
            {
                yield break;
            }

            shakenCamera = camera;
            shakenCameraBaseLocalPos = shakenCamera.localPosition;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float falloff = Mathf.Clamp01(1f - elapsed / duration);
                shakenCamera.localPosition = shakenCameraBaseLocalPos +
                                             UnityEngine.Random.insideUnitSphere * (magnitude * falloff);
                yield return null;
            }

            shakenCamera.localPosition = shakenCameraBaseLocalPos;
            shakenCamera = null;
        }

        // Kills every light in the scene for a beat -- the gallery spots, the guard room lamp and
        // the player's own flashlight alike. Used to cover a texture swap: the painting changes in
        // the dark, so the player sees the result rather than the switch.
        private void DimAllLights()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                dimmedLights.Add((light, light.intensity));
                light.intensity = 0f;
            }
        }

        private void DimLight(string lightObjectName)
        {
            var go = GameObject.Find(lightObjectName);
            var light = go != null ? go.GetComponent<Light>() : null;
            if (light == null)
            {
                return;
            }

            dimmedLights.Add((light, light.intensity));
            light.intensity = 0f;
        }

        private void RestoreLights()
        {
            foreach (var (light, intensity) in dimmedLights)
            {
                if (light != null)
                {
                    light.intensity = intensity;
                }
            }
            dimmedLights.Clear();
        }

        private void RestoreAll()
        {
            if (shakenCamera != null)
            {
                shakenCamera.localPosition = shakenCameraBaseLocalPos;
                shakenCamera = null;
            }

            if (tiltedCamera != null)
            {
                tiltedCamera.localRotation = tiltedCameraBaseRotation;
                // Nulling this also stops TiltCamera's loop if it's somehow still running.
                tiltedCamera = null;
            }

            if (aimedCamera != null)
            {
                aimedCamera.localRotation = aimedCameraBaseLocalRotation;
                aimedCamera = null;
            }

            RestoreLights();

            if (garbledText != null)
            {
                garbledText.text = garbledTextBase;
                garbledText.color = garbledTextBaseColor;
                garbledText = null;
            }

            if (swappedRenderer != null)
            {
                swappedRenderer.SetPropertyBlock(swappedBlockBefore);
                swappedRenderer = null;
                swappedBlockBefore = null;
            }

            if (spawnedFlash != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(spawnedFlash);
                }
                spawnedFlash = null;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(ScreenFadeController fadeController)
        {
            fade = fadeController;
        }
#endif
    }
}
