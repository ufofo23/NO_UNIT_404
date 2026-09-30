using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using NO404.Core;

namespace NO404.Net
{
    /// <summary>
    /// The Netcode half of a shift (v3.0 34, 46).
    ///
    /// This owns the NetworkManager and the connection roster. It deliberately does NOT open
    /// or join rooms: <see cref="MultiplayerSessionService"/> does that, and the Multiplayer
    /// Services SDK is what calls StartHost and StartClient once a Relay session exists. The
    /// split is the point - swapping Relay for a Steam lobby later replaces that service and
    /// leaves everything here, and everything above here, untouched.
    ///
    /// The shape underneath is the one v3.0 46.1 asks for and the only one this codebase can
    /// carry without being rewritten: the host simulates and everyone else watches and acts.
    /// Every service in <see cref="ServiceHub"/> is a static singleton holding authoritative
    /// state, and two copies running side by side would drift apart inside a minute. So a
    /// client runs a body, a camera and a set of screens fed from the host, and everything it
    /// does to the building goes over the wire as a request.
    ///
    /// Not here yet, on purpose: host migration and reconnect. A session is one sitting.
    /// </summary>
    public sealed class NetSession : MonoBehaviour
    {
        /// <summary>Where the generated prefabs live. Kept in step with NetPrefabSetup.</summary>
        public const string ShiftPrefabResource = "NO404/Net/ShiftObject";
        public const string PlayerPrefabResource = "NO404/Net/NetPlayer";

        public static NetSession Instance { get; private set; }

        NetworkManager _manager;
        GameObject _shiftPrefab;
        GameObject _playerPrefab;

        readonly Dictionary<ulong, string> _playerIdByClient = new Dictionary<ulong, string>();

        /// <summary>
        /// The NetworkManager this session owns. Exposed for diagnostics and for the smoke
        /// test that starts a loopback host - nothing in the game should reach for it.
        /// </summary>
        public NetworkManager Manager { get { return _manager; } }

        public bool Running { get { return _manager != null && _manager.IsListening; } }
        public bool IsHost { get { return _manager != null && _manager.IsHost; } }
        public bool IsClient { get { return _manager != null && _manager.IsClient && !_manager.IsHost; } }

        /// <summary>
        /// True when this copy owns the simulation. Single player is a machine with no session,
        /// so every "am I in charge" test in the game reads this and is right in both cases
        /// without knowing that networking exists.
        /// </summary>
        public static bool Authoritative
        {
            get { return Instance == null || !Instance.Running || Instance.IsHost; }
        }

        public int ConnectedCount
        {
            get { return _manager != null && _manager.IsListening ? _manager.ConnectedClientsIds.Count : 1; }
        }

        /// <summary>True once the prefabs the session needs are present and registered.</summary>
        public bool PrefabsReady { get { return _shiftPrefab != null && _playerPrefab != null; } }

        public event Action OnSessionChanged;

        // -----------------------------------------------------------------
        // setup
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates the session object at the root of the scene, deliberately NOT under the
        /// bootstrap root everything else in this game hangs from.
        ///
        /// Netcode refuses to run a NetworkManager nested under another GameObject and says so
        /// at Awake - so parenting it, which is what every other service here does, silently
        /// left StartHost returning false and co-op completely dead. It survived a full
        /// compile, a content validation pass and every unit test, because none of those start
        /// a host. A PlayMode smoke test is what found it, and there is one for that reason.
        /// </summary>
        public static NetSession Create()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("[NO404 Net]");
            DontDestroyOnLoad(go);

            Instance = go.AddComponent<NetSession>();
            Instance.Build();
            return Instance;
        }

        void Build()
        {
            _manager = gameObject.AddComponent<NetworkManager>();
            var transport = gameObject.AddComponent<UnityTransport>();

            _manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = false,

                // Both machines build the same world procedurally from the same content, and
                // the client follows the host's night index rather than a loaded scene. There
                // is no second scene to hand over, so Netcode's scene management would only
                // add a synchronisation step that can fail (v3.0 item 12).
                EnableSceneManagement = false,
                TickRate = 30
            };

            _shiftPrefab = LoadPrefab(ShiftPrefabResource);
            _playerPrefab = LoadPrefab(PlayerPrefabResource);

            if (_playerPrefab != null) _manager.NetworkConfig.PlayerPrefab = _playerPrefab;
            if (_shiftPrefab != null) _manager.AddNetworkPrefab(_shiftPrefab);

            _manager.OnServerStarted += OnServerStarted;
            _manager.OnClientConnectedCallback += OnClientConnected;
            _manager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        /// <summary>
        /// Loads one of the generated prefabs.
        ///
        /// They are assets rather than objects built in code because Netcode stamps a
        /// NetworkObject's id hash at import time: a GameObject created with `new GameObject()`
        /// has a hash of zero and cannot be spawned, and the failure is silent.
        /// </summary>
        static GameObject LoadPrefab(string path)
        {
            var prefab = Resources.Load<GameObject>(path);

            if (prefab == null)
                Log.Error("Multiplayer", "missing " + path +
                                         " - run Tools > NO404 > Net > Rebuild Net Prefabs");

            return prefab;
        }

        void OnDestroy()
        {
            if (_manager == null) return;
            _manager.OnServerStarted -= OnServerStarted;
            _manager.OnClientConnectedCallback -= OnClientConnected;
            _manager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // -----------------------------------------------------------------
        // lifecycle
        // -----------------------------------------------------------------

        /// <summary>
        /// The host's shared state object, spawned the moment Netcode starts listening.
        ///
        /// Hung off OnServerStarted rather than off session creation, because the SDK is what
        /// starts the host and there is no earlier moment at which spawning is legal.
        /// </summary>
        void OnServerStarted()
        {
            Log.Info("Multiplayer", "Host created");
            SpawnSharedShift();
            Raise();
        }

        void SpawnSharedShift()
        {
            if (_shiftPrefab == null || NetShift.Instance != null) return;

            var go = Instantiate(_shiftPrefab);
            go.name = "[NO404 Shift]";
            go.transform.SetParent(null, false);

            var netObject = go.GetComponent<NetworkObject>();
            if (netObject == null)
            {
                Log.Error("Multiplayer", "the shift prefab has no NetworkObject");
                Destroy(go);
                return;
            }

            netObject.Spawn();
        }

        /// <summary>
        /// Stops Netcode without touching the session.
        ///
        /// Called when a session ends: the SDK starts the NetworkManager but does not always
        /// stop it, and one left listening refuses the next session with an error that names
        /// nothing useful.
        /// </summary>
        public void ShutdownTransport()
        {
            if (_manager != null && _manager.IsListening) _manager.Shutdown();

            _playerIdByClient.Clear();
            ServiceHub.Presence.Reset();
            Raise();
        }

        // -----------------------------------------------------------------
        // roster
        // -----------------------------------------------------------------

        /// <summary>
        /// A stable, readable name for a connection.
        ///
        /// Derived from the Netcode client id rather than from a join order counter, so it
        /// survives people leaving and rejoining and means the same thing on every machine.
        /// v3.0 34.3 wants the campaign owner identifiable in the save; "P1" is also far more
        /// use in a log than a 64-bit id.
        /// </summary>
        public static string PlayerIdFor(ulong clientId)
        {
            return "P" + (clientId + 1);
        }

        void OnClientConnected(ulong clientId)
        {
            var playerId = PlayerIdFor(clientId);
            _playerIdByClient[clientId] = playerId;

            Log.Info("Multiplayer", "Client connected");
            Log.Info("Multiplayer", "ClientId: " + clientId + " (" + playerId + ")");

            if (_manager != null && clientId == _manager.LocalClientId)
                ServiceHub.Presence.SetLocalPlayerId(playerId);
            else
                ServiceHub.Presence.Report(playerId, ZoneIds.Office);

            // Whoever just arrived needs the whole shift, not the next thing that changes in it.
            if (IsHost) NetShift.ForceMirror();

            Raise();
        }

        void OnClientDisconnected(ulong clientId)
        {
            string playerId;
            if (_playerIdByClient.TryGetValue(clientId, out playerId))
            {
                ServiceHub.Presence.Remove(playerId);
                _playerIdByClient.Remove(clientId);
                Log.Info("Multiplayer", "Client disconnected: " + playerId);
            }

            // The host going away ends the sitting. There is no migration yet, and pretending
            // otherwise would leave a client running a night nobody is simulating.
            if (IsClient && clientId == NetworkManager.ServerClientId)
            {
                Log.Info("Multiplayer", "the host disconnected");
                ShutdownTransport();
                return;
            }

            Raise();
        }

        void Update()
        {
            // Where a background session callback becomes a main-thread action.
            MultiplayerSessionService.PumpMainThread();
        }

        void Raise() { var cb = OnSessionChanged; if (cb != null) cb(); }
    }
}
