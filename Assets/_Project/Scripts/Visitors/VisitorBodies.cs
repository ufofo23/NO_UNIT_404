using System.Collections.Generic;
using UnityEngine;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Visitors
{
    /// <summary>
    /// Puts a body in the building for everybody the door let in (v3.0 38).
    ///
    /// <see cref="ActiveVisitorService"/> simulates a granted visitor completely - which zone
    /// they are in, when they wander off their route, whether anything saw them - and drew
    /// none of it. The consequence of admitting somebody was a card on a tracking board, so
    /// the twelve cameras the Access system is built around could not answer the one question
    /// it asks: where did they actually go? A visitor was a fact about a UI, not a person in a
    /// corridor.
    ///
    /// This is presentation only. It never moves the simulation and never tells the service
    /// anything - it reads CurrentZone and draws the body there, so the visitor appears on a
    /// camera exactly when the simulation already considers them observable, and disappears
    /// from the world the moment their floor streams out. That keeps 38.4's gap intact: the
    /// board still only knows where they were last seen, and seeing them is still something
    /// the player has to do, either by watching the right feed or by standing in the room.
    ///
    /// Runs on every machine rather than only the host, because on a client the mirrored board
    /// is what that caretaker's cameras should be showing (v3.0 46.1).
    /// </summary>
    public sealed class VisitorBodies : MonoBehaviour
    {
        /// <summary>Metres per game-scaled second. Unhurried, and slower than the player.</summary>
        public const float WalkSpeed = 1.65f;

        /// <summary>How far off the walls a visitor is willing to stand.</summary>
        const float WallMargin = 0.9f;

        /// <summary>Real seconds spent standing still on arriving somewhere.</summary>
        const float PauseSeconds = 2.5f;

        sealed class Body
        {
            public GameObject Root;
            public string ZoneId;
            public Vector3 Target;
            public float PauseUntil;
            public System.Random Random;
            public FirstGuestMotion Motion;
        }

        readonly Dictionary<string, Body> _bodies = new Dictionary<string, Body>(4);
        readonly List<string> _stale = new List<string>(4);

        /// <summary>Whoever is currently standing at the front door, waiting to be decided about.</summary>
        GameObject _caller;
        string _callerId;
        FirstGuestMotion _callerMotion;

        public int Count { get { return _bodies.Count; } }

        public static VisitorBodies Create(Transform parent)
        {
            var go = new GameObject("VisitorBodies");
            go.transform.SetParent(parent, false);
            return go.AddComponent<VisitorBodies>();
        }

        /// <summary>
        /// Driven by GameLoop rather than Update, so visitors stop dead in the evidence board
        /// and crawl in a conversation like everything else the clock owns (GDD 6.2).
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            SyncCaller();
            float scale = ServiceHub.Clock != null ? ServiceHub.Clock.TimeScale : 1f;
            float scaledSeconds = deltaSeconds * scale;
            if (_callerMotion != null) _callerMotion.Tick(scaledSeconds, 0f);

            var tracked = ServiceHub.ActiveVisitors != null ? ServiceHub.ActiveVisitors.All : null;
            if (tracked == null || tracked.Count == 0)
            {
                ClearAdmitted();
                return;
            }

            float walked = deltaSeconds * scale * WalkSpeed;

            for (int i = 0; i < tracked.Count; i++) Sync(tracked[i], walked, scaledSeconds);

            DropWhoeverLeft(tracked);
        }

        /// <summary>
        /// The person at the front door (v3.0 38, GDD 13.7).
        ///
        /// Until the entrance existed there was nowhere to put them and nothing to see through,
        /// so a caller was a panel on a screen: the walk down to the glass that
        /// <c>ReadChannel.Glass</c> charges for bought a view of an empty room. Now they are
        /// standing on the doorstep for as long as they are at the door, which is what makes
        /// the walk worth the desk it costs.
        ///
        /// Nothing is read off this body. Which tells are visible is still
        /// <c>DoorReadService.IsVisible</c>'s answer, and it still only asks whether somebody
        /// is in the lobby.
        /// </summary>
        void SyncCaller()
        {
            var active = ServiceHub.Interphone != null ? ServiceHub.Interphone.Active : null;
            var visitorId = active != null ? active.visitorId : null;

            // Somebody who has been let in stops being a caller and becomes a tracked visitor,
            // whose body is built from their zone like everybody else's.
            bool admitted = visitorId != null && ServiceHub.ActiveVisitors != null &&
                            ServiceHub.ActiveVisitors.Find(visitorId) != null;

            var doorstep = WorldBuilder.LobbyDoorstep;
            bool wanted = visitorId != null && !admitted && doorstep != null;

            if (!wanted || visitorId != _callerId)
            {
                if (_caller != null) Destroy(_caller);
                _caller = null;
                _callerId = null;
                _callerMotion = null;
            }

            if (!wanted || _caller != null) return;

            _caller = VisitorAppearance.Build(doorstep, visitorId, "CALLER_" + visitorId);
            _callerMotion = _caller.GetComponent<FirstGuestMotion>();

            // Facing the glass, which is the way somebody who has just pressed the bell stands.
            _caller.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _callerId = visitorId;
        }

        /// <summary>
        /// One visitor's body, caught up with where the simulation says they are.
        ///
        /// A zone with no root is a floor that has not streamed in. The body goes away and the
        /// visitor keeps walking their route unseen, which is the correct outcome rather than a
        /// missing case: nobody could have been looking at that corridor anyway.
        /// </summary>
        void Sync(ActiveVisitorService.Tracked tracked, float walked, float scaledSeconds)
        {
            var visitorId = tracked.VisitorId;
            if (string.IsNullOrEmpty(visitorId)) return;

            Body body;
            if (!_bodies.TryGetValue(visitorId, out body))
            {
                body = new Body { Random = new System.Random(visitorId.GetHashCode()) };
                _bodies[visitorId] = body;
            }

            var zoneRoot = ZoneRegistry.Find(tracked.CurrentZone);
            if (zoneRoot == null)
            {
                Release(body);
                return;
            }

            // Root can also be null because the zone unloaded and took the body with it, so
            // this is a rebuild rather than a reparent - a destroyed transform cannot be moved.
            if (body.Root == null || body.ZoneId != tracked.CurrentZone)
            {
                Release(body);

                body.Root = VisitorAppearance.Build(zoneRoot, visitorId, "VISITOR_" + visitorId);
                body.Motion = body.Root.GetComponent<FirstGuestMotion>();
                body.ZoneId = tracked.CurrentZone;

                // Somebody who has just been let in came through the front door, so that is
                // where they start. Anywhere else in the building they are simply found where
                // the simulation says they are.
                body.Root.transform.localPosition = EntryPointFor(tracked, body.Random);
                body.Target = SpotIn(tracked.CurrentZone, body.Random);
                body.PauseUntil = 0f;
            }

            var before = body.Root.transform.position;
            Walk(body, walked);
            if (body.Motion != null)
                body.Motion.Tick(scaledSeconds, scaledSeconds > 0f
                    ? Vector3.Distance(before, body.Root.transform.position) / scaledSeconds : 0f);
        }

        /// <summary>
        /// Wandering inside the zone, not walking between them.
        ///
        /// Crossing the building is the service's job and happens on game seconds; this only
        /// stops a visitor standing frozen in the middle of a corridor for the ninety seconds
        /// a leg takes, which reads as a broken game rather than as a person.
        /// </summary>
        void Walk(Body body, float walked)
        {
            if (body.Root == null || walked <= 0f) return;
            if (Time.unscaledTime < body.PauseUntil) return;

            var here = body.Root.transform.localPosition;

            if (Vector3.Distance(here, body.Target) <= 0.05f)
            {
                body.Target = SpotIn(body.ZoneId, body.Random);
                body.PauseUntil = Time.unscaledTime + PauseSeconds;
                return;
            }

            body.Root.transform.localPosition = Vector3.MoveTowards(here, body.Target, walked);

            var direction = body.Target - here;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                body.Root.transform.localRotation =
                    Quaternion.Slerp(body.Root.transform.localRotation,
                                     Quaternion.LookRotation(direction, Vector3.up),
                                     6f * Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Where a body first appears in a zone.
        ///
        /// The lobby is the one zone with a door to the street in it, and a visitor whose
        /// first leg is the lobby has just walked through it - so they come from the vestibule
        /// rather than materialising somewhere in the middle of the room.
        /// </summary>
        static Vector3 EntryPointFor(ActiveVisitorService.Tracked tracked, System.Random random)
        {
            bool justArrived = tracked.CurrentZone == ZoneIds.Lobby &&
                               ActiveVisitorService.DwellSeconds(tracked) < ActiveVisitorService.SecondsPerLeg;

            var vestibule = WorldBuilder.LobbyVestibule;
            if (justArrived && vestibule != null) return vestibule.localPosition;

            return SpotIn(tracked.CurrentZone, random);
        }

        /// <summary>A spot on this zone's floor, clear of its walls. Deterministic per visitor.</summary>
        static Vector3 SpotIn(string zoneId, System.Random random)
        {
            var size = WorldBuilder.SizeOf(zoneId);
            float halfX = Mathf.Max(0.4f, size.x * 0.5f - WallMargin);
            float halfZ = Mathf.Max(0.4f, size.y * 0.5f - WallMargin);

            var spot = new Vector3((float)(random.NextDouble() * 2.0 - 1.0) * halfX,
                                   0.02f,
                                   (float)(random.NextDouble() * 2.0 - 1.0) * halfZ);

            // The far end of the lobby is the entrance porch, and a body with no collider
            // wandering into it stands inside the glazing. Somebody who has been let in has
            // no business back at the door anyway - they came in through it.
            if (zoneId == ZoneIds.Lobby && spot.z > LobbyWanderLimitZ) spot.z = LobbyWanderLimitZ;

            return spot;
        }

        /// <summary>Where the lobby stops being a room and starts being the way out.</summary>
        const float LobbyWanderLimitZ = 1.9f;

        void DropWhoeverLeft(IReadOnlyList<ActiveVisitorService.Tracked> tracked)
        {
            _stale.Clear();

            foreach (var pair in _bodies)
            {
                bool live = false;
                for (int i = 0; i < tracked.Count; i++)
                {
                    if (tracked[i].VisitorId != pair.Key) continue;
                    live = true;
                    break;
                }

                if (!live) _stale.Add(pair.Key);
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                Body body;
                if (_bodies.TryGetValue(_stale[i], out body)) Release(body);
                _bodies.Remove(_stale[i]);
            }
        }

        void Release(Body body)
        {
            if (body.Root != null) Destroy(body.Root);
            body.Root = null;
            body.ZoneId = null;
            body.Motion = null;
        }

        /// <summary>Everybody goes home at 06:00.</summary>
        public void Clear()
        {
            ClearAdmitted();

            if (_caller != null) Destroy(_caller);
            _caller = null;
            _callerId = null;
            _callerMotion = null;
        }

        void ClearAdmitted()
        {
            if (_bodies.Count == 0) return;

            foreach (var pair in _bodies) Release(pair.Value);
            _bodies.Clear();
        }

        void OnDestroy() { Clear(); }
    }
}
