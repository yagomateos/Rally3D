using UnityEngine;

namespace Rally.VFX
{
    /// <summary>
    /// "Overcast after rain" atmosphere around the camera: floating mist motes, a light residual
    /// drizzle and low drifting fog wisps. Follows the camera so density is constant everywhere.
    /// </summary>
    public class WeatherEffects : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Material moteMaterial;
        [SerializeField] private Material drizzleMaterial;
        [SerializeField] private Material fogMaterial;
        [SerializeField, Range(0f, 1f)] private float drizzle = 0.25f;
        [SerializeField] private float motesPerSecond = 40f;
        [SerializeField] private float fogWispsPerSecond = 3f;

        private ParticleSystem motes, rain, wisps;

        public void Configure(Transform target, Material mote, Material rainMat, Material fogMat)
        {
            followTarget = target;
            moteMaterial = mote;
            drizzleMaterial = rainMat;
            fogMaterial = fogMat;
        }

        private void Start()
        {
            if (followTarget == null && Camera.main != null) followTarget = Camera.main.transform;

            motes = Create("Motes", moteMaterial, 500, ParticleSystemRenderMode.Billboard, 0f);
            var shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 14f, 40f);
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startSpeed = 0.2f;
            main.startColor = new Color(1f, 1f, 1f, 0.5f);
            var emission = motes.emission;
            emission.rateOverTime = motesPerSecond;
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.2f;

            rain = Create("Drizzle", drizzleMaterial, 1500, ParticleSystemRenderMode.Stretch, 1f);
            var rshape = rain.shape;
            rshape.enabled = true;
            rshape.shapeType = ParticleSystemShapeType.Box;
            rshape.scale = new Vector3(45f, 1f, 45f);
            rshape.position = new Vector3(0f, 14f, 0f);
            var rmain = rain.main;
            rmain.startLifetime = 1.4f;
            rmain.startSpeed = 11f;
            rmain.startSize = 0.018f;
            rmain.startColor = new Color(0.85f, 0.88f, 0.92f, 0.35f);
            rain.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var remission = rain.emission;
            remission.rateOverTime = 900f * drizzle;
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1f;

            wisps = Create("FogWisps", fogMaterial, 60, ParticleSystemRenderMode.Billboard, 0f);
            var wshape = wisps.shape;
            wshape.enabled = true;
            wshape.shapeType = ParticleSystemShapeType.Box;
            wshape.scale = new Vector3(160f, 4f, 160f);
            wshape.position = new Vector3(0f, -1f, 0f);
            var wmain = wisps.main;
            wmain.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
            wmain.startSize = new ParticleSystem.MinMaxCurve(14f, 26f);
            wmain.startSpeed = 0.3f;
            wmain.startColor = new Color(0.8f, 0.83f, 0.86f, 0.1f);
            var wemission = wisps.emission;
            wemission.rateOverTime = fogWispsPerSecond;
            var col = wisps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            wisps.GetComponent<ParticleSystemRenderer>().sortingFudge = 50f;

            foreach (var ps in new[] { motes, rain, wisps }) ps.Play();
        }

        private ParticleSystem Create(string name, Material material, int max, ParticleSystemRenderMode mode, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.prewarm = true;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private void LateUpdate()
        {
            if (followTarget != null) transform.position = followTarget.position;
        }
    }
}
