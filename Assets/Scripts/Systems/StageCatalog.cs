using UnityEngine;

namespace Rally.Systems
{
    /// <summary>The stages offered in the main menu (each one is its own scene in the build).</summary>
    public static class StageCatalog
    {
        public struct Entry
        {
            public string scene;       // scene name in the build settings
            public string number;      // "TRAMO 01"
            public string name;        // "PINAR DE VALDENIEBLA"
            public string description;

            /// <summary>Best time saved by <see cref="RaceManager"/> for this stage (0 = none yet).</summary>
            public float BestTime => PlayerPrefs.GetFloat($"BestTime_{number}_{name}", 0f);
        }

        public static readonly Entry[] Stages =
        {
            new Entry { scene = "Stage01", number = "TRAMO 01", name = "PINAR DE VALDENIEBLA", description = "TIERRA · GRAVA · BARRO · ASFALTO   ·   NUBLADO, MOJADO" },
            new Entry { scene = "Stage02", number = "TRAMO 02", name = "PUERTO DE PEÑA BLANCA", description = "NIEVE · HIELO · NIEVE BLANDA   ·   NEVANDO" },
            new Entry { scene = "Stage03", number = "TRAMO 03", name = "DUNAS DEL DESIERTO", description = "ARENA · PISTA DURA · ARENA BLANDA · ROCA   ·   DESPEJADO, CALIMA" },
            new Entry { scene = "Stage04", number = "TRAMO 04", name = "COSTERA DE ASFALTO", description = "ASFALTO · CURVAS RÁPIDAS · MÁS VELOCIDAD   ·   PUESTA DE SOL" },
        };

        /// <summary>Index of the stage whose scene is open (0 if unknown).</summary>
        public static int Current
        {
            get
            {
                string active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                for (int i = 0; i < Stages.Length; i++) if (Stages[i].scene == active) return i;
                return 0;
            }
        }
    }
}
