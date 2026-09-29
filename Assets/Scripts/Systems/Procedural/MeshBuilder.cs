using System.Collections.Generic;
using UnityEngine;

namespace Rally.Procedural
{
    /// <summary>
    /// Incremental mesh builder with multiple sub-meshes and a set of primitive helpers
    /// (boxes, hulls, cylinders, cones, noisy spheres) used to build props, trees and cars.
    /// </summary>
    public class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<List<int>> submeshes = new List<List<int>>();

        public int VertexCount => vertices.Count;

        public Matrix4x4 Transform { get; set; } = Matrix4x4.identity;

        private List<int> Sub(int index)
        {
            while (submeshes.Count <= index) submeshes.Add(new List<int>());
            return submeshes[index];
        }

        private int AddVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            vertices.Add(Transform.MultiplyPoint3x4(p));
            normals.Add(Transform.MultiplyVector(n).normalized);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        /// <summary>Flat-shaded quad; corners are listed counter-clockwise as seen from the visible side.</summary>
        public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale = 1f)
        {
            Vector3 n = Vector3.Cross(d - a, b - a).normalized;
            Vector3 u = (b - a).normalized;
            Vector3 v = Vector3.Cross(n, u);
            Vector2 UV(Vector3 p) => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) * uvScale;
            int i0 = AddVertex(a, n, UV(a));
            int i1 = AddVertex(b, n, UV(b));
            int i2 = AddVertex(c, n, UV(c));
            int i3 = AddVertex(d, n, UV(d));
            var t = Sub(sub);
            t.Add(i0); t.Add(i3); t.Add(i1);
            t.Add(i1); t.Add(i3); t.Add(i2);
        }

        /// <summary>Quad with UVs spanning 0..1 (a = 0,0  b = 1,0  c = 1,1  d = 0,1), optionally mirrored in U.</summary>
        public void QuadUV(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool flipU = false)
        {
            Vector3 n = Vector3.Cross(d - a, b - a).normalized;
            float u0 = flipU ? 1f : 0f, u1 = 1f - u0;
            int i0 = AddVertex(a, n, new Vector2(u0, 0f));
            int i1 = AddVertex(b, n, new Vector2(u1, 0f));
            int i2 = AddVertex(c, n, new Vector2(u1, 1f));
            int i3 = AddVertex(d, n, new Vector2(u0, 1f));
            var t = Sub(sub);
            t.Add(i0); t.Add(i3); t.Add(i1);
            t.Add(i1); t.Add(i3); t.Add(i2);
        }

        public void Triangle(int sub, Vector3 a, Vector3 b, Vector3 c, float uvScale = 1f)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Vector3 u = (b - a).normalized;
            Vector3 v = Vector3.Cross(n, u);
            Vector2 UV(Vector3 p) => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) * uvScale;
            var t = Sub(sub);
            t.Add(AddVertex(a, n, UV(a)));
            t.Add(AddVertex(b, n, UV(b)));
            t.Add(AddVertex(c, n, UV(c)));
        }

        /// <summary>
        /// Flat-shaded hexahedron from 8 corners: bottom (0..3) then top (4..7), both ordered
        /// back-left, back-right, front-right, front-left.
        /// </summary>
        public void Hull(int sub, Vector3[] c, float uvScale = 1f)
        {
            Quad(sub, c[4], c[5], c[6], c[7], uvScale); // top
            Quad(sub, c[3], c[2], c[1], c[0], uvScale); // bottom
            Quad(sub, c[7], c[6], c[2], c[3], uvScale); // front
            Quad(sub, c[5], c[4], c[0], c[1], uvScale); // back
            Quad(sub, c[4], c[7], c[3], c[0], uvScale); // left
            Quad(sub, c[6], c[5], c[1], c[2], uvScale); // right
        }

        public void Box(int sub, Vector3 center, Vector3 size, float uvScale = 1f)
        {
            Vector3 h = size * 0.5f;
            Hull(sub, new[]
            {
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, -h.y, h.z), center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(-h.x, h.y, -h.z), center + new Vector3(h.x, h.y, -h.z),
                center + new Vector3(h.x, h.y, h.z), center + new Vector3(-h.x, h.y, h.z)
            }, uvScale);
        }

        /// <summary>Tapered box: top face scaled by <paramref name="topScale"/> (x, z) and shifted along z.</summary>
        public void TaperedBox(int sub, Vector3 center, Vector3 size, Vector2 topScale, float topShiftZ = 0f, float uvScale = 1f)
        {
            Vector3 h = size * 0.5f;
            float tx = h.x * topScale.x, tz = h.z * topScale.y;
            Hull(sub, new[]
            {
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, -h.y, h.z), center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(-tx, h.y, -tz + topShiftZ), center + new Vector3(tx, h.y, -tz + topShiftZ),
                center + new Vector3(tx, h.y, tz + topShiftZ), center + new Vector3(-tx, h.y, tz + topShiftZ)
            }, uvScale);
        }

        /// <summary>Smooth-sided frustum along +Y (cylinder when radii match, cone when top = 0).</summary>
        public void Frustum(int sub, Vector3 baseCenter, float bottomRadius, float topRadius, float height, int segments,
            bool capBottom = true, bool capTop = true, float jitter = 0f, int seed = 0)
        {
            var t = Sub(sub);
            float slope = (bottomRadius - topRadius) / Mathf.Max(0.0001f, height);
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                float j = jitter > 0f ? 1f + (Noise.Hash01(i % segments, seed, 5) - 0.5f) * jitter : 1f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 n = new Vector3(dir.x, slope, dir.z).normalized;
                float u = (float)i / segments;
                AddVertex(baseCenter + dir * bottomRadius * j, n, new Vector2(u * 3f, 0f));
                AddVertex(baseCenter + dir * topRadius * j + Vector3.up * height, n, new Vector2(u * 3f, height));
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = start + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                t.Add(b0); t.Add(t0); t.Add(b1);
                t.Add(b1); t.Add(t0); t.Add(t1);
            }
            if (capTop && topRadius > 0.001f) Disc(sub, baseCenter + Vector3.up * height, topRadius, segments, true);
            if (capBottom && bottomRadius > 0.001f) Disc(sub, baseCenter, bottomRadius, segments, false);
        }

        public void Disc(int sub, Vector3 center, float radius, int segments, bool facingUp)
        {
            var t = Sub(sub);
            Vector3 n = facingUp ? Vector3.up : Vector3.down;
            int c = AddVertex(center, n, new Vector2(0.5f, 0.5f));
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                AddVertex(center + d * radius, n, new Vector2(d.x * 0.5f + 0.5f, d.z * 0.5f + 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                if (facingUp) { t.Add(c); t.Add(start + i + 1); t.Add(start + i); }
                else { t.Add(c); t.Add(start + i); t.Add(start + i + 1); }
            }
        }

        /// <summary>Faceted noisy sphere (rocks, bushes, foliage clumps).</summary>
        public void Blob(int sub, Vector3 center, Vector3 radii, int subdivisions, float noise, int seed, bool flatShaded = true)
        {
            var (verts, tris) = Icosphere(subdivisions);
            var displaced = new Vector3[verts.Count];
            float so = seed * 1.731f;
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                float n = Mathf.PerlinNoise(v.x * 1.7f + so, v.y * 1.7f + v.z * 1.3f - so) * 2f - 1f;
                float n2 = Mathf.PerlinNoise(v.z * 4.1f - so, v.x * 3.7f + v.y * 2.3f + so) * 2f - 1f;
                float r = 1f + n * noise + n2 * noise * 0.35f;
                displaced[i] = center + Vector3.Scale(v * r, radii);
            }

            var t = Sub(sub);
            if (flatShaded)
            {
                for (int i = 0; i < tris.Count; i += 3)
                    Triangle(sub, displaced[tris[i]], displaced[tris[i + 1]], displaced[tris[i + 2]]);
                return;
            }

            int start = vertices.Count;
            for (int i = 0; i < displaced.Length; i++)
                AddVertex(displaced[i], (displaced[i] - center).normalized, new Vector2(verts[i].x + verts[i].z, verts[i].y));
            foreach (int index in tris) t.Add(start + index);
        }

        private static (List<Vector3>, List<int>) Icosphere(int subdivisions)
        {
            float p = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new List<Vector3>
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1)
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var nf = new List<int>(f.Count * 4);
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int idx)) return idx;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    cache[key] = v.Count - 1;
                    return v.Count - 1;
                }
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            return (v, f);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
