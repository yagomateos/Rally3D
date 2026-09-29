using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rally.Systems;
using UnityEngine;
using UnityEngine.TestTools;
using static Rally.Tests.StageTestUtility;

namespace Rally.Tests
{
    /// <summary>QA-01, QA-02 and QA-03 from the QA report: resetting and recovering the car.</summary>
    public class ResetTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadStage();
            yield return StartRace();
        }

        /// <summary>QA-01: cutting across country and pressing R must not move the car forward.</summary>
        [UnityTest]
        public IEnumerator QA01_ResetAfterCuttingOffRoad_ReturnsToWhereTheCarLeftTheRoad()
        {
            var path = Race.Path;
            float leftRoadAt = Player.Distance;

            // Drive 200 m in 10 m steps, 30 m to the right of the road (well outside the verge).
            for (float d = leftRoadAt + 10f; d <= leftRoadAt + 200f; d += 10f)
            {
                Vector3 p = path.PositionAt(d) + path.RightAt(d) * 30f;
                p.y = GroundHeight(p) + 1.5f;
                Place(Player, p, Quaternion.LookRotation(path.TangentAt(d)));
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            Assert.Greater(Player.Distance, leftRoadAt + 150f, "Test setup: progress should follow the car.");

            Assert.IsTrue(Player.ResetToTrack(), "Reset was refused.");
            Assert.Less(Player.Distance, leftRoadAt + 20f,
                "Reset placed the car ahead of the point where it left the road (shortcut exploit).");
        }

        /// <summary>QA-02: the reset spot must not overlap another car.</summary>
        [UnityTest]
        public IEnumerator QA02_ResetOntoAnotherCar_PicksAFreeSpot()
        {
            var path = Race.Path;
            var rival = Race.Participants.First(p => !p.IsPlayer);
            rival.Car.ControlEnabled = false;

            // Player alone on a straight bit of road, rival parked exactly where the reset would go.
            float d = path.StartDistance + 60f;
            PlaceOnRoad(Player, path, d);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            yield return null;

            PlaceOnRoad(rival, path, Player.Distance - 4f);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(1.3f); // reset cooldown

            Assert.IsTrue(Player.ResetToTrack(), "Reset was refused.");
            Physics.SyncTransforms();

            float gap = Vector3.Distance(Player.transform.position, rival.transform.position);
            Assert.IsFalse(BodiesOverlap(Player, rival), $"Player was reset inside a parked car ({gap:0.0} m apart).");
        }

        /// <summary>True if any body collider (not wheels) of one car penetrates the other.</summary>
        private static bool BodiesOverlap(RaceParticipant a, RaceParticipant b)
        {
            foreach (var ca in a.GetComponents<BoxCollider>())
            foreach (var cb in b.GetComponents<BoxCollider>())
            {
                if (Physics.ComputePenetration(ca, ca.transform.position, ca.transform.rotation,
                        cb, cb.transform.position, cb.transform.rotation, out _, out float depth) && depth > 0.01f)
                    return true;
            }
            return false;
        }

        /// <summary>QA-03: a car left on its roof must recover without player input.</summary>
        [UnityTest]
        public IEnumerator QA03_CarOnItsRoof_RecoversAutomatically()
        {
            var path = Race.Path;
            float d = path.StartDistance + 80f;
            Vector3 p = path.PositionAt(d) + Vector3.up * 1.4f;
            Vector3 t = path.TangentAt(d);
            Place(Player, p, Quaternion.LookRotation(new Vector3(t.x, 0f, t.z), Vector3.down));

            yield return new WaitForSeconds(6f);

            float upright = Vector3.Dot(Player.Car.transform.up, Vector3.up);
            Assert.Greater(upright, 0.7f, $"Car is still upside down after 6 s (up·Y = {upright:0.00}).");
        }
    }
}
