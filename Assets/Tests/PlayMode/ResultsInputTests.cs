using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Rally.Tests.StageTestUtility;

namespace Rally.Tests
{
    /// <summary>QA-04: confirming on the results screen must reload the stage exactly once.</summary>
    public class ResultsInputTests : InputTestFixture
    {
        private int sceneLoads;

        private void CountLoad(Scene scene, LoadSceneMode mode) => sceneLoads++;

        [UnityTest]
        public IEnumerator QA04_ConfirmOnResults_RestartsStageOnce()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return LoadStage();
            yield return StartRace();

            // Finish the stage by crossing every checkpoint in order.
            foreach (var checkpoint in Race.Checkpoints) Player.NotifyCheckpoint(checkpoint);
            float t = 0f;
            while (!Race.ResultsShown && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(Race.ResultsShown, "Results screen never appeared.");
            yield return null;

            sceneLoads = 0;
            SceneManager.sceneLoaded += CountLoad;
            try
            {
                Press(keyboard.enterKey);
                yield return null;
                Release(keyboard.enterKey);
                yield return new WaitForSecondsRealtime(3f);
            }
            finally
            {
                SceneManager.sceneLoaded -= CountLoad;
            }

            Assert.AreEqual(1, sceneLoads, "Enter on the results screen should reload the stage exactly once.");
        }
    }
}
