using Rally.Systems;
using UnityEngine;

namespace Rally.Track
{
    /// <summary>Trigger volume across the road. Participants must cross checkpoints in order.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private int index;
        [SerializeField] private float distanceAlongTrack;
        [SerializeField] private bool isFinish;

        public int Index => index;
        public float DistanceAlongTrack => distanceAlongTrack;
        public bool IsFinish => isFinish;

        public void Configure(int checkpointIndex, float distance, bool finish)
        {
            index = checkpointIndex;
            distanceAlongTrack = distance;
            isFinish = finish;
        }

        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null) return;
            var participant = body.GetComponent<RaceParticipant>();
            if (participant != null) participant.NotifyCheckpoint(this);
        }
    }
}
