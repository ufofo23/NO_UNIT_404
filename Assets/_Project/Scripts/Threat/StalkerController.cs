using System.Collections.Generic;
using UnityEngine;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Threat
{
    public enum StalkerState { Patrol = 0, Suspicious = 1, Investigate = 2, Closing = 3 }

    /// <summary>
    /// The night-5 patrol (GDD 9.6 / 15.2).
    ///
    /// The rules the design fixes and this class implements literally:
    ///  - it never kills; being caught costs evidence and time
    ///  - it moves at 85-95% of the player's sprint, so running away always works
    ///  - awareness builds and decays, so a glimpse is survivable
    ///  - crouching and hiding spots actually change the numbers
    /// </summary>
    public sealed class StalkerController : MonoBehaviour
    {
        public const float PatrolSpeed = 1.6f;
        public const float VisionRange = 12f;
        public const float VisionHalfAngle = 55f;
        public const float CatchDistance = 1.6f;

        /// <summary>
        /// GDD 15.2: 85-95% of the player's sprint, and never faster. The exact fraction is
        /// the difficulty option from GDD 24.2, so story mode really is escapable.
        /// </summary>
        public static float ChaseSpeed
        {
            get { return PlayerController.SprintSpeed * DifficultyProfile.ChaseSpeedFactor; }
        }

        /// <summary>
        /// How long he keeps looking once he has lost sight of the caretaker.
        ///
        /// Twelve seconds normally, and longer for every uncancelled key. Spec 16 T18 says a
        /// reissued card with the old one still live makes the night-5 intrusion harder to
        /// deal with, and this is the honest form of that: he is not faster - GDD 15.2 promises
        /// that running away always works and nothing here may break it - he simply is not on
        /// the wrong side of a door any more, so he stays in the room longer and hiding costs
        /// more than it used to.
        /// </summary>
        public const float BaseInvestigateSeconds = 12f;
        public const float InvestigateSecondsPerKey = 3f;
        public const int MaxUncancelledKeys = 3;

        public static float InvestigateSeconds
        {
            get
            {
                var state = ServiceHub.State;
                if (state == null) return BaseInvestigateSeconds;

                int keys = Mathf.Min(state.GetStat(StatIds.ChairmanAccessEase), MaxUncancelledKeys);
                return BaseInvestigateSeconds + keys * InvestigateSecondsPerKey;
            }
        }

        readonly List<PatrolWaypoint> _waypoints = new List<PatrolWaypoint>();

        Transform _player;
        int _waypointIndex;
        Vector3 _lastKnownPosition;
        float _investigateTimer;

        /// <summary>
        /// Counts down while he stands at a waypoint that has a dwell. He is not idle here -
        /// he is doing the thing the route says he came to do, and the sightline check keeps
        /// running, so standing still is when he is most likely to notice a moving player.
        /// </summary>
        float _dwellTimer;

        public StalkerState State { get; private set; }
        public float Awareness { get; private set; }
        public string ZoneId { get; private set; }

        public void Setup(string zoneId, Transform player, IEnumerable<PatrolWaypoint> waypoints)
        {
            ZoneId = zoneId;
            _player = player;

            _waypoints.Clear();
            if (waypoints != null) foreach (var point in waypoints) _waypoints.Add(point);

            if (_waypoints.Count > 0) transform.position = _waypoints[0].Position;
            _waypointIndex = 0;
            _dwellTimer = 0f;
            State = StalkerState.Patrol;
        }

        /// <summary>True while he is stopped at a waypoint rather than walking between two.</summary>
        public bool IsDwelling { get { return _dwellTimer > 0f; } }

        void Update()
        {
            if (_player == null || ServiceHub.Threat == null || !ServiceHub.Threat.IsActive) return;

            float dt = Time.deltaTime;

            bool visible = CanSeePlayer();
            UpdateAwareness(visible, dt);
            UpdateState(visible);
            Move(dt);

            if (State == StalkerState.Closing &&
                Vector3.Distance(transform.position, _player.position) <= CatchDistance)
            {
                ServiceHub.Threat.ReportCaught(this);
            }
        }

        bool CanSeePlayer()
        {
            // Hiding is absolute: a spot that only sometimes works would be unreadable.
            if (ServiceHub.Threat.IsHidden) return false;

            // Only a threat in the same zone matters; zones are far apart in world space.
            if (ServiceHub.Player.CurrentZone != ZoneId) return false;

            var toPlayer = _player.position - transform.position;
            float distance = toPlayer.magnitude;
            if (distance > VisionRange) return false;

            var flat = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;
            if (Vector3.Angle(transform.forward, flat) > VisionHalfAngle) return false;

            // Line of sight: anything solid in the way breaks it.
            var origin = transform.position + Vector3.up * 1.5f;
            var target = _player.position + Vector3.up * 1.0f;
            RaycastHit hit;
            if (Physics.Linecast(origin, target, out hit, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.GetComponentInParent<PlayerController>() == null) return false;
            }

            return true;
        }

        void UpdateAwareness(bool visible, float dt)
        {
            if (visible)
            {
                float distance = Vector3.Distance(transform.position, _player.position);
                float proximity = Mathf.Clamp01(1f - distance / VisionRange);

                float gain = 0.35f + proximity * 0.9f;
                if (ServiceHub.Threat.PlayerCrouching) gain *= 0.55f;

                Awareness = Mathf.Clamp01(Awareness + gain * dt);
                _lastKnownPosition = _player.position;
            }
            else
            {
                Awareness = Mathf.Clamp01(Awareness - 0.35f * dt);
            }
        }

        void UpdateState(bool visible)
        {
            var previous = State;

            if (Awareness >= 1f) State = StalkerState.Closing;
            else if (Awareness >= 0.55f) State = StalkerState.Investigate;
            else if (Awareness > 0.05f) State = StalkerState.Suspicious;
            else State = StalkerState.Patrol;

            if (State == StalkerState.Investigate && previous != StalkerState.Investigate)
                _investigateTimer = InvestigateSeconds;

            if (State != StalkerState.Closing && !visible && _investigateTimer > 0f)
                _investigateTimer -= Time.deltaTime;
        }

        void Move(float dt)
        {
            Vector3 destination;
            float speed;

            switch (State)
            {
                case StalkerState.Closing:
                    destination = _player.position;
                    speed = ChaseSpeed;
                    break;

                case StalkerState.Investigate:
                    destination = _lastKnownPosition;
                    speed = PatrolSpeed * 1.8f;
                    break;

                case StalkerState.Suspicious:
                    destination = transform.position;   // stops and looks
                    speed = 0f;
                    break;

                default:
                    destination = NextWaypoint();
                    speed = PatrolSpeed;
                    break;
            }

            var offset = destination - transform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude > 0.04f)
            {
                var direction = offset.normalized;
                if (speed > 0f) transform.position += direction * speed * dt;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                                                      Quaternion.LookRotation(direction),
                                                      dt * 4f);
            }
            else if (State == StalkerState.Patrol)
            {
                AdvanceWaypoint(dt);
            }
        }

        Vector3 NextWaypoint()
        {
            return _waypoints.Count == 0 ? transform.position : _waypoints[_waypointIndex].Position;
        }

        /// <summary>
        /// Arrival. A waypoint with a dwell holds him there until it runs out; the rest are
        /// corners and he turns straight through them.
        /// </summary>
        void AdvanceWaypoint(float dt)
        {
            if (_waypoints.Count == 0) return;

            if (_dwellTimer > 0f)
            {
                _dwellTimer -= dt;
                if (_dwellTimer > 0f) return;
            }
            else if (_waypoints[_waypointIndex].DwellSeconds > 0f)
            {
                _dwellTimer = _waypoints[_waypointIndex].DwellSeconds;
                return;
            }

            _dwellTimer = 0f;
            _waypointIndex = (_waypointIndex + 1) % _waypoints.Count;
        }

        public void ResetAfterCatch(Vector3 position)
        {
            Awareness = 0f;
            State = StalkerState.Patrol;
            _investigateTimer = 0f;
            _dwellTimer = 0f;
            transform.position = position;
        }
    }
}
