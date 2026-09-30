using UnityEngine;
using UnityEngine.UI;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.Anomalies
{
    /// <summary>
    /// A piece of text in the world that stops being reliable as the caretaker takes on more
    /// of the building (v2.1 spec 0.10.4).
    ///
    /// Two effects, because the spec describes two and they belong to different bands: from
    /// 25 a glyph deforms for a moment, and from 50 a stair landing can show a floor it is
    /// not. Both live here rather than in the world builder because both are about what the
    /// caretaker is carrying, not about where the sign is.
    ///
    /// The rule that shapes the whole component is spec 0.10.4's own and spec 32's: this may
    /// make a reading doubtful and may never make it wrong to act on. So every distortion is
    /// temporary and self-correcting - the label restores itself on the next interval, and a
    /// caretaker who simply looks again gets the truth. Nothing downstream reads these strings;
    /// StairNavigator computes the next landing from FloorPlan, and always did.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class DistortedLabel : MonoBehaviour
    {
        /// <summary>Game seconds between one look at the sign and the next.</summary>
        public const int IntervalSeconds = 40;

        /// <summary>How long a distorted reading stands before it corrects itself.</summary>
        public const int HoldSeconds = 8;

        [SerializeField] string _textKey;
        [SerializeField] string _floorId;
        [SerializeField] bool _mayMisreadFloor;

        Text _label;
        string _truth;
        int _nextChangeSecond;
        bool _distorted;

        public bool IsDistorted { get { return _distorted; } }
        public string Truth { get { return _truth; } }

        /// <summary>
        /// A label that can only lose a glyph.
        ///
        /// Used for anything that is not a floor number: a corrupted character is a moment of
        /// doubt, and doubt about a corridor name costs the caretaker nothing but the second
        /// look it takes to clear it.
        /// </summary>
        public DistortedLabel Setup(string textKey)
        {
            _textKey = textKey;
            return this;
        }

        /// <summary>
        /// A landing sign, which can also read as the wrong floor entirely (spec 0.10.4, 50+).
        ///
        /// The floor it belongs to is passed in so the misreading can be a neighbour rather
        /// than a random storey: a sign that said B2 on the fifth floor would be noticed
        /// instantly and be nothing but a bug. One floor out is the reading that costs a
        /// caretaker a flight before they realise.
        /// </summary>
        public DistortedLabel SetupLanding(string textKey, string floorId)
        {
            _textKey = textKey;
            _floorId = floorId;
            _mayMisreadFloor = true;
            return this;
        }

        void Awake()
        {
            _label = GetComponent<Text>();
            _truth = string.IsNullOrEmpty(_textKey) ? _label.text : Loc.T(_textKey);
            _label.text = _truth;
        }

        void OnEnable()
        {
            EventBus.Subscribe<PlaythroughResetEvent>(HandleReset);
            Restore();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PlaythroughResetEvent>(HandleReset);
        }

        void HandleReset(PlaythroughResetEvent evt)
        {
            _nextChangeSecond = 0;
            Restore();
        }

        void Update()
        {
            var distortion = ServiceHub.Distortion;
            var clock = ServiceHub.Clock;
            if (distortion == null || clock == null) return;

            // A clean caretaker sees clean signs, and a sign that was mid-distortion when the
            // exposure came back down corrects itself rather than staying wrong.
            if (distortion.Band < DistortionBand.Faint)
            {
                if (_distorted) Restore();
                return;
            }

            int now = clock.GameSecond;
            if (now < _nextChangeSecond) return;

            if (_distorted) { Restore(); _nextChangeSecond = now + IntervalSeconds; return; }

            Distort(distortion);
            _nextChangeSecond = now + HoldSeconds;
        }

        void Distort(DistortionDirector distortion)
        {
            // The wrong floor first, because it is the stronger reading and the two would
            // otherwise fight over the same characters.
            if (_mayMisreadFloor && distortion.MayMisreadFloor && Random.value < 0.5f)
            {
                var neighbour = NeighbourOf(_floorId);
                if (!string.IsNullOrEmpty(neighbour))
                {
                    _label.text = Loc.T(LandingKeyOf(neighbour));
                    _distorted = true;
                    return;
                }
            }

            var corrupted = distortion.Corrupt(_truth);
            if (corrupted == _truth) return;

            _label.text = corrupted;
            _distorted = true;
        }

        void Restore()
        {
            if (_label == null) return;
            _label.text = _truth;
            _distorted = false;
        }

        /// <summary>
        /// A floor one step up or down the building, or null at the ends of it.
        ///
        /// Read off FloorPlan rather than from a table here, so a sign can never misread as a
        /// storey the building does not have - the thirteenth floor included (spec 0.7.1).
        /// </summary>
        public static string NeighbourOf(string floorId)
        {
            var order = FloorPlan.Order;
            int index = System.Array.IndexOf(order, floorId);
            if (index < 0) return null;

            bool up = Random.value < 0.5f;
            int target = up ? index + 1 : index - 1;
            if (target < 0 || target >= order.Length) target = up ? index - 1 : index + 1;
            if (target < 0 || target >= order.Length) return null;

            return order[target];
        }

        public static string LandingKeyOf(string floorId)
        {
            return "world.stairs." + floorId.ToLowerInvariant() + ".short";
        }
    }
}
