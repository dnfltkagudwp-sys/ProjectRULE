using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Continuous horror-style flicker for a Light's intensity, driven by Perlin noise so it reads
    // as an unstable flicker rather than a mechanical pulse. Used as SoundFromExhibit's non-audio
    // cue -- see AnomalyRuntimeApplier.PlaySoundCue -- so the anomaly is still noticeable to a
    // player who can't hear (or has muted) the placeholder audio cue.
    public class FlickerLight : MonoBehaviour
    {
        public float baseIntensity = 2.5f;
        public float flickerAmount = 1.5f;
        public float flickerSpeed = 8f;

        private Light targetLight;
        private float noiseOffset;

        private void Awake()
        {
            targetLight = GetComponent<Light>();
            noiseOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise(noiseOffset, Time.time * flickerSpeed);
            targetLight.intensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmount;
        }
    }
}
