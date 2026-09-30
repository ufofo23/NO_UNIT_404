using UnityEngine;
using UnityEngine.UI;

namespace NO404.UI
{
    /// <summary>
    /// The photograph on a resident record.
    ///
    /// It exists for GDD 2.3 marketing hook #7: the person in the file slowly turns their head
    /// until they are looking at the player. The database had no photographs at all, so the
    /// scene was unbuildable - and a still portrait is worth having on its own, because a
    /// record with a face on it is a person and a record without one is a row.
    ///
    /// Greybox art: a head, a jaw and two eyes built from uGUI rectangles. Turning is done by
    /// sliding the eyes and features across the face rather than rotating anything, which reads
    /// correctly at this fidelity and will be replaced wholesale by the art pass.
    /// </summary>
    public sealed class ResidentPortrait : MonoBehaviour
    {
        /// <summary>How long the head takes to come round. Slow enough to doubt yourself.</summary>
        public const float TurnSeconds = 6f;

        /// <summary>How far off-centre the features sit while the subject is in profile.</summary>
        const float ProfileOffset = 0.26f;

        RectTransform _features;
        Image _leftEye;
        Image _rightEye;

        float _turn;          // 0 = profile, 1 = facing the player
        bool _turning;

        public static ResidentPortrait Create(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                              Vector2 offsetMin, Vector2 offsetMax)
        {
            var frame = UiFactory.CreatePanel("Portrait", parent, new Color(0.11f, 0.12f, 0.13f, 1f));
            UiFactory.SetAnchoredRect(frame.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);

            var portrait = frame.gameObject.AddComponent<ResidentPortrait>();
            portrait.Build(frame.rectTransform);
            return portrait;
        }

        void Build(RectTransform root)
        {
            // Shoulders, then the head sitting on them.
            var shoulders = UiFactory.CreatePanel("Shoulders", root, new Color(0.20f, 0.21f, 0.23f, 1f));
            UiFactory.SetAnchoredRect(shoulders.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                      new Vector2(-52f, 0f), new Vector2(52f, 34f));

            var head = UiFactory.CreatePanel("Head", root, new Color(0.30f, 0.29f, 0.28f, 1f));
            UiFactory.SetAnchoredRect(head.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                      new Vector2(-34f, 30f), new Vector2(34f, 116f));

            // Everything that betrays which way the subject is facing lives under here.
            _features = UiFactory.CreateRect("Features", head.transform);
            UiFactory.Stretch(_features, 0f, 0f);

            _leftEye = UiFactory.CreatePanel("EyeL", _features, new Color(0.07f, 0.07f, 0.08f, 1f));
            UiFactory.Pin(_leftEye.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f),
                          new Vector2(-15f, 0f), new Vector2(9f, 5f));

            _rightEye = UiFactory.CreatePanel("EyeR", _features, new Color(0.07f, 0.07f, 0.08f, 1f));
            UiFactory.Pin(_rightEye.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f),
                          new Vector2(15f, 0f), new Vector2(9f, 5f));

            ApplyTurn();
        }

        /// <summary>Snaps back to a filed, three-quarter-away photograph.</summary>
        public void ResetToProfile()
        {
            _turning = false;
            _turn = 0f;
            ApplyTurn();
        }

        /// <summary>Starts the head coming round. Nothing announces it; it simply happens.</summary>
        public void LookAtViewer()
        {
            if (_turn >= 1f) return;
            _turning = true;
        }

        void Update()
        {
            if (!_turning || _turn >= 1f) return;

            _turn = Mathf.MoveTowards(_turn, 1f, Time.unscaledDeltaTime / TurnSeconds);
            ApplyTurn();
        }

        /// <summary>
        /// In profile the features are pushed to one side and the far eye is hidden. Coming
        /// round brings them to centre and the second eye back into view.
        /// </summary>
        void ApplyTurn()
        {
            if (_features == null) return;

            float offset = Mathf.Lerp(ProfileOffset, 0f, _turn);
            _features.anchorMin = new Vector2(offset, 0f);
            _features.anchorMax = new Vector2(1f + offset, 1f);
            _features.offsetMin = Vector2.zero;
            _features.offsetMax = Vector2.zero;

            // The far eye fades in over the second half of the turn.
            if (_leftEye != null)
            {
                var colour = _leftEye.color;
                colour.a = Mathf.Clamp01((_turn - 0.35f) / 0.65f);
                _leftEye.color = colour;
            }
        }
    }
}
