using System.Collections.Generic;
using UnityEngine;

namespace Rally.Audio
{
    /// <summary>
    /// Synthesises placeholder sound effects so the game is fully audible without audio assets.
    /// Every clip is generated once and cached. Real clips can be assigned on the components instead.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;
        /// <summary>RPM the engine loops are synthesised at (pitch 1).</summary>
        public const float EngineBaseRpm = 3000f;

        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        private static AudioClip Cached(string name, int samples, System.Func<int, float[]> generate)
        {
            if (cache.TryGetValue(name, out var clip) && clip != null) return clip;
            float[] data = generate(samples);
            clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            cache[name] = clip;
            return clip;
        }

        /// <summary>4-cylinder engine loop. <paramref name="load"/> adds harmonics and grit.</summary>
        public static AudioClip Engine(bool onLoad)
        {
            return Cached(onLoad ? "Engine_OnLoad" : "Engine_OffLoad", SampleRate, n =>
            {
                var data = new float[n];
                var rng = new System.Random(onLoad ? 11 : 23);
                float firing = EngineBaseRpm / 60f * 2f; // 100 Hz: loop contains an exact number of cycles
                float noiseState = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float phase = t * firing * Mathf.PI * 2f;

                    // Pulse-like combustion wave: fundamental + odd/even harmonics.
                    float s = Mathf.Sin(phase) * 0.9f
                              + Mathf.Sin(phase * 2f + 0.4f) * (onLoad ? 0.55f : 0.3f)
                              + Mathf.Sin(phase * 3f + 1.1f) * (onLoad ? 0.4f : 0.12f)
                              + Mathf.Sin(phase * 4f + 0.2f) * (onLoad ? 0.25f : 0.05f)
                              + Mathf.Sin(phase * 0.5f) * 0.35f; // cylinder imbalance rumble
                    s = (float)System.Math.Tanh(s * (onLoad ? 1.8f : 1.1f));

                    // Filtered noise for exhaust roughness, gated by the firing pulses.
                    noiseState = Mathf.Lerp(noiseState, (float)rng.NextDouble() * 2f - 1f, 0.25f);
                    float gate = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase)), 4f);
                    s += noiseState * gate * (onLoad ? 0.45f : 0.2f);

                    data[i] = s * 0.5f;
                }
                return data;
            });
        }

        public static AudioClip Turbo() => Cached("Turbo", SampleRate, n =>
        {
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                data[i] = (Mathf.Sin(t * 2200f * Mathf.PI * 2f) * 0.6f + Mathf.Sin(t * 3300f * Mathf.PI * 2f) * 0.2f) * 0.3f;
            }
            return data;
        });

        /// <summary>Loose gravel crunching under sliding tyres.</summary>
        public static AudioClip GravelSlide() => Cached("GravelSlide", SampleRate * 2, n =>
        {
            var data = new float[n];
            var rng = new System.Random(5);
            float lp = 0f, env = 0f;
            for (int i = 0; i < n; i++)
            {
                if (rng.NextDouble() < 0.004) env = 0.6f + (float)rng.NextDouble() * 0.4f;
                env *= 0.9985f;
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp = Mathf.Lerp(lp, white, 0.35f);
                data[i] = (lp * 0.35f + white * env * 0.4f) * 0.8f;
            }
            return Loopify(data);
        });

        /// <summary>Continuous tyre roll on loose surfaces.</summary>
        public static AudioClip Rolling() => Cached("Rolling", SampleRate * 2, n =>
        {
            var data = new float[n];
            var rng = new System.Random(9);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                if (rng.NextDouble() < 0.02) white *= 4f;
                lp = Mathf.Lerp(lp, white, 0.12f);
                lp2 = Mathf.Lerp(lp2, lp, 0.3f);
                data[i] = lp2 * 0.9f;
            }
            return Loopify(data);
        });

        /// <summary>Tarmac tyre squeal.</summary>
        public static AudioClip Squeal() => Cached("Squeal", SampleRate, n =>
        {
            var data = new float[n];
            var rng = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float f = 820f + Mathf.Sin(t * 7f * Mathf.PI * 2f) * 25f;
                data[i] = (Mathf.Sin(t * f * Mathf.PI * 2f) * 0.35f + ((float)rng.NextDouble() * 2f - 1f) * 0.12f) * 0.6f;
            }
            return Loopify(data);
        });

        public static AudioClip Wind() => Cached("Wind", SampleRate * 3, n =>
        {
            var data = new float[n];
            var rng = new System.Random(17);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.05f);
                float swell = 0.7f + Mathf.Sin(t * 0.9f) * 0.2f + Mathf.Sin(t * 2.3f) * 0.1f;
                data[i] = lp * swell * 1.6f;
            }
            return Loopify(data);
        });

        public static AudioClip Impact() => Cached("Impact", SampleRate / 2, n =>
        {
            var data = new float[n];
            var rng = new System.Random(29);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 14f);
                lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.3f);
                data[i] = (Mathf.Sin(t * 70f * Mathf.PI * 2f) * 0.8f + lp * 0.7f + Mathf.Sin(t * 410f * Mathf.PI * 2f) * 0.2f * Mathf.Exp(-t * 30f)) * env;
            }
            return data;
        });

        public static AudioClip Landing() => Cached("Landing", SampleRate * 3 / 4, n =>
        {
            var data = new float[n];
            var rng = new System.Random(31);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float thump = Mathf.Sin(t * 48f * Mathf.PI * 2f) * Mathf.Exp(-t * 9f);
                float rattle = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 6f) * 0.35f * (Mathf.Sin(t * 60f) > 0.3f ? 1f : 0.3f);
                data[i] = (thump + rattle) * 0.9f;
            }
            return data;
        });

        /// <summary>Forest ambience: soft wind bed, distant birds and water drips after the rain.</summary>
        public static AudioClip Ambience() => Cached("Ambience", SampleRate * 16, n =>
        {
            var data = new float[n];
            var rng = new System.Random(41);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.02f);
                data[i] = lp * 0.5f;
            }
            // Bird chirps.
            for (int c = 0; c < 26; c++)
            {
                int start = rng.Next(0, n - SampleRate);
                float baseF = 2400f + (float)rng.NextDouble() * 2200f;
                int notes = rng.Next(2, 6);
                for (int k = 0; k < notes; k++)
                {
                    int s0 = start + k * (int)(SampleRate * 0.13f);
                    int len = (int)(SampleRate * 0.08f);
                    for (int i = 0; i < len && s0 + i < n; i++)
                    {
                        float t = (float)i / SampleRate;
                        float f = baseF + Mathf.Sin(t * 60f) * 500f + k * 120f;
                        float env = Mathf.Sin(Mathf.PI * i / len);
                        data[s0 + i] += Mathf.Sin(t * f * Mathf.PI * 2f) * env * 0.05f;
                    }
                }
            }
            // Drips.
            for (int d = 0; d < 40; d++)
            {
                int start = rng.Next(0, n - 4000);
                float f = 900f + (float)rng.NextDouble() * 900f;
                for (int i = 0; i < 3000; i++)
                {
                    float t = (float)i / SampleRate;
                    data[start + i] += Mathf.Sin(t * (f + t * 4000f) * Mathf.PI * 2f) * Mathf.Exp(-t * 60f) * 0.04f;
                }
            }
            return Loopify(data);
        });

        public static AudioClip Beep(float frequency, float duration) => Cached($"Beep_{frequency}_{duration}", (int)(SampleRate * duration), n =>
        {
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Min(1f, t * 200f) * Mathf.Min(1f, (duration - t) * 30f);
                data[i] = (Mathf.Sin(t * frequency * Mathf.PI * 2f) * 0.7f + Mathf.Sin(t * frequency * 2f * Mathf.PI * 2f) * 0.15f) * env * 0.5f;
            }
            return data;
        });

        /// <summary>Crossfades the clip's tail into its head so it loops without clicks.</summary>
        private static float[] Loopify(float[] data)
        {
            int fade = Mathf.Min(data.Length / 8, SampleRate / 10);
            int n = data.Length - fade;
            var result = new float[n];
            System.Array.Copy(data, result, n);
            for (int i = 0; i < fade; i++)
            {
                float t = (float)i / fade;
                result[i] = Mathf.Lerp(data[n + i], data[i], t);
            }
            return result;
        }
    }
}
