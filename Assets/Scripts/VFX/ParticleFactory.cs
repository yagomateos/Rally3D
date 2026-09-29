using UnityEngine;

namespace Rally.VFX
{
    /// <summary>Configures particle systems in code so effects stay consistent and data driven.</summary>
    public static class ParticleFactory
    {
        public struct Settings
        {
            public string name;
            public Material material;
            public int maxParticles;
            public float gravity;
            public float drag;
            public bool growOverLife;
            public float noiseStrength;
            public ParticleSystemRenderMode renderMode;
            public float stretch;
            public float fadeIn;
        }

        public static ParticleSystem Create(Transform parent, Settings s)
        {
            var go = new GameObject(s.name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = s.maxParticles;
            main.gravityModifier = s.gravity;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = 1f;
            main.startSpeed = 0f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, Mathf.Max(0.01f, s.fadeIn)),
                    new GradientAlphaKey(0.6f, 0.55f), new GradientAlphaKey(0f, 1f)
                });
            colorOverLife.color = gradient;

            if (s.growOverLife)
            {
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.35f), new Keyframe(0.3f, 0.8f), new Keyframe(1f, 1.6f)));
            }

            if (s.drag > 0f)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = s.drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = true;
            }

            if (s.noiseStrength > 0f)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = s.noiseStrength;
                noise.frequency = 0.35f;
                noise.scrollSpeed = 0.3f;
                noise.quality = ParticleSystemNoiseQuality.Low;
            }

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = s.renderMode == ParticleSystemRenderMode.Billboard;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = s.renderMode;
            renderer.sharedMaterial = s.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = s.growOverLife ? 5f : 0f;
            renderer.maxParticleSize = 3f;
            if (s.renderMode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = s.stretch;
                renderer.lengthScale = 1f;
            }

            ps.Play();
            return ps;
        }
    }
}
