using UnityEngine;
using NO404.Core;
using NO404.Gameplay;
using NO404.Interaction;

namespace NO404.Anomalies
{
    /// <summary>
    /// What a mishandled floor looks like (v2.1 spec 0.10.3).
    ///
    /// FloorRisk is never shown as a number - the spec says so twice - so until something read
    /// it, a floor the caretaker had ruined was indistinguishable from one they had kept. The
    /// counter went up, the save recorded it, and the corridor was the same corridor.
    ///
    /// One of these rides each zone root and is switched by the risk on the floor that zone
    /// belongs to. The tiers are cumulative on purpose: a floor at 3 is still doing everything
    /// a floor at 1 does, because that is how the spec reads and because a corridor that got
    /// quieter as it got worse would be the wrong signal entirely.
    /// </summary>
    public sealed class FloorDressing : MonoBehaviour
    {
        [SerializeField] string _zoneId;
        [SerializeField] string _floorId;

        Light _light;
        float _baseIntensity;

        /// <summary>Tier 2: something that was not there a moment ago, and is not there now.</summary>
        GameObject _afterimage;
        /// <summary>Tier 3: a door in a wall that has never had one.</summary>
        GameObject _falseDoor;
        /// <summary>Tier 4: the way through that used to be open.</summary>
        GameObject _blockedRoute;

        FloorRiskTier _appliedTier = FloorRiskTier.Clear;
        int _nextAfterimageSecond;

        public string ZoneId { get { return _zoneId; } }
        public string FloorId { get { return _floorId; } }
        public FloorRiskTier AppliedTier { get { return _appliedTier; } }

        public FloorDressing Setup(string zoneId, string floorId)
        {
            _zoneId = zoneId;
            _floorId = floorId;

            // Apply here as well as in OnEnable, because OnEnable has already run by the time
            // this is called: AddComponent enables the component before the caller can say
            // which floor it belongs to, so that first pass asked the risk service about a
            // null floor and got Clear. Without this line a zone that streams in while its
            // floor is already at 4 comes up spotless and stays that way until the risk
            // happens to change again - which on a bad floor is exactly when it will not.
            Apply(CurrentTier);
            return this;
        }

        void Awake()
        {
            _light = GetComponentInChildren<Light>();
            if (_light != null) _baseIntensity = _light.intensity;
        }

        void OnEnable()
        {
            EventBus.Subscribe<FloorRiskChangedEvent>(HandleRiskChanged);
            EventBus.Subscribe<SaveRestoredEvent>(HandleSaveRestored);
            Apply(CurrentTier);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<FloorRiskChangedEvent>(HandleRiskChanged);
            EventBus.Unsubscribe<SaveRestoredEvent>(HandleSaveRestored);
        }

        FloorRiskTier CurrentTier
        {
            get
            {
                var risk = ServiceHub.Risk;
                return risk == null ? FloorRiskTier.Clear : risk.TierOf(_floorId);
            }
        }

        void HandleRiskChanged(FloorRiskChangedEvent evt)
        {
            if (evt.FloorId == _floorId) Apply(CurrentTier);
        }

        void HandleSaveRestored(SaveRestoredEvent evt) { Apply(CurrentTier); }

        /// <summary>
        /// Build what this tier needs and switch on what it shows.
        ///
        /// Everything is created lazily and then only enabled or disabled, so a floor that
        /// swings between tiers during a night is not repeatedly allocating a door.
        /// </summary>
        public void Apply(FloorRiskTier tier)
        {
            _appliedTier = tier;

            // Tier 1 and up: the light is never quite right again (spec 0.10.3).
            if (_light != null)
            {
                float factor = 1f - 0.08f * (int)tier;
                _light.intensity = _baseIntensity * Mathf.Max(0.5f, factor);
            }

            EnsureAfterimage(tier >= FloorRiskTier.Deceptive);
            EnsureFalseDoor(tier >= FloorRiskTier.Obstructive);
            EnsureBlockedRoute(tier >= FloorRiskTier.Hostile);
        }

        void EnsureAfterimage(bool wanted)
        {
            if (!wanted)
            {
                if (_afterimage != null) _afterimage.SetActive(false);
                return;
            }

            // Whether this call is the one that brings it back, decided before anything is
            // created: a primitive comes into the world already active, so reading activeSelf
            // afterwards says "it was always here" about an object that did not exist a line
            // ago - and the hold below never happens.
            bool arriving = _afterimage == null || !_afterimage.activeSelf;

            if (_afterimage == null)
            {
                _afterimage = Box("RiskAfterimage", new Vector3(0f, 0.9f, 0f),
                                  new Vector3(0.35f, 1.7f, 0.25f),
                                  new Color(0.16f, 0.16f, 0.18f));
                // It is not a thing in the room. Walking into it would make it one, so the
                // collider is switched off rather than destroyed - Destroy is not available
                // in edit mode, and a disabled collider is the same nothing at runtime.
                var collider = _afterimage.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }

            // Hold it for a full interval before the flicker starts.
            //
            // Without this the first Update after the tier is reached re-rolls immediately -
            // the clock is tens of thousands of game seconds in, so any interval measured from
            // zero is long past - and better than a third of the time the afterimage vanishes
            // on the same frame it appeared. The one moment it most needs to be seen is the
            // moment the floor got worse.
            if (arriving)
            {
                var clock = ServiceHub.Clock;
                _nextAfterimageSecond = (clock != null ? clock.GameSecond : 0)
                                      + AfterimageIntervalSeconds;
            }

            _afterimage.SetActive(true);
        }

        void EnsureFalseDoor(bool wanted)
        {
            if (!wanted)
            {
                if (_falseDoor != null) _falseDoor.SetActive(false);
                return;
            }

            if (_falseDoor == null)
            {
                _falseDoor = Box("RiskFalseDoor", new Vector3(FalseDoorOffset, 1.0f, 0f),
                                 new Vector3(0.1f, 2.0f, 0.9f),
                                 new Color(0.30f, 0.26f, 0.24f));

                // Spec 0.10.3 tier 3: it is a door, it can be used, and it does not go
                // anywhere. Being refused by it is the point; being unable to try is not.
                _falseDoor.AddComponent<FalseDoor>();
            }

            _falseDoor.SetActive(true);
        }

        void EnsureBlockedRoute(bool wanted)
        {
            if (!wanted)
            {
                if (_blockedRoute != null) _blockedRoute.SetActive(false);
                return;
            }

            if (_blockedRoute == null)
                _blockedRoute = Box("RiskBlockedRoute", new Vector3(-2.4f, 0.9f, 0f),
                                    new Vector3(0.5f, 1.8f, 1.6f),
                                    new Color(0.34f, 0.30f, 0.22f));

            _blockedRoute.SetActive(true);
        }

        /// <summary>
        /// The afterimage moves when nobody is looking, which is all tier 2 ever promised.
        ///
        /// On the game clock rather than on frames, so it is the same on every machine and so
        /// a paused shift is genuinely paused.
        /// </summary>
        void Update()
        {
            // Gated on the tier rather than on whether it is currently showing. Reading the
            // object's own visibility would have been a one-way door: the first interval that
            // hid it would also stop the loop that brings it back.
            if (_afterimage == null || _appliedTier < FloorRiskTier.Deceptive) return;

            var clock = ServiceHub.Clock;
            if (clock == null) return;

            if (clock.GameSecond < _nextAfterimageSecond) return;
            _nextAfterimageSecond = clock.GameSecond + AfterimageIntervalSeconds;

            var position = _afterimage.transform.localPosition;
            position.x = Random.Range(-2f, 2f);
            position.z = Random.Range(-2f, 2f);
            _afterimage.transform.localPosition = position;
            _afterimage.SetActive(Random.value > 0.35f);
        }

        const int AfterimageIntervalSeconds = 45;

        /// <summary>Where the door that is not a door stands, in zone-local metres.</summary>
        const float FalseDoorOffset = 2.4f;

        GameObject Box(string name, Vector3 localPosition, Vector3 size, Color colour)
        {
            // The dressing goes through the same seam as everything else, so an afterimage or
            // a false door can be a real model without this file knowing about it.
            var art = PropArt.TryBuild(transform, name, localPosition, size);
            if (art != null) return art;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            // sharedMaterial, not material: touching .material instantiates a copy per prop,
            // which leaks in the editor and would give every dressed floor its own material at
            // runtime for no reason. One material per colour is what the greybox already does.
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = MaterialFor(colour);

            return go;
        }

        static readonly System.Collections.Generic.Dictionary<Color, Material> Materials =
            new System.Collections.Generic.Dictionary<Color, Material>();

        /// <summary>
        /// One shared material per colour, built off whatever a primitive ships with.
        ///
        /// The world builder keeps its own table of these, but the dressing is created long
        /// after the builder has finished and cannot reach it, so it keeps a small one of its
        /// own rather than holding a reference to a system it does not otherwise need.
        /// </summary>
        static Material MaterialFor(Color colour)
        {
            Material material;
            if (Materials.TryGetValue(colour, out material) && material != null) return material;

            material = Core.GreyboxMaterial.Tinted(colour);
            material.name = "RISK_DRESSING";

            Materials[colour] = material;
            return material;
        }
    }

    /// <summary>
    /// A door that is not one (spec 0.10.3 tier 3).
    ///
    /// It offers the ordinary door prompt and then refuses, because a false door the player
    /// cannot even try is just a wall with a different texture. The refusal is the beat.
    /// </summary>
    public sealed class FalseDoor : MonoBehaviour, IInteractable
    {
        public float Range { get { return 1.8f; } }

        public InteractionPrompt GetPrompt(in PlayerContext context)
        {
            return InteractionPrompt.Simple("ui.prompt.open_unit_door");
        }

        public bool CanInteract(in PlayerContext context, out string reasonKey)
        {
            reasonKey = string.Empty;
            return true;
        }

        public void Interact(in PlayerContext context)
        {
            EventBus.Publish(new NotificationEvent("notify.risk.false_door", NotificationSeverity.Warning));
            ServiceHub.Captions.Ambient("caption.false_door");
        }
    }
}
