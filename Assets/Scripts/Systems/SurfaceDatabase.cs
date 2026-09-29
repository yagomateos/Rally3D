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

        /// <summary>
        /// Snow stage table. Same surface slots, winter meaning: Dirt = packed snow, Gravel = snow over gravel,
        /// Mud = slush, Asphalt = ice, Grass = deep snow off the road. Grip is lower everywhere (arcade values:
        /// studded rally tyres still bite on ice), dust is a white snow spray.
        /// </summary>
        public static SurfaceProperties[] CreateSnow()
        {
            Color spray = new Color(0.93f, 0.95f, 0.98f, 1f);
            Color flakes = new Color(0.86f, 0.89f, 0.93f, 1f);

            var packed = SurfaceProperties.Default;
            packed.grip = 0.66f; packed.rollingResistance = 9f; packed.bumpiness = 0.3f;
            packed.dustAmount = 1.3f; packed.dustColor = spray; packed.dustSize = 1.2f;
            packed.debrisAmount = 0.6f; packed.debrisColor = flakes;
            packed.rollingNoise = 0.45f; packed.skidNoise = 0.5f; packed.skidMarkOpacity = 0.35f;

            var snowGravel = SurfaceProperties.Default;
            snowGravel.type = SurfaceType.Gravel;
            snowGravel.grip = 0.75f; snowGravel.rollingResistance = 9f; snowGravel.bumpiness = 0.45f;
            snowGravel.dustAmount = 1.1f; snowGravel.dustColor = spray; snowGravel.dustSize = 1.2f;
            snowGravel.debrisAmount = 0.9f; snowGravel.debrisColor = new Color(0.5f, 0.5f, 0.52f, 1f);
            snowGravel.rollingNoise = 0.7f; snowGravel.skidNoise = 0.6f; snowGravel.skidMarkOpacity = 0.4f;

            var slush = SurfaceProperties.Default;
            slush.type = SurfaceType.Mud;
            slush.grip = 0.55f; slush.rollingResistance = 26f; slush.bumpiness = 0.25f;
            slush.dustAmount = 0.4f; slush.dustColor = new Color(0.75f, 0.77f, 0.8f, 1f); slush.dustSize = 0.7f;
            slush.debrisAmount = 1.2f; slush.debrisColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            slush.rollingNoise = 0.5f; slush.skidNoise = 0.35f; slush.skidMarkOpacity = 0.7f;

            var ice = SurfaceProperties.Default;
            ice.type = SurfaceType.Asphalt;
            ice.grip = 0.45f; ice.rollingResistance = 2f; ice.bumpiness = 0.05f;
            ice.dustAmount = 0.05f; ice.dustColor = spray; ice.dustSize = 0.6f;
            ice.debrisAmount = 0f;
            ice.rollingNoise = 0.3f; ice.skidNoise = 0.15f; ice.skidMarkOpacity = 0.1f;

            var deepSnow = SurfaceProperties.Default;
            deepSnow.type = SurfaceType.Grass;
            deepSnow.grip = 0.5f; deepSnow.rollingResistance = 45f; deepSnow.bumpiness = 0.3f;
            deepSnow.dustAmount = 1.8f; deepSnow.dustColor = spray; deepSnow.dustSize = 1.4f;
            deepSnow.debrisAmount = 0.8f; deepSnow.debrisColor = flakes;
            deepSnow.rollingNoise = 0.4f; deepSnow.skidNoise = 0.3f; deepSnow.skidMarkOpacity = 0.5f;

            return new[] { packed, snowGravel, slush, ice, deepSnow };
        }
    }
}
