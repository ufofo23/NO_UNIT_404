using System.Collections.Generic;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Anomalies
{
    /// <summary>One machine's standing state.</summary>
    public sealed class AnomalyToolRuntime
    {
        public AnomalyToolDefinition Definition { get; private set; }

        /// <summary>The machine has woken up and will answer (spec 21 unlock times).</summary>
        public bool Awake { get; private set; }

        /// <summary>How many transactions the caretaker has taken from it this playthrough.</summary>
        public int UseCount { get; private set; }

        // ---- hold (spec 23 A02 / A04) ----------------------------------------

        public bool HoldOpen { get; private set; }
        public bool HoldExpired { get; private set; }
        public int HoldEndsAtGameSecond { get; private set; }
        /// <summary>The thing that is out on loan, so a late return knows what came back.</summary>
        public string HeldFlagId { get; private set; }
        /// <summary>Warnings already emitted, so escalation happens once each.</summary>
        public int WarningsFired { get; private set; }

        public AnomalyToolRuntime(AnomalyToolDefinition definition) { Definition = definition; }

        public string ToolId { get { return Definition != null ? Definition.toolId : string.Empty; } }

        public void SetAwake(bool awake) { Awake = awake; }
        public void NoteUse() { UseCount++; }

        public void OpenHold(int endsAtGameSecond, string heldFlagId)
        {
            HoldOpen = true;
            HoldExpired = false;
            HoldEndsAtGameSecond = endsAtGameSecond;
            HeldFlagId = heldFlagId;
            WarningsFired = 0;
        }

        public void MarkExpired() { HoldExpired = true; }
        public void NoteWarning() { WarningsFired++; }

        public void CloseHold()
        {
            HoldOpen = false;
            HoldExpired = false;
            HoldEndsAtGameSecond = 0;
            HeldFlagId = null;
            WarningsFired = 0;
        }

        public void RestoreFrom(bool awake, int useCount, bool holdOpen, bool holdExpired,
                                int holdEndsAt, string heldFlagId, int warningsFired)
        {
            Awake = awake;
            UseCount = useCount;
            HoldOpen = holdOpen;
            HoldExpired = holdExpired;
            HoldEndsAtGameSecond = holdEndsAt;
            HeldFlagId = heldFlagId;
            WarningsFired = warningsFired;
        }
    }

    /// <summary>
    /// The five anomalous tools A01..A05 (v2.1 spec 23).
    ///
    /// These machines exist so that a caretaker who is stuck is never only stuck. Spec 23 is
    /// explicit that they are not cheats: every one of them trades something the player wants
    /// now for something they keep losing later, and the losses are deliberately kept out of
    /// the ending algorithm (spec 32) so that leaning on them makes the last night harder
    /// without closing any ending off.
    ///
    /// Three rules are enforced here rather than left to content:
    ///
    /// - A machine that is not awake cannot be used at all. Spec 21 gives each one the night
    ///   and the beat it comes alive on, and until then a POS is a POS.
    /// - The price is applied at the moment the option is taken, together with whatever the
    ///   option gives. There is no path where the caretaker receives the object and the cost
    ///   is charged later, or the other way round.
    /// - A hold that runs out charges its price exactly once, and A02's hold does not end
    ///   there: spec 23 wants the door still open and the way out still a single correct
    ///   interaction rather than a mashed key.
    /// </summary>
    public sealed class AnomalyToolService
    {
        readonly ContentDatabase _content;
        readonly GameClock _clock;
        readonly GameStateService _state;

        readonly Dictionary<string, AnomalyToolRuntime> _tools =
            new Dictionary<string, AnomalyToolRuntime>();

        public AnomalyToolService(ContentDatabase content, GameClock clock, GameStateService state)
        {
            _content = content;
            _clock = clock;
            _state = state;
            BuildRuntimes();
        }

        void BuildRuntimes()
        {
            _tools.Clear();
            foreach (var definition in _content.AnomalyTools)
            {
                if (definition == null || string.IsNullOrEmpty(definition.toolId)) continue;
                _tools[definition.toolId] = new AnomalyToolRuntime(definition);
            }
        }

        public IEnumerable<AnomalyToolRuntime> AllTools { get { return _tools.Values; } }

        public AnomalyToolRuntime Find(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return null;
            AnomalyToolRuntime runtime;
            return _tools.TryGetValue(toolId, out runtime) ? runtime : null;
        }

        public bool IsAwake(string toolId)
        {
            var runtime = Find(toolId);
            return runtime != null && runtime.Awake;
        }

        // ---- the open session ---------------------------------------------------

        /// <summary>The machine the caretaker is standing at, or null.</summary>
        public AnomalyToolRuntime OpenTool { get; private set; }
        /// <summary>Which screen of that machine's menu is showing.</summary>
        public AnomalyToolMenuDefinition OpenMenu { get; private set; }
        /// <summary>What the machine last printed back, shown above the menu until it closes.</summary>
        public string LastResultKey { get; private set; }

        public bool SessionOpen { get { return OpenTool != null; } }

        // ---- night boundary -----------------------------------------------------

        public void Reset()
        {
            Close();
            BuildRuntimes();
        }

        /// <summary>
        /// Wake whatever spec 21 says is awake tonight.
        ///
        /// A machine that woke on an earlier night stays awake: spec 23 describes them as
        /// things the building has always had and only now answers with, and a locker that
        /// worked on night 2 and refused on night 3 would read as a bug rather than as
        /// anything the caretaker did.
        /// </summary>
        public void BeginNight(int nightIndex)
        {
            Close();

            foreach (var runtime in _tools.Values)
            {
                var def = runtime.Definition;

                // A shift that ended with something still out ends the loan too. The tool is
                // gone and the price has already been charged by Tick; this only clears the
                // bookkeeping so the next night does not inherit an expired window.
                if (runtime.HoldOpen && !def.holdSurvivesExpiry) runtime.CloseHold();

                if (runtime.Awake || def.unlockNight <= 0 || nightIndex < def.unlockNight) continue;

                string failReason;
                if (!ConditionEvaluator.EvaluateAll(def.unlockConditions, out failReason)) continue;

                Wake(runtime);
            }
        }

        /// <summary>
        /// Re-check the tools whose night has come but whose conditions had not.
        ///
        /// Called when the world moves rather than on a timer, because every one of spec 21's
        /// unlock beats is the end of something the caretaker did - M06 closed, C07 filed,
        /// M18 survived - and none of them is a clock reading.
        /// </summary>
        public void RefreshAvailability()
        {
            int night = _state.NightIndex;
            foreach (var runtime in _tools.Values)
            {
                var def = runtime.Definition;
                if (runtime.Awake || def.unlockNight <= 0 || night < def.unlockNight) continue;

                string failReason;
                if (!ConditionEvaluator.EvaluateAll(def.unlockConditions, out failReason)) continue;

                Wake(runtime);
            }
        }

        /// <summary>
        /// Wake a machine now, whatever night it is (developer console only).
        ///
        /// Spec 30.2 asks that every anomaly be tested at exposure 75 and at floor risk 4, and
        /// several of the machines only exist on one night - so reaching those states on the
        /// night a machine is awake is otherwise a matter of playing to it. This is the same
        /// concession GameLoop.DebugStartNight already makes.
        /// </summary>
        public bool ForceWake(string toolId)
        {
            var runtime = Find(toolId);
            if (runtime == null) return false;
            if (runtime.Awake) return true;

            Wake(runtime);
            return true;
        }

        void Wake(AnomalyToolRuntime runtime)
        {
            runtime.SetAwake(true);
            var def = runtime.Definition;

            EventBus.Publish(new AnomalyToolAwakeEvent(def.toolId));
            if (!string.IsNullOrEmpty(def.unlockNotifyKey))
                EventBus.Publish(new NotificationEvent(def.unlockNotifyKey, NotificationSeverity.Warning));

            Log.Info("Tools", def.toolId + " is awake");
        }

        // ---- using a machine -----------------------------------------------------

        public bool Open(string toolId)
        {
            var runtime = Find(toolId);
            if (runtime == null || !runtime.Awake) return false;

            var menu = runtime.Definition.RootMenu;
            if (menu == null)
            {
                Log.Error("Tools", toolId + " has no root menu");
                return false;
            }

            OpenTool = runtime;
            OpenMenu = menu;
            LastResultKey = null;

            EventBus.Publish(new AnomalyToolSessionEvent(toolId, true));
            return true;
        }

        public void Close()
        {
            if (OpenTool == null) return;

            var toolId = OpenTool.ToolId;
            OpenTool = null;
            OpenMenu = null;
            LastResultKey = null;

            EventBus.Publish(new AnomalyToolSessionEvent(toolId, false));
        }

        /// <summary>
        /// The options on the current screen that the caretaker can actually see.
        ///
        /// Spec 23 A01 makes this the whole input model: there is no free-text box, only the
        /// keywords already observed, so a machine offering an option is itself a statement
        /// that the caretaker has seen enough to ask it.
        /// </summary>
        public void CollectVisibleOptions(List<AnomalyToolOptionDefinition> into)
        {
            if (into == null) return;
            into.Clear();
            if (OpenMenu == null || OpenMenu.options == null) return;

            for (int i = 0; i < OpenMenu.options.Length; i++)
            {
                var option = OpenMenu.options[i];
                if (option == null || !IsOfferable(OpenTool, option)) continue;
                into.Add(option);
            }
        }

        /// <summary>
        /// Whether a machine will offer this option right now.
        ///
        /// The authored conditions cover the world; the three rules below cover the machine
        /// itself, and they are here rather than in content because content has no way to
        /// express them. A toolbox with something already out cannot lend a second thing, a
        /// return slot with nothing to receive is not a menu line, and a vending machine will
        /// not take a token the caretaker does not have.
        /// </summary>
        bool IsOfferable(AnomalyToolRuntime runtime, AnomalyToolOptionDefinition option)
        {
            if (option.opensHold && runtime.HoldOpen) return false;
            if (option.releasesHold && !runtime.HoldOpen) return false;

            if (!string.IsNullOrEmpty(option.consumesFlagId) && !_state.GetFlag(option.consumesFlagId))
                return false;

            string failReason;
            return ConditionEvaluator.EvaluateAll(option.conditions, out failReason);
        }

        /// <summary>
        /// Take one option: pay for it, receive it, and either move on or finish.
        ///
        /// Returns false when the option is not on the menu, which covers both a stale click
        /// and an option whose conditions stopped holding while the screen was open.
        /// </summary>
        public bool Choose(string optionId)
        {
            if (OpenTool == null || OpenMenu == null) return false;

            var option = OpenMenu.Find(optionId);
            if (option == null) return false;

            if (!IsOfferable(OpenTool, option))
            {
                Log.Warn("Tools", OpenTool.ToolId + " option " + optionId + " is not available");
                return false;
            }

            var definition = OpenTool.Definition;

            // Releasing is not a transaction: the return slot has no price of its own, and
            // charging the caretaker for bringing a tool back would invert the whole rule.
            if (option.releasesHold)
            {
                ReleaseHold(definition.toolId);
                Close();
                return true;
            }

            if (!string.IsNullOrEmpty(option.consumesFlagId))
                _state.SetFlag(option.consumesFlagId, false);

            ServiceHub.Cases.ApplyConsequences(option.onChosen);

            if (!string.IsNullOrEmpty(option.grantsFlagId))
                _state.SetFlag(option.grantsFlagId, true);

            // Spec 23 A02: a machine used as a way out costs the caretaker the claim that they
            // got through unaided, and nothing else.
            if (option.opensHold || !string.IsNullOrEmpty(option.grantsFlagId))
                _state.SetFlag(FlagIds.AnomalyToolUsed, true);

            OpenTool.NoteUse();

            if (option.opensHold && definition.hold != AnomalyToolHold.None)
                OpenHold(OpenTool, option.grantsFlagId);

            if (option.driftGameSeconds > 0)
                EventBus.Publish(new AnomalyDriftEvent(definition.toolId,
                                                       _clock.GameSecond + option.driftGameSeconds));

            EventBus.Publish(new AnomalyToolUsedEvent(definition.toolId, option.optionId,
                                                      option.resultKey));

            if (!string.IsNullOrEmpty(option.resultKey)) LastResultKey = option.resultKey;

            ServiceHub.Save.RequestAutosave(Save.SaveReason.MajorChoice);

            var next = definition.FindMenu(option.nextMenuId);
            if (next != null)
            {
                OpenMenu = next;
                return true;
            }

            // An option that hands over a physical thing ends the transaction; one that only
            // prints or plays keeps the screen up to be read.
            //
            // The difference matters more than it looks. A locker whose menu stayed open would
            // be spending the five seconds it just gave the caretaker to shut the door, and a
            // vending machine whose menu stayed open would be spending the ten seconds of pull
            // that taking the photograph costs. In both cases the price is paid in the room,
            // so the machine has to let go of the player to charge it.
            if (!string.IsNullOrEmpty(option.grantsFlagId))
            {
                if (!string.IsNullOrEmpty(option.resultKey))
                    EventBus.Publish(new NotificationEvent(option.resultKey, NotificationSeverity.Info));
                Close();
                return true;
            }

            if (string.IsNullOrEmpty(option.resultKey)) Close();
            return true;
        }

        // ---- holds ----------------------------------------------------------------

        void OpenHold(AnomalyToolRuntime runtime, string heldFlagId)
        {
            var def = runtime.Definition;
            runtime.OpenHold(_clock.GameSecond + def.holdGameSeconds, heldFlagId);

            EventBus.Publish(new AnomalyToolHoldEvent(def.toolId, true, false));
            if (!string.IsNullOrEmpty(def.holdPromptKey))
                EventBus.Publish(new NotificationEvent(def.holdPromptKey, NotificationSeverity.Warning));
        }

        /// <summary>Game seconds left on a tool's hold, or -1 when it has none open.</summary>
        public int HoldRemaining(string toolId)
        {
            var runtime = Find(toolId);
            if (runtime == null || !runtime.HoldOpen) return -1;
            int remaining = runtime.HoldEndsAtGameSecond - _clock.GameSecond;
            return remaining < 0 ? 0 : remaining;
        }

        public bool HasOpenHold(string toolId)
        {
            var runtime = Find(toolId);
            return runtime != null && runtime.HoldOpen;
        }

        /// <summary>
        /// Shut the door, or put the tool back.
        ///
        /// One correct interaction, whether the window has run out or not - spec 23 A02 is
        /// specific that the way out of the locker is doing the thing properly once, and a
        /// caretaker who is late for the toolbox should still be able to hand back what is
        /// left rather than be told the interaction no longer exists.
        /// </summary>
        public bool ReleaseHold(string toolId)
        {
            var runtime = Find(toolId);
            if (runtime == null || !runtime.HoldOpen) return false;

            var def = runtime.Definition;
            bool late = runtime.HoldExpired;

            if (!string.IsNullOrEmpty(runtime.HeldFlagId) && def.hold == AnomalyToolHold.ReturnTool)
                _state.SetFlag(runtime.HeldFlagId, false);

            // Spec 23 A02 puts the price of the open locker on the way out rather than on the
            // timer, because the caretaker is not being charged for being slow - they are
            // being charged for what reached them while the door stood open.
            if (late) ServiceHub.Cases.ApplyConsequences(def.onLateRelease);

            runtime.CloseHold();

            EventBus.Publish(new AnomalyToolHoldEvent(def.toolId, false, late));

            string notifyKey = late ? def.holdLateKey : def.holdReleasedKey;
            if (!string.IsNullOrEmpty(notifyKey))
                EventBus.Publish(new NotificationEvent(notifyKey,
                    late ? NotificationSeverity.Warning : NotificationSeverity.Info));

            ServiceHub.Save.RequestAutosave(Save.SaveReason.MajorChoice);
            return true;
        }

        /// <summary>Called once per frame by GameLoop, after the manual events.</summary>
        public void Tick()
        {
            foreach (var runtime in _tools.Values)
            {
                if (!runtime.HoldOpen) continue;
                var def = runtime.Definition;

                EmitDueWarnings(runtime);

                if (!runtime.HoldExpired)
                {
                    if (_clock.GameSecond < runtime.HoldEndsAtGameSecond) continue;
                    Expire(runtime);
                }

                // A04's loan is over the moment it expires: there is nothing left to hand back.
                if (!def.holdSurvivesExpiry && runtime.HoldOpen) runtime.CloseHold();
            }
        }

        /// <summary>
        /// The escalation, on the schedule the tool was authored with.
        ///
        /// Spec 23 A02 puts its three beats at six, eight and ten seconds against a
        /// five-second window, so they are not spaced through the window - they are what
        /// happens once it has already gone. A04 is the other way round and its one reminder
        /// comes while the hour is still running. Both are a list of elapsed times, which is
        /// why this reads a schedule rather than dividing anything.
        /// </summary>
        void EmitDueWarnings(AnomalyToolRuntime runtime)
        {
            var def = runtime.Definition;
            var warnings = def.holdWarnKeys;
            var times = def.holdWarnAtGameSeconds;
            if (warnings == null || times == null) return;

            int count = warnings.Length < times.Length ? warnings.Length : times.Length;
            if (runtime.WarningsFired >= count) return;

            int elapsed = def.holdGameSeconds - (runtime.HoldEndsAtGameSecond - _clock.GameSecond);

            while (runtime.WarningsFired < count && elapsed >= times[runtime.WarningsFired])
            {
                var key = warnings[runtime.WarningsFired];
                runtime.NoteWarning();
                if (!string.IsNullOrEmpty(key))
                    EventBus.Publish(new NotificationEvent(key, NotificationSeverity.Warning));
            }
        }

        void Expire(AnomalyToolRuntime runtime)
        {
            var def = runtime.Definition;
            runtime.MarkExpired();

            ServiceHub.Cases.ApplyConsequences(def.onHoldExpired);

            // The tool went with the hour. What the caretaker is carrying has to go with it,
            // or the debt would buy them a permanent loan.
            if (def.hold == AnomalyToolHold.ReturnTool && !string.IsNullOrEmpty(runtime.HeldFlagId))
                _state.SetFlag(runtime.HeldFlagId, false);

            EventBus.Publish(new AnomalyToolHoldEvent(def.toolId, def.holdSurvivesExpiry, true));
            Log.Info("Tools", def.toolId + " hold expired");
        }

        // ---- save -------------------------------------------------------------------

        public void LoadFrom(IEnumerable<Save.AnomalyToolSaveEntry> entries)
        {
            Reset();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                var runtime = Find(entry.toolId);
                if (runtime == null)
                {
                    Log.Warn("Tools", "save references unknown tool " + entry.toolId);
                    continue;
                }

                runtime.RestoreFrom(entry.awake, entry.useCount, entry.holdOpen, entry.holdExpired,
                                    entry.holdEndsAt, entry.heldFlagId, entry.warningsFired);
            }
        }
    }
}
