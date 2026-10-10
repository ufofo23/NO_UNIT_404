using UnityEngine;
using UnityEngine.UI;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>Main menu (GDD 16.15). Placeholder styling; the layout matches the shipped item order.</summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        public System.Action OnContinue;
        public System.Action OnNewGame;
        public System.Action OnEndless;
        public System.Action OnDevStart;
        public System.Action OnGallery;
        public System.Action OnCredits;
        public System.Action OnQuit;

        Button _continueButton;
        Button _newGameButton;
        Button _endlessButton;
        Button _devStartButton;
        Button _galleryButton;
        Text _title;
        Text _footer;

        InputField _address;
        Text _sessionStatus;
        Text _codeLabel;
        Text _codeValue;
        Text _joinLabel;
        Button _hostButton;
        Button _joinButton;
        Button _leaveButton;
        Button _copyButton;
        float _copied;

        /// <summary>
        /// Opening or joining a shift (v3.0 34).
        ///
        /// On the main menu and above New Game on purpose: a session has to exist before a
        /// night starts, because the host is the machine that will be running it and everybody
        /// needs to be in before the clock is.
        ///
        /// Nothing here knows what Relay is. It asks MultiplayerSessionService for a room and
        /// a code; when Steam invites replace that, this panel does not change.
        /// </summary>
        void BuildCoopPanel(RectTransform root)
        {
            var panel = UiFactory.CreatePanel("Coop", root, UiFactory.Panel);
            UiFactory.Pin(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                          new Vector2(-120f, -30f), new Vector2(440f, 430f));

            var header = UiFactory.CreateText("CoopTitle", panel.transform, Loc.T("ui.net.title"), 22,
                                               TextAnchor.UpperLeft);
            UiFactory.Pin(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -18f), new Vector2(400f, 30f));

            _sessionStatus = UiFactory.CreateText("CoopStatus", panel.transform, string.Empty, 15,
                                                   TextAnchor.UpperLeft, UiFactory.TextMuted);
            UiFactory.Pin(_sessionStatus.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -54f), new Vector2(404f, 44f));

            _hostButton = UiFactory.CreateButton("Host", panel.transform, Loc.T("ui.net.host"), 18, Host);
            UiFactory.Pin(_hostButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -104f), new Vector2(404f, 44f));

            // The code, big enough to read out over voice chat without leaning in.
            _codeLabel = UiFactory.CreateText("CodeLabel", panel.transform, string.Empty, 15,
                                               TextAnchor.UpperLeft, UiFactory.TextMuted);
            UiFactory.Pin(_codeLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -156f), new Vector2(404f, 22f));

            _codeValue = UiFactory.CreateText("CodeValue", panel.transform, string.Empty, 34,
                                               TextAnchor.MiddleLeft, UiFactory.Accent);
            UiFactory.Pin(_codeValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -200f), new Vector2(260f, 42f));

            _copyButton = UiFactory.CreateButton("CopyCode", panel.transform, Loc.T("ui.net.copy"), 15,
                                                  CopyCode);
            UiFactory.Pin(_copyButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(284f, -200f), new Vector2(138f, 42f));

            var divider = UiFactory.CreatePanel("Divider", panel.transform, UiFactory.Line);
            UiFactory.Pin(divider.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -256f), new Vector2(404f, 1f));

            _joinLabel = UiFactory.CreateText("JoinLabel", panel.transform, Loc.T("ui.net.code_prompt"), 15,
                                               TextAnchor.UpperLeft, UiFactory.TextMuted);
            UiFactory.Pin(_joinLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -284f), new Vector2(404f, 22f));

            _address = UiFactory.CreateInputField("JoinCode", panel.transform,
                                                   Loc.T("ui.net.code_placeholder"), 20);
            UiFactory.Pin(_address.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -330f), new Vector2(404f, 44f));
            _address.characterLimit = 12;

            _joinButton = UiFactory.CreateButton("Join", panel.transform, Loc.T("ui.net.join"), 18, Join);
            UiFactory.Pin(_joinButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -382f), new Vector2(404f, 44f));

            _leaveButton = UiFactory.CreateButton("Leave", panel.transform, Loc.T("ui.net.leave"), 18, Leave);
            UiFactory.Pin(_leaveButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(18f, -382f), new Vector2(404f, 44f));

            RefreshSession();
        }

        static async void Host()
        {
            await Net.MultiplayerSessionService.HostAsync();
        }

        async void Join()
        {
            var code = _address != null ? _address.text : null;
            await Net.MultiplayerSessionService.JoinAsync(code);
        }

        static async void Leave()
        {
            await Net.MultiplayerSessionService.LeaveAsync();
        }

        void CopyCode()
        {
            var code = Net.MultiplayerSessionService.JoinCode;
            if (string.IsNullOrEmpty(code)) return;

            GUIUtility.systemCopyBuffer = code;
            _copied = Time.unscaledTime + 1.5f;
        }

        void Update() { RefreshSession(); }

        /// <summary>
        /// Redraws the panel from the session service.
        ///
        /// Polled rather than event-driven because the session's own events arrive on
        /// background threads and Unity UI is not allowed to hear about them there. The panel
        /// is six labels; a poll is cheaper than the marshalling would be.
        /// </summary>
        void RefreshSession()
        {
            if (_sessionStatus == null) return;

            var state = Net.MultiplayerSessionService.State;
            bool inSession = Net.MultiplayerSessionService.InSession;
            bool working = state == Net.MultiplayerSessionService.Status.Working;
            bool hosting = state == Net.MultiplayerSessionService.Status.Hosting;

            _sessionStatus.text = StatusLine(state, inSession, working);
            _sessionStatus.color = state == Net.MultiplayerSessionService.Status.Failed
                ? UiFactory.Danger
                : UiFactory.TextMuted;

            // The code half only means anything to whoever opened the room.
            var code = Net.MultiplayerSessionService.JoinCode;
            bool showCode = hosting && !string.IsNullOrEmpty(code);

            _codeLabel.gameObject.SetActive(showCode);
            _codeValue.gameObject.SetActive(showCode);
            _copyButton.gameObject.SetActive(showCode);

            if (showCode)
            {
                _codeLabel.text = Loc.T("ui.net.code_label");
                _codeValue.text = code;

                var copyText = _copyButton.GetComponentInChildren<Text>();
                if (copyText != null)
                    copyText.text = Time.unscaledTime < _copied
                        ? Loc.T("ui.net.copied")
                        : Loc.T("ui.net.copy");
            }

            bool showJoin = !inSession && !working;
            _joinLabel.gameObject.SetActive(showJoin);
            _address.gameObject.SetActive(showJoin);
            _joinButton.gameObject.SetActive(showJoin);

            _hostButton.gameObject.SetActive(showJoin);
            _leaveButton.gameObject.SetActive(inSession);

            _hostButton.interactable = !working;
            _joinButton.interactable = !working;

            // BuildCoopPanel ends by calling this, and it runs before the campaign buttons
            // below it are created - so everything above this line has to stand on its own
            // and everything below it has to tolerate not existing yet.
            //
            // Reaching past this point during the build threw, Build() unwound, and the menu
            // came up with no 새 게임 / 이어하기 / 끝없는 교대 on it at all. The buttons were
            // not hidden; they were never made.
            if (_continueButton == null) return;

            // Somebody who has walked into another caretaker's room does not open the
            // building; they wait for it to open. Pressing these would start a second, private
            // night on top of the one they joined - which the host's next mirror would then
            // overwrite, one service at a time, in front of them.
            bool guest = inSession && !hosting;
            _continueButton.interactable = !guest && ServiceHub.Save.HasSave;
            _newGameButton.interactable = !guest;
            _endlessButton.interactable = !guest;
            if (_devStartButton != null) _devStartButton.interactable = !guest;
        }

        static string StatusLine(Net.MultiplayerSessionService.Status state, bool inSession, bool working)
        {
            if (working) return Loc.T("ui.net.status.working");

            if (state == Net.MultiplayerSessionService.Status.Failed)
            {
                var key = Net.MultiplayerSessionService.LastErrorKey;
                return Loc.T(string.IsNullOrEmpty(key) ? "ui.net.error.join_failed" : key);
            }

            if (!inSession) return Loc.T("ui.net.status.solo");

            return Loc.T("ui.net.status.count",
                         Net.MultiplayerSessionService.PlayerCount,
                         Net.MultiplayerSessionService.Capacity);
        }

        public static MainMenuView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("MainMenu", parent, 350);
            var view = canvas.gameObject.AddComponent<MainMenuView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var background = UiFactory.CreatePanel("Background", root, UiFactory.Background);
            UiFactory.Stretch(background.rectTransform, 0f, 0f);

            _title = UiFactory.CreateText("Title", root, Loc.T("ui.menu.title"), 54, TextAnchor.MiddleLeft);
            UiFactory.Pin(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(120f, -160f), new Vector2(1000f, 70f));

            var subtitle = UiFactory.CreateText("Subtitle", root, Loc.T("ui.menu.subtitle"), 22,
                                                TextAnchor.MiddleLeft, UiFactory.TextMuted);
            UiFactory.Pin(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(122f, -220f), new Vector2(1000f, 34f));

            var menu = UiFactory.CreateRect("Menu", root);
            UiFactory.Pin(menu, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                          new Vector2(120f, -60f), new Vector2(420f, DevConsole.Enabled ? 372f : 316f));
            UiFactory.AddVerticalLayout(menu, 8f);

            BuildCoopPanel(root);

            _continueButton = UiFactory.CreateButton("Continue", menu, Loc.T("ui.menu.continue"), 20,
                                                     () => { var cb = OnContinue; if (cb != null) cb(); });
            UiFactory.SetHeight(_continueButton.gameObject, 48f);

            _newGameButton = UiFactory.CreateButton("NewGame", menu, Loc.T("ui.menu.new_game"), 20,
                () => { var cb = OnNewGame; if (cb != null) cb(); });
            UiFactory.SetHeight(_newGameButton.gameObject, 48f);

            // Endless shift: the same building with none of the story, running until the
            // caretaker is dismissed. It costs almost nothing because the traffic and the
            // anomalies are already generated rather than authored.
            _endlessButton = UiFactory.CreateButton("Endless", menu, Loc.T("ui.menu.endless"), 20,
                () => { var cb = OnEndless; if (cb != null) cb(); });
            UiFactory.SetHeight(_endlessButton.gameObject, 48f);

            // The developer start (GDD 20.21). Not built at all outside the editor and
            // development builds, rather than built and hidden: a button that does not exist
            // cannot be reached by a release binary however the menu is driven.
            if (DevConsole.Enabled)
            {
                _devStartButton = UiFactory.CreateButton("DevStart", menu, Loc.T("ui.menu.dev_start"), 20,
                    () => { var cb = OnDevStart; if (cb != null) cb(); });
                UiFactory.SetHeight(_devStartButton.gameObject, 48f);
            }

            _galleryButton = UiFactory.CreateButton("Gallery", menu, Loc.T("ui.menu.gallery"), 20,
                () => { var cb = OnGallery; if (cb != null) cb(); });
            UiFactory.SetHeight(_galleryButton.gameObject, 48f);

            // Credits sit above Quit rather than below it: the licence notices they carry
            // have to be reachable, and nobody looks past the last button on a menu.
            UiFactory.SetHeight(UiFactory.CreateButton("Credits", menu, Loc.T("ui.menu.credits"), 20,
                () => { var cb = OnCredits; if (cb != null) cb(); }).gameObject, 48f);

            UiFactory.SetHeight(UiFactory.CreateButton("Quit", menu, Loc.T("ui.menu.quit"), 20,
                () => { var cb = OnQuit; if (cb != null) cb(); }).gameObject, 48f);

            _footer = UiFactory.CreateText("Footer", root, string.Empty, 15, TextAnchor.LowerRight,
                                           UiFactory.TextMuted);
            UiFactory.Pin(_footer.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                          new Vector2(-40f, 32f), new Vector2(900f, 24f));
        }

        public void SetOpen(bool open)
        {
            gameObject.SetActive(open);
            if (!open) return;

            _continueButton.interactable = ServiceHub.Save.HasSave;
            _galleryButton.interactable = ServiceHub.Endings.UnlockedCount > 0;
            _footer.text = Loc.T("ui.menu.footer", Application.version);

            // GDD 10.3: after the "erased" ending the title itself loses its first characters.
            var full = Loc.T("ui.menu.title");
            _title.text = ServiceHub.Endings.LastAftermath == Endings.EndingAftermath.CorruptTitle && full.Length > 2
                ? full.Substring(2)
                : full;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
