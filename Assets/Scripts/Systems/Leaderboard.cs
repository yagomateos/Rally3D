using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Rally.Systems
{
    /// <summary>
    /// Best-times table per stage (top 10: name, time, car, date). Always kept on this device; when
    /// <see cref="LeaderboardConfig"/> (Resources/LeaderboardConfig) has a server URL, times are also sent to it and
    /// the table shows the online top 10 (see server/leaderboard-worker.js and docs/CLASIFICACION_ONLINE.md).
    /// </summary>
    public static class Leaderboard
    {
        public const int Size = 10;

        [Serializable]
        public struct Entry
        {
            public string name;
            public float time;
            public string car;
            public string date;
        }

        [Serializable]
        private class EntryList { public Entry[] entries = new Entry[0]; }

        [Serializable]
        private class Submission { public string stage, name, car; public float time; }

        // ------------------------------------------------------------------ player name

        private const string NameKey = "Rally.PlayerName";

        public static string PlayerName
        {
            get
            {
                string n = PlayerPrefs.GetString(NameKey, "");
                if (string.IsNullOrEmpty(n))
                {
                    n = "PILOTO " + UnityEngine.Random.Range(100, 1000);
                    PlayerName = n;
                }
                return n;
            }
            set
            {
                PlayerPrefs.SetString(NameKey, Clean(value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Upper case letters, digits and spaces, at most 16 characters.</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "PILOTO";
            var chars = raw.ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').Take(16).ToArray();
            string s = new string(chars).Trim();
            return s.Length == 0 ? "PILOTO" : s;
        }

        // ------------------------------------------------------------------ local table

        public static string StageKey(string number, string name) => $"{number}_{name}";

        private static string LocalKey(string stageKey) => "Rally.Times." + stageKey;

        public static List<Entry> LocalTop(string stageKey)
        {
            var json = PlayerPrefs.GetString(LocalKey(stageKey), "");
            if (string.IsNullOrEmpty(json)) return new List<Entry>();
            try { return (JsonUtility.FromJson<EntryList>(json)?.entries ?? new Entry[0]).ToList(); }
            catch (ArgumentException) { return new List<Entry>(); }
        }

        /// <summary>Adds a finished run to this device's table. Returns its place (1-based) or 0 if outside the top 10.</summary>
        public static int SubmitLocal(string stageKey, string car, float time)
        {
            if (time <= 0f) return 0;
            var list = LocalTop(stageKey);
            var entry = new Entry { name = PlayerName, time = time, car = car, date = DateTime.Now.ToString("dd/MM/yyyy") };
            list.Add(entry);
            list = list.OrderBy(e => e.time).Take(Size).ToList();
            PlayerPrefs.SetString(LocalKey(stageKey), JsonUtility.ToJson(new EntryList { entries = list.ToArray() }));
            PlayerPrefs.Save();
            int place = list.FindIndex(e => Mathf.Approximately(e.time, time) && e.name == entry.name) + 1;
            return place;
        }

        // ------------------------------------------------------------------ online table

        public static bool Online => !string.IsNullOrEmpty(LeaderboardConfig.Url);

        /// <summary>Online top 10 for a stage; calls back with null when offline or on error.</summary>
        public static IEnumerator FetchOnline(string stageKey, Action<List<Entry>> done)
        {
            if (!Online) { done?.Invoke(null); yield break; }
            using var request = UnityWebRequest.Get($"{LeaderboardConfig.Url.TrimEnd('/')}/scores?stage={UnityWebRequest.EscapeURL(stageKey)}");
            request.timeout = 8;
            yield return request.SendWebRequest();
            done?.Invoke(request.result == UnityWebRequest.Result.Success ? Parse(request.downloadHandler.text) : null);
        }

        /// <summary>Sends a run to the server; calls back with the updated online top 10 (null when offline or on error).</summary>
        public static IEnumerator SubmitOnline(string stageKey, string car, float time, Action<List<Entry>> done)
        {
            if (!Online || time <= 0f) { done?.Invoke(null); yield break; }
            var body = JsonUtility.ToJson(new Submission { stage = stageKey, name = PlayerName, car = car, time = time });
            using var request = new UnityWebRequest($"{LeaderboardConfig.Url.TrimEnd('/')}/scores", "POST")
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 8
            };
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            done?.Invoke(request.result == UnityWebRequest.Result.Success ? Parse(request.downloadHandler.text) : null);
        }

        private static List<Entry> Parse(string json)
        {
            try { return (JsonUtility.FromJson<EntryList>(json)?.entries ?? new Entry[0]).OrderBy(e => e.time).Take(Size).ToList(); }
            catch (ArgumentException) { return null; }
        }

        /// <summary>Runs the online requests (which need a MonoBehaviour) across stage loads.</summary>
        public static void Run(IEnumerator routine)
        {
            if (runner == null)
            {
                var go = new GameObject("LeaderboardRunner") { hideFlags = HideFlags.HideInHierarchy };
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
            }
            runner.StartCoroutine(routine);
        }

        private static Runner runner;
        private class Runner : MonoBehaviour { }
    }
}
