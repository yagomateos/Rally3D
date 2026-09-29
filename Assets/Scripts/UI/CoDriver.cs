using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using Rally.Systems;
using Rally.Track;
using UnityEngine;
using UnityEngine.UI;

namespace Rally.UI
{
    /// <summary>
    /// Co-driver: calls the pace notes (<see cref="Pacenotes"/>) a couple of seconds before each corner or jump,
    /// on screen (top centre, with chevrons towards the corner) and, in the browser, out loud in Spanish.
    /// Two notes close together are called as one ("IZQUIERDA 3 Y DERECHA 4").
    /// </summary>
    public class CoDriver : MonoBehaviour
    {
        public enum Mode { VozYTexto = 0, SoloTexto = 1, Desactivado = 2 }

        public static Mode Setting
        {
            get => (Mode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(ModeKey, (int)value); PlayerPrefs.Save(); }
        }
        public static readonly string[] ModeNames = { "VOZ Y TEXTO", "SOLO TEXTO", "DESACTIVADO" };
        private const string ModeKey = "Rally.CoDriver";

        private const float LeadSeconds = 2.6f;   // call this long before reaching the note
        private const float MinLead = 45f, MaxLead = 160f;
        private const float LinkGap = 40f;        // notes closer than this are called together

        private RaceManager race;
        private List<Pacenotes.Note> notes;
        private int next;
        private float shownUntil;
        private CanvasGroup panel;

        /// <summary>Last call shown (for tests and debugging).</summary>
        public string LastCall { get; private set; } = "";
        public float PanelAlpha => panel != null ? panel.alpha : 0f;
        public int NoteCount => notes != null ? notes.Count : 0;
        private Text mainText, modifierText;
        private RectTransform[] chevronsLeft, chevronsRight;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RallySpeech_Speak(string text, float rate);
        [DllImport("__Internal")] private static extern void RallySpeech_Stop();
#endif

        public void Build(RaceManager raceManager, RectTransform hudRoot)
        {
            race = raceManager;
            notes = Pacenotes.Generate(race.Path, race.Stage);

            var box = UIFactory.Panel("Pacenote", hudRoot, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(560f, 96f), UIFactory.PanelDark);
            panel = box.gameObject.AddComponent<CanvasGroup>();
            panel.alpha = 0f;
            panel.interactable = panel.blocksRaycasts = false;
            UIFactory.Panel("Top", box.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(560f, 4f), UIFactory.Accent);
            mainText = UIFactory.Label("Call", box.transform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(420f, 52f),
                "", 42, TextAnchor.MiddleCenter, UIFactory.TextMain);
            UIFactory.AddShadow(mainText, 2f);
            modifierText = UIFactory.Label("Modifiers", box.transform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(420f, 28f),
                "", 22, TextAnchor.MiddleCenter, UIFactory.Accent);
            chevronsLeft = Chevrons(box.transform, -1);
            chevronsRight = Chevrons(box.transform, 1);
        }

        // Up to three "<" (or ">") marks on the corner's side: more marks = tighter corner.
        private static RectTransform[] Chevrons(Transform parent, int side)
        {
            var result = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                var chevron = UIFactory.Anchored("Chevron", parent, new Vector2(side < 0 ? 0f : 1f, 0.5f),
                    new Vector2(side * -(34f + i * 26f), 0f), new Vector2(24f, 48f), new Vector2(0.5f, 0.5f));
                for (int arm = 0; arm < 2; arm++)
                {
                    var bar = UIFactory.Panel("Arm", chevron, new Vector2(0.5f, 0.5f), new Vector2(0f, arm == 0 ? 10f : -10f),
                        new Vector2(30f, 7f), UIFactory.Accent, new Vector2(0.5f, 0.5f));
                    // "<": top arm rises to the right, bottom arm falls to the right; mirrored for ">".
                    float a = (arm == 0 ? 40f : -40f) * (side < 0 ? 1f : -1f);
                    bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, a);
                }
                result[i] = chevron;
            }
            return result;
        }

        private void Update()
        {
            if (race == null || notes == null || Setting == Mode.Desactivado) { if (panel != null) panel.alpha = 0f; return; }
            var player = race.Player;
            if (player == null || race.CurrentState != RaceManager.State.Racing || player.HasFinished)
            {
                panel.alpha = Mathf.MoveTowards(panel.alpha, 0f, Time.unscaledDeltaTime * 4f);
                return;
            }

            float here = player.Distance;
            while (next < notes.Count && notes[next].distance < here - 5f) next++; // passed (or skipped by a reset)

            float lead = Mathf.Clamp(player.Car.SpeedKph / 3.6f * LeadSeconds, MinLead, MaxLead);
            if (next < notes.Count && notes[next].distance - here <= lead)
            {
                var note = notes[next];
                string text = note.Text, spoken = note.Spoken, modifiers = note.Modifiers;
                next++;
                // Link a second note that follows closely: "IZQUIERDA 3 Y DERECHA 4".
                if (next < notes.Count && notes[next].distance - note.distance < LinkGap)
                {
                    text += "  Y  " + notes[next].Text;
                    spoken += " y " + notes[next].Spoken;
                    next++;
                }
                Show(note, text, modifiers);
                Speak(spoken);
            }

            if (Time.time > shownUntil) panel.alpha = Mathf.MoveTowards(panel.alpha, 0f, Time.deltaTime * 3f);
        }

        private void Show(Pacenotes.Note note, string text, string modifiers)
        {
            mainText.text = text;
            LastCall = text;
            mainText.fontSize = text.Length > 18 ? 30 : 42;
            modifierText.text = modifiers;
            int marks = note.jump ? 0 : note.severity <= 1 ? 3 : note.severity <= 3 ? 2 : 1;
            for (int i = 0; i < 3; i++)
            {
                chevronsLeft[i].gameObject.SetActive(note.direction < 0 && i < marks);
                chevronsRight[i].gameObject.SetActive(note.direction > 0 && i < marks);
            }
            panel.alpha = 1f;
            shownUntil = Time.time + 2.4f;
        }

        private static void Speak(string text)
        {
            if (Setting != Mode.VozYTexto) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            RallySpeech_Speak(text, 1.15f);
#endif
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            RallySpeech_Stop();
#endif
        }
    }
}
