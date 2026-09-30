using UnityEngine;
using NO404.Core;

namespace NO404.Residents
{
    /// <summary>
    /// One person walking through a zone.
    ///
    /// GDD 3.3 cut "주민 전원의 실시간 이동" from the 4 month schedule, which left the building
    /// empty: twelve cameras watching corridors nobody ever crosses. That emptiness is also
    /// what made eight of the GDD 12.3 anomaly types unbuildable - "같은 배달원 동시 등장",
    /// "선자가 계단을 빠르게 뛰어 올라감", "강태호가 두 방향에서 동시에 접근" all need ordinary
    /// traffic to be a contradiction against.
    ///
    /// So this is deliberately not an NPC in the usual sense. It has no AI, no schedule of its
    /// own and no dialogue: it walks a line and leaves. ResidentTrafficService decides who
    /// walks, when, and whether the access log agrees.
    /// </summary>
    public sealed class ResidentActor : MonoBehaviour
    {
        /// <summary>Unhurried. GDD 8.2 puts the player at 2.4, so nobody here outpaces them.</summary>
        public const float WalkSpeed = 1.1f;

        /// <summary>The one exception: catalogue #10 is Sunja taking the stairs far too fast.</summary>
        public const float HurriedSpeed = 3.4f;

        Vector3[] _path;
        int _target;
        float _speed = WalkSpeed;

        public bool Finished { get; private set; }
        public string NameKey { get; private set; }
        public string ZoneId { get; private set; }

        /// <summary>
        /// Builds a figure inside a zone and starts it walking. A null zone root means the
        /// floor is not streamed in, and the caller is expected to skip the actor entirely -
        /// the access log entry still gets written, because the record exists whether or not
        /// anyone was watching.
        /// </summary>
        public static ResidentActor Spawn(Transform zoneRoot, string zoneId, string nameKey,
                                          Vector3[] localPath, Color color, bool cctvOnly, float speed)
        {
            if (zoneRoot == null || localPath == null || localPath.Length < 2) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "RESIDENT_" + nameKey;
            go.transform.SetParent(zoneRoot, false);
            go.transform.localScale = new Vector3(0.44f, 0.85f, 0.44f);   // 1.7m tall

            // No collider: a 1.55m corridor (GDD 17.4) is too narrow for two capsules to pass,
            // and being physically shoved into a corner by a neighbour is not a horror beat,
            // it is a bug report. Residents are seen, never bumped into.
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            if (cctvOnly && Layers.CctvOnly >= 0) go.layer = Layers.CctvOnly;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // A primitive's own material is the built-in one, which URP cannot draw.
                renderer.sharedMaterial = Core.GreyboxMaterial.Tinted(color);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var actor = go.AddComponent<ResidentActor>();
            actor.NameKey = nameKey;
            actor.ZoneId = zoneId;
            actor._path = localPath;
            actor._speed = speed;
            actor._target = 1;
            go.transform.localPosition = localPath[0];
            actor.FaceTarget();

            return actor;
        }

        /// <summary>
        /// Driven by ResidentTrafficService rather than Update, so people walk on game time:
        /// they slow to a crawl while the player is in the facility OS and stop dead in the
        /// evidence board, exactly like everything else the clock owns (GDD 6.2).
        /// </summary>
        public void Advance(float seconds)
        {
            if (Finished || _path == null || seconds <= 0f) return;

            float budget = _speed * seconds;

            while (budget > 0f && !Finished)
            {
                var here = transform.localPosition;
                var next = _path[_target];
                float gap = Vector3.Distance(here, next);

                if (gap <= budget)
                {
                    transform.localPosition = next;
                    budget -= gap;
                    _target++;

                    if (_target >= _path.Length) { Finished = true; return; }
                    FaceTarget();
                    continue;
                }

                transform.localPosition = Vector3.MoveTowards(here, next, budget);
                budget = 0f;
            }
        }

        void FaceTarget()
        {
            if (_target >= _path.Length) return;

            var direction = _path[_target] - transform.localPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        public void Despawn()
        {
            Finished = true;
            if (this != null && gameObject != null) Destroy(gameObject);
        }
    }
}
