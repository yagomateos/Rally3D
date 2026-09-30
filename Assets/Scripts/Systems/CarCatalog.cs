using System;
using Rally.Car;
using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// The cars the player can pick in the main menu. Each one is one of the three liveries built into the
    /// stage, with its own handling: the player's car gets that livery (swapping with the rival who wore it)
    /// and a private copy of the tuning with the car's modifiers applied. Each car also has its own body
    /// (Resources/CarModels, built by CarModelFactory), inspired by a classic rally car but with an invented
    /// name and no brand: rivals wear the body of the car their livery belongs to.
    /// </summary>
    public static class CarCatalog
    {
        public struct Entry
        {
            public string livery;      // livery / prefab name used by the stage builder (materials Paint_<livery> ...)
            public string model;       // body mesh in Resources/CarModels
            public string name;        // shown in the menu
            public string description;
            public int speed, acceleration, grip; // 1..5, for the menu bars
            public Action<CarTuning> modify;
            /// <summary>Own colours on top of the base livery's car (for cars that aren't one of the rivals' liveries).</summary>
            public bool repaint;
            public Color paint, accent;
            public string number;
        }

        public const string ModelFolder = "CarModels";
        public const string ModelPleyades = "Pleyades", ModelLanza = "Lanza", ModelItalica = "Italica",
            ModelEscolta = "Escolta", ModelLeon = "Leon";

        public static string ModelMeshName(string model) => "M_Car_" + model;

        /// <summary>
        /// Cabin of each body: waistline height (the cabin floor), the driver's head, the roof (height and rear
        /// edge) and the foot of the rear window. The body generator builds the cabin from these and the CABINA camera sits inside them.
        /// </summary>
        public struct Cockpit { public float seatY, headZ, roofY, roofRear, cabinRear; }

        /// <summary>Left-hand drive: the driver sits on the left, the co-driver on the right.</summary>
        public const float DriverX = -0.36f;

        public static Cockpit CockpitOf(string model)
        {
            switch (model)
            {
                case ModelPleyades: return new Cockpit { seatY = 0.93f, headZ = -0.38f, roofY = 1.4f, roofRear = -0.72f, cabinRear = -1.2f };
                case ModelLanza: return new Cockpit { seatY = 0.92f, headZ = -0.44f, roofY = 1.42f, roofRear = -0.8f, cabinRear = -1.25f };
                case ModelItalica: return new Cockpit { seatY = 0.93f, headZ = -0.26f, roofY = 1.38f, roofRear = -1.64f, cabinRear = -1.8f };
                case ModelEscolta: return new Cockpit { seatY = 0.9f, headZ = -0.32f, roofY = 1.34f, roofRear = -0.62f, cabinRear = -1.05f };
                case ModelLeon: return new Cockpit { seatY = 0.93f, headZ = -0.32f, roofY = 1.38f, roofRear = -1.46f, cabinRear = -1.66f };
                default: return new Cockpit { seatY = 0.93f, headZ = -0.38f, roofY = 1.4f, roofRear = -0.72f, cabinRear = -1.2f };
            }
        }

        /// <summary>
        /// CABINA camera position in the body's space: the classic onboard shot from the roll cage between the
        /// seats, with the driver, the co-driver, the wheel and the road through the windscreen all in view.
        /// </summary>
        public static Vector3 CockpitCamera(GameObject car)
        {
            var body = car.transform.Find("Body");
            var mesh = body != null ? body.GetComponent<MeshFilter>()?.sharedMesh : null;
            string model = null;
            if (mesh != null)
                foreach (var e in Cars)
                    if (mesh.name.StartsWith(ModelMeshName(e.model))) { model = e.model; break; } // dented copies keep the name
            var c = CockpitOf(model);
            // Between the two seats, half a metre behind the helmets, just under the roof. Behind the roof the rear
            // window slopes down: go only as far back as still leaves the camera above the helmets' centres.
            float slope = (c.roofY - c.seatY) / Mathf.Max(0.05f, c.roofRear - c.cabinRear);
            float lowest = c.seatY + 0.27f, highest = c.roofY - 0.09f;
            float z = Mathf.Max(c.headZ - 0.5f, c.roofRear - (c.roofY - 0.07f - lowest) / slope);
            float y = z >= c.roofRear ? highest : Mathf.Min(highest, c.roofY - (c.roofRear - z) * slope - 0.07f);
            return new Vector3(0f, y, z);
        }

        public static readonly Entry[] Cars =
        {
            new Entry
            {
                livery = "RivalCar_Azure", model = ModelPleyades, name = "PLÉYADES WRX  #3",
                description = "AÑOS 90. EQUILIBRADO Y FÁCIL DE LLEVAR EN TODAS LAS SUPERFICIES.",
                speed = 3, acceleration = 3, grip = 4,
                modify = t => { }
            },
            new Entry
            {
                livery = "RivalCar_Crimson", model = ModelLanza, name = "LANZA EVO  #11",
                description = "EL MÁS RÁPIDO EN RECTA, PERO PATINA MÁS EN LAS CURVAS.",
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
                livery = "PlayerCar", model = ModelItalica, name = "ITÁLICA INTEGRAL  #7",
                description = "AÑOS 80. MUCHO AGARRE Y GIRO RÁPIDO. MENOS PUNTA.",
                speed = 2, acceleration = 3, grip = 5,
                repaint = true, paint = new Color(0.92f, 0.92f, 0.9f), accent = new Color(0.8f, 0.08f, 0.08f), number = "7",
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
                livery = "PlayerCar", model = ModelEscolta, name = "ESCOLTA MK1  #1",
                description = "AÑOS 70, TRACCIÓN TRASERA. DERRAPA CON FACILIDAD: PARA EXPERTOS.",
                speed = 3, acceleration = 4, grip = 2,
                repaint = true, paint = new Color(0.93f, 0.9f, 0.82f), accent = new Color(0.1f, 0.28f, 0.7f), number = "1",
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
                livery = "RivalCar_Crimson", model = ModelLeon, name = "LEÓN T16  #9",
                description = "GRUPO B, MOTOR CENTRAL. MUCHÍSIMA POTENCIA: MUY DIFÍCIL DE DOMAR.",
                speed = 5, acceleration = 5, grip = 2,
                repaint = true, paint = new Color(0.99f, 0.99f, 0.99f), accent = new Color(1f, 0.78f, 0.08f), number = "9",
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
            ApplyBodies(race, entry);

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

        /// <summary>
        /// Body shapes: the player's car gets the chosen model; each rival the model of the car its livery
        /// belongs to (the first catalogue entry wearing it without a repaint).
        /// </summary>
        private static void ApplyBodies(RaceManager race, Entry chosen)
        {
            foreach (var p in race.Participants)
            {
                string model = p == race.Player ? chosen.model : ModelForLivery(LiveryOf(p.gameObject));
                var mesh = ModelMesh(model);
                var body = p.transform.Find("Body");
                var filter = body != null ? body.GetComponent<MeshFilter>() : null;
                if (mesh != null && filter != null && filter.sharedMesh != mesh) filter.sharedMesh = mesh;
                SetRims(p, model);
                HideNumbersBehindBody(p.gameObject);
            }
        }

        private static Material numberMaterial;

        /// <summary>
        /// The door numbers used the font's own material, which draws on top of everything: the far door's number
        /// showed through the car (and inside the cabin). Sprites/Default is depth-tested and always in the build.
        /// </summary>
        public static void HideNumbersBehindBody(GameObject car)
        {
            foreach (var text in car.GetComponentsInChildren<TextMesh>(true))
            {
                var r = text.GetComponent<MeshRenderer>();
                if (r == null || text.font == null) continue;
                if (numberMaterial == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    if (shader == null) return;
                    numberMaterial = new Material(shader) { name = "CarNumber", mainTexture = text.font.material.mainTexture };
                }
                if (r.sharedMaterial != numberMaterial) r.sharedMaterial = numberMaterial;
            }
        }

        // Wheel colour of the cars known for it (gold, white or black rims); the rest keep the silver ones.
        private static Color? RimColour(string model) =>
            model == ModelPleyades ? new Color(0.78f, 0.6f, 0.2f) :
            model == ModelLanza ? new Color(0.88f, 0.88f, 0.86f) :
            model == ModelEscolta ? new Color(0.12f, 0.12f, 0.13f) : (Color?)null;

        private const string RimMaterial = "Car_Rim";
        private static readonly System.Collections.Generic.Dictionary<string, Material> rimMaterials =
            new System.Collections.Generic.Dictionary<string, Material>();
        private static Material silverRim;

        private static void SetRims(RaceParticipant car, string model)
        {
            foreach (var r in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].name != RimMaterial) continue;
                    if (silverRim == null && !rimMaterials.ContainsValue(mats[i])) silverRim = mats[i];
                    var colour = RimColour(model);
                    Material wanted = silverRim;
                    if (colour != null && silverRim != null && (!rimMaterials.TryGetValue(model, out wanted) || wanted == null))
                    {
                        wanted = new Material(silverRim) { name = RimMaterial }; // same name: recognised on the next swap
                        wanted.SetColor("_BaseColor", colour.Value);
                        rimMaterials[model] = wanted;
                    }
                    if (wanted != null && mats[i] != wanted) { mats[i] = wanted; r.sharedMaterials = mats; }
                }
            }
        }

        private static string ModelForLivery(string livery)
        {
            foreach (var e in Cars) if (!e.repaint && e.livery == livery) return e.model;
            foreach (var e in Cars) if (e.livery == livery) return e.model;
            return null;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Mesh> models =
            new System.Collections.Generic.Dictionary<string, Mesh>();

        public static Mesh ModelMesh(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            if (!models.TryGetValue(model, out var mesh) || mesh == null)
                models[model] = mesh = Resources.Load<Mesh>($"{ModelFolder}/{ModelMeshName(model)}");
            return mesh;
        }

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
