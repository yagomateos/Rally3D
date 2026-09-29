using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rally.Systems;
using UnityEngine;
using UnityEngine.TestTools;
using static Rally.Tests.StageTestUtility;

namespace Rally.Tests
{
    /// <summary>
    /// Measurement, not a regular test: runs Stage01 at 4x speed on each difficulty and logs how long the rivals take,
    /// i.e. the time the player has to beat. Explicit: run it on purpose (a few minutes).
    /// </summary>
    public class DifficultyMeasureTests
    {
        [UnityTest, Explicit("Measurement run: rivals' stage times per difficulty (several minutes).")]
        public IEnumerator Measure_RivalStageTimes_PerDifficulty()
        {
            int saved = DifficultyData.Selected;
            var best = new float[3];
            try
            {
                for (int level = 0; level < 3; level++)
                {
                    DifficultyData.Selected = level;
                    yield return LoadStage();
                    yield return StartRace();
                    Time.timeScale = 4f;
                    var rivals = Race.Participants.Where(p => !p.IsPlayer).ToList();
                    float t = 0f;
                    float follow = 0f;
                    while (rivals.Any(r => !r.HasFinished) && t < 400f)
                    {
                        t += Time.deltaTime;
                        // Keep the player's car just behind the leading rival, so the catch-up (which only acts when
                        // a rival is far ahead of the player) stays off: these are the times of a close race.
                        follow += Time.deltaTime;
                        var leader = rivals.Where(r => !r.HasFinished).OrderByDescending(r => r.Distance).FirstOrDefault();
                        if (follow > 1f && leader != null && leader.Distance - Player.Distance > 40f)
                        {
                            follow = 0f;
                            PlaceOnRoad(Player, Race.Path, leader.Distance - 25f);
                        }
                        yield return null;
                    }
                    Time.timeScale = 1f;
                    var times = rivals.Select(r => r.HasFinished ? r.FinishTime : float.PositiveInfinity).ToList();
                    best[level] = times.Min();
                    Debug.Log($"[Difficulty] {DifficultyData.Instance.levels[level].name}: " +
                              string.Join("  ", rivals.Select(r => $"{r.DisplayName} {(r.HasFinished ? RaceManager.FormatTime(r.FinishTime) : "no acabó")}")));
                }
            }
            finally
            {
                Time.timeScale = 1f;
                DifficultyData.Selected = saved;
            }
            Assert.Greater(best[0], best[1], "FÁCIL rivals should be slower than NORMAL.");
            Assert.Greater(best[1], best[2], "NORMAL rivals should be slower than DIFÍCIL.");
        }
    }
}
