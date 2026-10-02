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
    // Every sequence restores whatever it touched (camera offset, light intensities, hygrometer material,
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
        [Tooltip("Seconds the exhibit's voice plays right behind the player's head before the hard cut.")]
        [SerializeField] private float soundBehindDuration = 1.2f;
        [Tooltip("Where the voice is placed, in the camera's local space (metres). Off to one side on purpose: Unity's default panning can't tell front from back, so dead behind would sound dead ahead.")]
        [SerializeField] private Vector3 soundBehindOffset = new(-0.35f, 0f, -0.35f);
        [SerializeField, Range(0f, 1f)] private float soundBehindVolume = 1f;
        [SerializeField] private float soundBlackHold = 0.9f;

        [Header("High-humidity death (seconds)")]
        [Tooltip("How long the hygrometer glitches and the light stutters.")]
        [SerializeField] private float humidityGlitchDuration = 1f;
        [Tooltip("Seconds between display swaps -- around a frame or two, so it reads as a fault rather than the device changing state.")]
        [SerializeField] private float humidityGlitchInterval = 0.025f;
        [Tooltip("Chance per swap that the hygrometer drops out entirely for that step.")]
        [SerializeField, Range(0f, 1f)] private float humidityBlankChance = 0.2f;
        [SerializeField] private float humidityFadeOut = 0.08f;
        [SerializeField] private float humidityBlackHold = 0.9f;

        [Header("Inspection-door terminal death")]
        [Tooltip("How far the view rolls off level, in degrees.")]
        [SerializeField] private float doorTiltDegrees = 28f;
        [Tooltip("Seconds the roll takes to reach that angle.")]
        [SerializeField] private float doorTiltDuration = 1.2f;
        [Tooltip("Seconds of tilting before the fade starts -- keep shorter than the roll so it's cut off mid-tilt.")]
        [SerializeField] private float doorFadeStartDelay = 1f;
        [SerializeField] private float doorFadeOut = 0.6f;
        [SerializeField] private float doorBlackHold = 0.9f;

        [Header("Patrol-failed death (seconds)")]
        [Tooltip("Quiet beat after stepping into the guard room, before the knock.")]
        [SerializeField] private float failedBeforeKnock = 0.8f;
        [Tooltip("From the knock starting to the guard room lamp going out -- tuned to land in the knock clip's pause, so the second, louder round of knocking comes in the dark.")]
        [SerializeField] private float failedAfterKnock = 1.8f;
        [Tooltip("Sitting in the unlit guard room before the fade -- long enough for the second round of knocking to finish.")]
        [SerializeField] private float failedDarkBeforeFade = 2.4f;
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
        private const string GuardRoomLightObjectName = "Point_GuardRoom";

        // Same texture-orientation convention as AnomalyRuntimeApplier/ApplyPaintingBaseTextures:
        // the north wall's cubes need their V flipped, the west wall's don't.
        private static readonly Vector4 NorthFlipST = new(1, -1, 0, 1);
        private static readonly Vector4 WestIdentityST = new(1, 1, 0, 0);

        private AudioSource audioSource;

        private Transform tiltedCamera;
        private Quaternion tiltedCameraBaseRotation;
        private Transform aimedCamera;
        private Quaternion aimedCameraBaseLocalRotation;
        private readonly List<(Light light, float intensity)> dimmedLights = new();
        private Renderer glitchedRenderer;
        private Material glitchedMaterialBefore;
        private GameObject spawnedFlash;
        private GameObject spawnedBehindVoice;
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
                // Shares the tilt for now: the player is at the entrance, nowhere near the guard
                // room lamp PatrolFailed's sequence dims, and its knock sting would just repeat
                // the knock they were told to wait out.
                case DeathSequenceIds.KnockOnDoor:
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
            PlaySting(SoundBank.Instance?.DeathEyesOpenPortrait, ProceduralSfx.HarshBurst);
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
            PlaySting(SoundBank.Instance?.DeathPersonInLandscape, null);

            yield return new WaitForSeconds(landscapeExposure);
            yield return fade.FadeTo(1f, landscapeFadeOut);
            yield return new WaitForSeconds(landscapeBlackHold);
        }

        // Deliberately shows nothing: the player has their back to the exhibit, so the horror is
        // that the voice they were walking away from is suddenly right behind their head -- the
        // same voice, not a new sting, so it reads instantly. No shake, so nothing competes with
        // it; the cut to black is instant and takes the voice with it.
        private IEnumerator SoundFromExhibitDeath()
        {
            var cue = FindExhibitCueSource();
            var clip = cue != null && cue.clip != null ? cue.clip : SoundBank.Instance?.SoundFromExhibitCue;
            float from = cue != null && cue.isPlaying ? cue.time : 0f;
            StopExhibitCue();

            if (clip != null)
            {
                PlayVoiceBehindHead(clip, FindVoicedTime(clip, from));
            }
            yield return new WaitForSeconds(soundBehindDuration);

            StopVoiceBehindHead();
            // Optional hit on the cut itself -- an empty slot just cuts to silence.
            PlaySting(SoundBank.Instance?.DeathSoundFromExhibit, null);
            yield return fade.FadeTo(1f, 0f);
            yield return new WaitForSeconds(soundBlackHold);
        }

        // Not an electrical accident -- the display can't hold any one state, flickering between
        // all of them (and out entirely) faster than a real reading ever changes, and the room is
        // lit for an instant by something far brighter than the gallery's own lamps.
        private IEnumerator HighHumidityDeath()
        {
            var controller = PatrolRuntimeController.Instance;
            var bindings = controller != null ? controller.SceneBindings : null;
            var hygrometer = bindings != null ? bindings.Resolve(TargetRef.Simple(TargetKind.Thermometer)) : null;
            var renderer = hygrometer != null ? hygrometer.GetComponent<Renderer>() : null;
            var states = HygrometerDisplay.LoadAllStates();
            if (renderer != null)
            {
                glitchedRenderer = renderer;
                glitchedMaterialBefore = renderer.sharedMaterial;
            }

            if (hygrometer != null)
            {
                spawnedFlash = new GameObject("DeathFlash");
                spawnedFlash.transform.position = hygrometer.position;
                var flash = spawnedFlash.AddComponent<Light>();
                flash.type = LightType.Point;
                flash.color = new Color(0.85f, 0.9f, 1f);
                flash.range = 10f;
                flash.intensity = 0f;
            }

            PlaySting(SoundBank.Instance?.DeathHighHumidity, ProceduralSfx.Zap);

            var rng = new System.Random(20260928);
            float elapsed = 0f;
            float nextSwap = 0f;
            int lastState = -1;
            while (elapsed < humidityGlitchDuration)
            {
                elapsed += Time.deltaTime;
                if (glitchedRenderer != null && elapsed >= nextSwap)
                {
                    nextSwap = elapsed + humidityGlitchInterval;
                    bool blank = rng.NextDouble() < humidityBlankChance;
                    glitchedRenderer.enabled = !blank;
                    if (!blank)
                    {
                        // Never the same state twice in a row, so every visible step is a change.
                        int i = rng.Next(states.Length);
                        if (i == lastState)
                        {
                            i = (i + 1) % states.Length;
                        }
                        lastState = i;
                        if (states[i] != null)
                        {
                            glitchedRenderer.sharedMaterial = states[i];
                        }
                    }
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
            PlaySting(SoundBank.Instance?.DeathInspectionDoorWideOpen, null);
            StartCoroutine(TiltCamera(doorTiltDegrees, doorTiltDuration));
            yield return new WaitForSeconds(doorFadeStartDelay);
            yield return fade.FadeTo(1f, doorFadeOut);
            yield return new WaitForSeconds(doorBlackHold);
        }

        // The quiet one: the player thinks the shift is over, and something knocks to be let in.
        private IEnumerator PatrolFailedDeath()
        {
            yield return new WaitForSeconds(failedBeforeKnock);
            PlaySting(SoundBank.Instance?.DeathPatrolFailed, ProceduralSfx.Knock);
            yield return new WaitForSeconds(failedAfterKnock);

            DimLight(GuardRoomLightObjectName);
            yield return new WaitForSeconds(failedDarkBeforeFade);

            yield return fade.FadeTo(1f, failedFadeOut);
            yield return new WaitForSeconds(failedBlackHold);
        }

        // The SoundBank clip if one is assigned, else the generated placeholder (null = silent).
        private void PlaySting(AudioClip bankClip, AudioClip fallback)
        {
            var clip = bankClip != null ? bankClip : fallback;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
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
                Debug.LogError($"[DeathSequenceUI] No death texture for {target.Value} -- " +
                               "is AnomalyVisualAssets in the scene and wired (RuleGhost/Anomalies/Wire Anomaly Visual Assets)?");
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

            // From AnomalyVisualAssets (a scene reference) so the build includes them -- the old
            // AssetDatabase path lookup only worked in the Editor.
            var assets = AnomalyVisualAssets.Instance;
            if (assets == null)
            {
                return null;
            }
            return target.Wall switch
            {
                PaintingWall.North => assets.PortraitDeath(target.Index),
                PaintingWall.West => assets.LandscapeDeath(target.Index),
                _ => null
            };
        }

        // Cuts the painting's own voice dead at the instant of death (it may be looping all round).
        private void StopExhibitCue()
        {
            var source = FindExhibitCueSource();
            if (source != null)
            {
                source.Stop();
            }
        }

        private static AudioSource FindExhibitCueSource()
        {
            var cue = GameObject.Find(ExhibitCueObjectName);
            return cue != null ? cue.GetComponent<AudioSource>() : null;
        }

        // Parented to the camera so it stays at the player's ear for the whole beat.
        private void PlayVoiceBehindHead(AudioClip clip, float startTime)
        {
            var camera = ResolvePlayerCamera();
            if (camera == null)
            {
                return;
            }

            spawnedBehindVoice = new GameObject("DeathVoiceBehind");
            spawnedBehindVoice.transform.SetParent(camera, false);
            spawnedBehindVoice.transform.localPosition = soundBehindOffset;
            var source = spawnedBehindVoice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            // Inside minDistance there's no attenuation, so the offset only steers the panning.
            source.minDistance = 1f;
            source.loop = true;
            source.clip = clip;
            source.volume = soundBehindVolume * (SoundBank.Instance != null ? SoundBank.Instance.SfxVolume : 1f);
            source.time = Mathf.Clamp(startTime, 0f, clip.length - 0.01f);
            source.Play();
        }

        private void StopVoiceBehindHead()
        {
            if (spawnedBehindVoice != null)
            {
                spawnedBehindVoice.GetComponent<AudioSource>().Stop();
            }
        }

        // The joined voice clip has short silences between phrases; starting inside one would
        // leave the whole beat silent, so skip ahead (wrapping) to the next 10ms window that's
        // actually voiced. GetData needs Decompress On Load (Unity's default for a short clip);
        // any other load type just starts where the cue was.
        private static float FindVoicedTime(AudioClip clip, float from)
        {
            const float VoicedRms = 0.0056f; // about -45 dBFS, the same line the gaps were trimmed at
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                return from;
            }

            int window = clip.frequency / 100;
            var buffer = new float[window * clip.channels];
            int total = clip.samples;
            int start = Mathf.Clamp((int)(from * clip.frequency), 0, total - 1);
            for (int scanned = 0; scanned < total; scanned += window)
            {
                int pos = (start + scanned) % total;
                if (pos + window > total || !clip.GetData(buffer, pos))
                {
                    continue;
                }

                double acc = 0;
                foreach (float v in buffer)
                {
                    acc += v * v;
                }
                if (Math.Sqrt(acc / buffer.Length) >= VoicedRms)
                {
                    return (float)pos / clip.frequency;
                }
            }
            return from;
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

            if (glitchedRenderer != null)
            {
                glitchedRenderer.sharedMaterial = glitchedMaterialBefore;
                glitchedRenderer.enabled = true;
                glitchedRenderer = null;
                glitchedMaterialBefore = null;
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

            if (spawnedBehindVoice != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(spawnedBehindVoice);
                }
                spawnedBehindVoice = null;
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
