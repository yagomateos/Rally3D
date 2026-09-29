using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rally.Track;
using UnityEngine.TestTools;
using static Rally.Tests.StageTestUtility;

namespace Rally.Tests
{
    /// <summary>QA-08: pace notes generated from Stage01's road.</summary>
    public class PacenoteTests
    {
        [UnityTest]
        public IEnumerator QA08_Pacenotes_CallCornersBothWaysAndTheThreeJumps()
        {
            yield return LoadStage();
            var notes = Pacenotes.Generate(Race.Path, Race.Stage);

            var corners = notes.Where(n => !n.jump).ToList();
            Assert.GreaterOrEqual(corners.Count, 8, "A 2.9 km rally stage should have a good number of corner calls.");
            Assert.IsTrue(corners.Any(n => n.direction < 0) && corners.Any(n => n.direction > 0), "Corners both ways expected.");
            Assert.IsTrue(corners.All(n => n.severity >= 0 && n.severity <= 6), "Grades must be 0 (hairpin) to 6.");
            Assert.AreEqual(3, notes.Count(n => n.jump), "Stage01 has three jumps: " + string.Join(", ", notes.Where(n => n.jump).Select(n => n.distance.ToString("0"))));
            for (int i = 1; i < notes.Count; i++)
                Assert.LessOrEqual(notes[i - 1].distance, notes[i].distance, "Notes must be in stage order.");
            UnityEngine.Debug.Log("[Pacenotes] " + string.Join(" | ", notes.Select(n => $"{n.distance:0}m {n.Text} {n.Modifiers}")));
        }

        /// <summary>QA-09: approaching the first corner, the co-driver calls it on screen.</summary>
        [UnityTest]
        public IEnumerator QA09_CoDriver_CallsTheNextCornerOnScreen()
        {
            yield return LoadStage();
            yield return StartRace();
            var coDriver = UnityEngine.Object.FindFirstObjectByType<Rally.UI.CoDriver>();
            Assert.IsNotNull(coDriver, "The HUD should add the co-driver.");
            Assert.Greater(coDriver.NoteCount, 0, "The co-driver has no notes.");

            var first = Pacenotes.Generate(Race.Path, Race.Stage).First(n => n.distance > Player.Distance + 60f);
            PlaceOnRoad(Player, Race.Path, first.distance - 30f);
            float t = 0f;
            while (coDriver.PanelAlpha < 0.5f && t < 2f) { t += UnityEngine.Time.deltaTime; yield return null; }

            Assert.Greater(coDriver.PanelAlpha, 0.5f, "The call was not shown.");
            StringAssert.StartsWith(first.Text, coDriver.LastCall, "Wrong call for the next note.");
        }
    }
}
