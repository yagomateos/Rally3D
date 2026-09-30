using System.Collections;
using NUnit.Framework;
using Rally.Systems;
using Rally.Track;
using Rally.Track.Generation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rally.Tests
{
    /// <summary>
    /// QA-14: the terrain never shows through the road. Samples the driving surface along the whole stage and
    /// compares the terrain height with the road surface under the same point (guards the web size cuts to the
    /// heightmap resolution).
    /// </summary>
    public class TerrainRoadTests
    {
        private const float Tolerance = 0.03f;

        [UnityTest]
        public IEnumerator QA14_Terrain_StaysBelowTheRoad([Values("Stage01", "Stage02", "Stage03", "Stage04", "Stage05")] string scene)
        {
            yield return Rally.Systems.StageLoader.LoadRoutine(scene);
            float t = 0f;
            while ((RaceManager.Instance == null || RaceManager.Instance.Player == null) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsNotNull(RaceManager.Instance?.Player, $"{scene} did not initialise a player.");
            yield return new WaitForFixedUpdate();

            var terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No terrain in the stage.");
            var route = StageRoute.Build(RaceManager.Instance.Stage);

            int checkedPoints = 0, poking = 0;
            float worst = 0f;
            for (int i = 0; i < route.Count; i += 2)
            {
                Vector2 tangent = route.Tangent(i);
                Vector2 right = new Vector2(tangent.y, -tangent.x);
                float half = route.Widths[i] * 0.5f; // driving surface only: the verges sink into the terrain by design
                for (float lateral = -half; lateral <= half; lateral += 0.5f)
                {
                    Vector2 p = route.Planar[i] + right * lateral;
                    var origin = new Vector3(p.x, 2000f, p.y);
                    float roadY = float.NegativeInfinity;
                    foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 4000f, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.collider.GetComponent<SurfaceZone>() != null) roadY = Mathf.Max(roadY, hit.point.y);
                    if (float.IsNegativeInfinity(roadY)) continue;

                    float terrainY = terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
                    checkedPoints++;
                    float above = terrainY - roadY;
                    if (above > Tolerance) poking++;
                    worst = Mathf.Max(worst, above);
                }
            }

            float share = checkedPoints > 0 ? (float)poking / checkedPoints : 1f;
            Debug.Log($"[QA14] {scene}: heightmap {terrain.terrainData.heightmapResolution}, {checkedPoints} points, " +
                      $"{poking} above the road ({share:P2}), worst {worst:0.00} m");
            Assert.Greater(checkedPoints, 1000, "Road colliders not found.");
            Assert.Less(share, 0.005f, "The terrain shows through the road in too many places.");
            Assert.Less(worst, 0.15f, "The terrain rises visibly through the road somewhere.");
        }
    }
}
