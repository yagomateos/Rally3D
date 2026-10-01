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
        private static readonly Dictionary<AudioClip, float[]> sampleData = new Dictionary<AudioClip, float[]>();

        private static AudioClip Cached(string name, int samples, System.Func<int, float[]> generate)
        {
            if (cache.TryGetValue(name, out var clip) && clip != null) return clip;
            float[] data = generate(samples);
            clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            cache[name] = clip;
            sampleData[clip] = data;
            return clip;
        }

        /// <summary>
        /// Muffled copy of a generated loop, as heard from inside the car (CABINA view). WebGL has no audio filters,
        /// so the low-pass is baked into a second clip. Null for clips this class didn't generate.
        /// </summary>
        public static AudioClip Muffled(AudioClip source, float cutoffHz)
        {
            if (source == null || !sampleData.TryGetValue(source, out var data)) return null;
            return Cached($"{source.name}_Muffled{cutoffHz:0}", data.Length, n =>
            {
                var output = new float[n];
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / SampleRate);
                // Two one-pole stages; a first pass over the loop warms the filters up so the copy loops seamlessly.
                float s1 = 0f, s2 = 0f;
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < n; i++)
                    {
                        s1 += (data[i] - s1) * a;
                        s2 += (s1 - s2) * a;
                        if (pass == 1) output[i] = s2;
                    }
                return output;
            });
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

        /// <summary>Sheep bleat, "beeeh": a voiced tone with vowel formants and the fast tremolo that makes it quaver.</summary>
        public static AudioClip Bleat() => Cached("Bleat", (int)(SampleRate * 0.85f), n =>
            Voice(n, new System.Random(61), t => Mathf.Lerp(185f, 232f, Mathf.Clamp01(t / 0.06f)) - t * 30f,
                tremoloHz: 22f, tremoloDepth: 0.55f, formant1: 620f, formant2: 1900f, noise: 0.05f, drive: 1.2f, peak: 0.6f));

        /// <summary>A sheep hit by a car: high, loud, rising then falling panicked bleat.</summary>
        public static AudioClip SheepScream() => Cached("SheepScream", (int)(SampleRate * 1.1f), n =>
            Voice(n, new System.Random(67), t => t < 0.25f ? Mathf.Lerp(360f, 540f, t / 0.25f) : Mathf.Lerp(540f, 300f, (t - 0.25f) / 0.85f),
                tremoloHz: 31f, tremoloDepth: 0.7f, formant1: 850f, formant2: 2300f, noise: 0.16f, drive: 2.2f, peak: 0.95f));

        /// <summary>
        /// Harmonic voice: up to 24 harmonics of a gliding pitch, each weighted by two vowel formants, amplitude tremolo
        /// (with a touch of matching vibrato), breath noise, soft clipping and a short attack / release.
        /// </summary>
        private static float[] Voice(int n, System.Random rng, System.Func<float, float> pitch, float tremoloHz, float tremoloDepth,
            float formant1, float formant2, float noise, float drive, float peak)
        {
            var data = new float[n];
            float phase = 0f, breath = 0f, max = 0.0001f;
            float duration = (float)n / SampleRate;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float trem = Mathf.Sin(t * tremoloHz * Mathf.PI * 2f);
                float f0 = pitch(t) * (1f + trem * 0.035f);
                phase += f0 / SampleRate * Mathf.PI * 2f;
                if (phase > Mathf.PI * 2000f) phase -= Mathf.PI * 2000f;

                float v = 0f;
                for (int h = 1; h <= 24; h++)
                {
                    float f = f0 * h;
                    if (f > 6000f) break;
                    float w = Mathf.Exp(-Sq((f - formant1) / 260f)) + 0.6f * Mathf.Exp(-Sq((f - formant2) / 420f)) + 0.12f / h;
                    v += Mathf.Sin(phase * h) * w;
                }
                breath = Mathf.Lerp(breath, (float)rng.NextDouble() * 2f - 1f, 0.3f);
                v += breath * noise * 3f;
                v *= 1f - tremoloDepth * (0.5f + 0.5f * trem);
                float env = Mathf.Clamp01(t / 0.04f) * Mathf.Clamp01((duration - t) / 0.15f);
                v = (float)System.Math.Tanh(v * drive) * env;
                data[i] = v;
                max = Mathf.Max(max, Mathf.Abs(v));
            }
            for (int i = 0; i < n; i++) data[i] *= peak / max;
            return data;
        }

        private static float Sq(float x) => x * x;

        /// <summary>
        /// Victory music: a brass fanfare (ta-ta-ta-taaa, a rising arpeggio and a held final chord with a snare roll
        /// and a cymbal crash) followed by a short, bouncy victory tune with bass and drums. About 8.5 s.
        /// </summary>
        public static AudioClip Fanfare() => Cached("Fanfare", (int)(SampleRate * 8.6f), n =>
        {
            var data = new float[n];
            // (frequency Hz, start s, length s, volume)
            var notes = new List<(float f, float t, float d, float v)>
            {
                (392f, 0f, 0.12f, 0.8f), (392f, 0.15f, 0.12f, 0.8f), (392f, 0.3f, 0.12f, 0.8f), (523.25f, 0.45f, 0.5f, 1f),
                (659.25f, 1.0f, 0.22f, 0.9f), (783.99f, 1.25f, 0.22f, 0.9f),
                (1046.5f, 1.5f, 1.3f, 1f), (523.25f, 1.5f, 1.3f, 0.6f), (659.25f, 1.5f, 1.3f, 0.55f), (783.99f, 1.5f, 1.3f, 0.55f),
            };
            // Victory tune in C, 150 bpm (0.4 s a beat), from 3.0 s.
            float[] melody = { 523.25f, 659.25f, 783.99f, 659.25f, 698.46f, 880f, 1046.5f, 880f, 783.99f, 987.77f, 1174.66f, 987.77f, 1046.5f, 783.99f, 1046.5f, 0f };
            float[] bass = { 130.81f, 174.61f, 196f, 130.81f };
            for (int i = 0; i < melody.Length; i++)
                if (melody[i] > 0f) notes.Add((melody[i], 3f + i * 0.3f, i == 14 ? 0.8f : 0.24f, 0.7f));
            for (int b = 0; b < 4; b++)
                for (int k = 0; k < 4; k++) notes.Add((bass[b], 3f + b * 1.2f + k * 0.3f, 0.22f, 0.55f));
            notes.Add((523.25f, 7.8f, 0.7f, 0.5f)); notes.Add((659.25f, 7.8f, 0.7f, 0.45f)); notes.Add((783.99f, 7.8f, 0.7f, 0.45f));

            foreach (var (f, t0, d, vol) in notes)
            {
                int start = (int)(t0 * SampleRate), len = (int)((d + 0.08f) * SampleRate);
                float phase = 0f;
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float vib = t > 0.15f ? 1f + Mathf.Sin(t * 5.5f * Mathf.PI * 2f) * 0.006f : 1f;
                    phase += f * vib / SampleRate * Mathf.PI * 2f;
                    float s = 0f;
                    for (int h = 1; h <= 8; h++) s += Mathf.Sin(phase * h) / h * (h == 1 ? 1f : 0.8f); // brassy saw-ish tone
                    float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((d + 0.08f - t) / 0.08f);
                    data[start + i] += s * env * vol * 0.18f;
                }
            }

            var rng = new System.Random(71);
            void Noise(float t0, float length, float decay, float vol, float tone)
            {
                int start = (int)(t0 * SampleRate), len = (int)(length * SampleRate);
                float lp = 0f;
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, tone);
                    data[start + i] += lp * Mathf.Exp(-t * decay) * vol;
                }
            }
            void Kick(float t0)
            {
                int start = (int)(t0 * SampleRate), len = (int)(0.18f * SampleRate);
                float phase = 0f;
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    phase += Mathf.Lerp(120f, 45f, t / 0.18f) / SampleRate * Mathf.PI * 2f;
                    data[start + i] += Mathf.Sin(phase) * Mathf.Exp(-t * 18f) * 0.5f;
                }
            }
            for (float t = 1.2f; t < 1.5f; t += 0.035f) Noise(t, 0.05f, 60f, 0.12f, 0.9f); // snare roll
            Noise(1.5f, 1.6f, 2.2f, 0.22f, 0.95f);                                             // cymbal crash
            Kick(0.45f); Kick(1.5f);
            for (int beat = 0; beat < 16; beat++)
            {
                Kick(3f + beat * 0.6f);
                Noise(3.3f + beat * 0.6f, 0.12f, 30f, 0.14f, 0.85f); // snare on the off-beat
            }
            Noise(7.8f, 0.8f, 4f, 0.18f, 0.95f);

            float max = 0.0001f;
            for (int i = 0; i < n; i++) { data[i] = (float)System.Math.Tanh(data[i] * 1.4f); max = Mathf.Max(max, Mathf.Abs(data[i])); }
            for (int i = 0; i < n; i++) data[i] *= 0.8f / max;
            return data;
        });

        /// <summary>Crowd cheering and clapping, swelling and fading over about 5 s.</summary>
        public static AudioClip Cheer() => Cached("Cheer", SampleRate * 5, n =>
        {
            var data = new float[n];
            var rng = new System.Random(83);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float w = (float)rng.NextDouble() * 2f - 1f;
                lp1 = Mathf.Lerp(lp1, w, 0.12f);  // band of voices
                lp2 = Mathf.Lerp(lp2, lp1, 0.3f);
                float swell = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((5f - t) / 1.5f) * (0.75f + 0.25f * Mathf.Sin(t * 3.1f));
                data[i] = (lp1 - lp2) * swell * 1.6f;
            }
            for (int c = 0; c < 260; c++) // claps
            {
                int start = rng.Next(0, n - SampleRate / 20);
                float amp = 0.15f + (float)rng.NextDouble() * 0.2f;
                for (int i = 0; i < SampleRate / 40; i++)
                    data[start + i] += ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-i / (SampleRate * 0.004f)) * amp;
            }
            float max = 0.0001f;
            for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            for (int i = 0; i < n; i++) data[i] *= 0.6f / max;
            return data;
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
