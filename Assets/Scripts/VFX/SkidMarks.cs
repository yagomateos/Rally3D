using UnityEngine;

namespace Rally.VFX
{
    /// <summary>
    /// Shared ring-buffer mesh of tyre marks. Wheels add connected segments while sliding;
    /// the oldest marks are recycled.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SkidMarks : MonoBehaviour
    {
        [SerializeField] private int maxSegments = 2400;
        [SerializeField] private float markWidth = 0.24f;
        [SerializeField] private float groundOffset = 0.025f;
        [SerializeField] private float minSegmentLength = 0.25f;

        public static SkidMarks Instance { get; private set; }

        private Vector3[] vertices;
        private Vector3[] normals;
        private Color32[] colors;
        private Vector2[] uvs;
        private int[] triangles;
        private Mesh mesh;
        private int next;
        private bool dirty;

        private struct Anchor
        {
            public Vector3 left;
            public Vector3 right;
            public Vector3 position;
            public byte alpha;
        }

        private readonly System.Collections.Generic.Dictionary<int, Anchor> lastAnchor =
            new System.Collections.Generic.Dictionary<int, Anchor>();

        private void Awake()
        {
            Instance = this;
            vertices = new Vector3[maxSegments * 4];
            normals = new Vector3[maxSegments * 4];
            colors = new Color32[maxSegments * 4];
            uvs = new Vector2[maxSegments * 4];
            triangles = new int[maxSegments * 6];
            for (int i = 0; i < maxSegments; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                uvs[v] = new Vector2(0f, 0f); uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(0f, 1f); uvs[v + 3] = new Vector2(1f, 1f);
            }

            mesh = new Mesh { name = "SkidMarks" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors32 = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            GetComponent<MeshFilter>().sharedMesh = mesh;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Add (or continue) a mark for the given wheel. Intensity 0 ends the current mark.</summary>
        public void AddMark(int wheelId, Vector3 point, Vector3 normal, Vector3 forward, float intensity)
        {
            if (intensity <= 0.02f)
            {
                lastAnchor.Remove(wheelId);
                return;
            }

            Vector3 side = Vector3.Cross(normal, forward).normalized * (markWidth * 0.5f);
            Vector3 pos = point + normal * groundOffset;
            var anchor = new Anchor
            {
                position = pos,
                left = pos - side,
                right = pos + side,
                alpha = (byte)(Mathf.Clamp01(intensity) * 255f)
            };

            if (!lastAnchor.TryGetValue(wheelId, out var previous))
            {
                lastAnchor[wheelId] = anchor;
                return;
            }
            if ((pos - previous.position).sqrMagnitude < minSegmentLength * minSegmentLength) return;
            if ((pos - previous.position).sqrMagnitude > 4f)
            {
                lastAnchor[wheelId] = anchor; // teleport / reset: start a new mark
                return;
            }

            int v = next * 4;
            vertices[v] = previous.left;
            vertices[v + 1] = previous.right;
            vertices[v + 2] = anchor.left;
            vertices[v + 3] = anchor.right;
            for (int k = 0; k < 4; k++) normals[v + k] = normal;
            colors[v] = colors[v + 1] = new Color32(255, 255, 255, previous.alpha);
            colors[v + 2] = colors[v + 3] = new Color32(255, 255, 255, anchor.alpha);

            next = (next + 1) % maxSegments;
            lastAnchor[wheelId] = anchor;
            dirty = true;
        }

        private void LateUpdate()
        {
            if (!dirty) return;
            dirty = false;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors32 = colors;
        }
    }
}
