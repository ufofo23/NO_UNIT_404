using System.Collections.Generic;
using UnityEngine;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// Keeps a short rolling record of where the player has been.
    ///
    /// It exists for one scene: GDD 2.3 marketing hook #2 and catalogue entries #13
    /// ("플레이어의 지연 복제") and #24. A camera shows the office, the player is standing
    /// still at the desk, and on the monitor they walk away from it. That only works if the
    /// game remembers the walk, so this records it and AnomalyStager replays it on the
    /// CCTV-only layer - the same rule as every other staged anomaly, so turning round finds
    /// nothing (GDD 4.4).
    /// </summary>
    public sealed class PlayerEchoRecorder : MonoBehaviour
    {
        /// <summary>Seconds of history kept. Longer than any lag an anomaly asks for.</summary>
        public const float HistorySeconds = 60f;
        const float SampleInterval = 0.25f;

        /// <summary>Below this the player was standing still and there is no walk to replay.</summary>
        const float MinimumPathLength = 1.5f;

        struct Sample
        {
            public float RealTime;
            public Vector3 Position;
            public Sample(float realTime, Vector3 position) { RealTime = realTime; Position = position; }
        }

        readonly Queue<Sample> _history = new Queue<Sample>(256);
        float _nextSampleTime;

        public static PlayerEchoRecorder Attach(GameObject player)
        {
            return player == null ? null : player.AddComponent<PlayerEchoRecorder>();
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _nextSampleTime) return;
            _nextSampleTime = now + SampleInterval;

            _history.Enqueue(new Sample(now, transform.position));

            while (_history.Count > 0 && now - _history.Peek().RealTime > HistorySeconds)
                _history.Dequeue();
        }

        /// <summary>
        /// The route the player walked, ending <paramref name="lagSeconds"/> ago, expressed in
        /// <paramref name="zoneRoot"/>'s local space so a ResidentActor can walk it.
        /// Returns null when there is nothing worth replaying - the caller falls back to an
        /// ordinary staged prop rather than putting a motionless double on the screen.
        /// </summary>
        public Vector3[] EchoPath(Transform zoneRoot, float lagSeconds)
        {
            if (zoneRoot == null || _history.Count < 2) return null;

            float cutoff = Time.unscaledTime - Mathf.Max(0f, lagSeconds);
            var points = new List<Vector3>(64);
            float length = 0f;

            foreach (var sample in _history)
            {
                if (sample.RealTime > cutoff) break;

                var local = zoneRoot.InverseTransformPoint(sample.Position);
                local.y = 0.02f;

                // Drop samples the player barely moved between, so a minute of standing at the
                // desk does not become sixty identical waypoints.
                if (points.Count > 0)
                {
                    float step = Vector3.Distance(points[points.Count - 1], local);
                    if (step < 0.25f) continue;
                    length += step;
                }

                points.Add(local);
            }

            return points.Count >= 2 && length >= MinimumPathLength ? points.ToArray() : null;
        }

        public void Clear() { _history.Clear(); }
    }
}
