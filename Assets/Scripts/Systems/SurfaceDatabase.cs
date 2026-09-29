using System.Collections.Generic;
using UnityEngine;

namespace Rally.Systems
{
    [CreateAssetMenu(menuName = "Rally/Surface Database", fileName = "SurfaceDatabase")]
    public class SurfaceDatabase : ScriptableObject
    {
        [SerializeField] private SurfaceProperties[] surfaces = new SurfaceProperties[0];

        private Dictionary<SurfaceType, SurfaceProperties> lookup;

        public SurfaceProperties Get(SurfaceType type)
        {
            if (lookup == null) BuildLookup();
            return lookup.TryGetValue(type, out var props) ? props : SurfaceProperties.Default;
        }

        public void SetSurfaces(SurfaceProperties[] values)
        {
            surfaces = values;
            lookup = null;
        }

        private void OnValidate() => lookup = null;

        private void BuildLookup()
        {
            lookup = new Dictionary<SurfaceType, SurfaceProperties>();
            foreach (var s in surfaces) lookup[s.type] = s;
        }

        /// <summary>Sensible defaults used by the stage builder.</summary>
        public static SurfaceProperties[] CreateDefaults()
        {
            var dirt = SurfaceProperties.Default;

            var gravel = SurfaceProperties.Default;
            gravel.type = SurfaceType.Gravel;
            gravel.grip = 0.86f;
            gravel.rollingResistance = 8f;
            gravel.bumpiness = 0.5f;
            gravel.dustAmount = 1.5f;
            gravel.dustColor = new Color(0.72f, 0.67f, 0.58f, 1f);
            gravel.dustSize = 1.3f;
            gravel.debrisAmount = 1.2f;
            gravel.debrisColor = new Color(0.42f, 0.4f, 0.37f, 1f);
            gravel.rollingNoise = 0.85f;
            gravel.skidNoise = 0.75f;
            gravel.skidMarkOpacity = 0.45f;

            var mud = SurfaceProperties.Default;
            mud.type = SurfaceType.Mud;
            mud.grip = 0.62f;
            mud.rollingResistance = 30f;
            mud.bumpiness = 0.3f;
            mud.dustAmount = 0.15f;
            mud.dustColor = new Color(0.33f, 0.27f, 0.2f, 1f);
            mud.dustSize = 0.55f;
            mud.debrisAmount = 1.6f;
            mud.debrisColor = new Color(0.2f, 0.15f, 0.1f, 1f);
            mud.rollingNoise = 0.5f;
            mud.skidNoise = 0.35f;
            mud.skidMarkOpacity = 0.8f;

            var asphalt = SurfaceProperties.Default;
            asphalt.type = SurfaceType.Asphalt;
            asphalt.grip = 1.3f;
            asphalt.rollingResistance = 2.5f;
            asphalt.bumpiness = 0.08f;
            asphalt.dustAmount = 0.05f;
            asphalt.dustColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            asphalt.dustSize = 0.7f;
            asphalt.debrisAmount = 0f;
            asphalt.rollingNoise = 0.2f;
            asphalt.skidNoise = 1f;
            asphalt.skidMarkOpacity = 0.7f;

            var grass = SurfaceProperties.Default;
            grass.type = SurfaceType.Grass;
            grass.grip = 0.72f;
            grass.rollingResistance = 16f;
            grass.bumpiness = 0.7f;
            grass.dustAmount = 0.35f;
            grass.dustColor = new Color(0.45f, 0.42f, 0.32f, 1f);
            grass.dustSize = 0.8f;
            grass.debrisAmount = 0.6f;
            grass.debrisColor = new Color(0.22f, 0.26f, 0.14f, 1f);
            grass.rollingNoise = 0.45f;
            grass.skidNoise = 0.3f;
            grass.skidMarkOpacity = 0.35f;

            return new[] { dirt, gravel, mud, asphalt, grass };
        }
    }
}
