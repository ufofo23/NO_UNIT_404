using UnityEngine;
using NO404.Core;
using NO404.Interaction;

namespace NO404.Gameplay
{
    /// <summary>
    /// First-person controller with the exact numbers from GDD 8.2:
    /// walk 2.4, sprint 4.2, crouch 1.4 m/s, 0.12s accel/decel smoothing, FOV 68 (60-90),
    /// head bob 20% default, mouse sensitivity 0.1-5.0.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public const float WalkSpeed = 2.4f;
        public const float SprintSpeed = 4.2f;
        public const float CrouchSpeed = 1.4f;
        public const float SmoothTime = 0.12f;
        public const float StandHeight = 1.75f;
        public const float CrouchHeight = 1.15f;

        /// <summary>
        /// Longest frame movement and gravity are allowed to see. GameLoop feeds real unscaled
        /// delta time, so a hitch - an additive floor load, a shader compile, a save write -
        /// used to arrive here as one enormous step and push the capsule straight through the
        /// floor slab. Simulating a slow frame as a slow frame is the wrong trade for a game
        /// with no jump: losing a little movement during a stall is invisible, falling out of
        /// the building is not.
        /// </summary>
        const float MaxStepSeconds = 1f / 30f;

        /// <summary>
        /// Hard ceiling on how far one Move call may travel downward. Shorter than the floor
        /// slab (BuildingSpec.FloorThickness), so a sweep can never start above a floor and
        /// end below it. Nothing in this game falls on purpose, so this costs no real motion.
        /// </summary>
        const float MaxFallStep = 0.12f;

        /// <summary>
        /// Below this far under the current zone's lowest walkable floor, the player is out of
        /// the world.
        ///
        /// Measured from the zone's floor rather than from its origin, because one zone's floor
        /// is not at its origin: the stairwell is a shaft whose slab sits a full flight below
        /// (WorldBuilder.FloorDropOf). Measuring from the origin would have read walking down
        /// the basement flight as falling through the building and teleported the player back
        /// to the top of it - every time.
        /// </summary>
        const float FallRecoveryDepth = 2.5f;

        [SerializeField] Camera _camera;
        [SerializeField] Transform _cameraPivot;
        [SerializeField] Light _flashlight;

        CharacterController _controller;
        InteractionRaycaster _raycaster;
        InputService _input;

        Vector3 _velocity;
        Vector3 _smoothedVelocity;
        Vector3 _smoothDamp;
        float _pitch;
        float _yaw;
        float _bobPhase;
        bool _crouching;
        bool _controlEnabled = true;

        bool _movementLocked;

        /// <summary>Where the roof is pulling, and until when (v2.1 spec 23 A05).</summary>
        Vector3 _driftTarget;
        int _driftEndsAtGameSecond = -1;

        public Camera Camera { get { return _camera; } }
        public bool FlashlightOn { get { return _flashlight != null && _flashlight.enabled; } }
        public InteractionRaycaster Raycaster { get { return _raycaster; } }
        public bool IsCrouching { get { return _crouching; } }

        /// <summary>
        /// True while the caretaker is actually covering ground. Read by the noise layer, so
        /// it has to mean "feet on concrete" rather than "a key is held" - a player pushed
        /// against a wall is holding W and is not making a sound.
        /// </summary>
        public bool IsMoving
        {
            get
            {
                var flat = new Vector3(_smoothedVelocity.x, 0f, _smoothedVelocity.z);
                return flat.sqrMagnitude > 0.06f;
            }
        }

        /// <summary>True while moving at sprint pace, which is the loudest the game gets.</summary>
        public bool IsSprinting
        {
            get
            {
                if (_crouching || !IsMoving) return false;
                var flat = new Vector3(_smoothedVelocity.x, 0f, _smoothedVelocity.z);
                return flat.magnitude > (WalkSpeed + SprintSpeed) * 0.5f;
            }
        }

        /// <summary>
        /// Freezes movement while leaving look and interaction alive. Used by hiding spots so
        /// the player can still look around and press the key that gets them out again.
        /// </summary>
        public void SetMovementLocked(bool locked)
        {
            _movementLocked = locked;
            if (!locked) return;

            _smoothedVelocity = Vector3.zero;
            _velocity = Vector3.zero;
        }

        public void Bind(InputService input, InteractionRaycaster raycaster)
        {
            _input = input;
            _raycaster = raycaster;
        }

        /// <summary>
        /// The caretaker, for code that has to know where they are without being handed a
        /// reference (v2.1 spec 22).
        ///
        /// The anomaly props need this: spec 22 judges M02 on whether the player came within
        /// four metres, M18 on five, and M04 on whether they turned round while cleaning.
        /// None of those is an interaction, so none of them arrives with a PlayerContext, and
        /// a prop built into a streamed scene has nobody to hand it a reference.
        ///
        /// A static set from Awake rather than a lookup: GameObject.Find is banned (GDD 21.2),
        /// and there is exactly one player.
        /// </summary>
        public static PlayerController Active { get; private set; }

        void Awake()
        {
            Active = this;
            _controller = GetComponent<CharacterController>();
            _controller.height = StandHeight;
            _controller.center = new Vector3(0f, StandHeight * 0.5f, 0f);
            _controller.radius = 0.3f;

            if (_camera == null) _camera = GetComponentInChildren<Camera>();
            if (_cameraPivot == null && _camera != null) _cameraPivot = _camera.transform;

            _yaw = transform.eulerAngles.y;
        }

        void Start()
        {
            ApplySettings(ServiceHub.Settings.Current);
            ServiceHub.Settings.OnApplied += ApplySettings;
        }

        void OnEnable() { EventBus.Subscribe<PlayerDriftEvent>(HandleDrift); }
        void OnDisable() { EventBus.Unsubscribe<PlayerDriftEvent>(HandleDrift); }

        void HandleDrift(PlayerDriftEvent evt)
        {
            _driftTarget = evt.Target;
            _driftEndsAtGameSecond = evt.EndsAtGameSecond;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (ServiceHub.Settings != null) ServiceHub.Settings.OnApplied -= ApplySettings;
        }

        void ApplySettings(GameSettings settings)
        {
            if (_camera != null) _camera.fieldOfView = settings.fieldOfView;
        }

        public void SetControlEnabled(bool enabled)
        {
            _controlEnabled = enabled;
            if (!enabled) _velocity = Vector3.zero;
        }

        public void Tick(float deltaTime)
        {
            if (_input == null) return;

            float step = Mathf.Min(deltaTime, MaxStepSeconds);

            if (_controlEnabled)
            {
                Look(step);
                Move(step);
                HandleCrouch();
                HandleFlashlight();
            }

            // Runs whether or not control is enabled: the PC and conversations freeze movement,
            // so a player who is already through the floor when one opens would otherwise have
            // no way back. This is the last line of defence, not the fix.
            CheckFallRecovery();

            ScanInteraction(step);
        }

        /// <summary>
        /// Puts the player back on the current zone's spawn if they have ended up under it.
        /// Greybox floors are 0.5m slabs with open sides, so any escape at all is a soft-lock:
        /// there is nothing to land on and no way to walk back up (GDD 21 - no dead ends).
        /// </summary>
        void CheckFallRecovery()
        {
            var zoneId = ServiceHub.Player.CurrentZone;
            if (string.IsNullOrEmpty(zoneId)) return;

            var root = ZoneRegistry.Find(zoneId);
            if (root == null) return;

            float floorY = root.position.y - WorldBuilder.FloorDropOf(zoneId);
            if (transform.position.y > floorY - FallRecoveryDepth) return;

            var spawn = ZoneRegistry.FindSpawn(zoneId);
            if (spawn == null) return;

            Teleport(spawn.position, transform.rotation);
            Log.Warn("Player", "fell out of " + zoneId + "; returned to its spawn");
        }

        void Look(float deltaTime)
        {
            var settings = ServiceHub.Settings.Current;
            var look = _input.LookValue * (settings.mouseSensitivity * 0.08f);

            _yaw += look.x;
            _pitch -= settings.invertY ? -look.y : look.y;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_cameraPivot != null) _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void Move(float deltaTime)
        {
            var move = _movementLocked ? Vector2.zero : _input.MoveValue;
            var desired = transform.right * move.x + transform.forward * move.y;
            if (desired.sqrMagnitude > 1f) desired.Normalize();

            desired = ApplyDrift(desired);

            float speed = _crouching ? CrouchSpeed : (_input.SprintHeld && !_crouching ? SprintSpeed : WalkSpeed);
            if (!_crouching && _input.SprintHeld) speed *= ToolDebtSprintFactor;

            var target = desired * speed;

            _smoothedVelocity = Vector3.SmoothDamp(_smoothedVelocity, target, ref _smoothDamp, SmoothTime, 100f, deltaTime);

            // Simple gravity: no jumping in this game (GDD 8.1 has no jump binding).
            _velocity.x = _smoothedVelocity.x;
            _velocity.z = _smoothedVelocity.z;
            _velocity.y = _controller.isGrounded ? -1f : _velocity.y - 9.81f * deltaTime;

            var motion = _velocity * deltaTime;
            motion.y = Mathf.Clamp(motion.y, -MaxFallStep, MaxFallStep);
            _controller.Move(motion);

            ApplyHeadBob(_smoothedVelocity.magnitude, speed, deltaTime);
        }

        /// <summary>
        /// What an unreturned tool costs to run with (v2.1 spec 23 A04).
        ///
        /// Ten per cent of sprint speed per debt, and never more than thirty - the spec caps it
        /// deliberately, because the debt is meant to make the last night heavier rather than
        /// to make crossing the building impossible. Walking is untouched: a caretaker who
        /// cannot run is still a caretaker who can get where they are going.
        /// </summary>
        static float ToolDebtSprintFactor
        {
            get
            {
                var risk = ServiceHub.Risk;
                if (risk == null) return 1f;

                int debt = risk.ToolDebt;
                if (debt <= 0) return 1f;
                return 1f - 0.1f * Mathf.Min(debt, 3);
            }
        }

        /// <summary>
        /// The pull toward the parapet in the ten seconds after the photograph (spec 23 A05).
        ///
        /// Deliberately weak, and deliberately additive to whatever the player is already
        /// doing: spec 23 says the caretaker who keeps pushing the other way recovers
        /// normally and that nothing forces a fall, so this can never be more than a lean.
        /// Standing still is also a way out - the pull moves the input, and no input is
        /// nothing to move.
        /// </summary>
        Vector3 ApplyDrift(Vector3 desired)
        {
            if (_driftEndsAtGameSecond < 0) return desired;

            var clock = ServiceHub.Clock;
            if (clock == null || clock.GameSecond >= _driftEndsAtGameSecond)
            {
                _driftEndsAtGameSecond = -1;
                return desired;
            }

            if (desired.sqrMagnitude < 0.0001f) return desired;

            var toward = _driftTarget - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.01f) return desired;

            var biased = desired + toward.normalized * DriftStrength;
            return biased.sqrMagnitude > 1f ? biased.normalized : biased;
        }

        /// <summary>How far the parapet can bend one step. A lean, not a shove.</summary>
        const float DriftStrength = 0.35f;

        /// <summary>Head bob multiplier from tool debt (spec 23 A04), capped with the speed loss.</summary>
        static float ToolDebtTremorFactor
        {
            get
            {
                var risk = ServiceHub.Risk;
                if (risk == null) return 1f;

                int debt = risk.ToolDebt;
                return debt <= 0 ? 1f : 1f + 0.05f * Mathf.Min(debt, 3);
            }
        }

        void ApplyHeadBob(float currentSpeed, float maxSpeed, float deltaTime)
        {
            if (_cameraPivot == null) return;

            var settings = ServiceHub.Settings.Current;
            // Spec 23 A04 gives an unreturned tool a five per cent hand tremor per debt. It
            // rides the existing bob so that the accessibility switch still turns it off:
            // reduce motion zeroes the amount and no debt can put it back.
            float amount = settings.reduceMotion ? 0f : settings.headBobAmount * ToolDebtTremorFactor;

            float baseHeight = (_crouching ? CrouchHeight : StandHeight) - 0.12f;
            if (amount <= 0.001f || currentSpeed < 0.05f)
            {
                var rest = _cameraPivot.localPosition;
                _cameraPivot.localPosition = Vector3.Lerp(rest, new Vector3(0f, baseHeight, 0f), deltaTime * 8f);
                return;
            }

            _bobPhase += deltaTime * (currentSpeed / Mathf.Max(0.01f, maxSpeed)) * 9f;
            float vertical = Mathf.Sin(_bobPhase) * 0.035f * amount;
            float horizontal = Mathf.Cos(_bobPhase * 0.5f) * 0.02f * amount;
            _cameraPivot.localPosition = new Vector3(horizontal, baseHeight + vertical, 0f);
        }

        void HandleCrouch()
        {
            if (_movementLocked || !_input.Crouch.WasPressedThisFrame()) return;

            _crouching = !_crouching;
            _controller.height = _crouching ? CrouchHeight : StandHeight;
            _controller.center = new Vector3(0f, _controller.height * 0.5f, 0f);
        }

        void HandleFlashlight()
        {
            if (_flashlight == null || !_input.Flashlight.WasPressedThisFrame()) return;
            _flashlight.enabled = !_flashlight.enabled;
        }

        void ScanInteraction(float deltaTime)
        {
            if (_raycaster == null) return;

            var context = new PlayerContext
            {
                Transform = transform,
                ZoneId = ServiceHub.Player.CurrentZone,
                Crouching = _crouching
            };

            _raycaster.Scan(context);

            if (!_controlEnabled) return;
            _raycaster.Tick(_input.Interact.WasPressedThisFrame(), _input.Interact.IsPressed(), deltaTime, context);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;

            _yaw = rotation.eulerAngles.y;
            _velocity = Vector3.zero;
            _smoothedVelocity = Vector3.zero;
            _smoothDamp = Vector3.zero;

            // Zone spawns sit 0.1m above their floor, so the frame after a transition always
            // began airborne - and that is exactly the frame a zone load or an autosave stalls.
            // Settling onto the floor now means isGrounded is already true when Move next runs.
            _controller.Move(Vector3.down * 0.2f);
        }

        public void SetFlashlight(bool on)
        {
            if (_flashlight != null) _flashlight.enabled = on;
        }

        public Save.PlayerSaveEntry Capture()
        {
            var position = transform.position;
            return new Save.PlayerSaveEntry
            {
                zoneId = ServiceHub.Player.CurrentZone,
                posX = position.x, posY = position.y, posZ = position.z,
                yaw = _yaw, pitch = _pitch,
                flashlightOn = FlashlightOn
            };
        }

        public void Restore(Save.PlayerSaveEntry entry)
        {
            if (entry == null) return;

            Teleport(new Vector3(entry.posX, entry.posY, entry.posZ), Quaternion.Euler(0f, entry.yaw, 0f));
            _pitch = entry.pitch;
            if (_cameraPivot != null) _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            SetFlashlight(entry.flashlightOn);
            ServiceHub.Player.EnterZone(entry.zoneId);
        }

        public void AssignReferences(Camera cam, Transform pivot, Light flashlight)
        {
            _camera = cam;
            _cameraPivot = pivot;
            _flashlight = flashlight;
        }
    }
}
