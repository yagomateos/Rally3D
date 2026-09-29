using Rally.Systems;
using UnityEngine;

namespace Rally.Audio
{
    /// <summary>Ambient bed plus race cues (countdown, checkpoints, finish).</summary>
    public class StageAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip ambience;
        [SerializeField] private float ambienceVolume = 0.35f;
        [SerializeField] private float cueVolume = 0.5f;

        private AudioSource ambienceSource;
        private AudioSource cues;
        private RaceManager race;

        private void Awake()
        {
            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.clip = ambience != null ? ambience : ProceduralAudio.Ambience();
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.volume = ambienceVolume;
            ambienceSource.Play();

            cues = gameObject.AddComponent<AudioSource>();
            cues.spatialBlend = 0f;
            cues.ignoreListenerPause = true;
        }

        private void Start()
        {
            race = RaceManager.Instance;
            if (race == null) return;
            race.CountdownTick += OnTick;
            race.Go += OnGo;
            race.CheckpointPassed += OnCheckpoint;
        }

        private void OnDestroy()
        {
            if (race == null) return;
            race.CountdownTick -= OnTick;
            race.Go -= OnGo;
            race.CheckpointPassed -= OnCheckpoint;
        }

        private void OnTick(int value) => cues.PlayOneShot(ProceduralAudio.Beep(880f, 0.18f), cueVolume);
        private void OnGo() => cues.PlayOneShot(ProceduralAudio.Beep(1320f, 0.45f), cueVolume);

        private void OnCheckpoint(RaceParticipant participant, Track.Checkpoint checkpoint, float time)
        {
            if (!participant.IsPlayer) return;
            cues.PlayOneShot(ProceduralAudio.Beep(checkpoint.IsFinish ? 1568f : 1175f, checkpoint.IsFinish ? 0.6f : 0.12f), cueVolume * 0.7f);
        }
    }
}
