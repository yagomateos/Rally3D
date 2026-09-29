using System.Collections;
using NUnit.Framework;
using Rally.CameraSystem;
using UnityEngine;
using UnityEngine.TestTools;
using static Rally.Tests.StageTestUtility;

namespace Rally.Tests
{
    /// <summary>QA-13: holding "look back" turns the camera round smoothly and releasing it brings it back.</summary>
    public class LookBackTests
    {
        [UnityTest]
        public IEnumerator QA13_LookBack_TurnsCameraRoundSmoothlyAndBack()
        {
            yield return LoadStage();
            yield return StartRace();
            var cam = Camera.main.transform;
            var car = Player.transform;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Vector3.Dot(cam.forward, car.forward), 0.5f, "Camera should start looking forward.");

            float maxStep = 0f;
            Quaternion last = cam.rotation;
            RallyCamera.TouchLookBack = true;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                yield return null;
                maxStep = Mathf.Max(maxStep, Quaternion.Angle(last, cam.rotation));
                last = cam.rotation;
            }
            Assert.Less(Vector3.Dot(cam.forward, car.forward), -0.5f, "Holding look-back should face the camera backwards.");

            RallyCamera.TouchLookBack = false;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                yield return null;
                maxStep = Mathf.Max(maxStep, Quaternion.Angle(last, cam.rotation));
                last = cam.rotation;
            }
            Assert.Greater(Vector3.Dot(cam.forward, car.forward), 0.5f, "Releasing it should bring the camera back.");
            Assert.Less(maxStep, 45f, "The swing must be gradual, not a cut.");
        }

        /// <summary>QA-18: the right stick looks round the car in any direction and the view returns when released.</summary>
        [UnityTest]
        public IEnumerator QA18_RightStick_LooksInAnyDirection()
        {
            yield return LoadStage();
            yield return StartRace();
            var cam = Camera.main.transform;
            var car = Player.transform;
            yield return new WaitForSeconds(0.5f);

            RallyCamera.ExternalLook = new Vector2(1f, 0f); // stick right
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(Vector3.Dot(cam.forward, car.right), 0.7f, "Stick right should look to the car's right.");

            RallyCamera.ExternalLook = new Vector2(-1f, 0f); // stick left, straight from right
            yield return new WaitForSeconds(0.8f);
            Assert.Less(Vector3.Dot(cam.forward, car.right), -0.7f, "Stick left should look to the car's left.");

            RallyCamera.ExternalLook = new Vector2(0f, -1f); // stick down
            yield return new WaitForSeconds(0.8f);
            Assert.Less(Vector3.Dot(cam.forward, car.forward), -0.7f, "Stick down should look behind.");

            RallyCamera.ExternalLook = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(Vector3.Dot(cam.forward, car.forward), 0.7f, "Releasing the stick should bring the view back ahead.");
        }
    }
}
