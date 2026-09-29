using UnityEngine;

namespace Rally.Systems
{
    /// <summary>Runtime settings that must be in place before any car simulates.</summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Physics step. Vehicles need a finer step than Unity's default 0.02 s.")]
        [SerializeField] private float fixedTimestep = 0.01f;
#pragma warning disable 0414 // unused in web builds, where the browser drives the frame rate
        [SerializeField] private int targetFrameRate = 120;
#pragma warning restore 0414

        private void Awake()
        {
            Time.fixedDeltaTime = fixedTimestep;
            Time.maximumDeltaTime = 0.1f;
#if UNITY_WEBGL && !UNITY_EDITOR
            // On the web any value other than -1 swaps requestAnimationFrame for setTimeout and stutters.
            Application.targetFrameRate = -1;
#else
            Application.targetFrameRate = targetFrameRate;
#endif
            Physics.simulationMode = SimulationMode.FixedUpdate;
        }
    }
}
