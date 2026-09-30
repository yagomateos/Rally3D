using System;
using Rally.Car;
using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// The cars the player can pick in the main menu. Each one is one of the three liveries built into the
    /// stage, with its own handling: the player's car gets that livery (swapping with the rival who wore it)
    /// and a private copy of the tuning with the car's modifiers applied.
    /// </summary>
    public static class CarCatalog
    {
        public struct Entry
        {
            public string livery;      // livery / prefab name used by the stage builder (materials Paint_<livery> ...)
            public string name;        // shown in the menu
            public string description;
            public int speed, acceleration, grip; // 1..5, for the menu bars
            public Action<CarTuning> modify;
            /// <summary>Own colours on top of the base livery's car (for cars that aren't one of the rivals' liveries).</summary>
            public bool repaint;
            public Color paint, accent;
            public string number;
        }

        public static readonly Entry[] Cars =
        {
            new Entry
            {
                livery = "PlayerCar", name = "VALDENIEBLA  #7", description = "EQUILIBRADO. FÁCIL DE LLEVAR EN TODAS LAS SUPERFICIES.",
                speed = 3, acceleration = 3, grip = 4,
                modify = t => { }
            },
            new Entry
            {
                livery = "RivalCar_Azure", name = "AZUR  #3", description = "EL MÁS RÁPIDO EN RECTA, PERO PATINA MÁS EN LAS CURVAS.",
                speed = 5, acceleration = 4, grip = 2,
                modify = t =>
                {
                    t.peakTorque *= 1.08f;
                    t.maxSpeedKph += 12f;
                    t.aeroDrag *= 0.94f;
                    t.sidewaysStiffness *= 0.93f;
                    t.mass += 40f;
                }
            },
            new Entry
            {
                livery = "RivalCar_Crimson", name = "CARMESÍ  #11", description = "MUCHO AGARRE Y GIRO RÁPIDO. MENOS PUNTA.",
                speed = 2, acceleration = 3, grip = 5,
                modify = t =>
                {
                    t.sidewaysStiffness *= 1.08f;
                    t.forwardStiffness *= 1.05f;
                    t.steerSpeed *= 1.12f;
                    t.maxSpeedKph -= 10f;
                    t.peakTorque *= 0.96f;
                }
            },
            new Entry
            {
                livery = "PlayerCar", name = "LEYENDA  #1", description = "TRACCIÓN TRASERA. DERRAPA CON FACILIDAD: PARA EXPERTOS.",
                speed = 3, acceleration = 4, grip = 2,
                repaint = true, paint = new Color(0.05f, 0.28f, 0.14f), accent = new Color(0.95f, 0.75f, 0.2f), number = "1",
                modify = t =>
                {
                    t.rearTorqueBias = 1f;         // all the power to the rear wheels
                    t.powerOversteerGrip = 0.78f;  // the tail steps out under power
                    t.handbrakeRearGrip = 0.34f;
                    t.peakTorque *= 1.05f;
                    t.tractionControl = Mathf.Min(t.tractionControl, 0.2f);
                    t.yawStability *= 0.8f;
                }
            },
            new Entry
            {
                livery = "RivalCar_Crimson", name = "GRUPO B  #9", description = "MUCHÍSIMA POTENCIA Y PUNTA. MUY DIFÍCIL DE DOMAR.",
                speed = 5, acceleration = 5, grip = 2,
                repaint = true, paint = new Color(0.07f, 0.07f, 0.08f), accent = new Color(1f, 0.82f, 0.1f), number = "9",
                modify = t =>
                {
                    t.peakTorque *= 1.28f;
                    t.maxSpeedKph += 18f;
                    t.mass -= 60f;
                    t.sidewaysStiffness *= 0.94f;
                    t.shiftTime *= 0.8f;
                    t.brakeTorque *= 1.06f;
                }
            },
        };

        private const string SelectedKey = "Rally.SelectedCar";

        /// <summary>Last car picked (remembered between sessions).</summary>
        public static int Selected
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, 0), 0, Cars.Length - 1);
            set
            {
                PlayerPrefs.SetInt(SelectedKey, Mathf.Clamp(value, 0, Cars.Length - 1));
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Gives the player the chosen car: livery (paint, accent, stripes, number, map colour) swapped with the
        /// rival who wore it, and handling from a private tuning copy. Safe to call repeatedly while browsing.
        /// </summary>
        public static void Apply(RaceManager race, int index)
        {
            if (race == null || race.Player == null) return;
            var entry = Cars[Mathf.Clamp(index, 0, Cars.Length - 1)];
            var player = race.Player;
            RemovePaint(player); // back to the livery's own materials before any swap

            string playerLivery = LiveryOf(player.gameObject);
            if (playerLivery != null && playerLivery != entry.livery)
            {
                foreach (var other in race.Participants)
                {
                    if (other == player || LiveryOf(other.gameObject) != entry.livery) continue;
                    SwapLivery(player, other);
                    break;
                }
            }

            if (entry.repaint) Repaint(player, entry);

            var controller = player.Car;
            var current = controller.Tuning;
            bool isCopy = current.name.EndsWith(CopySuffix);
            if (!isCopy) sharedTuning = current; // the shared asset: every pick starts from it, so modifiers never stack
            var tuning = UnityEngine.Object.Instantiate(sharedTuning);
            tuning.name = sharedTuning.name + CopySuffix;
            entry.modify(tuning);
            DifficultyData.ApplyToPlayer(tuning); // assists of the chosen difficulty level
            if (race.Stage != null) race.Stage.ApplyToTuning(tuning); // e.g. the coastal stage's higher top speed
            controller.ApplyTuning(tuning);
            if (isCopy) UnityEngine.Object.Destroy(current);
        }

        private const string CopySuffix = " (jugador)";

        // Runtime paint clones on the player's car → the livery material they replaced (never edit the assets).
        private static readonly System.Collections.Generic.Dictionary<Material, Material> paintClones =
            new System.Collections.Generic.Dictionary<Material, Material>();
        private static string originalNumber;
        private static Color? originalColor;

        /// <summary>Own colours for a car that isn't one of the rivals' liveries: runtime copies of its paint.</summary>
        private static void Repaint(RaceParticipant player, Entry entry)
        {
            string livery = LiveryOf(player.gameObject);
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    Color? tint = m.name == "Paint_" + livery ? entry.paint : m.name == "Accent_" + livery ? entry.accent : (Color?)null;
                    if (tint == null) continue;
                    var clone = new Material(m) { name = m.name }; // same name: the livery is still recognised
                    clone.SetColor("_BaseColor", tint.Value);
                    paintClones[clone] = m;
                    mats[i] = clone;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
            var numbers = player.GetComponentsInChildren<TextMesh>(true);
            if (numbers.Length > 0 && originalNumber == null) originalNumber = numbers[0].text;
            foreach (var t in numbers) t.text = entry.number;
            if (originalColor == null) originalColor = player.Color;
            player.Configure(player.DisplayName, player.IsPlayer, entry.paint);
        }

        private static void RemovePaint(RaceParticipant player)
        {
            if (paintClones.Count == 0) return;
            bool found = false; // clones left from a previous stage load are just freed
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && paintClones.TryGetValue(mats[i], out var original)) { mats[i] = original; changed = true; }
                if (changed) { r.sharedMaterials = mats; found = true; }
            }
            foreach (var clone in paintClones.Keys) UnityEngine.Object.Destroy(clone);
            paintClones.Clear();
            if (found && originalNumber != null)
                foreach (var t in player.GetComponentsInChildren<TextMesh>(true)) t.text = originalNumber;
            if (found && originalColor != null) player.Configure(player.DisplayName, player.IsPlayer, originalColor.Value);
            originalNumber = null;
            originalColor = null;
        }
        private static CarTuning sharedTuning;

        /// <summary>Livery name of a car, read from its paint material ("Paint_&lt;livery&gt;").</summary>
        public static string LiveryOf(GameObject car)
        {
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name.StartsWith("Paint_")) return m.name.Substring("Paint_".Length);
            return null;
        }

        private static void SwapLivery(RaceParticipant a, RaceParticipant b)
        {
            string la = LiveryOf(a.gameObject), lb = LiveryOf(b.gameObject);
            var matsA = LiveryMaterials(a.gameObject, la);
            var matsB = LiveryMaterials(b.gameObject, lb);
            Remap(a.gameObject, matsA, matsB);
            Remap(b.gameObject, matsB, matsA);

            var numbersA = a.GetComponentsInChildren<TextMesh>(true);
            var numbersB = b.GetComponentsInChildren<TextMesh>(true);
            string na = numbersA.Length > 0 ? numbersA[0].text : null, nb = numbersB.Length > 0 ? numbersB[0].text : null;
            if (na != null && nb != null)
            {
                foreach (var t in numbersA) t.text = nb;
                foreach (var t in numbersB) t.text = na;
            }

            Color ca = a.Color, cb = b.Color;
            a.Configure(a.DisplayName, a.IsPlayer, cb);
            b.Configure(b.DisplayName, b.IsPlayer, ca);
        }

        // Paint, accent and stripe materials of one livery, in that order.
        private static Material[] LiveryMaterials(GameObject car, string livery)
        {
            var result = new Material[3];
            string[] prefixes = { "Paint_", "Accent_", "Stripe_" };
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    for (int i = 0; i < 3; i++)
                        if (m != null && m.name == prefixes[i] + livery) result[i] = m;
            return result;
        }

        private static void Remap(GameObject car, Material[] from, Material[] to)
        {
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int s = 0; s < mats.Length; s++)
                    for (int i = 0; i < 3; i++)
                        if (from[i] != null && to[i] != null && mats[s] == from[i]) { mats[s] = to[i]; changed = true; break; }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
