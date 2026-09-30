using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// CAMPEONATO: the four stages in a row, the times of every driver added up. Kept in memory across the stage
    /// loads (a page reload ends it). A rival still on the stage when the player's results appear gets a time
    /// extrapolated from its pace so far. No stage restarts during a championship; leaving to the menu abandons it.
    /// </summary>
    public static class Championship
    {
        public struct Entry
        {
            public string name;
            public bool isPlayer;
            public float total;
            public float lastStage;
        }

        public static bool Active { get; private set; }
        public static int StageIndex { get; private set; }
        public static int StageCount => StageCatalog.Stages.Length;
        public static bool IsLastStage => StageIndex >= StageCount - 1;
        /// <summary>Stages whose times have been added (the current one counts once its results are in).</summary>
        public static int StagesDone { get; private set; }

        private static readonly Dictionary<string, Entry> totals = new Dictionary<string, Entry>();

        private static bool advancing;

        /// <summary>Called by each stage's race manager when it wakes up.</summary>
        public static void StageLoaded() => advancing = false;

        public static void Begin()
        {
            advancing = false;
            Active = true;
            StageIndex = 0;
            StagesDone = 0;
            totals.Clear();
        }

        public static void Abandon()
        {
            Active = false;
            totals.Clear();
        }

        public static string CurrentScene => StageCatalog.Stages[Mathf.Clamp(StageIndex, 0, StageCount - 1)].scene;

        /// <summary>Adds this stage's times (once per stage). Called when the player's results are shown.</summary>
        public static void RecordStage(RaceManager race)
        {
            if (!Active || race == null || StagesDone > StageIndex) return;
            foreach (var p in race.Participants)
            {
                if (p == null) continue;
                float time = StageTime(race, p);
                totals.TryGetValue(p.DisplayName, out var e);
                e.name = p.DisplayName;
                e.isPlayer = p.IsPlayer;
                e.total += time;
                e.lastStage = time;
                totals[p.DisplayName] = e;
            }
            StagesDone = StageIndex + 1;
        }

        /// <summary>A driver's time for the stage: their finish time, or an estimate from their pace so far.</summary>
        public static float StageTime(RaceManager race, RaceParticipant p)
        {
            if (p.HasFinished) return p.FinishTime;
            float elapsed = Mathf.Max(1f, race.StageTime);
            float covered = Mathf.Clamp01(p.Progress01);
            // Pace so far over the rest of the stage; a car that has barely moved gets a generous ceiling.
            return covered > 0.1f ? elapsed / covered : elapsed * 2.5f;
        }

        /// <summary>Overall order, fastest total first.</summary>
        public static IReadOnlyList<Entry> Standings() => totals.Values.OrderBy(e => e.total).ToList();

        public static int PlayerPosition()
        {
            var list = Standings();
            for (int i = 0; i < list.Count; i++) if (list[i].isPlayer) return i + 1;
            return list.Count;
        }

        /// <summary>On to the next stage (loads it and starts it straight away).</summary>
        public static void NextStage()
        {
            // The focused button and the Confirm key can both fire on the same press: advance once per stage.
            if (!Active || IsLastStage || advancing) return;
            advancing = true;
            StageIndex++;
            RaceManager.LoadStageAndStart(CurrentScene);
        }

        /// <summary>Test hook: sets the stage the championship is on.</summary>
        public static void SetStageForTests(int index) => StageIndex = Mathf.Clamp(index, 0, StageCount - 1);
    }
}
