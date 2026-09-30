using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using NO404.Core;

namespace NO404.Net
{
    /// <summary>
    /// One caretaker, as an object every machine agrees exists.
    ///
    /// Netcode spawns one of these per connection and gives it to that connection. The owner's
    /// copy is invisible and follows the local <see cref="PlayerController"/>; everyone else's
    /// copy is a body and a torch in a corridor. The split is what stops two people driving
    /// one character, and it is why the camera, the audio listener, the input and the
    /// interaction raycaster all stay where they already are - on the local player object that
    /// singleplayer has always used - rather than being moved onto a network prefab.
    ///
    /// That is a deliberate reading of "owner-only components". The alternative, putting the
    /// real player rig inside this prefab, would mean rebuilding a rig the game currently
    /// creates in code and would put a network dependency in the middle of movement. This way
    /// singleplayer runs down exactly the same path it did before, with no NetworkObject
    /// anywhere near it.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetPlayer : NetworkBehaviour
    {
        /// <summary>How often the owner tells everyone which zone they are standing in.</summary>
        public const float ZoneReportSeconds = 0.4f;

        readonly NetworkVariable<FixedString32Bytes> _zone =
            new NetworkVariable<FixedString32Bytes>(default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        /// <summary>
        /// This caretaker's torch and camera wall, packed into a byte (GDD 15.5).
        ///
        /// Bit 2 is the torch, bits 0-1 are the screens - 0 none, 1 the grid, 2 one channel
        /// full-screen. It rides at the same slow rate as the zone because the reserve is
        /// charged per game minute: nothing here needs to be accurate to the frame, only
        /// accurate to the bill.
        /// </summary>
        readonly NetworkVariable<byte> _load =
            new NetworkVariable<byte>(0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        GameObject _body;
        float _nextReport;
        float _nextReassert;
        string _lastReported;

        public string PlayerId { get { return NetSession.PlayerIdFor(OwnerClientId); } }

        public override void OnNetworkSpawn()
        {
            _zone.OnValueChanged += OnZoneChanged;
            _load.OnValueChanged += OnLoadChanged;

            if (IsOwner)
            {
                // You are already looking through this character's eyes. Drawing a second one
                // around your own camera is the classic first-day multiplayer bug.
                Log.Info("Multiplayer", "Player spawned (local, " + PlayerId + ")");
                return;
            }

            _body = BuildBody(transform);
            ServiceHub.Presence.Report(PlayerId, ZoneOrOffice(_zone.Value));
            ServiceHub.Presence.ReportLoad(PlayerId, (_load.Value & 4) != 0, _load.Value & 3);
            Log.Info("Multiplayer", "Player spawned (remote, " + PlayerId + ")");
        }

        public override void OnNetworkDespawn()
        {
            _zone.OnValueChanged -= OnZoneChanged;
            _load.OnValueChanged -= OnLoadChanged;

            if (!IsOwner) ServiceHub.Presence.Remove(PlayerId);
            if (_body != null) Destroy(_body);
        }

        void OnZoneChanged(FixedString32Bytes previous, FixedString32Bytes current)
        {
            if (IsOwner) return;

            // Where a colleague is standing is a fact the building's own rules read: a tell at
            // the lobby glass is visible because somebody is at the glass, whoever that is.
            ServiceHub.Presence.Report(PlayerId, ZoneOrOffice(current));
        }

        void OnLoadChanged(byte previous, byte current)
        {
            if (IsOwner) return;
            ServiceHub.Presence.ReportLoad(PlayerId, (current & 4) != 0, current & 3);
        }

        static string ZoneOrOffice(FixedString32Bytes zone)
        {
            var value = zone.ToString();
            return string.IsNullOrEmpty(value) ? ZoneIds.Office : value;
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner) { FollowLocalPlayer(); return; }

            ReassertPresence();
            SmoothRemoteBody();
        }

        /// <summary>
        /// Carries the local player's position onto the network object.
        ///
        /// The controller still owns movement; this only reports it. NetworkTransform on the
        /// same object is what actually replicates the result.
        /// </summary>
        void FollowLocalPlayer()
        {
            var local = GameLoop.Instance != null ? GameLoop.Instance.PlayerTransform : null;
            if (local == null) return;

            transform.SetPositionAndRotation(local.position, local.rotation);

            if (Time.time < _nextReport) return;
            _nextReport = Time.time + ZoneReportSeconds;

            byte load = GameLoop.Instance != null ? GameLoop.Instance.LocalPowerLoad : (byte)0;
            if (_load.Value != load) _load.Value = load;

            var zoneId = ServiceHub.Player.CurrentZone;
            if (zoneId == _lastReported) return;

            _lastReported = zoneId;
            _zone.Value = zoneId ?? ZoneIds.Office;
        }

        /// <summary>
        /// NetworkTransform already interpolates position. This only keeps the body upright:
        /// the pitch of somebody's head is not worth a byte, and a capsule tipped forward
        /// reads as a bug rather than as a person looking down.
        /// </summary>
        /// <summary>
        /// Says again where this caretaker is and what they are drawing.
        ///
        /// Both facts ride on NetworkVariables and therefore only arrive when they change,
        /// which is correct right up until something clears the roster underneath them -
        /// <c>ServiceHub.ResetPlaythrough</c> calls <c>Presence.Reset()</c>, and the host
        /// pressing New Game with two people already connected is exactly that. The colleagues
        /// then simply stopped existing as far as the building's rules were concerned, until
        /// they next happened to walk through a door.
        ///
        /// So the roster is re-asserted rather than only updated. It costs a dictionary write
        /// twice a second per remote caretaker and it makes the whole thing self-healing.
        /// </summary>
        void ReassertPresence()
        {
            if (Time.time < _nextReassert) return;
            _nextReassert = Time.time + ZoneReportSeconds;

            ServiceHub.Presence.Report(PlayerId, ZoneOrOffice(_zone.Value));
            ServiceHub.Presence.ReportLoad(PlayerId, (_load.Value & 4) != 0, _load.Value & 3);
        }

        void SmoothRemoteBody()
        {
            if (_body == null) return;

            var yaw = transform.eulerAngles.y;
            _body.transform.rotation = Quaternion.Slerp(_body.transform.rotation,
                                                        Quaternion.Euler(0f, yaw, 0f),
                                                        12f * Time.deltaTime);
        }

        /// <summary>
        /// What a colleague looks like down a dark corridor: a shape and a torch. There is no
        /// character model in this project, and a capsule with a light on it reads correctly
        /// at every distance the game actually shows anybody at.
        ///
        /// The shape itself moved to <see cref="DummyBody"/> once visitors needed the same
        /// one. The white cap is what says this is a caretaker and not somebody the door let
        /// in - see DummyBody for why that lives on the head rather than on the body.
        /// </summary>
        static GameObject BuildBody(Transform parent)
        {
            return DummyBody.Build(parent, DummyBody.CaretakerCap, torch: true, cctvOnly: false,
                                   name: "Body");
        }
    }
}
