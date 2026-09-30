using System.Collections;
using NUnit.Framework;
using Rally.Car;
using Rally.Systems;
using UnityEngine;
using UnityEngine.TestTools;
using static Rally.Tests.StageTestUtility;

namespace Rally.Tests
{
    /// <summary>QA-06 and QA-07: reset penalty and collision damage.</summary>
    public class DamageAndPenaltyTests
    {
        private CarDamage.Mode previousSetting;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousSetting = CarDamage.Setting;
            CarDamage.Setting = CarDamage.Mode.Completos;
            yield return LoadStage();
            yield return StartRace();
        }

        [TearDown]
        public void TearDown() => CarDamage.Setting = previousSetting;

        /// <summary>QA-06: a reset asked for by the player costs 5 seconds, shown in the clock and the finish time.</summary>
        [UnityTest]
        public IEnumerator QA06_PlayerReset_AddsFiveSecondPenalty()
        {
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, Player.Penalty, "No penalty before any reset.");

            Race.PlayerReset();
            yield return null;

            Assert.AreEqual(RaceManager.ResetPenalty, Player.Penalty, 1e-4f, "Reset did not add the penalty.");
            Assert.AreEqual(Race.StageTime + RaceManager.ResetPenalty, Race.PlayerTime, 1e-3f, "Clock does not include the penalty.");
        }

        /// <summary>QA-07: a hard frontal hit dents the car, raises the damage and costs engine power.</summary>
        [UnityTest]
        public IEnumerator QA07_HardFrontalHit_DamagesCarAndReducesPower()
        {
            var damage = Player.GetComponent<CarDamage>();
            Assert.IsNotNull(damage, "The race manager should add CarDamage to every car.");
            Assert.AreEqual(0f, damage.Damage01, "Car should start intact.");

            // A heavy wall 6 m ahead on the road; drive the car into it at ~72 km/h.
            PlaceOnRoad(Player, Race.Path, Player.Distance + 40f);
            yield return new WaitForFixedUpdate();
            var car = Player.Car;
            Vector3 forward = car.transform.forward;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(8f, 4f, 1f);
            wall.transform.position = car.transform.position + forward * 6f + Vector3.up * 0.5f;
            wall.transform.rotation = Quaternion.LookRotation(forward);

            var mesh = car.transform.Find("Body").GetComponent<MeshFilter>();
            Vector3[] before = mesh.sharedMesh.vertices;

            car.Body.linearVelocity = forward * 20f;
            float t = 0f;
            while (damage.Damage01 <= 0f && t < 2f) { t += Time.deltaTime; yield return new WaitForFixedUpdate(); }
            Object.Destroy(wall);

            Assert.Greater(damage.Damage01, 0.05f, "A 72 km/h crash should cause visible damage.");
            Assert.Less(car.DamagePowerScale, 1f, "Front damage should cost engine power.");

            Vector3[] after = mesh.sharedMesh.vertices;
            int moved = 0;
            float alongForward = 0f, deepest = 0f;
            for (int i = 0; i < after.Length; i++)
            {
                Vector3 delta = after[i] - before[i];
                if (delta.sqrMagnitude < 1e-6f) continue;
                moved++;
                alongForward += delta.z; // body local +Z is the car's front
                deepest = Mathf.Max(deepest, delta.magnitude);
            }
            Assert.Greater(moved, 0, "The body mesh should be dented.");
            Assert.Greater(deepest, 0.05f, "The dent must be deep enough to see (at least 5 cm).");
            Assert.Less(alongForward / moved, 0f, "A frontal hit must push the bodywork back (inwards), not out.");
        }
    }
}
