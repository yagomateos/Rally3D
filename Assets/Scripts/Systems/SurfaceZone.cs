using System.Collections.Generic;
using UnityEngine;

namespace Rally.Systems
{
    /// <summary>Tags a collider with the surface type the car drives on.</summary>
    public class SurfaceZone : MonoBehaviour
    {
        [SerializeField] private SurfaceType surface = SurfaceType.Dirt;

        public SurfaceType Surface
        {
            get => surface;
            set => surface = value;
        }

        private static readonly Dictionary<int, SurfaceType> cache = new Dictionary<int, SurfaceType>();

        /// <summary>Resolve a collider's surface with caching (GetComponent is avoided per frame).</summary>
        public static SurfaceType Resolve(Collider collider, SurfaceType fallback)
        {
            if (collider == null) return fallback;
            int id = collider.GetInstanceID();
            if (cache.TryGetValue(id, out var type)) return type;

            var zone = collider.GetComponentInParent<SurfaceZone>();
            type = zone != null ? zone.surface : fallback;
            cache[id] = type;
            return type;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cache.Clear();
    }
}
