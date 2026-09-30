using System.Collections.Generic;
using UnityEngine;
using NO404.Core;

namespace NO404.CCTV
{
    /// <summary>
    /// Rendering side of the CCTV system (GDD 20.13).
    ///
    /// Resolution/rate tiers:
    ///   - selected full-screen channel: 1280x720 @ 30fps
    ///   - selected grid channel:         960x540  @ 30fps
    ///   - other visible grid channels:   480x270  @ 12fps
    ///   - channels that are not on screen: camera disabled entirely, logic still ticks
    ///
    /// Under URP a camera cannot be driven with Camera.Render(), so the rate limit works by
    /// enabling a camera for exactly one frame per interval: a camera with a target texture
    /// renders once while enabled, then goes back to costing nothing.
    /// </summary>
    public sealed class CctvRig : MonoBehaviour
    {
        public sealed class Feed
        {
            public string CameraId;
            public string ZoneId;
            public Camera Camera;
            public RenderTexture Texture;
            public int Width;
            public int Height;
            public float Interval;
            public float NextRenderTime;
            public bool Visible;
        }

        readonly List<Feed> _feeds = new List<Feed>(12);
        readonly Dictionary<string, Feed> _byId = new Dictionary<string, Feed>(12);

        ContentData.ContentDatabase _content;

        public IReadOnlyList<Feed> Feeds { get { return _feeds; } }

        public static CctvRig Create(Transform parent)
        {
            var go = new GameObject("CctvRig");
            go.transform.SetParent(parent, false);
            return go.AddComponent<CctvRig>();
        }

        /// <summary>Builds every feed whose zone is currently resident. Called once at boot.</summary>
        public void BuildCameras(ContentData.ContentDatabase content)
        {
            Clear();
            _content = content;

            var channels = content.CctvChannels;
            for (int i = 0; i < channels.Count; i++) TryBuildCamera(channels[i]);

            Log.Info("CCTV", "built " + _feeds.Count + " camera feeds");
        }

        /// <summary>
        /// A floor just streamed in: build only the cameras that live in it instead of tearing
        /// down and recreating all twelve feeds.
        /// </summary>
        public void AddCamerasForGroup(string group)
        {
            if (_content == null) return;

            var zones = Gameplay.ZoneGroups.ZonesIn(group);
            var channels = _content.CctvChannels;
            for (int i = 0; i < channels.Count; i++)
            {
                if (_byId.ContainsKey(channels[i].cameraId)) continue;
                if (!InZones(channels[i].zoneId, zones)) continue;
                TryBuildCamera(channels[i]);
            }
        }

        /// <summary>A floor just streamed out: drop only the cameras that lived in it.</summary>
        public void RemoveCamerasForGroup(string group)
        {
            var zones = Gameplay.ZoneGroups.ZonesIn(group);
            for (int i = _feeds.Count - 1; i >= 0; i--)
            {
                var feed = _feeds[i];
                if (!InZones(feed.ZoneId, zones)) continue;

                DestroyFeed(feed);
                _feeds.RemoveAt(i);
                _byId.Remove(feed.CameraId);
            }
        }

        static bool InZones(string zoneId, string[] zones)
        {
            for (int i = 0; i < zones.Length; i++) if (zones[i] == zoneId) return true;
            return false;
        }

        /// <summary>
        /// Every camera's zoneId names a real zone (GDD 12.1), so a missing root only ever
        /// means the floor has not streamed in yet - that is normal, not a warning.
        /// </summary>
        void TryBuildCamera(CctvChannelDefinition definition)
        {
            var zoneRoot = Gameplay.ZoneRegistry.Find(definition.zoneId);
            if (zoneRoot == null)
            {
                if (Gameplay.ZoneGroups.GroupOf(definition.zoneId) == null)
                    Log.Warn("CCTV", "unknown zone for " + definition.cameraId + " (" + definition.zoneId + ")");
                return;
            }

            var go = new GameObject("CAM_" + definition.cameraId);
            go.transform.SetParent(zoneRoot, false);
            go.transform.localPosition = definition.localPosition;
            go.transform.localRotation = Quaternion.Euler(definition.localEuler);

            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = definition.fieldOfView;
            // CAM-01 is also the interphone lens. Its old pose looked away from the
            // doorstep, leaving the first caller out of frame even with a real model.
            var doorstep = Gameplay.WorldBuilder.LobbyDoorstep;
            if (definition.cameraId == "CAM-01" && doorstep != null)
            {
                go.transform.position = doorstep.position + new Vector3(.18f, 1.66f, -.90f);
                go.transform.rotation = Quaternion.LookRotation(
                    doorstep.position + Vector3.up * 1.50f - go.transform.position);
                camera.fieldOfView = 60f;
            }
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 1f);
            camera.enabled = false;   // switched on for one frame per interval
            camera.depth = -10;

            var feed = new Feed
            {
                CameraId = definition.cameraId,
                ZoneId = definition.zoneId,
                Camera = camera,
                Width = 480,
                Height = 270,
                Interval = 1f / 12f
            };
            Allocate(feed, 480, 270);

            _feeds.Add(feed);
            _byId[definition.cameraId] = feed;
        }

        public RenderTexture TextureFor(string cameraId)
        {
            Feed feed;
            return _byId.TryGetValue(cameraId, out feed) ? feed.Texture : null;
        }

        /// <summary>The live camera for a channel, or null while its floor is streamed out.</summary>
        public Camera CameraFor(string cameraId)
        {
            Feed feed;
            return _byId.TryGetValue(cameraId, out feed) ? feed.Camera : null;
        }

        /// <summary>
        /// Declares which channels are on screen this frame and at what tier.
        /// Anything not listed stops rendering entirely.
        /// </summary>
        public void SetVisible(IReadOnlyList<string> visibleIds, string focusedId, bool fullscreen)
        {
            for (int i = 0; i < _feeds.Count; i++) _feeds[i].Visible = false;

            if (visibleIds != null)
            {
                for (int i = 0; i < visibleIds.Count; i++)
                {
                    Feed feed;
                    if (!_byId.TryGetValue(visibleIds[i], out feed)) continue;

                    feed.Visible = true;
                    bool focused = visibleIds[i] == focusedId;

                    int width = focused ? (fullscreen ? 1280 : 960) : 480;
                    int height = focused ? (fullscreen ? 720 : 540) : 270;
                    feed.Interval = focused ? 1f / 30f : 1f / 12f;

                    if (feed.Width != width || feed.Height != height) Allocate(feed, width, height);
                }
            }
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;

            for (int i = 0; i < _feeds.Count; i++)
            {
                var feed = _feeds[i];
                if (feed.Camera == null) continue;

                // A camera that rendered last frame is switched off again immediately, so a
                // channel costs one render per interval no matter how long it stays on screen.
                if (feed.Camera.enabled) feed.Camera.enabled = false;

                if (!feed.Visible || now < feed.NextRenderTime) continue;

                feed.NextRenderTime = now + feed.Interval;
                feed.Camera.enabled = true;
            }
        }

        void Allocate(Feed feed, int width, int height)
        {
            if (feed.Texture != null)
            {
                feed.Camera.targetTexture = null;
                feed.Texture.Release();
                Destroy(feed.Texture);
            }

            feed.Width = width;
            feed.Height = height;
            feed.Texture = new RenderTexture(width, height, 16, RenderTextureFormat.Default)
            {
                name = "RT_" + feed.CameraId,
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1
            };
            feed.Texture.Create();
            feed.Camera.targetTexture = feed.Texture;
        }

        void Clear()
        {
            for (int i = 0; i < _feeds.Count; i++) DestroyFeed(_feeds[i]);

            _feeds.Clear();
            _byId.Clear();
        }

        static void DestroyFeed(Feed feed)
        {
            if (feed.Camera != null)
            {
                feed.Camera.targetTexture = null;
                Destroy(feed.Camera.gameObject);
            }
            if (feed.Texture == null) return;
            feed.Texture.Release();
            Destroy(feed.Texture);
        }

        void OnDestroy() { Clear(); }
    }
}
