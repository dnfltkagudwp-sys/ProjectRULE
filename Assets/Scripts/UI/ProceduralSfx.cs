using UnityEngine;

namespace RuleGhost.UI
{
    // Code-generated placeholder stings for the death sequences -- no horror audio assets exist in
    // the project yet, same situation (and same approach) as AnomalyRuntimeApplier's placeholder
    // exhibit cue. Each clip is built once and cached. Swapping in real assets later means
    // changing only these three properties; nothing outside this class refers to the synthesis.
    //
    // Deterministic (fixed-seed System.Random rather than UnityEngine.Random) so a given build
    // always produces the identical clip instead of a subtly different burst every session.
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;

        private static AudioClip harshBurst;
        private static AudioClip zap;
        private static AudioClip knock;

        // SoundFromExhibit's death: the thing that was across the room is suddenly at your ear.
        public static AudioClip HarshBurst
        {
            get
            {
                if (harshBurst == null) harshBurst = BuildHarshBurst();
                return harshBurst;
            }
        }

        // HighHumidity's death: the readout glitching out, not a domestic electrical fault.
        public static AudioClip Zap
        {
            get
            {
                if (zap == null) zap = BuildZap();
                return zap;
            }
        }

        // Shared 순찰 실패 death: three knocks on the guard room door.
        public static AudioClip Knock
        {
            get
            {
                if (knock == null) knock = BuildKnock();
                return knock;
            }
        }

        private static AudioClip BuildHarshBurst()
        {
            // Two layers at once rather than one loud bang: a short low-frequency impact you feel,
            // and a harsh high-frequency rasp on top. A plain explosion-style boom reads as a cheap
            // jump scare; the combination reads as something physically wrong in the room.
            const float duration = 0.3f;
            var rng = new System.Random(4711);
            var data = new float[Mathf.CeilToInt(SampleRate * duration)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;

                // Sub impact: drops 70Hz -> 40Hz, gone well before the clip ends.
                float impact = Mathf.Sin(2f * Mathf.PI * (70f - 100f * t) * t) * Mathf.Exp(-11f * t);
                // Rasp: broadband noise shaped by a much faster decay, so it's front-loaded.
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-26f * t);
                // A couple of ms of ramp so the very first sample isn't a bare click.
                float attack = Mathf.Clamp01(t / 0.002f);

                data[i] = Mathf.Clamp((impact * 0.9f + noise * 0.55f) * attack, -1f, 1f);
            }
            return Create("Death_HarshBurst", data);
        }

        private static AudioClip BuildZap()
        {
            const float duration = 0.3f;
            var rng = new System.Random(1337);
            var data = new float[Mathf.CeilToInt(SampleRate * duration)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-14f * t);
                float crackle = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 2200f * t)) * 0.5f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                data[i] = (crackle + noise * 0.8f) * envelope * 0.8f;
            }
            return Create("Death_Zap", data);
        }

        private static AudioClip BuildKnock()
        {
            const float duration = 1.0f;
            float[] knockStarts = { 0f, 0.28f, 0.54f };
            var data = new float[Mathf.CeilToInt(SampleRate * duration)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;
                foreach (float start in knockStarts)
                {
                    if (t < start) continue;
                    float local = t - start;
                    // Low, heavily damped -- a knuckle on a door, not a tone.
                    sample += Mathf.Sin(2f * Mathf.PI * 90f * local) * Mathf.Exp(-38f * local);
                }
                data[i] = Mathf.Clamp(sample, -1f, 1f) * 0.9f;
            }
            return Create("Death_Knock", data);
        }

        private static AudioClip Create(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
