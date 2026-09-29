using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rally.AI;
using Rally.Car;
using UnityEngine;
using UnityEngine.TestTools;
using static Rally.Tests.StageTestUtility;

namespace Rally.Tests
{
    /// <summary>QA-11: when the player rams a rival, the rival is pushed and loses control for a moment.</summary>
    public class ContactTests
    {
        [UnityTest]
        public IEnumerator QA11_PlayerHitsRival_RivalIsPushedAndStunned()
        {
            yield return LoadStage();
            yield return StartRace();
            var rival = Race.Participants.First(p => !p.IsPlayer);
            Assert.IsNotNull(Player.GetComponent<PlayerContactPush>(), "The race manager should add PlayerContactPush to the player.");

            // Rival stopped on the road, player 10 m behind it in the same lane, running into it at ~65 km/h.
            float d = Player.Distance + 120f;
            PlaceOnRoad(rival, Race.Path, d);
            PlaceOnRoad(Player, Race.Path, d - 10f);
            yield return new WaitForFixedUpdate();
            Vector3 rivalStart = rival.transform.position;
            Player.Car.Body.linearVelocity = Player.transform.forward * 18f;

            float t = 0f;
            while (rival.Car.StabilityScale >= 1f && t < 2f) { t += Time.deltaTime; yield return new WaitForFixedUpdate(); }

            Assert.Less(rival.Car.StabilityScale, 1f, "The rival should be stunned (weak stability assist) after the hit.");
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(Vector3.Distance(rivalStart, rival.transform.position), 2f, "The rival should be pushed by the hit.");
        }
    }
}
