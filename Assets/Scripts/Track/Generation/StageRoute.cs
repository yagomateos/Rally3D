using System.Collections.Generic;
using Rally.Systems;
using UnityEngine;

namespace Rally.Track.Generation
{
    /// <summary>
    /// Turns a <see cref="StageDefinition"/> into an evenly sampled centre line (planar + elevation).
    /// </summary>
    public class StageRoute
    {
        public readonly List<Vector2> Planar = new List<Vector2>();
        public readonly List<float> Widths = new List<float>();
        public readonly List<SurfaceType> Surfaces = new List<SurfaceType>();
        public readonly List<RoadsideStyle> Roadside = new List<RoadsideStyle>();
        public readonly List<Vector2> Jumps = new List<Vector2>(); // x = distance, y = height
        public float[] Heights;
        public float Spacing { get; private set; }

        public int Count => Planar.Count;
        public float Length => (Count - 1) * Spacing;

        public Vector3 Point(int i) => new Vector3(Planar[i].x, Heights != null ? Heights[i] : 0f, Planar[i].y);

        public Vector2 Tangent(int i)
        {
            int a = Mathf.Max(0, i - 1), b = Mathf.Min(Count - 1, i + 1);
            return (Planar[b] - Planar[a]).normalized;
        }

        public Rect Bounds()
        {
            Vector2 min = Planar[0], max = Planar[0];
            foreach (var p in Planar)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static StageRoute Build(StageDefinition def)
        {
            var route = new StageRoute { Spacing = def.sampleSpacing };
            float step = def.sampleSpacing;
            Vector2 pos = def.startPosition;
            float heading = def.startHeading;
            float distance = 0f;

            route.Add(pos, def.segments[0]);

            // Arc and line lengths are accumulated in a continuous "carry" so samples stay evenly spaced.
            foreach (var seg in def.segments)
            {
                float length = seg.kind == SegmentKind.Straight
                    ? seg.amount
                    : Mathf.Abs(seg.amount) * Mathf.Deg2Rad * seg.radius;
                float headingRate = seg.kind == SegmentKind.Turn ? seg.amount / length : 0f;
                int steps = Mathf.Max(1, Mathf.RoundToInt(length / step));
                float ds = length / steps;

                if (seg.jumpHeight > 0f) route.Jumps.Add(new Vector2(distance + length * 0.55f, seg.jumpHeight));

                for (int i = 0; i < steps; i++)
                {
                    heading += headingRate * ds * 0.5f;
                    float rad = heading * Mathf.Deg2Rad;
                    pos += new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * ds;
                    heading += headingRate * ds * 0.5f;
                    route.Add(pos, seg);
                }
                distance += length;
            }

            route.Resample(step);
            return route;
        }

        private void Add(Vector2 p, TrackSegment seg)
        {
            Planar.Add(p);
            Widths.Add(seg.width);
            Surfaces.Add(seg.surface);
            Roadside.Add(seg.roadside);
        }

        /// <summary>Re-sample to exact constant spacing and smooth width transitions.</summary>
        private void Resample(float step)
        {
            var srcP = new List<Vector2>(Planar);
            var srcW = new List<float>(Widths);
            var srcS = new List<SurfaceType>(Surfaces);
            var srcR = new List<RoadsideStyle>(Roadside);
            Planar.Clear(); Widths.Clear(); Surfaces.Clear(); Roadside.Clear();

            Planar.Add(srcP[0]); Widths.Add(srcW[0]); Surfaces.Add(srcS[0]); Roadside.Add(srcR[0]);
            float carry = 0f;
            for (int i = 1; i < srcP.Count; i++)
            {
                Vector2 a = srcP[i - 1], b = srcP[i];
                float segLen = Vector2.Distance(a, b);
                float t = step - carry;
                while (t <= segLen)
                {
                    Planar.Add(Vector2.Lerp(a, b, t / segLen));
                    Widths.Add(srcW[i]);
                    Surfaces.Add(srcS[i]);
                    Roadside.Add(srcR[i]);
                    t += step;
                }
                carry = segLen - (t - step);
            }

            // Smooth width changes over ~30 m so the road never steps.
            int radius = Mathf.CeilToInt(15f / step);
            var smoothed = new float[Widths.Count];
            for (int i = 0; i < Widths.Count; i++)
            {
                float sum = 0f; int n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int j = Mathf.Clamp(i + k, 0, Widths.Count - 1);
                    sum += Widths[j]; n++;
                }
                smoothed[i] = sum / n;
            }
            for (int i = 0; i < smoothed.Length; i++) Widths[i] = smoothed[i];
        }
    }
}
