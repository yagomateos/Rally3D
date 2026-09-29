using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rally.AI;
using Rally.Car;
using Rally.Systems;
using Rally.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rally.Tests
{
    /// <summary>QA-15: the desert stage loads with its own sand grip table, loose sand off the piste and calima.</summary>
    public class DesertStageTests
    {
        [UnityTest]
        public IEnumerator QA15_DesertStage_UsesSandGripAndLooseSandOffRoad()
        {
            yield return SceneManager.LoadSceneAsync("Stage03", LoadSceneMode.Single);
            float t = 0f;
            while ((RaceManager.Instance == null || RaceManager.Instance.Player == null) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            var race = RaceManager.Instance;
            Assert.IsNotNull(race?.Player, "Stage03 did not initialise a player.");

            Assert.AreEqual(StageDefinition.DesertTheme, race.Stage.theme, "Stage03 should be the desert theme.");
            Assert.AreEqual("DUNAS DEL DESIERTO", race.Stage.stageName);
            var sand = race.Stage.surfaces;
            Assert.IsNotNull(sand, "The desert stage needs its own surface table.");
            foreach (var p in race.Participants)
                Assert.AreSame(sand, p.GetComponent<CarController>().Surfaces, $"{p.DisplayName} is not on sand grip.");
            Assert.AreSame(sand, AIDriver.StageSurfaces, "The AI must plan its speeds with the sand grip.");

            // Off the piste the loose sand grips much less and drags more; the piste raises more dust than stage 1's dirt.
            var piste = sand.Get(SurfaceType.Dirt);
            var loose = sand.Get(SurfaceType.Grass);
            Assert.Less(loose.grip, piste.grip * 0.7f, "Loose sand off the road should have much less traction.");
            Assert.Greater(loose.rollingResistance, piste.rollingResistance * 3f, "Loose sand should drag.");
            Assert.Greater(piste.dustAmount, 1.5f, "Dense dust behind the cars on the piste.");

            var weather = Object.FindFirstObjectByType<Rally.VFX.WeatherEffects>();
            Assert.IsNotNull(weather, "No weather effects.");
            var drizzle = weather.transform.Find("Drizzle")?.GetComponent<ParticleSystem>();
            Assert.IsNotNull(drizzle);
            Assert.AreEqual(0f, drizzle.emission.rateOverTime.constant, "It should not rain in the desert.");
            Assert.IsTrue(RenderSettings.fog, "Calima needs the fog.");

            var notes = Pacenotes.Generate(race.Path, race.Stage);
            Assert.AreEqual(3, notes.Count(n => n.jump), "Stage03 has three dune-crest jumps.");
            Assert.GreaterOrEqual(notes.Count(n => !n.jump), 8, "Corner calls expected.");
        }
    }
}
