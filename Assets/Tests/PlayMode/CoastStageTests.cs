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
    /// <summary>
    /// QA-16: the coastal tarmac stage: grippy asphalt, a higher top speed for the player's car, guard rails,
    /// street lights, curve warning signs, the sea below the road and a clear sunset (no rain).
    /// </summary>
    public class CoastStageTests
    {
        [UnityTest]
        public IEnumerator QA16_CoastStage_FastTarmacWithRailsLightsAndSigns()
        {
            yield return SceneManager.LoadSceneAsync("Stage04", LoadSceneMode.Single);
            float t = 0f;
            while ((RaceManager.Instance == null || RaceManager.Instance.Player == null) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            var race = RaceManager.Instance;
            Assert.IsNotNull(race?.Player, "Stage04 did not initialise a player.");

            Assert.AreEqual(StageDefinition.CoastTheme, race.Stage.theme, "Stage04 should be the coast theme.");
            Assert.AreEqual("COSTERA DE ASFALTO", race.Stage.stageName);
            var table = race.Stage.surfaces;
            Assert.IsNotNull(table, "The coastal stage needs its own surface table.");
            Assert.AreSame(table, AIDriver.StageSurfaces, "The AI must plan its speeds with the coastal grip.");
            Assert.GreaterOrEqual(table.Get(SurfaceType.Asphalt).grip, 1.4f, "Fresh tarmac should grip a lot.");
            for (float d = 0f; d < race.Path.StageLength; d += 20f)
                Assert.AreEqual(SurfaceType.Asphalt, race.Path.SurfaceAt(d), $"The whole stage is tarmac (not at {d:0} m).");

            // The top speed challenge reaches the player's car when the countdown starts.
            float before = race.Player.Car.Tuning.maxSpeedKph;
            race.BeginCountdown();
            yield return null;
            float after = race.Player.Car.Tuning.maxSpeedKph;
            Assert.Greater(race.Stage.topSpeedScale, 1.05f);
            Assert.Greater(after, 200f, $"Top speed limiter should be raised on this stage (was {before:0}, now {after:0} km/h).");

            int signs = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(x => x.name == "CurveSign");
            int lights = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(x => x.name == "StreetLight");
            var rails = GameObject.Find("GuardRails");
            Debug.Log($"[QA16] {signs} curve signs, {lights} street lights, limiter {before:0} -> {after:0} km/h");
            Assert.GreaterOrEqual(signs, 6, "Curve warning signs before the corners.");
            Assert.GreaterOrEqual(lights, 30, "Street lights along the road.");
            Assert.IsNotNull(rails != null ? rails.transform.Find("Rail") : null, "Guard rails along the coast.");

            var sea = GameObject.Find("Sea");
            Assert.IsNotNull(sea, "No sea.");
            float lowestRoad = Enumerable.Range(0, race.Path.Count).Min(i => race.Path.GetPoint(i).y);
            Assert.Less(sea.transform.position.y, lowestRoad - 5f, "The sea must stay below the road.");

            var weather = Object.FindFirstObjectByType<Rally.VFX.WeatherEffects>();
            var drizzle = weather.transform.Find("Drizzle")?.GetComponent<ParticleSystem>();
            Assert.AreEqual(0f, drizzle.emission.rateOverTime.constant, "Clear sunset: no rain.");

            var notes = Pacenotes.Generate(race.Path, race.Stage);
            Assert.AreEqual(0, notes.Count(n => n.jump), "Smooth tarmac: no jumps.");
            Assert.GreaterOrEqual(notes.Count(n => !n.jump), 6, "Corner calls expected (bends wider than ~220 m are called as straights).");
        }
    }
}
