using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NO404.Core;
using NO404.Cases;

namespace NO404.UI
{
    /// <summary>
    /// Task tablet (GDD 16.14). Right-hand 42% slide panel, opened with Tab, game time at
    /// 0.5x. Never covers the centre of the screen.
    /// </summary>
    public sealed class TabletView : MonoBehaviour
    {
        CanvasGroup _group;
        RectTransform _panel;
        Text _body;
        bool _open;

        public bool IsOpen { get { return _open; } }

        public static TabletView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("Tablet", parent, 150);
            var view = canvas.gameObject.AddComponent<TabletView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            var panel = UiFactory.CreatePanel("Panel", root, UiFactory.Panel);
            _panel = panel.rectTransform;
            UiFactory.SetAnchoredRect(_panel, new Vector2(0.58f, 0f), new Vector2(1f, 1f),
                                      new Vector2(0f, 0f), new Vector2(0f, 0f));

            _body = UiFactory.CreateText("Body", panel.transform, string.Empty, 17, TextAnchor.UpperLeft);
            UiFactory.Stretch(_body.rectTransform, 24f, 24f);

            gameObject.SetActive(false);
        }

        public void Toggle()
        {
            _open = !_open;
            gameObject.SetActive(true);
            _group.blocksRaycasts = _open;
            if (_open) Rebuild();
        }

        public void Close()
        {
            _open = false;
            _group.blocksRaycasts = false;
        }

        void Rebuild()
        {
            var text = Loc.T("ui.tablet.title") + "\n\n" + Loc.T("ui.tablet.section.tasks") + "\n";

            foreach (var runtime in ServiceHub.Cases.AllCases)
            {
                if (!runtime.State.IsActive()) continue;
                var objective = runtime.CurrentVisibleObjective();
                text += "  [" + runtime.CaseId + "] " + Loc.T(runtime.Definition.titleKey) + "\n";
                if (objective != null) text += "      " + Loc.T(objective.titleKey) + "\n";
            }

            // Spec 0.10.4, from 50: a card for work nobody filed. Drawn exactly like the
            // others on purpose - the tablet is not allowed to know which one it is, and
            // neither is the caretaker until they check it (spec 0.4).
            var distortion = ServiceHub.Distortion;
            if (distortion != null)
            {
                var ghost = distortion.FalseTaskCardFor(ServiceHub.State.NightIndex);
                if (ghost.Exists)
                {
                    text += "  [" + ghost.Id + "] " + Loc.T(ghost.TitleKey) + "\n";
                    text += "      " + Loc.T(ghost.ObjectiveKey) + "\n";
                }
            }

            text += "\n" + Loc.T("ui.tablet.section.access") + "\n";
            foreach (var level in ServiceHub.State.GrantedAccess)
                text += "  " + Loc.T("ui.access_level." + level) + "\n";

            text += "\n" + Loc.T("ui.tablet.section.evidence") + "\n";
            text += "  " + Loc.T("ui.hud.evidence_count", ServiceHub.Evidence.Count) + "\n";

            text += "\n" + Loc.T("ui.tablet.section.map") + "\n";
            text += "  " + Loc.T("ui.zone." + ServiceHub.Player.CurrentZone) + "\n";

            text += "\n" + BuildManual();
            text += "\n" + BuildAnomalyLog();

            _body.text = text;
        }

        /// <summary>
        /// 야간 특이상황 대응 지침 (v2.1 spec 0.9.3).
        ///
        /// Reached through the tablet rather than given its own key, because the spec puts it
        /// behind Tab and because a caretaker in the middle of something should not have to
        /// close what they are reading to check what they are supposed to be doing.
        ///
        /// The page for whatever is happening right now is printed in full; everything else is
        /// a title. That ordering is the whole design of this panel - spec 0.9.3 lays a page
        /// out as 관찰 / 금지 / 조치 / 비고 precisely so a player can find the prohibition
        /// while something is walking towards them, and burying it under seventeen other pages
        /// would undo that.
        /// </summary>
        static string BuildManual()
        {
            var manual = ServiceHub.Manual;
            if (manual == null) return string.Empty;

            var text = Loc.T("ui.manual.title") + "\n";
            text += "  " + Loc.T("ui.manual.count", manual.UnlockedCount) + "\n";

            if (manual.UnlockedCount == 0)
                return text + "  " + Loc.T("ui.manual.empty") + "\n";

            var open = OpenPage(manual);
            if (open != null) text += "\n" + RenderPage(open);

            foreach (var page in manual.UnlockedPages)
            {
                if (open != null && page.pageId == open.pageId) continue;
                text += "  [" + page.eventId + "] " + Loc.T(page.titleKey) + "\n";
            }

            return text;
        }

        /// <summary>The page for the anomaly the caretaker is currently inside, if any.</summary>
        static Manual.ManualPage OpenPage(Manual.ManualService manual)
        {
            var events = ServiceHub.ManualEvents;
            if (events == null) return null;

            var active = events.ActiveEvents;
            for (int i = 0; i < active.Count; i++)
            {
                var page = manual.Find(active[i].Definition.manualPageId);
                if (page != null && manual.IsUnlocked(page.pageId)) return page;
            }
            return null;
        }

        static string RenderPage(Manual.ManualPage page)
        {
            var text = "  [" + page.eventId + "] " + Loc.T(page.titleKey) + "\n";
            text += Section(page.observationKeys, "ui.manual.section.observe", page, false);
            text += Section(page.prohibitionKeys, "ui.manual.section.forbid", page, true);
            text += Numbered(page.stepKeys, "ui.manual.section.act");
            text += Section(page.noteKeys, "ui.manual.section.note", page, false);
            return text + "\n";
        }

        static string Section(string[] keys, string headerKey, Manual.ManualPage page, bool markLethal)
        {
            if (keys == null || keys.Length == 0) return string.Empty;

            var text = "    " + Loc.T(headerKey) + "\n";
            for (int i = 0; i < keys.Length; i++)
            {
                // The lethal marker is the one thing on a page that must not be skimmed past
                // (spec 0.10.5), so it is printed on the line rather than as a page-level flag.
                string lethal = markLethal && page.IsLethal(i) ? "  <" + Loc.T("ui.manual.lethal") + ">" : string.Empty;
                text += "      - " + Loc.T(keys[i]) + lethal + "\n";
            }
            return text;
        }

        static string Numbered(string[] keys, string headerKey)
        {
            if (keys == null || keys.Length == 0) return string.Empty;

            var text = "    " + Loc.T(headerKey) + "\n";
            for (int i = 0; i < keys.Length; i++)
                text += "      " + (i + 1) + ". " + Loc.T(keys[i]) + "\n";
            return text;
        }

        /// <summary>
        /// The anomaly log (GDD 3.2 / 12.3).
        ///
        /// Thirty-six numbered slots. A type the caretaker has correctly filed shows what it
        /// was; a type they have not is a number and nothing else. That gap is the point - it
        /// is the only place this game tells the player there is more to find, and the reason
        /// to keep watching a wall of cameras on a night when nothing is being asked of them.
        ///
        /// Filed entries are listed first, so the log reads as something being built rather
        /// than as a column of blanks with a few hits in it.
        /// </summary>
        static string BuildAnomalyLog()
        {
            var cctv = ServiceHub.Cctv;
            int total = CCTV.AnomalyCatalogue.TypeCount;
            int found = cctv != null ? cctv.CataloguedCount : 0;

            var text = Loc.T("ui.tablet.section.anomalies") + "\n";
            text += "  " + Loc.T("ui.tablet.anomalies_count", found, total) + "\n";

            if (cctv == null) return text;

            for (int type = 1; type <= total; type++)
            {
                if (!cctv.IsCatalogued(type)) continue;
                text += "  " + type.ToString("00") + "  " +
                        Loc.T(CCTV.AnomalyCatalogue.DescriptionKey(type)) + "\n";
            }

            var blanks = string.Empty;
            for (int type = 1; type <= total; type++)
                if (!cctv.IsCatalogued(type)) blanks += type.ToString("00") + " ";

            if (blanks.Length > 0)
                text += "  " + Loc.T("ui.tablet.anomalies_unknown") + "\n  " + blanks.TrimEnd() + "\n";

            return text;
        }

        void Update()
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, _open ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);
            if (!_open && _group.alpha <= 0f) gameObject.SetActive(false);
        }
    }

    /// <summary>Face-to-face / phone conversation overlay. Interphone dialogue lives in the PC app.</summary>
    public sealed class DialogueView : MonoBehaviour
    {
        Text _speaker;
        Text _body;
        RectTransform _choices;
        Image _timerFill;
        string _builtForNode;
        float _timeLeft;

        public static DialogueView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("Dialogue", parent, 250);
            var view = canvas.gameObject.AddComponent<DialogueView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var panel = UiFactory.CreatePanel("Panel", root, new Color(0.05f, 0.05f, 0.06f, 0.92f));
            UiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f),
                                      new Vector2(0f, 60f), new Vector2(0f, 300f));

            _speaker = UiFactory.CreateText("Speaker", panel.transform, string.Empty, 18, TextAnchor.UpperLeft,
                                            UiFactory.Accent);
            UiFactory.SetAnchoredRect(_speaker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(24f, -40f), new Vector2(-24f, -12f));

            _body = UiFactory.CreateText("Body", panel.transform, string.Empty, 20, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(_body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(24f, 120f), new Vector2(-24f, -44f));

            _choices = UiFactory.CreateRect("Choices", panel.transform);
            UiFactory.SetAnchoredRect(_choices, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(24f, 16f), new Vector2(-24f, 112f));
            UiFactory.AddVerticalLayout(_choices, 4f);

            var timerBg = UiFactory.CreatePanel("Timer", panel.transform, new Color(0f, 0f, 0f, 0.6f));
            UiFactory.Pin(timerBg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                          new Vector2(-24f, -14f), new Vector2(160f, 6f));
            _timerFill = UiFactory.CreatePanel("TimerFill", timerBg.transform, UiFactory.Warning);
            _timerFill.rectTransform.anchorMin = Vector2.zero;
            _timerFill.rectTransform.anchorMax = Vector2.one;
            _timerFill.rectTransform.offsetMin = Vector2.zero;
            _timerFill.rectTransform.offsetMax = Vector2.zero;

            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            var dialogue = ServiceHub.Dialogue;
            bool show = dialogue.IsActive && dialogue.Current != null &&
                        dialogue.Current.channel != Dialogue.DialogueChannel.Interphone;

            if (!show)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                _builtForNode = null;
                return;
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            var line = dialogue.CurrentLine;
            if (line == null) return;

            _speaker.text = Loc.T(line.SpeakerKey);
            _body.text = Loc.T(line.TextKey);

            if (_builtForNode != line.NodeId)
            {
                _builtForNode = line.NodeId;
                _timeLeft = line.TimeLimit;
                RebuildChoices(line);
            }

            if (line.TimeLimit > 0f && line.HasChoices && !DifficultyProfile.ChoiceTimersDisabled)
            {
                _timerFill.transform.parent.gameObject.SetActive(true);
                _timeLeft -= Time.unscaledDeltaTime;
                _timerFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_timeLeft / line.TimeLimit), 1f);
                // GDD 16.9: a timeout becomes "hold", never an automatic refusal.
                if (_timeLeft <= 0f) dialogue.Timeout();
            }
            else
            {
                _timerFill.transform.parent.gameObject.SetActive(false);
            }
        }

        void RebuildChoices(Dialogue.DialogueLine line)
        {
            UiFactory.ClearChildren(_choices);

            if (line.HasChoices)
            {
                for (int i = 0; i < line.Choices.Count; i++)
                {
                    var choice = line.Choices[i];
                    var button = UiFactory.CreateButton("Choice_" + choice.choiceId, _choices,
                        Loc.T(choice.textKey), 17,
                        () => NO404.Net.NetShift.RequestDialogueChoice(choice.choiceId));
                    UiFactory.SetHeight(button.gameObject, 30f);
                }
            }
            else
            {
                var button = UiFactory.CreateButton("Continue", _choices, Loc.T("ui.dialogue.continue"), 17,
                                                    () => NO404.Net.NetShift.Request(
                                                        NO404.Net.NetShift.ShiftAct.AdvanceDialogue));
                UiFactory.SetHeight(button.gameObject, 30f);
            }
        }
    }

    /// <summary>Pause menu (GDD 16.16). Freezes game time completely.</summary>
    public sealed class PauseView : MonoBehaviour
    {
        public System.Action OnResume;
        public System.Action OnQuitToMenu;

        Text _status;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        public static PauseView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("Pause", parent, 300);
            var view = canvas.gameObject.AddComponent<PauseView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var dim = UiFactory.CreatePanel("Dim", root, new Color(0f, 0f, 0f, 0.75f));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f);

            var panel = UiFactory.CreatePanel("Panel", root, UiFactory.Panel);
            UiFactory.Pin(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          Vector2.zero, new Vector2(480f, 460f));
            UiFactory.AddVerticalLayout(panel.rectTransform, 8f, new RectOffset(24, 24, 24, 24));

            var title = UiFactory.CreateText("Title", panel.transform, Loc.T("ui.pause.title"), 26,
                                             TextAnchor.MiddleCenter);
            UiFactory.SetHeight(title.gameObject, 40f);

            UiFactory.SetHeight(UiFactory.CreateButton("Resume", panel.transform, Loc.T("ui.pause.resume"), 18,
                () => { var cb = OnResume; if (cb != null) cb(); }).gameObject, 44f);

            UiFactory.SetHeight(UiFactory.CreateButton("Save", panel.transform, Loc.T("ui.pause.save"), 18,
                () => ServiceHub.Save.SaveAsync(Save.SaveReason.Manual, System.Threading.CancellationToken.None)).gameObject, 44f);

            UiFactory.SetHeight(UiFactory.CreateButton("Load", panel.transform, Loc.T("ui.pause.load"), 18,
                () => ServiceHub.Save.LoadAsync(System.Threading.CancellationToken.None)).gameObject, 44f);

            UiFactory.SetHeight(UiFactory.CreateButton("Menu", panel.transform, Loc.T("ui.pause.quit_to_menu"), 18,
                () => { var cb = OnQuitToMenu; if (cb != null) cb(); }).gameObject, 44f);

            _status = UiFactory.CreateText("Status", panel.transform, string.Empty, 15, TextAnchor.MiddleCenter,
                                           UiFactory.TextMuted);
            UiFactory.SetHeight(_status.gameObject, 60f);

            gameObject.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            gameObject.SetActive(open);
            if (!open) return;

            _status.text = Loc.T("ui.pause.status",
                                 ServiceHub.State.NightIndex,
                                 ServiceHub.Clock.ToClockString(),
                                 ServiceHub.Evidence.Count);
        }
    }

    /// <summary>Developer console (GDD 20.21). Editor and development builds only.</summary>
    public sealed class DevConsoleView : MonoBehaviour
    {
        InputField _input;
        Text _output;
        readonly List<string> _lines = new List<string>();

        public bool IsOpen { get { return gameObject.activeSelf; } }

        public static DevConsoleView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("DevConsole", parent, 400);
            var view = canvas.gameObject.AddComponent<DevConsoleView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var panel = UiFactory.CreatePanel("Panel", root, new Color(0f, 0f, 0f, 0.9f));
            UiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(0f, -420f), new Vector2(0f, 0f));

            _output = UiFactory.CreateText("Output", panel.transform, string.Empty, 15, TextAnchor.LowerLeft);
            UiFactory.SetAnchoredRect(_output.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(12f, 40f), new Vector2(-12f, -8f));

            _input = UiFactory.CreateInputField("Input", panel.transform, "> ", 16);
            UiFactory.SetAnchoredRect(_input.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(12f, 6f), new Vector2(-12f, 34f));
            _input.onEndEdit.AddListener(Execute);

            gameObject.SetActive(false);
        }

        public void Toggle()
        {
            bool open = !gameObject.activeSelf;
            gameObject.SetActive(open);
            if (!open) return;

            _input.text = string.Empty;
            _input.ActivateInputField();
        }

        void Execute(string command)
        {
            if (string.IsNullOrEmpty(command)) return;

            _lines.Add("> " + command);
            _lines.Add(DevConsole.Execute(command));
            while (_lines.Count > 22) _lines.RemoveAt(0);

            _output.text = string.Join("\n", _lines.ToArray());
            _input.text = string.Empty;
            _input.ActivateInputField();
        }
    }
}
