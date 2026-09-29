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
    /// <summary>QA-12: pushing a rival from behind at low speed moves it (no braking against the push, no launch).</summary>
    public class PushTests
    {
        [UnityTest]
        public IEnumerator QA12_LowSpeedPushFromBehind_MovesRivalSmoothly()
        {
            yield return LoadStage();
            yield return StartRace();
            var rival = Race.Participants.First(p => !p.IsPlayer);
            var ai = rival.GetComponent<AIDriver>();
            ai.SpeedCapKph = 5f; // the rival crawls at 5 km/h

            float d = Player.Distance + 150f;
            PlaceOnRoad(rival, Race.Path, d);
            PlaceOnRoad(Player, Race.Path, d - 6.5f);
            yield return new WaitForFixedUpdate();

            var input = Player.GetComponent<PlayerCarInput>();
            input.AcelerarTactil(true); // roll into it and keep pushing
            float maxRivalKph = 0f, t = 0f;
            while (t < 4f)
            {
                t += Time.deltaTime;
                maxRivalKph = Mathf.Max(maxRivalKph, rival.Car.SpeedKph);
                yield return null;
            }
            input.AcelerarTactil(false);
            ai.SpeedCapKph = float.MaxValue;

            Assert.Greater(maxRivalKph, 12f, "Pushed from behind, the rival should be carried well above its own 5 km/h.");
            Assert.Less(maxRivalKph, Player.Car.SpeedKph + 25f, "The push must not launch the rival.");
            Assert.Less(Vector3.Angle(Vector3.up, rival.transform.up), 35f, "Cars must not climb onto each other.");
        }
    }
}
