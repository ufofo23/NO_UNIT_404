using System;
using System.Collections.Generic;
using UnityEngine;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Residents
{
    /// <summary>What a movement is for. The player is never told which one they are looking at.</summary>
    public enum TrafficKind
    {
        /// <summary>A resident going about their night. The access log agrees with the camera.</summary>
        Ordinary = 0,
        /// <summary>Someone crosses a camera and the access log has no idea they were there.</summary>
        Unlogged = 1,
        /// <summary>The log says they badged in somewhere they could not have walked from in time.</summary>
        Impossible = 2,
        /// <summary>Not a person. Only the lens sees it, and it leaves no record at all.</summary>
        Echo = 3
    }

    /// <summary>
    /// Puts people in the building.
    ///
    /// This exists because of a measurement: every night's authored beats finish between 23:10
    /// and 01:40 while the shift runs to 06:00, leaving 17 to 32 real minutes of a twelve
    /// camera security game in which nothing whatsoever moves. Lengthening the cases was not
    /// the answer - the answer was that a night watchman's screens should never be still.
    ///
    /// The escalation is the point. Early in the shift the traffic is real and the log agrees,
    /// which is what teaches the player what "correct" looks like. As the night deepens the
    /// living traffic thins out and what is left stops matching the records. GDD 4.4 requires
    /// that every appearance yields information or a choice, so nothing here is a bare scare:
    /// an Unlogged crossing is a face with no entry, an Impossible one writes a record the
    /// Access app's own compare tool will call physically impossible, and both can be caught
    /// on a snapshot. GDD 15.2 is respected too - none of these people ever chase anyone.
    /// </summary>
    public sealed class ResidentTrafficService
    {
        /// <summary>Ordinary traffic stops around here and the building starts lying instead.</summary>
        public const int DeepNightSecond = 25 * 3600;      // 01:00

        /// <summary>
        /// Shortest and longest gap between movements, in game seconds. These two and the
        /// pressure floor in <see cref="Gap"/> are the density knobs: as written a quiet night
        /// lands around forty movements and the worst night around seventy, so something
        /// crosses a camera every twenty to sixty real seconds. Push them down and the building
        /// turns into a railway station; push them up and the dead air comes back.
        /// </summary>
        const int MinGapSeconds = 240;
        const int MaxGapSeconds = 1200;

        /// <summary>A snapshot of a contradiction is worth finding (GDD 4.4).</summary>
        const int ResonanceForContradiction = 1;

        public struct Movement
        {
            public int GameSecond;
            public string ResidentId;
            public string NameKey;
            public string ZoneId;
            public string CameraId;
            public string LocationKey;
            public TrafficKind Kind;
            public bool Inbound;
            /// <summary>Set only for a wrongly admitted visitor walking the building (GDD 15.4).</summary>
            public string IntruderVisitorId;
        }

        readonly List<Movement> _pending = new List<Movement>(32);
        readonly List<ResidentActor> _live = new List<ResidentActor>(8);
        readonly HashSet<string> _rewarded = new HashSet<string>();

        System.Random _random = new System.Random(0);
        int _nightIndex;

        /// <summary>Movements still to come tonight. The HUD never shows this; debug only.</summary>
        public int PendingCount { get { return _pending.Count; } }

        /// <summary>
        /// Tonight's schedule, read-only. The night simulator (Tools > NO404 > Data > Simulate
        /// Nights) reads it to count how much of a night is contradictory before any of it is
        /// spent on balance numbers - guessing that volume is how GDD 15.4 got it wrong once.
        /// </summary>
        public IReadOnlyList<Movement> Scheduled { get { return _pending; } }
        public int LiveCount { get { return _live.Count; } }

        // ---- corridors people actually use ---------------------------------

        struct Route
        {
            public string ZoneId;
            public string CameraId;
            public string LocationKey;
            public Route(string zoneId, string cameraId, string locationKey)
            {
                ZoneId = zoneId; CameraId = cameraId; LocationKey = locationKey;
            }
        }

        static readonly Route[] Routes =
        {
            new Route(ZoneIds.Lobby,      "CAM-02", "log.location.lobby"),
            new Route(ZoneIds.Floor04,    "CAM-04", "log.location.floor04"),
            new Route(ZoneIds.Floor03,    "CAM-06", "log.location.floor08"),
            new Route(ZoneIds.Floor05,           "CAM-07", "log.location.floor05"),
            new Route(ZoneIds.Stairwell,  "CAM-05", "log.location.stairwell"),
            new Route(ZoneIds.Parking,    "CAM-09", "log.location.parking")
        };

        // ---- night lifecycle -------------------------------------------------

        public void BeginNight(int nightIndex)
        {
            Clear();
            _nightIndex = nightIndex;

            // Seeded per night so a reloaded save replays the same building. A night that
            // shuffles its own traffic every reload would make every contradiction unfalsifiable.
            _random = new System.Random(unchecked(0x4E4F344 + nightIndex * 7919));

            BuildSchedule();
            Log.Info("Traffic", "night " + nightIndex + ": " + _pending.Count + " movements scheduled");
        }

        public void EndNight() { Clear(); }
        public void Reset() { Clear(); _nightIndex = 0; }

        void Clear()
        {
            for (int i = 0; i < _live.Count; i++) if (_live[i] != null) _live[i].Despawn();
            _live.Clear();
            _pending.Clear();
            _rewarded.Clear();
        }

        // ---- the schedule ----------------------------------------------------

        void BuildSchedule()
        {
            var residents = LivingResidents();
            if (residents.Count == 0) return;

            int now = GameClock.ShiftStartSecond;

            while (now < GameClock.ShiftEndSecond)
            {
                now += Gap(now);
                if (now >= GameClock.ShiftEndSecond) break;

                var route = Routes[_random.Next(Routes.Length)];
                var resident = residents[_random.Next(residents.Count)];

                _pending.Add(new Movement
                {
                    GameSecond = now,
                    ResidentId = resident.residentId,
                    NameKey = resident.nameKey,
                    ZoneId = route.ZoneId,
                    CameraId = route.CameraId,
                    LocationKey = route.LocationKey,
                    Kind = KindFor(now),
                    Inbound = _random.Next(2) == 0
                });
            }
        }

        /// <summary>
        /// Traffic is dense at the start of the shift, thins towards 01:00 and then picks back
        /// up - because after 01:00 what is moving is not the neighbours coming home.
        /// </summary>
        int Gap(int gameSecond)
        {
            float through = Mathf.InverseLerp(GameClock.ShiftStartSecond, GameClock.ShiftEndSecond, gameSecond);

            // A shallow U: busy 22:00-23:30 with people coming home, quiet either side of
            // 01:00, and busy again by 04:00 - with nothing that should be awake.
            float density = Mathf.Abs(through - 0.45f) * 2f;            // 0 at the trough, ~1 at the ends
            float baseline = Mathf.Lerp(MaxGapSeconds, MinGapSeconds * 1.5f, density);

            // Later nights, and a player Harin has taken an interest in, get a busier building.
            float pressure = 1f - Mathf.Clamp01(_nightIndex / 8f)
                                - Mathf.Clamp01(ServiceHub.State.GetStat(StatIds.HarinResonance) / 240f);

            int gap = Mathf.RoundToInt(baseline * Mathf.Max(0.55f, pressure));
            return Mathf.Clamp(gap + _random.Next(-90, 150), MinGapSeconds, MaxGapSeconds);
        }

        /// <summary>
        /// Before 01:00 almost everything is real, which is what makes it worth watching later.
        /// After 01:00 the proportion inverts, and it inverts harder every night.
        /// </summary>
        TrafficKind KindFor(int gameSecond)
        {
            bool deep = gameSecond >= DeepNightSecond;

            // Steep in the night index on purpose. The prologue is a teaching shift (GDD 9.1)
            // and a player who has not yet learned what a correct log entry looks like cannot
            // read a wrong one - so night 0 gets two or three uneasy moments, not a dozen.
            // By night 6 almost nothing crossing a camera is what the records say it is.
            int wrongChance = deep ? 10 + _nightIndex * 12 : 2 + _nightIndex * 2;
            wrongChance += ServiceHub.State.GetStat(StatIds.HarinResonance) / 5;

            if (_random.Next(100) >= Mathf.Clamp(wrongChance, 0, 92)) return TrafficKind.Ordinary;

            // Echoes are the rarest and only ever appear once the player is deep in the story.
            int roll = _random.Next(100);
            if (deep && _nightIndex >= 2 && roll < 30) return TrafficKind.Echo;
            return roll < 65 ? TrafficKind.Unlogged : TrafficKind.Impossible;
        }

        List<ResidentDefinition> LivingResidents()
        {
            var result = new List<ResidentDefinition>();

            foreach (var resident in ServiceHub.Content.Residents)
            {
                if (resident == null) continue;

                // The vacant units and the record that should not exist do not walk anywhere.
                var status = ServiceHub.Residents.StatusOf(resident);
                if (status == ResidentStatus.Vacant || status == ResidentStatus.Unregistered) continue;

                result.Add(resident);
            }

            return result;
        }

        /// <summary>
        /// Puts a wrongly admitted visitor into the building (GDD 15.4).
        ///
        /// Until this existed the consequence of waving the wrong person through was a number
        /// on a summary screen. Now they walk. Four crossings, spread over the next twelve
        /// game minutes, on cameras chosen by the night seed - and the access log has no idea
        /// any of them happened, which is exactly the contradiction 12.3 asks the player to
        /// name. Filing that report is what makes them stop (CctvService.Report).
        /// </summary>
        public void InjectIntruder(string visitorId, string nameKey)
        {
            if (string.IsNullOrEmpty(visitorId) || _random == null) return;

            int at = ServiceHub.Clock.GameSecond + IntruderFirstDelaySeconds;

            for (int i = 0; i < IntruderCrossings; i++)
            {
                if (at >= GameClock.ShiftEndSecond) break;

                var route = Routes[_random.Next(Routes.Length)];

                var movement = new Movement
                {
                    GameSecond = at,
                    ResidentId = visitorId,
                    NameKey = nameKey,
                    ZoneId = route.ZoneId,
                    CameraId = route.CameraId,
                    LocationKey = route.LocationKey,
                    Kind = TrafficKind.Unlogged,
                    Inbound = i % 2 == 0,
                    IntruderVisitorId = visitorId
                };

                // Tick releases from the front and assumes ascending time, so this has to go
                // in at the right index rather than on the end.
                int index = 0;
                while (index < _pending.Count && _pending[index].GameSecond <= movement.GameSecond) index++;
                _pending.Insert(index, movement);

                at += IntruderGapSeconds;
            }

            Log.Info("Traffic", "intruder " + visitorId + " is walking the building");
        }

        /// <summary>Game seconds before a wrongly admitted visitor first crosses a camera.</summary>
        public const int IntruderFirstDelaySeconds = 90;
        public const int IntruderGapSeconds = 240;
        public const int IntruderCrossings = 4;

        /// <summary>Sighting ids for an intruder start with this, so a report can name them.</summary>
        public const string IntruderSourcePrefix = "intruder:";

        // ---- per-frame -------------------------------------------------------

        /// <summary>
        /// Somebody the host says walked past a camera (v3.0 46.1).
        ///
        /// A joining caretaker does not schedule traffic - they cannot, because half of these
        /// movements are contradictions the access log is deliberately not carrying, and two
        /// machines rolling their own would disagree about which. So the schedule stays the
        /// host's and this is the arrival: one person, in a zone this copy has loaded, walking
        /// the same way. No record is written, because the host already wrote it and it came
        /// over in the mirror.
        /// </summary>
        public void SpawnMirroredActor(string nameKey, string zoneId, TrafficKind kind,
                                       bool inbound, float speed)
        {
            var root = ZoneRegistry.Find(zoneId);
            if (root == null) return;

            var actor = ResidentActor.Spawn(root, zoneId, nameKey, PathThrough(zoneId, inbound),
                                            ColorFor(kind), kind == TrafficKind.Echo, speed);
            if (actor == null) return;

            _live.Add(actor);

            if (kind != TrafficKind.Echo && ServiceHub.Player.CurrentZone == zoneId)
                ServiceHub.Audio.PlayCueAt(AudioCue.Footsteps, actor.transform.position);
        }

        /// <summary>
        /// Walks the people who are already here, and schedules nobody new.
        ///
        /// What a client runs instead of <see cref="Tick"/>. The whole of the rest of Tick -
        /// releasing the schedule, writing records, offering contradictions - is the host's
        /// simulation, and a client running it would produce a second, different night on top
        /// of the one it is supposed to be watching.
        /// </summary>
        public void TickActorsOnly(float deltaSeconds)
        {
            float walked = deltaSeconds * ServiceHub.Clock.TimeScale * GameClock.GameSecondsPerRealSecond;
            AdvanceActors(walked);
        }

        void AdvanceActors(float walked)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var actor = _live[i];
                if (actor == null) { _live.RemoveAt(i); continue; }

                actor.Advance(walked);
                if (!actor.Finished) continue;

                actor.Despawn();
                _live.RemoveAt(i);
            }
        }

        public void Tick(float deltaSeconds)
        {
            int now = ServiceHub.Clock.GameSecond;

            // The schedule is built in ascending time order and released from the front, so
            // the access log receives entries in the order they happened even when a stalled
            // frame makes several of them come due at once.
            int due = 0;
            while (due < _pending.Count && _pending[due].GameSecond <= now) due++;

            for (int i = 0; i < due; i++) Release(_pending[i]);
            if (due > 0) _pending.RemoveRange(0, due);

            AdvanceActors(deltaSeconds * ServiceHub.Clock.TimeScale * GameClock.GameSecondsPerRealSecond);
        }

        void Release(Movement movement)
        {
            WriteRecord(movement);

            // Everybody on shift gets the same person in the same corridor. Sent before the
            // local spawn and regardless of whether this machine's floor is loaded, because
            // whose floors are streamed in is a fact about each caretaker's night, not about
            // who walked past the camera (v3.0 46.1).
            Net.NetShift.BroadcastTraffic(movement.NameKey, movement.ZoneId, (int)movement.Kind,
                                          movement.Inbound, SpeedFor(movement));

            // An unloaded floor means nobody could have been watching that camera anyway. The
            // record above still stands, so the contradiction survives for the Access app.
            var root = ZoneRegistry.Find(movement.ZoneId);
            if (root == null) return;

            var actor = ResidentActor.Spawn(root, movement.ZoneId, movement.NameKey,
                                            PathThrough(movement.ZoneId, movement.Inbound),
                                            ColorFor(movement.Kind),
                                            movement.Kind == TrafficKind.Echo,
                                            SpeedFor(movement));
            if (actor == null) return;

            _live.Add(actor);

            // Heard, not just seen - but only from the room the player is standing in. An echo
            // makes no sound at all, which is the point of it.
            //
            // Placed on the actor rather than played flat: a corridor is 1.55m wide and the
            // player has to be able to tell which end of it someone walked in from, which is
            // GDD 19.3's reason for putting important sounds in 3D.
            if (movement.Kind != TrafficKind.Echo && ServiceHub.Player.CurrentZone == movement.ZoneId)
                ServiceHub.Audio.PlayCueAt(AudioCue.Footsteps, actor.transform.position);

            if (movement.Kind != TrafficKind.Ordinary) OfferContradiction(movement);
        }

        /// <summary>
        /// GDD 12.3 #10: Sunja going up the stairs far faster than an 81 year old should.
        /// The catalogue asks for it by name and it needs ordinary walking to read against.
        /// </summary>
        static float SpeedFor(Movement movement)
        {
            bool hurriedStairs = movement.ZoneId == ZoneIds.Stairwell && movement.Kind != TrafficKind.Ordinary;
            return hurriedStairs ? ResidentActor.HurriedSpeed : ResidentActor.WalkSpeed;
        }

        /// <summary>
        /// The access log is the other half of every contradiction, so what does and does not
        /// get written here is the whole mechanic.
        /// </summary>
        void WriteRecord(Movement movement)
        {
            switch (movement.Kind)
            {
                case TrafficKind.Ordinary:
                    ServiceHub.AccessLog.Add(movement.GameSecond, AccessSubject.Resident, movement.NameKey,
                                             CardOf(movement.ResidentId), movement.LocationKey,
                                             movement.CameraId, movement.Inbound);
                    break;

                case TrafficKind.Unlogged:
                case TrafficKind.Echo:
                    // Nothing. That absence is the evidence.
                    break;

                case TrafficKind.Impossible:
                    // Two entries close enough together that no one could have walked between
                    // them, sharing a contradiction group so the Access app's compare flags it.
                    var elsewhere = Routes[_random.Next(Routes.Length)];
                    string group = "grp_traffic_" + movement.GameSecond;

                    ServiceHub.AccessLog.Add(movement.GameSecond, AccessSubject.Resident, movement.NameKey,
                                             CardOf(movement.ResidentId), movement.LocationKey,
                                             movement.CameraId, movement.Inbound, group);
                    ServiceHub.AccessLog.Add(movement.GameSecond + 20, AccessSubject.Resident, movement.NameKey,
                                             CardOf(movement.ResidentId), elsewhere.LocationKey,
                                             elsewhere.CameraId, !movement.Inbound, group);
                    break;
            }
        }

        static string CardOf(string residentId)
        {
            var resident = ServiceHub.Residents.FindById(residentId);
            return resident != null ? resident.cardId : string.Empty;
        }

        /// <summary>
        /// A contradictory crossing becomes something the player can name (GDD 12.3 "인물 모순").
        ///
        /// The payout deliberately does not come from having happened to be looking - it comes
        /// from filing the report. Being in the right place is luck; saying what it was is play.
        /// The sighting is registered whether or not anyone saw it live, because the rewind
        /// buffer and the access log both still hold it (GDD 12.4).
        /// </summary>
        void OfferContradiction(Movement movement)
        {
            string key = string.IsNullOrEmpty(movement.IntruderVisitorId)
                ? movement.ResidentId + ":" + movement.GameSecond
                : IntruderSourcePrefix + movement.IntruderVisitorId + ":" + movement.GameSecond;

            if (!_rewarded.Add(key)) return;

            ServiceHub.Cctv.RegisterSighting(movement.CameraId, CCTV.AnomalyCategory.PersonContradiction,
                                             VisibleGameSeconds(movement.ZoneId),
                                             key, null, ResonanceForContradiction,
                                             string.IsNullOrEmpty(movement.IntruderVisitorId)
                                                 ? CCTV.SightingOrigin.Traffic
                                                 : CCTV.SightingOrigin.Intruder);

            bool watching = ServiceHub.Player.InPcMode && ServiceHub.Player.CurrentAppId == AppIds.Cctv;
            if (!watching)
            {
                // GDD 12.4 asks for a "미확인 움직임" marker, not a popup: with forty
                // contradictions on a late night, a notification each time would be nagging
                // rather than unnerving. The tile label turns amber and the counter ticks up.
                ServiceHub.Cctv.MarkUnreviewedMotion(movement.CameraId);
                return;
            }

            ServiceHub.Captions.Ambient(movement.Kind == TrafficKind.Echo
                ? "caption.someone_who_is_not_there"
                : "caption.someone_unlogged");
        }

        /// <summary>Roughly how long the walk across this zone takes, in game seconds.</summary>
        static int VisibleGameSeconds(string zoneId)
        {
            float width = Mathf.Max(2f, WorldBuilder.SizeOf(zoneId).x - 1.2f);
            float realSeconds = width / ResidentActor.WalkSpeed;
            return Mathf.RoundToInt(realSeconds * GameClock.GameSecondsPerRealSecond);
        }

        /// <summary>
        /// GDD 12.4 forbids anomalies that can only be told apart by colour, so this is a
        /// legibility aid on a greybox capsule, not the signal. The signal is the record.
        /// </summary>
        static Color ColorFor(TrafficKind kind)
        {
            switch (kind)
            {
                case TrafficKind.Echo: return new Color(0.48f, 0.46f, 0.44f);
                case TrafficKind.Impossible: return new Color(0.40f, 0.38f, 0.42f);
                default: return new Color(0.52f, 0.50f, 0.47f);
            }
        }

        /// <summary>A straight walk across the zone, entering from one end and leaving at the other.</summary>
        static Vector3[] PathThrough(string zoneId, bool inbound)
        {
            var size = WorldBuilder.SizeOf(zoneId);
            float half = size.x * 0.5f - 0.6f;
            if (half < 0.4f) half = 0.4f;

            float from = inbound ? -half : half;
            float to = -from;

            return new[]
            {
                new Vector3(from, 0.02f, 0f),
                new Vector3(to, 0.02f, 0f)
            };
        }
    }
}
