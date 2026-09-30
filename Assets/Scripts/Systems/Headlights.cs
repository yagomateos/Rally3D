using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// Headlights for the night stage: a spot light per car (no shadows, so three of them stay cheap on the web) plus
    /// a pool of light drawn on the ground ahead: a translucent beam-shaped surface that follows the terrain. The pool
    /// looks the same on every device (it needs no extra lighting shader variants, which the web build may lack).
    /// Added at load by <see cref="RaceManager"/> on night stages.
    /// </summary>
    public class Headlights : MonoBehaviour
    {
        [SerializeField] private float range = 95f;
        [SerializeField] private float spotAngle = 60f;
        [SerializeField] private float intensity = 14f;
        [SerializeField] private Color colour = new Color(1f, 0.95f, 0.85f);

        public Light Beam { get; private set; }
        public MeshRenderer Pool { get; private set; }

        private const int Rows = 9;
        // From the chase camera the car's roof hides the road up to ~30 m ahead, so the pool stays bright well past
        // that and only fades towards its far end.
        private const float PoolNear = 1.8f, PoolFar = 75f, PoolNearHalfWidth = 1.2f, PoolFarHalfWidth = 10f;
        private Mesh poolMesh;
        private Vector3[] poolVerts;
        private int groundMask;

        private void Start()
        {
            BuildPool();
            var beam = new GameObject("Headlights");
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0f, 0.75f, 2.2f);
            beam.transform.localRotation = Quaternion.Euler(3.5f, 0f, 0f); // aimed down the road, reaching far
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

        private void BuildPool()
        {
            // Sprites/Default: unlit, translucent, and always in every build (a particle shader variant the
            // downloadable stages' bundles might lack left the pool invisible on the web).
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            var mat = new Material(shader) { name = "HeadlightPool", mainTexture = PoolTexture(), color = new Color(1f, 0.95f, 0.82f, 0.7f) };

            var go = new GameObject(name + "_LightPool");
            poolMesh = new Mesh { name = "HeadlightPool" };
            poolVerts = new Vector3[Rows * 3];
            var uvs = new Vector2[Rows * 3];
            var tris = new System.Collections.Generic.List<int>();
            for (int r = 0; r < Rows; r++)
            {
                float v = (float)r / (Rows - 1);
                for (int c = 0; c < 3; c++) uvs[r * 3 + c] = new Vector2(c * 0.5f, v);
                if (r == Rows - 1) continue;
                for (int c = 0; c < 2; c++)
                {
                    int a = r * 3 + c, b = a + 1, d = a + 3, e = d + 1;
                    tris.AddRange(new[] { a, d, b, b, d, e });
                }
            }
            poolMesh.vertices = poolVerts;
            poolMesh.uv = uvs;
            // The shader multiplies by vertex colour; without it WebGL reads black and the pool vanishes.
            var white = new Color[poolVerts.Length];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            poolMesh.colors = white;
            poolMesh.triangles = tris.ToArray();
            go.AddComponent<MeshFilter>().sharedMesh = poolMesh;
            Pool = go.AddComponent<MeshRenderer>();
            Pool.sharedMaterial = mat;
            Pool.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Pool.receiveShadows = false;
            groundMask = ~(1 << LayerMask.NameToLayer("Vehicle") | 1 << 2);
        }

        /// <summary>Soft beam: bright in the middle, fading to the sides and into the distance.</summary>
        private static Texture2D PoolTexture()
        {
            const int w = 64, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HeadlightPool" };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float side = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f), 1.4f);
                    float along = Mathf.SmoothStep(0f, 1f, v / 0.06f) * (1f - Mathf.SmoothStep(0.55f, 1f, v));
                    px[y * w + x] = new Color(1f, 1f, 1f, side * along);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private void LateUpdate()
        {
            if (poolMesh == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            for (int r = 0; r < Rows; r++)
            {
                float t = (float)r / (Rows - 1);
                float d = Mathf.Lerp(PoolNear, PoolFar, t);
                float half = Mathf.Lerp(PoolNearHalfWidth, PoolFarHalfWidth, t);
                for (int c = 0; c < 3; c++)
                {
                    Vector3 p = transform.position + fwd * d + right * ((c - 1) * half);
                    // Lie on whatever is below (road, terrain), a little above it: the web's depth buffer is coarser
                    // than a desktop GPU's, and 6 cm let the road hide the pool there.
                    if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var hit, 12f, groundMask, QueryTriggerInteraction.Ignore))
                        p.y = hit.point.y + 0.25f;
                    poolVerts[r * 3 + c] = p;
                }
            }
            poolMesh.vertices = poolVerts;
            poolMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (Pool != null) Destroy(Pool.gameObject);
        }
    }
}
