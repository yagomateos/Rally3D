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
    /// <summary>QA-10: the snow stage loads with its own grip table for the cars and the AI.</summary>
    public class SnowStageTests
    {
        [UnityTest]
        public IEnumerator QA10_SnowStage_UsesSnowGripForCarsAndAI()
        {
            yield return SceneManager.LoadSceneAsync("Stage02", LoadSceneMode.Single);
            float t = 0f;
            while ((RaceManager.Instance == null || RaceManager.Instance.Player == null) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            var race = RaceManager.Instance;
            Assert.IsNotNull(race?.Player, "Stage02 did not initialise a player.");

            Assert.AreEqual(1, race.Stage.theme, "Stage02 should be the snow theme.");
            Assert.IsNotNull(race.Stage.surfaces, "The snow stage needs its own surface table.");
            foreach (var p in race.Participants)
                Assert.AreSame(race.Stage.surfaces, p.GetComponent<CarController>().Surfaces, $"{p.DisplayName} is not on snow grip.");
            Assert.AreSame(race.Stage.surfaces, AIDriver.StageSurfaces, "The AI must plan its speeds with the snow grip.");
            Assert.Less(race.Stage.surfaces.Get(SurfaceType.Asphalt).grip, 0.6f, "Ice should be slippery.");

            var notes = Pacenotes.Generate(race.Path, race.Stage);
            Assert.AreEqual(3, notes.Count(n => n.jump), "Stage02 has three jumps.");
            Assert.GreaterOrEqual(notes.Count(n => !n.jump), 8, "Corner calls expected.");
        }
    }
}
