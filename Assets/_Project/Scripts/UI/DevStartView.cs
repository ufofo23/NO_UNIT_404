using UnityEngine;
using UnityEngine.UI;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>
    /// The developer start screen: pick a night, pick what the shift is stripped down to, go.
    ///
    /// Reached from a main-menu button that is only built where <see cref="DevConsole.Enabled"/>
    /// is true, so a release binary has no way in. Everything it offers already existed as a
    /// console command; what it adds is that nobody has to remember the order to type them in.
    ///
    /// The choices are kept on the view rather than reset each time it opens: a tester going
    /// round the same night for the tenth time wants the same four settings, not the defaults.
    /// </summary>
    public sealed class DevStartView : MonoBehaviour
    {
        public System.Action<DevStartOptions> OnStart;
        public System.Action OnClose;

        readonly DevStartOptions _options = new DevStartOptions();

        Image[] _nightButtons;
        Image[] _priorButtons;

        public static DevStartView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("DevStart", parent, 360);
            var view = canvas.gameObject.AddComponent<DevStartView>();
            view.Build((RectTransform)canvas.transform);
            view.gameObject.SetActive(false);
            return view;
        }

        void Build(RectTransform root)
        {
            var background = UiFactory.CreatePanel("Background", root, UiFactory.Background);
            UiFactory.Stretch(background.rectTransform, 0f, 0f);

            var title = UiFactory.CreateText("Title", root, Loc.T("ui.dev.title"), 34,
                                             TextAnchor.MiddleLeft);
            Place(title.rectTransform, -110f, 1200f, 46f);

            var hint = UiFactory.CreateText("Hint", root, Loc.T("ui.dev.hint"), 16,
                                            TextAnchor.MiddleLeft, UiFactory.TextMuted);
            Place(hint.rectTransform, -160f, 1200f, 26f);

            // ---- which night ----
            Label(root, "NightLabel", "ui.dev.night_label", -220f);

            _nightButtons = new Image[GameLoop.FinalNight];
            for (int i = 0; i < _nightButtons.Length; i++)
            {
                int night = i + 1;
                var button = UiFactory.CreateButton("Night" + night, root,
                                                    Loc.T("ui.dev.night_button", night), 18,
                                                    () => { _options.Night = night; Refresh(); });
                UiFactory.Pin(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                              new Vector2(120f + i * 128f, -256f), new Vector2(120f, 48f));
                _nightButtons[i] = button.GetComponent<Image>();
            }

            // ---- what the earlier nights left behind ----
            Label(root, "PriorLabel", "ui.dev.prior_label", -340f);

            _priorButtons = new[]
            {
                PriorButton(root, DevPriorNights.Untouched, "ui.dev.prior.untouched", 0),
                PriorButton(root, DevPriorNights.HandledWell, "ui.dev.prior.well", 1),
                PriorButton(root, DevPriorNights.HandledBadly, "ui.dev.prior.badly", 2)
            };

            // ---- switches ----
            var mainOnly = UiFactory.CreateToggle("MainOnly", root, Loc.T("ui.dev.main_only"),
                                                  _options.MainOnly, v => _options.MainOnly = v);
            Place((RectTransform)mainOnly.transform, -470f, 1100f, 32f);

            var allowSaves = UiFactory.CreateToggle("AllowSaves", root, Loc.T("ui.dev.allow_saves"),
                                                    _options.AllowSaves, v => _options.AllowSaves = v);
            Place((RectTransform)allowSaves.transform, -516f, 1100f, 32f);

            var close = UiFactory.CreateButton("Close", root, Loc.T("ui.common.back"), 18,
                                               () => { var cb = OnClose; if (cb != null) cb(); });
            UiFactory.Pin(close.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(120f, 48f), new Vector2(200f, 44f));

            var start = UiFactory.CreateButton("Start", root, Loc.T("ui.dev.start"), 18, Submit);
            UiFactory.Pin(start.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(336f, 48f), new Vector2(260f, 44f));

            Refresh();
        }

        static void Label(RectTransform root, string name, string key, float y)
        {
            var text = UiFactory.CreateText(name, root, Loc.T(key), 18, TextAnchor.MiddleLeft,
                                            UiFactory.TextMuted);
            Place(text.rectTransform, y, 1200f, 28f);
        }

        /// <summary>Pins a row to the left margin every other menu screen uses.</summary>
        static void Place(RectTransform rect, float y, float width, float height)
        {
            UiFactory.Pin(rect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(120f, y), new Vector2(width, height));
        }

        Image PriorButton(RectTransform root, DevPriorNights prior, string key, int column)
        {
            var button = UiFactory.CreateButton("Prior" + prior, root, Loc.T(key), 18,
                                                () => { _options.PriorNights = prior; Refresh(); });
            UiFactory.Pin(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(120f + column * 256f, -376f), new Vector2(248f, 48f));
            return button.GetComponent<Image>();
        }

        void Submit()
        {
            // A copy, so a shift that is already running cannot be re-aimed by somebody
            // coming back to this screen and clicking a different night.
            var cb = OnStart;
            if (cb == null) return;

            cb(new DevStartOptions
            {
                Night = _options.Night,
                MainOnly = _options.MainOnly,
                AllowSaves = _options.AllowSaves,
                PriorNights = _options.PriorNights
            });
        }

        /// <summary>Marks the chosen night and the chosen history.</summary>
        void Refresh()
        {
            for (int i = 0; i < _nightButtons.Length; i++)
                _nightButtons[i].color = i + 1 == _options.Night ? UiFactory.Accent : UiFactory.PanelAlt;

            for (int i = 0; i < _priorButtons.Length; i++)
                _priorButtons[i].color = i == (int)_options.PriorNights ? UiFactory.Accent : UiFactory.PanelAlt;
        }

        /// <summary>
        /// Points the screen at a night. Used when it is opened mid-shift, so it starts on
        /// the night the tester is standing in rather than on whatever was picked last.
        /// </summary>
        public void SelectNight(int night)
        {
            _options.Night = Mathf.Clamp(night, 1, GameLoop.FinalNight);
            Refresh();
        }

        public void SetOpen(bool open)
        {
            gameObject.SetActive(open);
            if (!open) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
