using System;
using System.Collections.Generic;
using UnityEngine;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Threat
{
    /// <summary>
    /// Owns the game's only threat windows (GDD 15.2: ordinary patrols are never chased).
    /// Nothing stalks the player outside the nights and zones registered here.
    ///
    /// Being caught is a cost, not a death: the chairman takes a piece of evidence, the
    /// player's performance is doctored and time is lost (GDD 9.6).
    /// </summary>
    public enum ThreatKind { None = 0, Patrol = 1, Fire = 2 }

    public sealed class ThreatService
    {
        public const int CaughtPerformancePenalty = 8;
        public const int CaughtTimeLossSeconds = 600;   // ten game minutes

        /// <summary>How long the night-6 fire gives the player, in game seconds.</summary>
        public const int FireEscapeSeconds = 900;

        /// <summary>Below this the stairs are partly blocked and the route changes (GDD 9.7).</summary>
        public const int BlockedStairsSafety = 40;

        /// <summary>
        /// What a lost courtyard costs the escape (v2.1 spec 21.7).
        ///
        /// M08 ignored or got wrong leaves the playground with swing shadows and exit signs
        /// pointing the wrong way, and the spec says plainly that the escape takes longer for
        /// it. Three minutes of a fifteen-minute window is enough to be felt on a bad route
        /// and not enough to decide the night on its own.
        /// </summary>
        public const int CourtyardPenaltySeconds = 180;

        /// <summary>T01 left a car across the B1 fire lane and the exit is narrower for it.</summary>
        public const int FireLanePenaltySeconds = 120;

        /// <summary>
        /// The floor under the escape window however badly the night went.
        ///
        /// Spec 24.2 forbids a wrong answer from closing the main line off, and an escape with
        /// no time in it would do exactly that. Every penalty above is real and none of them
        /// can take the last five minutes away.
        /// </summary>
        public const int MinimumFireEscapeSeconds = 300;

        /// <summary>Pairs of hands the evacuation needs before the fire (spec 7.4).</summary>
        public const int BaseEvacuationHands = 2;

        readonly List<StalkerController> _stalkers = new List<StalkerController>();
        readonly List<HidingSpot> _hidingSpots = new List<HidingSpot>();
        readonly List<string> _pendingZones = new List<string>();

        Transform _root;
        PlayerController _player;
        HidingSpot _currentSpot;
        Vector3 _preHidePosition;
        Quaternion _preHideRotation;

        /// <summary>
        /// Alert at which the chairman stops waiting for night 5 to have someone walk the
        /// basement, and the level at which he starts taking things.
        /// </summary>
        // v5.0 5.2 put ChairmanAlert on 0..100, so both thresholds moved with it. Half of
        // the scale still means "he has started watching" and four fifths still means "he has
        // started taking things".
        public const int WatchedAlert = 50;
        public const int ConfiscationAlert = 80;

        /// <summary>Earliest night the chairman will act on his own suspicion.</summary>
        public const int EarliestWatchedNight = 3;

        /// <summary>Nights on which a threat exists regardless of anything the player did.</summary>
        static readonly Dictionary<int, string[]> ThreatZonesByNight = new Dictionary<int, string[]>
        {
            { 5, new[] { ZoneIds.Archive, ZoneIds.Parking } }
        };

        /// <summary>
        /// Where someone is walking tonight.
        ///
        /// ChairmanAlert was written to in two places and read in none - seven nights of
        /// accumulating a number that changed nothing, which made every choice about the
        /// chairman weightless. Pushing him raises it; at WatchedAlert he puts someone in the
        /// basement on the nights the player is most likely to be down there, which is the
        /// consequence those choices were always supposed to have.
        ///
        /// GDD 15.2 still holds: this is a patrol to be avoided, not a chase, and it only
        /// exists in the zones the player chooses to enter.
        /// </summary>
        static string[] ThreatZonesFor(int nightIndex)
        {
            string[] scripted;
            ThreatZonesByNight.TryGetValue(nightIndex, out scripted);

            int alert = ServiceHub.State.GetStat(StatIds.ChairmanAlert);
            bool watched = alert >= WatchedAlert && nightIndex >= EarliestWatchedNight;

            if (!watched) return scripted ?? new string[0];
            if (scripted != null) return scripted;

            Log.Info("Threat", "chairman alert " + alert + ": the basement is being watched");
            return new[] { ZoneIds.Archive, ZoneIds.Parking };
        }

        public bool IsActive { get; private set; }
        public bool IsHidden { get { return _currentSpot != null; } }
        public int CaughtCount { get; private set; }

        public ThreatKind Kind { get; private set; }

        int _fireEndsAtGameSecond;

        /// <summary>1 at the moment the fire starts, 0 when the time is gone.</summary>
        public float FireProgress01
        {
            get
            {
                if (Kind != ThreatKind.Fire) return 0f;
                int left = _fireEndsAtGameSecond - ServiceHub.Clock.GameSecond;
                return Mathf.Clamp01(left / (float)FireEscapeSeconds);
            }
        }

        public event Action<StalkerController> OnCaught;

        public bool PlayerCrouching { get { return _player != null && _player.IsCrouching; } }

        /// <summary>Highest awareness across live stalkers - the HUD reads this.</summary>
        public float Awareness
        {
            get
            {
                float highest = 0f;
                for (int i = 0; i < _stalkers.Count; i++)
                    if (_stalkers[i] != null && _stalkers[i].Awareness > highest)
                        highest = _stalkers[i].Awareness;
                return highest;
            }
        }

        public void Bind(Transform root, PlayerController player)
        {
            _root = root;
            _player = player;
        }

        public void RegisterHidingSpot(HidingSpot spot)
        {
            if (spot != null && !_hidingSpots.Contains(spot)) _hidingSpots.Add(spot);
        }

        // ---- night lifecycle -------------------------------------------------

        public void BeginNight(int nightIndex)
        {
            Clear();

            ConfiscateIfAlarmed(nightIndex);

            // dev.mainonly: no stalker. Placed after the confiscation because that is the
            // chairman acting on an alert level the campaign built up, which is main-line
            // state rather than something the night invented tonight - a shift that quietly
            // handed the equipment back would be testing a save the player does not have.
            if (MainOnlyMode.Active)
            {
                Log.Info("Threat", "no threat on night " + nightIndex + " (dev.mainonly)");
                return;
            }

            var zones = ThreatZonesFor(nightIndex);
            if (zones.Length == 0) return;

            IsActive = zones.Length > 0;
            if (IsActive) Kind = ThreatKind.Patrol;

            // Threat zones are streamed floors, so at shift start (the player is still in the
            // office) they are usually not resident yet. Whatever does not spawn now is
            // retried every Tick until its zone actually loads (GDD 9.6: the threat is
            // discovered when the player walks in, not before).
            for (int i = 0; i < zones.Length; i++)
                if (!TrySpawnStalker(zones[i])) _pendingZones.Add(zones[i]);

            Log.Info("Threat", IsActive
                ? "threat window open on night " + nightIndex + " in " + string.Join(", ", zones)
                : "no threat on night " + nightIndex);
        }

        /// <summary>
        /// At the top of the scale the chairman stops watching and starts removing things.
        ///
        /// Only ever takes evidence flagged archiveCritical = false. CLAUDE.md and GDD 14.4
        /// both require that no single misstep can permanently close off the truth ending, so
        /// what the player needs to prove the case is untouchable - what goes missing is the
        /// corroboration that made proving it comfortable.
        /// </summary>
        void ConfiscateIfAlarmed(int nightIndex)
        {
            if (nightIndex < EarliestWatchedNight) return;
            if (ServiceHub.State.GetStat(StatIds.ChairmanAlert) < ConfiscationAlert) return;

            string taken = null;
            foreach (var pair in ServiceHub.Evidence.Owned)
            {
                var definition = pair.Value != null ? pair.Value.Definition : null;
                if (definition == null || definition.archiveCritical) continue;

                taken = pair.Key;
                break;
            }

            if (taken == null) return;

            ServiceHub.Evidence.Remove(taken);
            ServiceHub.State.AddStat(StatIds.ArchiveIntegrity, -10, "reason.evidence_confiscated");

            EventBus.Publish(new NotificationEvent("ui.notify.evidence_confiscated",
                                                   NotificationSeverity.Urgent));
            ServiceHub.Analytics.Track("evidence_confiscated", taken);
            Log.Warn("Threat", "chairman confiscated " + taken);
        }

        bool TrySpawnStalker(string zoneId)
        {
            var zoneRoot = ZoneRegistry.Find(zoneId);
            if (zoneRoot == null || _root == null || _player == null) return false;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "STALKER_" + zoneId;
            go.transform.SetParent(_root, false);
            go.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);

            // The body must not block the player's own movement or the line-of-sight check.
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = Core.GreyboxMaterial.Tinted(new Color(0.42f, 0.22f, 0.22f));

            var stalker = go.AddComponent<StalkerController>();
            stalker.Setup(zoneId, _player.transform, PatrolRoutes.For(zoneId, zoneRoot.position));
            _stalkers.Add(stalker);
            return true;
        }

        public void EndNight() { Clear(); }

        // ---- night 6: the fire (GDD 9.7 step 5, 15.2) ------------------------

        /// <summary>
        /// Starts the escape. Nothing chases the player here - the building does. Failing is
        /// a cost, not a death: the ledger is lost and the building takes damage.
        /// </summary>
        public void BeginFireEscape()
        {
            if (Kind == ThreatKind.Fire) return;

            Kind = ThreatKind.Fire;
            IsActive = true;
            _fireEndsAtGameSecond = ServiceHub.Clock.GameSecond + FireEscapeWindow();

            // Who is still in the building, decided before the escape rather than during it:
            // the residents left when they were told to, and the help they had is the help
            // the caretaker earned on the nights before this one (spec 7.4).
            ResolveEvacuation();

            EventBus.Publish(new NotificationEvent("ui.notify.fire_started", NotificationSeverity.Urgent));
            ServiceHub.Captions.Ambient("caption.fire_starts");
            ServiceHub.Audio.PlayCue(AudioCue.FireAlarm);

            // GDD 9.7: with a strong resonance Harin shows the way out.
            if (ServiceHub.State.GetStat(StatIds.HarinResonance) >= 60)
            {
                // With the stairwell gone and the courtyard safe, that is the way she points -
                // the children she was one of are the reason it is open (spec 21.7).
                string route = !StairsBlocked ? "ui.notify.harin_guides_stairs"
                             : ServiceHub.State.GetFlag(FlagIds.CourtyardEscapeSafe)
                                 ? "ui.notify.harin_guides_courtyard"
                                 : "ui.notify.harin_guides_elevator";
                EventBus.Publish(new NotificationEvent(route, NotificationSeverity.Info));
            }

            Log.Info("Threat", "fire escape started; stairs blocked = " + StairsBlocked);
        }

        /// <summary>
        /// How long the caretaker has to get out, after the building has had its say.
        ///
        /// This is where the last night finally spends what the earlier ones recorded. Every
        /// term below is a decision the player made and forgot about: a playground left to the
        /// shadows, a car nobody towed, a tool that was never handed back, a floor allowed to
        /// get worse. None of them can end the run on their own - the window has a floor under
        /// it - and together they are the difference between walking out and running.
        /// </summary>
        public int FireEscapeWindow()
        {
            var state = ServiceHub.State;
            return FireEscapeWindowFor(state.GetFlag(FlagIds.CourtyardEscapeSafe),
                                       state.GetFlag(FlagIds.FireLaneBlocked),
                                       ServiceHub.Risk.EscapeDifficultyModifier);
        }

        /// <summary>
        /// The window, given what the earlier nights left behind.
        ///
        /// Static and parameterised so the arithmetic can be checked on its own. What this
        /// method decides is the whole payoff of spec 30.3, and it is exactly the kind of
        /// thing that quietly stops being true - a term dropped, a sign flipped - while every
        /// surrounding system still runs.
        /// </summary>
        public static int FireEscapeWindowFor(bool courtyardSafe, bool fireLaneBlocked,
                                              int difficultyModifier)
        {
            int seconds = FireEscapeSeconds;

            // Spec 21.7: M08 solved makes the courtyard a way out; ignored, 1F actively lies.
            if (!courtyardSafe) seconds -= CourtyardPenaltySeconds;

            // Spec 30.3: the fire lane T01 left blocked on night 1 is still blocked.
            if (fireLaneBlocked) seconds -= FireLanePenaltySeconds;

            // Spec 31 C14: the debts and the risk make this harder and never decide an ending.
            // One game minute per point, which is why the modifier is multiplied rather than
            // subtracted raw - it counts things, not seconds.
            if (difficultyModifier > 0) seconds -= difficultyModifier * 60;

            return seconds < MinimumFireEscapeSeconds ? MinimumFireEscapeSeconds : seconds;
        }

        /// <summary>
        /// Somewhere that counts as out of the building (spec 21.7).
        ///
        /// The lobby and the office are always exits. The courtyard is one only for a
        /// caretaker who gave the shadow children back their ball on night 6 - which is the
        /// whole of what M08 buys, and the reason it matters that it is the night the stairs
        /// can be gone.
        /// </summary>
        public bool IsFireExit(string zoneId)
        {
            return IsFireExit(zoneId, ServiceHub.State.GetFlag(FlagIds.CourtyardEscapeSafe));
        }

        public static bool IsFireExit(string zoneId, bool courtyardSafe)
        {
            if (zoneId == ZoneIds.Lobby || zoneId == ZoneIds.Office) return true;
            return zoneId == ZoneIds.Playground && courtyardSafe;
        }

        /// <summary>
        /// Spec 7.4: the relationships from the earlier nights work like manpower on the last
        /// one.
        ///
        /// Four people can help get the building empty, and each of them is a flag set by a
        /// choice made nights ago - Min-seo with the medically frail, Ji-woo knocking on the
        /// sixth floor, Tae-ho holding the basement and the fourth, Sun-ja walking out on her
        /// own. Against that stands what the building has been left in: a stairwell in poor
        /// repair, and a water outage that M09 caused by force-stopping the pump (spec 30.3).
        /// </summary>
        public int EvacuationHands()
        {
            var state = ServiceHub.State;
            return EvacuationHandsFrom(
                state.GetFlag(FlagIds.MinseoTrusted),
                state.GetFlag(FlagIds.JiwooTrusted) && !state.GetFlag(FlagIds.JiwooRisk),
                state.GetFlag(FlagIds.TaehoCooperates),
                state.GetFlag(FlagIds.SunjaHealthy) && !state.GetFlag(FlagIds.SunjaCritical));
        }

        /// <summary>
        /// Four people, counted.
        ///
        /// Two of them can be taken away by a routine task nobody thought was important.
        /// Spec 16 T11 says a roof door left to itself limits what Ji-woo can do on the last
        /// night, and spec 16 T10 says a heating complaint left unanswered means Sun-ja is no
        /// longer walking out on her own - and each of those only matters if the caretaker had
        /// earned that person in the first place, which is why the cancellation happens at the
        /// point of reading rather than at the point of writing.
        /// </summary>
        public static int EvacuationHandsFrom(bool minseo, bool jiwoo, bool taeho, bool sunja)
        {
            int hands = 0;
            if (minseo) hands++;
            if (jiwoo) hands++;
            if (taeho) hands++;
            if (sunja) hands++;
            return hands;
        }

        public static int EvacuationHandsRequired(bool waterOutage, int buildingSafety)
        {
            int required = BaseEvacuationHands;

            // Spec 30.3: no water means longer to get people ready to move.
            if (waterOutage) required++;
            if (buildingSafety <= BlockedStairsSafety) required++;
            return required;
        }

        /// <summary>
        /// Whether the building emptied in time (spec 7.4).
        ///
        /// Option C in spec 7.4 is not a failed evacuation - it is no evacuation, so what
        /// decides it is the state of the building rather than who turned up to help.
        /// </summary>
        public static bool EvacuationSucceeds(bool ordered, int hands, bool waterOutage,
                                              int buildingSafety)
        {
            return ordered
                ? hands >= EvacuationHandsRequired(waterOutage, buildingSafety)
                : buildingSafety > BlockedStairsSafety;
        }

        void ResolveEvacuation()
        {
            var state = ServiceHub.State;

            bool ordered = state.GetFlag(FlagIds.EvacuationOrdered);
            int safety = state.GetStat(StatIds.BuildingSafety);
            bool succeeded = EvacuationSucceeds(ordered, EvacuationHands(),
                                                state.GetFlag(FlagIds.WaterOutage), safety);

            state.SetFlag(succeeded ? FlagIds.EvacuationSuccess : FlagIds.EvacuationFailed, true);

            if (succeeded)
            {
                EventBus.Publish(new NotificationEvent("ui.notify.evacuation_clear",
                                                       NotificationSeverity.Info));
            }
            else
            {
                // Feeds ending D through BuildingSafety rather than being read by the ending
                // algorithm directly, so the failure is a fact about the building and not a
                // separate switch nobody can see (GDD 10).
                state.AddStat(StatIds.BuildingSafety, -6, "reason.evacuation_incomplete");
                EventBus.Publish(new NotificationEvent("ui.notify.evacuation_incomplete",
                                                       NotificationSeverity.Urgent));
            }

            Log.Info("Threat", "evacuation ordered=" + ordered + " hands=" + EvacuationHands() +
                               "/" + EvacuationHandsRequired(state.GetFlag(FlagIds.WaterOutage), safety) +
                               " -> " + (succeeded ? "clear" : "incomplete"));
        }

        /// <summary>GDD 9.7: a poorly maintained building loses part of its stairwell.</summary>
        public bool StairsBlocked
        {
            get
            {
                return Kind == ThreatKind.Fire &&
                       ServiceHub.State.GetStat(StatIds.BuildingSafety) <= BlockedStairsSafety;
            }
        }

        public bool IsRouteBlocked(string targetZoneId)
        {
            return StairsBlocked && targetZoneId == ZoneIds.Stairwell;
        }

        public void Tick()
        {
            if (_pendingZones.Count > 0)
                for (int i = _pendingZones.Count - 1; i >= 0; i--)
                    if (TrySpawnStalker(_pendingZones[i])) _pendingZones.RemoveAt(i);

            if (Kind != ThreatKind.Fire) return;

            var zone = ServiceHub.Player.CurrentZone;
            if (IsFireExit(zone)) { CompleteFireEscape(true); return; }

            if (ServiceHub.Clock.GameSecond >= _fireEndsAtGameSecond) CompleteFireEscape(false);
        }

        void CompleteFireEscape(bool escaped)
        {
            Kind = ThreatKind.None;
            IsActive = false;

            if (escaped)
            {
                ServiceHub.State.SetFlag(FlagIds.FireEscaped, true);
                ServiceHub.State.AddStat(StatIds.BuildingSafety, 4, "reason.escaped_fire");
                EventBus.Publish(new NotificationEvent("ui.notify.fire_escaped", NotificationSeverity.Info));
                Log.Info("Threat", "fire escape survived");
            }
            else
            {
                ServiceHub.Evidence.Remove("E19_ORIGINAL_LEDGER");
                ServiceHub.State.AddStat(StatIds.BuildingSafety, -8, "reason.trapped_by_fire");
                ServiceHub.State.AddStat(StatIds.ChairmanAlert, 10, "reason.trapped_by_fire");

                EventBus.Publish(new NotificationEvent("ui.notify.fire_failed", NotificationSeverity.Urgent));
                ServiceHub.Analytics.Track(AnalyticsService.Events.DeathOrFailure, "fire_escape");
                Log.Warn("Threat", "fire escape failed; the ledger is gone");

                var lobby = ZoneRegistry.FindSpawn(ZoneIds.Lobby);
                if (_player != null && lobby != null)
                {
                    LeaveHiding();
                    _player.Teleport(lobby.position, lobby.rotation);
                    ServiceHub.Player.EnterZone(ZoneIds.Parking);
                }
            }

            ServiceHub.Save.RequestAutosave(Save.SaveReason.MajorChoice);
        }

        void Clear()
        {
            LeaveHiding();

            for (int i = 0; i < _stalkers.Count; i++)
                if (_stalkers[i] != null) UnityEngine.Object.Destroy(_stalkers[i].gameObject);

            _stalkers.Clear();
            _pendingZones.Clear();
            IsActive = false;
            Kind = ThreatKind.None;
            _fireEndsAtGameSecond = 0;
        }

        // ---- hiding ----------------------------------------------------------

        public void EnterHiding(HidingSpot spot)
        {
            if (spot == null || _player == null || _currentSpot != null) return;

            _currentSpot = spot;
            spot.SetOccupied(true);

            _preHidePosition = _player.transform.position;
            _preHideRotation = _player.transform.rotation;

            _player.Teleport(spot.Anchor.position, spot.Anchor.rotation);
            _player.SetMovementLocked(true);

            Log.Info("Threat", "player hid in " + spot.name);
        }

        public void LeaveHiding()
        {
            if (_currentSpot == null) return;

            _currentSpot.SetOccupied(false);
            _currentSpot = null;

            if (_player == null) return;
            _player.SetMovementLocked(false);
            _player.Teleport(_preHidePosition, _preHideRotation);
        }

        // ---- being caught ----------------------------------------------------

        public void ReportCaught(StalkerController stalker)
        {
            CaughtCount++;

            // Inside the blackout circle, past its third rung, being caught is the end of the
            // shift rather than a tax on it. Everything below that rung still costs exactly
            // what it always cost - the circle raises the price of the same mistake, it does
            // not replace it, and the night is the most the player can ever lose.
            // Everything in the caretaker's hands goes back where it came from. This is the
            // real cost now - not evidence destroyed, not a stat docked, but the walk you just
            // made having to be made again with the building already awake.


            var taken = TakeOnePieceOfEvidence();

            ServiceHub.State.AddStat(StatIds.Performance, -CaughtPerformancePenalty, "reason.caught");
            ServiceHub.State.AddStat(StatIds.ChairmanAlert, 10, "reason.caught");
            ServiceHub.Clock.AdvanceSeconds(CaughtTimeLossSeconds);

            EventBus.Publish(new NotificationEvent(
                string.IsNullOrEmpty(taken) ? "ui.notify.caught" : "ui.notify.caught_evidence",
                NotificationSeverity.Urgent));

            ServiceHub.Analytics.Track(AnalyticsService.Events.DeathOrFailure,
                                       "caught:" + (taken ?? "nothing"));
            Log.Warn("Threat", "player caught; evidence taken: " + (taken ?? "none"));

            // Send both parties away from each other rather than killing anyone (GDD 15.2).
            var retreat = ZoneRegistry.FindSpawn(ZoneIds.Parking);
            if (_player != null && retreat != null)
            {
                LeaveHiding();
                _player.Teleport(retreat.position, retreat.rotation);
                ServiceHub.Player.EnterZone(ZoneIds.Lobby);
            }

            var zoneRoot = ZoneRegistry.Find(stalker.ZoneId);
            stalker.ResetAfterCatch(zoneRoot != null ? zoneRoot.position : stalker.transform.position);

            var cb = OnCaught;
            if (cb != null) cb(stalker);

            ServiceHub.Save.RequestAutosave(Save.SaveReason.MajorChoice);
        }

        /// <summary>
        /// Takes one piece of evidence that is not archive-critical, so a catch can never
        /// remove the last route to the truth ending (GDD 14.4).
        /// </summary>
        string TakeOnePieceOfEvidence()
        {
            foreach (var pair in ServiceHub.Evidence.Owned)
            {
                var definition = pair.Value.Definition;
                if (definition == null || definition.archiveCritical) continue;

                ServiceHub.Evidence.Remove(pair.Key);
                return pair.Key;
            }

            return null;
        }

        public void Reset()
        {
            Clear();
            CaughtCount = 0;
        }
    }
}
