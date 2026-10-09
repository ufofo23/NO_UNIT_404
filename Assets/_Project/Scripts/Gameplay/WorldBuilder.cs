using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NO404.Core;
using NO404.Interaction;

namespace NO404.Gameplay
{
    /// <summary>
    /// Procedural greybox of every P0 space (GDD 3.1 / 23.1), built from primitives at load
    /// time. This is scaffolding, not art: it exists so the systems, cameras and navigation
    /// distances are playable and measurable before the art pass replaces it with real meshes.
    ///
    /// Every dimension comes from BuildingSpec (GDD 17.4) or from the per-space sizes in
    /// GDD 17.5 - an 8x6 office, a 25x18 parking section, 18 square metres for unit 404 - so
    /// replacing the blockout with real modules is a swap rather than a re-layout.
    ///
    /// Vertical circulation is modelled the way the design needs it: the elevator car is a
    /// real zone you ride in (so its indicator can lie about the floor), and the stairwell is
    /// a parallel route that still works when the elevator loses power on night 5.
    ///
    /// Zones are split across additive scenes by ZoneGroups. A builder only creates the zones
    /// its own scene owns, so the same class serves every streamed scene.
    /// </summary>
    public sealed partial class WorldBuilder : MonoBehaviour
    {
        struct ZoneSpec
        {
            public string Id;
            public Vector3 Origin;
            public Vector2 Size;    // x = width, y = depth, both on the 0.5m grid
            public float Height;
            public Color Floor;
            public Color Wall;
            public float LightIntensity;

            /// <summary>
            /// How far below the zone's own y the walkable floor actually sits.
            ///
            /// Zero everywhere except the stairwell, which is a shaft rather than a room: its
            /// slab is at the basement landing and everything else in it is a platform above
            /// that. PlayerController reads this so descending a real flight of stairs is not
            /// mistaken for falling out of the world.
            /// </summary>
            public float FloorDrop;
        }

        const float Corridor = BuildingSpec.CorridorWidth;
        const float Storey = BuildingSpec.WallHeight;
        const float DoorY = BuildingSpec.UnitDoorHeight * 0.5f;

        static readonly ZoneSpec[] Zones =
        {
            // GDD 17.5.1: the office is about 8m x 6m.
            Zone(ZoneIds.Office,         new Vector3(0f, 0f, 0f),     new Vector2(8f, 6f),     Storey, 0.24f, 0.30f, 2.2f),
            Zone(ZoneIds.Lobby,          new Vector3(0f, 0f, 30f),    new Vector2(12f, 8f),    Storey, 0.28f, 0.34f, 1.5f),
            // A six-person car: 1.5m square, lower ceiling than a room.
            Zone(ZoneIds.Elevator,       new Vector3(30f, 0f, 30f),   new Vector2(1.5f, 1.5f), 2.4f,   0.30f, 0.36f, 1.1f),
            // GDD 17.5.8. Not a room with plates on the wall - a shaft with real flights.
            // Seven landings, 2.8m apart, B1 to 6F (v5.1 3.1), which is what makes "up" and
            // "down" things the player does rather than reads.
            Zone(ZoneIds.Stairwell,      new Vector3(0f, 0f, 50f),    new Vector2(5f, 9f),     ShaftHeight, 0.20f, 0.26f, 0.9f, StairShaft.Drop),
            // GDD 17.5.3: build a 25m x 18m section, not the whole level.
            // B1 (v5.1 3.1): parking, recycling, and - beside them on the same level - the
            // electrical room, the pump and tank plant, and the records room.
            Zone(ZoneIds.Parking,        new Vector3(0f, -12f, 0f),   new Vector2(25f, 18f),   Storey, 0.18f, 0.22f, 1.2f),
            Zone(ZoneIds.RecyclingYard,  new Vector3(0f, -12f, 30f),  new Vector2(10f, 8f),    Storey, 0.17f, 0.21f, 0.9f),
            Zone(ZoneIds.Machinery,      new Vector3(40f, -12f, 0f),  new Vector2(10f, 8f),    Storey, 0.17f, 0.21f, 1.0f),
            Zone(ZoneIds.PumpRoom,       new Vector3(40f, -12f, 14f), new Vector2(8f, 6f),     Storey, 0.16f, 0.20f, 0.9f),
            Zone(ZoneIds.PipeRoom,       new Vector3(40f, -12f, 26f), new Vector2(10f, 4f),    2.3f,   0.15f, 0.19f, 0.7f),
            Zone(ZoneIds.Toolroom,       new Vector3(40f, -12f, 36f), new Vector2(6f, 5f),     Storey, 0.16f, 0.20f, 0.9f),
            Zone(ZoneIds.Archive,        new Vector3(40f, -12f, 46f), new Vector2(6f, 5f),     Storey, 0.19f, 0.24f, 1.0f),

            // 1F (spec 25): the hub the building means to feel safe.
            Zone(ZoneIds.Laundry,        new Vector3(30f, 0f, 0f),    new Vector2(6f, 5f),     Storey, 0.26f, 0.32f, 1.6f),
            Zone(ZoneIds.ConvenienceStore, new Vector3(30f, 0f, 10f), new Vector2(7f, 5f),     Storey, 0.27f, 0.33f, 1.8f),
            Zone(ZoneIds.Playground,     new Vector3(30f, 0f, 22f),   new Vector2(14f, 10f),   6f,     0.20f, 0.24f, 0.6f),

            // 2F (spec 25): shared resident facilities.
            Zone(ZoneIds.Floor02,        new Vector3(90f, 0f, 0f),    CorridorSize(),          Storey, 0.23f, 0.31f, 1.3f),
            Zone(ZoneIds.Lounge,         new Vector3(90f, 0f, 12f),   new Vector2(8f, 6f),     Storey, 0.24f, 0.30f, 1.2f),
            Zone(ZoneIds.FitnessRoom,    new Vector3(90f, 0f, 22f),   new Vector2(10f, 7f),    Storey, 0.23f, 0.29f, 1.4f),
            Zone(ZoneIds.Terrace,        new Vector3(90f, 0f, 34f),   new Vector2(12f, 8f),    6f,     0.21f, 0.25f, 0.7f),

            Zone(ZoneIds.Floor04,        new Vector3(60f, 0f, 0f),    CorridorSize(),          Storey, 0.22f, 0.30f, 1.3f),
            // GDD 17.5.4: unpainted concrete, cramped, between floors.
            // 1.0m wide: 0.8m of clear floor, noticeably tighter than the 1.55m corridor.
            Zone(ZoneIds.ServicePassage, new Vector3(60f, 0f, 12f),   new Vector2(12f, 1f),    2.3f,   0.14f, 0.17f, 0.7f),
            Zone(ZoneIds.Floor03,        new Vector3(60f, 0f, 24f),   CorridorSize(),          Storey, 0.22f, 0.31f, 1.3f),
            Zone(ZoneIds.Floor05,        new Vector3(90f, 0f, 46f),   CorridorSize(),          Storey, 0.22f, 0.30f, 1.2f),
            Zone(ZoneIds.Floor06,        new Vector3(90f, 0f, 58f),   CorridorSize(),          Storey, 0.22f, 0.30f, 1.2f),
            // v5.1 3.1: no roof and no thirteenth floor are built. Their ids survive only so
            // old saves and the retired night-response content still compile against them.
            // GDD 17.5.7: 18 square metres.
            Zone(ZoneIds.Unit404,        new Vector3(60f, 0f, 18f),   new Vector2(4.5f, 4f),   2.3f,   0.14f, 0.18f, 0.6f)
        };

        static Vector2 CorridorSize()
        {
            return new Vector2(BuildingSpec.CorridorLength, Corridor);
        }

        static ZoneSpec Zone(string id, Vector3 origin, Vector2 size, float height,
                             float floorGrey, float wallGrey, float lightIntensity,
                             float floorDrop = 0f)
        {
            return new ZoneSpec
            {
                Id = id,
                Origin = origin,
                Size = size,
                Height = height,
                Floor = new Color(floorGrey, floorGrey, floorGrey * 1.02f, 1f),
                Wall = new Color(wallGrey, wallGrey, wallGrey * 1.05f, 1f),
                LightIntensity = lightIntensity,
                FloorDrop = floorDrop
            };
        }

        /// <summary>
        /// The stairwell's landing heights, in zone-local metres (GDD 17.5.8).
        ///
        /// Every one of them is one flight from its neighbour, so the shaft can be built and
        /// read from this table alone: the door to a floor is on the landing at that height,
        /// and the flight between two landings is the thing the player walks.
        /// </summary>
        public static class StairShaft
        {
            public const float Basement = -BuildingSpec.FlightRise;          // B1,  -2.8
            public const float Ground = 0f;                                  // 1F
            public const float Floor02 = BuildingSpec.FlightRise;            // +2.8
            public const float Floor03 = BuildingSpec.FlightRise * 2f;       // +5.6
            public const float Floor04 = BuildingSpec.FlightRise * 3f;       // +8.4
            public const float Floor05 = BuildingSpec.FlightRise * 4f;       // +11.2
            public const float Floor06 = BuildingSpec.FlightRise * 5f;       // +14.0

            /// <summary>How far the slab sits below the zone origin. B1 is one flight down.</summary>
            public const float Drop = BuildingSpec.FlightRise;

            /// <summary>Ceiling height above the zone origin: the top landing plus a storey.</summary>
            public const float Ceiling = Floor06 + BuildingSpec.WallHeight;  // 16.6

            /// <summary>
            /// Height of a landing, from the floor position in FloorPlan.Order.
            ///
            /// Derived rather than looked up, because a table of nine heights beside a table
            /// of nine floors is two things that have to be kept in agreement. Spec 0.8.2
            /// needs the landing the player entered from to be the landing they are standing
            /// on, and that only holds while there is one definition of which landings exist.
            /// </summary>
            public static float HeightOf(string floorId)
            {
                int index = FloorPlan.IndexOf(floorId);
                if (index < 0) return Ground;
                return (index - FloorPlan.IndexOf(FloorPlan.F1)) * BuildingSpec.FlightRise;
            }

            /// <summary>
            /// Which end of the shaft a landing sits at. Consecutive landings alternate, which
            /// is what makes the flights a switchback rather than a ladder.
            /// </summary>
            public static bool IsNorth(string floorId)
            {
                int offset = FloorPlan.IndexOf(floorId) - FloorPlan.IndexOf(FloorPlan.F1);
                return ((offset % 2) + 2) % 2 == 0;
            }

            public static float LandingZOf(string floorId)
            {
                return IsNorth(floorId) ? NorthLandingZ : SouthLandingZ;
            }

            /// <summary>Where a flight touching this landing starts or ends along z.</summary>
            public static float FlightEndOf(string floorId)
            {
                return IsNorth(floorId) ? FlightHalfRun : -FlightHalfRun;
            }

            /// <summary>Landings alternate ends of the shaft; flights run between them.</summary>
            public const float SouthLandingZ = -BuildingSpec.LandingSpan * 0.5f;   // -3.5
            public const float NorthLandingZ = BuildingSpec.LandingSpan * 0.5f;    // +3.5
            public const float LandingHalfDepth = BuildingSpec.LandingDepth * 0.5f;

            /// <summary>
            /// The two parallel runs a switchback stairwell is made of.
            ///
            /// Consecutive flights alternate between them, which is not decoration: three
            /// flights stacked in one lane put the underside of the flight above only 1.8m
            /// over the treads of the one below, and the player walks the last third of every
            /// flight through the staircase overhead. Alternating puts two full flights - 5.6m
            /// - between anything sharing a lane.
            /// </summary>
            public const float LaneAX = 1.35f;
            public const float LaneBX = -1.35f;

            /// <summary>Where a flight starts and stops along z.</summary>
            public const float FlightHalfRun = BuildingSpec.FlightRun * 0.5f; // 2.32
        }

        const float ShaftHeight = StairShaft.Ceiling;

        /// <summary>
        /// How far below its origin a zone's walkable floor reaches (GDD 17.5.8).
        /// PlayerController's fall recovery measures from this, not from the origin.
        /// </summary>
        public static float FloorDropOf(string zoneId)
        {
            for (int i = 0; i < Zones.Length; i++) if (Zones[i].Id == zoneId) return Zones[i].FloorDrop;
            return 0f;
        }

        public static Vector2 SizeOf(string zoneId)
        {
            for (int i = 0; i < Zones.Length; i++) if (Zones[i].Id == zoneId) return Zones[i].Size;
            return Vector2.one;
        }

        public static float HeightOf(string zoneId)
        {
            for (int i = 0; i < Zones.Length; i++) if (Zones[i].Id == zoneId) return Zones[i].Height;
            return Storey;
        }

        /// <summary>Half-extents of a zone, so props are placed relative to its walls.</summary>
        static Vector2 HalfOf(string zoneId) { return SizeOf(zoneId) * 0.5f; }

        readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

        Material _defaultMaterial;
        string[] _zoneFilter;

        public ElevatorController Elevator { get; private set; }

        /// <summary>
        /// Set by whichever builder owns the office. GameLoop reads these instead of searching
        /// the scene, because the office lives in the core scene and the player is created
        /// before any streamed scene exists.
        /// </summary>
        public static TerminalInteractable OfficeTerminal { get; private set; }
        public static Transform OfficeSpawn { get; private set; }

        /// <summary>
        /// The two places the shared entrance gives other systems to point at.
        ///
        /// <see cref="LobbyDoorstep"/> is the street side of the front door - where somebody
        /// who has rung the bell is standing while the caretaker decides about them - and
        /// <see cref="LobbyVestibule"/> is just inside it, which is where anybody who is let
        /// in arrives from. Before these existed a granted visitor simply materialised in the
        /// middle of the lobby, having come through no door at all.
        /// </summary>
        public static Transform LobbyDoorstep { get; private set; }
        public static Transform LobbyVestibule { get; private set; }

        /// <summary>
        /// Where the player stands on each stairwell landing (v2.1 spec 0.8.2).
        ///
        /// A stairwell has one spawn point and nine landings, which is precisely the gap the
        /// old "every stair door comes out in the lobby" bug lived in. StairEntryDoor asks for
        /// the landing belonging to the floor it is on, so there is no default to fall back to.
        /// </summary>
        static readonly Dictionary<string, Transform> _landingAnchors = new Dictionary<string, Transform>();

        public static Transform LandingAnchor(string floorId)
        {
            if (string.IsNullOrEmpty(floorId)) return null;
            Transform anchor;
            return _landingAnchors.TryGetValue(floorId, out anchor) ? anchor : null;
        }

        public static WorldBuilder Create(Transform parent)
        {
            var go = new GameObject("World");
            go.transform.SetParent(parent, false);
            return go.AddComponent<WorldBuilder>();
        }

        /// <summary>Builds the listed zones. A null filter builds every zone (tests, tools).</summary>
        public void Build(string[] zoneFilter = null)
        {
            _zoneFilter = zoneFilter;
            CacheDefaultMaterial();

            for (int i = 0; i < Zones.Length; i++)
            {
                if (!Owns(Zones[i].Id)) continue;
                BuildZone(Zones[i]);
            }

            BuildOfficeInterior();
            BuildLobbyInterior();
            BuildElevatorInterior();
            BuildStairwellInterior();
            BuildParkingInterior();
            BuildArchiveInterior();
            BuildCorridors();
            BuildServicePassageInterior();
            BuildUnit404Interior();
            BuildSelectedMainProps();
            BuildSubquestProps();
            BuildDressing();
            BuildTransitions();
            BuildManualStages();

            // Brightness and streamer mode lift the floor of the lighting rather than
            // washing the image out (GDD 3.2 / 16.16).
            float lift = ServiceHub.Settings.Current.brightness;
            if (ServiceHub.Settings.Current.streamerMode) lift += 0.25f;

            RenderLook.ApplyAmbient(lift);
            RenderSettings.fog = false;

            Log.Info("World", "greybox built: " +
                              (_zoneFilter == null ? "all zones" : string.Join(", ", _zoneFilter)));
        }

        bool Owns(string zoneId)
        {
            if (_zoneFilter == null) return true;
            for (int i = 0; i < _zoneFilter.Length; i++) if (_zoneFilter[i] == zoneId) return true;
            return false;
        }

        /// <summary>
        /// The root of a zone this builder is responsible for, or null.
        ///
        /// The distinction matters because the office and the lobby never unload: they are in
        /// the core scene for the whole session (GDD 20.5). An interior builder that asked the
        /// registry alone would find them from *every* streamed scene, and so loading the
        /// fourth floor would furnish the office a second time - duplicating its props and
        /// repointing OfficeTerminal at a copy that GameLoop never wired, which is a terminal
        /// the player can walk up to and press E at for nothing.
        /// </summary>
        Transform OwnedRoot(string zoneId)
        {
            return Owns(zoneId) ? ZoneRegistry.Find(zoneId) : null;
        }

        /// <summary>Unregisters this builder's zones so a scene unload leaves no dangling ids.</summary>
        void OnDestroy()
        {
            if (_zoneFilter == null) { ZoneRegistry.Clear(); return; }
            for (int i = 0; i < _zoneFilter.Length; i++) ZoneRegistry.Unregister(_zoneFilter[i]);
        }

        void CacheDefaultMaterial()
        {
            // Was a probe primitive's sharedMaterial, which is the built-in default and which
            // URP cannot draw - the entire building rendered magenta in a build, silently,
            // because a wrong-but-present shader does not raise anything. GreyboxMaterial
            // takes it from the active pipeline instead.
            _defaultMaterial = GreyboxMaterial.Default;
        }

        Material MaterialFor(Color color)
        {
            var key = ColorUtility.ToHtmlStringRGBA(color);
            Material material;
            if (_materials.TryGetValue(key, out material)) return material;

            material = GreyboxMaterial.Tinted(color);
            material.name = "GREYBOX_" + key;

            _materials[key] = material;
            return material;
        }

        // ---- zone shells ---------------------------------------------------

        void BuildZone(ZoneSpec spec)
        {
            var root = new GameObject("ZONE_" + spec.Id).transform;
            root.SetParent(transform, false);
            root.position = spec.Origin;

            float halfWidth = spec.Size.x * 0.5f;
            float halfDepth = spec.Size.y * 0.5f;
            float t = BuildingSpec.WallThickness;

            // The slab hangs below the walking surface, so the surface is still exactly
            // y = -FloorDrop in zone space while the collider has real depth under it. Only
            // the stairwell drops; every other zone walks on its own y = 0.
            float floorT = BuildingSpec.FloorThickness;
            float floorTop = -spec.FloorDrop;
            Box(root, "Floor", new Vector3(0f, floorTop - floorT * 0.5f, 0f),
                new Vector3(spec.Size.x, floorT, spec.Size.y), spec.Floor);

            Box(root, "Ceiling", new Vector3(0f, spec.Height + t * 0.5f, 0f),
                new Vector3(spec.Size.x, t, spec.Size.y), spec.Wall * 0.8f);

            float wallSpan = spec.Height + spec.FloorDrop;
            float wallY = floorTop + wallSpan * 0.5f;
            Box(root, "Wall_N", new Vector3(0f, wallY, halfDepth), new Vector3(spec.Size.x, wallSpan, t), spec.Wall);
            Box(root, "Wall_S", new Vector3(0f, wallY, -halfDepth), new Vector3(spec.Size.x, wallSpan, t), spec.Wall);
            Box(root, "Wall_E", new Vector3(halfWidth, wallY, 0f), new Vector3(t, wallSpan, spec.Size.y), spec.Wall);
            Box(root, "Wall_W", new Vector3(-halfWidth, wallY, 0f), new Vector3(t, wallSpan, spec.Size.y), spec.Wall);

            // One practical light per zone; the real lighting pass replaces these (GDD 17.10).
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, spec.Height - 0.35f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = Mathf.Max(spec.Size.x, spec.Size.y) * 1.2f;
            // Filmic tonemapping darkens the mid-tones, so rooms lit by this one light alone
            // (unit 404, the plant rooms) need more of it than the greybox did.
            light.intensity = spec.LightIntensity * 1.6f;
            light.color = spec.Id == ZoneIds.Parking || spec.Id == ZoneIds.Archive
                ? new Color(0.85f, 0.9f, 1f)
                : new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.None;   // greybox: shadow budget is GDD 20.20

            if (IsCorridor(spec.Id)) lightGo.AddComponent<CircuitLight>().Setup(CircuitIds.CorridorLights);

            DressShell(root, spec);

            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(root, false);
            spawn.localPosition = SpawnFor(spec);

            // What a mishandled floor looks like (v2.1 spec 0.10.3). Every zone that belongs
            // to a floor carries one; the lift and the shaft belong to none, and a risk tier
            // for nowhere would have nothing to dress.
            var floorId = FloorPlan.FloorOfZone(spec.Id);
            if (!string.IsNullOrEmpty(floorId))
                root.gameObject.AddComponent<Anomalies.FloorDressing>().Setup(spec.Id, floorId);

            ZoneRegistry.Register(spec.Id, root, spawn);
        }

        /// <summary>
        /// A spawn point that is always clear of the walls. A 1.55m corridor has no room to
        /// stand off-centre, so corridors spawn on the centre line.
        /// </summary>
        static Vector3 SpawnFor(ZoneSpec spec)
        {
            // The stairwell is entered from the lobby, which arrives on the ground landing at
            // the north end of the shaft - not at the bottom of it.
            if (spec.Id == ZoneIds.Stairwell)
                return new Vector3(0f, StairShaft.Ground + 0.1f, StairShaft.NorthLandingZ);

            if (IsCorridor(spec.Id) || spec.Id == ZoneIds.Elevator) return new Vector3(0f, 0.1f, 0f);

            float halfDepth = spec.Size.y * 0.5f;
            float inset = Mathf.Min(1.5f, spec.Size.y * 0.3f);
            return new Vector3(0f, 0.1f, -halfDepth + inset);
        }

        static bool IsCorridor(string zoneId)
        {
            return zoneId == ZoneIds.Floor02 || zoneId == ZoneIds.Floor03 || zoneId == ZoneIds.Floor04
                || zoneId == ZoneIds.Floor05 || zoneId == ZoneIds.Floor06 || zoneId == ZoneIds.ServicePassage;
        }

        /// <summary>
        /// One solid thing in a room - a wall, a bench, a swing set, a locker.
        ///
        /// Asks for the finished model first and falls back to the tinted box, so the art pass
        /// lands one prop at a time without touching this file (PropArt). Everything after the
        /// call site is identical either way: the caller gets a GameObject at the right place,
        /// with a collider, ready to have its behaviour components added.
        /// </summary>
        GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color)
        {
            var art = PropArt.TryBuild(parent, name, localPosition, size);
            if (art != null) return art;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            SurfaceArt.Apply(go, size, SurfaceForProp(name), color, IsShell(name) ? 0f : SurfaceArt.PropBevel);
            return go;
        }

        /// <summary>
        /// Places a shared <see cref="BasementLayout"/> table. The blocks sit on the floor, so
        /// the table carries the footprint and the height and the y comes out of those rather
        /// than being written twice.
        /// </summary>
        void PlaceBlocks(Transform parent, BasementLayout.Block[] blocks)
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i];
                Box(parent, b.Name, new Vector3(b.Centre.x, b.Height * 0.5f, b.Centre.y),
                    new Vector3(b.Size.x, b.Height, b.Size.y), b.Colour);
            }
        }

        /// <summary>
        /// A piece of evidence lying in the world.
        ///
        /// The reach is computed from where the object actually is, not taken from GDD 8.3's
        /// 1.4m as written. That number is right for a document on a desk and impossible for
        /// anything on the floor: the ray starts at the eye, 1.63m up, so a slipper lying on
        /// the boards is 1.5m away before the player has taken a step. The red slipper (GDD
        /// 9.1) sat visible and unreachable for exactly that reason - the only way to pick it
        /// up was to crouch, which nothing in the game suggests.
        ///
        /// So the reach covers the drop from eye height plus room to stand, and never goes
        /// below the spec value.
        /// </summary>
        GameObject Pickup(Transform parent, string name, Vector3 position, Vector3 size, Color color,
                          string evidenceId, string labelKey, string requiredFlagId = null, int fromNight = -1)
        {
            var go = Box(parent, name, position, size, color);
            var pickup = go.AddComponent<EvidencePickup>();
            pickup.Setup(evidenceId, labelKey);
            pickup.Configure(labelKey, ReachFor(position.y + size.y * 0.5f), false);
            pickup.SetAvailability(requiredFlagId, fromNight);
            return go;
        }

        /// <summary>
        /// A fixture that is looked at rather than taken: a wall clock, a patch of floor.
        ///
        /// It stays where it is after it has been read - the building does not lose its clock
        /// because the caretaker checked the time - and it only means something on the one
        /// night the story is about it, so outside that night it is furniture.
        /// </summary>
        GameObject Observation(Transform parent, string name, Vector3 position, Vector3 size, Color color,
                               string evidenceId, string labelKey, int night)
        {
            var go = Box(parent, name, position, size, color);
            var pickup = go.AddComponent<EvidencePickup>();
            pickup.Setup(evidenceId, labelKey, false);
            pickup.Configure(labelKey, ReachFor(position.y + size.y * 0.5f), false);
            pickup.SetAvailability(null, night, night);
            return go;
        }

        /// <summary>Reach needed to aim at something whose top is at <paramref name="topY"/>.</summary>
        public static float ReachFor(float topY)
        {
            const float documentReach = 1.4f;      // GDD 8.3
            const float standingRoom = 0.7f;       // how far in front of it the player stands

            float eye = PlayerController.StandHeight - 0.12f;
            float drop = Mathf.Max(0f, eye - topY);

            float needed = Mathf.Sqrt(drop * drop + standingRoom * standingRoom) + 0.15f;
            return Mathf.Min(InteractionRaycaster.DefaultRange, Mathf.Max(documentReach, needed));
        }

        /// <summary>A cabinet or recess the player can wait a patrol out in (GDD 9.6).</summary>
        void AddHidingSpot(Transform parent, string name, Vector3 localPosition, Vector3 size)
        {
            var go = Box(parent, name, localPosition, size, new Color(0.21f, 0.21f, 0.24f));

            var anchor = new GameObject("Anchor").transform;
            anchor.SetParent(go.transform, false);
            anchor.localPosition = new Vector3(0f, -0.35f, 0f);

            var spot = go.AddComponent<Threat.HidingSpot>();
            spot.Setup(anchor, "ui.prompt.hide");
            ServiceHub.Threat.RegisterHidingSpot(spot);
        }

        // ---- interiors -----------------------------------------------------

        void BuildOfficeInterior()
        {
            var root = OwnedRoot(ZoneIds.Office);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Office);          // 4.0 x 3.0
            float north = half.y - 0.15f;
            float west = -half.x + 0.35f;
            float east = half.x - 0.35f;

            Box(root, "Desk", new Vector3(0f, 0.4f, north - 0.6f), new Vector3(3f, 0.8f, 0.8f),
                new Color(0.30f, 0.27f, 0.24f));
            var monitor = Box(root, "Monitor", new Vector3(0f, 1.15f, north - 0.35f),
                              new Vector3(1.1f, 0.6f, 0.08f), new Color(0.10f, 0.14f, 0.16f));

            OfficeTerminal = monitor.AddComponent<TerminalInteractable>();
            OfficeTerminal.Configure("ui.prompt.use_pc", 2.2f, true);
            OfficeSpawn = ZoneRegistry.FindSpawn(ZoneIds.Office);

            Box(root, "Chair", new Vector3(0f, 0.3f, north - 1.5f), new Vector3(0.6f, 0.6f, 0.6f),
                new Color(0.18f, 0.18f, 0.20f));

            // Left on the floor by the door after the prologue's first CCTV anomaly (GDD 9.1):
            // the child is at the office door, so the slipper is at the office door - the far
            // side of the room from the desk - and it only exists once she has been on CAM-03.
            var slipper = Pickup(root, "RedSlipper", new Vector3(0.55f, 0.07f, -half.y + 0.9f),
                                 new Vector3(0.16f, 0.1f, 0.26f), new Color(0.62f, 0.13f, 0.13f),
                                 "EV_RED_SLIPPER", "ui.prompt.take_slipper", FlagIds.SlipperDropped);
            slipper.GetComponent<EvidencePickup>().HideUntilAvailable();

            // GDD 15.6: the office door, on the wall the desk faces away from. GDD 15.1 always
            // claimed this room was the safe one; until there was a bolt and a peephole on it,
            // "safe" and "inert" looked identical from the chair.
            // Both boxes sit clear of the wall and clear of each other. The first pass put the
            // panel half inside the 0.2m wall slab and overlapped the peephole with it by two
            // centimetres, which is the sort of thing that reads as "the peephole does nothing"
            // rather than as a placement bug.
            const float wallFace = BuildingSpec.WallThickness * 0.5f;

            var doorFrame = Box(root, "OfficeDoorPanel", new Vector3(-0.9f, 1.05f, -half.y + wallFace + 0.12f),
                                new Vector3(0.9f, 2.1f, 0.1f), new Color(0.30f, 0.28f, 0.26f));
            var officeDoor = doorFrame.AddComponent<Interaction.OfficeDoorController>();
            officeDoor.Configure("ui.prompt.office_door_lock", 1.8f, true);

            // Proud of the panel and at eye height, so aiming high finds the peephole and
            // aiming anywhere else on the door finds the bolt.
            var peepholeBox = Box(root, "OfficeDoorPeephole",
                                  new Vector3(-0.9f, 1.55f, -half.y + wallFace + 0.24f),
                                  new Vector3(0.12f, 0.12f, 0.06f), new Color(0.16f, 0.16f, 0.17f));
            var peephole = peepholeBox.AddComponent<Interaction.OfficeDoorPeephole>();
            peephole.Configure("ui.prompt.office_door_peephole", 1.6f, true);
            peephole.Bind(officeDoor);

            // GDD 17.5.1: a 40-slot key cabinet on the west wall.
            Box(root, "KeyCabinet", new Vector3(west, 1.1f, 0.8f), new Vector3(0.3f, 1.6f, 1.6f),
                new Color(0.26f, 0.26f, 0.27f));

            Pickup(root, "HandoverMemo", new Vector3(-1.1f, 0.85f, north - 0.7f),
                   new Vector3(0.3f, 0.02f, 0.42f), new Color(0.78f, 0.76f, 0.68f),
                   "EV_HANDOVER", "ui.prompt.read_memo");

            // 8cm nearer the front edge than its mirror on the left, to leave the back-right
            // of the desk clear for the cassette deck below. Still fully on the desk: the
            // sheet is 0.42 deep and the desk front edge is at north - 1.0.
            Pickup(root, "Contract", new Vector3(1.1f, 0.85f, north - 0.78f),
                   new Vector3(0.3f, 0.02f, 0.42f), new Color(0.80f, 0.78f, 0.72f),
                   "EV_CONTRACT", "ui.prompt.read_contract");

            var radio = Box(root, "Radio", new Vector3(west + 0.2f, 1.5f, -0.8f),
                            new Vector3(0.3f, 0.2f, 0.3f), new Color(0.22f, 0.24f, 0.22f));
            radio.AddComponent<DialogueInteractable>().Setup("D_PROLOGUE_RADIO", "ui.prompt.use_radio");

            // Night 2: the melted cassette from the 2009 delivery box.
            //
            // On the desk, right of the monitor. It used to be at x 1.9 while the desk ends
            // at 1.5, so the only prop C04 asks for was hanging in the air past the end of
            // the desk. x 1.0 clears the monitor (which reaches 0.55) and z north - 0.38
            // clears the contract sheet in the row in front, while staying inside the desk
            // and inside reach from the chair side. y 0.875 stands it on the 0.8 desktop
            // rather than a couple of centimetres above it.
            Pickup(root, "CassettePlayer", new Vector3(1.0f, 0.875f, north - 0.38f),
                   new Vector3(0.45f, 0.15f, 0.3f), new Color(0.24f, 0.23f, 0.22f),
                   "E05_FIRE_CALL_TAPE_A", "ui.prompt.play_tape", FlagIds.KnowsPastFootage, 2);

            // Night 4: the drawer springs open after the call from 404 (GDD 9.5).
            Pickup(root, "DocumentDrawer", new Vector3(west, 1.2f, -1.8f),
                   new Vector3(0.3f, 0.35f, 0.9f), new Color(0.29f, 0.28f, 0.26f),
                   "E14_FIRE_DOOR_LOCK_LOG", "ui.prompt.open_drawer", FlagIds.Knows404, 4);

            // Night 6: the building address system (GDD 9.7 step 2).
            var pa = Box(root, "PublicAddress", new Vector3(east, 1.6f, 0f),
                         new Vector3(0.25f, 0.4f, 0.3f), new Color(0.33f, 0.32f, 0.30f));
            var paDialogue = pa.AddComponent<DialogueInteractable>();
            paDialogue.Setup("D_N6_EVACUATION", "ui.prompt.use_pa");
            paDialogue.SetAvailability(null, 6);
        }

        void BuildLobbyInterior()
        {
            var root = OwnedRoot(ZoneIds.Lobby);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Lobby);           // 6.0 x 4.0

            // GDD 17.5.2: a stainless mailbox bank and a parcel shelf.
            Box(root, "Mailboxes", new Vector3(-half.x + 0.25f, 1.2f, 1f),
                new Vector3(0.3f, 2f, 4f), new Color(0.30f, 0.31f, 0.30f));
            Box(root, "ParcelShelf", new Vector3(half.x - 0.3f, 0.8f, 1f),
                new Vector3(0.4f, 1.6f, 3f), new Color(0.32f, 0.29f, 0.25f));

            Pickup(root, "Key404", new Vector3(-half.x + 0.6f, 1.1f, 1f),
                   new Vector3(0.12f, 0.05f, 0.22f), new Color(0.68f, 0.62f, 0.35f),
                   "EV_KEY404", "ui.prompt.take_key");

            // Night 2: the carton of a drink discontinued in 2009.
            Pickup(root, "OldDeliveryBox", new Vector3(half.x - 1.2f, 0.3f, -0.5f),
                   new Vector3(0.5f, 0.5f, 0.5f), new Color(0.55f, 0.48f, 0.38f),
                   "E04_OLD_DELIVERY_LABEL", "ui.prompt.inspect_box", null, 2);

            // Night 2, N2-M01 (GDD v5.1 11): the two invariants that are only in the room.
            // CAM-02 shows a man sorting boxes in here; these are what the room itself says.
            //
            // The clock hangs over the parcel shelf, where the feed shows him working, so
            // the thing to compare is in the same part of the picture. The mat is on the
            // lobby side of the inner doorway: anybody who came in out of the rain crossed it.
            Observation(root, "LobbyWallClock", new Vector3(half.x - 0.08f, 2.0f, 1f),
                        new Vector3(0.05f, 0.36f, 0.36f), new Color(0.80f, 0.78f, 0.70f),
                        "EV_LOBBY_CLOCK", "ui.prompt.inspect_clock", 2);

            Observation(root, "EntranceMat", new Vector3(-3.65f, 0.012f, 1.8f),
                        new Vector3(1.4f, 0.02f, 0.8f), new Color(0.20f, 0.19f, 0.18f),
                        "EV_LOBBY_FOOTPRINTS", "ui.prompt.inspect_floor", 2);

            BuildLobbyEntrance(root, half);
        }

        /// <summary>
        /// The shared entrance (GDD 17.5.2, v3.0 38).
        ///
        /// The lobby had no way in. That is a strange thing for a building to be missing and a
        /// worse thing for this game to be missing, because the entire front half of it is
        /// about one question - who is at the door - and there was no door: a caller was a
        /// panel on a screen and then, if admitted, a person who appeared in the middle of an
        /// empty room. The zone's own id comment has always called it 로비 / 공동현관, and
        /// <c>ui.prompt.go_lobby</c> reads "공동현관으로" in Korean, so the fiction was there and
        /// the geometry was not.
        ///
        /// It is built as a porch standing against the far wall rather than as a zone of its
        /// own, for the same reason <see cref="Visitors.VisitorAccess.ZonesFor"/> already gives
        /// for not having a vestibule zone: the building has nothing to stream out there, and
        /// what the vestibule buys is the close look, not floor space. Standing in it is still
        /// standing in the lobby, which is exactly what <c>DoorReadService.AtTheGlass</c> asks.
        ///
        /// The glass is frames and mullions with the panes left out. A greybox pane would
        /// either be opaque - hiding the person the whole walk down exists to look at - or
        /// need a transparent URP material, and this project has already paid once for
        /// improvising a material (see GreyboxMaterial). An invisible slab across the outer
        /// opening is what actually stops anybody walking out into the night.
        ///
        /// The east half of this wall is taken: the lift door sits at x=2 and the stair door
        /// at x=4.5, so the entrance takes the western run and the three of them share the
        /// wall the way they would in a real lobby.
        /// </summary>
        void BuildLobbyEntrance(Transform root, Vector2 half)
        {
            const float InnerZ = 2.4f;      // lobby side of the vestibule
            const float OuterZ = 3.35f;     // the front door itself
            const float DoorstepZ = 3.7f;   // where somebody who has rung the bell waits
            const float Head = BuildingSpec.UnitDoorHeight + 0.15f;

            float west = -half.x;           // the lobby's own west wall closes this end
            float east = -1.3f;             // clear of the lift door at x=2
            float centre = (west + east) * 0.5f;

            var frame = new Color(0.34f, 0.35f, 0.36f);
            var mullion = new Color(0.46f, 0.48f, 0.50f);

            // ---- the vestibule's east partition -----------------------------
            Box(root, "EntrancePartition", new Vector3(east, Head * 0.5f, (InnerZ + half.y) * 0.5f),
                new Vector3(0.12f, Head, half.y - InnerZ), frame);

            // ---- inner line: glass either side of an open doorway ------------
            GlassRun(root, "EntranceInner", west + 0.06f, centre - 0.8f, InnerZ, Head, frame, mullion);
            GlassRun(root, "EntranceInner2", centre + 0.8f, east, InnerZ, Head, frame, mullion);
            Box(root, "EntranceInnerHead", new Vector3(centre, Head + 0.06f, InnerZ),
                new Vector3(1.6f, 0.12f, 0.14f), frame);

            // ---- outer line: the front door, shut ----------------------------
            GlassRun(root, "FrontDoor", west + 0.06f, east, OuterZ, Head, frame, mullion);

            // Two leaves' worth of mullion, so it reads as a door rather than as a window.
            Box(root, "FrontDoorLeafW", new Vector3(centre - 0.02f, Head * 0.5f, OuterZ),
                new Vector3(0.05f, Head, 0.1f), mullion);

            // What actually keeps the caretaker inside for the length of their shift. The
            // renderer goes so the caller is visible through the opening; the collider stays.
            var shut = Box(root, "FrontDoorShut", new Vector3(centre, Head * 0.5f, OuterZ),
                           new Vector3(east - west - 0.2f, Head, 0.06f), frame);
            var shutRenderer = shut.GetComponent<MeshRenderer>();
            if (shutRenderer != null) shutRenderer.enabled = false;

            // ---- the street side of the door ---------------------------------
            Box(root, "Doorstep", new Vector3(centre, 0.01f, DoorstepZ),
                new Vector3(east - west - 0.4f, 0.02f, half.y - OuterZ - 0.05f),
                new Color(0.16f, 0.16f, 0.17f));

            // The unit the caller presses. Deliberately not interactive: the decision is made
            // at the desk panel (v3.0 38.2), and a second way to answer the door would undo
            // the cost the whole system is built on.
            Box(root, "EntranceCallPanel", new Vector3(east - 0.45f, 1.35f, OuterZ + 0.12f),
                new Vector3(0.24f, 0.36f, 0.08f), new Color(0.28f, 0.30f, 0.31f));

            // Between the door head (2.2m) and the ceiling (2.6m), not half inside the ceiling,
            // and over the west glass: the doorway's own head carries the door sensor (N1-R03).
            SignPlate(root, "EntranceSign", new Vector3(centre - 1.25f, Head + 0.2f, InnerZ - 0.07f), 1.7f, 0.3f, 0f,
                      "world.sign.entrance", new Color(0.12f, 0.17f, 0.24f), SignWhite);

            LobbyVestibule = Anchor(root, "VestibuleAnchor", new Vector3(centre, 0f, (InnerZ + OuterZ) * 0.5f));
            // Stand in the clear pane beside the central door mullion.
            LobbyDoorstep = Anchor(root, "DoorstepAnchor", new Vector3(centre + .48f, 0f, DoorstepZ));
            // The exterior porch has its own small lamp, so a caller remains readable
            // during the opening interior blackout.
            Box(root, "PorchLamp", new Vector3(centre, 2.13f, OuterZ - .15f),
                new Vector3(.42f, .07f, .12f), new Color(.72f, .70f, .58f));
            var porchLamp = new GameObject("PorchLight");
            porchLamp.transform.SetParent(root, false);
            porchLamp.transform.localPosition = new Vector3(centre, 1.98f, OuterZ - .20f);
            var porchLight = porchLamp.AddComponent<Light>();
            porchLight.type = LightType.Point;
            porchLight.color = new Color(1f, .94f, .87f);
            porchLight.range = 2.6f;
            porchLight.intensity = 1.1f;
        }

        /// <summary>One run of shopfront glazing: a frame with the pane left out.</summary>
        void GlassRun(Transform root, string name, float fromX, float toX, float z, float head,
                      Color frame, Color mullion)
        {
            float width = toX - fromX;
            if (width <= 0.1f) return;

            float centre = (fromX + toX) * 0.5f;

            Box(root, name + "_Sill", new Vector3(centre, 0.06f, z), new Vector3(width, 0.12f, 0.14f), frame);
            Box(root, name + "_Head", new Vector3(centre, head, z), new Vector3(width, 0.12f, 0.14f), frame);
            Box(root, name + "_PostA", new Vector3(fromX, head * 0.5f, z), new Vector3(0.1f, head, 0.14f), frame);
            Box(root, name + "_PostB", new Vector3(toX, head * 0.5f, z), new Vector3(0.1f, head, 0.14f), frame);

            // A mullion roughly every 1.2m, which is what stops a 4m run of empty frame
            // reading as a hole in the wall.
            int bays = Mathf.Max(1, Mathf.RoundToInt(width / 1.2f));
            for (int i = 1; i < bays; i++)
                Box(root, name + "_Mullion" + i, new Vector3(fromX + width * i / bays, head * 0.5f, z),
                    new Vector3(0.05f, head, 0.12f), mullion);
        }

        /// <summary>An empty transform other systems can be pointed at.</summary>
        static Transform Anchor(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        void BuildElevatorInterior()
        {
            var root = OwnedRoot(ZoneIds.Elevator);
            if (root == null) return;

            Elevator = root.gameObject.AddComponent<ElevatorController>();

            var half = HalfOf(ZoneIds.Elevator);        // 0.75 x 0.75
            float panelX = half.x - 0.1f;

            // In front of the back wall's inner face (half - 0.1), not inside it.
            Box(root, "Handrail", new Vector3(0f, 0.9f, half.y - 0.14f),
                new Vector3(1.2f, 0.05f, 0.05f), new Color(0.40f, 0.41f, 0.42f));

            // Turned to face into the car. It had been printing into the back wall.
            var display = BuildWorldLabel(root, "Indicator", new Vector3(0f, 2.05f, half.y - 0.12f),
                                          0.6f, 0.3f, 96, 0f);
            display.text = "1";
            display.color = LedRed;
            display.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < ElevatorController.Panel.Length; i++)
            {
                var entry = ElevatorController.Panel[i];
                float y = 0.78f + i * 0.18f;

                var button = Box(root, "Btn_" + entry.FloorId, new Vector3(panelX, y, 0.2f),
                                 new Vector3(0.06f, 0.13f, 0.13f), new Color(0.44f, 0.46f, 0.46f));

                // The basement is staff-only; every resident floor is not.
                var access = entry.FloorId == FloorPlan.B1 ? AccessLevel.Staff2 : AccessLevel.Staff1;
                button.AddComponent<ElevatorButton>()
                      .Setup(Elevator, entry.FloorId, entry.ZoneId, entry.LabelKey, access);
            }

            // The hidden maintenance button (GDD 9.4), invisible until Maintenance access exists.
            var maintenance = Box(root, "Btn_M", new Vector3(panelX, 0.6f, 0.2f),
                                  new Vector3(0.06f, 0.13f, 0.13f), new Color(0.62f, 0.55f, 0.30f));
            // The service level is on the fourth floor, between the corridor and the room the
            // drawings deny (spec 22 M01 step 11): the M button opens onto 4F, not between
            // floors. Passing its floor id keeps the car indicator honest about where it is.
            maintenance.AddComponent<ElevatorButton>()
                       .Setup(Elevator, FloorPlan.F4, ZoneIds.ServicePassage,
                              "ui.elevator.maintenance", AccessLevel.Maintenance);
            maintenance.SetActive(false);

            Elevator.Bind(display, maintenance);
        }

        /// <summary>
        /// The stairwell (GDD 17.5.8).
        ///
        /// This used to be a 3x5 box with a decorative eight-step flight against one wall and
        /// four identical unlabelled plates at chest height on another. A playtester's report
        /// was exact: "the stairs only go to the roof and the first floor" - because nothing
        /// in the room said otherwise. A stairwell is the one space in this building whose
        /// whole job is to tell you which way is up, and that one was not doing it.
        ///
        /// It is now a shaft. Five landings a flight apart, a single flight going down to the
        /// basement and three going up, a handrail on every open edge, and a sign at every
        /// landing saying which floor its door opens onto. The floors themselves are still
        /// separate streamed zones sitting side by side in world space - that does not change -
        /// but the act of getting between them is now climbing or descending real stairs.
        /// </summary>
        void BuildStairwellInterior()
        {
            var root = OwnedRoot(ZoneIds.Stairwell);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Stairwell);       // 2.5 x 4.5

            var stepColor = new Color(0.26f, 0.26f, 0.27f);
            var landingColor = new Color(0.23f, 0.23f, 0.25f);

            // ---- landings ----------------------------------------------------
            //
            // One per floor, alternating ends, a flight apart. The door on each is built in
            // BuildTransitions from the same table, so a sign and its door cannot drift.
            _landingAnchors.Clear();
            for (int i = 0; i < StairSigns.Length; i++)
            {
                var sign = StairSigns[i];
                Landing(root, "Landing_" + sign.Name, sign.Height, sign.LandingZ, half, landingColor);

                // Where a player entering the shaft from this floor stands. One per landing,
                // so StairEntryDoor never has to compute a position (spec 0.8.2).
                var anchor = new GameObject("LandingAnchor_" + sign.Name);
                anchor.transform.SetParent(root, false);
                anchor.transform.localPosition = new Vector3(0f, sign.Height + 0.1f, sign.LandingZ);
                // Facing the flights, which leave from the far edge of the landing.
                anchor.transform.localRotation = Quaternion.Euler(0f, sign.LandingZ > 0f ? 180f : 0f, 0f);
                _landingAnchors[sign.FloorId] = anchor.transform;
            }

            // Climbing has to actually change which landing the player is on, or every door in
            // the shaft but the one they came in by stays shut.
            var floorIds = new string[StairSigns.Length];
            var heights = new float[StairSigns.Length];
            for (int i = 0; i < StairSigns.Length; i++)
            {
                floorIds[i] = StairSigns[i].FloorId;
                heights[i] = StairSigns[i].Height;
            }
            root.gameObject.AddComponent<StairLandingTracker>().Setup(floorIds, heights);

            // ---- the flights, alternating lanes the way a switchback does -----
            //
            // One flight between each pair of adjacent floors, B1 to 6F: six of them, and no
            // gaps. This is the physical half of spec 0.8 - climbing out of the 4F
            // landing reaches 5F because there is a flight there and no other way up.
            //
            // Consecutive flights swap lanes so nothing shares a lane with anything less than
            // two flights - 5.6m - away. Three in one lane would put the underside of one
            // flight 1.8m above the treads of the next.
            for (int i = 0; i + 1 < StairSigns.Length; i++)
            {
                var lower = StairSigns[i];
                var upper = StairSigns[i + 1];
                float lane = (i % 2 == 0) ? StairShaft.LaneBX : StairShaft.LaneAX;

                Flight(root, "Flight_" + lower.Name + "_" + upper.Name, lane,
                       lower.Height, upper.Height,
                       StairShaft.FlightEndOf(lower.FloorId), StairShaft.FlightEndOf(upper.FloorId),
                       stepColor);
            }

            // ---- one light per landing --------------------------------------
            //
            // The zone's single point light sits at the ceiling of an eleven metre shaft,
            // which lights the top landing and nothing else.
            for (int i = 0; i < StairSigns.Length; i++)
            {
                var sign = StairSigns[i];
                var lightGo = new GameObject("LandingLight_" + sign.Name);
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = new Vector3(0f, sign.Height + 2.3f, sign.LandingZ);

                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 7f;
                light.intensity = 1.5f;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.shadows = LightShadows.None;
                lightGo.AddComponent<CircuitLight>().Setup(CircuitIds.CorridorLights);
            }

            // ---- signage ------------------------------------------------------
            //
            // The whole point of the rebuild. A number at every landing, and an arrow on the
            // wall beside each flight saying where it goes.
            for (int i = 0; i < StairSigns.Length; i++)
            {
                var sign = StairSigns[i];
                bool north = sign.LandingZ > 0f;
                float wallZ = north ? half.y - 0.12f : -half.y + 0.12f;

                // Over the landing door (2.05m), not across it.
                var label = BuildWorldLabel(root, "Sign_" + sign.Name,
                                            new Vector3(0f, sign.Height + 2.33f, wallZ),
                                            1.9f, 0.34f, 90,
                                            north ? 0f : 180f);
                label.text = Loc.T(sign.LabelKey);
                label.color = StencilPaint;
                label.alignment = TextAnchor.MiddleCenter;
                // Spec 0.10.4: from 25 a glyph on it can deform for a moment.
                label.gameObject.AddComponent<Anomalies.DistortedLabel>().Setup(sign.LabelKey);

                // A stripe of floor number on the landing itself, readable while walking.
                var floorMark = BuildWorldLabel(root, "Mark_" + sign.Name,
                                                new Vector3(0f, sign.Height + 0.02f, sign.LandingZ),
                                                1.6f, 0.8f, 130, 0f, true);
                floorMark.text = Loc.T(sign.ShortKey);
                floorMark.color = FloorPaint;
                floorMark.alignment = TextAnchor.MiddleCenter;
                // And from 50 the number itself can be a neighbour's. Only the paint lies -
                // StairNavigator still computes the next landing from FloorPlan (spec 0.8.1).
                floorMark.gameObject.AddComponent<Anomalies.DistortedLabel>()
                         .SetupLanding(sign.ShortKey, sign.FloorId);
            }

            // Arrows beside the flights, on the lane wall each flight actually runs in.
            // Both flights out of the ground landing start at its edge: up in lane A, down
            // in lane B. The arrows go on the walls those lanes run against.
            var upArrow = BuildWorldLabel(root, "Sign_Up",
                                          new Vector3(half.x - 0.12f, StairShaft.Ground + 1.9f, 2.0f),
                                          1.6f, 0.4f, 96, 90f);
            upArrow.text = Loc.T("world.stairs.up");
            upArrow.color = StencilPaint;

            var downArrow = BuildWorldLabel(root, "Sign_Down",
                                            new Vector3(-half.x + 0.12f, StairShaft.Ground + 1.9f, 2.0f),
                                            1.6f, 0.4f, 96, -90f);
            downArrow.text = Loc.T("world.stairs.down");
            downArrow.color = StencilPaint;

            // On the ground landing, against the wall. It used to be at zone y - which is now
            // three metres of open shaft above the slab.
            AddHidingSpot(root, "HidingCabinetA",
                          new Vector3(-half.x + 0.45f, StairShaft.Ground + 0.8f, StairShaft.NorthLandingZ),
                          new Vector3(0.6f, 1.6f, 0.5f));
        }

        void BuildParkingInterior()
        {
            var root = OwnedRoot(ZoneIds.Parking);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Parking);         // 12.5 x 9.0

            // GDD 17.5.3: six pillars, no more than eight cars, plus the unregistered car in
            // the fire lane (T01). The table is shared with the night-5 patrol routes.
            PlaceBlocks(root, BasementLayout.ParkingBlocks(Storey));

            AddHidingSpot(root, "DarkUtilityCorner", new Vector3(half.x - 1.2f, 0.9f, -half.y + 1.2f),
                          new Vector3(1.2f, 1.8f, 1.2f));

            // GDD 15.5: the night reserve can only be topped up from down here. Every trip
            // costs whatever arrives at the front door while the office is empty.
            var breakerBox = Box(root, "BreakerPanel", new Vector3(-half.x + 0.3f, 1.4f, 2.5f),
                                 new Vector3(0.2f, 1.1f, 0.8f), new Color(0.22f, 0.26f, 0.24f));
            breakerBox.AddComponent<Interaction.BreakerPanel>().Configure("ui.prompt.breaker", 1.8f, true);

            // Night 6: the cutter that opens the 404 steel door (GDD 9.7 step 3).
            Pickup(root, "Toolbox", new Vector3(-half.x + 1f, 0.4f, -half.y + 1f),
                   new Vector3(0.9f, 0.8f, 0.5f), new Color(0.45f, 0.38f, 0.22f),
                   "E21_HYDRAULIC_CUTTER", "ui.prompt.take_cutter", null, 6);
        }

        void BuildArchiveInterior()
        {
            var root = OwnedRoot(ZoneIds.Archive);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Archive);         // 3.0 x 2.5

            PlaceBlocks(root, BasementLayout.ArchiveBlocks(half));

            Pickup(root, "MaintenanceBill2009", new Vector3(-2.2f, 1.15f, half.y - 0.7f),
                   new Vector3(0.3f, 0.02f, 0.4f), new Color(0.76f, 0.74f, 0.66f),
                   "E20_2009_MAINTENANCE_BILL", "ui.prompt.read_bill", null, 4);

            Pickup(root, "ChairmanLog", new Vector3(half.x - 0.8f, 1.15f, -1f),
                   new Vector3(0.3f, 0.02f, 0.4f), new Color(0.74f, 0.72f, 0.64f),
                   "E16_CHAIRMAN_ACCESS_LOG", "ui.prompt.read_access_log", null, 5);

            Pickup(root, "SmokeDevice", new Vector3(0f, 0.25f, -1.5f), new Vector3(0.3f, 0.3f, 0.3f),
                   new Color(0.30f, 0.30f, 0.32f), "E15_SMOKE_DEVICE", "ui.prompt.inspect_device", null, 5);

            AddHidingSpot(root, "HidingCabinetB", new Vector3(-half.x + 0.4f, 0.8f, -half.y + 0.5f),
                          new Vector3(0.6f, 1.6f, 0.5f));
        }

        // ---- residential corridors ------------------------------------------

        void BuildCorridors()
        {
            // v2.1 spec 0.11 moved the residents down: Seon-ja from 303 to 303, the vacant
            // unit from 304 to 304, the complainant from 305 to 305, Ji-woo from 602 to 602.
            // v5.1 3.1: 201~205 and the shared utility cupboard.
            AddCorridorDoors(ZoneIds.Floor02, new[] { "201", "202", "203", "204", "205" },
                             BuildingSpec.StandardDoorX);
            // 304 is sealed, not locked (GDD 9.2).
            // v5.1 3.1 and the 3F reference sheet: 303, 304 and 305 only. The first two
            // places on the corridor are the pipe shaft and a stretch of wall (WorldBuilder.Reference).
            AddCorridorDoors(ZoneIds.Floor03, new[] { null, null, "303", "304", "305" },
                             BuildingSpec.StandardDoorX, "304");
            // v5.1 3.1: 401~403, the wall where 404 is, 405, and the sealed service door.
            AddCorridorDoors(ZoneIds.Floor04, new[] { "401", "402", "403", "405" },
                             BuildingSpec.Floor04DoorX);
            // 504 was structurally removed years ago; its plate is still on the patched wall
            // and there has never been a door (spec 22 M17). Deliberately a different thing
            // from 404, which is a room the drawings deny (spec 32).
            AddCorridorDoors(ZoneIds.Floor05, new[] { "501", "502", "503", "504", "505" },
                             BuildingSpec.StandardDoorX, "504");
            AddCorridorDoors(ZoneIds.Floor06, new[] { "601", "602", "603", "604", "605" },
                             BuildingSpec.StandardDoorX);

            BuildFloor04Extras();
            BuildFloor03Extras();
            BuildFloor06Extras();
        }

        /// <summary>
        /// The unit doors down one side of a 복도식 corridor, each with its number on it.
        ///
        /// The number is the part that was missing. Every door had a name plate beside it -
        /// a 0.2m box with no text on it, and no way for a player standing in the corridor to
        /// tell 303 from 304. The first thing night 1 asks is to go and listen at 304.
        ///
        /// <paramref name="sealedUnit"/> is not given a door. GDD 9.2 is explicit that 304 has
        /// no handle and is papered over, and the greybox had been building a normal locked
        /// door there - which put a collider in front of the one interactable the case needs.
        /// </summary>
        void AddCorridorDoors(string zoneId, string[] units, float[] doorX, string sealedUnit = null)
        {
            var root = OwnedRoot(zoneId);
            if (root == null) return;

            float half = HalfOf(zoneId).y;
            float doorZ = half - 0.06f;

            for (int i = 0; i < units.Length && i < doorX.Length; i++)
            {
                if (units[i] == null) continue;
                if (units[i] == sealedUnit) { SealedUnitWall(root, units[i], doorX[i], doorZ); continue; }

                var frame = Box(root, "Door_" + units[i], new Vector3(doorX[i], DoorY, doorZ),
                                new Vector3(BuildingSpec.UnitDoorWidth, BuildingSpec.UnitDoorHeight, 0.08f),
                                new Color(0.24f, 0.23f, 0.22f));

                var leaf = new GameObject("Leaf").transform;
                leaf.SetParent(frame.transform, false);

                var door = frame.AddComponent<DoorController>();
                door.Setup("DOOR_" + units[i], leaf, AccessLevel.Maintenance, DoorState.ClosedLocked,
                           "ui.prompt.open_unit_door");

                // A handle, so a door that has one reads differently from the one that does not.
                Box(root, "Handle_" + units[i],
                    new Vector3(doorX[i] + BuildingSpec.UnitDoorWidth * 0.35f, 1.05f, doorZ - 0.07f),
                    new Vector3(0.11f, 0.04f, 0.06f), new Color(0.58f, 0.56f, 0.48f));

                UnitNumberPlate(root, units[i], doorX[i], doorZ);
                DoorFurniture(root, doorX[i], doorZ);
            }

            CorridorFloorSigns(root, zoneId);
        }

        /// <summary>
        /// A steel 현관문 is not a flat panel: it sits in a casing, has a peephole, and since
        /// the 2000s nearly every one has a digital lock fitted above the handle.
        /// </summary>
        void DoorFurniture(Transform root, float x, float doorZ)
        {
            float face = doorZ - 0.05f;
            var casing = new Color(0.20f, 0.20f, 0.20f);
            float w = BuildingSpec.UnitDoorWidth, h = BuildingSpec.UnitDoorHeight;
            Trim(root, "Casing_L_" + x, new Vector3(x - w * 0.5f - 0.03f, h * 0.5f, face), new Vector3(0.06f, h + 0.06f, 0.03f), casing);
            Trim(root, "Casing_R_" + x, new Vector3(x + w * 0.5f + 0.03f, h * 0.5f, face), new Vector3(0.06f, h + 0.06f, 0.03f), casing);
            Trim(root, "Casing_T_" + x, new Vector3(x, h + 0.03f, face), new Vector3(w + 0.12f, 0.06f, 0.03f), casing);

            float lockX = x + w * 0.35f;
            Trim(root, "DoorLock_" + x, new Vector3(lockX, 1.32f, doorZ - 0.075f), new Vector3(0.075f, 0.17f, 0.035f), new Color(0.16f, 0.16f, 0.17f));
            var keypad = Trim(root, "DoorLockPad_" + x, new Vector3(lockX, 1.34f, doorZ - 0.094f), new Vector3(0.05f, 0.08f, 0.004f), Color.black);
            SurfaceArt.ApplyGlow(keypad, new Color(0.10f, 0.25f, 0.45f) * 0.6f);
            Part(root, PrimitiveType.Cylinder, "Peephole_" + x, new Vector3(x, 1.55f, doorZ - 0.065f),
                 new Vector3(0.025f, 0.012f, 0.025f), new Color(0.6f, 0.58f, 0.5f), false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // And on the other side from the number, the unit's meter cabinet.
            MeterBox(root, x - w * 0.5f - 0.42f, doorZ);
        }

        static bool IsShell(string name)
        {
            return name == "Floor" || name == "Ceiling" || name.StartsWith("Wall_") ||
                   name.StartsWith("WindowView") || name == "StreetView";
        }

        /// <summary>The lit name plate beside a unit door, with the unit number on it.</summary>
        void UnitNumberPlate(Transform root, string unit, float x, float doorZ)
        {
            float plateX = x + BuildingSpec.UnitDoorWidth * 0.5f + 0.22f;

            // Proud of the wall. The wall's inner face is 4cm in front of doorZ, and the plate
            // used to sit inside it - so no unit number in the building had ever been visible.
            Box(root, "Plate_" + unit, new Vector3(plateX, 1.85f, doorZ - 0.07f),
                new Vector3(0.34f, 0.18f, 0.02f), new Color(0.14f, 0.15f, 0.16f));

            var label = BuildWorldLabel(root, "PlateText_" + unit,
                                        new Vector3(plateX, 1.85f, doorZ - 0.085f),
                                        0.34f, 0.18f, 64, 0f);
            label.text = unit;
            label.color = PlateInk;
            label.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>
        /// Unit 304 (GDD 9.2 / 17.5.4): no door, no handle, papered over. The listening spot
        /// sits proud of that wall so the raycast reaches it - the sound source used to be
        /// behind the door frame's collider, which is why "listen at 304" could not be done.
        /// </summary>
        void SealedUnitWall(Transform root, string unit, float x, float doorZ)
        {
            // Wallpaper over the opening. Very slightly proud of the wall and a shade off it,
            // so it reads as a covered door rather than as blank corridor.
            Box(root, "Papered_" + unit, new Vector3(x, DoorY, doorZ - 0.06f),
                new Vector3(BuildingSpec.UnitDoorWidth + 0.12f, BuildingSpec.UnitDoorHeight + 0.1f, 0.04f),
                new Color(0.30f, 0.29f, 0.27f));

            // The plate is still there. Nobody took it down.
            UnitNumberPlate(root, unit, x, doorZ);
        }

        /// <summary>
        /// The floor number at both ends of a corridor, the way a real 복도식 block signs its
        /// landings. A player who arrives by lift and one who arrives by stairs both need to
        /// be told which floor they are standing on.
        /// </summary>
        void CorridorFloorSigns(Transform root, string zoneId)
        {
            string key = FloorSignKeyFor(zoneId);
            if (string.IsNullOrEmpty(key)) return;

            var half = HalfOf(zoneId);

            // Painted on the wall over the end doors. They used to be centred at 2.05m, which is
            // the top edge of the doors under them. The fourth floor's west end is boarded up
            // (WorldBuilder.Reference), so its number goes over the lift door's new place.
            var west = zoneId == ZoneIds.Floor04
                ? BuildWorldLabel(root, "FloorSign_W", new Vector3(Floor04LiftX, 2.22f, -half.y + 0.12f), 0.9f, 0.26f, 80, 180f)
                : BuildWorldLabel(root, "FloorSign_W", new Vector3(-half.x + 0.14f, 2.38f, 0f), 1.2f, 0.36f, 110, -90f);
            west.text = Loc.T(key);
            west.color = StencilPaint;

            var east = BuildWorldLabel(root, "FloorSign_E",
                                       new Vector3(half.x - 0.14f, 2.38f, 0f), 1.2f, 0.36f, 110, 90f);
            east.text = Loc.T(key);
            east.color = StencilPaint;
        }

        static string FloorSignKeyFor(string zoneId)
        {
            string floorId = FloorPlan.FloorOfZone(zoneId);
            return floorId == null ? null : "world.stairs." + floorId.ToLowerInvariant() + ".short";
        }

        void BuildFloor04Extras()
        {
            var root = OwnedRoot(ZoneIds.Floor04);
            if (root == null) return;

            float half = HalfOf(ZoneIds.Floor04).y;

            // GDD 17.5.4: the stretch of wall between 403 and 405 is 1.2m wider than the same
            // wall on every other floor. It is the only part of the plan that is off-grid,
            // which is exactly what it should be: it is the room that was built there.
            Box(root, "BlankWall404",
                new Vector3(BuildingSpec.HiddenWallCentreX, 1.05f, half - 0.04f),
                new Vector3(BuildingSpec.HiddenWallExtra, 2.1f, 0.05f), new Color(0.19f, 0.19f, 0.21f));

            // v5.1 3.1: the sealed 4F-SERVICE door. It does not open; the passage behind it
            // is reached the way N3-M01 says, through the lift.
            float sealX = BuildingSpec.Floor04DoorX[4];
            Box(root, "SealedServiceDoor", new Vector3(sealX, BuildingSpec.FireDoorHeight * 0.5f, half - 0.07f),
                new Vector3(BuildingSpec.FireDoorWidth, BuildingSpec.FireDoorHeight, 0.08f), new Color(0.30f, 0.27f, 0.24f));
            Trim(root, "SealedServiceDoor_Tape", new Vector3(sealX, 1.2f, half - 0.12f),
                new Vector3(BuildingSpec.FireDoorWidth + 0.1f, 0.08f, 0.02f), new Color(0.70f, 0.58f, 0.18f));
            var sealLabel = BuildWorldLabel(root, "SealedServiceDoor_Sign", new Vector3(sealX, 2.25f, half - 0.09f),
                                            1.2f, 0.22f, 64, 0f);
            sealLabel.text = Loc.T("world.sign.service_sealed");
            sealLabel.color = WarningRed;
            sealLabel.alignment = TextAnchor.MiddleCenter;

            // The fire door control panel used during the night-5 replay (GDD 9.6).
            var panel = Box(root, "FireDoorPanel", new Vector3(-9f, 1.3f, -half + 0.06f),
                            new Vector3(0.35f, 0.45f, 0.08f), new Color(0.40f, 0.26f, 0.24f));
            panel.AddComponent<FlagInteractable>()
                 .Setup(FlagIds.FireDoorUnlocked, "ui.prompt.unlock_fire_door", 5, "E17_2009_CCTV_REPLAY");
        }

        /// <summary>The third floor: Seon-ja in 303, the vacant 304, the complainant in 305.</summary>
        void BuildFloor03Extras()
        {
            var root = OwnedRoot(ZoneIds.Floor03);
            if (root == null) return;

            float half = HalfOf(ZoneIds.Floor03).y;

            // 304 is the vacant unit that makes noise on night 1 (C01). It sits proud of the
            // papered-over wall rather than behind a door frame: the interaction raycast stops
            // at the first collider it meets, and a playtest found this one unreachable
            // because the locked door for the unit was in front of it.
            //
            // Waist height and half a metre wide, on the centre of the covered opening, so
            // aiming anywhere at the wallpaper finds it.
            var listen = Pickup(root, "SoundSource304",
                                new Vector3(BuildingSpec.StandardDoorX[3], 1.15f, half - 0.16f),
                                new Vector3(0.5f, 0.5f, 0.08f), new Color(0.33f, 0.29f, 0.27f),
                                "EV_304_SOUND", "ui.prompt.record_sound");
            listen.GetComponent<EvidencePickup>().Configure("ui.prompt.record_sound", 2.0f, false);

            // Night 4: Seon-ja invites the player into 303 if she is well (GDD 9.5). Spec 0.11
            // put her next door to the vacant unit so the night-1 walk and the night-4 visit
            // are the same short trip.
            var sunjaDoor = Box(root, "Door303_Talk",
                                new Vector3(BuildingSpec.StandardDoorX[2] - 0.55f, 1.1f, half - 0.12f),
                                new Vector3(0.2f, 0.2f, 0.06f), new Color(0.33f, 0.31f, 0.27f));
            var sunjaDialogue = sunjaDoor.AddComponent<DialogueInteractable>();
            sunjaDialogue.Setup("D_N4_SUNJA_TESTIMONY", "ui.prompt.talk_sunja");
            sunjaDialogue.SetAvailability(FlagIds.SunjaHealthy, 4);
        }

        /// <summary>
        /// The sixth floor: Ji-woo in 602.
        ///
        /// She used to live on the thirteenth, which under v2.1 is not a floor anyone lives on
        /// - it is the anomaly (spec 0.11). Putting her below the roof stairs is what connects
        /// her to the lift and the roof, which is what nights 2 and 3 are about.
        /// </summary>
        void BuildFloor06Extras()
        {
            var root = OwnedRoot(ZoneIds.Floor06);
            if (root == null) return;

            float half = HalfOf(ZoneIds.Floor06).y;

            var jiwooDoor = Box(root, "Door602_Talk",
                                new Vector3(BuildingSpec.StandardDoorX[1] - 0.55f, 1.1f, half - 0.12f),
                                new Vector3(0.2f, 0.2f, 0.06f), new Color(0.33f, 0.31f, 0.27f));
            var jiwooDialogue = jiwooDoor.AddComponent<DialogueInteractable>();
            jiwooDialogue.Setup("D_N3_JIWOO_ELEVATOR", "ui.prompt.talk_jiwoo");
            jiwooDialogue.SetAvailability(null, 3);
        }

        void BuildServicePassageInterior()
        {
            var root = OwnedRoot(ZoneIds.ServicePassage);
            if (root == null) return;

            var half = HalfOf(ZoneIds.ServicePassage);  // 6.0 x 0.6
            float wall = half.y - 0.08f;

            for (int i = 0; i < 6; i++)
                Box(root, "Pipe" + i, new Vector3(-5f + i * 2f, 2f, wall), new Vector3(1.8f, 0.12f, 0.12f),
                    new Color(0.27f, 0.25f, 0.22f));

            // v5.1: pencil marks say 언니 123 / 동생 117; no personal names.
            Pickup(root, "ChildHeightMarks", new Vector3(-2f, 1.2f, wall), new Vector3(0.5f, 0.5f, 0.03f),
                   new Color(0.42f, 0.40f, 0.36f), "EV_HEIGHT_MARKS", "ui.prompt.inspect_marks", null, 3);

            Pickup(root, "OldExtinguisher", new Vector3(2f, 0.45f, wall), new Vector3(0.18f, 0.55f, 0.18f),
                   new Color(0.46f, 0.20f, 0.18f), "E09_OLD_FIRE_EXTINGUISHER_SERIAL",
                   "ui.prompt.inspect_extinguisher");

            Pickup(root, "MaintenanceFloorLog", new Vector3(-4.5f, 1.1f, wall), new Vector3(0.3f, 0.4f, 0.03f),
                   new Color(0.72f, 0.70f, 0.62f), "E07_HIDDEN_MAINTENANCE_FLOOR_LOG", "ui.prompt.read_floor_log");

            // Night 3: Taeho is standing in the passage and does not want the player past him
            // (GDD 9.4 / case C06). Case C06 fires on entering this zone and its only objective
            // is this conversation, so without him on the floor the case had no way to close and
            // TaehoCooperates - the flag that opens the records room on night 4 - could never be
            // set. He blocks the way in, between the entrance and the evidence.
            var taeho = Box(root, "Taeho", new Vector3(-3.2f, 0.9f, 0f),
                            new Vector3(0.45f, 1.75f, 0.35f), new Color(0.29f, 0.31f, 0.30f));
            var taehoDialogue = taeho.AddComponent<DialogueInteractable>();
            taehoDialogue.Setup("D_N3_TAEHO_PASSAGE", "ui.prompt.talk_taeho");
            taehoDialogue.SetAvailability(null, 3);

            Pickup(root, "RadioSignal", new Vector3(half.x - 0.6f, 1f, 0f), new Vector3(0.25f, 0.25f, 0.25f),
                   new Color(0.30f, 0.34f, 0.30f), "E10_DONGSIK_RADIO_SIGNAL", "ui.prompt.listen_radio");

            Pickup(root, "DongsikLocation", new Vector3(half.x - 0.6f, 1.5f, 0f), new Vector3(0.25f, 0.25f, 0.25f),
                   new Color(0.34f, 0.38f, 0.34f), "E18_DONGSIK_LOCATION", "ui.prompt.trace_signal", null, 5);
        }

        void BuildUnit404Interior()
        {
            var root = OwnedRoot(ZoneIds.Unit404);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Unit404);         // 2.25 x 2.0

            // GDD 17.5.7: sink and portable stove, a bunk bed, an illegal heater, child drawings.
            Box(root, "Sink", new Vector3(-half.x + 0.5f, 0.45f, half.y - 0.4f), new Vector3(0.9f, 0.9f, 0.55f),
                new Color(0.30f, 0.30f, 0.31f));
            Box(root, "BunkBed", new Vector3(half.x - 0.55f, 0.8f, 0f), new Vector3(0.9f, 1.6f, 2f),
                new Color(0.32f, 0.28f, 0.24f));
            Box(root, "Heater", new Vector3(-half.x + 0.35f, 0.2f, -1f), new Vector3(0.4f, 0.35f, 0.25f),
                new Color(0.40f, 0.24f, 0.20f));
            Box(root, "ChildDrawings", new Vector3(-0.4f, 1.5f, half.y - 0.06f), new Vector3(1.4f, 0.7f, 0.02f),
                new Color(0.72f, 0.70f, 0.60f));

            Pickup(root, "OriginalLedger", new Vector3(half.x - 0.6f, 1.05f, -half.y + 0.5f),
                   new Vector3(0.3f, 0.07f, 0.4f), new Color(0.66f, 0.60f, 0.46f),
                   "E19_ORIGINAL_LEDGER", "ui.prompt.take_ledger", null, 6);

            var dongsik = Box(root, "Dongsik", new Vector3(-0.9f, 0.3f, -1f), new Vector3(0.55f, 0.6f, 1.4f),
                              new Color(0.36f, 0.32f, 0.30f));
            var rescue = dongsik.AddComponent<DialogueInteractable>();
            rescue.Setup("D_N6_DONGSIK_RESCUE", "ui.prompt.rescue_dongsik");
            rescue.SetAvailability(null, 6);

            var chairman = Box(root, "Chairman", new Vector3(0.6f, 0.9f, half.y - 0.7f),
                               new Vector3(0.45f, 1.75f, 0.35f), new Color(0.30f, 0.28f, 0.30f));
            var confront = chairman.AddComponent<DialogueInteractable>();
            confront.Setup("D_N6_CHAIRMAN_CONFRONT", "ui.prompt.confront_chairman");
            confront.SetAvailability(null, 6);
        }

        // ---- connections ---------------------------------------------------

        static readonly Vector3 DoorSize =
            new Vector3(BuildingSpec.UnitDoorWidth, BuildingSpec.UnitDoorHeight, 0.1f);

        void BuildTransitions()
        {
            var officeHalf = HalfOf(ZoneIds.Office);
            var lobbyHalf = HalfOf(ZoneIds.Lobby);

            // Office <-> lobby
            Transition(ZoneIds.Office, "DoorToLobby", new Vector3(0f, DoorY, -officeHalf.y + 0.06f),
                       ZoneIds.Lobby, AccessLevel.Staff1, "ui.prompt.go_lobby", DoorSize);
            Transition(ZoneIds.Lobby, "DoorToOffice", new Vector3(-lobbyHalf.x + 0.06f, DoorY, -2f),
                       ZoneIds.Office, AccessLevel.Staff1, "ui.prompt.go_office", DoorSize);

            // ---- vertical circulation, one floor at a time --------------------
            //
            // Both loops run over FloorPlan rather than over a list of floors that happen to
            // have doors. A floor that exists gets a lift door if the lift serves it and a
            // stair door either way; the thirteenth is not in FloorPlan, so it gets neither.
            foreach (var floor in FloorPlan.All)
            {
                string zoneId = floor.PrimaryZoneId;
                if (SizeOf(zoneId) == Vector2.one) continue;   // no such zone in the greybox

                var zoneHalf = HalfOf(zoneId);

                // The lobby is the one floor whose primary zone is not a corridor, so its two
                // doors sit on the north wall rather than at the ends of a run.
                bool lobby = zoneId == ZoneIds.Lobby;

                if (floor.ElevatorAccessible)
                    ElevatorDoor(zoneId, lobby
                        ? new Vector3(2f, DoorY, zoneHalf.y - 0.06f)
                        : zoneId == ZoneIds.Floor04
                            // The west end is boarded over (WorldBuilder.Reference).
                            ? new Vector3(Floor04LiftX, DoorY, -zoneHalf.y + 0.06f)
                            : new Vector3(-zoneHalf.x + 0.06f, DoorY, 0f));

                StairDoor(zoneId, floor.FloorId, lobby
                    ? new Vector3(4.5f, DoorY, zoneHalf.y - 0.06f)
                    : new Vector3(zoneHalf.x - 0.06f, DoorY, 0f), AccessLevel.Staff1);

                // And the fire door on that floor's landing, inside the shaft. Height and end
                // come from the same table the sign does, so the door marked 4층 is on the
                // landing marked 4층 by construction rather than by care.
                LandingDoor("StairsTo_" + floor.FloorId, floor.FloorId, AccessLevel.Staff1,
                            "ui.prompt.stairs_" + floor.FloorId.ToLowerInvariant());
            }

            // ---- B1 records room, off the parking level (v5.1 3.1) -------------
            // A caretaker's own records room is staff space from night 1: v5.1 sends them
            // there on night 3 (N3-R06) and it is never a story-gated door.
            Transition(ZoneIds.Parking, "ArchiveDoor", new Vector3(2f, DoorY, -HalfOf(ZoneIds.Parking).y + 0.06f),
                       ZoneIds.Archive, AccessLevel.Staff2, "ui.prompt.go_archive", DoorSize);
            Transition(ZoneIds.Archive, "ArchiveBack", new Vector3(0f, DoorY, -HalfOf(ZoneIds.Archive).y + 0.06f),
                       ZoneIds.Parking, AccessLevel.Staff1, "ui.prompt.go_back", DoorSize);

            BuildFloorSideRooms();

            // Service passage: entered by the elevator's maintenance button, exits onto 4F.
            Transition(ZoneIds.ServicePassage, "PassageExit",
                       new Vector3(-HalfOf(ZoneIds.ServicePassage).x + 0.06f, DoorY, 0f),
                       ZoneIds.Floor04, AccessLevel.Staff1, "ui.prompt.go_back", DoorSize);

            BuildSteelDoor();

            Transition(ZoneIds.Unit404, "Back404", new Vector3(0f, DoorY, -HalfOf(ZoneIds.Unit404).y + 0.06f),
                       ZoneIds.ServicePassage, AccessLevel.Staff1, "ui.prompt.go_back", DoorSize);
        }

        void BuildSteelDoor()
        {
            var passage = OwnedRoot(ZoneIds.ServicePassage);
            if (passage == null) return;

            var half = HalfOf(ZoneIds.ServicePassage);

            // A fire door, 1.0 x 2.1 (GDD 17.4), at the far end of the passage.
            var steel = Box(passage, "SteelDoor404",
                            new Vector3(half.x - 0.06f, BuildingSpec.FireDoorHeight * 0.5f, 0f),
                            new Vector3(0.1f, BuildingSpec.FireDoorHeight, BuildingSpec.FireDoorWidth),
                            new Color(0.22f, 0.20f, 0.20f));

            var transition = steel.AddComponent<ZoneTransition>();
            transition.Setup(ZoneIds.Unit404, AccessLevel.Key404, "ui.prompt.open_steel_door");
            transition.RequireEvidence("E21_HYDRAULIC_CUTTER", "ui.prompt.door_barred");
        }

        /// <summary>
        /// A fire door on the stairwell landing whose sign says <paramref name="signName"/>.
        ///
        /// This replaced StairPlate, which put four identical 0.35m plates in a row at chest
        /// height on one wall. They were transitions, so they worked; they were unlabelled and
        /// all at the same height, so nobody could tell them apart or tell which way they led.
        /// </summary>
        void LandingDoor(string name, string floorId, AccessLevel access,
                         string labelKey, float offsetX = 0f)
        {
            var root = OwnedRoot(ZoneIds.Stairwell);
            if (root == null) return;

            var half = HalfOf(ZoneIds.Stairwell);

            for (int i = 0; i < StairSigns.Length; i++)
            {
                if (StairSigns[i].FloorId != floorId) continue;

                bool north = StairSigns[i].LandingZ > 0f;
                float z = north ? half.y - 0.06f : -half.y + 0.06f;

                var go = Box(root, name,
                             new Vector3(offsetX, StairSigns[i].Height + DoorY, z),
                             DoorSize, new Color(0.34f, 0.40f, 0.42f));
                go.AddComponent<Interaction.StairLandingDoor>().SetupLanding(floorId, access, labelKey);
                return;
            }

            Log.Error("World", "no stair landing for floor " + floorId);
        }

        /// <summary>
        /// One row per landing: how high it is, which end of the shaft it is at, and what its
        /// door opens onto. BuildStairwellInterior and BuildTransitions both read this, so a
        /// sign can never end up on a landing whose door goes somewhere else.
        /// </summary>
        struct StairSign
        {
            public string Name;
            public string FloorId;
            public float Height;
            public float LandingZ;
            public string LabelKey;     // "B1 지하주차장"
            public string ShortKey;     // "B1"
        }

        /// <summary>
        /// One landing per floor, generated from FloorPlan (spec 27).
        ///
        /// This used to be five hand-written rows, which is how a fifteen-storey building came
        /// to have a stairwell serving B1, 1F, 4F, 8F and 13F. Generating it means a floor that
        /// exists has a landing and a floor that does not cannot be given one - so the
        /// thirteenth is missing here for the same reason it is missing everywhere else.
        /// </summary>
        static readonly StairSign[] StairSigns = BuildStairSigns();

        static StairSign[] BuildStairSigns()
        {
            var order = FloorPlan.Order;
            var signs = new StairSign[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                string floorId = order[i];
                string slug = floorId.ToLowerInvariant();
                signs[i] = new StairSign
                {
                    Name = FloorPlan.SignOf(floorId),
                    FloorId = floorId,
                    Height = StairShaft.HeightOf(floorId),
                    LandingZ = StairShaft.LandingZOf(floorId),
                    LabelKey = "world.stairs." + slug,
                    ShortKey = "world.stairs." + slug + ".short"
                };
            }
            return signs;
        }

        /// <summary>
        /// A landing platform spanning the shaft, railed along the one edge that opens onto it.
        ///
        /// The rail covers only the gap between the two stair lanes. It used to span the full
        /// width, which put a bar at chest height across the mouth of both flights - a
        /// stairwell nobody could walk into.
        /// </summary>
        void Landing(Transform root, string name, float y, float z, Vector2 half, Color color)
        {
            const float slab = 0.2f;
            Box(root, name, new Vector3(0f, y - slab * 0.5f, z),
                new Vector3(half.x * 2f - BuildingSpec.WallThickness, slab, BuildingSpec.LandingDepth), color);

            // The lanes leave from this edge; the strip between them opens onto the shaft.
            float edgeZ = z > 0f ? z - StairShaft.LandingHalfDepth : z + StairShaft.LandingHalfDepth;
            float gap = (StairShaft.LaneAX - BuildingSpec.StairWidth * 0.5f) * 2f;

            Box(root, name + "_Rail", new Vector3(0f, y + 1.0f, edgeZ),
                new Vector3(gap, 0.06f, 0.06f), new Color(0.42f, 0.42f, 0.45f));
        }

        /// <summary>
        /// One flight. Treads are built from the spec rather than typed in, so the rise stays
        /// under PlayerController's step offset and the run stays wide enough to walk.
        /// </summary>
        void Flight(Transform root, string name, float laneX, float fromY, float toY,
                    float fromZ, float toZ, Color color)
        {
            const float tread = 0.12f;
            int steps = BuildingSpec.StepsPerFlight;
            float rise = (toY - fromY) / steps;
            float run = (toZ - fromZ) / steps;

            for (int i = 0; i < steps; i++)
            {
                // The top of tread i is one rise above the landing it left.
                float top = fromY + rise * (i + 1);
                float z = fromZ + run * (i + 0.5f);

                Box(root, name + "_Step" + i, new Vector3(laneX, top - tread * 0.5f, z),
                    new Vector3(BuildingSpec.StairWidth, tread, Mathf.Abs(run) + 0.02f), color);
            }
        }

        /// <summary>
        /// The rooms hanging off a floor rather than sitting on the stairwell (spec 25).
        ///
        /// Every one is a two-way door, because the whole point of the compressed building is
        /// that the player learns it: a room you can walk into and have to find your way out
        /// of is a room they will avoid rather than remember.
        /// </summary>
        void BuildFloorSideRooms()
        {
            // B1 (v5.1 3.1): the electrical room off the parking level, the plant off that.
            SideRoom(ZoneIds.Parking, ZoneIds.RecyclingYard, "ui.prompt.go_recycling", 8f);
            SideRoom(ZoneIds.Parking, ZoneIds.Machinery, "ui.prompt.go_electrical", -8f);
            SideRoom(ZoneIds.Machinery, ZoneIds.PumpRoom, "ui.prompt.go_pump_room", -3f);
            SideRoom(ZoneIds.Machinery, ZoneIds.PipeRoom, "ui.prompt.go_pipe_room", 1.5f);
            SideRoom(ZoneIds.Machinery, ZoneIds.Toolroom, "ui.prompt.go_toolroom", 3.5f);

            // 1F, all off the lobby.
            SideRoom(ZoneIds.Lobby, ZoneIds.Laundry, "ui.prompt.go_laundry", -4f);
            SideRoom(ZoneIds.Lobby, ZoneIds.ConvenienceStore, "ui.prompt.go_store", -1f);
            SideRoom(ZoneIds.Lobby, ZoneIds.Playground, "ui.prompt.go_playground", 5f);

            // 2F shared facilities, off the corridor.
            SideRoom(ZoneIds.Floor02, ZoneIds.Lounge, "ui.prompt.go_lounge", -6f);
            SideRoom(ZoneIds.Floor02, ZoneIds.FitnessRoom, "ui.prompt.go_fitness", -1f);
            SideRoom(ZoneIds.Floor02, ZoneIds.Terrace, "ui.prompt.go_terrace", 5f);
        }

        /// <summary>A door into a side room, and the door back out of it.</summary>
        void SideRoom(string fromZone, string toZone, string labelKey, float offsetX)
        {
            var fromHalf = HalfOf(fromZone);
            var toHalf = HalfOf(toZone);

            Transition(fromZone, "DoorTo_" + toZone, new Vector3(offsetX, DoorY, -fromHalf.y + 0.06f),
                       toZone, AccessLevel.Staff1, labelKey, DoorSize);
            Transition(toZone, "DoorBack_" + fromZone, new Vector3(0f, DoorY, -toHalf.y + 0.06f),
                       fromZone, AccessLevel.Staff1, "ui.prompt.go_back", DoorSize);
        }

        void ElevatorDoor(string fromZone, Vector3 localPosition)
        {
            Transition(fromZone, "ElevatorDoor", localPosition, ZoneIds.Elevator,
                       AccessLevel.Staff1, "ui.prompt.enter_elevator", DoorSize);
        }

        /// <summary>
        /// The stair door on a floor (v2.1 spec 0.8.2).
        ///
        /// Built as a StairEntryDoor rather than a plain transition: it knows which floor it
        /// is on and nothing about where it comes out, which is what stops it coming out in
        /// the lobby.
        /// </summary>
        void StairDoor(string fromZone, string fromFloorId, Vector3 localPosition, AccessLevel access)
        {
            var root = OwnedRoot(fromZone);
            if (root == null) return;

            var zoneHalf = HalfOf(fromZone);
            bool onSideWall = Mathf.Abs(Mathf.Abs(localPosition.x) - zoneHalf.x) < 0.2f;
            var built = onSideWall ? new Vector3(DoorSize.z, DoorSize.y, DoorSize.x) : DoorSize;

            var go = Box(root, "StairDoor", localPosition, built, new Color(0.34f, 0.40f, 0.42f));
            go.AddComponent<Interaction.StairEntryDoor>()
              .SetupStairEntry(fromFloorId, access, "ui.prompt.enter_stairs");
        }

        void Transition(string fromZone, string name, Vector3 localPosition, string toZone,
                        AccessLevel access, string labelKey, Vector3 size)
        {
            // Only build the doors that belong to a zone this scene owns. The destination is
            // resolved when the player uses the door, because it may not be loaded yet.
            var root = OwnedRoot(fromZone);
            if (root == null) return;

            // A door on an east or west wall has to be turned to sit flat against it.
            var zoneHalf = HalfOf(fromZone);
            bool onSideWall = Mathf.Abs(Mathf.Abs(localPosition.x) - zoneHalf.x) < 0.2f;
            var built = onSideWall ? new Vector3(size.z, size.y, size.x) : size;

            var go = Box(root, name, localPosition, built, new Color(0.34f, 0.40f, 0.42f));
            go.AddComponent<ZoneTransition>().Setup(toZone, access, labelKey);
        }

        /// <summary>
        /// A piece of world-space signage.
        ///
        /// <paramref name="yaw"/> is the direction the reader looks in: 0 for a sign on a north
        /// wall, 180 on a south wall, 90 on an east wall, -90 on a west wall. Text is drawn
        /// from both sides, so the wrong yaw does not hide a sign - it mirrors it, which is how
        /// every north-wall plate in the building came to read backwards until a screenshot
        /// of the lobby's entrance sign showed it. Every label in this greybox was built without
        /// one until a playtester reported not being able to find unit 304 - which had a name
        /// plate beside its door the whole time, blank and facing the concrete.
        ///
        /// <paramref name="onFloor"/> lays it flat instead, for floor numbers painted on a
        /// stair landing.
        /// </summary>
        Text BuildWorldLabel(Transform parent, string name, Vector3 localPosition,
                             float width, float height, int fontSize,
                             float yaw = 0f, bool onFloor = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = onFloor
                ? Quaternion.Euler(90f, yaw, 0f)
                : Quaternion.Euler(0f, yaw, 0f);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width * 100f, height * 100f);
            rect.localScale = Vector3.one * 0.01f;

            var text = UI.UiFactory.CreateText("Value", go.transform, string.Empty, fontSize,
                                               TextAnchor.MiddleCenter, UI.UiFactory.Accent);
            UI.UiFactory.Stretch(text.rectTransform, 0f, 0f);

            // fontSize is in canvas units at 0.01 scale, so 64 meant 0.64m glyphs on an 18cm
            // plate: "304" wrapped one digit per line. Fit the text to its plate instead, with
            // the requested size as the ceiling.
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 1;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return text;
        }
    }

    /// <summary>
    /// A corridor light that goes out when its circuit is shed (GDD 15.5 / 9.6).
    /// </summary>
    public sealed class CircuitLight : MonoBehaviour
    {
        Light _light;
        string _circuitId;
        float _baseIntensity;

        public void Setup(string circuitId)
        {
            _circuitId = circuitId;
            _light = GetComponent<Light>();
            if (_light != null) _baseIntensity = _light.intensity;
        }

        void Update()
        {
            if (_light == null || ServiceHub.Facility == null) return;

            bool registered = ServiceHub.Facility.HasCircuit(_circuitId);
            bool on = !registered || ServiceHub.Facility.IsCircuitOn(_circuitId);

            float target = on ? _baseIntensity : _baseIntensity * 0.12f;
            _light.intensity = Mathf.MoveTowards(_light.intensity, target, Time.deltaTime * 4f);
        }
    }
}
