using Rally.Systems;
using UnityEngine;

namespace Rally.Track
{
    /// <summary>
    /// Coarse grid of the dominant driving surface painted on the terrain, so off-road wheels
    /// react to verges, mud patches and grass without reading splat maps at runtime.
    /// </summary>
    [RequireComponent(typeof(Terrain))]
    public class TerrainSurfaceMap : MonoBehaviour
    {
        [SerializeField] private int resolution;
        [SerializeField] private Vector3 origin;
        [SerializeField] private float size;
        [SerializeField] private byte[] cells = new byte[0];

        public static TerrainSurfaceMap Instance { get; private set; }

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        public void SetData(int gridResolution, Vector3 worldOrigin, float worldSize, byte[] data)
        {
            resolution = gridResolution;
            origin = worldOrigin;
            size = worldSize;
            cells = data;
        }

        public SurfaceType Sample(Vector3 worldPosition, SurfaceType fallback)
        {
            if (cells == null || cells.Length == 0) return fallback;
            int x = Mathf.FloorToInt((worldPosition.x - origin.x) / size * resolution);
            int z = Mathf.FloorToInt((worldPosition.z - origin.z) / size * resolution);
            if (x < 0 || z < 0 || x >= resolution || z >= resolution) return fallback;
            return (SurfaceType)cells[z * resolution + x];
        }
    }
}
