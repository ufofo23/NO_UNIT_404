using UnityEngine;
using UnityEngine.UI;
using NO404.Core;
using S = NO404.Gameplay.SurfaceArt.Surface;

namespace NO404.Gameplay
{
    /// <summary>
    /// The art pass on top of the greybox (v5.1 3.1): what each space is made of, the windows
    /// and the night beyond them, the fittings, and the furniture that tells a caretaker
    /// which room they are in before any sign does.
    ///
    /// Nothing here moves a collider the layout depends on. Surfaces are re-skinned in place,
    /// windows and fittings sit flush to walls and ceilings above head height, and floor
    /// markings carry no collider at all - so every patrol route, reach distance and corridor
    /// clearance measured against the greybox still holds.
    /// </summary>
    public sealed partial class WorldBuilder
    {
        struct Theme
        {
            public S Floor, Wall, Ceiling;
            public Color FloorTint, WallTint, CeilingTint;
            public Theme(S floor, S wall, S ceiling, float floorTint = 1f, float wallTint = 1f, float ceilingTint = 1f)
            {
                Floor = floor; Wall = wall; Ceiling = ceiling;
                FloorTint = Color.white * floorTint; WallTint = Color.white * wallTint; CeilingTint = Color.white * ceilingTint;
                FloorTint.a = WallTint.a = CeilingTint.a = 1f;
            }
        }

        static Theme ThemeOf(string zoneId)
        {
            switch (zoneId)
            {
                // The reference sheets paint every public room of the block the same way:
                // cream over a teal dado, cracked grey tile underfoot, board ceilings.
                case ZoneIds.Office: return new Theme(S.Terrazzo, S.LobbyWall, S.Ceiling, 0.95f, 1f, 0.95f);
                case ZoneIds.Lobby: return new Theme(S.Terrazzo, S.LobbyWall, S.Ceiling);
                case ZoneIds.Elevator: return new Theme(S.Linoleum, S.Metal, S.Metal, 0.6f, 0.75f, 0.7f);
                case ZoneIds.Stairwell: return new Theme(S.Concrete, S.StairWall, S.Concrete, 0.9f, 1f, 0.8f);
                case ZoneIds.Parking: return new Theme(S.Epoxy, S.Concrete, S.Concrete, 1f, 0.9f, 0.8f);
                case ZoneIds.RecyclingYard: return new Theme(S.Concrete, S.Concrete, S.Concrete, 0.8f, 0.85f, 0.7f);
                case ZoneIds.Machinery:
                case ZoneIds.PumpRoom:
                case ZoneIds.PipeRoom:
                case ZoneIds.Toolroom: return new Theme(S.Epoxy, S.Concrete, S.Concrete, 0.85f, 0.85f, 0.75f);
                case ZoneIds.Archive: return new Theme(S.Terrazzo, S.LobbyWall, S.Ceiling, 0.85f, 0.85f, 0.9f);
                case ZoneIds.Laundry: return new Theme(S.Tile, S.Tile, S.Ceiling, 0.85f, 1f, 1f);
                case ZoneIds.ConvenienceStore: return new Theme(S.Tile, S.Plaster, S.Ceiling);
                case ZoneIds.Playground:
                case ZoneIds.Terrace: return new Theme(S.Concrete, S.Concrete, S.Concrete, 0.7f, 0.8f, 0.5f);
                case ZoneIds.Lounge:
                case ZoneIds.FitnessRoom: return new Theme(S.Linoleum, S.Plaster, S.Ceiling);
                case ZoneIds.ServicePassage: return new Theme(S.Epoxy, S.Concrete, S.Concrete, 0.8f, 0.7f, 0.6f);
                case ZoneIds.Unit404: return new Theme(S.Jangpan, S.Wallpaper, S.Plaster, 0.8f, 0.85f, 0.6f);
                default: return new Theme(S.Terrazzo, S.CorridorWall, S.Ceiling, 0.9f, 1f, 0.9f);
            }
        }

        /// <summary>Re-skins a zone shell's floor, ceiling and walls with its theme.</summary>
        void DressShell(Transform root, ZoneSpec spec)
        {
            if (!SurfaceArt.Enabled) return;
            var theme = ThemeOf(spec.Id);
            float t = BuildingSpec.WallThickness;
            float wallSpan = spec.Height + spec.FloorDrop;

            Skin(root, "Floor", new Vector3(spec.Size.x, BuildingSpec.FloorThickness, spec.Size.y), theme.Floor, theme.FloorTint);
            Skin(root, "Ceiling", new Vector3(spec.Size.x, t, spec.Size.y), theme.Ceiling, theme.CeilingTint);
            Skin(root, "Wall_N", new Vector3(spec.Size.x, wallSpan, t), theme.Wall, theme.WallTint);
            Skin(root, "Wall_S", new Vector3(spec.Size.x, wallSpan, t), theme.Wall, theme.WallTint);
            Skin(root, "Wall_E", new Vector3(t, wallSpan, spec.Size.y), theme.Wall, theme.WallTint);
            Skin(root, "Wall_W", new Vector3(t, wallSpan, spec.Size.y), theme.Wall, theme.WallTint);
        }

        static void Skin(Transform root, string child, Vector3 size, S surface, Color tint)
        {
            var part = root.Find(child);
            if (part == null || PropArt.HasArt(child)) return;
            SurfaceArt.Apply(part.gameObject, size, surface, tint);
        }

        /// <summary>
        /// The surface an ordinary box gets from its name. Tinted with the greybox colour, so
        /// every prop keeps the value the layout pass gave it and gains grain on top.
        /// </summary>
        static S SurfaceForProp(string name)
        {
            if (name.Contains("Desk") || name.Contains("BunkBed") || name.Contains("Counter") ||
                name.Contains("Wardrobe") || name.Contains("Bench")) return S.Wood;
            if (name.Contains("Pipe")) return S.Metal;
            if (name.Contains("Pillar") || name.Contains("Step") || name.Contains("Landing")) return S.Concrete;
            if (name.Contains("ElevatorDoor")) return S.Metal;
            if (name.Contains("Door") || name.Contains("Leaf") || name.Contains("Cabinet") ||
                name.Contains("Panel")) return S.DoorPaint;
            if (name.Contains("Mailbox") || name.Contains("Handrail") || name.Contains("Rail") ||
                name.Contains("Btn_") || name.Contains("Washer") || name.Contains("Server")) return S.Metal;
            return S.Grain;
        }

        // =================================================================
        // the pass
        // =================================================================

        void BuildDressing()
        {
            foreach (var corridor in new[] { ZoneIds.Floor02, ZoneIds.Floor03, ZoneIds.Floor04, ZoneIds.Floor05, ZoneIds.Floor06 })
            {
                CorridorWindows(corridor);
                CeilingFittings(corridor, new[] { -7.5f, -2.5f, 2.5f, 7.5f });
                ExitSign(corridor);
            }

            CeilingFittings(ZoneIds.Office, new[] { -1.5f, 1.5f });
            CeilingFittings(ZoneIds.Lobby, new[] { -3f, 3f }, new[] { -2f, 1.5f });
            CeilingFittings(ZoneIds.Laundry, new[] { 0f });
            CeilingFittings(ZoneIds.Archive, new[] { 0f });
            CeilingFittings(ZoneIds.Machinery, new[] { -2f, 2f });
            CeilingFittings(ZoneIds.PumpRoom, new[] { 0f });
            CeilingFittings(ZoneIds.Parking, new[] { -9f, -3f, 3f, 9f }, new[] { -6.5f, 0f, 6.5f });
            // The sheet's service passage is lit, badly, by tubes - not by one bulb nobody can find.
            CeilingFittings(ZoneIds.ServicePassage, new[] { -3.5f, 3f });

            foreach (var corridor in new[] { ZoneIds.Floor02, ZoneIds.Floor03, ZoneIds.Floor04, ZoneIds.Floor05, ZoneIds.Floor06 })
                CorridorDetails(corridor);

            ReflectionProbes();

            DressLobby();
            DressOffice();
            DressParking();
            DressPlant();
            DressLaundry();
            DressFloor02();
            DressFloor06();
            DressServicePassage();
            DressUnit404();
            BuildReferenceDressing();
        }

        /// <summary>
        /// The open side of a 복도식 corridor: four windows on the wall opposite the doors,
        /// and the rest of the city behind them, still awake.
        /// </summary>
        void CorridorWindows(string zoneId)
        {
            var root = OwnedRoot(zoneId);
            if (root == null) return;

            float z = -HalfOf(zoneId).y + BuildingSpec.WallThickness * 0.5f;
            const float Bottom = 1.2f, Top = 2.3f, Width = 2.4f;
            float h = Top - Bottom, y = (Top + Bottom) * 0.5f;
            var frame = new Color(0.36f, 0.37f, 0.36f);

            // 2F's south wall also carries the doors to the shared rooms (-6, -1, 5); the
            // standard spacing put two windows straight across them.
            var xs = zoneId == ZoneIds.Floor02 ? new[] { -3.5f, 2f, 8f } : new[] { -6f, -2f, 2f, 6f };
            foreach (float x in xs)
            {
                var view = Box(root, "WindowView_" + x, new Vector3(x, y, z + 0.012f), new Vector3(Width, h, 0.02f), Color.black);
                StripCollider(view);
                // The skyline shifts along the corridor, so four windows do not show one photograph.
                SurfaceArt.ApplyView(view, new Vector3(Width, h, 0.02f), S.CityNight, 1.3f);
                OffsetView(view, (x + 10f) / 20f);

                Trim(root, "WindowSill_" + x, new Vector3(x, Bottom - 0.03f, z + 0.06f), new Vector3(Width + 0.12f, 0.06f, 0.12f), frame);
                Trim(root, "WindowHead_" + x, new Vector3(x, Top + 0.03f, z + 0.04f), new Vector3(Width + 0.12f, 0.06f, 0.08f), frame);
                Trim(root, "WindowJambL_" + x, new Vector3(x - Width * 0.5f, y, z + 0.04f), new Vector3(0.06f, h, 0.08f), frame);
                Trim(root, "WindowJambR_" + x, new Vector3(x + Width * 0.5f, y, z + 0.04f), new Vector3(0.06f, h, 0.08f), frame);
                Trim(root, "WindowMullion_" + x, new Vector3(x, y, z + 0.04f), new Vector3(0.04f, h, 0.06f), frame);
            }
        }

        static void OffsetView(GameObject view, float u)
        {
            var renderer = view.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            // A quarter of the skyline per window, starting where this window sits.
            block.SetVector("_BaseMap_ST", new Vector4(0.25f, 1f, u * 0.75f, 0f));
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Cool-white fluorescent, slightly green, the way old magnetic-ballast tubes are.</summary>
        static readonly Color TubeLight = new Color(0.90f, 0.97f, 1f);
        static readonly Color TubeGlow = new Color(0.95f, 0.98f, 0.92f) * 2.6f;

        /// <summary>
        /// Fluorescent battens, each one a light of its own.
        ///
        /// A room lit by one point at its centre reads as a render; a corridor lit by a row
        /// of tubes has pools and gaps, and that unevenness is most of what makes it read as a
        /// place. The zone's single central light is switched off wherever fittings take over.
        /// One tube per corridor is on its way out and flickers.
        /// </summary>
        void CeilingFittings(string zoneId, float[] xs, float[] zs = null)
        {
            var root = OwnedRoot(zoneId);
            if (root == null) return;
            if (zs == null) zs = new[] { 0f };
            float ceiling = HeightOf(zoneId);
            var housing = new Color(0.55f, 0.55f, 0.53f);
            bool corridor = IsCorridor(zoneId);
            // The car park is dark concrete and dark paint: twelve fittings and still the
            // brightest of them have to work harder than a room's.
            bool parking = zoneId == ZoneIds.Parking;
            float range = corridor ? 6.5f : parking ? 9.5f : 8.5f;
            float intensity = corridor ? 1.9f : parking ? 3.6f : 2.8f;

            int n = 0;
            foreach (float z in zs)
                for (int i = 0; i < xs.Length; i++, n++)
                {
                    float x = xs[i];
                    Trim(root, "Fitting_" + n, new Vector3(x, ceiling - 0.035f, z), new Vector3(1.25f, 0.07f, 0.22f), housing);
                    var tube = Box(root, "FittingTube_" + n, new Vector3(x, ceiling - 0.075f, z), new Vector3(1.15f, 0.02f, 0.13f), Color.white);
                    StripCollider(tube);
                    SurfaceArt.ApplyGlow(tube, TubeGlow);

                    var lamp = new GameObject("FittingLight_" + n);
                    lamp.transform.SetParent(root, false);
                    // Far enough below the tile that the ceiling is lit as a wash, not a hot spot.
                    lamp.transform.localPosition = new Vector3(x, ceiling - 0.4f, z);
                    var light = lamp.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = range;
                    light.intensity = intensity;
                    light.color = TubeLight;
                    light.shadows = LightShadows.None;
                    light.shadowStrength = 0.85f;
                    lamp.AddComponent<ProximityShadow>();

                    bool dying = corridor && i == xs.Length - 1;
                    if (dying) lamp.AddComponent<FlickerLight>().Setup(light, tube.GetComponent<Renderer>(), TubeGlow);
                    else if (corridor) lamp.AddComponent<CircuitLight>().Setup(CircuitIds.CorridorLights);
                }

            var central = root.Find("Light");
            var centralLight = central != null ? central.GetComponent<Light>() : null;
            if (centralLight != null) centralLight.enabled = false;
        }

        /// <summary>
        /// What a real 복도식 corridor has that a box does not: a skirting board you can see
        /// the edge of, a conduit run along the top of the wall with its junction boxes, and
        /// sprinkler heads down the ceiling.
        /// </summary>
        void CorridorDetails(string zoneId)
        {
            var root = OwnedRoot(zoneId);
            if (root == null) return;
            var half = HalfOf(zoneId);
            float face = half.y - BuildingSpec.WallThickness * 0.5f;
            float ceiling = HeightOf(zoneId);
            var skirt = new Color(0.12f, 0.12f, 0.12f);

            Trim(root, "Skirting_S", new Vector3(0f, 0.045f, -face + 0.008f), new Vector3(half.x * 2f - 0.2f, 0.09f, 0.016f), skirt);

            // On the door side, in runs between the doors.
            float[] doors = zoneId == ZoneIds.Floor04 ? BuildingSpec.Floor04DoorX : BuildingSpec.StandardDoorX;
            float from = -half.x + 0.1f;
            for (int i = 0; i <= doors.Length; i++)
            {
                float to = i < doors.Length ? doors[i] - 0.56f : half.x - 0.1f;
                if (to - from > 0.1f)
                    Trim(root, "Skirting_N" + i, new Vector3((from + to) * 0.5f, 0.045f, face - 0.008f),
                         new Vector3(to - from, 0.09f, 0.016f), skirt);
                if (i < doors.Length) from = doors[i] + 0.56f;
            }

            var conduit = new Color(0.50f, 0.50f, 0.48f);
            Trim(root, "Conduit", new Vector3(0f, ceiling - 0.12f, face - 0.03f), new Vector3(half.x * 2f - 0.3f, 0.035f, 0.035f), conduit);
            for (int i = 0; i < 4; i++)
                Trim(root, "JunctionBox_" + i, new Vector3(-7.5f + i * 5f, ceiling - 0.12f, face - 0.045f),
                     new Vector3(0.11f, 0.11f, 0.06f), conduit);

            for (int i = 0; i < 6; i++)
            {
                float x = -8.5f + i * 3.4f;
                Part(root, PrimitiveType.Cylinder, "SprinklerHead_" + i, new Vector3(x, ceiling - 0.03f, 0.45f),
                     new Vector3(0.06f, 0.05f, 0.06f), new Color(0.75f, 0.74f, 0.70f), false);
                Part(root, PrimitiveType.Cylinder, "SprinklerRose_" + i, new Vector3(x, ceiling - 0.006f, 0.45f),
                     new Vector3(0.11f, 0.012f, 0.11f), new Color(0.62f, 0.62f, 0.60f), false);
            }
        }

        /// <summary>
        /// One reflection probe per room, rendered once when the room is built, so a polished
        /// floor reflects that room rather than a sky the building does not have.
        /// </summary>
        void ReflectionProbes()
        {
            QualitySettings.realtimeReflectionProbes = true;
            for (int i = 0; i < Zones.Length; i++)
            {
                var spec = Zones[i];
                var root = OwnedRoot(spec.Id);
                if (root == null) continue;

                var go = new GameObject("ReflectionProbe");
                go.transform.SetParent(root, false);
                float height = spec.Height + spec.FloorDrop;
                go.transform.localPosition = new Vector3(0f, -spec.FloorDrop + Mathf.Min(1.6f, height * 0.5f), 0f);

                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.resolution = 128;
                probe.hdr = true;
                probe.boxProjection = true;
                probe.size = new Vector3(spec.Size.x, height, spec.Size.y);
                probe.center = new Vector3(0f, -spec.FloorDrop + height * 0.5f - go.transform.localPosition.y, 0f);
                probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
                probe.backgroundColor = new Color(0.01f, 0.01f, 0.015f);
                probe.cullingMask = Layers.WithoutCctvOnly(~0);
                if (Application.isPlaying) probe.RenderProbe();
            }
        }

        /// <summary>The green running-man sign over the stair door.</summary>
        void ExitSign(string zoneId)
        {
            var root = OwnedRoot(zoneId);
            if (root == null) return;
            var half = HalfOf(zoneId);
            // Beside the stair door rather than over it: the floor number owns that spot.
            var box = Box(root, "ExitSign", new Vector3(half.x - 0.12f, 2.2f, -0.95f), new Vector3(0.08f, 0.14f, 0.42f), Color.green);
            StripCollider(box);
            SurfaceArt.ApplyGlow(box, new Color(0.15f, 0.75f, 0.35f) * 1.8f);

            var label = BuildWorldLabel(root, "ExitSignText", new Vector3(half.x - 0.17f, 2.2f, -0.95f), 0.42f, 0.14f, 40, 90f);
            label.text = Loc.T("world.sign.exit");
            label.color = new Color(0.9f, 1f, 0.9f);
            label.alignment = TextAnchor.MiddleCenter;
        }

        // =================================================================
        // rooms
        // =================================================================

        void DressLobby()
        {
            var root = OwnedRoot(ZoneIds.Lobby);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Lobby);

            // The street beyond the front door - wet tarmac, one lamp, the block across the road.
            var street = Box(root, "StreetView", new Vector3(-3.65f, 1.3f, half.y - 0.12f), new Vector3(4.6f, 2.6f, 0.02f), Color.black);
            StripCollider(street);
            SurfaceArt.ApplyView(street, new Vector3(4.6f, 2.6f, 0.02f), S.Street, 1.25f);

            // The guard's counter, where visitor paperwork is laid out.
            Box(root, "GuardCounter", new Vector3(0.3f, 0.45f, 1.8f), new Vector3(1.4f, 0.9f, 0.45f), new Color(0.36f, 0.30f, 0.24f));
            // Mailbox doors: a grid scored onto the bank so it reads as forty boxes, not one.
            for (int row = 0; row < 5; row++)
                Trim(root, "MailboxRow_" + row, new Vector3(-half.x + 0.41f, 0.45f + row * 0.36f, 1f),
                     new Vector3(0.02f, 0.02f, 3.9f), new Color(0.18f, 0.18f, 0.18f));
            for (int col = 0; col < 8; col++)
                Trim(root, "MailboxCol_" + col, new Vector3(-half.x + 0.41f, 1.2f, -0.95f + col * 0.5f),
                     new Vector3(0.02f, 1.9f, 0.02f), new Color(0.18f, 0.18f, 0.18f));
            // Parcels on the shelf.
            for (int i = 0; i < 6; i++)
                Box(root, "Parcel_" + i, new Vector3(half.x - 0.32f, 0.55f + (i % 3) * 0.48f, -0.1f + (i / 3) * 1.4f),
                    new Vector3(0.3f, 0.26f, 0.35f + 0.05f * (i % 2)), new Color(0.55f, 0.45f, 0.32f));
        }

        void DressOffice()
        {
            var root = OwnedRoot(ZoneIds.Office);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Office);

            // A cork board of rotas and notices, and the CCTV wall's glow over the desk.
            Box(root, "NoticeBoard", new Vector3(-2.3f, 1.55f, half.y - 0.12f), new Vector3(1.2f, 0.8f, 0.04f), new Color(0.45f, 0.33f, 0.22f));
            for (int i = 0; i < 4; i++)
                Trim(root, "Notice_" + i, new Vector3(-2.7f + i * 0.27f, 1.6f + (i % 2) * 0.12f, half.y - 0.145f),
                     new Vector3(0.2f, 0.27f, 0.01f), new Color(0.78f, 0.77f, 0.70f));
            // The face of the monitor that looks at the chair.
            var glow = Box(root, "MonitorGlow", new Vector3(0f, 1.15f, half.y - 0.545f), new Vector3(1.0f, 0.5f, 0.01f), Color.black);
            StripCollider(glow);
            SurfaceArt.ApplyGlow(glow, new Color(0.10f, 0.18f, 0.20f));
            Box(root, "FilingCabinet", new Vector3(half.x - 0.35f, 0.65f, half.y - 0.5f), new Vector3(0.5f, 1.3f, 0.6f), new Color(0.32f, 0.33f, 0.33f));
        }

        void DressParking()
        {
            var root = OwnedRoot(ZoneIds.Parking);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Parking);

            // Bay lines between the parked cars, and the hatched fire lane the illegal car sits in.
            for (int i = 0; i < 7; i++)
                Decal(root, "BayLine_" + i, new Vector3(-12f + i * 4f, 0.005f, 5.5f), new Vector3(0.1f, 0.01f, 4.6f), new Color(0.75f, 0.75f, 0.70f));
            Decal(root, "FireLaneEdge", new Vector3(0f, 0.005f, -3.2f), new Vector3(half.x * 2f - 0.4f, 0.01f, 0.12f), new Color(0.75f, 0.60f, 0.12f));
            for (int i = 0; i < 10; i++)
                Decal(root, "FireLaneHatch_" + i, new Vector3(-10.5f + i * 2.4f, 0.006f, -6f), new Vector3(0.18f, 0.01f, 5f),
                      new Color(0.70f, 0.55f, 0.12f), 35f);

            DressCars(root);

            float wallFace = half.y - BuildingSpec.WallThickness * 0.5f;
            SignPlate(root, "ArchiveSign", new Vector3(2f, 2.3f, -wallFace), 0.9f, 0.24f, 180f, "world.sign.archive",
                      new Color(0.62f, 0.63f, 0.60f), SignInk);
            SignPlate(root, "ElectricalSign", new Vector3(-8f, 2.3f, -wallFace), 0.9f, 0.24f, 180f, "world.sign.electrical",
                      new Color(0.62f, 0.63f, 0.60f), SignInk);
        }

        /// <summary>Paint colours a 1990s car park is actually full of.</summary>
        static readonly Color[] CarPaint =
        {
            new Color(0.78f, 0.78f, 0.76f), new Color(0.52f, 0.53f, 0.54f), new Color(0.08f, 0.08f, 0.09f),
            new Color(0.12f, 0.16f, 0.26f), new Color(0.32f, 0.33f, 0.34f), new Color(0.40f, 0.10f, 0.09f)
        };

        /// <summary>
        /// The parked cars: body, glass cabin, wheels and lamps, inside the footprint of the
        /// block the patrol routes and the collider were laid out around. The block stays as
        /// the collider and stops being drawn.
        /// </summary>
        void DressCars(Transform root)
        {
            int n = 0;
            foreach (var block in BasementLayout.ParkingBlocks(Storey))
            {
                if (!block.Name.Contains("Car")) continue;
                var body = root.Find(block.Name);
                if (body == null || PropArt.HasArt(block.Name)) continue;
                var renderer = body.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = false;

                float x = block.Centre.x, z = block.Centre.y, w = block.Size.x, l = block.Size.y;
                var paint = block.Name == "IllegalCar" ? new Color(0.36f, 0.08f, 0.07f) : CarPaint[n++ % CarPaint.Length];
                var glass = new Color(0.05f, 0.06f, 0.07f);

                var lower = Trim(root, block.Name + "_Body", new Vector3(x, 0.52f, z), new Vector3(w, 0.62f, l), paint);
                SurfaceArt.Apply(lower, new Vector3(w, 0.62f, l), S.DoorPaint, paint, 0.09f);
                var cabin = Trim(root, block.Name + "_Cabin", new Vector3(x, 1.08f, z - 0.25f), new Vector3(w - 0.16f, 0.5f, l * 0.5f), glass);
                SurfaceArt.Apply(cabin, new Vector3(w - 0.16f, 0.5f, l * 0.5f), S.Metal, glass, 0.12f);
                var roof = Trim(root, block.Name + "_Roof", new Vector3(x, 1.34f, z - 0.25f), new Vector3(w - 0.3f, 0.04f, l * 0.42f), paint);
                SurfaceArt.Apply(roof, new Vector3(w - 0.3f, 0.04f, l * 0.42f), S.DoorPaint, paint, 0.015f);

                for (int i = 0; i < 4; i++)
                {
                    float wx = x + (i % 2 == 0 ? -1f : 1f) * (w * 0.5f - 0.1f);
                    float wz = z + (i < 2 ? -1f : 1f) * (l * 0.5f - 0.75f);
                    Part(root, PrimitiveType.Cylinder, block.Name + "_Wheel" + i, new Vector3(wx, 0.31f, wz),
                         new Vector3(0.62f, 0.2f, 0.62f), new Color(0.06f, 0.06f, 0.06f), false)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                // Tail lamps on the aisle side, unlit; headlamps against the wall.
                var tail = Trim(root, block.Name + "_Tail", new Vector3(x, 0.72f, z - l * 0.5f - 0.005f), new Vector3(w - 0.3f, 0.08f, 0.02f), Color.black);
                SurfaceArt.ApplyGlow(tail, new Color(0.25f, 0.02f, 0.02f));
                Trim(root, block.Name + "_Head", new Vector3(x, 0.68f, z + l * 0.5f + 0.005f), new Vector3(w - 0.3f, 0.1f, 0.02f), new Color(0.7f, 0.7f, 0.68f));
            }
        }

        void DressPlant()
        {
            var machinery = OwnedRoot(ZoneIds.Machinery);
            if (machinery != null)
            {
                var half = HalfOf(ZoneIds.Machinery);
                for (int i = 0; i < 4; i++)
                    Box(machinery, "Switchboard_" + i, new Vector3(-3f + i * 1.5f, 0.9f, half.y - 0.3f),
                        new Vector3(1.2f, 1.8f, 0.4f), new Color(0.40f, 0.42f, 0.40f));
                Box(machinery, "Transformer", new Vector3(-half.x + 0.8f, 0.8f, 0.5f), new Vector3(1.2f, 1.6f, 1.6f), new Color(0.30f, 0.33f, 0.30f));
                // v5.1 3.1: the lift pit access is down here.
                Decal(machinery, "LiftPitHatch", new Vector3(3.2f, 0.01f, 1.2f), new Vector3(1.2f, 0.02f, 1.2f), new Color(0.22f, 0.22f, 0.22f));
                Decal(machinery, "LiftPitHatchStripe", new Vector3(3.2f, 0.015f, 1.2f), new Vector3(1.25f, 0.02f, 0.08f), new Color(0.72f, 0.58f, 0.10f));
            }

            var pump = OwnedRoot(ZoneIds.PumpRoom);
            if (pump != null)
            {
                Part(pump, PrimitiveType.Cylinder, "WaterTank", new Vector3(-2.2f, 1.2f, 1.0f), new Vector3(2.0f, 2.4f, 2.0f), new Color(0.38f, 0.44f, 0.46f), true);
                Trim(pump, "TankLadder", new Vector3(-1.15f, 1.2f, 1.0f), new Vector3(0.05f, 2.2f, 0.4f), new Color(0.3f, 0.3f, 0.3f));
                for (int i = 0; i < 2; i++)
                {
                    Box(pump, "PumpMotor_" + i, new Vector3(1.0f + i * 1.2f, 0.35f, 1.9f), new Vector3(0.5f, 0.7f, 0.8f), new Color(0.20f, 0.36f, 0.48f));
                    Part(pump, PrimitiveType.Cylinder, "PumpPipe_" + i, new Vector3(1.0f + i * 1.2f, 1.4f, 2.4f), new Vector3(0.16f, 1.4f, 0.16f), new Color(0.35f, 0.35f, 0.33f), false);
                }
                Trim(pump, "PipeRun", new Vector3(0.5f, 2.1f, 2.6f), new Vector3(4f, 0.16f, 0.16f), new Color(0.35f, 0.35f, 0.33f));
            }

            var yard = OwnedRoot(ZoneIds.RecyclingYard);
            if (yard != null)
            {
                // The discarded wardrobe from 2008, with its old disposal sticker (v5.1 N1-R14).
                Box(yard, "DiscardedWardrobe", new Vector3(-3f, 0.9f, 2.85f), new Vector3(1.1f, 1.8f, 0.6f), new Color(0.42f, 0.33f, 0.24f));
                for (int i = 0; i < 3; i++)
                    Box(yard, "RecycleBin_" + i, new Vector3(1.0f + i * 1.1f, 0.5f, 3.2f), new Vector3(0.9f, 1.0f, 0.9f),
                        i == 0 ? new Color(0.20f, 0.36f, 0.22f) : i == 1 ? new Color(0.22f, 0.30f, 0.45f) : new Color(0.50f, 0.42f, 0.18f));
            }

            var archive = OwnedRoot(ZoneIds.Archive);
            if (archive != null)
                Box(archive, "ArchiveDesk", new Vector3(-1.6f, 0.4f, -1.2f), new Vector3(1.0f, 0.8f, 0.6f), new Color(0.36f, 0.30f, 0.24f));
        }

        void DressLaundry()
        {
            var root = OwnedRoot(ZoneIds.Laundry);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Laundry);
            for (int i = 0; i < 3; i++)
            {
                float x = -1.6f + i * 1.0f;
                Box(root, "Washer_" + i, new Vector3(x, 0.45f, half.y - 0.4f), new Vector3(0.7f, 0.9f, 0.65f), new Color(0.78f, 0.79f, 0.78f));
                var door = Part(root, PrimitiveType.Cylinder, "WasherDoor_" + i, new Vector3(x, 0.5f, half.y - 0.73f),
                                new Vector3(0.42f, 0.02f, 0.42f), new Color(0.15f, 0.17f, 0.20f), false);
                door.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        void DressFloor02()
        {
            var root = OwnedRoot(ZoneIds.Floor02);
            if (root == null) return;
            // v5.1 3.1: the shared utility cupboard, between two windows.
            Box(root, "UtilityCupboard", new Vector3(0f, 0.9f, -HalfOf(ZoneIds.Floor02).y + 0.25f), new Vector3(0.9f, 1.8f, 0.3f), new Color(0.40f, 0.40f, 0.38f));
        }

        void DressFloor06()
        {
            var root = OwnedRoot(ZoneIds.Floor06);
            if (root == null) return;
            float ceiling = HeightOf(ZoneIds.Floor06);
            // v5.1 3.1: the top floor's ceiling hatch and the ventilation box behind it.
            Trim(root, "CeilingHatchFrame", new Vector3(1.5f, ceiling - 0.02f, -0.3f), new Vector3(0.7f, 0.04f, 0.7f), new Color(0.40f, 0.40f, 0.38f));
            Trim(root, "VentDuct", new Vector3(-4f, ceiling - 0.15f, -0.72f), new Vector3(6f, 0.3f, 0.4f), new Color(0.55f, 0.56f, 0.56f));
        }

        /// <summary>
        /// The one bulb nobody took down when the unit was struck off: bare, on its flex, warmer
        /// and dimmer than anything else in the building.
        /// </summary>
        void DressUnit404()
        {
            var root = OwnedRoot(ZoneIds.Unit404);
            if (root == null) return;
            float ceiling = HeightOf(ZoneIds.Unit404);
            // Above eye height: the camera must never pass through it.
            var at = new Vector3(-0.3f, ceiling - 0.42f, 0.2f);

            Trim(root, "BulbFlex", at + new Vector3(0f, 0.22f, 0f), new Vector3(0.012f, 0.4f, 0.012f), new Color(0.08f, 0.08f, 0.08f));
            var bulb = Part(root, PrimitiveType.Sphere, "Bulb", at, new Vector3(0.08f, 0.1f, 0.08f), Color.white, false);
            SurfaceArt.ApplyGlow(bulb, new Color(1f, 0.66f, 0.36f) * 1.5f);

            var lamp = new GameObject("BulbLight");
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = at - new Vector3(0f, 0.08f, 0f);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5.5f;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.80f, 0.58f);
            light.shadows = LightShadows.None;
            light.shadowStrength = 0.9f;
            lamp.AddComponent<ProximityShadow>();
        }

        void DressServicePassage()
        {
            var root = OwnedRoot(ZoneIds.ServicePassage);
            if (root == null) return;
            // A single bare bulb on a cable - the only light anybody ever fitted in here.
            var bulb = Box(root, "BareBulb", new Vector3(0f, 2.05f, 0f), new Vector3(0.08f, 0.1f, 0.08f), Color.white);
            StripCollider(bulb);
            SurfaceArt.ApplyGlow(bulb, new Color(1f, 0.85f, 0.6f) * 3f);
            Trim(root, "BulbCable", new Vector3(0f, 2.2f, 0f), new Vector3(0.01f, 0.2f, 0.01f), Color.black);
        }

        // =================================================================
        // helpers
        // =================================================================

        /// <summary>A piece of trim: decorative, flush, and never something to collide with.</summary>
        GameObject Trim(Transform root, string name, Vector3 position, Vector3 size, Color colour)
        {
            var go = Box(root, name, position, size, colour);
            StripCollider(go);
            return go;
        }

        /// <summary>Paint on the floor. No collider, so it can never become a step.</summary>
        GameObject Decal(Transform root, string name, Vector3 position, Vector3 size, Color colour, float yaw = 0f)
        {
            var go = Trim(root, name, position, size, colour);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A round thing - a tank, a pipe, a dial. <paramref name="size"/> is its full extent.</summary>
        GameObject Part(Transform root, PrimitiveType type, string name, Vector3 position, Vector3 size, Color colour, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = type == PrimitiveType.Cylinder ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
            go.GetComponent<MeshRenderer>().sharedMaterial = SurfaceArt.Enabled ? SurfaceArt.Lit(SurfaceArt.Surface.Grain, colour) : MaterialFor(colour);
            if (!solid) StripCollider(go);
            return go;
        }

        static void StripCollider(GameObject go)
        {
            if (go == null) return;
            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                if (Application.isPlaying) Object.Destroy(colliders[i]);
                else Object.DestroyImmediate(colliders[i]);
            }
        }
    }
}
