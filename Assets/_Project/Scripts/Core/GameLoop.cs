using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NO404.Cases;
using NO404.CCTV;
using NO404.Gameplay;
using NO404.Interaction;
using NO404.UI;
using NO404.Visitors;

namespace NO404.Core
{
    public enum GameMode { Menu, Playing, Paused, NightSummary, Ending, Gallery, Credits, EndlessResult }

    /// <summary>
    /// Owns the frame loop and the mode machine. Everything else is a service or a view;
    /// this is the only place that decides what updates and at what game speed.
    ///
    /// Time scale table (GDD 6.2):
    ///   patrol, PC and tablet 1.0 | conversation 0.25 | board 0.0 then 0.35 | pause 0.0
    ///
    /// The PC and the tablet ran at 0.65 and 0.5 until v1.4 took the slowdown out: a screen
    /// where time crawls and nothing can reach you made leaving a window open the safest play
    /// on the hardest nights.
    /// </summary>
    public sealed class GameLoop : MonoBehaviour
    {
        public const int FinalNight = 6;

        public static GameLoop Instance { get; private set; }

        InputService _input;
        PlayerController _player;

        /// <summary>Where this copy's caretaker is standing, for a colleague's avatar to follow.</summary>
        public Transform PlayerTransform { get { return _player != null ? _player.transform : null; } }
        CctvRig _rig;
        AnomalyStager _stager;
        PlayerEchoRecorder _echo;
        VisitorBodies _visitorBodies;

        HudView _hud;
        PressureView _pressureHud;
        PcShellView _pc;
        TabletView _tablet;

        DialogueView _dialogue;
        IntroSequenceView _intro;
        AnomalyToolView _toolMenu;
        PauseView _pause;
        MainMenuView _menu;
        DevConsoleView _console;
        NightSummaryView _summary;
        EndlessResultView _endlessResult;
        EndingView _ending;
        EndingGalleryView _gallery;
        CreditsView _credits;

        GameMode _mode = GameMode.Menu;
        bool _worldBuilt;
        bool _endingResolved;

        // Scripted early-shift beats (GDD 9.1 / 9.2 / 15.6). One-shot flags rather than case
        // objectives because none of them is something the player is asked to do.
        bool _coldOpenKnockDone;
        bool _sawFloor03;
        bool _patrolKnockDone;

        public GameMode Mode { get { return _mode; } }

        // ---- construction --------------------------------------------------

        public static GameLoop Create(Transform persistentRoot)
        {
            var go = new GameObject("GameLoop");
            go.transform.SetParent(persistentRoot, false);
            var loop = go.AddComponent<GameLoop>();
            loop.BuildViews(persistentRoot);
            return loop;
        }

        void Awake() { Instance = this; }

        void OnEnable()
        {
            EventBus.Subscribe<EvidenceAcquiredEvent>(OnEvidenceAcquired);
            EventBus.Subscribe<CctvAnomalyEvent>(OnAnomalyCaption);
            EventBus.Subscribe<EvidenceLinkedEvent>(OnEvidenceLinked);
            EventBus.Subscribe<StatChangedEvent>(OnStatChanged);
            EventBus.Subscribe<ZoneChangedEvent>(OnZoneChanged);
            EventBus.Subscribe<CaseStateChangedEvent>(OnCaseStateChanged);
            EventBus.Subscribe<AnomalyToolSessionEvent>(OnToolSessionChanged);
            EventBus.Subscribe<ObjectiveChangedEvent>(OnObjectiveDone);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<EvidenceAcquiredEvent>(OnEvidenceAcquired);
            EventBus.Unsubscribe<CctvAnomalyEvent>(OnAnomalyCaption);
            EventBus.Unsubscribe<EvidenceLinkedEvent>(OnEvidenceLinked);
            EventBus.Unsubscribe<StatChangedEvent>(OnStatChanged);
            EventBus.Unsubscribe<ZoneChangedEvent>(OnZoneChanged);
            EventBus.Unsubscribe<CaseStateChangedEvent>(OnCaseStateChanged);
            EventBus.Unsubscribe<AnomalyToolSessionEvent>(OnToolSessionChanged);
            EventBus.Unsubscribe<ObjectiveChangedEvent>(OnObjectiveDone);
        }

        /// <summary>
        /// The bed under everything. Walking out of the office has to sound like leaving the
        /// only lit room in the building, so the tone changes with the kind of space rather
        /// than staying one flat hum for the whole shift.
        /// </summary>
        void OnZoneChanged(ZoneChangedEvent evt)
        {
            // The room tone is the one thing here that really is about the person holding this
            // copy of the game: it changes because *they* walked out of the office.
            ServiceHub.Audio.PlayAmbience(RoomToneFor(evt.ZoneId));
        }

        /// <summary>
        /// A caretaker - any caretaker - has arrived somewhere (v3.0 34, 37.3).
        ///
        /// Host only, and reached through NetShift's action funnel rather than off the local
        /// zone event, because night 1's two scripted knocks are facts about the shift and not
        /// about one player. The cold open empties the office by cutting the cameras; whoever
        /// walks back into it is who the door knocks at, and on a three-handed shift that is
        /// very often not the person who went down to the basement.
        /// </summary>
        public void NoteShiftZoneEntered(string zoneId)
        {
            if (ServiceHub.State.NightIndex != 1) return;

            if (zoneId == ZoneIds.Floor03) { _sawFloor03 = true; return; }
            if (zoneId != ZoneIds.Office) return;

            var door = OfficeDoorController.Instance;

            // GDD 9.1. The cold open's answer. The basement board is the first time the office
            // stands empty in the whole game, and it is not a patrol the player chose - the
            // building emptied the room by cutting the cameras. So the door is knocking when
            // they get back, inside the first twenty minutes, and the bolt and the peephole
            // are taught by something using them rather than by a prompt.
            //
            // This is where the prologue's last beat went. It used to hang off picking up the
            // 404 key on a night that charged nothing; now it hangs off the one errand the
            // shift makes compulsory.
            if (!_coldOpenKnockDone && ServiceHub.Power.BreakerResetsUsed > 0)
            {
                _coldOpenKnockDone = true;
                ShiftKnock(door, 3);
                return;
            }

            // GDD 9.2. The eighth-floor patrol empties the office a second time, and the shift
            // already answers it with the player's own delayed shape on CAM-08. The door
            // answers it too. Scripted, finite, and free - the ladder does not reach the door
            // this early on night 1 (GDD 15.4).
            if (!_sawFloor03 || _patrolKnockDone) return;

            _patrolKnockDone = true;
            ShiftKnock(door, 3);
        }

        /// <summary>
        /// Somebody knocks, and everyone on shift hears it.
        ///
        /// The office door is one door in one room, and a scripted knock is a beat of the
        /// night rather than a thing that happened to whoever is standing nearest it. A
        /// caretaker upstairs hearing nothing while the other two are being knocked at is the
        /// same bug as a caretaker seeing a live camera wall during the blackout.
        /// </summary>
        static void ShiftKnock(OfficeDoorController door, int knocks)
        {
            if (door != null) door.ScriptedKnock(knocks);
            Net.NetShift.Broadcast(Net.NetShift.Moment.OfficeKnock, null, null, knocks);
        }

        /// <summary>
        /// GDD 16.10 / 2.3 hook #1. The 404 record is the game's title and its cover image and
        /// it used to be unreachable until the night-4 sync, which is roughly an hour past the
        /// point where a Steam refund stops being free.
        ///
        /// It is still not searchable before night 4. It arrives once, unasked, the night the
        /// player first proves that someone is living in a unit that does not exist, sits on
        /// the list for three seconds and is gone. Searching again finds nothing, which is a
        /// better scene than finding it. C01 pays the resonance for that judgement either way,
        /// so a player who misses the row loses a moment and not a thread (GDD 14.4).
        /// </summary>
        void OnCaseStateChanged(CaseStateChangedEvent evt)
        {
            // A case leaving Dormant is the beat everything else in the night hangs off: the
            // share of routine callers, the one story caller who belongs to this scene, and
            // the phone call that was authored about this very case (GDD 13.4).
            //
            // Host only. Both of these schedule things - a caller at the door, a phone call -
            // and a client that ran them off its own mirrored copy of the case would queue a
            // second night's worth of work that only it can see (v3.0 46.1).
            if (!Net.NetSession.Authoritative) return;

            if (evt.Previous == CaseState.Dormant && evt.Current != CaseState.Dormant)
            {
                ReleaseCallersOn(evt.CaseId);
                ServiceHub.Phone.NotifyCaseStarted(evt.CaseId);
            }


        }

        static AudioCue RoomToneFor(string zoneId)
        {
            if (zoneId == ZoneIds.Office) return AudioCue.RoomToneOffice;
            if (zoneId == ZoneIds.Rooftop) return AudioCue.RoomToneOutside;
            if (zoneId == ZoneIds.Parking || zoneId == ZoneIds.Archive
                || zoneId == ZoneIds.ServicePassage || zoneId == ZoneIds.Unit404)
                return AudioCue.RoomToneBasement;

            return AudioCue.RoomToneCorridor;
        }

        void BuildViews(Transform persistentRoot)
        {
            _input = new InputService();
            _input.Enable();

            UiFactory.EnsureEventSystem(persistentRoot);

            _rig = CctvRig.Create(persistentRoot);

            _hud = HudView.Create(persistentRoot);
            _pressureHud = PressureView.Create(persistentRoot);
            _pc = PcShellView.Create(persistentRoot, _rig);
            _tablet = TabletView.Create(persistentRoot);
            _dialogue = DialogueView.Create(persistentRoot);
            _toolMenu = AnomalyToolView.Create(persistentRoot);
            _pause = PauseView.Create(persistentRoot);
            _menu = MainMenuView.Create(persistentRoot);
            _console = DevConsoleView.Create(persistentRoot);
            _summary = NightSummaryView.Create(persistentRoot);
            _endlessResult = EndlessResultView.Create(persistentRoot);
            _ending = EndingView.Create(persistentRoot);
            _gallery = EndingGalleryView.Create(persistentRoot);
            _credits = CreditsView.Create(persistentRoot);
            _intro = IntroSequenceView.Create(persistentRoot);

            _pause.OnResume = () => SetMode(GameMode.Playing);
            _pause.OnQuitToMenu = ReturnToMenu;

            _menu.OnContinue = ContinueGame;
            _menu.OnNewGame = NewGame;
            _menu.OnEndless = StartEndless;
            _menu.OnGallery = () => SetMode(GameMode.Gallery);
            _menu.OnCredits = () => SetMode(GameMode.Credits);
            _menu.OnQuit = QuitGame;

            _summary.OnContinue = AdvanceToNextNight;
            _endlessResult.OnContinue = ReturnToMenu;
            _ending.OnContinue = ReturnToMenu;
            _gallery.OnClose = ReturnToMenu;
            _credits.OnClose = () => SetMode(GameMode.Menu);

            ServiceHub.Phone.OnCallStartedRinging += OnPhoneRinging;
            ServiceHub.Interphone.OnVisitorArrived += OnVisitorArrived;

            ServiceHub.Save.CaptureDoors = DoorController.CaptureAll;
            ServiceHub.Save.RestoreDoors = DoorController.RestoreAll;
            DevConsole.TeleportToZone = TeleportToZone;

            _hud.SetVisible(false);
            _pressureHud.SetVisible(false);
            _menu.SetOpen(true);
        }

        // ---- world ---------------------------------------------------------

        /// <summary>
        /// Loads the always-resident core group and builds the player/rig/terminal from it.
        /// The core scene's own zones are registered inside ZoneSceneBuilder.Awake, which Unity
        /// defers to the next frame even for a synchronous additive load - so this waits for
        /// the office zone to actually be registered instead of reading WorldBuilder.OfficeSpawn/
        /// OfficeTerminal (and ZoneRegistry) one frame before they exist.
        /// </summary>
        IEnumerator EnsureWorldRoutine()
        {
            if (_worldBuilt) yield break;
            _worldBuilt = true;

            // The office and lobby are always resident (GDD 20.5); every other floor streams.
            ServiceHub.Zones.LoadCoreImmediate();
            while (!ZoneRegistry.IsLoaded(ZoneIds.Office)) yield return null;

            _player = CreatePlayer(WorldBuilder.OfficeSpawn);
            _rig.BuildCameras(ServiceHub.Content);

            _echo = PlayerEchoRecorder.Attach(_player.gameObject);
            _stager = CCTV.AnomalyStager.Create(transform.parent, _rig, ServiceHub.Content, _echo);
            _visitorBodies = VisitorBodies.Create(transform.parent);

            if (WorldBuilder.OfficeTerminal != null)
                WorldBuilder.OfficeTerminal.OnActivated = () => OpenPc(true);

            ServiceHub.Dialogue.OnLineChanged += OnDialogueLine;
            ServiceHub.Dialogue.OnConversationEnded += OnConversationEnded;

            ServiceHub.Save.CapturePlayer = () => _player.Capture();
            ServiceHub.Save.RestorePlayer = entry => _player.Restore(entry);
            ServiceHub.Threat.Bind(transform.parent, _player);

            // Cameras belong to zone roots, so the rig adds/drops just the affected group's
            // feeds whenever a floor arrives or leaves.
            ServiceHub.Zones.OnGroupLoaded += OnZoneGroupLoaded;
            ServiceHub.Zones.OnGroupUnloaded += OnZoneGroupUnloaded;
        }

        void OnZoneGroupLoaded(string group)
        {
            _rig.AddCamerasForGroup(group);
        }

        void OnZoneGroupUnloaded(string group)
        {
            _rig.RemoveCamerasForGroup(group);
        }

        // ---- captions (GDD 16.17) -------------------------------------------

        void OnDialogueLine(Dialogue.DialogueLine line)
        {
            if (line != null) ServiceHub.Captions.Speak(line.SpeakerKey, line.TextKey);
            RefreshPlayerControl();
        }

        /// <summary>Facing the chairman is what sets the building alight (GDD 9.7 step 5).</summary>
        void OnPhoneRinging(Phone.PhoneCallDefinition call)
        {
            if (call == null) return;

            ServiceHub.Audio.PlayCue(AudioCue.PhoneRing);
            EventBus.Publish(new NotificationEvent("ui.notify.incoming_call",
                                                   NotificationSeverity.Task));
        }

        /// <summary>
        /// Someone is at the front door.
        ///
        /// GDD 2.3 hook #5: the interphone rings whether or not anyone is at the desk, and a
        /// caretaker who has already left for the basement hears the office's own room tone
        /// come back through the handset. That is not a penalty for leaving; it is the shift
        /// explaining what leaving costs.
        /// </summary>
        void OnVisitorArrived(Visitors.VisitorDefinition visitor)
        {
            if (visitor == null) return;

            // Which of the two you hear depends on which room you are in, so this stays
            // local and is answered per caretaker.
            bool atTheDesk = ServiceHub.Player.CurrentZone == ZoneIds.Office;
            ServiceHub.Audio.PlayCue(atTheDesk ? AudioCue.Interphone : AudioCue.InterphoneSelfEcho);

            // The notice is one notice for the shift; the host publishes it and NetShift hands
            // it on, so a client that raised this off its own mirror must not publish a second.
            if (!Net.NetSession.Authoritative) return;

            EventBus.Publish(new NotificationEvent("ui.notify.interphone",
                                                   NotificationSeverity.Task));
        }

        void OnConversationEnded(string conversationId)
        {
            RefreshPlayerControl();

            if (conversationId != "D_N6_CHAIRMAN_CONFRONT") return;
            if (ServiceHub.State.NightIndex < FinalNight) return;

            ServiceHub.Threat.BeginFireEscape();
        }

        void OnAnomalyCaption(CctvAnomalyEvent evt)
        {
            if (!evt.Started) return;
            ServiceHub.Captions.Ambient(evt.CameraId == "CAM-08"
                ? "caption.elevator_stops"
                : "caption.distant_noise");
            ServiceHub.Audio.PlayCue(AudioCue.AnomalyStart);
        }

        PlayerController CreatePlayer(Transform spawn)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform.parent, false);
            if (spawn != null) go.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            var controller = go.AddComponent<CharacterController>();
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.35f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, PlayerController.StandHeight - 0.12f, 0f);

            var cameraGo = new GameObject("PlayerCamera");
            cameraGo.transform.SetParent(pivot, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = ServiceHub.Settings.Current.fieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.depth = 10;   // wins over any camera left in the loaded scene
            // Staged anomalies exist for the lens only (GDD 4.4) - the eye must not see them.
            camera.cullingMask = Layers.WithoutCctvOnly(camera.cullingMask);
            cameraGo.AddComponent<AudioListener>();
            RenderLook.Install(camera);

            var flashlightGo = new GameObject("Flashlight");
            flashlightGo.transform.SetParent(cameraGo.transform, false);
            var flashlight = flashlightGo.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = 18f;
            flashlight.spotAngle = 55f;
            flashlight.intensity = 3.5f;
            flashlight.enabled = false;

            // You have a body on your own monitors. It is the same dummy a colleague is drawn
            // with, white cap and all, put on the CCTV-only layer the camera above just culled
            // - so the lens sees a caretaker in the corridor and the eye does not see a capsule
            // wrapped round its own head. Being able to find yourself on the wall is what makes
            // the wall readable at all: it is the one figure on it whose position you know.
            //
            // Skipped outright without that layer, because the failure is not a missing body,
            // it is a capsule three centimetres from the player's eyes for the whole shift.
            if (Layers.CctvOnly >= 0)
                DummyBody.Build(go.transform, DummyBody.CaretakerCap, torch: false, cctvOnly: true,
                                name: "PlayerBody");

            var raycaster = go.AddComponent<InteractionRaycaster>();
            raycaster.Configure(cameraGo.transform);

            var player = go.AddComponent<PlayerController>();
            player.AssignReferences(camera, pivot, flashlight);
            player.Bind(_input, raycaster);

            return player;
        }

        // ---- playthrough ---------------------------------------------------

        /// <summary>
        /// Opens the building. Refused on a joined client, whose shift is the host's.
        ///
        /// The menu already greys these out; this is the second lock, because a shift started
        /// on two machines at once does not fail loudly - it drifts, and the first thing
        /// anybody notices is a report being refused against evidence that is visibly in the
        /// tray.
        /// </summary>
        public void NewGame() { NewGame(true); }

        /// <summary>
        /// Opens the building. Refused on a joined client, whose shift is the host's.
        ///
        /// <paramref name="withIntro"/> exists for the things that are not a person pressing
        /// a button - the screenshot probe and the campaign tests - because fifteen seconds of
        /// title cards is the right opening for a player and dead time for a machine. It is a
        /// parameter rather than a test-only door: a build that captures its own screenshots
        /// has the same reason to skip it as a test does.
        /// </summary>
        public void NewGame(bool withIntro)
        {
            if (!Net.NetSession.Authoritative) return;

            if (!withIntro)
            {
                _menu.SetOpen(false);
                StartCoroutine(NewGameRoutine());
                return;
            }

            // Fifteen seconds of who this person is and what the building is, then the shift.
            // The menu closes first so the sequence plays over black rather than over the
            // title, and the game is not started until it finishes or is skipped.
            _menu.SetOpen(false);
            _intro.OnFinished = () => StartCoroutine(NewGameRoutine());
            _intro.Play();
        }

        /// <summary>True once the building exists, so automation can wait for it.</summary>
        public bool WorldBuilt { get { return _worldBuilt; } }

        IEnumerator NewGameRoutine()
        {
            ServiceHub.ResetPlaythrough();
            yield return EnsureWorldRoutine();
            _endingResolved = false;

            var spawn = WorldBuilder.OfficeSpawn;
            if (spawn != null) _player.Teleport(spawn.position, spawn.rotation);
            ServiceHub.Player.EnterZone(ZoneIds.Office);

            // Night 1, not night 0. There is no night 0: the game opens in the middle of the
            // blackout rather than in front of a handover memo (GDD 9.1).
            StartNight(1);
            SetMode(GameMode.Playing);
            ServiceHub.Analytics.Track(AnalyticsService.Events.TutorialStarted);
        }

        public void ContinueGame()
        {
            if (!Net.NetSession.Authoritative) return;
            StartCoroutine(ContinueGameRoutine());
        }

        IEnumerator ContinueGameRoutine()
        {
            yield return EnsureWorldRoutine();
            _endingResolved = false;

            var task = ServiceHub.Save.LoadAsync(System.Threading.CancellationToken.None);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted) Log.Error("Boot", "load failed: " + t.Exception);
            }, System.Threading.Tasks.TaskScheduler.Default);

            SetMode(GameMode.Playing);
        }

        void StartNight(int nightIndex)
        {
            // Night 1 starts a conversation on its own (the radio, below), so a shift may now
            // arrive with one already on screen - a summary reached mid-sentence, a debug jump
            // out of one night into the next. A conversation that outlived its night would
            // hold player control off for the whole of the next one.
            ServiceHub.Dialogue.End();

            ServiceHub.Clock.SetGameSecond(GameClock.ShiftStartSecond);
            ServiceHub.State.BeginNight(nightIndex);
            ServiceHub.Cases.BeginNight(nightIndex);
            ServiceHub.ManualEvents.BeginNight(nightIndex);
            ServiceHub.AnomalyTools.BeginNight(nightIndex);
            // Pages whose stated source has arrived (spec 0.9.2). Called after the case
            // scheduler so a page a queued consequence handed over is already in the binder.
            ServiceHub.Manual.RefreshAvailability();
            // Spec 21 unlocks every machine off something the caretaker did rather than off
            // the clock, so this runs after the schedulers that may just have done it.
            ServiceHub.AnomalyTools.RefreshAvailability();
            ServiceHub.Cctv.BeginNight(nightIndex);

            // An endless run replays the roster, so last shift's judgements must not keep this
            // shift's callers away from the door.
            if (ServiceHub.State.EndlessMode) ServiceHub.Interphone.Reset();

            // The pool needs a campaign seed before the first draw. Minted on a new game and
            // replayed from the save on a continue, so the same campaign always deals the
            // same six nights (v5.0 4.4 step 10).
            if (ServiceHub.NightPool.CampaignSeed == 0) ServiceHub.NightPool.BeginCampaign(0);

            ServiceHub.Vitals.BeginNight(nightIndex);
            ServiceHub.Hints.Reset();
            ServiceHub.Captions.Clear();
            _coldOpenKnockDone = false;
            _sawFloor03 = false;
            _patrolKnockDone = false;
            _saidTheWorkIsDone = false;
            GrantScheduledAccess(nightIndex);
            ApplyNightOpeningState(nightIndex);
            SeedNightSchedule(nightIndex);
            // The one beat that is the shift itself, for nights with no queued work to hang
            // callers on. Released here rather than on a case, because there is no case.
            ReleaseCallersOn("@shift_start");
            ServiceHub.Phone.ScheduleNight(nightIndex);
            ServiceHub.Threat.BeginNight(nightIndex);
            ServiceHub.Traffic.BeginNight(nightIndex);

            // The two services that move on their own (GDD 15.4 / 15.5). Power first: the
            // pressure ladder reads the reserve every game minute.
            ServiceHub.Power.BeginNight(nightIndex);
            ServiceHub.Pressure.BeginNight(nightIndex);

            _boardStillnessLeft = BoardStillnessBudgetSeconds;

            ServiceHub.Save.RequestAutosave(Save.SaveReason.NightStart);
            Log.Info("Loop", "night " + nightIndex + " begins");
        }

        /// <summary>
        /// Starts a night outright, the way AdvanceToNextNight would (GDD 20.21).
        ///
        /// The console's night.set only ever moved GameStateService and CaseService, which is
        /// fine for testing a case but silently useless for testing a shift: the visitor
        /// roster, resident traffic, the CCTV schedule, the threat windows and both of the
        /// services that act on their own (15.4 / 15.5) are all started here, not there. A
        /// playtester jumping to night 6 the old way got a night with no pressure and a full
        /// reserve, which is exactly the night they were trying not to test.
        /// </summary>
        public bool DebugStartNight(int nightIndex)
        {
            if (!DevConsole.Enabled) return false;

            // The console opens on the main menu too, and StartNight assumes a world to start
            // it in: without this a tester who types the command before pressing New Game gets
            // a shift running over an empty scene rather than a refusal.
            if (!_worldBuilt) return false;

            StartNight(nightIndex);
            SetMode(GameMode.Playing);
            return true;
        }

        /// <summary>
        /// Leaves the night summary the way pressing Continue does (GDD 28.3).
        ///
        /// The summary is a dead end without it: the only route onward is a button, and a
        /// smoke test that has to find and click a Button in a code-built canvas is testing
        /// the widget kit rather than the shift it meant to check. Unlike DebugStartNight this
        /// takes no shortcut through the flow - it is the same call the button makes.
        /// </summary>
        public void DebugAdvanceNight() { AdvanceToNextNight(); }

        /// <summary>
        /// Walk onto a shift somebody else already started (v3.0 34).
        ///
        /// The host is running the night; this copy needs the building around it, a body
        /// standing in the office and its screens live. What it must NOT do is any of the
        /// things that decide something - no ResetPlaythrough, no StartNight, no case
        /// scheduling. It is arriving at work, not opening the building.
        /// </summary>
        public void JoinShiftInProgress(int nightIndex)
        {
            // Already on this night: nothing to do. Already playing a *different* one means
            // the host has moved on - a night ended and the next one opened while this copy
            // was still standing in the old one - and that has to be followed, not ignored.
            if (_mode == GameMode.Playing && ServiceHub.State.NightIndex == nightIndex) return;

            StartCoroutine(JoinShiftRoutine(nightIndex));
        }

        IEnumerator JoinShiftRoutine(int nightIndex)
        {
            yield return EnsureWorldRoutine();

            // The night index is the host's. State is set directly rather than through
            // BeginNight, which would re-run the night's scheduling on a machine that has no
            // business scheduling anything.
            ServiceHub.State.BeginNight(nightIndex);

            var spawn = WorldBuilder.OfficeSpawn;
            if (spawn != null && _player != null)
                _player.Teleport(spawn.position + ShiftSpawnOffset(), spawn.rotation);
            ServiceHub.Player.EnterZone(ZoneIds.Office);

            SetMode(GameMode.Playing);
            Log.Info("Net", "joined a shift already in progress (night " + nightIndex + ")");
        }

        /// <summary>
        /// Where this caretaker stands when the shift opens.
        ///
        /// The office has one spawn point and up to four people arriving on it. Three
        /// CharacterControllers in the same cubic metre shove each other across the room on
        /// the first frame, which reads as the game being broken before anybody has moved. A
        /// metre apart along the desk is enough, and in single player the offset is zero.
        /// </summary>
        static Vector3 ShiftSpawnOffset()
        {
            var id = ServiceHub.Presence.LocalPlayerId;
            int index = 0;
            if (!string.IsNullOrEmpty(id) && id.Length > 1)
                int.TryParse(id.Substring(1), out index);

            index = index > 0 ? index - 1 : 0;
            if (index == 0) return Vector3.zero;

            // Alternating sides, so nobody is pushed further from the desk than they need to be.
            float step = 0.9f * ((index + 1) / 2);
            return new Vector3(index % 2 == 1 ? step : -step, 0f, 0f);
        }

        /// <summary>Access levels unlock on a fixed schedule (GDD 15.3).</summary>
        void GrantScheduledAccess(int nightIndex)
        {
            var state = ServiceHub.State;

            for (int i = 0; i < AllAccessLevels.Length; i++)
                if (GrantsAccessOnNight(AllAccessLevels[i], nightIndex, state.GetFlag(FlagIds.TaehoCooperates)))
                    state.GrantAccess(AllAccessLevels[i]);
        }

        static readonly AccessLevel[] AllAccessLevels =
        {
            AccessLevel.Staff1, AccessLevel.Staff2, AccessLevel.Maintenance, AccessLevel.Archive
        };

        /// <summary>
        /// The access schedule itself (GDD 15.3), as a pure function so it can be checked
        /// without a scene.
        ///
        /// It is a function rather than four lines inside GrantScheduledAccess because of what
        /// happened when the cold open landed: Staff-2 opens the basement, the basement holds
        /// the breaker board, and the cold open made throwing that board night 1's first
        /// objective - while this schedule still handed Staff-2 out on night 2. Both routes
        /// down are gated (the lift button and the stairwell plate), so night 1's compulsory
        /// errand was behind a locked door and the camera wall could never come back. Nothing
        /// caught it: the smoke test pushes the clock to 06:00 and the fail-safe closes C00
        /// on its own, so a shift that cannot be played still ends.
        ///
        /// Now the rule is testable on its own, and EditMode asserts the one thing that must
        /// stay true - a night may not require a room it has not been given the key to.
        /// </summary>
        public static bool GrantsAccessOnNight(AccessLevel level, int nightIndex, bool taehoCooperates)
        {
            switch (level)
            {
                case AccessLevel.Staff1:
                    return true;

                // Basement plant room and the roof. Night 1, not night 2: the cold open sends
                // the caretaker to the breaker board in the first twenty minutes (GDD 9.1),
                // and a night watchman who cannot open their own plant room was always a
                // schedule written for a tutorial that no longer exists. The records room is
                // gated separately by AccessLevel.Archive, so this opens no story door early.
                case AccessLevel.Staff2:
                    return nightIndex >= 1;

                case AccessLevel.Maintenance:
                    return nightIndex >= 3;

                // Archive opens through Taeho's cooperation on night 4, or by night 5
                // regardless, so case C11 can never be locked out of its own location.
                case AccessLevel.Archive:
                    return nightIndex >= 5 || (nightIndex >= 4 && taehoCooperates);
            }

            return false;
        }

        /// <summary>Scripted state that is true the moment a night opens (GDD 9.x).</summary>
        void ApplyNightOpeningState(int nightIndex)
        {
            var state = ServiceHub.State;

            if (nightIndex == 1) state.SetFlag(FlagIds.SunjaHealthy, true);

            // GDD 9.1 - the cold open, and the first thing in the game.
            //
            // The old night 1 opened on a memo about a part-charged reserve. That is a budget
            // notice, and a budget notice is what a shift says when nothing is happening. This
            // shift opens with nothing on the camera wall at all: twelve channels cut, a
            // reserve already under the warning line (NightPowerService says that part out
            // loud on its own now), and a radio that has been repeating a three-day-old
            // transmission into an empty room since before the player unlocked the door.
            //
            // Every one of those points at the same place. The basement board is the only
            // thing in the building that answers any of it, and walking to it is what teaches
            // the game - including the part where the front door rings while nobody is at the
            // desk (GDD 15.5).
            //
            // Endless runs replay night 1's roster but not its opening: an endless caretaker
            // has heard this transmission already.
            if (nightIndex == 1 && !state.EndlessMode)
            {
                ServiceHub.Cctv.CutAllFeeds();
                EventBus.Publish(new NotificationEvent("ui.notify.feeds_cut",
                                                      NotificationSeverity.Urgent));
                ServiceHub.Dialogue.Start("D_PROLOGUE_RADIO");
            }

            // Night 4, 22:00: the database sync makes unit 404 searchable (GDD 9.5).
            if (nightIndex >= 4) state.SetFlag(FlagIds.Knows404, true);

            // Night 5: the overload puts every major system on one budget (GDD 9.6).
            if (nightIndex == 5)
            {
                for (int i = 0; i < CircuitIds.All.Length; i++)
                    ServiceHub.Facility.RegisterCircuit(CircuitIds.All[i], i < CircuitIds.MaxSimultaneous);
            }
        }
        /// <summary>
        /// Tonight's callers, filed under the beat that brings them (GDD 13.4, 15.7).
        ///
        /// There is no clock in here, and that is the whole design. A caller used to carry an
        /// arrival time; then the times were replaced by shares of the night's cases and the
        /// shares were still handed out as timestamps a few minutes into the future. Both are
        /// the same mistake in different clothes: a shift is paced by the caretaker, so any
        /// arrival written in advance is an arrival for a moment that may already have gone.
        /// A player who cleared night 1 by 22:05 finished every task and then stood in a
        /// finished building while the door kept going.
        ///
        /// So a caller is not scheduled. They are handed to the interphone the instant their
        /// beat happens, and the interphone queue - which already exists and already holds
        /// people in line - is what stops three arrivals at once from being three people
        /// standing at the door. Nothing is ever owed to a time that has passed, because
        /// nothing is ever owed to a time.
        ///
        /// Keyed by case id for the beats that are cases, and by the two keys below for the
        /// two beats that are not.
        /// </summary>
        readonly Dictionary<string, List<string>> _callersByBeat = new Dictionary<string, List<string>>();

        /// <summary>The beat that is the camera wall coming back, rather than a case (GDD 9.1).</summary>
        const string FeedsRestoredBeat = "@feeds_restored";

        /// <summary>Callers not yet released, so the shift knows somebody is still owed.</summary>
        public int PendingVisitorCount
        {
            get
            {
                int waiting = 0;
                foreach (var pair in _callersByBeat) waiting += pair.Value.Count;
                return waiting;
            }
        }

        /// <summary>File a caller under the beat that brings them.</summary>
        void CallerOn(string beat, string visitorId)
        {
            List<string> callers;
            if (!_callersByBeat.TryGetValue(beat, out callers))
                _callersByBeat[beat] = callers = new List<string>();

            callers.Add(visitorId);
        }

        /// <summary>
        /// The beat happened: everyone filed under it goes to the door now.
        ///
        /// All of them, at once, with no stagger. Two callers on one beat means the second
        /// waits behind the first in the interphone queue, which is what a queue is for and
        /// costs the caretaker exactly the time it takes them to deal with the first.
        /// </summary>
        void ReleaseCallersOn(string beat)
        {
            List<string> callers;
            if (!_callersByBeat.TryGetValue(beat, out callers)) return;

            _callersByBeat.Remove(beat);
            for (int i = 0; i < callers.Count; i++) ServiceHub.Interphone.Enqueue(callers[i]);

            if (callers.Count > 0)
                Log.Info("Loop", callers.Count + " caller(s) arrive on " + beat);
        }

        /// <summary>
        /// Night-specific scripted beats that are not case-driven: visitors arriving and the
        /// baseline access log every night starts from.
        /// </summary>
        void SeedNightSchedule(int nightIndex)
        {
            var log = ServiceHub.AccessLog;
            _callersByBeat.Clear();

            log.Add(ShiftTime(21, 40), Residents.AccessSubject.Resident, "resident.1102.name",
                    "RES-1102", "log.location.lobby", "CAM-02", false);
            log.Add(ShiftTime(21, 52), Residents.AccessSubject.Resident, "resident.803.name",
                    "RES-803", "log.location.lobby", "CAM-02", true);

            // The scripted callers go first. They are timed against named case beats rather
            // than against a share of the night, so they claim their place before anything is
            // divided up - and PlanCallers then skips whoever is already spoken for.
            //
            // That ordering is the whole fix. These callers carry a real nightIndex now,
            // because the night somebody arrives on is a fact about them and every count of a
            // night's door was blind to the plot without it (GDD 13.4). Rostering them a
            // second time would put them at the door twice.
            SeedScriptedCallers(nightIndex);

            switch (nightIndex)
            {
                case 1:
                    // Dong-sik badged onto the fourth floor three days ago and never badged
                    // out. The log carries it into the shift the player is standing in.
                    log.Add(ShiftTime(22, 5), Residents.AccessSubject.Staff, "resident.dongsik.name",
                            "STAFF-02", "log.location.floor04", "CAM-04", true, "grp_dongsik");

                    break;

                case 2:
                    // The lobby entry at 22:18 has no matching door release - that gap is the
                    // contradiction the player is meant to find (GDD 9.3).
                    log.Add(ShiftTime(22, 18), Residents.AccessSubject.Unknown, "visitor.junho.name",
                            "", "log.location.lobby", "CAM-02", true, "grp_junho");
                    break;

                case 3:
                    log.Add(ShiftTime(22, 34), Residents.AccessSubject.Staff, "resident.taeho.name",
                            "STAFF-07", "log.location.floor04", "CAM-05", true, "grp_taeho");
                    break;

                case 4:
                    log.Add(ShiftTime(22, 15), Residents.AccessSubject.Resident, "resident.1501.name",
                            "RES-1501", "log.location.lobby", "CAM-02", true);
                    break;

                case 5:
                    // The chairman reaches the archive corridor without ever badging in.
                    log.Add(ShiftTime(23, 18), Residents.AccessSubject.Unknown, "resident.1501.name",
                            "", "log.location.parking", "CAM-10", true, "grp_chairman");
                    break;

                case 6:
                    log.Add(ShiftTime(21, 58), Residents.AccessSubject.Staff, "resident.taeho.name",
                            "STAFF-07", "log.location.lobby", "CAM-02", true);
                    log.Add(ShiftTime(22, 6), Residents.AccessSubject.Resident, "resident.1501.name",
                            "RES-1501", "log.location.lobby", "CAM-02", true);
                    break;
            }

            // dev.mainonly: the roster below is the night's ordinary door traffic and none of
            // it belongs to a main quest, so none of it is placed. SeedScriptedCallers has
            // already kept whichever named callers the main is actually made of. The access
            // log above stays either way - it is what the main reads, not something that
            // arrives at the door.
            if (MainOnlyMode.Active) return;

            // Endless runs cycle the authored roster rather than running out of callers after
            // night 6, the same way the anomaly schedule does.
            int rosterNight = ServiceHub.State.EndlessMode ? 1 + (Mathf.Max(0, nightIndex) % 6) : nightIndex;

            // The authored arrival time still decides the ORDER these callers come in - a
            // designer put the refusable ones where they wanted them in the night - and no
            // longer decides when. PlanCallers turns that order into shares of the night's
            // cases.
            var roster = new List<VisitorDefinition>();
            foreach (var visitor in ServiceHub.Content.Visitors)
            {
                if (visitor == null || visitor.nightIndex != rosterNight) continue;
                if (AlreadyPlaced(visitor.visitorId)) continue;
                roster.Add(visitor);
            }
            roster.Sort((a, b) =>
            {
                int byTime = a.arrivalGameSecond.CompareTo(b.arrivalGameSecond);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.visitorId, b.visitorId);
            });

            PlanCallers(roster);
        }

        /// <summary>
        /// The callers a night has written into it by name, filed against the case beat they
        /// belong to rather than against a clock time.
        /// </summary>
        void SeedScriptedCallers(int nightIndex)
        {
            switch (nightIndex)
            {
                case 1:
                    // dev.mainonly: all four of night 1's callers are the ordinary job the
                    // main is hidden inside rather than the main itself - N1-M01 is the 304
                    // noise and the bill behind it, and none of these four is in it. They are
                    // filed against its beat for pacing, which is exactly what this mode is
                    // taking away.
                    if (MainOnlyMode.Active) break;

                    // v5.0 10. Night 1 is still meant to look like a job, so the two ordinary
                    // callers are at the door early - the player has to be shown what ordinary
                    // looks like before anything is allowed to be strange.
                    // v5.1 4.2 puts the main fourth, so "early" is the first job of the
                    // shift rather than the main's beat.
                    string firstBeat = ServiceHub.Cases.NightChain.Count > 0
                        ? ServiceHub.Cases.NightChain[0] : "N1-M01";
                    CallerOn(firstBeat, "vis_n0_guest_jiwoo");
                    CallerOn(firstBeat, "vis_n0_courier");

                    // The parcel for the vacant unit waits for the camera wall. One of the
                    // things its invoice has to be checked against is the vehicle log on
                    // CAM-01, and asking for that judgement while the camera is dark is asking
                    // for a guess and then charging for it.
                    CallerOn(FeedsRestoredBeat, "vis_courier_late");

                    // Min-seo arrives while the caretaker is on the third floor recording the
                    // noise from 304 - the same scene a second time, now that they know it.
                    CallerOn("N1-R01", "vis_minseo");
                    break;

                case 2:
                    // v5.0 11. Both Jun-hos belong to the night's main quest, and the second
                    // one is the contradiction, so he comes with it rather than later. Which
                    // is also why dev.mainonly leaves them alone: strip these two and N2-M01
                    // is a quest about two couriers with nobody at the door.
                    CallerOn("N2-M01", "vis_junho_real");
                    CallerOn("N2-M01", "vis_junho_second");
                    break;
            }
        }

        /// <summary>True when this caller has already been filed against a beat tonight.</summary>
        bool AlreadyPlaced(string visitorId)
        {
            foreach (var pair in _callersByBeat)
                if (pair.Value.Contains(visitorId)) return true;
            return false;
        }

        /// <summary>
        /// Divide tonight's routine callers among tonight's cases.
        ///
        /// The last case always gets exactly one, because that is the promise: when the final
        /// job of the night begins there is one person left to deal with, and they arrive as it
        /// begins. Everything before that is spread as evenly as the numbers allow, with the
        /// remainder going to the earliest cases - the front of a shift is when a caretaker has
        /// the most slack, and the back of it is when they are on the fourth floor with a torch.
        ///
        /// A caller with no case to hang on is filed under the first one rather than given a
        /// time. There is no time to give them.
        /// </summary>
        void PlanCallers(List<VisitorDefinition> roster)
        {
            if (roster.Count == 0) return;

            var chain = ServiceHub.Cases.NightChain;
            if (chain.Count == 0)
            {
                // A night with no queued work still has a door. Everyone comes with the shift.
                for (int i = 0; i < roster.Count; i++) CallerOn("@shift_start", roster[i].visitorId);
                return;
            }

            var share = new int[chain.Count];
            share[chain.Count - 1] = 1;

            if (chain.Count == 1)
            {
                share[0] = roster.Count;
            }
            else
            {
                int toSpread = roster.Count - 1;
                int earlier = chain.Count - 1;
                int each = toSpread / earlier;
                int remainder = toSpread % earlier;

                for (int i = 0; i < earlier; i++) share[i] = each + (i < remainder ? 1 : 0);
            }

            int next = 0;
            for (int i = 0; i < chain.Count && next < roster.Count; i++)
                for (int k = 0; k < share[i] && next < roster.Count; k++)
                    CallerOn(chain[i], roster[next++].visitorId);

            Log.Info("Loop", "callers tonight: " + roster.Count + " across " + chain.Count +
                             " case(s), one held for the last");
        }

        /// <summary>Releases scheduled callers to the interphone as their arrival time passes.</summary>
        void TickVisitorSchedule()
        {
            // Nothing here is waiting for a time any more - callers go to the door on their
            // beat. What is left is the one promise a beat cannot make on its own: a beat that
            // never happens must not keep somebody permanently un-arrived, because the shift
            // counts them and could then never close (GDD 6.3 step 8).
            //
            // The cold open's parcel is the case in point. It waits for the camera wall, and a
            // caretaker who somehow closes every job without ever throwing the breaker would
            // otherwise be holding a caller who is never coming.
            if (_callersByBeat.Count == 0 || !NoCaseLeftToStart()) return;

            var stranded = new List<string>(_callersByBeat.Keys);
            for (int i = 0; i < stranded.Count; i++) ReleaseCallersOn(stranded[i]);

            // The handset is owed the same: a call whose case never opened still has to ring.
            ServiceHub.Phone.ReleaseCallsWaitingOnCases();
        }

        /// <summary>
        /// True when no case tonight is still waiting to be handed out.
        ///
        /// The chain is the only thing that releases callers, so this is the moment it stops
        /// being able to: either every job is closed, or the queue is stuck on one that cannot
        /// start. Both mean the remaining callers have to be let go some other way.
        /// </summary>
        bool NoCaseLeftToStart()
        {
            // Tonight's roster for the same reason CurrentShiftBlocker reads it: an undrawn
            // case is Dormant forever, and a caller waiting on a beat that will never happen
            // would then never be let go.
            foreach (var runtime in ServiceHub.Cases.RosterTonight)
                if (runtime.State == CaseState.Dormant) return false;

            return true;
        }

        static int ShiftTime(int hour, int minute)
        {
            int normalized = hour < 12 ? hour + 24 : hour;
            return normalized * 3600 + minute * 60;
        }

        /// <summary>
        /// Endless runs end one way: the caretaker is dismissed. This is also the only thing
        /// in the game that ever reads Performance as a threshold - it sat at 60 for seven
        /// nights doing nothing, and here it is the whole clock.
        /// </summary>
        const int DismissalPerformance = 0;

        void AdvanceToNextNight()
        {
            // Opening the next shift is the host's to do, and only once. A client's Continue
            // asks for it and then waits: the night index is a NetworkVariable, so the answer
            // arrives as FollowNight and puts them back on the floor with everybody else.
            if (!Net.NetSession.Authoritative)
            {
                Net.NetShift.Request(Net.NetShift.ShiftAct.AdvanceNight);
                return;
            }

            int next = ServiceHub.State.NightIndex + 1;

            if (ServiceHub.State.EndlessMode)
            {
                if (ServiceHub.State.GetStat(StatIds.Performance) <= DismissalPerformance)
                {
                    EndEndlessRun();
                    return;
                }

                StartNight(next);
                SetMode(GameMode.Playing);
                return;
            }

            if (next > FinalNight)
            {
                ShowEnding();
                return;
            }

            StartNight(next);
            SetMode(GameMode.Playing);
        }

        /// <summary>
        /// A colleague pressed Continue on the night summary (v3.0 46.1).
        ///
        /// Guarded on the mode rather than trusted: three people press the button, and only
        /// the first press should open the next shift. The other two arrive to find the host
        /// already playing and are refused here.
        /// </summary>
        public void AdvanceNightForShift()
        {
            if (!Net.NetSession.Authoritative) return;
            if (_mode != GameMode.NightSummary) return;

            AdvanceToNextNight();
        }

        /// <summary>Starts an endless run from a clean slate.</summary>
        public void StartEndless()
        {
            if (!Net.NetSession.Authoritative) return;
            StartCoroutine(StartEndlessRoutine());
        }

        IEnumerator StartEndlessRoutine()
        {
            ServiceHub.ResetPlaythrough();
            yield return EnsureWorldRoutine();
            _endingResolved = false;

            ServiceHub.State.SetEndlessMode(true);

            var spawn = WorldBuilder.OfficeSpawn;
            if (spawn != null) _player.Teleport(spawn.position, spawn.rotation);
            ServiceHub.Player.EnterZone(ZoneIds.Office);

            StartNight(1);   // the campaign and an endless run now open on the same night
            SetMode(GameMode.Playing);
            ServiceHub.Analytics.Track("endless_started");
        }

        void EndEndlessRun()
        {
            var state = ServiceHub.State;
            int nights = state.NightIndex;

            // NightIndex is still the shift that just finished (State.EndNight does not
            // advance it), and endless runs start at 1 - so it is already the count of shifts
            // worked, not one more than it.
            int survived = Mathf.Max(0, nights);

            var settings = ServiceHub.Settings.Current;
            bool record = survived > settings.endlessBestNights;
            if (record)
            {
                settings.endlessBestNights = survived;
                ServiceHub.Settings.Save();
            }

            ServiceHub.Analytics.Track("endless_ended", null, survived);
            Log.Info("Loop", "endless run over after " + survived + " nights");

            _endlessResult.Populate(survived, settings.endlessBestNights, record);
            state.SetEndlessMode(false);

            ServiceHub.Audio.PlayAmbience(AudioCue.None);
            SetMode(GameMode.EndlessResult);
        }

        void ReturnToMenu()
        {
            ServiceHub.Audio.PlayAmbience(AudioCue.None);
            SetMode(GameMode.Menu);
            // The office/lobby core stays resident; every streamed floor is dropped here so a
            // session that returns to the menu does not keep every visited floor in memory
            // (GDD 20.5).
            ServiceHub.Zones.UnloadAllStreamed();
        }

        void QuitGame()
        {
            ServiceHub.Settings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- modes ---------------------------------------------------------

        void SetMode(GameMode mode)
        {
            var previous = _mode;
            _mode = mode;

            bool playing = mode == GameMode.Playing;

            // Publish() is the only writer of the shared night index and it only runs while
            // the host is playing, so leaving the shift has to say so once. Without this a
            // caretaker who joins during the night summary is dropped into a frozen copy of
            // the night that just ended, and the real next night never reaches them.
            if (!playing && previous == GameMode.Playing && Net.NetSession.Authoritative)
                Net.NetShift.ClearNight();

            _menu.SetOpen(mode == GameMode.Menu);
            _pause.SetOpen(mode == GameMode.Paused);
            _summary.SetOpen(mode == GameMode.NightSummary);
            _endlessResult.SetOpen(mode == GameMode.EndlessResult);
            _ending.SetOpen(mode == GameMode.Ending);
            _gallery.SetOpen(mode == GameMode.Gallery);
            _credits.SetOpen(mode == GameMode.Credits);
            _hud.SetVisible(playing);
            _pressureHud.SetVisible(playing);

            _input.SetGameplayEnabled(playing);

            RefreshPlayerControl();
        }

        /// <summary>
        /// True while a face-to-face/phone/radio conversation is on screen and needs mouse
        /// clicks (GDD 16.9 choice buttons). Interphone chatter lives inside the PC app, whose
        /// own IsOpen check already frees the cursor, so it is excluded here.
        /// </summary>
        bool DialogueBlockingView()
        {
            return ServiceHub.Dialogue.IsActive &&
                   ServiceHub.Dialogue.Current.channel != Dialogue.DialogueChannel.Interphone;
        }

        /// <summary>
        /// Single source of truth for "can the player look/move/interact right now" and
        /// whether the cursor should be free for UI clicks. Called from every state change
        /// that can affect either input path, including dialogue starting or ending.
        /// </summary>
        void RefreshPlayerControl()
        {
            bool playing = _mode == GameMode.Playing;
            bool dialogueBlocking = DialogueBlockingView();

            if (_player != null)
                _player.SetControlEnabled(playing && !_pc.IsOpen && !_tablet.IsOpen &&
                                          !dialogueBlocking && !_toolMenu.IsOpen);

            ApplyCursor();
        }

        void ApplyCursor()
        {
            bool freeCursor = _mode != GameMode.Playing || _pc.IsOpen || _console.IsOpen ||
                              DialogueBlockingView() || _toolMenu.IsOpen;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freeCursor;
        }

        /// <summary>
        /// Public so the office terminal can call it directly as a fallback if its OnActivated
        /// delegate was ever left unwired (GDD 21: a single wiring race must never soft-lock
        /// the only way back into the facility PC).
        /// </summary>

        public void OpenPc(bool open)
        {
            _pc.SetOpen(open);
            ServiceHub.Player.SetPcMode(open);
            RefreshPlayerControl();
        }

        void TeleportToZone(string zoneId) { StartCoroutine(TeleportToZoneRoutine(zoneId)); }

        IEnumerator TeleportToZoneRoutine(string zoneId)
        {
            yield return EnsureWorldRoutine();

            // The dev console can jump anywhere, so it has to pull the scene in first.
            ServiceHub.Zones.RequestZone(zoneId);

            var spawn = ZoneRegistry.FindSpawn(zoneId);
            if (spawn == null)
            {
                Log.Warn("Loop", "zone " + zoneId + " is still streaming; try again in a moment");
                yield break;
            }

            _player.Teleport(spawn.position, spawn.rotation);
            ServiceHub.Player.EnterZone(zoneId);
            Net.NetShift.Request(Net.NetShift.ShiftAct.EnterZone, zoneId);
        }

        /// <summary>
        /// Standing at a machine takes the caretaker out of the room the same way the PC and
        /// the tablet do (spec 23): look and movement stop, and the cursor comes back for the
        /// menu. Driven off the event rather than polled, so the panel opening and control
        /// changing hands are the same moment.
        /// </summary>
        /// <summary>
        /// What this caretaker is costing the night reserve (GDD 15.5, v3.0 34).
        ///
        /// Written into the presence roster rather than straight onto the power service,
        /// because with three people on shift there are three torches and three screens and
        /// the meter has to charge for all of them. NetPlayer carries everybody else's.
        /// </summary>
        void ReportPowerLoad()
        {
            int screens = 0;
            if (ServiceHub.Player.InPcMode && ServiceHub.Player.CurrentAppId == AppIds.Cctv)
                screens = ServiceHub.Cctv.GridMode ? 1 : 2;

            ServiceHub.Presence.ReportLoad(ServiceHub.Presence.LocalPlayerId,
                                           _player != null && _player.FlashlightOn,
                                           screens);
        }

        /// <summary>The local caretaker's torch and screens, packed for the wire.</summary>
        public byte LocalPowerLoad
        {
            get
            {
                var load = ServiceHub.Presence.LoadOf(ServiceHub.Presence.LocalPlayerId);
                return (byte)((load.Torch ? 4 : 0) | (load.Screens & 3));
            }
        }

        /// <summary>Whether tonight's "you can go home" line has already been said.</summary>
        bool _saidTheWorkIsDone;

        /// <summary>
        /// Say so when there is nothing left to do.
        ///
        /// The night's work is a queue, and a queue that has run out looks exactly like a queue
        /// that is stuck: the last card closes, no new card arrives, and the caretaker is left
        /// standing in the office deciding whether the game is broken. It is the same silence
        /// either way, and only one of them is correct.
        ///
        /// So the moment the shift actually becomes closeable, it is said out loud - once, and
        /// only once a night. Everything that could still be holding it open - a caller on the
        /// way, somebody at the door, a case the queue has not reached - already has its own
        /// message on the clock-off button, so this only fires when none of them do.
        /// </summary>
        void AnnounceWhenTheWorkIsDone()
        {
            if (_saidTheWorkIsDone) return;

            // Once for the shift, not once per caretaker. Three people are told the work is
            // done because the host says so; the caption rides over with the notice.
            if (!Net.NetSession.Authoritative) return;
            if (CurrentShiftBlocker != ShiftBlocker.None) return;

            _saidTheWorkIsDone = true;
            EventBus.Publish(new NotificationEvent("ui.notify.shift_complete",
                                                   NotificationSeverity.Task));
            ServiceHub.Captions.Ambient("caption.shift_complete");
            Net.NetShift.Broadcast(Net.NetShift.Moment.CaptionAmbient, "caption.shift_complete");
        }

        void OnToolSessionChanged(AnomalyToolSessionEvent evt)
        {
            RefreshPlayerControl();
        }

        /// <summary>
        /// The order the cold open is supposed to teach: fix the feeds, then judge the caller.
        ///
        /// The parcel for the vacant unit is the first thing in the game that can be got wrong
        /// at a price, and getting it right means checking four records - one of which is the
        /// vehicle log on CAM-01. Sending them to the door while the wall is dark asks the
        /// caretaker to make a judgement out of three facts and a guess, then charges them for
        /// the guess. The breaker coming back is what makes the question answerable, so it is
        /// what asks it.
        /// </summary>
        void OnObjectiveDone(ObjectiveChangedEvent evt)
        {
            if (!Net.NetSession.Authoritative) return;
            if (evt.ObjectiveId != "obj_restore_feeds" || !evt.Completed) return;
            ReleaseCallersOn(FeedsRestoredBeat);
        }

        void OnEvidenceAcquired(EvidenceAcquiredEvent evt)
        {
            ServiceHub.Audio.PlayCue(AudioCue.EvidenceTaken);

            // The key Dongsik left is what grants 404 access (GDD 15.3).
            // The key Dong-sik left is what grants 404 access (GDD 15.3). It used to also fire
            // the prologue's closing knock; that beat now belongs to coming back from the
            // basement (OnZoneChanged), which is an errand the shift forces rather than a prop
            // the player might never pick up.
            if (evt.EvidenceId == "EV_KEY404") ServiceHub.State.GrantAccess(AccessLevel.Key404);

            CheckAllEvidenceTypesAchievement();
        }

        /// <summary>GDD 14.1 lists 8 evidence types; this fires once the player owns one of each.</summary>
        void CheckAllEvidenceTypesAchievement()
        {
            const int typeCount = 8; // Evidence.EvidenceType has 8 members
            var seen = new bool[typeCount];
            int remaining = typeCount;

            foreach (var pair in ServiceHub.Evidence.Owned)
            {
                var definition = pair.Value.Definition;
                if (definition == null) continue;

                int index = (int)definition.type;
                if (index < 0 || index >= seen.Length || seen[index]) continue;

                seen[index] = true;
                if (--remaining == 0) break;
            }

            if (remaining == 0) ServiceHub.Steam.Unlock(AchievementIds.AllEvidenceTypes);
        }

        void OnEvidenceLinked(EvidenceLinkedEvent evt)
        {
            ServiceHub.Steam.Unlock(AchievementIds.LinkedTruth);
        }

        void OnStatChanged(StatChangedEvent evt)
        {
            if (evt.StatId == StatIds.CommunityTrust && evt.Current >= StatIds.MaxOf(StatIds.CommunityTrust))
                ServiceHub.Steam.Unlock(AchievementIds.TrustedCaretaker);
            else if (evt.StatId == StatIds.BuildingSafety && evt.Current >= StatIds.MaxOf(StatIds.BuildingSafety))
                ServiceHub.Steam.Unlock(AchievementIds.SafeHouse);
        }

        // ---- frame ---------------------------------------------------------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Before the mode check, not after: Steam's callbacks have to be pumped in the
            // menu and while paused too, or the overlay stops responding the moment the
            // player opens it - which is most of the time they will.
            ServiceHub.Steam.Tick();

            if (_console != null && _input.Console.WasPressedThisFrame() && DevConsole.Enabled)
            {
                _console.Toggle();
                ApplyCursor();
            }

            if (_input.Pause.WasPressedThisFrame()) HandlePauseKey();

            if (_mode != GameMode.Playing)
            {
                _dialogue.Refresh();
                return;
            }

            HandleHotkeys();

            ServiceHub.Clock.TimeScale = CurrentTimeScale();
            // Only one machine advances the night (v3.0 46.1).
            //
            // Everything in TickShift writes authoritative state - the clock, case progress,
            // what a visitor upstairs is doing - and two copies running it side by side drift
            // apart inside a minute. A joining caretaker gets the clock and the door from
            // NetShift instead. Their world, their torch and their footsteps stay their own,
            // which is why zone streaming is not in there.
            if (Net.NetSession.Authoritative)
            {
                TickShift(dt);
                if (Net.NetShift.Instance != null) Net.NetShift.Instance.Publish();
            }

            ServiceHub.Zones.Tick();

            // Drawing the visitors is not advancing them, so it happens on every machine: a
            // joining caretaker watches the same corridors, off the board the host mirrors to
            // them (v3.0 46.1). It also has to run after the streamer, or a visitor on a floor
            // that arrived this frame waits one more frame for a body.
            if (_visitorBodies != null) _visitorBodies.Tick(dt);

            // Same split for the neighbours. The host schedules who walks past which camera
            // and wrote it into the log; every copy only has to keep them walking, or a client
            // watches twelve corridors in which people appear and then stand perfectly still.
            //
            // The rewind buffer is the same shape of thing: a record of channel states that
            // arrived rather than were decided, so every copy fills its own.
            if (!Net.NetSession.Authoritative)
            {
                ServiceHub.Traffic.TickActorsOnly(dt);
                ServiceHub.Cctv.SampleFeedsOnly();
            }

            // Both of these are about one person's night rather than about the shift - a hint
            // for whoever is stuck, footsteps for whoever is carrying enough distortion to
            // hear them - so they run on every machine off state the mirror already carries.
            ServiceHub.Distortion.Tick();
            ServiceHub.Hints.Tick(Time.realtimeSinceStartup);

            // The service has no business reaching into the scene for a Light component.
            ServiceHub.Power.FlashlightOn = _player != null && _player.FlashlightOn;
            ReportPowerLoad();

            // A patrol close enough to hear is a caption, not just a sound (GDD 16.17).
            if (ServiceHub.Threat.IsActive && ServiceHub.Threat.Awareness > 0.25f)
                ServiceHub.Captions.Ambient("caption.footsteps");

            if (_player != null) _player.Tick(dt);

            AnnounceWhenTheWorkIsDone();

            _dialogue.Refresh();
            _toolMenu.Refresh();
            _hud.Refresh(_player != null ? _player.Raycaster : null, AnyInputThisFrame());
            _pressureHud.Refresh();

            // A lost shift is restarted from Update rather than from the event handler that
            // decided it: the handler runs inside a stalker collision, and tearing the night
            // down underneath it destroys the object mid-callback.
            //
            // Host only. When a shift ends is a decision, and three machines deciding it
            // independently off a mirror that is up to half a second old would end the night
            // three times at three different clocks. The others are told (FollowNightEnded).
            if (Net.NetSession.Authoritative && ShouldEndNight()) EndNight();
        }

        /// <summary>
        /// The shift ends at 06:00, or as soon as the last night's final case is closed -
        /// there is nothing left to do once the submission is made.
        /// </summary>
        bool ShouldEndNight()
        {
            if (ServiceHub.Clock.ShiftOver) return true;

            if (ServiceHub.State.NightIndex != FinalNight) return false;

            var finalCase = ServiceHub.Cases.Find("N6-M01");
            return finalCase != null && finalCase.State == CaseState.ConsequenceApplied;
        }

        /// <summary>
        /// GDD 6.3 step 8: the shift is over when the work is done, not when the clock says so.
        /// Every case scheduled for tonight has to be closed and nobody may be mid-sentence -
        /// otherwise clocking off would silently abandon a visitor at the door.
        ///
        /// This is offered, never forced: some of the night's evidence sits outside any case
        /// (the 2009 maintenance bill in the records room is only ever picked up voluntarily,
        /// and night 6 needs it), so ending early stays the player's decision.
        /// </summary>
        /// <summary>
        /// Why the caretaker cannot clock off yet, or None when they can.
        ///
        /// This exists because the button used to have one message for every reason - "남은
        /// 업무가 있습니다", tasks still open - and one of those reasons is not the player's
        /// fault at all. A shift schedules work across the whole night: night 1 does not
        /// hand out T03 until 00:10 and has callers booked until 01:35. Before those land the
        /// old check counted them as unfinished, so a player who had genuinely done every
        /// single thing available to them was told they had left something open, with no way
        /// to find out what. The game was lying to them.
        ///
        /// Blocking is still right - a shift is a shift, and clocking off at 23:00 would
        /// abandon four callers at the door. Telling the truth about why is the fix.
        /// </summary>
        public enum ShiftBlocker
        {
            None = 0,
            /// <summary>Mid-conversation. Walking out of a sentence is not clocking off.</summary>
            InConversation,
            /// <summary>The handset is ringing or live.</summary>
            OnTheLine,
            /// <summary>Somebody is at the front door right now.</summary>
            AtTheDoor,
            /// <summary>Work the player accepted and has not closed. This one IS on them.</summary>
            CaseOpen,
            /// <summary>Work tonight has not handed out yet. Not the player's fault.</summary>
            NotYetScheduled,
            /// <summary>The shift is already over, or there is nothing rostered tonight.</summary>
            NotOnShift
        }

        /// <summary>
        /// What is holding the shift open. Ordered so the most actionable reason wins: a case
        /// the player has left open outranks one the night has not issued yet, because only
        /// the first is something they can go and do something about.
        /// </summary>
        public ShiftBlocker CurrentShiftBlocker
        {
            get
            {
                if (_mode != GameMode.Playing || ServiceHub.Clock.ShiftOver) return ShiftBlocker.NotOnShift;

                if (ServiceHub.Dialogue.IsActive) return ShiftBlocker.InConversation;
                if (ServiceHub.Phone.IsRinging || ServiceHub.Phone.InCall) return ShiftBlocker.OnTheLine;
                if (ServiceHub.Interphone.HasWaitingVisitor) return ShiftBlocker.AtTheDoor;

                // Tonight's roster, not every case filed under tonight: a case the pool did
                // not draw never leaves Dormant, and Dormant is what this reads as work the
                // night has not handed out yet. Counting them held the clock-off button on
                // "pending" for the whole shift (v5.0 4.1).
                return BlockerForRoster(ServiceHub.Cases.RosterTonight, ServiceHub.State.NightIndex,
                                        PendingVisitorCount > 0);
            }
        }

        /// <summary>
        /// GDD 6.3 step 8: the shift is over when the work is done, not when the clock says so.
        ///
        /// This is offered, never forced: some of the night's evidence sits outside any case
        /// (the 2009 maintenance bill in the records room is only ever picked up voluntarily,
        /// and night 6 needs it), so ending early stays the player's decision.
        /// </summary>
        public bool CanEndShiftEarly()
        {
            return CurrentShiftBlocker == ShiftBlocker.None;
        }

        /// <summary>
        /// The roster half of <see cref="CurrentShiftBlocker"/>, pulled out so it can be
        /// driven by a test without a scene.
        ///
        /// The distinction it draws is the whole point of the fix: Dormant is work the night
        /// has not handed out yet, and everything else unresolved is work the player is
        /// holding. Collapsing the two is what made the dashboard accuse a caretaker who had
        /// done everything that existed.
        /// </summary>
        public static ShiftBlocker BlockerForRoster(IEnumerable<Cases.CaseRuntime> cases, int night,
                                                    bool anyPendingVisitor)
        {
            bool anyTonight = false;
            bool anyOpen = false;
            bool anyUnissued = anyPendingVisitor;

            if (cases != null)
            {
                foreach (var runtime in cases)
                {
                    if (runtime == null || runtime.Definition == null) continue;
                    if (runtime.Definition.nightIndex != night) continue;
                    anyTonight = true;

                    if (runtime.State.IsResolved()) continue;
                    if (runtime.State == CaseState.Dormant) anyUnissued = true;
                    else anyOpen = true;
                }
            }

            if (!anyTonight) return ShiftBlocker.NotOnShift;
            if (anyOpen) return ShiftBlocker.CaseOpen;
            if (anyUnissued) return ShiftBlocker.NotYetScheduled;

            return ShiftBlocker.None;
        }

        /// <summary>The line the home dashboard prints under the clock-off button.</summary>
        public static string ShiftBlockerKey(ShiftBlocker blocker)
        {
            switch (blocker)
            {
                case ShiftBlocker.None: return "ui.home.end_shift";
                case ShiftBlocker.InConversation: return "ui.home.end_shift_talking";
                case ShiftBlocker.OnTheLine: return "ui.home.end_shift_call";
                case ShiftBlocker.AtTheDoor: return "ui.home.end_shift_door";
                case ShiftBlocker.CaseOpen: return "ui.home.end_shift_blocked";
                case ShiftBlocker.NotYetScheduled: return "ui.home.end_shift_pending";
                default: return "ui.home.end_shift_off_duty";
            }
        }

        /// <summary>Clock off. Does nothing unless <see cref="CanEndShiftEarly"/> allows it.</summary>
        /// <summary>
        /// The night moving forward. Host only (v3.0 46.1).
        /// </summary>
        void TickShift(float dt)
        {
            ServiceHub.Clock.Tick(dt);
            SelectedMainQuestRules.Tick(dt);
            SubquestRules.Tick(dt);

            ServiceHub.Cases.Tick();
            ServiceHub.ManualEvents.Tick();
            ServiceHub.AnomalyTools.Tick();
            ServiceHub.Cctv.Tick();
            ServiceHub.Phone.Tick();
            ServiceHub.Threat.Tick();
            ServiceHub.Traffic.Tick(dt);
            ServiceHub.ActiveVisitors.Tick();

            ServiceHub.Power.Tick();
            ServiceHub.Pressure.Tick();
            ServiceHub.Save.Tick();

            TickVisitorSchedule();
        }

        public void RequestEndShift()
        {
            // One shift, one clock-off. A client that ran this locally would set its own clock
            // to 06:00, score the night off its mirror and land in the summary while the other
            // two were still working - and the next mirror would put its clock straight back.
            if (!Net.NetSession.Authoritative)
            {
                Net.NetShift.Request(Net.NetShift.ShiftAct.EndShift);
                return;
            }

            if (!CanEndShiftEarly()) return;

            // The night's own end-of-shift bookkeeping reads the clock, so hand it a finished
            // shift rather than a special case: 06:00 arrives early tonight.
            ServiceHub.Clock.SetGameSecond(GameClock.ShiftEndSecond);
            OpenPc(false);
            EndNight();
        }

        void HandlePauseKey()
        {
            if (_console != null && _console.IsOpen) { _console.Toggle(); ApplyCursor(); return; }

            if (_mode == GameMode.Gallery || _mode == GameMode.Credits) { SetMode(GameMode.Menu); return; }

            if (_mode == GameMode.Playing)
            {
                if (_pc.IsOpen) { OpenPc(false); return; }
                if (_toolMenu.IsOpen) { _toolMenu.Close(); RefreshPlayerControl(); return; }
                if (_tablet.IsOpen) { _tablet.Close(); RefreshPlayerControl(); return; }
                SetMode(GameMode.Paused);
            }
            else if (_mode == GameMode.Paused)
            {
                SetMode(GameMode.Playing);
            }
        }

        void HandleHotkeys()
        {

            if (_input.Tablet.WasPressedThisFrame() && !_pc.IsOpen)
            {
                _tablet.Toggle();
                RefreshPlayerControl();
            }

            if (_input.EvidenceBoard.WasPressedThisFrame() && _pc.IsOpen) _pc.Open(AppIds.Evidence);

            // Quick CCTV snapshot without opening the app (GDD 8.1 "R").
            if (_input.Snapshot.WasPressedThisFrame() && _pc.IsOpen &&
                _pc.ActiveAppId == AppIds.Cctv && !string.IsNullOrEmpty(ServiceHub.Cctv.SelectedCameraId))
            {
                Net.NetShift.Request(Net.NetShift.ShiftAct.TakeSnapshot, ServiceHub.Cctv.SelectedCameraId);
            }
        }

        /// <summary>
        /// GDD 15.7. Screens cost the same time as standing in a corridor. The evidence board
        /// stops the clock outright - laying out a case genuinely is thinking, not playing -
        /// but on a budget, and when the budget is gone the board runs slow instead of frozen.
        /// </summary>
        public const float BoardStillnessBudgetSeconds = 120f;

        float _boardStillnessLeft = BoardStillnessBudgetSeconds;

        public float BoardStillnessLeft { get { return _boardStillnessLeft; } }

        float CurrentTimeScale()
        {
            if (_pause.IsOpen || (_console != null && _console.IsOpen)) return 0f;

            if (_pc.IsOpen && _pc.ActiveAppId == AppIds.Evidence)
            {
                if (_boardStillnessLeft > 0f)
                {
                    _boardStillnessLeft -= Time.unscaledDeltaTime;
                    return 0f;
                }
                return 0.35f;
            }

            if (ServiceHub.Dialogue.IsActive) return ServiceHub.Dialogue.Current.timeScale;
            return 1f;
        }

        bool AnyInputThisFrame()
        {
            return _input.MoveValue.sqrMagnitude > 0.01f
                || _input.LookValue.sqrMagnitude > 0.5f
                || _input.Interact.WasPressedThisFrame();
        }

        void EndNight()
        {

            // What the shift gives back for having been survived (v5.0 6.3 / 7.3). Before the
            // summary reads anything, so the numbers on it are the ones the next night starts
            // with rather than the ones the corridor left behind.
            ServiceHub.Vitals.EndNight();

            ServiceHub.Threat.EndNight();
            ServiceHub.Traffic.EndNight();

            // The shift is over and the building empties. ActiveVisitorService has always had
            // this method and nothing ever called it, so a visitor admitted on night 3 stayed
            // on the tracking board for the rest of the playthrough - invisible until they had
            // a body, and a red dummy still pacing the fourth floor once they did.
            ServiceHub.ActiveVisitors.EndNight();
            if (_visitorBodies != null) _visitorBodies.Clear();
            // Spec 0.10: a night's accumulated distortion partly wears off; a floor's risk
            // does not.
            ServiceHub.Risk.OnNightEnded();
            ServiceHub.State.EndNight();
            ServiceHub.Analytics.Track(AnalyticsService.Events.NightCompleted, null, ServiceHub.State.NightIndex);
            ServiceHub.Save.RequestAutosave(Save.SaveReason.NightEnd);

            CheckNightEndAchievements();

            // An endless run never reaches an ending; AdvanceToNextNight decides whether the
            // caretaker still has a job.
            if (!ServiceHub.State.EndlessMode && ServiceHub.State.NightIndex >= FinalNight)
            {
                ShowEnding();
                return;
            }

            // Everybody clocks off together. Sent before the local mode change so a client is
            // not left standing in a building whose night has already been scored.
            Net.NetShift.Broadcast(Net.NetShift.Moment.NightEnded, null, null,
                                   ServiceHub.State.NightIndex);

            _summary.Populate();
            SetMode(GameMode.NightSummary);
        }

        /// <summary>
        /// The host's shift is over (v3.0 46.1).
        ///
        /// A client scores nothing and decides nothing here - the stats, the case outcomes and
        /// the achievements all came over in the mirror before this arrived. It only stops
        /// playing and reads the same summary off the same numbers.
        /// </summary>
        public void FollowNightEnded()
        {
            if (_mode != GameMode.Playing) return;

            OpenPc(false);
            if (_visitorBodies != null) _visitorBodies.Clear();

            _summary.Populate();
            SetMode(GameMode.NightSummary);
        }

        /// <summary>
        /// State.EndNight() (just above) does not advance NightIndex - that happens when the
        /// next night's StartNight runs - so this still reads as "the night that just ended".
        /// </summary>
        void CheckNightEndAchievements()
        {
            int night = ServiceHub.State.NightIndex;

            bool anyCase = false, anyRoutine = false;
            bool allCorrect = true, allRoutineCorrect = true;

            foreach (var runtime in ServiceHub.Cases.RosterTonight)
            {
                bool correct = runtime.ResolvedQuality() == DecisionQuality.Correct;

                anyCase = true;
                if (!correct) allCorrect = false;

                if (runtime.Definition.kind == CaseKind.RoutineTask)
                {
                    anyRoutine = true;
                    if (!correct) allRoutineCorrect = false;
                }
            }

            if (anyRoutine && allRoutineCorrect) ServiceHub.Steam.Unlock(AchievementIds.GoodCaretaker);
            if (anyCase && allCorrect) ServiceHub.Steam.Unlock(AchievementIds.FlawlessNight);

            // The only night with an active patrol threat right now (GDD 15.2 / ThreatService).
            if (night == 5 && ServiceHub.Threat.CaughtCount == 0) ServiceHub.Steam.Unlock(AchievementIds.Unseen);

            if (night >= 1 && ServiceHub.Cctv.UnreviewedMotionCount() == 0)
                ServiceHub.Steam.Unlock(AchievementIds.EveryEye);
        }

        void ShowEnding()
        {
            if (_endingResolved) { SetMode(GameMode.Ending); return; }
            _endingResolved = true;

            CheckEndOfGameAchievements();

            var ending = ServiceHub.Endings.Resolve();
            if (ending != null) ServiceHub.Endings.RecordLastEnding(ending.endingId);

            _ending.Show(ending);
            SetMode(GameMode.Ending);
        }

        void CheckEndOfGameAchievements()
        {
            bool missedAnyCall = false;
            foreach (var id in ServiceHub.Phone.MissedCalls) { missedAnyCall = true; break; }
            if (!missedAnyCall) ServiceHub.Steam.Unlock(AchievementIds.PerfectLine);

            // Spec 23 A02: the one thing the anomalous tools take away, and the only one.
            if (!ServiceHub.State.GetFlag(FlagIds.AnomalyToolUsed))
                ServiceHub.Steam.Unlock(AchievementIds.Unaided);
        }
    }

    /// <summary>End-of-shift summary (GDD 6.3 step 8). Placeholder layout.</summary>
    public sealed class NightSummaryView : MonoBehaviour
    {
        public System.Action OnContinue;
        Text _body;

        public static NightSummaryView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("NightSummary", parent, 320);
            var view = canvas.gameObject.AddComponent<NightSummaryView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var background = UiFactory.CreatePanel("Background", root, UiFactory.Background);
            UiFactory.Stretch(background.rectTransform, 0f, 0f);

            var title = UiFactory.CreateText("Title", root, Loc.T("ui.summary.title"), 34, TextAnchor.UpperLeft);
            UiFactory.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(120f, -120f), new Vector2(1200f, 44f));

            _body = UiFactory.CreateText("Body", root, string.Empty, 19, TextAnchor.UpperLeft);
            UiFactory.Pin(_body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(120f, -200f), new Vector2(1200f, 520f));

            var button = UiFactory.CreateButton("Continue", root, Loc.T("ui.summary.continue"), 20,
                                                () => { var cb = OnContinue; if (cb != null) cb(); });
            UiFactory.Pin(button.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(120f, 100f), new Vector2(320f, 52f));

            gameObject.SetActive(false);
        }

        public void SetOpen(bool open) { gameObject.SetActive(open); }

        /// <summary>
        /// The morning after (GDD 6.3 step 8).
        ///
        /// This used to be a receipt: three case counts and three stat numbers. A number going
        /// from 60 to 57 is not a consequence, it is a rounding error with a label, and it was
        /// the only thing the game gave back for a whole shift of judgements. Papers, Please
        /// does not end its day on a score - it ends it on a table, with heat and medicine and
        /// a name you recognise, and that screen is why people play the next day.
        ///
        /// So this leads with what the shift actually did and to whom, and it leads with the
        /// things that are still open. The numbers stay, at the bottom, where numbers belong.
        /// </summary>
        public void Populate()
        {
            var state = ServiceHub.State;
            var text = Loc.T(state.EndlessMode ? "ui.endless.shift" : "ui.summary.night",
                             state.NightIndex) + "\n\n";

            int correct = 0, partial = 0, wrong = 0, unresolved = 0;
            foreach (var runtime in ServiceHub.Cases.RosterTonight)
            {
                var quality = runtime.ResolvedQuality();
                if (quality == null) { unresolved++; continue; }

                switch (quality.Value)
                {
                    case DecisionQuality.Correct: correct++; break;
                    case DecisionQuality.Partial: partial++; break;
                    case DecisionQuality.Wrong: wrong++; break;
                }
            }

            // ---- what the shift did to people ------------------------------
            text += Loc.T("ui.summary.section.door") + "\n";
            text += "  " + Loc.T("ui.summary.judged", state.VisitorsCorrect, state.VisitorsWrong) + "\n";

            int intruders = ServiceHub.Pressure != null ? ServiceHub.Pressure.IntruderCount : 0;
            if (intruders > 0)
                text += "  " + Loc.T("ui.summary.intruder_left", intruders) + "\n";

            text += "\n";

            // ---- what is still open ----------------------------------------
            //
            // The open threads are the reason to come back, so they are on the screen the
            // player reads before deciding whether to. An unclosed loop is worth more than a
            // closed one (GDD 2.3): this is the only place the game gets to say one out loud.
            bool anyOpen = false;
            var open = Loc.T("ui.summary.section.open") + "\n";

            if (unresolved > 0) { open += "  " + Loc.T("ui.summary.open_cases", unresolved) + "\n"; anyOpen = true; }

            int unseen = ServiceHub.Cctv.UnreviewedMotionCount();
            if (unseen > 0) { open += "  " + Loc.T("ui.summary.unreviewed", unseen) + "\n"; anyOpen = true; }

            int missed = 0;
            foreach (var id in ServiceHub.Phone.MissedCalls) missed++;
            if (missed > 0) { open += "  " + Loc.T("ui.summary.missed_calls", missed) + "\n"; anyOpen = true; }

            if (state.GetFlag(FlagIds.Knows404) || state.GetStat(StatIds.HarinResonance) > 0)
            {
                open += "  " + Loc.T("ui.summary.open_404") + "\n";
                anyOpen = true;
            }

            if (anyOpen) text += open + "\n";

            // ---- and then the numbers --------------------------------------
            text += Loc.T("ui.summary.cases", correct, partial, wrong, unresolved) + "\n";
            text += Loc.T("ui.stat.performance") + "  " + state.GetStat(StatIds.Performance) + "   ";
            text += Loc.T("ui.stat.trust") + "  " + state.GetStat(StatIds.CommunityTrust) + "   ";
            text += Loc.T("ui.stat.safety") + "  " + state.GetStat(StatIds.BuildingSafety) + "\n";
            text += Loc.T("ui.hud.evidence_count", ServiceHub.Evidence.Count);

            _body.text = text;
        }

    }
}
