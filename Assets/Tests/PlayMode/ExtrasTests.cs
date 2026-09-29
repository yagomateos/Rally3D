using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rally.Systems;
using Rally.Track;
using Rally.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using static Rally.Tests.StageTestUtility;

namespace Rally.Tests
{
    /// <summary>Menu navigation with the stick, map edge walls, knockable roadside props and the win celebration.</summary>
    public class ExtrasTests
    {
        /// <summary>QA-19: the left stick moves through the pause menu and up / down wrap round the list.</summary>
        [UnityTest]
        public IEnumerator QA19_LeftStick_NavigatesMenuAndWrapsRound()
        {
            yield return LoadStage();
            yield return StartRace();
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Race.SetPaused(true);
                yield return null;
                var first = EventSystem.current.currentSelectedGameObject;
                Assert.IsNotNull(first, "The pause menu should select its first button.");
                Assert.AreEqual("Resume", first.name);

                yield return Push(pad, new Vector2(0f, 1f)); // up from the top wraps to the bottom
                var wrapped = EventSystem.current.currentSelectedGameObject;
                Assert.AreNotEqual(first, wrapped, "Stick up should move the selection.");
                Assert.Less(wrapped.transform.position.y, first.transform.position.y, "Up from the first button should wrap to the last.");

                yield return Push(pad, new Vector2(0f, -1f)); // and down from the bottom wraps back to the top
                Assert.AreEqual(first, EventSystem.current.currentSelectedGameObject, "Down from the last button should wrap to the first.");

                yield return Push(pad, new Vector2(0f, -1f));
                var second = EventSystem.current.currentSelectedGameObject;
                Assert.AreEqual("Restart", second.name, "Stick down should go to the next button.");
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
                Race.SetPaused(false);
            }
        }

        private static IEnumerator Push(Gamepad pad, Vector2 stick)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = stick });
            for (int i = 0; i < 3; i++) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>QA-20: driving off the edge of the map, the car hits an invisible wall instead of falling off.</summary>
        [UnityTest]
        public IEnumerator QA20_MapEdge_HasInvisibleWalls()
        {
            yield return LoadStage();
            yield return StartRace();
            var bounds = Object.FindFirstObjectByType<MapBounds>();
            Assert.IsNotNull(bounds, "Every stage needs walls at the edge of the map.");
            var terrain = Terrain.activeTerrain;
            Vector3 edgeEast = terrain.transform.position + new Vector3(terrain.terrainData.size.x - 25f, 0f, terrain.terrainData.size.z * 0.5f);
            edgeEast.y = GroundHeight(edgeEast) + 1f;
            Place(Player, edgeEast, Quaternion.LookRotation(Vector3.right));
            Player.Car.Body.linearVelocity = Vector3.right * 35f;
            yield return new WaitForSeconds(2f);
            float maxX = terrain.transform.position.x + terrain.terrainData.size.x;
            Assert.Less(Player.Car.Body.position.x, maxX, "The car went past the edge of the map.");
            Assert.Greater(Player.Car.Body.position.y, terrain.transform.position.y - 5f, "The car fell off the world.");
        }

        /// <summary>QA-21: roadside marker poles are knocked flying and barely slow the car; scrubs and people are solid.</summary>
        [UnityTest]
        public IEnumerator QA21_RoadsideProps_CollideOrGetKnockedFlying()
        {
            yield return LoadStage();
            yield return StartRace();
            Assert.IsTrue(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Spectator")).All(t => t.GetComponent<Collider>() != null), "Spectators need colliders.");

            var pole = Object.FindObjectsByType<Knockable>(FindObjectsSortMode.None).FirstOrDefault(k => k.name.StartsWith("MarkerPole"));
            Assert.IsNotNull(pole, "Marker poles should be knockable.");
            Vector3 at = pole.transform.position;
            Vector3 dir = Vector3.ProjectOnPlane(Race.Path.TangentAt(Race.Path.Project(at, Race.Path.ProjectGlobal(at))), Vector3.up).normalized;
            Vector3 start = at - dir * 12f;
            start.y = GroundHeight(start) + 0.6f;
            Place(Player, start, Quaternion.LookRotation(dir));
            Player.Car.Body.linearVelocity = dir * 18f;
            float t = 0f;
            while (!pole.Knocked && t < 2f) { t += Time.deltaTime; yield return new WaitForFixedUpdate(); }
            Assert.IsTrue(pole.Knocked, "Driving into the pole should knock it.");
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(Player.Car.Body.linearVelocity.magnitude, 12f, "A marker pole must not stop the car like a wall.");
            Assert.Greater(pole.GetComponent<Rigidbody>().linearVelocity.magnitude, 3f, "The pole should fly off.");
        }

        /// <summary>QA-22: the win celebration throws confetti and plays the victory music.</summary>
        [UnityTest]
        public IEnumerator QA22_Celebration_ConfettiAndMusic()
        {
            var canvasGo = new GameObject("TestCanvas", typeof(Canvas));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var celebration = Celebration.Play(canvasGo.transform);
            yield return new WaitForSecondsRealtime(0.5f);
            int confetti = celebration.GetComponentsInChildren<Transform>().Count(c => c.name == "Confetti");
            Assert.Greater(confetti, 150, "Plenty of confetti.");
            Assert.IsTrue(celebration.GetComponent<AudioSource>().isPlaying, "Victory music should be playing.");
            Object.Destroy(canvasGo);
        }
    }
}
