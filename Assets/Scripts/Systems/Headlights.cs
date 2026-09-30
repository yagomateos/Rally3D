using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// Real headlights for the night stage: one spot light per car (no shadows, so three of them stay cheap on the
    /// web) and a soft glow on the road just ahead. Added at load by <see cref="RaceManager"/> on night stages.
    /// </summary>
    public class Headlights : MonoBehaviour
    {
        [SerializeField] private float range = 70f;
        [SerializeField] private float spotAngle = 62f;
        [SerializeField] private float intensity = 9f;
        [SerializeField] private Color colour = new Color(1f, 0.95f, 0.85f);

        public Light Beam { get; private set; }

        private void Start()
        {
            var beam = new GameObject("Headlights");
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0f, 0.75f, 2.2f);
            beam.transform.localRotation = Quaternion.Euler(7f, 0f, 0f); // aimed slightly down the road
            Beam = beam.AddComponent<Light>();
            Beam.type = LightType.Spot;
            Beam.range = range;
            Beam.spotAngle = spotAngle;
            Beam.innerSpotAngle = spotAngle * 0.45f;
            Beam.intensity = intensity;
            Beam.color = colour;
            Beam.shadows = LightShadows.None;
            Beam.renderMode = LightRenderMode.ForcePixel;

            // A dim fill around the player's car so its own body isn't a black shape in the dark (player only:
            // every extra light costs on phones).
            var participant = GetComponent<RaceParticipant>();
            if (participant == null || !participant.IsPlayer) return;
            var fill = new GameObject("CarGlow");
            fill.transform.SetParent(transform, false);
            fill.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            var glow = fill.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 6f;
            glow.intensity = 0.6f;
            glow.color = new Color(0.6f, 0.7f, 1f);
            glow.shadows = LightShadows.None;
        }
    }
}
