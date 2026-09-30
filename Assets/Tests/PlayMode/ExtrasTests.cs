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

        /// <summary>QA-23: on stage 1 a flock appears standing in the road ahead and stays in the way.</summary>
        [UnityTest]
        public IEnumerator QA23_SheepFlock_StandsInTheRoad()
        {
            yield return LoadStage();
            yield return StartRace();
            var crossing = Object.FindFirstObjectByType<SheepCrossing>();
            Assert.IsNotNull(crossing, "Stage 1 should have sheep too.");
            Assert.GreaterOrEqual(crossing.CrossingPoints.Count, 2);
            float at = Player.Distance + 80f;
            var flock = crossing.SpawnFlockOnRoad(at);
            Assert.GreaterOrEqual(flock.Length, 1);
            yield return new WaitForSeconds(4f);
            foreach (var sheep in flock)
            {
                float d = Race.Path.Project(sheep.transform.position, at);
                float lateral = Mathf.Abs(Race.Path.LateralOffset(sheep.transform.position, d));
                Assert.Less(lateral, Race.Path.WidthAt(d) * 0.5f, "The sheep should still be on the road, in the way.");
            }
        }

        /// <summary>QA-24: off the road a countdown runs, and at zero the car is put back on the road for free.</summary>
        [UnityTest]
        public IEnumerator QA24_OffRoad_CountdownThenBackOnTheRoad()
        {
            yield return LoadStage();
            yield return StartRace();
            float d = Player.Distance + 60f;
            Vector3 off = Race.Path.PositionAt(d) + Race.Path.RightAt(d) * (Race.Path.WidthAt(d) * 0.5f + 14f);
            off.y = GroundHeight(off) + 0.5f;
            Quaternion rot = Quaternion.LookRotation(Race.Path.TangentAt(d));
            Place(Player, off, rot);
            float penaltyBefore = Player.Penalty;
            // Hold the car where it is (it may be on a slope) until the countdown puts it back on the road.
            bool started = false;
            for (float t = 0f; t < 7f; t += Time.deltaTime)
            {
                if (Player.OffRoadTimeLeft >= 0f)
                {
                    if (!started) Assert.Greater(Player.OffRoadTimeLeft, 4f, "The countdown should start from about 5 s.");
                    started = true;
                    Player.Car.Body.position = off;
                    Player.Car.Body.rotation = rot;
                    Player.Car.Body.linearVelocity = Vector3.zero;
                }
                else if (started) break; // reset happened
                yield return null;
            }
            Assert.IsTrue(started, "A countdown should run off the road.");
            yield return new WaitForSeconds(0.2f);
            float lateral = Mathf.Abs(Player.LateralOffset);
            Assert.Less(lateral, Race.Path.WidthAt(Player.Distance) * 0.5f + 3f, "At zero the car should be back on the road.");
            Assert.AreEqual(-1f, Player.OffRoadTimeLeft, "No countdown once back on the road.");
            Assert.AreEqual(penaltyBefore, Player.Penalty, "The automatic return costs no time.");
        }

        /// <summary>QA-25: with the driving assist on ALTA and no steering at all, the car follows the road round a bend.</summary>
        [UnityTest]
        public IEnumerator QA25_DrivingAssist_FollowsTheRoad()
        {
            var before = Rally.Car.DrivingAssist.Setting;
            try
            {
                Rally.Car.DrivingAssist.Setting = Rally.Car.DrivingAssist.Level.Alta;
                yield return LoadStage();
                yield return StartRace();
                Player.Car.Body.linearVelocity = Race.Path.TangentAt(Player.Distance) * 22f;
                float worst = 0f;
                for (float t = 0f; t < 8f; t += Time.deltaTime)
                {
                    yield return null;
                    float half = Race.Path.WidthAt(Player.Distance) * 0.5f;
                    worst = Mathf.Max(worst, Mathf.Abs(Player.LateralOffset) - half);
                }
                Debug.Log($"[QA25] travelled to {Player.Distance:0} m, worst {worst:0.0} m past the road edge");
                Assert.Greater(Player.Distance, Race.Path.StartDistance + 120f, "The car should have covered the first bend.");
                Assert.Less(worst, 1.5f, "With the assist the car should stay on the road with no steering input.");
            }
            finally { Rally.Car.DrivingAssist.Setting = before; }
        }

        /// <summary>QA-26: the graphics quality option changes draw distances and effects at once.</summary>
        [UnityTest]
        public IEnumerator QA26_GraphicsQuality_AppliesAtOnce()
        {
            var before = GraphicsQuality.Setting;
            try
            {
                yield return LoadStage();
                GraphicsQuality.Setting = GraphicsQuality.Level.Baja;
                yield return null;
                Assert.AreEqual(1500f, Camera.main.farClipPlane, 0.1f);
                Assert.AreEqual(350f, Terrain.activeTerrain.treeDistance, 0.1f);
                GraphicsQuality.Setting = GraphicsQuality.Level.Alta;
                yield return null;
                Assert.AreEqual(5000f, Camera.main.farClipPlane, 0.1f);
                Assert.AreEqual(560f, Terrain.activeTerrain.treeDistance, 0.1f);
            }
            finally { GraphicsQuality.Setting = before; }
        }

        /// <summary>
        /// QA-27: championship rules. Times add up across stages, rivals still on the stage get an estimate, no stage
        /// restarts, and the results offer the next stage.
        /// </summary>
        [UnityTest]
        public IEnumerator QA27_Championship_AddsTimesAndMovesOn()
        {
            Championship.Begin();
            try
            {
                yield return LoadStage();
                yield return StartRace();
                Assert.IsTrue(Championship.Active);

                // Finish the stage after a moment (player first; rivals still driving).
                yield return new WaitForSeconds(2f);
                foreach (var checkpoint in Race.Checkpoints) Player.NotifyCheckpoint(checkpoint);
                float t = 0f;
                while (!Race.ResultsShown && t < 6f) { t += Time.unscaledDeltaTime; yield return null; }
                Assert.IsTrue(Race.ResultsShown);
                Assert.AreEqual(1, Championship.StagesDone, "The first stage's times should be added.");
                var standings = Championship.Standings();
                Assert.AreEqual(Race.Participants.Count, standings.Count);
                Assert.IsTrue(standings.All(e => e.total > 0f), "Every driver needs a time (estimated if still running).");
                var mine = standings.First(e => e.isPlayer);
                Assert.AreEqual(Player.FinishTime, mine.total, 0.01f);

                // No restarts in a championship.
                var before = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
                Race.Restart();
                yield return null;
                Assert.AreEqual(before, UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle, "Restart must do nothing in a championship.");

                // Recording twice for the same stage must not add the times again.
                Championship.RecordStage(Race);
                Assert.AreEqual(mine.total, Championship.Standings().First(e => e.isPlayer).total, 0.01f);
                Assert.IsFalse(Championship.IsLastStage);

                // SIGUIENTE TRAMO: stage 2 loads and starts on its own; a second press in the same moment is ignored.
                Championship.NextStage();
                Championship.NextStage();
                t = 0f;
                while ((UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Stage02" || RaceManager.Instance == null
                        || RaceManager.Instance.CurrentState == RaceManager.State.Intro) && t < 20f)
                { t += Time.unscaledDeltaTime; yield return null; }
                Assert.AreEqual("Stage02", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                Assert.AreEqual(1, Championship.StageIndex, "One press, one stage.");
                Assert.AreNotEqual(RaceManager.State.Intro, RaceManager.Instance.CurrentState, "The next stage should start straight away.");
            }
            finally { Championship.Abandon(); }
        }

        /// <summary>QA-28: a new best run is saved and comes back as a translucent ghost following the stage clock.</summary>
        [UnityTest]
        public IEnumerator QA28_Ghost_SavesBestRunAndReplaysIt()
        {
            // Data round trip and interpolation.
            var samples = new System.Collections.Generic.List<GhostRun.Sample>();
            for (int i = 0; i < 20; i++)
                samples.Add(new GhostRun.Sample { t = i * GhostRun.SampleInterval, position = new Vector3(i, 0f, 0f), rotation = Quaternion.identity });
            var back = GhostRun.Decode(GhostRun.Encode(samples));
            Assert.AreEqual(20, back.Count);
            Assert.IsTrue(GhostRun.Evaluate(back, 0.55f, out var mid, out _));
            Assert.AreEqual(5.5f, mid.x, 0.01f);
            Assert.IsFalse(GhostRun.Evaluate(back, 5f, out _, out _), "Past the end of the run the ghost disappears.");

            yield return LoadStage();
            string bestKey = $"BestTime_{Race.Stage.stageNumber}_{Race.Stage.stageName}";
            string ghostKey = GhostRun.Key(Race);
            float oldBest = PlayerPrefs.GetFloat(bestKey, 0f);
            string oldGhost = PlayerPrefs.GetString(ghostKey, "");
            try
            {
                PlayerPrefs.DeleteKey(bestKey);
                PlayerPrefs.DeleteKey(ghostKey);
                yield return LoadStage(); // reload so the stage starts with no best time
                yield return StartRace();
                Player.Car.Body.linearVelocity = Race.Path.TangentAt(Player.Distance) * 15f;
                yield return new WaitForSeconds(3f);
                foreach (var checkpoint in Race.Checkpoints) Player.NotifyCheckpoint(checkpoint);
                yield return new WaitForSeconds(0.5f);
                Assert.IsTrue(Race.NewBest);
                Assert.IsNotEmpty(PlayerPrefs.GetString(ghostKey, ""), "A new best should save the ghost.");

                yield return LoadStage();
                var ghost = Object.FindFirstObjectByType<GhostCar>();
                Assert.IsNotNull(ghost, "The saved run should come back as a ghost.");
                yield return StartRace();
                yield return new WaitForSeconds(1.5f);
                Assert.IsTrue(ghost.Visible, "The ghost follows its run during the race.");
                Assert.IsNull(ghost.GetComponentInChildren<Collider>(), "The ghost must not collide with anything.");
            }
            finally
            {
                if (oldBest > 0f) PlayerPrefs.SetFloat(bestKey, oldBest); else PlayerPrefs.DeleteKey(bestKey);
                if (oldGhost.Length > 0) PlayerPrefs.SetString(ghostKey, oldGhost); else PlayerPrefs.DeleteKey(ghostKey);
            }
        }

        /// <summary>QA-29: the new cars have their own handling and paint, and picking another car undoes both.</summary>
        [UnityTest]
        public IEnumerator QA29_NewCars_OwnHandlingAndPaint()
        {
            yield return LoadStage();
            Assert.AreEqual(5, CarCatalog.Cars.Length);
            CarCatalog.Apply(Race, 3); // LEYENDA: rear-wheel drive
            yield return null;
            Assert.AreEqual(1f, Player.Car.Tuning.rearTorqueBias, 0.001f, "LEYENDA is rear-wheel drive.");
            Color paint = PaintOf(Player.gameObject);
            Assert.Less(Vector4.Distance(paint, CarCatalog.Cars[3].paint), 0.02f, "LEYENDA wears its own paint.");

            CarCatalog.Apply(Race, 4); // GRUPO B: much more power
            yield return null;
            Assert.Greater(Player.Car.Tuning.peakTorque, 560f);
            Assert.Less(Vector4.Distance(PaintOf(Player.gameObject), CarCatalog.Cars[4].paint), 0.02f);
            foreach (var p in Race.Participants)
                if (!p.IsPlayer) Assert.Greater(Vector4.Distance(PaintOf(p.gameObject), CarCatalog.Cars[4].paint), 0.1f, "Rivals keep their own paint.");

            CarCatalog.Apply(Race, 0); // back to the standard car
            yield return null;
            Assert.AreEqual(0.58f, Player.Car.Tuning.rearTorqueBias, 0.001f);
            Assert.Greater(Vector4.Distance(PaintOf(Player.gameObject), CarCatalog.Cars[4].paint), 0.1f, "The special paint is removed.");
        }

        private static Color PaintOf(GameObject car)
        {
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name.StartsWith("Paint_")) return m.GetColor("_BaseColor");
            return Color.clear;
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
