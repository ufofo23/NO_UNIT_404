using UnityEngine;
using UnityEngine.UI;
using NO404.Cases;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>
    /// In-world HUD (GDD 16.5): clock top-left, notification icons top-right, one tracked
    /// objective bottom-left, interaction prompt bottom-centre, flashlight/keys bottom-right.
    /// Idle fade after five seconds; opacity from settings.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        const float IdleFadeDelay = 5f;

        CanvasGroup _group;
        Text _clock;
        /// <summary>HP and SAN, the two the campaign is actually scored on (v5.0 8).</summary>
        Text _vitals;
        Text _objective;
        Text _prompt;
        Text _notifications;
        Text _status;
        Image _holdBar;
        RectTransform _holdBarRoot;
        Image _awarenessBar;
        RectTransform _awarenessRoot;
        Text _hint;
        Text _caption;

        float _lastActivity;
        float _notificationTimer;

        public static HudView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("HUD", parent, 100);
            var view = canvas.gameObject.AddComponent<HudView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _clock = UiFactory.CreateText("Clock", root, "22:00", 26, TextAnchor.UpperLeft);
            UiFactory.Pin(_clock.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(32f, -24f), new Vector2(420f, 34f));

            // Under the clock, because they are read the same way: a glance, often, without
            // stopping. v5.0 8 makes these two the ending, so hiding them in a menu would
            // mean the player learns what they cost only once it is too late to spend them
            // differently.
            _vitals = UiFactory.CreateText("Vitals", root, string.Empty, 19, TextAnchor.UpperLeft);
            UiFactory.Pin(_vitals.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(32f, -60f), new Vector2(420f, 26f));

            _notifications = UiFactory.CreateText("Notifications", root, string.Empty, 20, TextAnchor.UpperRight);
            UiFactory.Pin(_notifications.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                          new Vector2(-32f, -24f), new Vector2(620f, 34f));

            _objective = UiFactory.CreateText("Objective", root, string.Empty, 20, TextAnchor.LowerLeft);
            UiFactory.Pin(_objective.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(32f, 32f), new Vector2(760f, 30f));

            _status = UiFactory.CreateText("Status", root, string.Empty, 18, TextAnchor.LowerRight);
            UiFactory.Pin(_status.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                          new Vector2(-32f, 32f), new Vector2(520f, 26f));

            _prompt = UiFactory.CreateText("Prompt", root, string.Empty, 22, TextAnchor.LowerCenter);
            UiFactory.Pin(_prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                          new Vector2(0f, 220f), new Vector2(900f, 30f));

            var holdRoot = UiFactory.CreatePanel("HoldBarBg", root, new Color(0f, 0f, 0f, 0.5f));
            _holdBarRoot = holdRoot.rectTransform;
            UiFactory.Pin(_holdBarRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                          new Vector2(0f, 200f), new Vector2(220f, 6f));

            var fill = UiFactory.CreatePanel("HoldBarFill", holdRoot.transform, UiFactory.Accent);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = new Vector2(0f, 0f);
            _holdBar = fill;

            // Awareness readout for the night-5 threat window. Hidden the rest of the game,
            // because GDD 15.2 says ordinary patrols must never feel like a stealth section.
            var awarenessBg = UiFactory.CreatePanel("AwarenessBg", root, new Color(0f, 0f, 0f, 0.55f));
            _awarenessRoot = awarenessBg.rectTransform;
            UiFactory.Pin(_awarenessRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                          new Vector2(0f, -60f), new Vector2(260f, 8f));

            var awarenessFill = UiFactory.CreatePanel("AwarenessFill", awarenessBg.transform, UiFactory.Warning);
            awarenessFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            awarenessFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            awarenessFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            awarenessFill.rectTransform.offsetMin = Vector2.zero;
            awarenessFill.rectTransform.offsetMax = Vector2.zero;
            _awarenessBar = awarenessFill;

            // Hint ladder (GDD 24.3), just under the tracked objective.
            _hint = UiFactory.CreateText("Hint", root, string.Empty, 18, TextAnchor.LowerLeft,
                                         UiFactory.Warning);
            UiFactory.Pin(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(32f, 66f), new Vector2(900f, 26f));

            // Subtitles: bottom centre, at most two lines (GDD 16.17).
            _caption = UiFactory.CreateText("Caption", root, string.Empty, 24, TextAnchor.LowerCenter);
            UiFactory.Pin(_caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                          new Vector2(0f, 140f), new Vector2(1200f, 68f));

            _awarenessRoot.gameObject.SetActive(false);
            _holdBarRoot.gameObject.SetActive(false);
            _lastActivity = Time.unscaledTime;
        }

        void RefreshHintAndCaption()
        {
            var hints = ServiceHub.Hints;
            _hint.text = hints != null ? hints.CurrentHint : string.Empty;

            var captions = ServiceHub.Captions;
            var line = captions != null ? captions.Current : string.Empty;

            _caption.text = line;
            _caption.color = captions != null && captions.CurrentIsAmbient
                ? UiFactory.TextMuted
                : UiFactory.TextPrimary;

            // Subtitle size is an accessibility option, so it is applied every frame rather
            // than baked in at build time (GDD 16.16: 90-160%).
            int size = Mathf.RoundToInt(24f * ServiceHub.Settings.Current.subtitleScale);
            if (_caption.fontSize != size) _caption.fontSize = size;
        }

        void RefreshAwareness()
        {
            var threat = ServiceHub.Threat;
            bool show = threat != null && threat.IsActive;

            if (_awarenessRoot.gameObject.activeSelf != show) _awarenessRoot.gameObject.SetActive(show);
            if (!show) return;

            // During the fire the same bar counts down instead of counting up.
            if (threat.Kind == Threat.ThreatKind.Fire)
            {
                float remaining = threat.FireProgress01;
                _awarenessBar.rectTransform.anchorMax = new Vector2(remaining, 1f);
                _awarenessBar.color = remaining < 0.3f ? UiFactory.Danger : UiFactory.Warning;
                return;
            }

            float awareness = threat.Awareness;
            _awarenessBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(awareness), 1f);
            _awarenessBar.color = threat.IsHidden ? UiFactory.Ok
                                : awareness > 0.7f ? UiFactory.Danger
                                : UiFactory.Warning;
        }

        void OnEnable()
        {
            EventBus.Subscribe<NotificationEvent>(OnNotification);
            EventBus.Subscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
            EventBus.Subscribe<CaseStartedEvent>(OnCaseStarted);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<NotificationEvent>(OnNotification);
            EventBus.Unsubscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
            EventBus.Unsubscribe<CaseStartedEvent>(OnCaseStarted);
        }

        void OnNotification(NotificationEvent evt)
        {
            _notifications.text = Loc.T(evt.BodyKey);
            _notifications.color = evt.Severity == NotificationSeverity.Urgent ? UiFactory.Danger
                                 : evt.Severity == NotificationSeverity.Warning ? UiFactory.Warning
                                 : UiFactory.TextPrimary;
            _notificationTimer = 2.5f;   // GDD 16.5: expand 2.5s then shrink
            Poke();
        }

        void OnObjectiveChanged(ObjectiveChangedEvent evt) { Poke(); }
        void OnCaseStarted(CaseStartedEvent evt) { Poke(); }

        /// <summary>Any player input restores full opacity.</summary>
        public void Poke() { _lastActivity = Time.unscaledTime; }

        /// <summary>
        /// Draws HP and SAN, and colours them by what they are about to cost.
        ///
        /// The colour changes at the ending gate rather than at some prettier round number:
        /// thirty is the line v5.0 8.1 refuses to let anybody past, so thirty is the line the
        /// HUD warns at. Amber is the last band in which the run is still recoverable.
        /// </summary>
        void RefreshVitals()
        {
            var vitals = ServiceHub.Vitals;
            if (_vitals == null || vitals == null) return;

            _vitals.text = Loc.T("ui.hud.vitals", vitals.Hp, vitals.San);

            int worst = vitals.Hp < vitals.San ? vitals.Hp : vitals.San;
            _vitals.color = worst < VitalService.EndingGateMinimum ? UiFactory.Danger
                          : worst < 50 ? UiFactory.Warning
                          : UiFactory.TextMuted;
        }

        public void Refresh(Interaction.InteractionRaycaster raycaster, bool anyInput)
        {
            if (anyInput) Poke();

            var clock = ServiceHub.Clock;
            var reading = clock.ToClockString() + "  " + Loc.T("ui.hud.weekday.monday");

            // Spec 0.10.4, from 25: one frame of a deformed glyph on the office monitor.
            // Applied at the point of drawing rather than stored, so the next frame is right
            // again and nothing ever reads the clock back out of this label.
            var distortion = ServiceHub.Distortion;
            _clock.text = distortion != null ? distortion.Corrupt(reading) : reading;

            RefreshVitals();

            var tracked = ServiceHub.Cases.TrackedCase;
            var objective = tracked != null ? tracked.CurrentVisibleObjective() : null;
            _objective.text = objective != null
                ? Loc.T("ui.hud.objective_prefix") + " " + Loc.T(objective.titleKey)
                : string.Empty;

            int unreviewed = ServiceHub.Cctv.UnreviewedMotionCount();
            if (_notificationTimer > 0f) _notificationTimer -= Time.unscaledDeltaTime;
            else if (unreviewed > 0) _notifications.text = Loc.T("ui.notify.unreviewed_motion", unreviewed);
            else _notifications.text = string.Empty;

            _status.text = Loc.T("ui.hud.evidence_count", ServiceHub.Evidence.Count);

            if (raycaster != null && raycaster.HasTarget)
            {
                var promptData = raycaster.CurrentPrompt;
                var label = promptData.Enabled
                    ? Loc.T("ui.prompt.format", Loc.T("ui.prompt.key_interact"), Loc.T(promptData.LabelKey))
                    : Loc.T(promptData.DisabledReasonKey);

                _prompt.text = label;
                _prompt.color = promptData.Enabled ? UiFactory.TextPrimary : UiFactory.TextMuted;

                bool holding = promptData.Kind == Interaction.InteractionKind.Hold && raycaster.HoldProgress > 0f;
                _holdBarRoot.gameObject.SetActive(holding);
                if (holding)
                    _holdBar.rectTransform.anchorMax = new Vector2(raycaster.HoldProgress, 1f);
            }
            else
            {
                _prompt.text = string.Empty;
                _holdBarRoot.gameObject.SetActive(false);
            }

            RefreshAwareness();
            RefreshHintAndCaption();
            ApplyOpacity();
        }

        void ApplyOpacity()
        {
            float baseOpacity = ServiceHub.Settings.Current.hudOpacity;
            float idle = Time.unscaledTime - _lastActivity;
            float target = idle > IdleFadeDelay ? baseOpacity * 0.45f : baseOpacity;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * 1.5f);
        }

        public void SetVisible(bool visible) { gameObject.SetActive(visible); }
    }
}
