using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// Best-run ghost for time trials against yourself. <see cref="GhostRecorder"/> samples the player's car ten
    /// times a second during the race and, on a new best time, saves the run (PlayerPrefs, compact base64).
    /// <see cref="GhostCar"/> replays it as a translucent copy of the player's car, following the stage clock.
    /// FANTASMA option in OPCIONES (on by default).
    /// </summary>
    public static class GhostRun
    {
        public const float SampleInterval = 0.1f;
        private const int FloatsPerSample = 8; // t, x, y, z, qx, qy, qz, qw

        public struct Sample
        {
            public float t;
            public Vector3 position;
            public Quaternion rotation;
        }

        private const string OptionKey = "Rally.Ghost";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(OptionKey, 1) == 1;
            set { PlayerPrefs.SetInt(OptionKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static string Key(RaceManager race) => $"Ghost_{race.Stage.stageNumber}_{race.Stage.stageName}";

        public static string Encode(IReadOnlyList<Sample> samples)
        {
            var bytes = new byte[samples.Count * FloatsPerSample * 4];
            int o = 0;
            void Put(float f) { BitConverter.GetBytes(f).CopyTo(bytes, o); o += 4; }
            foreach (var s in samples)
            {
                Put(s.t);
                Put(s.position.x); Put(s.position.y); Put(s.position.z);
                Put(s.rotation.x); Put(s.rotation.y); Put(s.rotation.z); Put(s.rotation.w);
            }
            return Convert.ToBase64String(bytes);
        }

        public static List<Sample> Decode(string data)
        {
            var list = new List<Sample>();
            if (string.IsNullOrEmpty(data)) return list;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); }
            catch (FormatException) { return list; }
            int count = bytes.Length / (FloatsPerSample * 4);
            for (int i = 0; i < count; i++)
            {
                int o = i * FloatsPerSample * 4;
                float F(int k) => BitConverter.ToSingle(bytes, o + k * 4);
                list.Add(new Sample
                {
                    t = F(0),
                    position = new Vector3(F(1), F(2), F(3)),
                    rotation = new Quaternion(F(4), F(5), F(6), F(7))
                });
            }
            return list;
        }

        /// <summary>Pose at time <paramref name="t"/>, interpolated between samples. False past the end of the run.</summary>
        public static bool Evaluate(IReadOnlyList<Sample> samples, float t, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (samples == null || samples.Count == 0 || t > samples[samples.Count - 1].t) return false;
            if (t <= samples[0].t) { position = samples[0].position; rotation = samples[0].rotation; return true; }
            // Samples are evenly spaced in time: jump close, then walk.
            int i = Mathf.Clamp(Mathf.FloorToInt((t - samples[0].t) / SampleInterval), 0, samples.Count - 2);
            while (i > 0 && samples[i].t > t) i--;
            while (i < samples.Count - 2 && samples[i + 1].t < t) i++;
            var a = samples[i];
            var b = samples[i + 1];
            float k = Mathf.InverseLerp(a.t, b.t, t);
            position = Vector3.Lerp(a.position, b.position, k);
            rotation = Quaternion.Slerp(a.rotation, b.rotation, k);
            return true;
        }
    }

    /// <summary>Records the player's run; <see cref="RaceManager"/> asks it to save when the run is a new best.</summary>
    public class GhostRecorder : MonoBehaviour
    {
        private readonly List<GhostRun.Sample> samples = new List<GhostRun.Sample>();
        private RaceManager race;
        private float nextSample;

        public int SampleCount => samples.Count;

        private void Start() => race = RaceManager.Instance;

        private void Update()
        {
            if (race == null || race.CurrentState != RaceManager.State.Racing) return;
            if (race.StageTime < nextSample) return;
            nextSample = race.StageTime + GhostRun.SampleInterval;
            samples.Add(new GhostRun.Sample { t = race.StageTime, position = transform.position, rotation = transform.rotation });
        }

        public void SaveAsBest()
        {
            if (race == null || samples.Count < 10) return;
            samples.Add(new GhostRun.Sample { t = race.StageTime, position = transform.position, rotation = transform.rotation });
            PlayerPrefs.SetString(GhostRun.Key(race), GhostRun.Encode(samples));
            PlayerPrefs.Save();
        }
    }

    /// <summary>Translucent copy of the player's car replaying the saved best run.</summary>
    public class GhostCar : MonoBehaviour
    {
        private List<GhostRun.Sample> run;
        private RaceManager race;
        private Transform player;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Color tint;

        public bool Visible { get; private set; }

        /// <summary>Builds the ghost from the player's car meshes if a saved run exists (and the option is on).</summary>
        public static GhostCar Create(RaceManager race)
        {
            if (!GhostRun.Enabled || race.Player == null) return null;
            var run = GhostRun.Decode(PlayerPrefs.GetString(GhostRun.Key(race), ""));
            if (run.Count < 10) return null;
            var material = Resources.Load<Material>("GhostCar");
            if (material == null) return null;

            var root = new GameObject("GhostCar");
            Transform car = race.Player.transform;
            foreach (var filter in car.GetComponentsInChildren<MeshFilter>())
            {
                var source = filter.GetComponent<MeshRenderer>();
                if (source == null || !source.enabled || filter.sharedMesh == null) continue;
                var part = new GameObject(filter.name);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = car.InverseTransformPoint(filter.transform.position);
                part.transform.localRotation = Quaternion.Inverse(car.rotation) * filter.transform.rotation;
                part.transform.localScale = filter.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var r = part.AddComponent<MeshRenderer>();
                var mats = new Material[filter.sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            var ghost = root.AddComponent<GhostCar>();
            ghost.run = run;
            ghost.race = race;
            ghost.player = car;
            ghost.renderers = root.GetComponentsInChildren<Renderer>();
            ghost.block = new MaterialPropertyBlock();
            ghost.tint = material.GetColor("_BaseColor");
            ghost.Place(0f);
            return ghost;
        }

        private void LateUpdate()
        {
            if (race == null) return;
            float t = race.CurrentState == RaceManager.State.Racing || race.CurrentState == RaceManager.State.Finished ? race.StageTime : 0f;
            Place(t);
        }

        private void Place(float t)
        {
            Visible = GhostRun.Evaluate(run, t, out var pos, out var rot);
            if (Visible) transform.SetPositionAndRotation(pos, rot);
            // Fade out when right on top of the player's own car, so it never hides it.
            float d = player != null ? Vector3.Distance(player.position, transform.position) : 99f;
            float alpha = Visible ? tint.a * Mathf.InverseLerp(1.5f, 6f, d) : 0f;
            block.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, alpha));
            foreach (var r in renderers)
            {
                r.enabled = alpha > 0.01f;
                r.SetPropertyBlock(block);
            }
        }
    }
}
