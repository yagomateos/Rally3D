using UnityEngine;

namespace Rally.Systems
{
    /// <summary>Server address for the online table (empty = this device only). Resources/LeaderboardConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Rally/Leaderboard Config", fileName = "LeaderboardConfig")]
    public class LeaderboardConfig : ScriptableObject
    {
        [Tooltip("Base URL of the leaderboard server, e.g. https://rally3d-scores.<account>.workers.dev (empty = offline).")]
        public string url = "";

        private static LeaderboardConfig instance;
        private static bool loaded;

        public static string Url
        {
            get
            {
                if (!loaded) { instance = Resources.Load<LeaderboardConfig>("LeaderboardConfig"); loaded = true; }
                return instance != null ? instance.url : "";
            }
        }
    }
}
