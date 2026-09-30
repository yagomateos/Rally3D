using System;
using System.Collections.Generic;
using Rally.CameraSystem;
using UnityEngine;

namespace Rally.Systems
{
    /// <summary>Records one car's run (10 samples a second, from GO until the results appear) for the replay.</summary>
    public class ReplayRecorder : MonoBehaviour
    {
        public readonly List<GhostRun.Sample> Samples = new List<GhostRun.Sample>();
        private RaceManager race;
        private float next;

        private void Start() => race = RaceManager.Instance;

        private void Update()
        {
            if (race == null || ReplayDirector.Playing) return;
            bool running = race.CurrentState == RaceManager.State.Racing ||
                           (race.CurrentState == RaceManager.State.Finished && !race.ResultsShown);
            if (!running || race.StageTime < next) return;
            next = race.StageTime + GhostRun.SampleInterval;
            Samples.Add(new GhostRun.Sample { t = race.StageTime, position = transform.position, rotation = transform.rotation });
        }
    }

    /// <summary>
    /// The replay after a stage: every car follows its recording while a TV-style director cuts between a chase
    /// camera, trackside cameras the player's car drives past and a helicopter view. The cars are frozen
    /// (kinematic) for it, which is fine: the stage is over. Skip with Confirm, a tap or the SALTAR button.
    /// </summary>
    public class ReplayDirector : MonoBehaviour
    {
        public static bool Playing { get; private set; }
        /// <summary>Frame the last replay ended on (its skip press must not also restart the stage).</summary>
        public static int EndedFrame { get; private set; } = -10;

        private enum Shot { Chase, Trackside, Helicopter }

        private RaceManager race;
        private readonly List<(Transform car, List<GhostRun.Sample> run)> cars = new List<(Transform, List<GhostRun.Sample>)>();
        private Transform player;
        private Camera cam;
        private RallyCamera chase;
        private Action onEnd;
        private float time, endTime, shotTime;
        private Shot shot;
        private Vector3 tripod;
        private float tripodDistance;
        private int shotIndex;

        public float Time01 => endTime > 0f ? time / endTime : 0f;

        public static ReplayDirector Play(RaceManager race, Action onEnd)
        {
            if (race == null || race.Player == null) return null;
            var playerRecorder = race.Player.GetComponent<ReplayRecorder>();
            if (playerRecorder == null || playerRecorder.Samples.Count < 10) return null;

            var director = new GameObject("ReplayDirector").AddComponent<ReplayDirector>();
            director.race = race;
            director.onEnd = onEnd;
            director.player = race.Player.transform;
            director.endTime = playerRecorder.Samples[playerRecorder.Samples.Count - 1].t;
            foreach (var p in race.Participants)
            {
                var rec = p.GetComponent<ReplayRecorder>();
                if (rec == null || rec.Samples.Count < 2) continue;
                director.cars.Add((p.transform, rec.Samples));
                // Frozen for the replay: the recording moves them, and no race logic (resets, countdowns) interferes.
                if (p.Car != null && p.Car.Body != null)
                {
                    p.Car.ControlEnabled = false;
                    p.Car.enabled = false;
                    p.Car.Body.isKinematic = true;
                    p.Car.Body.interpolation = RigidbodyInterpolation.None;
                }
                p.enabled = false;
            }
            director.cam = Camera.main;
            director.chase = director.cam != null ? director.cam.GetComponent<RallyCamera>() : null;
            if (director.chase != null) director.chase.enabled = false;
            Playing = true;
            director.StartShot(Shot.Chase);
            return director;
        }

        public void Stop()
        {
            if (!Playing) return;
            Playing = false;
            EndedFrame = UnityEngine.Time.frameCount;
            if (chase != null)
            {
                chase.enabled = true;
                chase.SnapToTarget();
            }
            onEnd?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Playing) Playing = false;
        }

        private void Update()
        {
            var input = RallyInput.Instance;
            if (time > 0.3f && input != null && (input.Confirm.WasPressedThisFrame() || input.Pause.WasPressedThisFrame())) { Stop(); return; }

            time += UnityEngine.Time.unscaledDeltaTime;
            if (time >= endTime) { Stop(); return; }
            foreach (var (car, run) in cars)
                if (GhostRun.Evaluate(run, time, out var pos, out var rot)) car.SetPositionAndRotation(pos, rot);
        }

        private void LateUpdate()
        {
            if (cam == null || !Playing) return;
            shotTime += UnityEngine.Time.unscaledDeltaTime;
            Vector3 target = player.position + Vector3.up * 0.8f;

            switch (shot)
            {
                case Shot.Chase:
                {
                    Vector3 back = player.position - player.forward * 7f + Vector3.up * 2.4f;
                    cam.transform.position = Vector3.Lerp(cam.transform.position, back, 1f - Mathf.Exp(-6f * UnityEngine.Time.unscaledDeltaTime));
                    cam.transform.rotation = Quaternion.LookRotation(target + player.forward * 6f - cam.transform.position);
                    if (shotTime > 5f) NextShot();
                    break;
                }
                case Shot.Trackside:
                {
                    cam.transform.position = tripod;
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                        Quaternion.LookRotation(target - tripod), 1f - Mathf.Exp(-10f * UnityEngine.Time.unscaledDeltaTime));
                    // Cut once the car has gone well past the camera.
                    var p = race.Path;
                    float carDistance = p.Project(player.position, tripodDistance, 200f);
                    if (carDistance > tripodDistance + 25f || shotTime > 8f) NextShot();
                    break;
                }
                case Shot.Helicopter:
                {
                    Vector3 above = player.position - player.forward * 18f + Vector3.up * 22f;
                    cam.transform.position = Vector3.Lerp(cam.transform.position, above, 1f - Mathf.Exp(-3f * UnityEngine.Time.unscaledDeltaTime));
                    cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position);
                    if (shotTime > 5f) NextShot();
                    break;
                }
            }
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, shot == Shot.Trackside ? 40f : 60f, 1f - Mathf.Exp(-4f * UnityEngine.Time.unscaledDeltaTime));
        }

        private void NextShot()
        {
            shotIndex++;
            Shot[] order = { Shot.Chase, Shot.Trackside, Shot.Helicopter, Shot.Trackside };
            StartShot(order[shotIndex % order.Length]);
        }

        private void StartShot(Shot next)
        {
            shot = next;
            shotTime = 0f;
            if (next == Shot.Trackside)
            {
                // A camera on a tripod beside the road, a little ahead of the player's car.
                var p = race.Path;
                float d = p.Project(player.position, race.Player.Distance, 200f) + 45f;
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                tripodDistance = d;
                tripod = p.PositionAt(d) + p.RightAt(d) * side * (p.WidthAt(d) * 0.5f + 7f) + Vector3.up * 2.2f;
                if (Physics.Raycast(tripod + Vector3.up * 50f, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                    tripod.y = Mathf.Max(tripod.y, hit.point.y + 1.8f);
                cam.transform.position = tripod;
                cam.transform.LookAt(player.position);
            }
            else if (next == Shot.Chase)
            {
                cam.transform.position = player.position - player.forward * 7f + Vector3.up * 2.4f;
            }
        }
    }
}
