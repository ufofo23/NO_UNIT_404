using UnityEngine;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// Tells <see cref="StairNavigator"/> which landing the player has actually reached.
    ///
    /// This was the missing half of the stairwell. The navigator knew how to answer "what is
    /// one flight up from here", the shaft had nine real landings at nine real heights, and
    /// nothing connected the two - so the current landing stayed on whichever floor the player
    /// entered from, however far they climbed. Every other landing door then refused to open,
    /// because each one only works from the landing it is on.
    ///
    /// It watches height rather than using trigger volumes on the landings: the heights are
    /// already the single definition of where the landings are (BuildStairSigns reads the same
    /// table), and a set of colliders sized to match would be a second copy of that to keep in
    /// step.
    /// </summary>
    public sealed class StairLandingTracker : MonoBehaviour
    {
        /// <summary>
        /// How close to a landing counts as being on it.
        ///
        /// Just under half a flight, so the bands do not overlap and there is no height in the
        /// shaft that belongs to two landings. Mid-flight the player belongs to neither and
        /// the current landing simply does not change, which is right: they are on the stairs.
        /// </summary>
        public const float LandingBand = BuildingSpec.FlightRise * 0.45f;

        string[] _floorIds;
        float[] _heights;
        string _lastReported;

        public void Setup(string[] floorIds, float[] heights)
        {
            _floorIds = floorIds;
            _heights = heights;
        }

        void OnEnable() { _lastReported = null; }

        void Update()
        {
            if (_floorIds == null || _floorIds.Length == 0) return;

            var player = PlayerController.Active;
            if (player == null) return;

            // Only while they are actually in the shaft. Every floor sits at its own place in
            // world space, so a player on the fifth floor is at a height that means nothing here.
            if (ServiceHub.Player == null || ServiceHub.Player.CurrentZone != ZoneIds.Stairwell) return;

            float localY = transform.InverseTransformPoint(player.transform.position).y;

            string nearest = null;
            float best = LandingBand;

            for (int i = 0; i < _floorIds.Length; i++)
            {
                float distance = Mathf.Abs(localY - _heights[i]);
                if (distance > best) continue;
                best = distance;
                nearest = _floorIds[i];
            }

            if (nearest == null || nearest == _lastReported) return;

            _lastReported = nearest;
            ServiceHub.Stairs.ArriveAt(nearest);
        }
    }
}
