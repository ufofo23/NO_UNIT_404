using UnityEngine;
using UnityEngine.UI;
using NO404.Core;
using NO404.Pressure;

namespace NO404.UI
{
    /// <summary>
    /// The night's counter-pressure, on screen (GDD 16.19).
    ///
    /// Deliberately not a bar with a number on it. GDD 16.5 keeps hidden variables hidden, and
    /// a percentage would turn "something is in the corridor" into a resource to optimise. The
    /// player gets what the caretaker would get: the room darkens at the edges, the office door
    /// gets named in words once it is being knocked on, and the reserve - which is a real gauge
    /// on a real panel - is the only figure shown.
    ///
    /// Sits under the HUD canvas so prompts and captions always read on top of it.
    /// </summary>
    public sealed class PressureView : MonoBehaviour
    {
        const float VignetteMaxAlpha = 0.42f;
        const float PulseHz = 0.9f;

        static readonly Color VignetteColor = new Color(0.05f, 0.02f, 0.03f, 1f);

        Image _vignette;
        Text _doorLine;
        RectTransform _reserveRoot;
        Image _reserveFill;
        Text _reserveLabel;

        float _shown;

        public static PressureView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("PressureHud", parent, 99);
            var view = canvas.gameObject.AddComponent<PressureView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            _vignette = UiFactory.CreatePanel("Vignette", root, VignetteColor);
            UiFactory.Stretch(_vignette.rectTransform, 0f, 0f);
            _vignette.color = new Color(VignetteColor.r, VignetteColor.g, VignetteColor.b, 0f);

            // Named in words, not measured. "Someone is knocking" is information the caretaker
            // has; "pressure 62" is information only the designer has.
            _doorLine = UiFactory.CreateText("DoorLine", root, string.Empty, 20, TextAnchor.UpperCenter,
                                             UiFactory.Warning);
            UiFactory.Pin(_doorLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                          new Vector2(0f, -96f), new Vector2(900f, 28f));

            var reserveBg = UiFactory.CreatePanel("ReserveBg", root, new Color(0f, 0f, 0f, 0.55f));
            _reserveRoot = reserveBg.rectTransform;
            UiFactory.Pin(_reserveRoot, new Vector2(1f, 0f), new Vector2(1f, 0f),
                          new Vector2(-32f, 64f), new Vector2(220f, 8f));

            _reserveFill = UiFactory.CreatePanel("ReserveFill", reserveBg.transform, UiFactory.Ok);
            _reserveFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            _reserveFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            _reserveFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _reserveFill.rectTransform.offsetMin = Vector2.zero;
            _reserveFill.rectTransform.offsetMax = Vector2.zero;

            _reserveLabel = UiFactory.CreateText("ReserveLabel", root, string.Empty, 15, TextAnchor.LowerRight,
                                                 UiFactory.TextMuted);
            UiFactory.Pin(_reserveLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                          new Vector2(-32f, 76f), new Vector2(320f, 22f));
        }

        void OnEnable() { EventBus.Subscribe<OfficeDoorChangedEvent>(OnDoorChanged); }
        void OnDisable() { EventBus.Unsubscribe<OfficeDoorChangedEvent>(OnDoorChanged); }

        Interaction.OfficeDoorState _doorState;
        bool _doorLocked;

        void OnDoorChanged(OfficeDoorChangedEvent evt)
        {
            _doorState = evt.State;
            _doorLocked = evt.Locked;
        }

        public void Refresh()
        {
            RefreshVignette();
            RefreshDoorLine();
            RefreshReserve();
        }

        void RefreshVignette()
        {
            var pressure = ServiceHub.Pressure;
            float target = pressure == null ? 0f : pressure.Value01;

            // Squared so the first half of the night stays clean and the last quarter does not.
            float alpha = VignetteMaxAlpha * target * target;

            if (!ServiceHub.Settings.Current.reduceMotion && target > 0.5f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseHz * Mathf.PI * 2f);
                alpha *= Mathf.Lerp(0.75f, 1f, pulse);
            }

            _shown = Mathf.MoveTowards(_shown, alpha, Time.unscaledDeltaTime * 0.5f);
            _vignette.color = new Color(VignetteColor.r, VignetteColor.g, VignetteColor.b, _shown);
        }

        void RefreshDoorLine()
        {
            string key = _doorState == Interaction.OfficeDoorState.Knocking ? "ui.hud.door_knocking"
                       : _doorState == Interaction.OfficeDoorState.HandleTurning ? "ui.hud.door_handle"
                       : _doorLocked ? "ui.hud.door_bolted"
                       : null;

            _doorLine.text = key == null ? string.Empty : Loc.T(key);
            _doorLine.color = _doorState == Interaction.OfficeDoorState.HandleTurning
                ? UiFactory.Danger
                : UiFactory.Warning;
        }

        void RefreshReserve()
        {
            var power = ServiceHub.Power;
            if (power == null) { _reserveRoot.gameObject.SetActive(false); return; }

            if (!_reserveRoot.gameObject.activeSelf) _reserveRoot.gameObject.SetActive(true);

            _reserveFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(power.Reserve01), 1f);
            _reserveFill.color = power.IsCritical ? UiFactory.Danger
                               : power.IsLow ? UiFactory.Warning
                               : UiFactory.Ok;

            _reserveLabel.text = Loc.T("ui.hud.reserve", power.ReservePercent);
            _reserveLabel.color = power.IsLow ? UiFactory.Warning : UiFactory.TextMuted;
        }

        public void SetVisible(bool visible) { gameObject.SetActive(visible); }
    }
}
