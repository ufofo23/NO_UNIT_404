using System.Collections.Generic;
using UnityEngine;
using NO404.ContentData;
using NO404.Core;

namespace NO404.CCTV
{
    /// <summary>
    /// Puts an anomaly in front of its camera while it runs.
    ///
    /// CctvService owns the schedule and CctvRig owns the picture, but until this existed
    /// nothing ever entered the frame: an anomaly was a tint and a caption over an empty
    /// corridor. This is the greybox stand-in for that performance - a primitive shaped and
    /// placed by the GDD 12.3 family, on the CCTV-only layer so the player camera never
    /// renders it (GDD 4.4: walking to the spot must find nothing there).
    ///
    /// Replacing these primitives with real animated actors is a prefab swap in StageProp.
    /// </summary>
    public sealed class AnomalyStager : MonoBehaviour
    {
        /// <summary>How far down the camera's line of sight the prop is placed.</summary>
        const float StageDistance = 3.2f;

        /// <summary>GDD 5.9: Harin is a child. Her anomalies read at a child's height.</summary>
        const float ChildHeight = 1.18f;
        const float AdultHeight = 1.72f;

        /// <summary>
        /// GDD 12.3 #13 and #24: the double of the player, lagging behind what they actually
        /// did. Marketing hook #2 in GDD 2.3.
        /// </summary>
        const int EchoDelayedType = 13;
        const int EchoAheadType = 24;
        const float EchoLagSeconds = 10f;

        /// <summary>
        /// GDD 12.3 #9: the same courier standing in the lobby twice at once. Marketing hook
        /// #4 in GDD 2.3, and the image night 2's whole case is built on.
        /// </summary>
        const int DoubledPersonType = 9;
        const float DoubleSeparation = 1.3f;

        readonly Dictionary<string, GameObject> _staged = new Dictionary<string, GameObject>();

        /// <summary>
        /// Staged props that walk. ResidentTrafficService drives its own people; anything this
        /// class puts on a screen has to be driven here, on the same game clock, or the echo
        /// spawns and then stands perfectly still.
        /// </summary>
        readonly List<Residents.ResidentActor> _walkers = new List<Residents.ResidentActor>(4);

        ContentDatabase _content;
        CctvRig _rig;
        Gameplay.PlayerEchoRecorder _echo;

        public static AnomalyStager Create(Transform parent, CctvRig rig, ContentDatabase content,
                                           Gameplay.PlayerEchoRecorder echo)
        {
            var go = new GameObject("AnomalyStager");
            go.transform.SetParent(parent, false);

            var stager = go.AddComponent<AnomalyStager>();
            stager._rig = rig;
            stager._content = content;
            stager._echo = echo;
            return stager;
        }

        void OnEnable()
        {
            EventBus.Subscribe<CctvAnomalyEvent>(OnAnomaly);
            EventBus.Subscribe<PlaythroughResetEvent>(OnPlaythroughReset);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<CctvAnomalyEvent>(OnAnomaly);
            EventBus.Unsubscribe<PlaythroughResetEvent>(OnPlaythroughReset);
        }

        void OnPlaythroughReset(PlaythroughResetEvent evt) { ClearAll(); }

        void Update()
        {
            if (_walkers.Count == 0) return;

            float walked = Time.unscaledDeltaTime * ServiceHub.Clock.TimeScale
                           * GameClock.GameSecondsPerRealSecond;

            for (int i = _walkers.Count - 1; i >= 0; i--)
            {
                var walker = _walkers[i];
                if (walker == null) { _walkers.RemoveAt(i); continue; }

                walker.Advance(walked);
                if (walker.Finished) _walkers.RemoveAt(i);   // left standing until its anomaly ends
            }
        }

        void OnAnomaly(CctvAnomalyEvent evt)
        {
            if (evt.Started) Stage(evt.AnomalyId, evt.CameraId);
            else Strike(evt.AnomalyId);
        }

        void Stage(string anomalyId, string cameraId)
        {
            if (string.IsNullOrEmpty(anomalyId) || _staged.ContainsKey(anomalyId)) return;

            var anomaly = _content != null ? _content.FindAnomaly(anomalyId) : null;
            if (anomaly == null) return;

            // No camera means the floor is streamed out, so nothing is watching this feed
            // anyway. The anomaly still runs in the service; it just has no stage tonight.
            var camera = _rig != null ? _rig.CameraFor(cameraId) : null;
            if (camera == null) return;

            var prop = StagePlayerEcho(anomaly, camera)
                       ?? StageDoubledPerson(anomaly, camera)
                       ?? StageProp(anomaly, camera);
            if (prop != null) _staged[anomalyId] = prop;
        }

        void Strike(string anomalyId)
        {
            if (string.IsNullOrEmpty(anomalyId)) return;

            GameObject prop;
            if (!_staged.TryGetValue(anomalyId, out prop)) return;

            _staged.Remove(anomalyId);
            if (prop != null) Destroy(prop);
        }

        void ClearAll()
        {
            foreach (var pair in _staged) if (pair.Value != null) Destroy(pair.Value);
            _staged.Clear();
            _walkers.Clear();
        }

        /// <summary>
        /// Replays the player's own last minute on the CCTV-only layer, so the monitor shows
        /// them walking a route they finished ten seconds ago while they stand still watching.
        ///
        /// Returns null - and the caller falls back to an ordinary prop - unless everything the
        /// scene needs is true: the right catalogue entry, a camera in the room the player is
        /// actually standing in, and enough recorded walking to be worth replaying. A double
        /// standing motionless in a corridor the player has never entered is not the shot.
        /// </summary>
        GameObject StagePlayerEcho(AnomalyDefinition anomaly, Camera camera)
        {
            if (anomaly.typeNumber != EchoDelayedType && anomaly.typeNumber != EchoAheadType) return null;
            if (_echo == null) return null;

            var zoneRoot = camera.transform.parent;
            if (zoneRoot == null) return null;

            // The double only reads as the player's double if it is in the player's own room.
            var channel = ServiceHub.Cctv.Find(anomaly.cameraId);
            if (channel == null || channel.Definition == null) return null;
            if (channel.Definition.zoneId != ServiceHub.Player.CurrentZone) return null;

            var path = _echo.EchoPath(zoneRoot, EchoLagSeconds);
            if (path == null) return null;

            var actor = Residents.ResidentActor.Spawn(zoneRoot, channel.Definition.zoneId, "speaker.self",
                                                      path, new Color(0.34f, 0.36f, 0.40f), true,
                                                      Gameplay.PlayerController.WalkSpeed);
            if (actor == null) return null;

            actor.name = "ANOMALY_ECHO_" + anomaly.anomalyId;
            _walkers.Add(actor);

            Log.Info("CCTV", "staged the player's echo on " + anomaly.cameraId);
            return actor.gameObject;
        }

        /// <summary>
        /// Two of the same person, standing still, in one frame. Not two people who look alike -
        /// identical height, identical colour, side by side, both facing the lens. The player
        /// is meant to count them before they understand them.
        /// </summary>
        GameObject StageDoubledPerson(AnomalyDefinition anomaly, Camera camera)
        {
            if (anomaly.typeNumber != DoubledPersonType) return null;

            var zoneRoot = camera.transform.parent;
            if (zoneRoot == null) return null;

            var group = new GameObject("ANOMALY_DOUBLE_" + anomaly.anomalyId);
            group.transform.SetParent(zoneRoot, false);

            var centre = camera.transform.position + camera.transform.forward * StageDistance;
            var sideways = camera.transform.right;
            float floorY = zoneRoot.position.y;
            var colour = ColorFor(anomaly);

            for (int i = 0; i < 2; i++)
            {
                float offset = i == 0 ? -DoubleSeparation : DoubleSeparation;
                var placement = centre + sideways * offset;

                var figure = BuildFigure(group.transform, colour, AdultHeight);
                figure.transform.position = new Vector3(placement.x, floorY + AdultHeight * 0.5f, placement.z);
                figure.transform.rotation = Quaternion.LookRotation(-camera.transform.forward, Vector3.up);
            }

            Log.Info("CCTV", "staged two of the same person on " + anomaly.cameraId);
            return group;
        }

        /// <summary>One capsule on the CCTV-only layer, tinted and stripped of its collider.</summary>
        GameObject BuildFigure(Transform parent, Color colour, float height)
        {
            var figure = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            figure.transform.SetParent(parent, false);
            figure.transform.localScale = new Vector3(0.42f, height * 0.5f, 0.42f);

            var collider = figure.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            if (Layers.CctvOnly >= 0) figure.layer = Layers.CctvOnly;

            var renderer = figure.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = Core.GreyboxMaterial.Tinted(colour);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return figure;
        }

        GameObject StageProp(AnomalyDefinition anomaly, Camera camera)
        {
            bool figure = anomaly.category == AnomalyCategory.PersonContradiction
                       || anomaly.category == AnomalyCategory.DirectThreat;

            float height = figure ? (IsHarin(anomaly.typeNumber) ? ChildHeight : AdultHeight) : 0.5f;
            var artName = "ANOMALY_" + anomaly.anomalyId;

            // The promise this class has carried in a comment since it was written: swapping
            // these primitives for real actors is a prefab drop. Now it is one. A figure asks
            // for a model of its own name; anything without one is still a capsule.
            var prop = Gameplay.PropArt.TryBuild(camera.transform.parent, artName, Vector3.zero,
                                                 new Vector3(0.42f, height, 0.42f),
                                                 wantsCollider: false);
            bool isArt = prop != null;

            if (!isArt)
            {
                prop = GameObject.CreatePrimitive(figure ? PrimitiveType.Capsule : PrimitiveType.Cube);
                prop.name = artName;
                prop.transform.SetParent(camera.transform.parent, false);

                // The collider would block patrols and the interaction raycast, and the prop is
                // never meant to be touchable - it only exists for the lens.
                var collider = prop.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                prop.transform.localScale = figure
                    ? new Vector3(0.42f, height * 0.5f, 0.42f)   // a capsule's scale is a half-height
                    : new Vector3(height, height, height);
            }

            // Recursive, because a model has children and every one of them has to stay off
            // the player's camera.
            Gameplay.PropArt.SetLayerRecursively(prop, Layers.CctvOnly);

            // Straight down the lens, then dropped to stand on the floor of the camera's zone.
            var placement = camera.transform.position + camera.transform.forward * StageDistance;
            var zoneRoot = camera.transform.parent;
            float floorY = zoneRoot != null ? zoneRoot.position.y : placement.y;
            prop.transform.position = new Vector3(placement.x, floorY + height * 0.5f, placement.z);
            prop.transform.rotation = Quaternion.LookRotation(-camera.transform.forward, Vector3.up);

            // CreatePrimitive does NOT carry the pipeline's material - it carries the legacy
            // built-in one, which is why the whole game rendered magenta in a build. The
            // comment that used to sit here said the opposite and was believed for a while.
            var renderer = prop.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = Core.GreyboxMaterial.Tinted(ColorFor(anomaly));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Log.Trace("CCTV", "staged " + anomaly.anomalyId + " on " + anomaly.cameraId);
            return prop;
        }

        /// <summary>The catalogue entries that are Harin herself (GDD 12.3 #5, #14, #31).</summary>
        static bool IsHarin(int typeNumber)
        {
            return typeNumber == 5 || typeNumber == 14 || typeNumber == 31;
        }

        /// <summary>
        /// GDD 12.4 forbids anomalies that are only distinguishable by colour, so this is a
        /// readability aid on top of shape, height and placement - never the only signal.
        /// </summary>
        static Color ColorFor(AnomalyDefinition anomaly)
        {
            if (IsHarin(anomaly.typeNumber)) return new Color(0.86f, 0.74f, 0.22f);  // the yellow raincoat

            switch (anomaly.category)
            {
                case AnomalyCategory.PersonContradiction: return new Color(0.62f, 0.60f, 0.58f);
                case AnomalyCategory.DirectThreat: return new Color(0.20f, 0.20f, 0.22f);
                case AnomalyCategory.TimeEnvironment: return new Color(0.52f, 0.58f, 0.66f);
                case AnomalyCategory.SpaceContradiction: return new Color(0.44f, 0.42f, 0.46f);
                default: return new Color(0.70f, 0.66f, 0.58f);
            }
        }
    }
}
