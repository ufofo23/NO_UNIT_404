using System;
using System.Collections.Generic;
using NO404.Core;
using NO404.Residents;

namespace NO404.Visitors
{
    /// <summary>
    /// What a visitor does after the door opens (v3.0 33.2, 38, FINAL RULE B-02).
    ///
    /// The old door ended at the button. Somebody pressed 문 열기, the caller vanished, a
    /// number moved on a summary screen, and the most interesting thing about a stranger in a
    /// building at two in the morning - what they actually did once they were inside - was not
    /// in the game at all. B-02 makes that a bug: every P0 visitor event has to run at least
    /// two more steps after entry.
    ///
    /// So a granted visitor becomes an entity that walks a route, gets seen or not seen, goes
    /// somewhere it should not, and has to be found again. The caretaker's pass is a decision
    /// with a consequence they will have to go and stand in.
    ///
    /// Deliberately not a tracker. The HUD shows where somebody was last seen and how long ago
    /// (38.4), never where they are, because the whole tension of the system lives in the gap
    /// between those two things.
    /// </summary>
    public sealed class ActiveVisitorService
    {
        /// <summary>Game seconds a visitor spends crossing one zone of their route.</summary>
        public const int SecondsPerLeg = 90;

        /// <summary>Game seconds at a destination before they turn round and leave.</summary>
        public const int DwellAtDestinationSeconds = 240;

        /// <summary>
        /// After this long with nothing seeing them, the card stops claiming to know where they
        /// are (38.4).
        ///
        /// Three minutes, and the number is bounded on both sides by the schedule rather than
        /// picked for feel. It has to exceed one leg, or a visitor would read as lost every
        /// time they walked down a corridor; it has to fall short of a destination dwell, or a
        /// visitor standing still in a room with no camera would be re-observed before the card
        /// could ever age - which is what the first draft did, quietly making the whole
        /// "위치 불명" half of 38.4 a state the UI could draw and the simulation could not enter.
        /// </summary>
        public const int StaleAfterSeconds = 180;

        /// <summary>Escort tolerance, expressed in zones rather than metres (38.9).</summary>
        public const int EscortGraceSeconds = 30;

        public sealed class Tracked
        {
            public VisitorDefinition Definition;
            public AccessToken Token;
            public VisitorState State;
            public VisitorFlags Flags;

            /// <summary>Index into the definition's expected route.</summary>
            public int RouteStep;
            public int NextMoveSecond;

            public string CurrentZone;
            public string LastKnownZone;

            /// <summary>The last zone written into the access log, so standing and watching
            /// somebody does not fill the log with one line per frame.</summary>
            public string LastLoggedZone;
            public int LastKnownSecond;
            public int EnteredSecond;

            /// <summary>Zones they have been in that their pass did not cover.</summary>
            public readonly List<string> Violations = new List<string>();

            public int EscortBrokenSince;

            public string VisitorId { get { return Definition != null ? Definition.visitorId : null; } }
        }

        readonly List<Tracked> _tracked = new List<Tracked>();
        readonly Dictionary<string, string> _cameraByZone = new Dictionary<string, string>(StringComparer.Ordinal);

        int _tokenCounter;

        public IReadOnlyList<Tracked> All { get { return _tracked; } }
        public int Count { get { return _tracked.Count; } }

        /// <summary>Raised when a visitor is first observed somewhere their pass does not cover.</summary>
        public event Action<Tracked, string> OnViolation;

        /// <summary>Raised when a visitor leaves the building, one way or another.</summary>
        public event Action<Tracked> OnExited;

        // -----------------------------------------------------------------
        // admitting
        // -----------------------------------------------------------------

        /// <summary>
        /// Turn a grant into somebody who is now in the building.
        ///
        /// Reject and Hold produce no entity - nobody came in - which is the one case where
        /// the door really does end the event, and it is also the case the player is being
        /// taught not to reach for by reflex.
        /// </summary>
        public AccessToken Admit(VisitorDefinition definition, VisitorAccessLevel level, string grantedByPlayerId)
        {
            if (definition == null || !VisitorAccess.LetsThemIn(level)) return null;

            var token = new AccessToken
            {
                tokenId = "TOK-" + (++_tokenCounter).ToString("000"),
                visitorId = definition.visitorId,
                level = level,
                allowedZones = VisitorAccess.ZonesFor(level, definition.destinationZone),
                escortRequired = level == VisitorAccessLevel.Escorted,
                grantedByPlayerId = grantedByPlayerId,
                grantedSecond = Now
            };

            token.expireSecond = level == VisitorAccessLevel.FloorPass
                ? Now + VisitorAccess.FloorPassSeconds
                : (level == VisitorAccessLevel.FullTemporary ? Now + VisitorAccess.FullTemporarySeconds : 0);

            var tracked = new Tracked
            {
                Definition = definition,
                Token = token,
                State = VisitorState.Inside,
                Flags = level == VisitorAccessLevel.Escorted ? VisitorFlags.Escorted : VisitorFlags.None,
                RouteStep = 0,
                CurrentZone = ZoneIds.Lobby,
                LastKnownZone = ZoneIds.Lobby,
                LastKnownSecond = Now,
                EnteredSecond = Now,
                NextMoveSecond = Now + SecondsPerLeg
            };

            if (definition.isHistoricalReplay) tracked.Flags |= VisitorFlags.HistoricalReplay;
            if (definition.anomalousButSafe) tracked.Flags |= VisitorFlags.AnomalousButSafe;

            _tracked.Add(tracked);

            // Entry through the front door is always logged. It is the one position the
            // caretaker can be sure of, and everything after it is inference.
            tracked.LastLoggedZone = ZoneIds.Lobby;
            Log(tracked, ZoneIds.Lobby, "log.location.lobby", "CAM-02", true);

            EventBus.Publish(new VisitorAccessGrantedEvent(definition.visitorId, (int)level));
            NO404.Core.Log.Info("Visitors", definition.visitorId + " admitted at " + level +
                                            " (" + token.tokenId + ")");
            return token;
        }

        /// <summary>
        /// Pull somebody's pass from the desk (45.3).
        ///
        /// They are not teleported anywhere. The next door simply stops opening, which is what
        /// a real reader does and which leaves the caretaker with the interesting half of the
        /// problem: there is now somebody in the building who cannot get out on their own.
        /// </summary>
        public bool Revoke(string visitorId)
        {
            var tracked = Find(visitorId);
            if (tracked == null || tracked.Token == null || tracked.Token.revoked) return false;

            tracked.Token.revoked = true;
            tracked.Flags |= VisitorFlags.LockedIn;

            EventBus.Publish(new NotificationEvent("ui.access.notify.revoked", NotificationSeverity.Warning));
            NO404.Core.Log.Info("Visitors", visitorId + " pass revoked");
            return true;
        }

        public Tracked Find(string visitorId)
        {
            if (string.IsNullOrEmpty(visitorId)) return null;
            for (int i = 0; i < _tracked.Count; i++)
                if (_tracked[i].VisitorId == visitorId) return _tracked[i];
            return null;
        }

        // -----------------------------------------------------------------
        // the night moving underneath them
        // -----------------------------------------------------------------

        public void Tick()
        {
            int now = Now;

            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                var tracked = _tracked[i];

                UpdateEscort(tracked, now);

                // Being in the room is a continuous observation, not an event. A caretaker
                // who has walked up to the second floor and is standing next to somebody
                // knows where they are for as long as they stay there.
                if (ServiceHub.Presence != null && ServiceHub.Presence.AnyoneIn(tracked.CurrentZone))
                    Observe(tracked, now);

                UpdateStaleness(tracked, now);

                if (now < tracked.NextMoveSecond) continue;

                Advance(tracked, now);

                if (tracked.State != VisitorState.Exited) continue;

                _tracked.RemoveAt(i);
                var cb = OnExited;
                if (cb != null) cb(tracked);
            }
        }

        /// <summary>
        /// One leg of the route, or the decision to stop walking it.
        ///
        /// A visitor who is going to deviate does it here, at the moment the route would have
        /// taken them somewhere their pass covers and they go somewhere it does not. Nothing
        /// announces it. The caretaker finds out because a camera catches them on a landing
        /// they have no business being on, or because they never find out at all.
        /// </summary>
        void Advance(Tracked tracked, int now)
        {
            var route = tracked.Definition.expectedRoute;

            if (tracked.State == VisitorState.AtDestination)
            {
                tracked.State = VisitorState.Leaving;
                tracked.NextMoveSecond = now + SecondsPerLeg;
                MoveTo(tracked, ZoneIds.Lobby, now);
                return;
            }

            if (tracked.State == VisitorState.Leaving)
            {
                tracked.State = VisitorState.Exited;
                Log(tracked, ZoneIds.Lobby, "log.location.lobby", "CAM-02", false);
                return;
            }

            if (tracked.State == VisitorState.Deviating)
            {
                // They stay where they went. Somebody has to come and deal with it.
                tracked.NextMoveSecond = now + SecondsPerLeg;
                Observe(tracked, now);
                return;
            }

            if (route == null || tracked.RouteStep >= route.Length)
            {
                tracked.State = VisitorState.AtDestination;
                tracked.NextMoveSecond = now + DwellAtDestinationSeconds;
                return;
            }

            var next = route[tracked.RouteStep++];
            tracked.NextMoveSecond = now + SecondsPerLeg;

            if (ShouldDeviate(tracked, next))
            {
                var target = string.IsNullOrEmpty(tracked.Definition.deviationZone)
                    ? ZoneIds.Floor04
                    : tracked.Definition.deviationZone;

                tracked.State = VisitorState.Deviating;
                tracked.Flags |= VisitorFlags.HostileIntent;
                MoveTo(tracked, target, now);
                return;
            }

            tracked.State = tracked.RouteStep >= route.Length
                ? VisitorState.AtDestination
                : VisitorState.EnRoute;

            if (tracked.State == VisitorState.AtDestination)
                tracked.NextMoveSecond = now + DwellAtDestinationSeconds;

            MoveTo(tracked, next, now);
        }

        /// <summary>
        /// Whether this leg is the one where they stop following the route.
        ///
        /// Only visitors authored to be capable of it, and only once. A revoked pass is its own
        /// reason: somebody who has just been shut out of the lift is not going to stand in the
        /// lobby waiting to be collected.
        /// </summary>
        bool ShouldDeviate(Tracked tracked, string nextZone)
        {
            if (!tracked.Definition.canDeviate) return false;
            if (tracked.State == VisitorState.Deviating) return false;

            // Escorted visitors do not wander while somebody is actually with them.
            if ((tracked.Flags & VisitorFlags.Escorted) != 0 &&
                (tracked.Flags & VisitorFlags.EscortBroken) == 0) return false;

            if (tracked.Token != null && tracked.Token.revoked) return true;

            // The pass reaching further than the route is the opening. A caller held to the
            // lobby has nowhere to deviate to; one handed the fourth floor does.
            return tracked.Token != null &&
                   VisitorAccess.Unaccompanied(tracked.Token.level) &&
                   tracked.RouteStep >= tracked.Definition.deviateAfterStep;
        }

        void MoveTo(Tracked tracked, string zoneId, int now)
        {
            tracked.CurrentZone = zoneId;

            if (tracked.Token != null && !tracked.Token.Allows(zoneId) &&
                !tracked.Violations.Contains(zoneId))
            {
                tracked.Violations.Add(zoneId);
                NoteViolation(tracked, zoneId);
            }

            Observe(tracked, now);
        }

        /// <summary>
        /// Does anything actually see them here?
        ///
        /// This is the whole tracking model (38.4). A camera sees them, a card reader records
        /// them, or a member of staff standing in the same room does - and if none of those
        /// happen, the last known position simply gets older. There is no fallback that quietly
        /// tells the player where somebody is.
        /// </summary>
        void Observe(Tracked tracked, int now)
        {
            bool seenByStaff = ServiceHub.Presence != null &&
                               ServiceHub.Presence.AnyoneIn(tracked.CurrentZone);

            string cameraId = CameraFor(tracked.CurrentZone);
            bool onCamera = cameraId != null && !CameraIsDown(cameraId);

            if (!seenByStaff && !onCamera) return;

            tracked.LastKnownZone = tracked.CurrentZone;
            tracked.LastKnownSecond = now;
            tracked.Flags &= ~VisitorFlags.Untracked;

            if (tracked.LastLoggedZone == tracked.CurrentZone) return;
            tracked.LastLoggedZone = tracked.CurrentZone;

            // A sighting somewhere the pass does not cover is the evidence the player is meant
            // to find. It goes in the access log as an unlogged crossing, exactly like an
            // intruder's, because from the building's point of view that is what it is.
            bool authorised = tracked.Token != null && tracked.Token.Allows(tracked.CurrentZone);
            Log(tracked, tracked.CurrentZone, LocationKeyFor(tracked.CurrentZone), cameraId, authorised);
        }

        void UpdateStaleness(Tracked tracked, int now)
        {
            bool stale = now - tracked.LastKnownSecond >= StaleAfterSeconds;

            if (stale) tracked.Flags |= VisitorFlags.Untracked;
            else tracked.Flags &= ~VisitorFlags.Untracked;
        }

        /// <summary>
        /// The escort condition, at the granularity the building actually has (38.9).
        ///
        /// The spec says five metres. Zones are what the streaming model and the save file
        /// speak, and "the same room" is the honest reading of five metres in a building made
        /// of corridors - it also means the rule survives the player teleporting between floors
        /// on the lift, which a metre check would not.
        /// </summary>
        void UpdateEscort(Tracked tracked, int now)
        {
            if (tracked.Token == null || !tracked.Token.escortRequired) return;
            if (tracked.State == VisitorState.Exited) return;

            // Any caretaker counts. In a two-handed shift the person who granted the escort is
            // very often not the person who ends up walking it (v3.0 35.3).
            bool together = ServiceHub.Presence != null &&
                            ServiceHub.Presence.AnyoneIn(tracked.CurrentZone);

            if (together)
            {
                tracked.Flags |= VisitorFlags.Escorted;
                tracked.Flags &= ~VisitorFlags.EscortBroken;
                tracked.EscortBrokenSince = 0;
                return;
            }

            if (tracked.EscortBrokenSince == 0)
            {
                tracked.EscortBrokenSince = now;
                EventBus.Publish(new NotificationEvent("ui.access.notify.escort_slipping",
                                                        NotificationSeverity.Warning));
                return;
            }

            if (now - tracked.EscortBrokenSince < EscortGraceSeconds) return;
            if ((tracked.Flags & VisitorFlags.EscortBroken) != 0) return;

            tracked.Flags |= VisitorFlags.EscortBroken;
            tracked.Flags &= ~VisitorFlags.Escorted;

            EventBus.Publish(new NotificationEvent("ui.access.notify.escort_broken",
                                                    NotificationSeverity.Urgent));
            NO404.Core.Log.Info("Visitors", tracked.VisitorId + " escort broken");
        }

        /// <summary>
        /// Somebody is where their pass does not reach.
        ///
        /// It costs pressure and it raises the floor's risk, and neither of those is a message
        /// box telling the player they were wrong. What tells them is the camera.
        /// </summary>
        void NoteViolation(Tracked tracked, string zoneId)
        {
            if (ServiceHub.Pressure != null)
                ServiceHub.Pressure.NoteWrongAdmit(tracked.VisitorId);

            if (ServiceHub.Risk != null)
                ServiceHub.Risk.AddFloorRisk(Gameplay.FloorPlan.FloorOfZone(zoneId), 1,
                                             "reason.visitor_off_route");

            EventBus.Publish(new VisitorOffRouteEvent(tracked.VisitorId, zoneId));

            var cb = OnViolation;
            if (cb != null) cb(tracked, zoneId);

            NO404.Core.Log.Info("Visitors", tracked.VisitorId + " is in " + zoneId +
                                            ", which their pass does not cover");
        }

        // -----------------------------------------------------------------
        // the building's own senses
        // -----------------------------------------------------------------

        string CameraFor(string zoneId)
        {
            if (_cameraByZone.Count == 0) BuildCameraMap();

            string cameraId;
            return _cameraByZone.TryGetValue(zoneId ?? string.Empty, out cameraId) ? cameraId : null;
        }

        /// <summary>
        /// Built from the channel list rather than written out here, so a camera moved in
        /// content cannot leave this service watching a corridor that no longer has one.
        /// </summary>
        void BuildCameraMap()
        {
            if (ServiceHub.Content == null) return;

            foreach (var channel in ServiceHub.Content.CctvChannels)
            {
                if (channel == null || string.IsNullOrEmpty(channel.zoneId)) continue;
                if (_cameraByZone.ContainsKey(channel.zoneId)) continue;
                _cameraByZone[channel.zoneId] = channel.cameraId;
            }
        }

        /// <summary>
        /// True while the camera wall cannot see anything at all.
        ///
        /// The cut is building-wide rather than per channel, which is the point of it: the one
        /// stretch of the night when nobody can be tracked is the stretch when the caretaker
        /// has to walk. A visitor who moves during a cut simply is not seen, and the card ages.
        /// </summary>
        static bool CameraIsDown(string cameraId)
        {
            return ServiceHub.Cctv != null && ServiceHub.Cctv.FeedsCut;
        }

        static string LocationKeyFor(string zoneId)
        {
            if (zoneId == ZoneIds.Lobby) return "log.location.lobby";
            if (zoneId == ZoneIds.Parking) return "log.location.parking";
            if (zoneId == ZoneIds.Stairwell) return "log.location.stairwell";
            if (zoneId == ZoneIds.Floor03) return "log.location.floor03";
            if (zoneId == ZoneIds.Floor04) return "log.location.floor04";
            return "log.location.building";
        }

        static void Log(Tracked tracked, string zoneId, string locationKey, string cameraId, bool authorised)
        {
            if (ServiceHub.AccessLog == null) return;

            ServiceHub.AccessLog.Add(
                ServiceHub.Clock.GameSecond,
                AccessSubject.Visitor,
                tracked.Definition.nameKey,
                tracked.Token != null ? tracked.Token.tokenId : string.Empty,
                locationKey,
                cameraId ?? string.Empty,
                authorised);
        }

        static int Now { get { return ServiceHub.Clock != null ? ServiceHub.Clock.GameSecond : 0; } }

        // -----------------------------------------------------------------
        // what the card says (38.4)
        // -----------------------------------------------------------------

        /// <summary>Game seconds since anything saw them.</summary>
        public static int SecondsSinceSeen(Tracked tracked)
        {
            return Now - tracked.LastKnownSecond;
        }

        /// <summary>Game seconds they have been inside.</summary>
        public static int DwellSeconds(Tracked tracked)
        {
            return Now - tracked.EnteredSecond;
        }

        public static string StateKey(Tracked tracked)
        {
            if ((tracked.Flags & VisitorFlags.EscortBroken) != 0) return "ui.access.state.escort_broken";
            if ((tracked.Flags & VisitorFlags.Untracked) != 0) return "ui.access.state.unknown";

            switch (tracked.State)
            {
                case VisitorState.Inside:        return "ui.access.state.inside";
                case VisitorState.EnRoute:       return "ui.access.state.en_route";
                case VisitorState.AtDestination: return "ui.access.state.at_destination";
                case VisitorState.Deviating:     return "ui.access.state.en_route";
                case VisitorState.Leaving:       return "ui.access.state.leaving";
            }
            return "ui.access.state.inside";
        }

        /// <summary>One row of the host's tracking board, on its way to a client.</summary>
        public struct MirrorRow
        {
            public string VisitorId;
            public string LastKnownZone;
            public int LastKnownSecond;
            public int EnteredSecond;
            public VisitorFlags Flags;
            public VisitorAccessLevel Level;
            public VisitorState State;
            public bool Revoked;
        }

        /// <summary>
        /// Replace this copy's board with the host's (v3.0 46.1).
        ///
        /// Rebuilt rather than merged. The board is small, and a merge that got one row wrong
        /// would leave a caretaker walking upstairs after somebody who left ten minutes ago -
        /// which is exactly the kind of bug this system's whole appeal makes unfalsifiable.
        /// </summary>
        public void ApplyMirror(IReadOnlyList<MirrorRow> rows)
        {
            _tracked.Clear();
            if (rows == null) return;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var definition = ServiceHub.Content != null
                    ? ServiceHub.Content.FindVisitor(row.VisitorId)
                    : null;
                if (definition == null) continue;

                _tracked.Add(new Tracked
                {
                    Definition = definition,
                    Token = new AccessToken
                    {
                        tokenId = string.Empty,
                        visitorId = row.VisitorId,
                        level = row.Level,
                        allowedZones = VisitorAccess.ZonesFor(row.Level, definition.destinationZone),
                        escortRequired = row.Level == VisitorAccessLevel.Escorted,
                        revoked = row.Revoked
                    },
                    State = row.State,
                    Flags = row.Flags,
                    CurrentZone = row.LastKnownZone,
                    LastKnownZone = row.LastKnownZone,
                    LastLoggedZone = row.LastKnownZone,
                    LastKnownSecond = row.LastKnownSecond,
                    EnteredSecond = row.EnteredSecond
                });
            }
        }

        public void Reset()
        {
            _tracked.Clear();
            _cameraByZone.Clear();
            _tokenCounter = 0;
        }

        public void EndNight() { _tracked.Clear(); }
    }
}
