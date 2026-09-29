using Rally.Systems;
using UnityEngine;

namespace Rally.Track
{
    /// <summary>
    /// Baked centre line of the stage, sampled at constant spacing.
    /// Used by checkpoints, AI, reset and progress tracking.
    /// </summary>
    public class TrackPath : MonoBehaviour
    {
        [SerializeField] private Vector3[] points = new Vector3[0];
        [SerializeField] private float[] widths = new float[0];
        [SerializeField] private SurfaceType[] surfaces = new SurfaceType[0];
        [SerializeField] private float spacing = 2f;
        [SerializeField] private float startDistance = 40f;
        [SerializeField] private float finishDistance;

        public int Count => points.Length;
        public float Spacing => spacing;
        public float Length => (points.Length - 1) * spacing;
        public float StartDistance => startDistance;
        public float FinishDistance => finishDistance;
        public float StageLength => finishDistance - startDistance;
        public Vector3 GetPoint(int index) => points[Mathf.Clamp(index, 0, points.Length - 1)];

        public void SetData(Vector3[] pts, float[] w, SurfaceType[] s, float sampleSpacing, float start, float finish)
        {
            points = pts;
            widths = w;
            surfaces = s;
            spacing = sampleSpacing;
            startDistance = start;
            finishDistance = finish;
        }

        public Vector3 PositionAt(float distance)
        {
            float f = Mathf.Clamp(distance / spacing, 0f, points.Length - 1.001f);
            int i = (int)f;
            return Vector3.Lerp(points[i], points[i + 1], f - i);
        }

        public Vector3 TangentAt(float distance)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(distance / spacing), 0, points.Length - 2);
            int a = Mathf.Max(0, i - 1), b = Mathf.Min(points.Length - 1, i + 2);
            return (points[b] - points[a]).normalized;
        }

        public Vector3 RightAt(float distance)
        {
            Vector3 t = TangentAt(distance);
            return new Vector3(t.z, 0f, -t.x).normalized;
        }

        public float WidthAt(float distance) => widths[IndexAt(distance)];
        public SurfaceType SurfaceAt(float distance) => surfaces[IndexAt(distance)];

        public int IndexAt(float distance) => Mathf.Clamp(Mathf.RoundToInt(distance / spacing), 0, points.Length - 1);

        /// <summary>Signed curvature (1/m, + = right turn) measured over a small window.</summary>
        public float CurvatureAt(float distance, float window = 6f)
        {
            Vector3 a = TangentAt(distance - window);
            Vector3 b = TangentAt(distance + window);
            float angle = Vector3.SignedAngle(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z), Vector3.up) * Mathf.Deg2Rad;
            return angle / (2f * window);
        }

        /// <summary>Closest distance along the path, searching around a previous estimate.</summary>
        public float Project(Vector3 position, float hint, float window = 60f)
        {
            int from = Mathf.Max(0, Mathf.FloorToInt((hint - window) / spacing));
            int to = Mathf.Min(points.Length - 1, Mathf.CeilToInt((hint + window) / spacing));
            return ProjectRange(position, from, to, out _);
        }

        public float ProjectGlobal(Vector3 position) => ProjectRange(position, 0, points.Length - 1, out _);

        /// <summary>Lateral offset (m) from the centre line, + = right.</summary>
        public float LateralOffset(Vector3 position, float distance)
        {
            Vector3 d = position - PositionAt(distance);
            return Vector3.Dot(new Vector3(d.x, 0f, d.z), RightAt(distance));
        }

        private float ProjectRange(Vector3 p, int from, int to, out float sqrDistance)
        {
            float best = 0f;
            sqrDistance = float.MaxValue;
            for (int i = from; i < to; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
                Vector2 ap = new Vector2(p.x - a.x, p.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                Vector2 closest = ab * t - ap;
                float h = Mathf.Lerp(a.y, b.y, t) - p.y;
                float sqr = closest.sqrMagnitude + h * h * 0.25f;
                if (sqr < sqrDistance)
                {
                    sqrDistance = sqr;
                    best = (i + t) * spacing;
                }
            }
            return best;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < points.Length - 1; i += 2)
                Gizmos.DrawLine(points[i] + Vector3.up, points[i + 1] + Vector3.up);
        }
#endif
    }
}
