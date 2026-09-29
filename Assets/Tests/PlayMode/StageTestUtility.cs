using System.Collections;
using NUnit.Framework;
using Rally.Systems;
using Rally.Track;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rally.Tests
{
    /// <summary>Shared helpers for PlayMode tests that run on the real Stage01 scene.</summary>
    public static class StageTestUtility
    {
        public const string SceneName = "Stage01";
        private const float Timeout = 20f;

        public static RaceManager Race => RaceManager.Instance;
        public static RaceParticipant Player => RaceManager.Instance.Player;

        /// <summary>Loads Stage01 and waits until the race manager has initialised the participants.</summary>
        public static IEnumerator LoadStage()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            float t = 0f;
            while ((RaceManager.Instance == null || RaceManager.Instance.Player == null) && t < Timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(RaceManager.Instance?.Player, "Stage01 did not initialise a player.");
        }

        /// <summary>Skips the title card and waits for GO.</summary>
        public static IEnumerator StartRace()
        {
            Race.BeginCountdown();
            float t = 0f;
            while (Race.CurrentState != RaceManager.State.Racing && t < Timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(RaceManager.State.Racing, Race.CurrentState, "Race never reached GO.");
        }

        /// <summary>Ground height below a point (terrain, road or props), ignoring cars and triggers.</summary>
        public static float GroundHeight(Vector3 point)
        {
            int mask = ~LayerMask.GetMask("Vehicle");
            return Physics.Raycast(point + Vector3.up * 300f, Vector3.down, out RaycastHit hit, 600f, mask, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : point.y;
        }

        /// <summary>Teleports a car and stops it.</summary>
        public static void Place(RaceParticipant car, Vector3 position, Quaternion rotation)
        {
            car.Car.Body.position = position;
            car.Car.Body.rotation = rotation;
            car.transform.SetPositionAndRotation(position, rotation);
            car.Car.ResetMotion();
            Physics.SyncTransforms();
        }

        /// <summary>Puts a car on the road centre line at a distance along the stage.</summary>
        public static void PlaceOnRoad(RaceParticipant car, TrackPath path, float distance)
        {
            Vector3 p = path.PositionAt(distance);
            Vector3 t = path.TangentAt(distance);
            Place(car, p + Vector3.up * 0.6f, Quaternion.LookRotation(new Vector3(t.x, 0f, t.z)));
        }
    }
}
