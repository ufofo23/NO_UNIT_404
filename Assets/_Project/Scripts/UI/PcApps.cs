using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NO404.Cases;
using NO404.CCTV;
using NO404.Core;
using NO404.Evidence;
using NO404.Facility;
using NO404.Residents;
using NO404.Visitors;

namespace NO404.UI
{
    // =========================================================================
    // Home dashboard (GDD 16.7)
    // =========================================================================

    public sealed class HomeApp : PcApp
    {
        RectTransform _list;
        Text _memo;
        Button _endShift;
        Text _endShiftLabel;
        RectTransform _visitorBoard;
        string _boardSignature;

        // GDD 16.7 / 15.7: one countdown per open card. Cards are only rebuilt when the case
        // list changes, so the labels are kept and re-stamped every frame instead.
        readonly List<CaseRuntime> _deadlineCases = new List<CaseRuntime>();
        readonly List<Text> _deadlineLabels = new List<Text>();

        public HomeApp() { AppId = AppIds.Home; TitleKey = "ui.app.home"; }

        protected override void Build(RectTransform root)
        {
            var header = UiFactory.CreateText("Header", root, Loc.T("ui.home.title"), 24, TextAnchor.UpperLeft);
            UiFactory.Pin(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(20f, -18f), new Vector2(600f, 32f));

            ScrollRect scroll;
            var content = UiFactory.CreateScrollView("Tasks", root, out scroll);
            UiFactory.SetAnchoredRect((RectTransform)scroll.transform, new Vector2(0f, 0f), new Vector2(0.62f, 1f),
                                      new Vector2(16f, 16f), new Vector2(-8f, -60f));
            _list = content;

            var memoPanel = UiFactory.CreatePanel("MemoPanel", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(memoPanel.rectTransform, new Vector2(0.62f, 0f), new Vector2(1f, 1f),
                                      new Vector2(8f, 300f), new Vector2(-16f, -60f));
            _memo = UiFactory.CreateText("Memo", memoPanel.transform, string.Empty, 17, TextAnchor.UpperLeft);
            UiFactory.Stretch(_memo.rectTransform, 14f, 12f);

            // Who is in the building (v3.0 38.4). It lives on the home screen rather than
            // behind the interphone on purpose: the caretaker should be reminded that somebody
            // is upstairs while they are doing something else entirely, which is when it
            // matters and when it is easiest to forget.
            var boardPanel = UiFactory.CreatePanel("VisitorBoard", root, UiFactory.PanelAlt);
            UiFactory.SetAnchoredRect(boardPanel.rectTransform, new Vector2(0.62f, 0f), new Vector2(1f, 0f),
                                      new Vector2(8f, 68f), new Vector2(-16f, 292f));

            var boardHeader = UiFactory.CreateText("BoardHeader", boardPanel.transform,
                                                    Loc.T("ui.access.board.title"), 14, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(boardHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(10f, -22f), new Vector2(-10f, -4f));
            boardHeader.color = UiFactory.TextMuted;

            _visitorBoard = UiFactory.CreateRect("BoardList", boardPanel.rectTransform);
            UiFactory.SetAnchoredRect(_visitorBoard, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(8f, 6f), new Vector2(-8f, -24f));
            UiFactory.AddVerticalLayout(_visitorBoard, 4f);

            // GDD 6.3 step 8. Without this the only way out of a finished shift is to wait for
            // 06:00, which is over half an hour of real time with nothing left to do.
            _endShift = UiFactory.CreateButton("EndShift", root, Loc.T("ui.home.end_shift"), 16, EndShift);
            UiFactory.SetAnchoredRect(_endShift.GetComponent<RectTransform>(),
                                      new Vector2(0.62f, 0f), new Vector2(1f, 0f),
                                      new Vector2(8f, 16f), new Vector2(-16f, 56f));
            _endShiftLabel = _endShift.GetComponentInChildren<Text>();
        }

        static void EndShift()
        {
            if (GameLoop.Instance != null) GameLoop.Instance.RequestEndShift();
        }

        public override void OnOpen() { Rebuild(); RefreshEndShift(); }

        public override void Refresh()
        {
            if (_list.childCount != CountVisibleCases()) Rebuild();
            RefreshEndShift();
            RefreshDeadlines();
            RefreshVisitorBoard();
        }

        /// <summary>
        /// The tracking board.
        ///
        /// It says where somebody was last seen and how long ago, and it never says where they
        /// are (v3.0 38.4). That distinction is the entire system: a caretaker reading
        /// "4F 계단 / 6분 전" has to decide whether six minutes is long enough to worry about,
        /// and no amount of staring at the card will answer that. Walking up there will.
        /// </summary>
        void RefreshVisitorBoard()
        {
            var service = ServiceHub.ActiveVisitors;
            if (service == null || _visitorBoard == null) return;

            var signature = string.Empty;
            for (int i = 0; i < service.All.Count; i++)
            {
                var t = service.All[i];
                signature += t.VisitorId + ":" + t.LastKnownZone + ":" +
                             (ActiveVisitorService.SecondsSinceSeen(t) / 60) + ":" +
                             (int)t.Flags + ":" + (t.Token != null && t.Token.revoked ? "R" : "-") + ";";
            }

            if (signature == _boardSignature) return;
            _boardSignature = signature;

            UiFactory.ClearChildren(_visitorBoard);

            if (service.All.Count == 0)
            {
                var empty = UiFactory.CreateText("None", _visitorBoard, Loc.T("ui.access.board.empty"),
                                                  13, TextAnchor.UpperLeft);
                empty.color = UiFactory.TextMuted;
                UiFactory.SetHeight(empty.gameObject, 24f);
                return;
            }

            for (int i = 0; i < service.All.Count; i++) BuildVisitorCard(service.All[i]);
        }

        void BuildVisitorCard(ActiveVisitorService.Tracked tracked)
        {
            var card = UiFactory.CreatePanel("Card_" + tracked.VisitorId, _visitorBoard, UiFactory.Panel);
            UiFactory.SetHeight(card.gameObject, 76f);

            bool stale = (tracked.Flags & VisitorFlags.Untracked) != 0;
            bool broken = (tracked.Flags & VisitorFlags.EscortBroken) != 0;

            var body = Loc.T(tracked.Definition.nameKey) + "   " +
                       Loc.T(VisitorAccess.LabelKey(tracked.Token.level)) +
                       (tracked.Token.revoked ? "  " + Loc.T("ui.access.board.revoked") : "") + "\n" +
                       Loc.T(ActiveVisitorService.StateKey(tracked)) + "\n" +
                       Loc.T("ui.access.board.last_seen",
                             Loc.T(ZoneLabelKey(tracked.LastKnownZone)),
                             ActiveVisitorService.SecondsSinceSeen(tracked) / 60) + "\n" +
                       Loc.T("ui.access.board.dwell",
                             ActiveVisitorService.DwellSeconds(tracked) / 60);

            var text = UiFactory.CreateText("Body", card.transform, body, 12, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(text.rectTransform, new Vector2(0f, 0f), new Vector2(0.68f, 1f),
                                      new Vector2(8f, 4f), new Vector2(-4f, -4f));

            // Colour says how old the information is, not whether the person is dangerous.
            // The board does not know that and must not pretend to.
            if (broken) text.color = UiFactory.Danger;
            else if (stale) text.color = UiFactory.Warning;

            if (tracked.Token == null || tracked.Token.revoked) return;

            var visitorId = tracked.VisitorId;
            var revoke = UiFactory.CreateButton("Revoke_" + visitorId, card.transform,
                                                Loc.T("ui.access.board.revoke"), 12,
                                                () => Net.NetShift.RequestRevoke(visitorId));
            UiFactory.SetAnchoredRect(revoke.GetComponent<RectTransform>(),
                                      new Vector2(0.68f, 0f), new Vector2(1f, 1f),
                                      new Vector2(4f, 8f), new Vector2(-8f, -8f));
        }

        static string ZoneLabelKey(string zoneId)
        {
            return string.IsNullOrEmpty(zoneId) ? "ui.access.zone.unknown" : "zone." + zoneId.ToLowerInvariant();
        }

        /// <summary>
        /// GDD 15.7. Work used to sit still and wait to be picked up, which is why a shift
        /// felt like a puzzle box rather than a job. Every open card now says how long it has,
        /// because a deadline the player cannot see is an ambush, not pressure.
        /// </summary>
        void RefreshDeadlines()
        {
            for (int i = 0; i < _deadlineCases.Count; i++)
            {
                var runtime = _deadlineCases[i];
                var label = _deadlineLabels[i];
                if (runtime == null || label == null) continue;

                if (!runtime.State.IsActive() || runtime.FailSafeFired)
                {
                    label.text = runtime != null && runtime.FailSafeFired
                        ? Loc.T("ui.home.deadline_missed")
                        : string.Empty;
                    label.color = UiFactory.TextMuted;
                    continue;
                }

                int left = ServiceHub.Cases.SecondsToDeadline(runtime);
                if (left == int.MaxValue) { label.text = string.Empty; continue; }

                int minutes = Mathf.Max(0, left) / 60;
                label.text = Loc.T("ui.home.deadline_in", minutes);
                label.color = minutes <= 10 ? UiFactory.Danger
                            : minutes <= 30 ? UiFactory.Warning
                            : UiFactory.TextMuted;
            }
        }

        /// <summary>
        /// The button stays visible but inert until the night's work is actually closed, and
        /// it now says which kind of work is holding it.
        ///
        /// It used to print "남은 업무가 있습니다" for every reason, including the one that is
        /// not the player's doing - work the night has not issued yet. A caretaker who had
        /// done everything available was told they had left something open. Naming the reason
        /// costs nothing and is the difference between a schedule and an accusation.
        /// </summary>
        void RefreshEndShift()
        {
            var loop = GameLoop.Instance;
            var blocker = loop != null ? loop.CurrentShiftBlocker : GameLoop.ShiftBlocker.NotOnShift;
            bool ready = blocker == GameLoop.ShiftBlocker.None;

            _endShift.interactable = ready;
            _endShiftLabel.text = Loc.T(GameLoop.ShiftBlockerKey(blocker));
            _endShiftLabel.color = ready ? UiFactory.TextPrimary
                                 : blocker == GameLoop.ShiftBlocker.CaseOpen ? UiFactory.Warning
                                 : UiFactory.TextMuted;
        }

        int CountVisibleCases()
        {
            int n = 0;
            foreach (var runtime in ServiceHub.Cases.AllCases)
                if (runtime.State != CaseState.Dormant) n++;
            return n;
        }

        void Rebuild()
        {
            UiFactory.ClearChildren(_list);
            _deadlineCases.Clear();
            _deadlineLabels.Clear();

            foreach (var runtime in ServiceHub.Cases.AllCases)
            {
                if (runtime.State == CaseState.Dormant) continue;

                // Framed like the residents list rows: the list is layout-driven, so the
                // border is an inset child rather than a sibling copied from a rect that
                // is not final yet.
                var frame = UiFactory.CreatePanel("Case_" + runtime.CaseId, _list, UiFactory.Line);
                UiFactory.SetHeight(frame.gameObject, 92f);

                var card = UiFactory.CreatePanel("Fill", frame.transform, CardColor(runtime.State));
                UiFactory.Stretch(card.rectTransform, 1.5f, 1.5f);

                var title = UiFactory.CreateText("Title", card.transform,
                    Loc.T(runtime.Definition.titleKey) + "   [" + runtime.CaseId + "]", 19, TextAnchor.UpperLeft);
                UiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                          new Vector2(12f, -32f), new Vector2(-12f, -8f));

                var objective = runtime.CurrentVisibleObjective();
                var body = UiFactory.CreateText("Body", card.transform,
                    objective != null ? Loc.T(objective.titleKey) : Loc.T("ui.home.case_state." + runtime.State),
                    16, TextAnchor.UpperLeft, UiFactory.TextMuted);
                UiFactory.SetAnchoredRect(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                          new Vector2(12f, 34f), new Vector2(-12f, -34f));

                var deadline = UiFactory.CreateText("Deadline", card.transform, string.Empty, 15,
                                                    TextAnchor.LowerLeft, UiFactory.TextMuted);
                UiFactory.Pin(deadline.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                              new Vector2(12f, 8f), new Vector2(300f, 24f));
                _deadlineCases.Add(runtime);
                _deadlineLabels.Add(deadline);

                var caseId = runtime.CaseId;
                var track = UiFactory.CreateButton("Track", card.transform, Loc.T("ui.home.track"), 14,
                                                   () => ServiceHub.Cases.SetTracked(caseId));
                UiFactory.Pin(track.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                              new Vector2(-12f, 8f), new Vector2(120f, 24f));
            }

            // The handover note is also the nightly briefing (GDD 13.2): each shift it names
            // one more thing worth checking at the door, and that check appears on the
            // interphone panel the same night. The player's checklist grows with the threat
            // instead of being handed over complete on night zero.
            int night = ServiceHub.State.NightIndex;
            _memo.text = Loc.T("ui.home.memo.title") + "\n\n" +
                         Loc.T("ui.home.memo.body") + "\n\n— — —\n\n" +
                         Loc.T("ui.home.briefing." + night + ".title") + "\n" +
                         Loc.T("ui.home.briefing." + night + ".body");
        }

        static Color CardColor(CaseState state)
        {
            switch (state)
            {
                case CaseState.DecisionReady: return new Color(0.30f, 0.26f, 0.14f);
                case CaseState.ResolvedCorrect:
                case CaseState.ConsequenceApplied: return new Color(0.14f, 0.24f, 0.22f);
                case CaseState.ResolvedWrong: return new Color(0.28f, 0.16f, 0.15f);
                default: return UiFactory.Panel;
            }
        }
    }

    // =========================================================================
    // CCTV (GDD 16.8)
    // =========================================================================

    public sealed class CctvApp : PcApp
    {
        readonly List<RawImage> _tiles = new List<RawImage>();
        readonly List<Text> _tileLabels = new List<Text>();
        readonly List<Button> _tileButtons = new List<Button>();
        readonly List<string> _visibleIds = new List<string>(6);

        RectTransform _gridRoot;
        Text _pageLabel;
        RectTransform _singleRoot;
        RawImage _singleImage;
        Text _singleLabel;
        Slider _rewindSlider;
        Text _rewindLabel;
        Text _reportResult;
        float _reportResultClearsAt;
        bool _brightnessBoost;

        public CctvApp() { AppId = AppIds.Cctv; TitleKey = "ui.app.cctv"; }

        protected override void Build(RectTransform root)
        {
            _gridRoot = UiFactory.CreateRect("Grid", root);
            UiFactory.Stretch(_gridRoot, 12f, 12f);

            // The grid draws six of the twelve channels (GDD 12.1), so it needs a second page.
            // Without one, CAM-07..CAM-12 could not be watched at all - and the elevator, the
            // records room and the parking ramp all live back there.
            var tiles = UiFactory.CreateRect("Tiles", _gridRoot);
            UiFactory.SetAnchoredRect(tiles, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(0f, 40f), new Vector2(0f, 0f));

            var grid = tiles.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(320f, 200f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            BuildPageBar(_gridRoot);

            for (int i = 0; i < 6; i++)
            {
                var tile = UiFactory.CreatePanel("Tile" + i, tiles, Color.black);
                var image = UiFactory.CreateRect("Feed", tile.transform).gameObject.AddComponent<RawImage>();
                UiFactory.Stretch(image.rectTransform, 2f, 20f);
                image.color = Color.white;

                var label = UiFactory.CreateText("Label", tile.transform, string.Empty, 14, TextAnchor.LowerLeft);
                UiFactory.SetAnchoredRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                          new Vector2(6f, 2f), new Vector2(-6f, 18f));

                int index = i;
                var button = tile.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OnTileClicked(index));

                _tiles.Add(image);
                _tileLabels.Add(label);
                _tileButtons.Add(button);
            }

            BuildSingleView(root);
        }

        void BuildPageBar(RectTransform gridRoot)
        {
            var bar = UiFactory.CreateRect("Pager", gridRoot);
            UiFactory.SetAnchoredRect(bar, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(0f, 4f), new Vector2(0f, 34f));

            var previous = UiFactory.CreateButton("PrevPage", bar, Loc.T("ui.cctv.prev_page"), 15, () => Page(-1));
            UiFactory.Pin(previous.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                          new Vector2(0f, 0f), new Vector2(150f, 26f));

            var next = UiFactory.CreateButton("NextPage", bar, Loc.T("ui.cctv.next_page"), 15, () => Page(1));
            UiFactory.Pin(next.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                          new Vector2(158f, 0f), new Vector2(150f, 26f));

            _pageLabel = UiFactory.CreateText("PageLabel", bar, string.Empty, 15, TextAnchor.MiddleLeft,
                                              UiFactory.TextMuted);
            UiFactory.Pin(_pageLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                          new Vector2(322f, 0f), new Vector2(420f, 26f));
        }

        void Page(int delta)
        {
            ServiceHub.Cctv.SetGridPage(ServiceHub.Cctv.GridPage + delta);
        }

        void BuildSingleView(RectTransform root)
        {
            _singleRoot = UiFactory.CreateRect("Single", root);
            UiFactory.Stretch(_singleRoot, 12f, 12f);

            var frame = UiFactory.CreatePanel("Frame", _singleRoot, Color.black);
            UiFactory.SetAnchoredRect(frame.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(0f, 142f), new Vector2(0f, -34f));

            _singleImage = UiFactory.CreateRect("Feed", frame.transform).gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(_singleImage.rectTransform, 2f, 2f);

            _singleLabel = UiFactory.CreateText("Label", _singleRoot, string.Empty, 18, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(_singleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(4f, -30f), new Vector2(-4f, 0f));

            BuildReportBar(_singleRoot);

            var bar = UiFactory.CreateRect("Controls", _singleRoot);
            UiFactory.SetAnchoredRect(bar, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(0f, 0f), new Vector2(0f, 88f));
            UiFactory.AddHorizontalLayout(bar, 8f, new RectOffset(0, 0, 8, 8));

            UiFactory.CreateButton("Back", bar, Loc.T("ui.cctv.back_to_grid"), 15, () =>
            {
                ServiceHub.Cctv.SetGridMode(true);
                UpdateMode();
            });

            UiFactory.CreateButton("Snapshot", bar, Loc.T("ui.cctv.snapshot"), 15, () =>
            {
                int offset = _rewindSlider != null ? (int)_rewindSlider.value : 0;
                Net.NetShift.Request(Net.NetShift.ShiftAct.TakeSnapshot,
                                     ServiceHub.Cctv.SelectedCameraId, null, offset);
            });

            UiFactory.CreateButton("Brightness", bar, Loc.T("ui.cctv.brightness"), 15, () =>
            {
                _brightnessBoost = !_brightnessBoost;
            });

            var rewindGroup = UiFactory.CreateRect("Rewind", bar);
            _rewindLabel = UiFactory.CreateText("RewindLabel", rewindGroup, string.Empty, 14, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(_rewindLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(0f, -18f), new Vector2(0f, 0f));

            _rewindSlider = UiFactory.CreateSlider("RewindSlider", rewindGroup, 0f, CctvService.RewindSeconds, 0f, null);
            UiFactory.SetAnchoredRect(_rewindSlider.GetComponent<RectTransform>(),
                                      new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(0f, 12f), new Vector2(0f, 30f));
        }

        /// <summary>
        /// The verb the CCTV half of the game was missing. Watching an anomaly used to do
        /// nothing on its own; now the player names the channel and the GDD 12.3 family, and
        /// is right or wrong about it.
        ///
        /// Deliberately no "something is here" indicator: GDD 12.2 forbids labelling feed
        /// state, and an indicator would answer the only question the player is being asked.
        /// A sighting can only be claimed once, wrong answers included, so trying all five
        /// costs more than it pays.
        /// </summary>
        void BuildReportBar(RectTransform root)
        {
            var bar = UiFactory.CreateRect("ReportBar", root);
            UiFactory.SetAnchoredRect(bar, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(0f, 92f), new Vector2(0f, 138f));

            var label = UiFactory.CreateText("ReportLabel", bar, Loc.T("ui.cctv.report.prompt"), 14,
                                             TextAnchor.MiddleLeft, UiFactory.TextMuted);
            UiFactory.Pin(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                          new Vector2(0f, 0f), new Vector2(150f, 26f));

            _reportResult = UiFactory.CreateText("ReportResult", bar, string.Empty, 14,
                                                 TextAnchor.MiddleLeft, UiFactory.TextMuted);
            UiFactory.Pin(_reportResult.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                          new Vector2(0f, 0f), new Vector2(260f, 26f));

            var categories = new[]
            {
                AnomalyCategory.ObjectChange,
                AnomalyCategory.PersonContradiction,
                AnomalyCategory.TimeEnvironment,
                AnomalyCategory.SpaceContradiction,
                AnomalyCategory.DirectThreat
            };

            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                var button = UiFactory.CreateButton("Report_" + category, bar,
                    Loc.T("ui.cctv.report.category." + category), 13, () => SubmitReport(category));
                UiFactory.Pin(button.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                              new Vector2(156f + i * 132f, 0f), new Vector2(128f, 26f));
            }
        }

        void SubmitReport(AnomalyCategory category)
        {
            var cameraId = ServiceHub.Cctv.SelectedCameraId;
            if (string.IsNullOrEmpty(cameraId)) return;

            // Whether the claim was right is the host's answer to give - it is the one that
            // holds the sighting list - so the outcome comes back rather than being computed
            // here. On a single-player machine and on the host it comes back immediately.
            Net.NetShift.OnReportAnswered = ShowReportOutcome;
            Net.NetShift.RequestReport(cameraId, category);
        }

        void ShowReportOutcome(ReportOutcome outcome)
        {
            _reportResult.text = Loc.T("ui.cctv.report.result." + outcome);
            _reportResult.color = outcome == ReportOutcome.Correct ? UiFactory.Ok
                                : outcome == ReportOutcome.WrongCategory ? UiFactory.Warning
                                : UiFactory.Danger;
            _reportResultClearsAt = Time.unscaledTime + 4f;
        }

        void OnTileClicked(int index)
        {
            if (index >= _visibleIds.Count) return;
            ServiceHub.Cctv.Select(_visibleIds[index]);
            ServiceHub.Cctv.SetGridMode(false);
            UpdateMode();
        }

        public override void OnOpen()
        {
            ServiceHub.Cctv.SetGridMode(true);
            UpdateMode();
        }

        public override void OnClose()
        {
            if (Shell.Rig != null) Shell.Rig.SetVisible(null, null, false);
        }

        void UpdateMode()
        {
            bool grid = ServiceHub.Cctv.GridMode;
            _gridRoot.gameObject.SetActive(grid);
            _singleRoot.gameObject.SetActive(!grid);
        }

        public override void Refresh()
        {
            var service = ServiceHub.Cctv;
            var rig = Shell.Rig;

            if (service.GridMode)
            {
                var channels = service.VisibleGridChannels();
                _visibleIds.Clear();

                for (int i = 0; i < _tiles.Count; i++)
                {
                    bool has = i < channels.Count;
                    _tiles[i].transform.parent.gameObject.SetActive(has);
                    if (!has) continue;

                    var channel = channels[i];
                    _visibleIds.Add(channel.CameraId);

                    // Watching a channel is what keeps its floor streamed in (GDD 20.5).
                    bool ready = RequestChannelZone(channel);

                    _tiles[i].texture = ready && rig != null ? rig.TextureFor(channel.CameraId) : null;
                    _tiles[i].color = ready ? TintFor(channel.State) : new Color(0.18f, 0.18f, 0.20f);
                    _tileLabels[i].text = channel.CameraId + "  " + Loc.T(channel.Definition.labelKey) + "   " +
                                          (ready ? StatusText(channel) : Loc.T("ui.cctv.loading"));
                    _tileLabels[i].color = channel.UnreviewedMotion ? UiFactory.Warning : UiFactory.TextMuted;
                }

                if (_pageLabel != null)
                {
                    int pages = (service.Channels.Count + 5) / 6;
                    _pageLabel.text = Loc.T("ui.cctv.page", service.GridPage + 1, pages) +
                                      "   " + Loc.T("ui.cctv.unreviewed", service.UnreviewedMotionCount());
                }

                if (rig != null) rig.SetVisible(_visibleIds, service.SelectedCameraId, false);
            }
            else
            {
                var channel = service.Find(service.SelectedCameraId);
                if (channel == null) return;

                _visibleIds.Clear();
                _visibleIds.Add(channel.CameraId);
                if (rig != null) rig.SetVisible(_visibleIds, channel.CameraId, true);

                bool ready = RequestChannelZone(channel);

                int offset = _rewindSlider != null ? (int)_rewindSlider.value : 0;
                var sample = offset > 0 ? service.Rewind(channel.CameraId, offset)
                                        : new FeedSample(ServiceHub.Clock.GameSecond, channel.State,
                                                         channel.ActiveAnomalyId, channel.Motion);

                _singleImage.texture = ready && rig != null ? rig.TextureFor(channel.CameraId) : null;
                _singleImage.color = ready
                    ? TintFor(sample.State) * (_brightnessBoost ? 1.2f : 1f)
                    : new Color(0.18f, 0.18f, 0.20f);

                _singleLabel.text = channel.CameraId + "  " + Loc.T(channel.Definition.labelKey) + "    " +
                                    (ready ? service.TimestampFor(sample.GameSecond)
                                           : Loc.T("ui.cctv.loading"));
                _rewindLabel.text = offset > 0 ? Loc.T("ui.cctv.rewind_seconds", offset) : Loc.T("ui.cctv.live");

                if (_reportResult != null && _reportResultClearsAt > 0f
                    && Time.unscaledTime >= _reportResultClearsAt)
                {
                    _reportResult.text = string.Empty;
                    _reportResultClearsAt = 0f;
                }
            }
        }

        /// <summary>
        /// Keeps the channel's floor resident while it is on screen and reports whether the
        /// picture can be shown yet. GDD 16.8 keeps this distinct from SIGNAL LOST: a
        /// streaming feed says "loading", a haunted one does not.
        /// </summary>
        static bool RequestChannelZone(CctvService.Channel channel)
        {
            var streamer = ServiceHub.Zones;
            if (streamer == null || channel.Definition == null) return true;

            var zoneId = channel.Definition.zoneId;
            streamer.RequestZone(zoneId);
            return streamer.IsZoneReady(zoneId);
        }

        /// <summary>
        /// GDD 16.8: base CCTV look is desaturated with a slight contrast lift; archival
        /// footage is warmer. The feed state is never spelled out in text.
        /// </summary>
        static Color TintFor(FeedState state)
        {
            switch (state)
            {
                case FeedState.Archive: return new Color(1.0f, 0.94f, 0.82f, 1f);
                case FeedState.Delayed: return new Color(0.92f, 0.95f, 1.0f, 1f);
                case FeedState.SignalLost: return new Color(0.25f, 0.25f, 0.25f, 1f);
                default: return new Color(0.86f, 0.88f, 0.88f, 1f);
            }
        }

        static string StatusText(CctvService.Channel channel)
        {
            if (channel.State == FeedState.SignalLost) return Loc.T("ui.cctv.signal_lost");
            if (channel.Motion) return Loc.T("ui.cctv.motion");
            return ServiceHub.Cctv.TimestampFor(ServiceHub.Clock.GameSecond);
        }
    }

    // =========================================================================
    // Phone / interphone (GDD 16.9)
    // =========================================================================

    /// <summary>
    /// The interphone: the front door, and the incoming handset when one is ringing.
    ///
    /// Half of this panel is records and half of it is the person, and the two halves are
    /// deliberately the same size. A caretaker who only reads the left-hand column is doing
    /// data entry; one who only reads the right-hand column is guessing about strangers. The
    /// judgement is graded on facts from either side (GDD 13.2), and there is no caller in the
    /// game who can be settled from one side alone.
    /// </summary>
    public sealed class PhoneApp : PcApp
    {
        RawImage _camera;
        Text _info;
        Text _dialogue;
        RectTransform _choiceRoot;
        RectTransform _actionRoot;
        RectTransform _visitorActions;
        RectTransform _callActions;
        RectTransform _observeRoot;
        RectTransform _tacticRoot;
        Text _observeHeader;
        /// <summary>
        /// The line the choice buttons on screen were built for.
        ///
        /// The line itself, not its node id. Every caller's conversation opens on a node
        /// called "start", so a panel that remembered only the id kept the last caller's
        /// questions up for the next one whenever both were sitting on their first line - a
        /// first visit offering "did you not just go in?".
        /// </summary>
        Dialogue.DialogueLine _builtForLine;
        string _observeSignature;
        string _tacticSignature;
        string _ladderSignature;

        public PhoneApp() { AppId = AppIds.Phone; TitleKey = "ui.app.phone"; }

        protected override void Build(RectTransform root)
        {
            // ---- left column: what the camera sees, and what is worth noticing in it ----
            var left = UiFactory.CreatePanel("Camera", root, Color.black);
            UiFactory.SetAnchoredRect(left.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                                      new Vector2(12f, 216f), new Vector2(-6f, -12f));
            _camera = UiFactory.CreateRect("Feed", left.transform).gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(_camera.rectTransform, 2f, 2f);

            var observe = UiFactory.CreatePanel("Observations", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(observe.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0f),
                                      new Vector2(12f, 96f), new Vector2(-6f, 210f));

            _observeHeader = UiFactory.CreateText("ObserveHeader", observe.transform, string.Empty, 13,
                                                  TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(_observeHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(8f, -20f), new Vector2(-8f, -4f));
            _observeHeader.color = UiFactory.TextMuted;

            _observeRoot = UiFactory.CreateRect("ObserveList", observe.rectTransform);
            UiFactory.SetAnchoredRect(_observeRoot, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(6f, 6f), new Vector2(-6f, -22f));
            UiFactory.AddVerticalLayout(_observeRoot, 3f);

            // ---- right column: the claim, the records, and how they are holding up ----
            var right = UiFactory.CreatePanel("Info", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(right.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                                      new Vector2(6f, 150f), new Vector2(-12f, -12f));
            _info = UiFactory.CreateText("InfoText", right.transform, string.Empty, 15, TextAnchor.UpperLeft);
            UiFactory.Stretch(_info.rectTransform, 12f, 10f);

            _tacticRoot = UiFactory.CreateRect("Tactics", root);
            UiFactory.SetAnchoredRect(_tacticRoot, new Vector2(0.5f, 0f), new Vector2(1f, 0f),
                                      new Vector2(6f, 96f), new Vector2(-12f, 144f));
            UiFactory.AddHorizontalLayout(_tacticRoot, 4f);

            // ---- the line they are speaking, and what can be said back ----
            _dialogue = UiFactory.CreateText("Dialogue", root, string.Empty, 17, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(_dialogue.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(12f, 54f), new Vector2(-12f, 92f));

            _choiceRoot = UiFactory.CreateRect("Choices", root);
            UiFactory.SetAnchoredRect(_choiceRoot, new Vector2(0f, 0f), new Vector2(0.55f, 0f),
                                      new Vector2(12f, 8f), new Vector2(-6f, 48f));
            UiFactory.AddHorizontalLayout(_choiceRoot, 6f);

            _actionRoot = UiFactory.CreateRect("Actions", root);
            UiFactory.SetAnchoredRect(_actionRoot, new Vector2(0.55f, 0f), new Vector2(1f, 0f),
                                      new Vector2(6f, 8f), new Vector2(-12f, 48f));
            UiFactory.AddHorizontalLayout(_actionRoot, 6f);

            BuildActions();
        }

        void BuildActions()
        {
            // The access ladder is rebuilt per caller rather than built once, because what is
            // on it depends on the night and on the caller (v3.0 38.2). A courier is never
            // offered the fourth floor.
            _visitorActions = UiFactory.CreateRect("VisitorActions", _actionRoot);
            UiFactory.AddHorizontalLayout(_visitorActions, 4f);

            _callActions = UiFactory.CreateRect("CallActions", _actionRoot);
            UiFactory.AddHorizontalLayout(_callActions, 6f);

            UiFactory.CreateButton("Answer", _callActions, Loc.T("ui.phone.answer"), 15,
                                   () => Net.NetShift.Request(Net.NetShift.ShiftAct.AnswerPhone));
            UiFactory.CreateButton("DeclineCall", _callActions, Loc.T("ui.phone.decline"), 15,
                                   () => Net.NetShift.Request(Net.NetShift.ShiftAct.DeclinePhone));

            _callActions.gameObject.SetActive(false);
        }

        public override void OnOpen() { TryStartVisitorTalk(); }

        /// <summary>
        /// Opens the interphone conversation for whoever is at the door.
        ///
        /// This is called every frame rather than only when the app opens. Two things can hold
        /// the dialogue service busy at the moment the app is opened - the radio that starts
        /// itself at 22:00 (GDD 9.1) and any phone call - and when that happened the caller's
        /// conversation was skipped for good. The player then had a door panel with no
        /// questions on it, which also meant no route to the independent facts GDD 13.2
        /// requires before a judgement can be graded correct.
        /// </summary>
        void TryStartVisitorTalk()
        {
            var visitor = ServiceHub.Interphone.Active;
            if (visitor == null || string.IsNullOrEmpty(visitor.conversationId)) return;
            if (ServiceHub.Dialogue.IsActive) return;

            ServiceHub.Dialogue.Start(visitor.conversationId);
        }

        public override void Refresh()
        {
            // An incoming call takes the handset before the front door does - but only if
            // there is actually a call on the line. RefreshCall used to be entered on the
            // strength of the flags alone and then return early when Phone.Active came back
            // null, which left whatever was last drawn frozen on screen.
            if ((ServiceHub.Phone.IsRinging || ServiceHub.Phone.InCall) && ServiceHub.Phone.Active != null)
            {
                RefreshCall();
                return;
            }

            var visitor = ServiceHub.Interphone.Active;
            _visitorActions.gameObject.SetActive(visitor != null);
            _callActions.gameObject.SetActive(false);
            _observeRoot.gameObject.SetActive(visitor != null);
            _tacticRoot.gameObject.SetActive(visitor != null);

            if (visitor == null)
            {
                _info.text = Loc.T("ui.phone.no_visitor");
                _observeHeader.text = string.Empty;
                _dialogue.text = string.Empty;
                _camera.texture = null;
                ClearChoices();
                ClearObservations();
                ClearTactics();
                UiFactory.ClearChildren(_visitorActions);
                _ladderSignature = null;
                _builtForLine = null;
                return;
            }

            TryStartVisitorTalk();

            _camera.texture = Shell.Rig != null ? Shell.Rig.TextureFor(visitor.cameraId) : null;
            if (Shell.Rig != null)
            {
                var ids = new List<string> { visitor.cameraId };
                Shell.Rig.SetVisible(ids, visitor.cameraId, false);
            }

            RefreshInfo(visitor);
            RefreshAccessLadder(visitor);
            RefreshObservations();
            RefreshTactics();
            RefreshScript(visitor);
        }

        // -----------------------------------------------------------------
        // the records half
        // -----------------------------------------------------------------

        void RefreshInfo(VisitorDefinition visitor)
        {
            var interphone = ServiceHub.Interphone;
            var read = interphone.Read;

            // What the caller says about themselves. This part is free - anyone at a door will
            // tell you who they are.
            var text = Loc.T(visitor.nameKey) + "\n" + Loc.T(visitor.purposeKey) + "\n\n" +
                       Loc.T("ui.phone.claimed_unit", visitor.targetUnit) + "\n\n" +
                       Loc.T("ui.phone.section.verify") + "\n";

            if (visitor.checks != null)
            {
                int night = ServiceHub.State.NightIndex;

                for (int i = 0; i < visitor.checks.Length; i++)
                {
                    var check = visitor.checks[i];

                    // The checklist grows with the threat: a check the player has not been
                    // taught yet is not on the panel at all, and the nightly briefing is what
                    // puts it there.
                    if (check.fromNight > night) continue;

                    // The line names what could be checked and where. The answer arrives only
                    // when the player has been there and looked - that walk is the game.
                    bool known = interphone.HasConsulted(check.crossReferenceAppId);
                    text += Loc.T(check.labelKey) + " : " +
                            (known ? Loc.T(check.valueKey)
                                   : Loc.T("ui.phone.check_unknown", Loc.T("ui.app." + check.crossReferenceAppId)))
                            + "\n";
                }
            }

            // How they are holding up. Never a number and never a verdict (GDD 13.5) - the
            // panel says what a person watching would say, and a person watching cannot tell
            // fear from guilt either.
            text += "\n" + Loc.T("ui.door.section.state") + "\n" +
                    Loc.T(read.AgitationKey) + "\n";

            // Somebody who has stood here before, and how it went. The building is small
            // enough that this is the ordinary case rather than a special one.
            var memory = MemoryLineKey(read.LastDecision);
            if (memory != null) text += Loc.T(memory) + "\n";

            if (!string.IsNullOrEmpty(read.LastReplyKey))
                text += "\n“" + Loc.T(read.LastReplyKey) + "”\n";

            text += "\n" + Loc.T("ui.phone.checks_consulted", interphone.EstablishedFactCount) + "\n";

            if (!interphone.HasEnoughInformation)
                text += Loc.T("ui.phone.checks_tip") + "\n";

            _info.text = text;
        }

        static string MemoryLineKey(VisitorAccessLevel last)
        {
            switch (last)
            {
                case VisitorAccessLevel.Pending:   return null;
                case VisitorAccessLevel.Reject:    return "ui.door.remembers.refused";
                case VisitorAccessLevel.Hold:      return "ui.door.remembers.held";
                case VisitorAccessLevel.Vestibule: return "ui.door.remembers.vestibule";
            }
            return "ui.door.remembers.admitted";
        }

        /// <summary>
        /// How far in this caller can be let, on this night.
        ///
        /// The ladder is not a menu of equivalent options. Every rung costs something in a
        /// different direction, and the panel refuses to say which one is right - the only
        /// help it gives is the reach of each level, because a caretaker who has read the
        /// handover knows that much and the player should not have to memorise it.
        /// </summary>
        void RefreshAccessLadder(VisitorDefinition visitor)
        {
            var offered = visitor.OfferedAccess(ServiceHub.State.NightIndex);

            var signature = visitor.visitorId + "|";
            for (int i = 0; i < offered.Length; i++) signature += offered[i] + ";";

            if (signature == _ladderSignature) return;
            _ladderSignature = signature;

            UiFactory.ClearChildren(_visitorActions);

            for (int i = 0; i < offered.Length; i++)
            {
                var level = offered[i];
                var label = Loc.T(VisitorAccess.LabelKey(level));

                var button = UiFactory.CreateButton("Access_" + level, _visitorActions, label, 13,
                                                    () => Net.NetShift.RequestGrant(level));

                var text = button.GetComponentInChildren<Text>();
                if (text == null) continue;

                // Turning somebody away and handing over the building are the two ends of the
                // decision, and they are the two the player should never press by accident.
                if (level == VisitorAccessLevel.Reject) text.color = UiFactory.Danger;
                else if (level == VisitorAccessLevel.FullTemporary) text.color = UiFactory.Warning;
            }
        }

        // -----------------------------------------------------------------
        // the person half
        // -----------------------------------------------------------------

        /// <summary>
        /// The observation board.
        ///
        /// Every line is something the caretaker can see or hear right now, described and
        /// never interpreted. Clicking one writes it into the shift notes; the panel does not
        /// say whether it was worth writing down, because that is the judgement being asked
        /// for. Three noted pieces of noise look exactly like three noted facts until the
        /// decision comes back ungraded.
        /// </summary>
        void RefreshObservations()
        {
            var read = ServiceHub.Interphone.Read;
            var visible = read.VisibleTells();

            _observeHeader.text = DoorReadService.AtTheGlass
                ? Loc.T("ui.door.section.observations_glass")
                : Loc.T("ui.door.section.observations");

            var signature = DoorReadService.AtTheGlass ? "glass|" : "desk|";
            for (int i = 0; i < visible.Count; i++)
                signature += visible[i].tellId + (read.HasNoted(visible[i].tellId) ? "*" : "") + ";";

            if (signature == _observeSignature) return;
            _observeSignature = signature;

            ClearObservations();

            if (visible.Count == 0)
            {
                var empty = UiFactory.CreateText("None", _observeRoot, Loc.T("ui.door.observations.none"),
                                                  13, TextAnchor.UpperLeft);
                empty.color = UiFactory.TextMuted;
                UiFactory.SetHeight(empty.gameObject, 34f);
                return;
            }

            for (int i = 0; i < visible.Count; i++)
            {
                var tell = visible[i];
                bool noted = read.HasNoted(tell.tellId);
                var label = (noted ? "■ " : "□ ") + Loc.T(tell.labelKey);

                var button = UiFactory.CreateButton("Obs_" + tell.tellId, _observeRoot, label, 13,
                                                    () => Net.NetShift.RequestNote(tell.tellId));
                UiFactory.SetHeight(button.gameObject, 22f);

                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.alignment = TextAnchor.MiddleLeft;
                    // Noted and unnoted, and nothing else. Colouring a tell by what it is
                    // worth would hand the player the answer the whole panel exists to ask.
                    text.color = noted ? UiFactory.Accent : UiFactory.TextPrimary;
                }
            }

            // The lobby is where the rest of it is. Said once, at the bottom, without saying
            // whether this particular caller has anything down there.
            if (!DoorReadService.AtTheGlass)
            {
                var hint = UiFactory.CreateText("GlassHint", _observeRoot, Loc.T("ui.door.observations.go_look"),
                                                 12, TextAnchor.UpperLeft);
                hint.color = UiFactory.TextMuted;
                UiFactory.SetHeight(hint.gameObject, 30f);
            }
        }

        /// <summary>
        /// The five ways to push (GDD 13.5). Each one costs game time, each one can be used
        /// once per caller, and the label carries the price so nobody spends a minute by
        /// accident.
        /// </summary>
        void RefreshTactics()
        {
            var read = ServiceHub.Interphone.Read;

            var signature = string.Empty;
            for (int i = 0; i < DoorRead.All.Length; i++)
                signature += (read.BlockedReasonKey(DoorRead.All[i]) ?? "-") + ";";

            if (signature == _tacticSignature) return;
            _tacticSignature = signature;

            UiFactory.ClearChildren(_tacticRoot);

            for (int i = 0; i < DoorRead.All.Length; i++)
            {
                var tactic = DoorRead.All[i];
                var blocked = read.BlockedReasonKey(tactic);

                var label = Loc.T("ui.door.tactic." + tactic.ToString().ToLowerInvariant()) +
                            "\n" + Loc.T("ui.door.tactic.cost", DoorReadService.CostOf(tactic) / 60);

                var button = UiFactory.CreateButton("Tactic_" + tactic, _tacticRoot, label, 12,
                                                    () => Net.NetShift.RequestTactic(tactic));
                button.interactable = blocked == null;

                var text = button.GetComponentInChildren<Text>();
                if (text != null) text.color = blocked == null ? UiFactory.TextPrimary : UiFactory.TextMuted;
            }
        }

        // -----------------------------------------------------------------
        // the caller's own script
        // -----------------------------------------------------------------

        void RefreshScript(VisitorDefinition visitor)
        {
            // Only this caller's conversation belongs on the door panel. The radio and the
            // handset use the same dialogue service, and printing their lines here made the
            // courier appear to be reading out a three-day-old radio log.
            var line = ServiceHub.Dialogue.Current != null &&
                       ServiceHub.Dialogue.Current.conversationId == visitor.conversationId
                ? ServiceHub.Dialogue.CurrentLine
                : null;

            if (line == null) { _dialogue.text = string.Empty; ClearChoices(); _builtForLine = null; return; }

            _dialogue.text = Loc.T(line.SpeakerKey) + ": " + Loc.T(line.TextKey);

            if (ReferenceEquals(_builtForLine, line)) return;
            _builtForLine = line;
            ClearChoices();

            for (int i = 0; i < line.Choices.Count; i++)
            {
                var choice = line.Choices[i];
                UiFactory.CreateButton("Choice_" + choice.choiceId, _choiceRoot, Loc.T(choice.textKey), 15, () =>
                {
                    // The caller's own account is one source however many questions are asked
                    // of it (GDD 13.2), and it is never enough on its own.
                    //
                    // Either caretaker can ask. v3.0 38.6 wants the answers to be able to
                    // differ between them later; for now they hear the same one, and the
                    // question itself is shared because the caller only says it once.
                    Net.NetShift.RequestMarkChecked(choice.choiceId);
                    Net.NetShift.RequestDialogueChoice(choice.choiceId);
                });
            }

            if (line.Choices.Count == 0 && ServiceHub.Dialogue.IsActive)
                UiFactory.CreateButton("Continue", _choiceRoot, Loc.T("ui.dialogue.continue"), 15,
                                       () => Net.NetShift.Request(Net.NetShift.ShiftAct.AdvanceDialogue));
        }

        /// <summary>Incoming call view: caller, then the conversation once answered.</summary>
        void RefreshCall()
        {
            var call = ServiceHub.Phone.Active;
            if (call == null) return;   // Refresh() has already checked; kept as a guard.

            _camera.texture = null;
            _visitorActions.gameObject.SetActive(false);
            _callActions.gameObject.SetActive(ServiceHub.Phone.IsRinging);
            _observeRoot.gameObject.SetActive(false);
            _tacticRoot.gameObject.SetActive(false);
            _observeHeader.text = string.Empty;

            _info.text = Loc.T("ui.phone.incoming") + "\n\n" +
                         Loc.T(call.callerNameKey) + "\n" +
                         Loc.T("ui.phone.number", call.callerNumber) + "\n\n" +
                         (ServiceHub.Phone.WaitingCount > 1
                             ? Loc.T("ui.phone.queue", ServiceHub.Phone.WaitingCount - 1)
                             : string.Empty);

            var line = ServiceHub.Dialogue.CurrentLine;
            if (!ServiceHub.Phone.InCall || line == null)
            {
                _dialogue.text = string.Empty;
                ClearChoices();
                _builtForLine = null;
                return;
            }

            _dialogue.text = Loc.T(line.SpeakerKey) + ": " + Loc.T(line.TextKey);

            if (ReferenceEquals(_builtForLine, line)) return;
            _builtForLine = line;
            ClearChoices();

            for (int i = 0; i < line.Choices.Count; i++)
            {
                var choice = line.Choices[i];
                UiFactory.CreateButton("Choice_" + choice.choiceId, _choiceRoot, Loc.T(choice.textKey), 15,
                                       () => Net.NetShift.RequestDialogueChoice(choice.choiceId));
            }

            if (line.Choices.Count == 0)
                UiFactory.CreateButton("Continue", _choiceRoot, Loc.T("ui.dialogue.continue"), 15,
                                       () => Net.NetShift.Request(Net.NetShift.ShiftAct.AdvanceDialogue));
        }

        void ClearChoices() { UiFactory.ClearChildren(_choiceRoot); }

        void ClearObservations()
        {
            UiFactory.ClearChildren(_observeRoot);
            _observeSignature = null;
        }

        void ClearTactics()
        {
            UiFactory.ClearChildren(_tacticRoot);
            _tacticSignature = null;
        }
    }

    // =========================================================================
    // Resident database (GDD 16.10)
    // =========================================================================

    public sealed class ResidentsApp : PcApp
    {
        /// <summary>Resonance at which the photographs stop behaving like photographs.</summary>
        const int PortraitTurnsResonance = 60;

        InputField _search;
        RectTransform _results;
        Text _detail;
        ResidentPortrait _portrait;
        RectTransform _actions;
        ResidentDefinition _shown;
        string _actionSignature;
        string _lastQuery;   // null forces the first rebuild
        int _lastResultCount = -1;

        public ResidentsApp() { AppId = AppIds.Residents; TitleKey = "ui.app.residents"; }

        protected override void Build(RectTransform root)
        {
            _search = UiFactory.CreateInputField("Search", root, Loc.T("ui.residents.search_hint"), 17);
            UiFactory.SetAnchoredRect(_search.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.55f, 1f),
                                      new Vector2(12f, -46f), new Vector2(-6f, -12f));

            ScrollRect scroll;
            _results = UiFactory.CreateScrollView("Results", root, out scroll);
            UiFactory.SetAnchoredRect((RectTransform)scroll.transform, new Vector2(0f, 0f), new Vector2(0.55f, 1f),
                                      new Vector2(12f, 12f), new Vector2(-6f, -52f));

            var detailPanel = UiFactory.CreatePanel("Detail", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(detailPanel.rectTransform, new Vector2(0.55f, 0f), new Vector2(1f, 1f),
                                      new Vector2(6f, 12f), new Vector2(-12f, -12f));

            // GDD 2.3 hook #7 needs a face to turn, and a record with a photograph on it reads
            // as a person rather than a row.
            _portrait = ResidentPortrait.Create(detailPanel.transform,
                                                new Vector2(0f, 1f), new Vector2(0f, 1f),
                                                new Vector2(14f, -160f), new Vector2(134f, -12f));

            _detail = UiFactory.CreateText("DetailText", detailPanel.transform, string.Empty, 16, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(_detail.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(146f, 12f), new Vector2(-14f, -12f));

            // What can be done to the open record (v5.1 13). Almost every row has nothing
            // here; the bar only exists for the one that can be printed, sent out or removed.
            _actions = UiFactory.CreateRect("RecordActions", detailPanel.transform);
            UiFactory.SetAnchoredRect(_actions, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(14f, 12f), new Vector2(-14f, 54f));
            UiFactory.AddHorizontalLayout(_actions, 6f);
        }

        public override void OnOpen()
        {
            _lastQuery = null;

            // GDD 16.10. The sync does not wait to be searched for: the row is simply on the
            // list the moment the app is opened, for three seconds, and then it is not.
            if (ServiceHub.Residents.StartQueuedGlimpse())
                EventBus.Publish(new NotificationEvent("ui.notify.db_sync_new_record",
                                                       NotificationSeverity.Warning));
        }

        public override void Refresh()
        {
            var query = _search.text;
            var results = ServiceHub.Residents.Search(query);

            // While the glimpsed row is on the list the count is what makes it appear and
            // then disappear, so the cache below is exactly the right trigger - except on the
            // frame the row expires with the same result count it arrived with, which cannot
            // happen: it is the only thing changing.
            RefreshRecordActions();

            if (query == _lastQuery && results.Count == _lastResultCount) return;
            _lastQuery = query;
            _lastResultCount = results.Count;

            UiFactory.ClearChildren(_results);

            for (int i = 0; i < results.Count; i++)
            {
                var resident = results[i];
                var label = resident.unitNumber + "   " +
                            Loc.T(ServiceHub.Residents.DisplayNameKey(resident)) + "   " +
                            Loc.T("ui.residents.status." + ServiceHub.Residents.StatusOf(resident));

                // A framed panel with the button inset inside it: the list is layout-driven,
                // so the border has to track the row's size every frame rather than copy a
                // rect that is not final yet (Unity resolves layout children asynchronously).
                var frame = UiFactory.CreatePanel("Row_" + resident.residentId, _results, UiFactory.Line);
                UiFactory.SetHeight(frame.gameObject, 34f);

                var button = UiFactory.CreateButton("Fill", frame.transform, label, 16,
                                                    () => ShowDetail(resident));
                UiFactory.Stretch(button.GetComponent<RectTransform>(), 1.5f, 1.5f);
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            }
        }

        /// <summary>
        /// Keeps the action bar, and the detail pane itself, true to the row.
        ///
        /// Rebuilt off a signature rather than every frame, and checked every frame because
        /// what is on offer changes under the screen: a row that has been printed cannot be
        /// printed again, and a row that has been deleted is not there to be looked at.
        /// </summary>
        void RefreshRecordActions()
        {
            if (_shown != null && !ServiceHub.Residents.IsVisible(_shown))
            {
                _shown = null;
                _detail.text = string.Empty;
                if (_portrait != null) _portrait.ResetToProfile();
            }

            var available = ServiceHub.Residents.AvailableActions(_shown);

            string signature = _shown == null ? string.Empty : _shown.residentId;
            for (int i = 0; i < available.Count; i++) signature += "|" + available[i].actionId;
            if (signature == _actionSignature) return;
            _actionSignature = signature;

            UiFactory.ClearChildren(_actions);
            if (_shown == null) return;

            for (int i = 0; i < available.Count; i++)
            {
                var action = available[i];
                var residentId = _shown.residentId;
                UiFactory.CreateButton("Action_" + action.actionId, _actions, Loc.T(action.labelKey), 14,
                    () => Net.NetShift.Request(Net.NetShift.ShiftAct.RecordAction, residentId, action.actionId));
            }
        }

        void ShowDetail(ResidentDefinition resident)
        {
            _shown = resident;
            ServiceHub.Player.ViewRecord(resident.residentId);
            Net.NetShift.Request(Net.NetShift.ShiftAct.ViewRecord, resident.residentId);
            Net.NetShift.RequestMarkChecked("residents." + resident.residentId);

            RefreshPortrait(resident);

            // Opening the 404 row is how the player obtains the record itself (GDD 9.5).
            if (!string.IsNullOrEmpty(resident.evidenceOnView))
                Net.NetShift.Request(Net.NetShift.ShiftAct.AcquireEvidence, resident.evidenceOnView,
                                     null, (int)EvidenceSource.Database);

            var text = Loc.T("ui.residents.unit") + " " + resident.unitNumber + "\n" +
                       Loc.T("ui.residents.name") + " " + Loc.T(resident.nameKey) + "\n" +
                       Loc.T("ui.residents.status_label") + " " +
                       Loc.T("ui.residents.status." + ServiceHub.Residents.StatusOf(resident)) + "\n" +
                       Loc.T("ui.residents.phone") + " ****" + resident.phoneLast4 + "\n" +
                       Loc.T("ui.residents.card") + " " +
                       (string.IsNullOrEmpty(resident.cardId) ? "-" : resident.cardId) + "\n" +
                       Loc.T("ui.residents.last_sync") + " " + resident.lastSyncDate + "\n\n";

            if (resident.vehicles != null && resident.vehicles.Length > 0)
            {
                text += Loc.T("ui.residents.vehicles") + "\n";
                for (int i = 0; i < resident.vehicles.Length; i++)
                    text += "  " + resident.vehicles[i].plate + "\n";
                text += "\n";
            }

            if (resident.recurringVisitors != null && resident.recurringVisitors.Length > 0)
            {
                text += Loc.T("ui.residents.registered_visitors") + "\n";
                for (int i = 0; i < resident.recurringVisitors.Length; i++)
                {
                    var v = resident.recurringVisitors[i];
                    text += "  " + Loc.T(v.nameKey) + " — " + Loc.T(v.purposeKey) +
                            (v.preRegistered ? "  " + Loc.T("ui.residents.pre_registered") : "") + "\n";
                }
                text += "\n";
            }

            if (resident.notes != null && resident.notes.Length > 0)
            {
                text += Loc.T("ui.residents.notes") + "\n";
                for (int i = 0; i < resident.notes.Length; i++)
                    text += "  " + Loc.T(resident.notes[i].noteKey) + "\n";
            }

            _detail.text = text;
        }

        /// <summary>
        /// GDD 2.3 hook #7. Every record opens as an ordinary filed photograph; the head only
        /// comes round on the record that should not exist, or once Harin has taken enough of
        /// an interest that the files stop behaving. Nothing in the UI comments on it.
        /// </summary>
        void RefreshPortrait(ResidentDefinition resident)
        {
            if (_portrait == null) return;

            _portrait.ResetToProfile();

            bool unregistered = ServiceHub.Residents.StatusOf(resident) == ResidentStatus.Unregistered;
            bool resonant = ServiceHub.State.GetStat(StatIds.HarinResonance) >= PortraitTurnsResonance;

            if (unregistered || resonant) _portrait.LookAtViewer();
        }
    }

    // =========================================================================
    // Access log (GDD 16.11)
    // =========================================================================

    public sealed class AccessApp : PcApp
    {
        RectTransform _rows;
        Text _comparison;
        AccessLogEntry _first;
        AccessLogEntry _second;
        int _lastCount = -1;

        public AccessApp() { AppId = AppIds.Access; TitleKey = "ui.app.access"; }

        protected override void Build(RectTransform root)
        {
            ScrollRect scroll;
            _rows = UiFactory.CreateScrollView("Rows", root, out scroll);
            UiFactory.SetAnchoredRect((RectTransform)scroll.transform, new Vector2(0f, 0f), new Vector2(0.62f, 1f),
                                      new Vector2(12f, 12f), new Vector2(-6f, -12f));

            var panel = UiFactory.CreatePanel("Compare", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.62f, 0f), new Vector2(1f, 1f),
                                      new Vector2(6f, 12f), new Vector2(-12f, -12f));

            _comparison = UiFactory.CreateText("CompareText", panel.transform, string.Empty, 16, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(_comparison.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(14f, 52f), new Vector2(-14f, -12f));

            var button = UiFactory.CreateButton("CompareButton", panel.transform, Loc.T("ui.access.compare"), 16, Compare);
            UiFactory.SetAnchoredRect(button.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(14f, 12f), new Vector2(-14f, 44f));
        }

        public override void OnOpen() { _lastCount = -1; }

        public override void Refresh()
        {
            var entries = ServiceHub.AccessLog.Entries;
            if (entries.Count == _lastCount) return;
            _lastCount = entries.Count;

            UiFactory.ClearChildren(_rows);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var label = GameClock.FormatSecond(entry.GameSecond) + "   " + Loc.T(entry.NameKey) + "   " +
                            Loc.T(entry.LocationKey) + "   " +
                            Loc.T(entry.Inbound ? "ui.access.in" : "ui.access.out");

                var captured = entry;
                var button = UiFactory.CreateButton("Row" + i, _rows, label, 15, () => SelectRow(captured));
                UiFactory.SetHeight(button.gameObject, 30f);
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            }
        }

        void SelectRow(AccessLogEntry entry)
        {
            if (_first == null) { _first = entry; }
            else if (_second == null && entry != _first) { _second = entry; }
            else { _first = entry; _second = null; }

            _comparison.text = Loc.T("ui.access.selected",
                _first != null ? GameClock.FormatSecond(_first.GameSecond) : "-",
                _second != null ? GameClock.FormatSecond(_second.GameSecond) : "-");
        }

        void Compare()
        {
            if (_first == null || _second == null)
            {
                _comparison.text = Loc.T("ui.access.select_two");
                return;
            }

            // GDD 16.11: report the facts, never the verdict.
            var result = ServiceHub.AccessLog.Compare(_first, _second);
            _comparison.text =
                Loc.T("ui.access.result.time_delta", Mathf.Abs(result.TimeDeltaSeconds)) + "\n" +
                Loc.T("ui.access.result.same_card", Loc.T(result.SameCard ? "ui.common.yes" : "ui.common.no")) + "\n" +
                Loc.T("ui.access.result.locations", Loc.T(_first.LocationKey), Loc.T(_second.LocationKey)) + "\n" +
                Loc.T("ui.access.result.travel_time", result.TravelSeconds);

            Net.NetShift.RequestMarkChecked("access.compare");
        }
    }

    // =========================================================================
    // Facility meters (GDD 16.12)
    // =========================================================================

    public sealed class FacilityApp : PcApp
    {
        MeterKind _kind = MeterKind.Power;
        RectTransform _graph;
        RectTransform _circuitRoot;
        Text _legend;
        Text _budget;
        MeterKind _builtKind = (MeterKind)(-1);
        int _builtSeriesCount = -1;
        int _builtCircuitState = -1;

        // Night reserve (GDD 15.5). Separate from the night-5 circuit budget above it: that
        // one is a scripted choice, this is the meter that runs every night of the game.
        Text _reserve;
        Button _lightsButton;
        Text _lightsLabel;

        public FacilityApp() { AppId = AppIds.Facility; TitleKey = "ui.app.facility"; }

        protected override void Build(RectTransform root)
        {
            var tabs = UiFactory.CreateRect("Tabs", root);
            UiFactory.SetAnchoredRect(tabs, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(12f, -44f), new Vector2(-12f, -10f));
            UiFactory.AddHorizontalLayout(tabs, 6f);

            AddTab(tabs, MeterKind.Power, "ui.facility.tab.power");
            AddTab(tabs, MeterKind.Water, "ui.facility.tab.water");
            AddTab(tabs, MeterKind.Heating, "ui.facility.tab.heating");
            AddTab(tabs, MeterKind.Elevator, "ui.facility.tab.elevator");
            AddTab(tabs, MeterKind.Fire, "ui.facility.tab.fire");
            AddTab(tabs, MeterKind.CctvNetwork, "ui.facility.tab.network");

            var panel = UiFactory.CreatePanel("Graph", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(12f, 120f), new Vector2(-12f, -52f));
            _graph = panel.rectTransform;

            _legend = UiFactory.CreateText("Legend", root, string.Empty, 15, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(_legend.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0f),
                                      new Vector2(12f, 12f), new Vector2(-6f, 112f));

            // Night-5 power budget: big obvious switches, plus the running load (GDD 16.12).
            _budget = UiFactory.CreateText("Budget", root, string.Empty, 16, TextAnchor.UpperLeft,
                                           UiFactory.Warning);
            UiFactory.SetAnchoredRect(_budget.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0f),
                                      new Vector2(6f, 88f), new Vector2(-12f, 112f));

            _circuitRoot = UiFactory.CreateRect("Circuits", root);
            UiFactory.SetAnchoredRect(_circuitRoot, new Vector2(0.5f, 0f), new Vector2(1f, 0f),
                                      new Vector2(6f, 12f), new Vector2(-12f, 84f));
            UiFactory.AddHorizontalLayout(_circuitRoot, 4f);

            _reserve = UiFactory.CreateText("Reserve", root, string.Empty, 16, TextAnchor.LowerLeft);
            UiFactory.SetAnchoredRect(_reserve.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0f),
                                      new Vector2(12f, 88f), new Vector2(-6f, 112f));

            _lightsButton = UiFactory.CreateButton("CorridorLights", root, string.Empty, 14, ToggleLights);
            UiFactory.SetAnchoredRect((RectTransform)_lightsButton.transform,
                                      new Vector2(0f, 0f), new Vector2(0f, 0f),
                                      new Vector2(12f, 52f), new Vector2(230f, 82f));
            _lightsLabel = _lightsButton.GetComponentInChildren<Text>();
        }

        static void ToggleLights()
        {
            var power = ServiceHub.Power;
            if (power != null) power.SetCorridorLights(!power.CorridorLightsOn);
        }

        /// <summary>
        /// GDD 15.5. The reserve is the only number in the facility app the player can spend
        /// down by looking at things, so it lives on the power tab next to the load it feeds.
        /// </summary>
        void RefreshReserve()
        {
            var power = ServiceHub.Power;
            bool show = power != null && _kind == MeterKind.Power;

            _reserve.gameObject.SetActive(show);
            _lightsButton.gameObject.SetActive(show);
            if (!show) return;

            string drawPerHour = (power.CurrentDrawPerHour() / 10f).ToString("0.0");
            _reserve.text = Loc.T("ui.facility.reserve", power.ReservePercent, drawPerHour)
                            + System.Environment.NewLine
                            + Loc.T("ui.facility.breaker_left", power.BreakerResetsRemaining);
            _reserve.color = power.IsCritical ? UiFactory.Danger
                           : power.IsLow ? UiFactory.Warning
                           : UiFactory.TextPrimary;

            _lightsLabel.text = Loc.T(power.CorridorLightsOn
                ? "ui.facility.corridor_lights_on"
                : "ui.facility.corridor_lights_off");
            _lightsButton.GetComponent<Image>().color = power.CorridorLightsOn
                ? UiFactory.Ok * 0.7f
                : UiFactory.Panel;
        }

        void RefreshCircuits()
        {
            var facility = ServiceHub.Facility;
            bool hasCircuits = facility.HasCircuit(CircuitIds.Elevator);

            _circuitRoot.gameObject.SetActive(hasCircuits && _kind == MeterKind.Power);
            _budget.gameObject.SetActive(hasCircuits && _kind == MeterKind.Power);
            if (!hasCircuits || _kind != MeterKind.Power) return;

            _budget.text = Loc.T("ui.facility.power_budget",
                                 facility.ActiveCircuitCount, CircuitIds.MaxSimultaneous);

            // Rebuild only when a switch actually changed.
            int state = 0;
            for (int i = 0; i < CircuitIds.All.Length; i++)
                if (facility.IsCircuitOn(CircuitIds.All[i])) state |= 1 << i;

            if (state == _builtCircuitState) return;
            _builtCircuitState = state;

            UiFactory.ClearChildren(_circuitRoot);

            for (int i = 0; i < CircuitIds.All.Length; i++)
            {
                var circuitId = CircuitIds.All[i];
                bool on = facility.IsCircuitOn(circuitId);

                var button = UiFactory.CreateButton("Circuit_" + i, _circuitRoot,
                    Loc.T(circuitId) + "\n" + Loc.T(on ? "ui.facility.circuit_on" : "ui.facility.circuit_off"),
                    13, () => facility.TrySetCircuit(circuitId, !facility.IsCircuitOn(circuitId)));

                button.GetComponent<Image>().color = on ? UiFactory.Ok * 0.7f : UiFactory.Panel;
            }
        }

        void AddTab(RectTransform parent, MeterKind kind, string labelKey)
        {
            UiFactory.CreateButton("Tab_" + kind, parent, Loc.T(labelKey), 15, () =>
            {
                _kind = kind;
                _builtKind = (MeterKind)(-1);
            });
        }

        public override void OnOpen() { _builtKind = (MeterKind)(-1); }

        public override void Refresh()
        {
            RefreshCircuits();
            RefreshReserve();

            var series = ServiceHub.Facility.VisibleSeries(_kind);
            if (_kind == _builtKind && series.Count == _builtSeriesCount) return;

            _builtKind = _kind;
            _builtSeriesCount = series.Count;

            UiFactory.ClearChildren(_graph);

            if (series.Count == 0)
            {
                _legend.text = Loc.T("ui.facility.no_data");
                return;
            }

            // Grouped vertical bars, one group per hour of the shift.
            float max = 0.01f;
            int sampleCount = 0;
            for (int s = 0; s < series.Count; s++)
            {
                if (series[s].Max > max) max = series[s].Max;
                if (series[s].Samples.Count > sampleCount) sampleCount = series[s].Samples.Count;
            }

            var legend = Loc.T("ui.facility.axis", Loc.T(series[0].UnitKey), max.ToString("0.0")) + "\n";

            // The hour scale along the bottom, and a unit number above every group.
            //
            // Without these the chart is anonymous: a playtest report said the utility app
            // "just shows a graph and never says which unit". It did say - in a 15pt legend in
            // the corner - which is not where anyone reading a chart looks. The tab that C01
            // is solved in has to name 804 on the chart itself.
            BuildTimeAxis(sampleCount);
            BuildSeriesHeaders(series);

            for (int s = 0; s < series.Count; s++)
            {
                var single = series[s];
                var color = BarColor(s);

                // GDD 16.12: in colour-blind mode the series also carry a distinct glyph, so
                // hue is never the only thing separating two lines.
                var glyph = ServiceHub.Settings.Current.colorBlindPatterns ? SeriesGlyph(s) + " " : "";
                legend += "  " + glyph + Loc.T(single.LabelKey) + " (" + single.SeriesId + ")\n";

                // Reading a graph is how meter-based evidence is obtained (e.g. the 804 water
                // anomaly in C01). Acquire is idempotent, so re-opening the tab is harmless.
                if (!string.IsNullOrEmpty(single.EvidenceId))
                    Net.NetShift.Request(Net.NetShift.ShiftAct.AcquireEvidence, single.EvidenceId,
                                         null, (int)EvidenceSource.Facility);

                for (int i = 0; i < single.Samples.Count; i++)
                {
                    float normalized = Mathf.Clamp01(single.Samples[i].Value / max);
                    var bar = UiFactory.CreatePanel("Bar_" + single.SeriesId + "_" + i, _graph, color);

                    float groupWidth = 1f / Mathf.Max(1, sampleCount);
                    float barWidth = groupWidth / Mathf.Max(1, series.Count);
                    float x0 = i * groupWidth + s * barWidth;

                    bar.rectTransform.anchorMin = new Vector2(x0 + barWidth * 0.1f, 0f);
                    bar.rectTransform.anchorMax = new Vector2(x0 + barWidth * 0.9f, normalized * 0.92f);
                    bar.rectTransform.offsetMin = new Vector2(0f, 8f);
                    bar.rectTransform.offsetMax = new Vector2(0f, 0f);
                }
            }

            _legend.text = legend;
        }

        /// <summary>
        /// The hour ticks under the chart. A shift is eight hours from 22:00, and every sample
        /// is one of them, so the axis is the sample index turned back into a clock reading.
        /// </summary>
        void BuildTimeAxis(int sampleCount)
        {
            if (sampleCount <= 0) return;

            for (int i = 0; i < sampleCount; i++)
            {
                int hour = (22 + i) % 24;
                var tick = UiFactory.CreateText("Tick_" + i, _graph, hour.ToString("00"), 11,
                                                TextAnchor.LowerCenter, UiFactory.TextMuted);

                float groupWidth = 1f / sampleCount;
                tick.rectTransform.anchorMin = new Vector2(i * groupWidth, 0f);
                tick.rectTransform.anchorMax = new Vector2((i + 1) * groupWidth, 0f);
                tick.rectTransform.offsetMin = new Vector2(0f, -2f);
                tick.rectTransform.offsetMax = new Vector2(0f, 14f);
            }
        }

        /// <summary>
        /// A coloured chip and the unit number for each series, across the top of the chart -
        /// the same colour the bars are drawn in, so the chart names itself.
        /// </summary>
        void BuildSeriesHeaders(IReadOnlyList<Facility.MeterSeries> series)
        {
            float slot = 1f / Mathf.Max(1, series.Count);

            for (int s = 0; s < series.Count; s++)
            {
                var chip = UiFactory.CreatePanel("Chip_" + series[s].SeriesId, _graph, BarColor(s));
                chip.rectTransform.anchorMin = new Vector2(s * slot + 0.012f, 1f);
                chip.rectTransform.anchorMax = new Vector2(s * slot + 0.012f, 1f);
                chip.rectTransform.sizeDelta = new Vector2(10f, 10f);
                chip.rectTransform.pivot = new Vector2(0f, 1f);
                chip.rectTransform.anchoredPosition = new Vector2(0f, -6f);

                var glyph = ServiceHub.Settings.Current.colorBlindPatterns ? SeriesGlyph(s) + " " : "";
                var name = UiFactory.CreateText("Head_" + series[s].SeriesId, _graph,
                                                glyph + Loc.T(series[s].LabelKey), 13,
                                                TextAnchor.UpperLeft);
                name.rectTransform.anchorMin = new Vector2(s * slot, 1f);
                name.rectTransform.anchorMax = new Vector2((s + 1) * slot, 1f);
                name.rectTransform.offsetMin = new Vector2(26f, -22f);
                name.rectTransform.offsetMax = new Vector2(-4f, -4f);
            }
        }

        static string SeriesGlyph(int index)
        {
            switch (index % 6)
            {
                case 0: return "■";
                case 1: return "●";
                case 2: return "▲";
                case 3: return "◆";
                case 4: return "▬";
                default: return "★";
            }
        }

        /// <summary>
        /// Distinct hue plus distinct brightness so the series stay separable in the
        /// colour-blind mode required by GDD 16.12.
        /// </summary>
        static Color BarColor(int index)
        {
            switch (index % 6)
            {
                case 0: return new Color(0.35f, 0.62f, 0.68f);
                case 1: return new Color(0.78f, 0.66f, 0.36f);
                case 2: return new Color(0.55f, 0.55f, 0.60f);
                case 3: return new Color(0.42f, 0.70f, 0.52f);
                case 4: return new Color(0.72f, 0.45f, 0.42f);
                default: return new Color(0.60f, 0.50f, 0.70f);
            }
        }
    }

    // =========================================================================
    // Evidence board and report (GDD 16.13 / 14.3)
    // =========================================================================

    public sealed class EvidenceApp : PcApp
    {
        const float CardWidth = 150f;
        const float CardHeight = 64f;

        readonly Dictionary<string, BoardCard> _cards = new Dictionary<string, BoardCard>();

        RectTransform _unplacedList;
        RectTransform _boardRoot;
        RectTransform _linkRoot;
        RectTransform _relationRoot;
        RectTransform _reportRoot;
        RectTransform _linkPreview;
        Text _detail;
        Canvas _canvas;

        BoardCard _selected;
        BoardCard _pendingFrom;
        BoardCard _pendingTo;

        int _builtEvidenceCount = -1;
        int _builtLinkCount = -1;

        public EvidenceApp() { AppId = AppIds.Evidence; TitleKey = "ui.app.evidence"; }

        protected override void Build(RectTransform root)
        {
            _canvas = root.GetComponentInParent<Canvas>();

            // Left: evidence not yet on the board (GDD 16.13 unsorted column).
            ScrollRect scroll;
            _unplacedList = UiFactory.CreateScrollView("Unplaced", root, out scroll);
            UiFactory.SetAnchoredRect((RectTransform)scroll.transform, new Vector2(0f, 0f), new Vector2(0.24f, 1f),
                                      new Vector2(12f, 12f), new Vector2(-6f, -12f));

            // Centre: the free-placement board.
            var boardPanel = UiFactory.CreatePanel("Board", root, new Color(0.10f, 0.11f, 0.12f, 1f));
            UiFactory.SetAnchoredRect(boardPanel.rectTransform, new Vector2(0.24f, 0f), new Vector2(0.72f, 1f),
                                      new Vector2(6f, 12f), new Vector2(-6f, -12f));
            _boardRoot = boardPanel.rectTransform;

            _linkRoot = UiFactory.CreateRect("Links", _boardRoot);
            UiFactory.Stretch(_linkRoot, 0f, 0f);

            var preview = UiFactory.CreatePanel("LinkPreview", _boardRoot, UiFactory.TextMuted);
            preview.raycastTarget = false;
            _linkPreview = preview.rectTransform;
            _linkPreview.gameObject.SetActive(false);

            // Right: selected card detail, the relation picker and the report.
            var detailPanel = UiFactory.CreatePanel("Detail", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(detailPanel.rectTransform, new Vector2(0.72f, 0.46f), new Vector2(1f, 1f),
                                      new Vector2(6f, 6f), new Vector2(-12f, -12f));
            _detail = UiFactory.CreateText("DetailText", detailPanel.transform, string.Empty, 15, TextAnchor.UpperLeft);
            UiFactory.Stretch(_detail.rectTransform, 12f, 10f);

            _relationRoot = UiFactory.CreateRect("Relations", root);
            UiFactory.SetAnchoredRect(_relationRoot, new Vector2(0.72f, 0.24f), new Vector2(1f, 0.46f),
                                      new Vector2(6f, 4f), new Vector2(-12f, -4f));
            UiFactory.AddVerticalLayout(_relationRoot, 3f);

            _reportRoot = UiFactory.CreateRect("Report", root);
            UiFactory.SetAnchoredRect(_reportRoot, new Vector2(0.72f, 0f), new Vector2(1f, 0.24f),
                                      new Vector2(6f, 12f), new Vector2(-12f, -4f));
            UiFactory.AddVerticalLayout(_reportRoot, 3f);
        }

        public override void OnOpen()
        {
            _builtEvidenceCount = -1;
            _builtLinkCount = -1;
            BuildReportButtons();
            ShowRelationPicker(false);
        }

        public override void Refresh()
        {
            int evidenceCount = ServiceHub.Evidence.Count;
            int linkCount = ServiceHub.Evidence.Links.Count;

            if (evidenceCount != _builtEvidenceCount)
            {
                _builtEvidenceCount = evidenceCount;
                RebuildUnplaced();
                RebuildBoard();
                BuildReportButtons();
            }

            if (linkCount != _builtLinkCount)
            {
                _builtLinkCount = linkCount;
                RebuildLinks();
            }

            UpdateLinkPositions();
        }

        // ---- left column ---------------------------------------------------

        void RebuildUnplaced()
        {
            UiFactory.ClearChildren(_unplacedList);

            foreach (var pair in ServiceHub.Evidence.Owned)
            {
                if (pair.Value.Placed) continue;

                var id = pair.Key;
                var label = Loc.T(pair.Value.Definition.displayNameKey);

                var button = UiFactory.CreateButton("Unplaced_" + id, _unplacedList, label, 14, () => PlaceOnBoard(id));
                UiFactory.SetHeight(button.gameObject, 34f);
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            }

            var hint = UiFactory.CreateText("Hint", _unplacedList, Loc.T("ui.evidence.place_hint"), 13,
                                            TextAnchor.UpperLeft, UiFactory.TextMuted);
            UiFactory.SetHeight(hint.gameObject, 48f);
        }

        void PlaceOnBoard(string evidenceId)
        {
            var runtime = ServiceHub.Evidence.Get(evidenceId);
            if (runtime == null || runtime.Placed) return;

            // Stagger new cards so they never land exactly on top of each other.
            int placed = 0;
            foreach (var pair in ServiceHub.Evidence.Owned) if (pair.Value.Placed) placed++;

            float x = 24f + (placed % 4) * (CardWidth + 18f);
            float y = -24f - (placed / 4) * (CardHeight + 18f);

            ServiceHub.Evidence.SetBoardPosition(evidenceId, x, y);
            _builtEvidenceCount = -1;   // force a rebuild next frame
        }

        // ---- board ----------------------------------------------------------

        void RebuildBoard()
        {
            _cards.Clear();

            for (int i = _boardRoot.childCount - 1; i >= 0; i--)
            {
                var child = _boardRoot.GetChild(i);
                if (child == _linkRoot || child == _linkPreview) continue;
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }

            foreach (var pair in ServiceHub.Evidence.Owned)
            {
                if (!pair.Value.Placed) continue;
                CreateCard(pair.Key, pair.Value);
            }
        }

        void CreateCard(string evidenceId, EvidenceRuntime runtime)
        {
            var frame = UiFactory.CreatePanel("Card_" + evidenceId, _boardRoot, UiFactory.PanelAlt);
            var rect = frame.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);
            rect.anchoredPosition = new Vector2(runtime.BoardX, runtime.BoardY);

            var label = UiFactory.CreateText("Label", frame.transform,
                Loc.T(runtime.Definition.displayNameKey), 13, TextAnchor.UpperLeft);
            label.raycastTarget = false;
            UiFactory.SetAnchoredRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(8f, 14f), new Vector2(-8f, -6f));

            var kind = UiFactory.CreateText("Kind", frame.transform,
                Loc.T("ui.evidence.type." + runtime.Definition.type), 11, TextAnchor.LowerLeft,
                UiFactory.TextMuted);
            kind.raycastTarget = false;
            UiFactory.SetAnchoredRect(kind.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(8f, 4f), new Vector2(-24f, 16f));

            var card = frame.gameObject.AddComponent<BoardCard>();
            card.Bind(evidenceId, _boardRoot, _canvas, frame);
            card.OnSelected = OnCardSelected;
            card.OnMoved = OnCardMoved;

            // The connector in the bottom-right corner starts a link.
            var handle = UiFactory.CreatePanel("Handle", frame.transform, UiFactory.Accent);
            UiFactory.Pin(handle.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                          new Vector2(-6f, 6f), new Vector2(14f, 14f));

            var linkHandle = handle.gameObject.AddComponent<LinkHandle>();
            linkHandle.Bind(card, _linkPreview);
            linkHandle.OnLinkProposed = OnLinkProposed;

            _cards[evidenceId] = card;
        }

        void OnCardMoved(BoardCard card)
        {
            var position = card.Rect.anchoredPosition;
            ServiceHub.Evidence.SetBoardPosition(card.EvidenceId, position.x, position.y);
            UpdateLinkPositions();
        }

        void OnCardSelected(BoardCard card)
        {
            foreach (var pair in _cards) if (pair.Value != null) pair.Value.SetSelected(pair.Value == card);

            _selected = card;
            ShowRelationPicker(false);
            RefreshDetail();
        }

        // ---- links -----------------------------------------------------------

        void OnLinkProposed(BoardCard from, BoardCard to)
        {
            _pendingFrom = from;
            _pendingTo = to;
            ShowRelationPicker(true);
        }

        void ShowRelationPicker(bool visible)
        {
            UiFactory.ClearChildren(_relationRoot);
            if (!visible || _pendingFrom == null || _pendingTo == null) return;

            var header = UiFactory.CreateText("Header", _relationRoot,
                Loc.T("ui.evidence.choose_relation"), 13, TextAnchor.UpperLeft, UiFactory.TextMuted);
            UiFactory.SetHeight(header.gameObject, 20f);

            AddRelationButton(EvidenceRelation.SameTime, "ui.evidence.link.same_time");
            AddRelationButton(EvidenceRelation.SamePerson, "ui.evidence.link.same_person");
            AddRelationButton(EvidenceRelation.LocationContradiction, "ui.evidence.link.location");
            AddRelationButton(EvidenceRelation.Cause, "ui.evidence.link.cause");
            AddRelationButton(EvidenceRelation.TestimonySupport, "ui.evidence.link.testimony");
        }

        void AddRelationButton(EvidenceRelation relation, string labelKey)
        {
            var button = UiFactory.CreateButton("Rel_" + relation, _relationRoot, Loc.T(labelKey), 12, () =>
            {
                if (_pendingFrom == null || _pendingTo == null) return;

                // The board is the shift's board. A link one caretaker draws has to be on all
                // three, and it is saved - so it goes to the host and comes back in the mirror.
                Net.NetShift.Request(Net.NetShift.ShiftAct.LinkEvidence,
                                     _pendingFrom.EvidenceId, _pendingTo.EvidenceId, (int)relation);
                _pendingFrom = null;
                _pendingTo = null;

                ShowRelationPicker(false);
                _builtLinkCount = -1;
                RefreshDetail();
            });

            UiFactory.SetHeight(button.gameObject, 22f);
            button.GetComponent<Image>().color = BoardLinkLayer.ColorFor(relation) * 0.6f;
        }

        void RebuildLinks()
        {
            UiFactory.ClearChildren(_linkRoot);

            var links = ServiceHub.Evidence.Links;
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (!_cards.ContainsKey(link.A) || !_cards.ContainsKey(link.B)) continue;

                var line = UiFactory.CreatePanel("Link_" + i, _linkRoot, BoardLinkLayer.ColorFor(link.Relation));
                line.raycastTarget = false;

                // Colour plus thickness plus a written relation: three separable channels.
                if (!ServiceHub.Settings.Current.colorBlindPatterns) continue;

                var tag = UiFactory.CreateText("Tag", line.transform,
                    Loc.T(BoardLinkLayer.LabelKeyFor(link.Relation)), 11, TextAnchor.MiddleCenter);
                tag.raycastTarget = false;
                UiFactory.SetAnchoredRect(tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                          new Vector2(-60f, -9f), new Vector2(60f, 9f));
            }

            UpdateLinkPositions();
        }

        void UpdateLinkPositions()
        {
            var links = ServiceHub.Evidence.Links;
            int drawn = 0;

            for (int i = 0; i < links.Count && drawn < _linkRoot.childCount; i++)
            {
                var link = links[i];
                BoardCard a, b;
                if (!_cards.TryGetValue(link.A, out a) || !_cards.TryGetValue(link.B, out b)) continue;
                if (a == null || b == null) continue;

                var line = (RectTransform)_linkRoot.GetChild(drawn);
                BoardLinkLayer.Stretch(line, a.Rect, b.Rect, BoardLinkLayer.ThicknessFor(link.Relation));
                drawn++;
            }
        }

        // ---- detail and report ------------------------------------------------

        void RefreshDetail()
        {
            if (_selected == null) { _detail.text = Loc.T("ui.evidence.selection_hint"); return; }

            var runtime = ServiceHub.Evidence.Get(_selected.EvidenceId);
            if (runtime == null) { _detail.text = string.Empty; return; }

            var text = Loc.T(runtime.Definition.displayNameKey) + "\n\n" +
                       Loc.T(runtime.Definition.descriptionKey) + "\n\n";

            var links = ServiceHub.Evidence.Links;
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (link.A != _selected.EvidenceId && link.B != _selected.EvidenceId) continue;

                var other = link.A == _selected.EvidenceId ? link.B : link.A;
                var otherRuntime = ServiceHub.Evidence.Get(other);
                var otherName = otherRuntime != null ? Loc.T(otherRuntime.Definition.displayNameKey) : other;

                // GDD 16.13: a good link reads as "likely related", never as "correct".
                //
                // Spec 0.10.4, from 75: real and planted clues stop being easy to tell apart.
                // The board is where that distinction is actually made, so this is where it
                // goes soft - both kinds of link read the same until the caretaker checks the
                // evidence itself. What the link *is* does not change, and neither does the
                // report built from it: spec 32 forbids moving the answer, only showing it.
                var distortion = ServiceHub.Distortion;
                bool blurred = distortion != null && distortion.CluesAreHardToTellApart;

                string marker = blurred
                    ? "ui.evidence.unverified"
                    : (link.Meaningful ? "ui.evidence.likely_related" : "ui.evidence.recorded");

                text += "  - " + otherName + "  [" + Loc.T(marker) + "]\n";
            }

            _detail.text = text;
        }

        void BuildReportButtons()
        {
            UiFactory.ClearChildren(_reportRoot);

            var tracked = ServiceHub.Cases.TrackedCase;
            if (tracked == null || tracked.State != CaseState.DecisionReady)
            {
                var hint = UiFactory.CreateText("Hint", _reportRoot, Loc.T("ui.report.not_ready"), 13,
                                                TextAnchor.UpperLeft, UiFactory.TextMuted);
                UiFactory.SetHeight(hint.gameObject, 24f);
                return;
            }

            var header = UiFactory.CreateText("Header", _reportRoot,
                Loc.T("ui.report.submit_for", Loc.T(tracked.Definition.titleKey)), 13, TextAnchor.UpperLeft);
            UiFactory.SetHeight(header.gameObject, 22f);

            // Only what can actually be filed. A decision whose conditions are not met is not
            // a choice the caretaker has, and listing it only to refuse it when pressed is the
            // dead button this project keeps removing (v5.1 19.1 Availability).
            var decisions = tracked.Definition.decisions;
            for (int i = 0; i < decisions.Length; i++)
            {
                var decision = decisions[i];
                var caseId = tracked.CaseId;

                string unmet;
                if (!ConditionEvaluator.EvaluateAll(decision.availability, out unmet)) continue;

                var button = UiFactory.CreateButton("Decision_" + decision.decisionId, _reportRoot,
                    Loc.T(decision.labelKey), 13, () => Submit(caseId, decision.decisionId));
                UiFactory.SetHeight(button.gameObject, 26f);
            }
        }

        void Submit(string caseId, string decisionId)
        {
            var attached = new List<string>();
            foreach (var pair in ServiceHub.Evidence.Owned) attached.Add(pair.Key);

            // Whether the filing was accepted, and what the case says back, is the host's
            // answer. It arrives here for whoever pressed the button and nobody else - the
            // other two see the outcome the way the building does, in the mirrored case state.
            Net.NetShift.OnDecisionAnswered = (accepted, key) =>
            {
                _detail.text = Loc.T(key);
                BuildReportButtons();
            };

            Net.NetShift.RequestDecision(caseId, decisionId, attached);
        }
    }

    // =========================================================================
    // Settings (GDD 16.16)
    // =========================================================================

    public sealed class SettingsApp : PcApp
    {
        RectTransform _content;

        public SettingsApp() { AppId = AppIds.Settings; TitleKey = "ui.app.settings"; }

        protected override void Build(RectTransform root)
        {
            ScrollRect scroll;
            _content = UiFactory.CreateScrollView("Options", root, out scroll);
            UiFactory.SetAnchoredRect((RectTransform)scroll.transform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(16f, 16f), new Vector2(-16f, -16f));

            var settings = ServiceHub.Settings.Current;

            // ---- difficulty (GDD 24.2) --------------------------------------
            Header("ui.settings.section.difficulty");
            AddChoice("ui.settings.difficulty",
                      new[] { "ui.difficulty.story", "ui.difficulty.standard", "ui.difficulty.supervisor" },
                      () => settings.difficulty,
                      value => { settings.difficulty = value; ServiceHub.Settings.Save(); });

            // ---- controls and camera ---------------------------------------
            Header("ui.settings.section.controls");
            AddSlider("ui.settings.fov", 60f, 90f, settings.fieldOfView,
                      v => { settings.fieldOfView = v; ServiceHub.Settings.Apply(); });
            AddSlider("ui.settings.sensitivity", 0.1f, 5f, settings.mouseSensitivity,
                      v => settings.mouseSensitivity = v);
            AddSlider("ui.settings.headbob", 0f, 1f, settings.headBobAmount,
                      v => settings.headBobAmount = v);
            AddSlider("ui.settings.camera_shake", 0f, 1f, settings.cameraShake,
                      v => settings.cameraShake = v);
            AddToggle("ui.settings.invert_y", settings.invertY, v => settings.invertY = v);

            // ---- display (GDD 16.16) ----------------------------------------
            // These four were in the settings file from the start and never reached the
            // engine, which is the same as not having them: a player on a 3440x1440 monitor
            // had no way to say so.
            Header("ui.settings.section.display");

            BuildResolutionList();
            AddStepper("ui.settings.resolution", DescribeResolution, StepResolution);

            AddChoice("ui.settings.window_mode",
                      new[] { "ui.window.fullscreen", "ui.window.borderless", "ui.window.windowed" },
                      () => settings.windowMode,
                      value =>
                      {
                          settings.windowMode = value;
                          ServiceHub.Settings.Apply();
                          ServiceHub.Settings.Save();
                      });

            // The engine's own level names ("Mobile", "PC") were being shown here verbatim,
            // which put two untranslated English words in the Korean options screen and named
            // a preset after a platform the game does not ship on. The three levels are
            // authored in QualitySettings in this order (GDD 16.16).
            AddChoice("ui.settings.quality",
                      new[] { "ui.quality.low", "ui.quality.medium", "ui.quality.high" },
                      () => settings.qualityLevel,
                      value =>
                      {
                          settings.qualityLevel = value;
                          ServiceHub.Settings.Apply();
                          ServiceHub.Settings.Save();
                      });

            AddChoice("ui.settings.frame_cap",
                      new[] { "ui.framecap.30", "ui.framecap.60", "ui.framecap.120", "ui.framecap.unlimited" },
                      () => FrameCapIndex(settings.targetFrameRate),
                      value =>
                      {
                          settings.targetFrameRate = GameSettings.FrameRateOptions[value];
                          ServiceHub.Settings.Apply();
                          ServiceHub.Settings.Save();
                      });

            AddToggle("ui.settings.vsync", settings.vsync, v =>
            {
                settings.vsync = v;
                ServiceHub.Settings.Apply();
                ServiceHub.Settings.Save();
            });

            // ---- video and audio -------------------------------------------
            Header("ui.settings.section.video");
            AddSlider("ui.settings.brightness", 0.75f, 1.5f, settings.brightness,
                      v => { settings.brightness = v; ServiceHub.Settings.Apply(); });
            AddSlider("ui.settings.hud_opacity", 0.2f, 1f, settings.hudOpacity,
                      v => settings.hudOpacity = v);
            AddSlider("ui.settings.master_volume", 0f, 1f, settings.masterVolume,
                      v => { settings.masterVolume = v; ServiceHub.Settings.Apply(); });

            // ---- subtitles (GDD 16.17) -------------------------------------
            Header("ui.settings.section.subtitles");
            AddToggle("ui.settings.subtitles", settings.subtitles, v => settings.subtitles = v);
            AddToggle("ui.settings.speaker_names", settings.subtitleSpeakerNames,
                      v => settings.subtitleSpeakerNames = v);
            AddToggle("ui.settings.ambient_subtitles", settings.ambientSubtitles,
                      v => settings.ambientSubtitles = v);
            AddSlider("ui.settings.subtitle_scale", 0.9f, 1.6f, settings.subtitleScale,
                      v => settings.subtitleScale = v);

            // ---- accessibility (GDD 16.16 / 24.3) ---------------------------
            Header("ui.settings.section.accessibility");
            AddChoice("ui.settings.hints",
                      new[] { "ui.hints.off", "ui.hints.delayed", "ui.hints.always" },
                      () => settings.hintMode,
                      value => settings.hintMode = value);
            AddToggle("ui.settings.no_choice_timers", settings.noChoiceTimers,
                      v => settings.noChoiceTimers = v);
            AddToggle("ui.settings.easier_chase", settings.easierChase, v => settings.easierChase = v);
            // GDD 16.16 / 15.4: halves how fast the night closes in and what a confrontation costs.
            AddToggle("ui.settings.calm_nights", settings.calmNights, v => settings.calmNights = v);
            AddToggle("ui.settings.reduce_motion", settings.reduceMotion, v => settings.reduceMotion = v);
            AddToggle("ui.settings.colorblind", settings.colorBlindPatterns,
                      v => settings.colorBlindPatterns = v);
            AddToggle("ui.settings.streamer_mode", settings.streamerMode,
                      v => { settings.streamerMode = v; ServiceHub.Settings.Apply(); });

            // ---- language ---------------------------------------------------
            Header("ui.settings.section.language");

            // Language names are written in their own language on purpose - they are the one
            // kind of label that must not be translated, so they are not string-table keys.
            var languageRow = UiFactory.CreateRect("Language", _content);
            UiFactory.SetHeight(languageRow.gameObject, 34f);
            UiFactory.AddHorizontalLayout(languageRow, 6f);
            UiFactory.CreateButton("Ko", languageRow, "한국어", 15, () => SetLanguage("ko"));
            UiFactory.CreateButton("En", languageRow, "English", 15, () => SetLanguage("en"));

            var save = UiFactory.CreateButton("Save", _content, Loc.T("ui.settings.save"), 16,
                                              () => ServiceHub.Settings.Save());
            UiFactory.SetHeight(save.gameObject, 36f);
        }

        void Header(string labelKey)
        {
            var header = UiFactory.CreateText("H_" + labelKey, _content, Loc.T(labelKey), 17,
                                              TextAnchor.LowerLeft, UiFactory.Accent);
            UiFactory.SetHeight(header.gameObject, 34f);
        }

        static void SetLanguage(string language)
        {
            ServiceHub.Settings.Current.language = language;
            ServiceHub.Localization.SetLanguage(language);
            ServiceHub.Settings.Save();
            ServiceHub.Analytics.Track(AnalyticsService.Events.AccessibilityChanged, "language:" + language);
        }

        // ---- display plumbing ------------------------------------------------

        readonly System.Collections.Generic.List<Vector2Int> _resolutions =
            new System.Collections.Generic.List<Vector2Int>();

        /// <summary>
        /// Every distinct size this monitor offers at or above the GDD 16.2 floor, refresh
        /// rates collapsed - the player picks a size, and the display keeps its own rate.
        /// </summary>
        void BuildResolutionList()
        {
            _resolutions.Clear();

            var seen = new System.Collections.Generic.HashSet<long>();
            var modes = Screen.resolutions;

            for (int i = 0; i < modes.Length; i++)
            {
                int width = modes[i].width;
                int height = modes[i].height;

                if (width < GameSettings.MinimumWidth || height < GameSettings.MinimumHeight) continue;
                if (!seen.Add(((long)width << 32) | (uint)height)) continue;

                _resolutions.Add(new Vector2Int(width, height));
            }

            // A headless or virtual display can report nothing at all; the reference size is
            // a better answer than an empty stepper.
            if (_resolutions.Count == 0)
                _resolutions.Add(new Vector2Int((int)UiFactory.ReferenceWidth, (int)UiFactory.ReferenceHeight));

            _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        int CurrentResolutionIndex()
        {
            var settings = ServiceHub.Settings.Current;

            int width = settings.resolutionWidth > 0 ? settings.resolutionWidth : Screen.width;
            int height = settings.resolutionHeight > 0 ? settings.resolutionHeight : Screen.height;

            for (int i = 0; i < _resolutions.Count; i++)
                if (_resolutions[i].x == width && _resolutions[i].y == height) return i;

            return _resolutions.Count - 1;
        }

        string DescribeResolution()
        {
            var size = _resolutions[CurrentResolutionIndex()];
            return size.x + " x " + size.y;
        }

        void StepResolution(int direction)
        {
            int index = CurrentResolutionIndex() + direction;
            index = Mathf.Clamp(index, 0, _resolutions.Count - 1);

            var settings = ServiceHub.Settings.Current;
            settings.resolutionWidth = _resolutions[index].x;
            settings.resolutionHeight = _resolutions[index].y;

            ServiceHub.Settings.Apply();
            ServiceHub.Settings.Save();
        }

        static int FrameCapIndex(int frameRate)
        {
            var options = GameSettings.FrameRateOptions;
            for (int i = 0; i < options.Length; i++)
                if (options[i] == frameRate) return i;

            return 1;   // 60, the default
        }

        /// <summary>
        /// A value with a previous and a next but no useful slider. Used for the resolution
        /// list, which is as long as the monitor says it is - a row of buttons would run off
        /// the panel on a display that offers thirty modes.
        /// </summary>
        void AddStepper(string labelKey, System.Func<string> describe, System.Action<int> step)
        {
            var row = UiFactory.CreateRect("Stepper_" + labelKey, _content);
            UiFactory.SetHeight(row.gameObject, 52f);

            var label = UiFactory.CreateText("Label", row, Loc.T(labelKey), 16, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(label.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 1f),
                                      Vector2.zero, Vector2.zero);

            var controls = UiFactory.CreateRect("Controls", row);
            UiFactory.SetAnchoredRect(controls, new Vector2(0f, 0f), new Vector2(1f, 0.55f),
                                      new Vector2(0f, 2f), new Vector2(0f, -2f));
            UiFactory.AddHorizontalLayout(controls, 4f);

            Text value = null;

            UiFactory.CreateButton("Prev", controls, "<", 14, () =>
            {
                step(-1);
                if (value != null) value.text = describe();
            });

            value = UiFactory.CreateText("Value", controls, describe(), 15, TextAnchor.MiddleCenter);

            UiFactory.CreateButton("Next", controls, ">", 14, () =>
            {
                step(1);
                if (value != null) value.text = describe();
            });
        }

        /// <summary>
        /// AddChoice for options whose labels are not translatable - quality preset names come
        /// from the engine, and resolutions are numbers.
        /// </summary>
        /// <summary>A row of mutually exclusive buttons, for options that are not a spectrum.</summary>
        void AddChoice(string labelKey, string[] optionKeys, System.Func<int> read, System.Action<int> write)
        {
            var labels = new string[optionKeys.Length];
            for (int i = 0; i < optionKeys.Length; i++) labels[i] = Loc.T(optionKeys[i]);

            BuildChoiceRow(labelKey, labels, read, write);
        }

        void BuildChoiceRow(string labelKey, string[] optionLabels, System.Func<int> read, System.Action<int> write)
        {
            var row = UiFactory.CreateRect("Choice_" + labelKey, _content);
            UiFactory.SetHeight(row.gameObject, 52f);

            var label = UiFactory.CreateText("Label", row, Loc.T(labelKey), 16, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(label.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 1f),
                                      Vector2.zero, Vector2.zero);

            var buttons = UiFactory.CreateRect("Buttons", row);
            UiFactory.SetAnchoredRect(buttons, new Vector2(0f, 0f), new Vector2(1f, 0.55f),
                                      new Vector2(0f, 2f), new Vector2(0f, -2f));
            UiFactory.AddHorizontalLayout(buttons, 4f);

            var images = new Image[optionLabels.Length];
            for (int i = 0; i < optionLabels.Length; i++)
            {
                int value = i;
                var button = UiFactory.CreateButton("Opt" + i, buttons, optionLabels[i], 14, () =>
                {
                    write(value);
                    ServiceHub.Analytics.Track(AnalyticsService.Events.AccessibilityChanged,
                                               labelKey + ":" + value);
                    for (int j = 0; j < images.Length; j++)
                        images[j].color = j == value ? UiFactory.Accent * 0.6f : UiFactory.PanelAlt;
                });

                images[i] = button.GetComponent<Image>();
            }

            int current = read();
            for (int i = 0; i < images.Length; i++)
                images[i].color = i == current ? UiFactory.Accent * 0.6f : UiFactory.PanelAlt;
        }

        void AddSlider(string labelKey, float min, float max, float value, System.Action<float> onChanged)
        {
            var row = UiFactory.CreateRect("Row_" + labelKey, _content);
            UiFactory.SetHeight(row.gameObject, 42f);

            var label = UiFactory.CreateText("Label", row, Loc.T(labelKey), 16, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f),
                                      Vector2.zero, Vector2.zero);

            var slider = UiFactory.CreateSlider("Slider", row, min, max, value, onChanged);
            UiFactory.SetAnchoredRect(slider.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0.5f),
                                      new Vector2(0f, 4f), new Vector2(0f, -4f));
        }

        void AddToggle(string labelKey, bool value, System.Action<bool> onChanged)
        {
            var toggle = UiFactory.CreateToggle("Toggle_" + labelKey, _content, Loc.T(labelKey), value, v =>
            {
                onChanged(v);
                ServiceHub.Analytics.Track(AnalyticsService.Events.AccessibilityChanged, labelKey + ":" + v);
            });
            UiFactory.SetHeight(toggle.gameObject, 30f);
        }
    }
}
