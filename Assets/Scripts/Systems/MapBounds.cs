using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// Invisible walls round the edge of the stage's terrain, like most games, so a car driven far off the road can
    /// no longer fall off the end of the world. On the coastal stage the sea covers the low edge of the map, so a
    /// car that drives into the sea is also put back on the road. Created at load by <see cref="RaceManager"/>.
    /// </summary>
    public class MapBounds : MonoBehaviour
    {
        [Tooltip("How far inside the terrain edge the walls stand.")]
        [SerializeField] private float inset = 3f;
        [SerializeField] private float wallHeight = 600f;
        [SerializeField] private float wallThickness = 10f;
        [Tooltip("A car this far below the sea surface is put back on the road.")]
        [SerializeField] private float sinkDepth = 0.6f;

        private RaceManager race;
        private float seaLevel = float.NegativeInfinity;

        public Bounds Area { get; private set; }

        public static MapBounds Create(RaceManager race)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return null;
            var bounds = new GameObject("MapBounds").AddComponent<MapBounds>();
            bounds.race = race;
            bounds.Build(terrain);
            return bounds;
        }

        private void Build(Terrain terrain)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float minX = origin.x + inset, maxX = origin.x + size.x - inset;
            float minZ = origin.z + inset, maxZ = origin.z + size.z - inset;
            float midY = origin.y + wallHeight * 0.5f - 100f;
            Area = new Bounds(new Vector3((minX + maxX) * 0.5f, midY, (minZ + maxZ) * 0.5f), new Vector3(maxX - minX, wallHeight, maxZ - minZ));

            float t = wallThickness;
            Wall("West", new Vector3(minX - t * 0.5f, midY, Area.center.z), new Vector3(t, wallHeight, size.z + t * 2f));
            Wall("East", new Vector3(maxX + t * 0.5f, midY, Area.center.z), new Vector3(t, wallHeight, size.z + t * 2f));
            Wall("South", new Vector3(Area.center.x, midY, minZ - t * 0.5f), new Vector3(size.x + t * 2f, wallHeight, t));
            Wall("North", new Vector3(Area.center.x, midY, maxZ + t * 0.5f), new Vector3(size.x + t * 2f, wallHeight, t));

            var sea = GameObject.Find("Sea");
            if (sea != null) seaLevel = sea.transform.position.y;
        }

        private void Wall(string wallName, Vector3 center, Vector3 size)
        {
            var go = new GameObject(wallName) { layer = 2 }; // Ignore Raycast: the chase camera looks through it
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        private void Update()
        {
            if (race == null || float.IsNegativeInfinity(seaLevel)) return;
            foreach (var p in race.Participants)
                if (p != null && p.Car != null && p.Car.Body != null && p.Car.Body.position.y < seaLevel - sinkDepth)
                    p.ResetToTrack();
        }
    }
}
