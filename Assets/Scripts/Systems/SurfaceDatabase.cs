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

        /// <summary>
        /// Desert stage table. Dirt = packed-sand piste, Gravel = rocky hamada, Mud = soft sand drifts,
        /// Asphalt = old tarmac with blown sand, Grass = loose sand off the road (much less traction and heavy drag,
        /// so leaving the piste costs time). Thick, light sand dust behind every car.
        /// </summary>
        public static SurfaceProperties[] CreateDesert()
        {
            Color sand = new Color(0.86f, 0.73f, 0.53f, 1f);
            Color paleSand = new Color(0.92f, 0.83f, 0.66f, 1f);
            Color grit = new Color(0.55f, 0.43f, 0.31f, 1f);

            var piste = SurfaceProperties.Default;
            piste.grip = 0.93f; piste.rollingResistance = 7f; piste.bumpiness = 0.3f;
            piste.dustAmount = 1.7f; piste.dustColor = sand; piste.dustSize = 1.5f;
            piste.debrisAmount = 0.4f; piste.debrisColor = grit;
            piste.rollingNoise = 0.55f; piste.skidNoise = 0.5f; piste.skidMarkOpacity = 0.45f;

            var hamada = SurfaceProperties.Default;
            hamada.type = SurfaceType.Gravel;
            hamada.grip = 0.86f; hamada.rollingResistance = 8f; hamada.bumpiness = 0.55f;
            hamada.dustAmount = 1.4f; hamada.dustColor = new Color(0.78f, 0.65f, 0.49f, 1f); hamada.dustSize = 1.3f;
            hamada.debrisAmount = 1.2f; hamada.debrisColor = new Color(0.48f, 0.36f, 0.26f, 1f);
            hamada.rollingNoise = 0.85f; hamada.skidNoise = 0.7f; hamada.skidMarkOpacity = 0.4f;

            var softSand = SurfaceProperties.Default;
            softSand.type = SurfaceType.Mud;
            softSand.grip = 0.68f; softSand.rollingResistance = 22f; softSand.bumpiness = 0.2f;
            softSand.dustAmount = 1.9f; softSand.dustColor = paleSand; softSand.dustSize = 1.6f;
            softSand.debrisAmount = 0.7f; softSand.debrisColor = sand;
            softSand.rollingNoise = 0.4f; softSand.skidNoise = 0.35f; softSand.skidMarkOpacity = 0.7f;

            var tarmac = SurfaceProperties.Default;
            tarmac.type = SurfaceType.Asphalt;
            tarmac.grip = 1.2f; tarmac.rollingResistance = 2.8f; tarmac.bumpiness = 0.12f;
            tarmac.dustAmount = 0.45f; tarmac.dustColor = sand; tarmac.dustSize = 1f;
            tarmac.debrisAmount = 0f;
            tarmac.rollingNoise = 0.25f; tarmac.skidNoise = 1f; tarmac.skidMarkOpacity = 0.6f;

            var looseSand = SurfaceProperties.Default;
            looseSand.type = SurfaceType.Grass;
            looseSand.grip = 0.55f; looseSand.rollingResistance = 30f; looseSand.bumpiness = 0.35f;
            looseSand.dustAmount = 2f; looseSand.dustColor = paleSand; looseSand.dustSize = 1.7f;
            looseSand.debrisAmount = 0.7f; looseSand.debrisColor = sand;
            looseSand.rollingNoise = 0.4f; looseSand.skidNoise = 0.3f; looseSand.skidMarkOpacity = 0.6f;

            return new[] { piste, hamada, softSand, tarmac, looseSand };
        }

        /// <summary>
        /// Coastal tarmac table: fresh, grippy asphalt (more than stage 1's village tarmac), a gravel run-off,
        /// dusty verges and dry Mediterranean scrub off the road. Almost no dust on the tarmac.
        /// </summary>
        public static SurfaceProperties[] CreateCoast()
        {
            var verge = SurfaceProperties.Default;
            verge.grip = 0.9f; verge.rollingResistance = 7f; verge.bumpiness = 0.35f;
            verge.dustAmount = 0.8f; verge.dustColor = new Color(0.66f, 0.58f, 0.46f, 1f); verge.dustSize = 1f;

            var runOff = SurfaceProperties.Default;
            runOff.type = SurfaceType.Gravel;
            runOff.grip = 0.84f; runOff.rollingResistance = 9f; runOff.bumpiness = 0.5f;
            runOff.dustAmount = 1.2f; runOff.dustColor = new Color(0.7f, 0.68f, 0.63f, 1f); runOff.dustSize = 1.1f;
            runOff.debrisAmount = 1.1f; runOff.debrisColor = new Color(0.55f, 0.53f, 0.5f, 1f);
            runOff.rollingNoise = 0.85f; runOff.skidNoise = 0.7f; runOff.skidMarkOpacity = 0.4f;

            var dryEarth = SurfaceProperties.Default;
            dryEarth.type = SurfaceType.Mud;
            dryEarth.grip = 0.78f; dryEarth.rollingResistance = 14f; dryEarth.bumpiness = 0.4f;
            dryEarth.dustAmount = 0.9f; dryEarth.dustColor = new Color(0.6f, 0.5f, 0.38f, 1f); dryEarth.dustSize = 1f;

            var tarmac = SurfaceProperties.Default;
            tarmac.type = SurfaceType.Asphalt;
            tarmac.grip = 1.45f; tarmac.rollingResistance = 2.3f; tarmac.bumpiness = 0.05f;
            tarmac.dustAmount = 0.03f; tarmac.dustColor = new Color(0.6f, 0.6f, 0.6f, 1f); tarmac.dustSize = 0.6f;
            tarmac.debrisAmount = 0f;
            tarmac.rollingNoise = 0.2f; tarmac.skidNoise = 1f; tarmac.skidMarkOpacity = 0.75f;

            var scrub = SurfaceProperties.Default;
            scrub.type = SurfaceType.Grass;
            scrub.grip = 0.7f; scrub.rollingResistance = 18f; scrub.bumpiness = 0.7f;
            scrub.dustAmount = 0.6f; scrub.dustColor = new Color(0.58f, 0.52f, 0.38f, 1f); scrub.dustSize = 0.9f;
            scrub.debrisAmount = 0.6f; scrub.debrisColor = new Color(0.35f, 0.33f, 0.2f, 1f);
            scrub.rollingNoise = 0.45f; scrub.skidNoise = 0.3f; scrub.skidMarkOpacity = 0.35f;

            return new[] { verge, runOff, dryEarth, tarmac, scrub };
        }
    }
}
