using System.Collections.Generic;
using Rally.Systems;
using UnityEngine;

namespace Rally.Track.Generation
{
    /// <summary>A contiguous stretch of road with a single surface, turned into one mesh.</summary>
    public struct RoadChunk
    {
        public int from;
        public int to;
        public SurfaceType surface;
    }

    /// <summary>
    /// Builds road ribbon meshes: crowned driving surface plus sloped verges that sink into the terrain.
    /// </summary>
    public static class RoadMeshBuilder
    {
        private const float VergeWidth = 1.1f;
        private const float VergeDrop = 0.32f;
        private const float Crown = 0.05f;
        private const float TextureLength = 9f;
        private const int MaxChunkSamples = 90;

        // Cross-section: lateral factor of half width (verges handled separately) and relative crown height.
        private static readonly float[] Profile = { -1f, -0.55f, 0f, 0.55f, 1f };

        public static List<RoadChunk> SplitIntoChunks(StageRoute route)
        {
            var chunks = new List<RoadChunk>();
            int start = 0;
            for (int i = 1; i < route.Count; i++)
            {
                bool surfaceChange = route.Surfaces[i] != route.Surfaces[start];
                bool tooLong = i - start >= MaxChunkSamples;
                if (!surfaceChange && !tooLong && i < route.Count - 1) continue;
                chunks.Add(new RoadChunk { from = start, to = i, surface = route.Surfaces[start] });
                start = i;
            }
            return chunks;
        }

        public static Mesh Build(StageRoute route, RoadChunk chunk, Vector3 origin)
        {
            int columns = Profile.Length + 2;
            int rows = chunk.to - chunk.from + 1;
            var vertices = new Vector3[rows * columns];
            var uvs = new Vector2[rows * columns];
            var triangles = new List<int>((rows - 1) * (columns - 1) * 6);

            for (int r = 0; r < rows; r++)
            {
                int i = chunk.from + r;
                Vector2 t = route.Tangent(i);
                Vector3 right = new Vector3(t.y, 0f, -t.x);
                Vector3 center = route.Point(i) - origin;
                float half = route.Widths[i] * 0.5f;
                float v = i * route.Spacing / TextureLength;

                int c = 0;
                vertices[r * columns + c] = center - right * (half + VergeWidth) + Vector3.down * VergeDrop;
                uvs[r * columns + c++] = new Vector2(-0.08f, v);
                foreach (float f in Profile)
                {
                    float crown = (1f - f * f) * Crown;
                    vertices[r * columns + c] = center + right * (f * half) + Vector3.up * crown;
                    uvs[r * columns + c++] = new Vector2(f * 0.5f + 0.5f, v);
                }
                vertices[r * columns + c] = center + right * (half + VergeWidth) + Vector3.down * VergeDrop;
                uvs[r * columns + c] = new Vector2(1.08f, v);
            }

            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < columns - 1; c++)
            {
                int a = r * columns + c, b = a + 1, d = a + columns, e = d + 1;
                triangles.Add(a); triangles.Add(d); triangles.Add(b);
                triangles.Add(b); triangles.Add(d); triangles.Add(e);
            }

            var mesh = new Mesh { name = $"Road_{chunk.from:0000}_{chunk.surface}" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
