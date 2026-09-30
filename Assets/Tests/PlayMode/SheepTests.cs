using System.Collections;
using NUnit.Framework;
using Rally.Car;
using Rally.Systems;
using Rally.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rally.Tests
{
    /// <summary>
    /// QA-17: sheep on the coastal stage. It walks across the road, bleats with 3D sound, and when the player's car
    /// hits it at speed it is knocked away screaming, while the car keeps most of its speed and takes little damage.
    /// </summary>
    public class SheepTests
    {
        [UnityTest]
        public IEnumerator QA17_Sheep_CrossesBleatsAndIsKnockedAwayWhenHit()
        {
            yield return Rally.Systems.StageLoader.LoadRoutine("Stage04");
            float t = 0f;
            while ((RaceManager.Instance == null || RaceManager.Instance.Player == null) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            var race = RaceManager.Instance;
            Assert.IsNotNull(race?.Player, "Stage04 did not initialise a player.");

            var crossing = Object.FindFirstObjectByType<SheepCrossing>();
            Assert.IsNotNull(crossing, "The coastal stage needs sheep crossings.");
            Assert.GreaterOrEqual(crossing.CrossingPoints.Count, 2, "At least two places where a sheep may cross.");

            race.BeginCountdown();
            t = 0f;
            while (race.CurrentState != RaceManager.State.Racing && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }

            // It walks across and bleats.
            var player = race.Player;
            float at = player.Distance + 60f;
            var sheep = crossing.SpawnAt(at, crossing.InlandSide(at));
            Assert.IsNotNull(sheep);
            var voice = sheep.GetComponent<AudioSource>();
            Assert.IsNotNull(voice, "The sheep needs a voice.");
            Assert.AreEqual(1f, voice.spatialBlend, "Bleats are 3D: only heard when close.");
            float startOffset = race.Path.LateralOffset(sheep.transform.position, at);
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(sheep.IsWalking);
            float offset = race.Path.LateralOffset(sheep.transform.position, at);
            Assert.Greater(Mathf.Abs(offset - startOffset), 1.3f, "The sheep should be walking across the road.");

            // A car hits it at ~75 km/h.
            var car = player.Car;
            Vector3 forward = race.Path.TangentAt(player.Distance);
            sheep.transform.position = car.Body.position + forward * 14f + Vector3.up * 0.1f;
            sheep.GetComponent<Rigidbody>().position = sheep.transform.position;
            car.Body.linearVelocity = forward * 21f;
            t = 0f;
            while (!sheep.WasHit && t < 3f) { t += Time.deltaTime; yield return new WaitForFixedUpdate(); }
            Assert.IsTrue(sheep.WasHit, "The car should have hit the sheep.");
            yield return new WaitForSeconds(0.3f);

            float carSpeed = car.Body.linearVelocity.magnitude;
            float damage = car.GetComponent<CarDamage>() != null ? car.GetComponent<CarDamage>().Damage01 : 0f;
            Debug.Log($"[QA17] car speed after the hit {carSpeed * 3.6f:0} km/h, damage {damage:0.00}");
            Assert.Greater(carSpeed, 12f, "A 70 kg sheep must not stop the car dead.");
            Assert.Less(damage, 0.25f, "Hitting a sheep should only dent the car a little.");
            Assert.Greater(sheep.GetComponent<Rigidbody>().linearVelocity.magnitude, 5f, "The sheep should be knocked away.");
        }

        /// <summary>
        /// Not a check: renders what the player sees while a sheep crosses 45 m ahead (and a close-up), to
        /// RALLY_SHOT_DIR, so its look and visibility can be reviewed.
        /// </summary>
        [UnityTest, Explicit("Renders screenshots for review.")]
        public IEnumerator SheepCrossing_Screenshots()
        {
            string dir = System.Environment.GetEnvironmentVariable("RALLY_SHOT_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Application.temporaryCachePath;
            yield return Rally.Systems.StageLoader.LoadRoutine("Stage04");
            float t = 0f;
            while ((RaceManager.Instance == null || RaceManager.Instance.Player == null) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            var race = RaceManager.Instance;
            race.BeginCountdown();
            t = 0f;
            while (race.CurrentState != RaceManager.State.Racing && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }

            var crossing = Object.FindFirstObjectByType<SheepCrossing>();
            float at = race.Player.Distance + 45f;
            var sheep = crossing.SpawnAt(at, crossing.InlandSide(at));
            var cam = Camera.main;
            float[] times = { 0.5f, 4f, 8f };
            float elapsed = 0f;
            for (int i = 0; i < times.Length; i++)
            {
                while (elapsed < times[i]) { elapsed += Time.deltaTime; yield return null; }
                Save(cam, $"{dir}/sheep-{i}.png");
            }
            // Close-up from the roadside.
            Vector3 p = sheep.transform.position;
            cam.enabled = false;
            cam.transform.position = p + sheep.transform.right * 2.4f + sheep.transform.forward * 1.2f + Vector3.up * 1.1f;
            cam.transform.LookAt(p + Vector3.up * 0.7f);
            Save(cam, $"{dir}/sheep-close.png");
            cam.enabled = true;
            Debug.Log($"[Sheep] screenshots in {dir}");
        }

        private static void Save(Camera cam, string file)
        {
            var rt = new RenderTexture(1280, 720, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = old;
            RenderTexture.active = null;
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
