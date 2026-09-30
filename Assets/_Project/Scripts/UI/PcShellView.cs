using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NO404.Cases;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>One application inside the facility OS.</summary>
    public abstract class PcApp
    {
        public string AppId { get; protected set; }
        public string TitleKey { get; protected set; }
        public RectTransform Root { get; private set; }

        protected PcShellView Shell;

        public void Initialize(PcShellView shell, RectTransform parent)
        {
            Shell = shell;
            Root = UiFactory.CreateRect("App_" + AppId, parent);
            UiFactory.Stretch(Root, 0f, 0f);
            Build(Root);
            Root.gameObject.SetActive(false);
        }

        protected abstract void Build(RectTransform root);
        public virtual void OnOpen() { }
        public virtual void OnClose() { }
        public virtual void Refresh() { }

        public void SetActive(bool active)
        {
            if (Root == null) return;
            Root.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// HAESOL FACILITY OS shell (GDD 16.6). Dimensions are the GDD's: 48px top bar,
    /// 84px left nav, 300px right status panel, 30px log strip. App switching is a 120ms
    /// fade with no sliding, and the chrome never moves when the game "lies" - only the
    /// data inside the apps does (GDD 16.1).
    /// </summary>
    public sealed class PcShellView : MonoBehaviour
    {
        const float TopBar = 48f;
        const float LeftNav = 84f;
        const float RightPanel = 300f;
        const float LogBar = 30f;

        readonly List<PcApp> _apps = new List<PcApp>();
        readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();

        CanvasGroup _group;
        Text _topBar;
        Text _statusText;
        Text _logText;
        RectTransform _content;
        PcApp _active;

        public CCTV.CctvRig Rig { get; private set; }
        public string ActiveAppId { get { return _active != null ? _active.AppId : string.Empty; } }
        public bool IsOpen { get; private set; }

        public static PcShellView Create(Transform parent, CCTV.CctvRig rig)
        {
            var canvas = UiFactory.CreateCanvas("FacilityOS", parent, 200);
            var view = canvas.gameObject.AddComponent<PcShellView>();
            view.Rig = rig;
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            _group = root.gameObject.AddComponent<CanvasGroup>();

            var background = UiFactory.CreatePanel("Background", root, UiFactory.Background);
            UiFactory.Stretch(background.rectTransform, 0f, 0f);

            // Top bar
            var top = UiFactory.CreatePanel("TopBar", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(top.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(0f, -TopBar), new Vector2(0f, 0f));
            _topBar = UiFactory.CreateText("TopBarText", top.transform, string.Empty, 18, TextAnchor.MiddleLeft);
            UiFactory.Stretch(_topBar.rectTransform, 16f, 0f);

            // Left nav
            var nav = UiFactory.CreatePanel("Nav", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(nav.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                      new Vector2(0f, LogBar), new Vector2(LeftNav, -TopBar));
            UiFactory.AddVerticalLayout(nav.rectTransform, 2f, new RectOffset(4, 4, 8, 8));

            // Right status panel
            var status = UiFactory.CreatePanel("Status", root, UiFactory.Panel);
            UiFactory.SetAnchoredRect(status.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                                      new Vector2(-RightPanel, LogBar), new Vector2(0f, -TopBar));
            _statusText = UiFactory.CreateText("StatusText", status.transform, string.Empty, 17, TextAnchor.UpperLeft);
            UiFactory.Stretch(_statusText.rectTransform, 14f, 12f);

            // Bottom log strip
            var log = UiFactory.CreatePanel("Log", root, UiFactory.PanelAlt);
            UiFactory.SetAnchoredRect(log.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(0f, 0f), new Vector2(0f, LogBar));
            _logText = UiFactory.CreateText("LogText", log.transform, string.Empty, 15, TextAnchor.MiddleLeft,
                                            UiFactory.TextMuted);
            UiFactory.Stretch(_logText.rectTransform, 12f, 0f);

            // Content area
            _content = UiFactory.CreateRect("Content", root);
            UiFactory.SetAnchoredRect(_content, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(LeftNav, LogBar), new Vector2(-RightPanel, -TopBar));

            RegisterApps();
            BuildNav(nav.rectTransform);

            _group.alpha = 0f;
            gameObject.SetActive(false);
        }

        void RegisterApps()
        {
            _apps.Add(new HomeApp());
            _apps.Add(new CctvApp());
            _apps.Add(new PhoneApp());
            _apps.Add(new ResidentsApp());
            _apps.Add(new AccessApp());
            _apps.Add(new FacilityApp());
            _apps.Add(new EvidenceApp());
            _apps.Add(new SettingsApp());

            for (int i = 0; i < _apps.Count; i++) _apps[i].Initialize(this, _content);
        }

        void BuildNav(RectTransform nav)
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                var app = _apps[i];
                var button = UiFactory.CreateButton("Nav_" + app.AppId, nav, Loc.T(app.TitleKey), 15,
                                                    () => Open(app.AppId));
                UiFactory.SetHeight(button.gameObject, 52f);
                _navButtons[app.AppId] = button;
            }
        }

        public void Open(string appId)
        {
            PcApp target = null;
            for (int i = 0; i < _apps.Count; i++) if (_apps[i].AppId == appId) { target = _apps[i]; break; }
            if (target == null || target == _active) return;

            if (_active != null)
            {
                _active.OnClose();
                _active.SetActive(false);
            }

            _active = target;
            _active.SetActive(true);
            _active.OnOpen();

            foreach (var pair in _navButtons)
            {
                var image = pair.Value.GetComponent<Image>();
                image.color = pair.Key == appId ? UiFactory.Accent * 0.6f : UiFactory.PanelAlt;
            }

            // Which app is on this screen is local; having opened it is the shift's, because
            // a case objective and a caller's release both hang off it (v3.0 46.1).
            ServiceHub.Player.OpenApp(appId);
            Net.NetShift.Request(Net.NetShift.ShiftAct.OpenApp, appId);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            gameObject.SetActive(open);

            if (open)
            {
                if (_active == null) Open(AppIds.Home);
                else { _active.OnOpen(); ServiceHub.Player.OpenApp(_active.AppId); }
            }
            else if (_active != null)
            {
                _active.OnClose();
            }
        }

        void Update()
        {
            // GDD 16.6: 120ms fade between states, no sliding panels.
            _group.alpha = Mathf.MoveTowards(_group.alpha, IsOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);

            if (!IsOpen) return;

            var clock = ServiceHub.Clock;
            _topBar.text = Loc.T("ui.pc.topbar",
                                 ServiceHub.State.NightIndex.ToString("00"),
                                 clock.ToClockString());

            // Tonight's roster rather than every case filed under tonight: the pool draws
            // four or five out of fifteen (v5.0 4.1), so counting the pool told the caretaker
            // they had eleven jobs left that were never going to be handed to them.
            int done = 0, total = 0;
            foreach (var runtime in ServiceHub.Cases.RosterTonight)
            {
                total++;
                if (runtime.State.IsResolved()) done++;
            }

            // What the shift is actually waiting on, which is not only the task list.
            //
            // The counter above has always been a count of cases, and the door has never been
            // a case: night 1 has callers booked until 01:35, so a caretaker could close every
            // task, read "6 / 6", and find the clock-off button refusing with nothing on the
            // screen to explain it. The button did say why, and it says it a few centimetres
            // under a number that reads as finished - which is not the same as being able to
            // see what is left.
            var loop = GameLoop.Instance;
            int due = loop != null ? loop.PendingVisitorCount : 0;
            if (ServiceHub.Interphone.HasWaitingVisitor) due++;

            string callers = due > 0 ? Loc.T("ui.pc.status.callers", due) + "\n\n" : string.Empty;

            _statusText.text =
                Loc.T("ui.pc.status.tasks", done, total) + "\n\n" +
                callers +
                Loc.T("ui.pc.status.alerts", ServiceHub.Cctv.UnreviewedMotionCount()) + "\n\n" +
                Loc.T("ui.stat.trust") + "  " + ServiceHub.State.GetStat(StatIds.CommunityTrust) + "\n" +
                Loc.T("ui.stat.safety") + "  " + ServiceHub.State.GetStat(StatIds.BuildingSafety) + "\n" +
                Loc.T("ui.stat.performance") + "  " + ServiceHub.State.GetStat(StatIds.Performance);

            _logText.text = Log.LastLine;

            if (_active != null) _active.Refresh();
        }
    }
}
