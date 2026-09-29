using System.Collections.Generic;
using UnityEngine;

namespace Rally.Track
{
    /// <summary>
    /// Rally pace notes generated from the stage's centre line: corners graded 1 (tightest) to 6 (fastest) the way
    /// real co-drivers call them, plus HORQUILLA (hairpin), LARGA / SE CIERRA / SE ABRE modifiers and jumps
    /// (detected as sharp crests in the elevation profile).
    /// </summary>
    public static class Pacenotes
    {
        public struct Note
        {
            public float distance;   // where the corner / jump starts, along the track
            public int direction;    // -1 left, +1 right, 0 none (jump)
            public int severity;     // 0 hairpin, 1..6 corner grade
            public bool jump, longCorner, tightens, opens;

            /// <summary>Text for the HUD, e.g. "IZQUIERDA 3", "HORQUILLA DERECHA", "SALTO".</summary>
            public string Text
            {
                get
                {
                    if (jump) return "SALTO";
                    string side = direction < 0 ? "IZQUIERDA" : "DERECHA";
                    return severity == 0 ? "HORQUILLA " + side : side + " " + severity;
                }
            }

            /// <summary>Second line: LARGA, SE CIERRA, SE ABRE.</summary>
            public string Modifiers
            {
                get
                {
                    var parts = new List<string>();
                    if (longCorner) parts.Add("LARGA");
                    if (tightens) parts.Add("SE CIERRA");
                    if (opens) parts.Add("SE ABRE");
                    return string.Join(" · ", parts);
                }
            }

            /// <summary>What the co-driver says (numbers as words for the speech engine).</summary>
            public string Spoken
            {
                get
                {
                    if (jump) return "salto";
                    string[] words = { "", "uno", "dos", "tres", "cuatro", "cinco", "seis" };
                    string side = direction < 0 ? "izquierda" : "derecha";
                    string s = severity == 0 ? "horquilla " + side : side + " " + words[severity];
                    if (longCorner) s += " larga";
                    if (tightens) s += ", se cierra";
                    if (opens) s += ", se abre";
                    return s;
                }
            }
        }

        private const float Step = 4f;
        private const float CornerCurvature = 1f / 220f; // gentler than this is a straight

        /// <param name="stage">When given, jumps come from the stage definition (exact); otherwise they are
        /// estimated from the road's elevation profile.</param>
        public static List<Note> Generate(TrackPath path, StageDefinition stage = null)
        {
            var notes = new List<Note>();
            AddCorners(path, notes);
            if (stage != null) AddJumps(stage, path, notes);
            else AddJumps(path, notes);
            notes.Sort((a, b) => a.distance.CompareTo(b.distance));
            return notes;
        }

        /// <summary>Corner grade from its tightest radius (metres).</summary>
        public static int Severity(float radius)
        {
            if (radius < 20f) return 0;
            if (radius < 35f) return 1;
            if (radius < 55f) return 2;
            if (radius < 80f) return 3;
            if (radius < 115f) return 4;
            if (radius < 170f) return 5;
            return 6;
        }

        private struct Segment { public float start, end, peak, peakAt, angle; public int sign; public List<float> k; }

        private static void AddCorners(TrackPath path, List<Note> notes)
        {
            var segments = new List<Segment>();
            Segment? current = null;
            for (float d = path.StartDistance; d <= path.FinishDistance; d += Step)
            {
                float k = path.CurvatureAt(d, 8f);
                int sign = Mathf.Abs(k) > CornerCurvature ? (k > 0f ? 1 : -1) : 0;
                if (current.HasValue && (sign == 0 || sign != current.Value.sign))
                {
                    segments.Add(current.Value);
                    current = null;
                }
                if (sign == 0) continue;
                var seg = current ?? new Segment { start = d, sign = sign, k = new List<float>() };
                seg.end = d;
                seg.angle += k * Step * Mathf.Rad2Deg;
                seg.k.Add(Mathf.Abs(k));
                if (Mathf.Abs(k) > seg.peak) { seg.peak = Mathf.Abs(k); seg.peakAt = d; }
                current = seg;
            }
            if (current.HasValue) segments.Add(current.Value);

            // Merge pieces of the same corner split by a brief straighter bit.
            var merged = new List<Segment>();
            foreach (var s in segments)
            {
                if (merged.Count > 0)
                {
                    var last = merged[merged.Count - 1];
                    if (last.sign == s.sign && s.start - last.end <= 16f)
                    {
                        last.end = s.end;
                        last.angle += s.angle;
                        last.k.AddRange(s.k);
                        if (s.peak > last.peak) { last.peak = s.peak; last.peakAt = s.peakAt; }
                        merged[merged.Count - 1] = last;
                        continue;
                    }
                }
                merged.Add(s);
            }

            foreach (var s in merged)
            {
                float radius = 1f / Mathf.Max(1e-4f, s.peak);
                if (Mathf.Abs(s.angle) < 15f && radius > 60f) continue; // a kink, not worth a call
                int half = s.k.Count / 2;
                float first = 0f, second = 0f;
                for (int i = 0; i < s.k.Count; i++) { if (i < half) first += s.k[i]; else second += s.k[i]; }
                first /= Mathf.Max(1, half);
                second /= Mathf.Max(1, s.k.Count - half);
                notes.Add(new Note
                {
                    distance = s.start,
                    direction = s.sign,
                    severity = Severity(radius),
                    longCorner = s.end - s.start > 70f,
                    tightens = s.k.Count >= 6 && second > first * 1.45f,
                    opens = s.k.Count >= 6 && first > second * 1.45f,
                });
            }
        }

        private static void AddJumps(StageDefinition stage, TrackPath path, List<Note> notes)
        {
            // Same route the stage builder used, so the distances match the track's.
            foreach (var jump in Generation.StageRoute.Build(stage).Jumps)
                if (jump.x >= path.StartDistance && jump.x <= path.FinishDistance)
                    notes.Add(new Note { distance = jump.x - 12f, jump = true }); // called just before the ramp
        }

        private static void AddJumps(TrackPath path, List<Note> notes)
        {
            // A crest where the road climbs and then drops away sharply.
            float lastJump = float.NegativeInfinity;
            float bestScore = 0f, bestAt = 0f;
            for (float d = path.StartDistance + 12f; d <= path.FinishDistance - 12f; d += 2f)
            {
                float y = path.PositionAt(d).y;
                float before = (y - path.PositionAt(d - 10f).y) / 10f;
                float after = (path.PositionAt(d + 10f).y - y) / 10f;
                float score = before - after;
                bool crest = before > 0.03f && score > 0.12f;
                if (crest && score > bestScore) { bestScore = score; bestAt = d; }
                if (!crest && bestScore > 0f)
                {
                    if (bestAt - lastJump > 40f)
                    {
                        notes.Add(new Note { distance = bestAt - 6f, jump = true });
                        lastJump = bestAt;
                    }
                    bestScore = 0f;
                }
            }
        }
    }
}
