using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NO404.Core;
using S = NO404.Gameplay.SurfaceArt.Surface;

namespace NO404.Gameplay
{
    /// <summary>
    /// The map reference sheets (map_refer/floor_ref_zip_input) laid onto the building.
    ///
    /// Each sheet is one floor of the same tired block - "폐허가 되기 전, 아직 사람이 사는 곳" -
    /// and what makes them read as a place is the stuff on the walls, not the walls: pipes
    /// along the ceiling line, a meter cabinet beside every door, a lift with its call panel,
    /// an extinguisher and a bin, notices nobody reads. Then each floor has its own thing:
    /// the 관리사무소 window in the lobby, hazard-painted pillars with B1 on them, the fourth
    /// floor's end boarded over in plywood and polythene, the fifth floor's red lamps.
    ///
    /// The rule from WorldBuilder.Dressing still holds: nothing here has a collider. Every
    /// patrol leg, reach distance and spawn clearance measured against the greybox stands,
    /// and no prop can end up between the player and an interactable's raycast.
    ///
    /// Signage is painted, not lit. World labels are unlit UI text, so a sign drawn in the
    /// UI accent colour glowed like a menu; paint and ink colours read as part of the wall.
    /// </summary>
    public sealed partial class WorldBuilder
    {
        // ---- paint and ink ---------------------------------------------------------
        static readonly Color StencilPaint = new Color(0.10f, 0.14f, 0.18f, 0.9f);
        static readonly Color FloorPaint = new Color(0.62f, 0.62f, 0.56f, 0.75f);
        static readonly Color PlateInk = new Color(0.62f, 0.63f, 0.60f);
        static readonly Color SignInk = new Color(0.08f, 0.10f, 0.12f);
        static readonly Color SignWhite = new Color(0.78f, 0.80f, 0.78f);
        static readonly Color WarningRed = new Color(0.58f, 0.07f, 0.06f);
        static readonly Color LedRed = new Color(1f, 0.20f, 0.12f);

        // ---- materials of the fittings ---------------------------------------------
        static readonly Color PipeGrey = new Color(0.46f, 0.50f, 0.50f);
        static readonly Color BinBlue = new Color(0.26f, 0.34f, 0.38f);
        static readonly Color PaperWhite = new Color(0.78f, 0.76f, 0.68f);
        static readonly Color ExtinguisherRed = new Color(0.62f, 0.12f, 0.10f);

        /// <summary>Where the fourth floor's lift door went when the end wall was boarded over.</summary>
        const float Floor04LiftX = -8f;

        void BuildReferenceDressing()
        {
            foreach (var corridor in new[] { ZoneIds.Floor02, ZoneIds.Floor03, ZoneIds.Floor04, ZoneIds.Floor05, ZoneIds.Floor06 })
                CorridorServices(corridor);

            ReferenceFloor02();
            ReferenceFloor03();
            ReferenceFloor04();
            ReferenceFloor05();
            ReferenceFloor06();
            ReferenceLobby();
            ReferenceParking();
            ReferencePlant();
            ReferenceServicePassage();
            ReferenceUnit404();
            ReferenceStairwell();
        }

        // =================================================================
        // every corridor
        // =================================================================

        /// <summary>
        /// Pipes along both walls under the ceiling, the lift's call panel and floor display,
        /// an extinguisher by the stairs and a bin by the lift.
        /// </summary>
        void CorridorServices(string zoneId)
        {
            var root = OwnedRoot(zoneId);
            if (root == null) return;
            var half = HalfOf(zoneId);
            float ceiling = HeightOf(zoneId);
            float face = half.y - BuildingSpec.WallThickness * 0.5f;
            float run = half.x * 2f - 0.3f;

            PipeX(root, "PipeN", new Vector3(0f, ceiling - 0.24f, face - 0.06f), run, 0.07f, PipeGrey);
            // Above the window heads (2.36m), which the first pass ran a pipe straight through.
            PipeX(root, "PipeS1", new Vector3(0f, ceiling - 0.10f, -face + 0.08f), run, 0.10f, PipeGrey);
            PipeX(root, "PipeS2", new Vector3(0f, ceiling - 0.20f, -face + 0.06f), run, 0.05f, new Color(0.38f, 0.40f, 0.38f));
            for (int i = 0; i < 7; i++)
            {
                float x = -9f + i * 3f;
                Trim(root, "PipeClipN_" + i, new Vector3(x, ceiling - 0.24f, face - 0.03f), new Vector3(0.03f, 0.1f, 0.06f), new Color(0.2f, 0.2f, 0.2f));
                Trim(root, "PipeClipS_" + i, new Vector3(x, ceiling - 0.16f, -face + 0.04f), new Vector3(0.03f, 0.14f, 0.08f), new Color(0.2f, 0.2f, 0.2f));
            }

            bool floor04 = zoneId == ZoneIds.Floor04;
            if (floor04)
                LiftCallPanel(root, zoneId, new Vector3(Floor04LiftX + 0.68f, 0f, -face + 0.01f), 180f);
            else
            {
                LiftCallPanel(root, zoneId, new Vector3(-half.x + 0.11f, 0f, 0.78f), -90f);
                Bin(root, "Bin_Lift", new Vector3(-half.x + 0.42f, 0f, 0.85f));
            }

            // By the stairs, against the south wall. The fifth floor's two are the loop's own
            // (N3-R08 / N5-R10) and must not have a twin standing next to them.
            if (zoneId != ZoneIds.Floor05)
            {
                float ex = zoneId == ZoneIds.Floor02 ? 9.35f : 8.6f;    // 2F has a window at 8
                Fixture(root, "Extinguisher", new Vector3(ex, 0.31f, -face + 0.14f), Kit.Extinguisher, 180f);
                SignPlate(root, "ExtinguisherSign", new Vector3(ex, 1.02f, -face + 0.01f), 0.34f, 0.12f, 180f,
                          "world.sign.extinguisher", ExtinguisherRed, SignWhite);
            }
        }

        /// <summary>The call buttons and the floor display above them, beside a lift door.</summary>
        void LiftCallPanel(Transform root, string zoneId, Vector3 wallPoint, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var front = rot * Vector3.back;
            var plate = Trim(root, "LiftCallPlate", wallPoint + new Vector3(0f, 1.2f, 0f) + front * 0.012f, new Vector3(0.13f, 0.32f, 0.024f), new Color(0.50f, 0.52f, 0.52f));
            plate.transform.localRotation = rot;
            SurfaceArt.Apply(plate, new Vector3(0.13f, 0.32f, 0.024f), S.Metal, new Color(0.62f, 0.64f, 0.64f), 0.004f);
            for (int i = 0; i < 2; i++)
            {
                var btn = Part(root, PrimitiveType.Cylinder, "LiftCallButton_" + i,
                               wallPoint + new Vector3(0f, 1.27f - i * 0.14f, 0f) + front * 0.028f,
                               new Vector3(0.045f, 0.012f, 0.045f), Color.white, false);
                btn.transform.localRotation = rot * Quaternion.Euler(90f, 0f, 0f);
                SurfaceArt.ApplyGlow(btn, new Color(0.55f, 0.45f, 0.25f) * (i == 0 ? 0.5f : 0.25f));
            }
            LiftDisplay(root, zoneId, wallPoint + new Vector3(0f, 1.52f, 0f), yaw);
        }

        /// <summary>The lift's floor display: a dark box with the floor in red.</summary>
        void LiftDisplay(Transform root, string zoneId, Vector3 wallPoint, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var front = rot * Vector3.back;
            var display = Trim(root, "LiftFloorDisplay", wallPoint + front * 0.018f, new Vector3(0.14f, 0.09f, 0.03f), new Color(0.04f, 0.04f, 0.04f));
            display.transform.localRotation = rot;
            string key = FloorSignKeyFor(zoneId);
            if (string.IsNullOrEmpty(key)) return;
            var led = BuildWorldLabel(root, "LiftFloorDisplayText", wallPoint + front * 0.036f, 0.12f, 0.07f, 40, yaw);
            led.text = Loc.T(key);
            led.color = LedRed;
            led.alignment = TextAnchor.MiddleCenter;
        }

        // =================================================================
        // floors
        // =================================================================

        /// <summary>2F: 주민공지 about the redevelopment, a plant somebody still waters, umbrellas.</summary>
        void ReferenceFloor02()
        {
            var root = OwnedRoot(ZoneIds.Floor02);
            if (root == null) return;
            float face = HalfOf(ZoneIds.Floor02).y - BuildingSpec.WallThickness * 0.5f;

            NoticeBoard(root, "ResidentsNotice", new Vector3(-8.3f, 1.5f, -face), 180f, "world.sign.residents_notice", 1.0f, 4);
            Poster(root, "RedevelopmentPoster", new Vector3(-2.2f, 1.55f, -face), 180f, "world.poster.redevelopment", 0.5f, 0.7f);
            PottedPlant(root, "Plant_2F_A", new Vector3(3.6f, 0f, -face + 0.3f));
            PottedPlant(root, "Plant_2F_B", new Vector3(-6.2f, 0f, face - 0.3f));
            UmbrellaStand(root, "Umbrellas_201", new Vector3(-8.35f, 0f, face - 0.25f));
        }

        /// <summary>
        /// 3F (sheet "3F 일반 주거층 (303~305호)"): the pipe shaft that carries sound between
        /// units where 301 was, Seon-ja's lived-in door at 303, the papered 304, the complaint
        /// household at 305 with its bags out and its note on the wall.
        /// </summary>
        void ReferenceFloor03()
        {
            var root = OwnedRoot(ZoneIds.Floor03);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Floor03);
            float face = half.y - BuildingSpec.WallThickness * 0.5f;
            float[] x = BuildingSpec.StandardDoorX;

            // The shaft: a steel access door, its sign, and the risers either side of it.
            var shaft = Trim(root, "PipeShaftDoor", new Vector3(x[0], 1.0f, face - 0.03f), new Vector3(0.8f, 2.0f, 0.05f), new Color(0.32f, 0.36f, 0.37f));
            SurfaceArt.Apply(shaft, new Vector3(0.8f, 2.0f, 0.05f), S.Metal, new Color(0.55f, 0.6f, 0.6f), 0.006f);
            for (int i = 0; i < 3; i++)
                Trim(root, "PipeShaftLouvre_" + i, new Vector3(x[0], 0.3f + i * 0.06f, face - 0.058f), new Vector3(0.5f, 0.02f, 0.01f), new Color(0.12f, 0.12f, 0.12f));
            Trim(root, "PipeShaftHandle", new Vector3(x[0] + 0.28f, 1.05f, face - 0.07f), new Vector3(0.03f, 0.14f, 0.04f), new Color(0.15f, 0.15f, 0.15f));
            SignPlate(root, "PipeShaftSign", new Vector3(x[0], 2.18f, face), 0.7f, 0.16f, 0f, "world.sign.pipe_shaft", new Color(0.16f, 0.22f, 0.30f), SignWhite);
            Paper(root, "PipeShaftNotice", new Vector3(x[0], 1.5f, face - 0.06f), 0f, 0.3f, 0.4f, "world.sign.staff_only", WarningRed);
            PipeY(root, "ShaftRiserA", new Vector3(x[0] - 0.62f, 1.3f, face - 0.07f), 2.6f, 0.09f, PipeGrey);
            PipeY(root, "ShaftRiserB", new Vector3(x[0] + 0.58f, 1.3f, face - 0.06f), 2.6f, 0.06f, new Color(0.40f, 0.34f, 0.28f));

            // Where 302 was: a note the residents put up.
            Paper(root, "QuietNote", new Vector3(x[1], 1.45f, face - 0.01f), 0f, 0.42f, 0.56f, "world.note.live_quietly", SignInk);

            // 303, lived in.
            ShoeRack(root, "ShoeRack_303", new Vector3(x[2] - 1.15f, 0f, face - 0.17f));
            PottedPlant(root, "Plant_303", new Vector3(x[2] + 0.95f, 0f, face - 0.25f));
            Fixture(root, "Parcel_303", new Vector3(x[2] + 1.45f, 0.16f, face - 0.3f), Kit.Package, 10f);

            // 305, the complaint household.
            TrashBag(root, "Bags_305A", new Vector3(x[4] + 1.1f, 0f, face - 0.3f), 0.95f);
            TrashBag(root, "Bags_305B", new Vector3(x[4] + 1.45f, 0f, face - 0.42f), 0.8f);
            Paper(root, "ComplaintNote", new Vector3(x[4] + 0.95f, 1.35f, face - 0.01f), 0f, 0.36f, 0.48f, "world.note.quiet_please", SignInk);

            NoticeBoard(root, "ResidentsNotice", new Vector3(0f, 1.5f, -face), 180f, "world.sign.residents_notice", 1.0f, 3);
        }

        /// <summary>
        /// 4F (sheet "4F 특이층"): the west end of the corridor is gone behind a temporary
        /// partition - plywood, polythene stapled over it, tape, the 관리사무소's notice - with
        /// the rubble of whatever was knocked down still on the floor and the ceiling open
        /// above it. The lift door moved to the south wall to make room; v5.1 already says
        /// the corridor is not the length the drawings give it.
        /// </summary>
        void ReferenceFloor04()
        {
            var root = OwnedRoot(ZoneIds.Floor04);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Floor04);
            float ceiling = HeightOf(ZoneIds.Floor04);
            float face = half.y - BuildingSpec.WallThickness * 0.5f;
            float wall = -half.x + BuildingSpec.WallThickness * 0.5f;     // the end wall's inner face

            // ---- the partition ----------------------------------------------------
            var ply = new Color(0.62f, 0.55f, 0.45f);
            for (int i = 0; i < 2; i++)
            {
                var board = Trim(root, "Partition_Ply_" + i, new Vector3(wall + 0.03f, 1.2f, -0.55f + i * 1.1f), new Vector3(0.02f, 2.4f, 1.08f), ply);
                SurfaceArt.Apply(board, new Vector3(0.02f, 2.4f, 1.08f), S.Plywood, Color.white, 0.003f);
                board.transform.localRotation = Quaternion.Euler(i == 0 ? 0.6f : -0.8f, 0f, i == 0 ? -0.5f : 0.4f);
            }
            for (int i = 0; i < 3; i++)
                Trim(root, "Partition_Batten_" + i, new Vector3(wall + 0.06f, 0.3f + i * 0.95f, 0f), new Vector3(0.04f, 0.07f, 2.2f), new Color(0.48f, 0.40f, 0.30f));

            // Polythene over all of it, sagging a little off true.
            var sheet = Trim(root, "Partition_Plastic", new Vector3(wall + 0.12f, (ceiling - 0.05f) * 0.5f, 0f), new Vector3(0.012f, ceiling - 0.05f, 2.16f), Color.white);
            SurfaceArt.Apply(sheet, new Vector3(0.012f, ceiling - 0.05f, 2.16f), S.Plastic, new Color(0.85f, 0.87f, 0.85f), 0f);
            sheet.transform.localRotation = Quaternion.Euler(0f, 0f, 1.2f);

            // Tape, corner to corner, and the notice.
            float tx = wall + 0.16f;
            Tape(root, "Partition_TapeA", new Vector3(tx, 0.35f, -1.0f), new Vector3(tx, 2.2f, 1.0f), Vector3.right);
            Tape(root, "Partition_TapeB", new Vector3(tx, 2.05f, -1.0f), new Vector3(tx, 0.5f, 1.0f), Vector3.right);
            Tape(root, "Partition_TapeC", new Vector3(tx + 0.005f, 1.05f, -1.05f), new Vector3(tx + 0.005f, 1.1f, 1.05f), Vector3.right);
            NoEntryNotice(root, new Vector3(tx + 0.012f, 1.5f, 0.05f), -90f);

            // ---- what came down ---------------------------------------------------
            var rng = new System.Random(404);
            for (int i = 0; i < 22; i++)
            {
                float sx = 0.06f + (float)rng.NextDouble() * 0.28f;
                float sy = 0.03f + (float)rng.NextDouble() * 0.12f;
                float sz = 0.06f + (float)rng.NextDouble() * 0.3f;
                var p = new Vector3(wall + 0.25f + (float)rng.NextDouble() * 1.4f, sy * 0.5f, -1.0f + (float)rng.NextDouble() * 2.0f);
                var chunk = Trim(root, "Debris_" + i, p, new Vector3(sx, sy, sz), Color.white);
                bool tile = i % 3 == 0;
                SurfaceArt.Apply(chunk, new Vector3(sx, sy, sz), tile ? S.Terrazzo : S.Concrete, new Color(0.8f, 0.8f, 0.78f), 0.004f);
                chunk.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 20f - 10f, (float)rng.NextDouble() * 180f, (float)rng.NextDouble() * 20f - 10f);
            }
            var panel = Trim(root, "FallenCeilingPanel", new Vector3(wall + 0.75f, 0.32f, 0.55f), new Vector3(0.6f, 0.015f, 0.6f), Color.white);
            SurfaceArt.Apply(panel, new Vector3(0.6f, 0.015f, 0.6f), S.Ceiling, Color.white, 0f);
            panel.transform.localRotation = Quaternion.Euler(0f, 25f, 58f);
            var board2 = Trim(root, "LooseBoard", new Vector3(wall + 0.55f, 0.75f, -0.75f), new Vector3(0.025f, 1.5f, 0.24f), ply);
            SurfaceArt.Apply(board2, new Vector3(0.025f, 1.5f, 0.24f), S.Plywood, Color.white, 0.003f);
            board2.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            Ladder(root, "Ladder", new Vector3(wall + 0.55f, 0f, 0.82f), 2.0f, 16f, 90f);
            Chair(root, "FallenChair", new Vector3(wall + 1.55f, 0.02f, -0.55f), 140f, true);

            // ---- the ceiling it fell from -----------------------------------------
            var hole = Trim(root, "CeilingHole", new Vector3(wall + 0.95f, ceiling - 0.006f, 0.2f), new Vector3(1.2f, 0.01f, 0.6f), Color.black);
            SurfaceArt.ApplyGlow(hole, new Color(0.008f, 0.008f, 0.01f));
            var hanging = Trim(root, "HangingPanel", new Vector3(wall + 1.35f, ceiling - 0.22f, -0.3f), new Vector3(0.6f, 0.015f, 0.6f), Color.white);
            SurfaceArt.Apply(hanging, new Vector3(0.6f, 0.015f, 0.6f), S.Ceiling, Color.white, 0f);
            hanging.transform.localRotation = Quaternion.Euler(-35f, 0f, 12f);
            Trim(root, "HangingWire", new Vector3(wall + 0.7f, ceiling - 0.35f, 0.4f), new Vector3(0.01f, 0.7f, 0.01f), new Color(0.08f, 0.08f, 0.08f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 8f);

            // Somebody wrote on the wall beside it.
            var scrawl = BuildWorldLabel(root, "Scrawl_404", new Vector3(-9.3f, 1.55f, face - 0.005f), 0.9f, 0.5f, 70, 0f);
            scrawl.text = Loc.T("world.scrawl.should_not_come");
            scrawl.color = new Color(0.12f, 0.12f, 0.12f, 0.75f);
            scrawl.alignment = TextAnchor.MiddleCenter;
            scrawl.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);   // the canvas carries the yaw

            // The off-grid stretch where 404 was plastered over: newer, different paint.
            var blank = root.Find("BlankWall404");
            if (blank != null && SurfaceArt.Enabled)
                SurfaceArt.Apply(blank.gameObject, blank.localScale, S.PeelingPaint, new Color(0.85f, 0.85f, 0.85f), 0f);

            // And the sealed service door gets the same treatment as the end wall.
            float sealX = BuildingSpec.Floor04DoorX[4];
            Tape(root, "SealedDoor_TapeA", new Vector3(sealX - 0.5f, 0.4f, face - 0.13f), new Vector3(sealX + 0.5f, 1.95f, face - 0.13f), Vector3.back);
            Tape(root, "SealedDoor_TapeB", new Vector3(sealX - 0.5f, 1.95f, face - 0.135f), new Vector3(sealX + 0.5f, 0.4f, face - 0.135f), Vector3.back);
        }

        /// <summary>5F: the long corridor that repeats (sheet 13F). Red caged lamps, and the stair sign.</summary>
        void ReferenceFloor05()
        {
            var root = OwnedRoot(ZoneIds.Floor05);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Floor05);
            float face = half.y - BuildingSpec.WallThickness * 0.5f;

            foreach (float x in new[] { -5.75f, -2.25f, 1.25f, 4.75f })
                EmergencyLamp(root, "EmergencyLamp_" + x, new Vector3(x, 2.15f, face));

            HangingSign(root, "EmergencyStairsSign", new Vector3(4.4f, 2.3f, 0f), 90f, 1.1f, "world.sign.emergency_stairs",
                        new Color(0.10f, 0.16f, 0.13f), SignWhite, HeightOf(ZoneIds.Floor05));
            NoticeBoard(root, "ResidentsNotice", new Vector3(0f, 1.5f, -face), 180f, "world.sign.residents_notice", 1.0f, 3);
        }

        /// <summary>6F: the top floor's ordinary corridor.</summary>
        void ReferenceFloor06()
        {
            var root = OwnedRoot(ZoneIds.Floor06);
            if (root == null) return;
            float face = HalfOf(ZoneIds.Floor06).y - BuildingSpec.WallThickness * 0.5f;
            NoticeBoard(root, "ResidentsNotice", new Vector3(0f, 1.5f, -face), 180f, "world.sign.residents_notice", 1.0f, 2);
            PottedPlant(root, "Plant_6F", new Vector3(-2.9f, 0f, -face + 0.3f));
        }

        // =================================================================
        // 1F lobby (sheet "1F 로비 / 관리사무소")
        // =================================================================

        /// <summary>
        /// The 관리사무소 window beside its door, the 우편함 and 게시판 signs, a poster, the
        /// floor painted on the wall by the stairs, the lift's display, an alarm bell, chairs.
        /// </summary>
        void ReferenceLobby()
        {
            var root = OwnedRoot(ZoneIds.Lobby);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Lobby);
            float w = -half.x + BuildingSpec.WallThickness * 0.5f;          // west wall face
            float e = half.x - BuildingSpec.WallThickness * 0.5f;           // east wall face
            float n = half.y - BuildingSpec.WallThickness * 0.5f;           // north wall face
            float s = -n;

            // ---- 관리사무소 window, south of its door (the door is at z -2) ------------
            const float winZ = -3.2f, winW = 1.15f, sill = 0.95f, head = 2.0f;
            float winH = head - sill, winY = (head + sill) * 0.5f;
            var frame = new Color(0.30f, 0.31f, 0.30f);
            // A lit office behind the blinds.
            var behind = Trim(root, "OfficeWindow_Back", new Vector3(w + 0.01f, winY, winZ), new Vector3(0.01f, winH, winW), Color.black);
            SurfaceArt.ApplyGlow(behind, new Color(0.07f, 0.065f, 0.05f));
            for (int i = 0; i < 14; i++)
                Trim(root, "OfficeWindow_Blind_" + i, new Vector3(w + 0.03f, sill + 0.06f + i * 0.068f, winZ), new Vector3(0.01f, 0.045f, winW - 0.04f), new Color(0.62f, 0.62f, 0.58f))
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            Trim(root, "OfficeWindow_Head", new Vector3(w + 0.04f, head + 0.03f, winZ), new Vector3(0.08f, 0.06f, winW + 0.12f), frame);
            Trim(root, "OfficeWindow_JambN", new Vector3(w + 0.04f, winY, winZ + winW * 0.5f + 0.03f), new Vector3(0.08f, winH, 0.06f), frame);
            Trim(root, "OfficeWindow_JambS", new Vector3(w + 0.04f, winY, winZ - winW * 0.5f - 0.03f), new Vector3(0.08f, winH, 0.06f), frame);
            Trim(root, "OfficeWindow_Mullion", new Vector3(w + 0.04f, winY, winZ), new Vector3(0.06f, winH, 0.04f), frame);
            var ledge = Trim(root, "OfficeWindow_Ledge", new Vector3(w + 0.16f, sill - 0.02f, winZ), new Vector3(0.3f, 0.04f, winW + 0.2f), new Color(0.36f, 0.30f, 0.24f));
            SurfaceArt.Apply(ledge, new Vector3(0.3f, 0.04f, winW + 0.2f), S.Wood, new Color(0.5f, 0.42f, 0.32f), 0.006f);
            SignPlate(root, "OfficeSign", new Vector3(w, 2.3f, -2.55f), 1.7f, 0.3f, -90f, "world.sign.office", new Color(0.12f, 0.17f, 0.24f), SignWhite);
            Poster(root, "SafetyPoster", new Vector3(w, 1.45f, -1.3f), -90f, "world.poster.safe_home", 0.36f, 0.5f);

            // ---- mailboxes, notices ----------------------------------------------------
            SignPlate(root, "MailboxSign", new Vector3(w, 2.36f, 1f), 0.9f, 0.2f, -90f, "world.sign.mailbox", new Color(0.62f, 0.63f, 0.60f), SignInk);
            NoticeBoard(root, "LobbyNoticeBoard", new Vector3(e, 1.5f, -1.05f), 90f, "world.sign.noticeboard", 1.0f, 4);   // clear of M06's return shelf
            Poster(root, "CleanPoster", new Vector3(1.8f, 1.55f, s), 180f, "world.poster.clean", 0.46f, 0.62f);

            // ---- by the stairs and the lift --------------------------------------------
            // Over the stair door, as on every corridor; the parcel lockers have the corner.
            var floorMark = BuildWorldLabel(root, "PaintedFloor_1F", new Vector3(4.5f, 2.33f, n - 0.005f), 0.9f, 0.4f, 130, 0f);
            floorMark.text = Loc.T("world.stairs.f1.short");
            floorMark.color = StencilPaint;
            floorMark.alignment = TextAnchor.MiddleCenter;
            AlarmBell(root, "AlarmBell", new Vector3(3.4f, 1.4f, n), 0f);
            // The call button here is N1-R11's (WorldBuilder.Subquests); only the display is added.
            LiftDisplay(root, ZoneIds.Lobby, new Vector3(2f, 2.25f, n - 0.01f), 0f);

            // ---- furniture ----------------------------------------------------------------
            Chair(root, "WaitingChairA", new Vector3(2.8f, 0f, s + 0.32f), 180f, false);
            Chair(root, "WaitingChairB", new Vector3(3.35f, 0f, s + 0.3f), 172f, false);
            Fixture(root, "Extinguisher", new Vector3(4.05f, 0.31f, s + 0.14f), Kit.Extinguisher, 180f);
            Bin(root, "Bin_Lobby", new Vector3(e - 0.3f, 0f, -3.45f));
        }

        // =================================================================
        // B1 (sheet "B1 지하주차장 / 기록실 / 기계실")
        // =================================================================

        void ReferenceParking()
        {
            var root = OwnedRoot(ZoneIds.Parking);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Parking);
            float ceiling = HeightOf(ZoneIds.Parking);
            float e = half.x - BuildingSpec.WallThickness * 0.5f;
            float n = half.y - BuildingSpec.WallThickness * 0.5f;

            // Hazard paint up the pillars and the level painted on them.
            foreach (var block in BasementLayout.ParkingBlocks(Storey))
            {
                if (!block.Name.StartsWith("Pillar")) continue;
                var c = block.Centre;
                var sleeve = Trim(root, block.Name + "_Hazard", new Vector3(c.x, 0.55f, c.y), new Vector3(block.Size.x + 0.02f, 1.1f, block.Size.y + 0.02f), Color.white);
                SurfaceArt.Apply(sleeve, new Vector3(block.Size.x + 0.02f, 1.1f, block.Size.y + 0.02f), S.Hazard, Color.white, 0.004f);
                float hz = block.Size.y * 0.5f + 0.006f;
                foreach (var side in new[] { -1f, 1f })
                {
                    var b1 = BuildWorldLabel(root, block.Name + "_B1_" + side, new Vector3(c.x, 1.85f, c.y + side * hz), 0.52f, 0.42f, 120, side < 0 ? 0f : 180f);
                    b1.text = Loc.T("world.stairs.b1.short");
                    b1.color = FloorPaint;
                    b1.alignment = TextAnchor.MiddleCenter;
                }
            }
            // The slow sign on the pillar the cars turn round.
            SpeedSign(root, new Vector3(9f, 1.25f, -4f - 0.31f), 0f);

            // Cones round the car in the fire lane, and by the shutter.
            Cone(root, "Cone_0", new Vector3(5.9f, 0f, -3.45f), 0f);
            Cone(root, "Cone_1", new Vector3(6.8f, 0f, -3.55f), 30f);
            Cone(root, "Cone_2", new Vector3(10.6f, 0f, -3.4f), 70f);
            Cone(root, "Cone_3", new Vector3(11.4f, 0f, 2.6f), 15f);

            // A roller shutter on the east wall (the old ramp), a barrier in front of it.
            Shutter(root, new Vector3(e, 0f, 0f), 3.6f, 2.45f);
            Barrier(root, "ShutterBarrier", new Vector3(e - 0.9f, 0f, -1.2f), 1.1f);

            // The mirror at the corner, the hanging direction sign, the drain across the aisle.
            ConvexMirror(root, new Vector3(e, 2.0f, n - 1.0f), 90f);
            HangingSign(root, "DirectionSign", new Vector3(-5.5f, 2.22f, 0f), 0f, 2.2f, "world.sign.parking_exit",
                        new Color(0.08f, 0.16f, 0.12f), SignWhite, ceiling);
            Grating(root, "AisleDrain", new Vector3(-0.5f, 0f, -1.6f), 8f);

            // Services under the slab.
            PipeX(root, "SlabPipeA", new Vector3(0f, ceiling - 0.22f, -2.6f), half.x * 2f - 0.4f, 0.16f, new Color(0.40f, 0.42f, 0.40f));
            PipeX(root, "SlabPipeB", new Vector3(0f, ceiling - 0.16f, 2.6f), half.x * 2f - 0.4f, 0.1f, new Color(0.55f, 0.20f, 0.16f));
            PipeX(root, "SlabPipeC", new Vector3(0f, ceiling - 0.3f, 2.85f), half.x * 2f - 0.4f, 0.06f, PipeGrey);

            var big = BuildWorldLabel(root, "PaintedB1_North", new Vector3(-6.5f, 1.7f, n - 0.005f), 1.4f, 0.9f, 200, 0f);
            big.text = Loc.T("world.stairs.b1.short");
            big.color = FloorPaint;
            big.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>The plant rooms: valve wheels on the pump pipes, a high-voltage notice on the boards.</summary>
        void ReferencePlant()
        {
            var machinery = OwnedRoot(ZoneIds.Machinery);
            if (machinery != null)
            {
                var half = HalfOf(ZoneIds.Machinery);
                for (int i = 0; i < 4; i += 2)
                    SignPlate(machinery, "HighVoltage_" + i, new Vector3(-3f + i * 1.5f, 1.55f, half.y - 0.5f), 0.4f, 0.16f, 0f,
                              "world.sign.high_voltage", new Color(0.80f, 0.64f, 0.12f), SignInk);
                PipeY(machinery, "MachineRiserA", new Vector3(half.x - 0.3f, 1.3f, half.y - 0.3f), 2.6f, 0.14f, new Color(0.55f, 0.20f, 0.16f));
                PipeY(machinery, "MachineRiserB", new Vector3(half.x - 0.6f, 1.3f, half.y - 0.25f), 2.6f, 0.09f, PipeGrey);
                Fixture(machinery, "MachineValve", new Vector3(half.x - 0.3f, 1.2f, half.y - 0.45f), Kit.Valve, 0f);
            }

            var pump = OwnedRoot(ZoneIds.PumpRoom);
            if (pump != null)
            {
                Fixture(pump, "PumpValve_0", new Vector3(1.0f, 1.05f, 2.22f), Kit.Valve, 0f);
                Drum(pump, "PumpDrum", new Vector3(-3.3f, 0f, -2.3f));
            }

            var archive = OwnedRoot(ZoneIds.Archive);
            if (archive != null)
            {
                var half = HalfOf(ZoneIds.Archive);
                // Boxes on the shelf runs, as on the sheet's 기록실 (내부).
                // Boards in front of each shelf run, the middle one at the height the 2009 bill
                // (E20) has always lain at, and nothing put down on top of the bill.
                var rng = new System.Random(2009);
                float front = half.y - 0.6f - 0.18f;
                for (int i = 0; i < 4; i++)
                    for (int level = 0; level < 3; level++)
                    {
                        float x = -2.2f + i * 1.5f;
                        float top = 0.53f + level * 0.61f;
                        Trim(archive, "ArchiveBoard_" + i + "_" + level, new Vector3(x, top - 0.01f, front + 0.02f), new Vector3(1.24f, 0.02f, 0.4f), new Color(0.30f, 0.31f, 0.31f));
                        if (i == 0 && level == 1) continue;
                        for (int k = 0; k < 2; k++)
                        {
                            if (rng.NextDouble() < 0.2) continue;
                            Fixture(archive, "ArchiveBox_" + i + "_" + level + "_" + k,
                                    new Vector3(x - 0.28f + k * 0.56f, top + 0.16f, front), Kit.Package, (float)rng.NextDouble() * 10f - 5f);
                        }
                    }
            }
        }

        /// <summary>
        /// The 4F service passage (sheet "4F 서비스 통로 / 숨겨진 404 흔적"): risers, a valve
        /// wheel, pressure gauges, the staff-only sign at the way in, and 404 painted on the
        /// wall with an arrow at the steel door.
        /// </summary>
        void ReferenceServicePassage()
        {
            var root = OwnedRoot(ZoneIds.ServicePassage);
            if (root == null) return;
            var half = HalfOf(ZoneIds.ServicePassage);
            float ceiling = HeightOf(ZoneIds.ServicePassage);
            float s = -half.y + BuildingSpec.WallThickness * 0.5f;
            float n = -s;

            PipeY(root, "RiserA", new Vector3(-5.2f, ceiling * 0.5f, s + 0.06f), ceiling, 0.09f, new Color(0.45f, 0.30f, 0.20f));
            PipeY(root, "RiserB", new Vector3(-0.8f, ceiling * 0.5f, s + 0.05f), ceiling, 0.07f, PipeGrey);
            PipeY(root, "RiserC", new Vector3(2.4f, ceiling * 0.5f, s + 0.06f), ceiling, 0.1f, new Color(0.55f, 0.20f, 0.16f));
            PipeX(root, "OverheadA", new Vector3(0f, ceiling - 0.12f, s + 0.08f), half.x * 2f - 0.2f, 0.1f, PipeGrey);
            PipeX(root, "OverheadB", new Vector3(0f, ceiling - 0.1f, n - 0.07f), half.x * 2f - 0.2f, 0.06f, new Color(0.40f, 0.34f, 0.28f));
            Fixture(root, "PassageValve", new Vector3(-5.2f, 1.25f, s + 0.16f), Kit.Valve, 180f);
            Fixture(root, "PassageGaugeA", new Vector3(-1.5f, 1.5f, s + 0.08f), Kit.Gauge, 180f);
            Fixture(root, "PassageGaugeB", new Vector3(-1.1f, 1.3f, s + 0.08f), Kit.Gauge, 180f);
            Fixture(root, "PassageGaugeC", new Vector3(-0.7f, 1.55f, s + 0.08f), Kit.Gauge, 180f);

            Paper(root, "PassageStaffOnly", new Vector3(-5.45f, 1.6f, n - 0.01f), 0f, 0.4f, 0.3f, "world.sign.staff_only", WarningRed);
            var arrow = BuildWorldLabel(root, "Painted404", new Vector3(3.6f, 1.55f, n - 0.005f), 1.0f, 0.55f, 150, 0f);
            arrow.text = Loc.T("world.paint.404_this_way");
            arrow.color = FloorPaint;      // pale paint: this concrete is darker than the stencil
            arrow.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>Unit 404 (the same sheet's "숨겨진 공간의 흔적"): the number, in red, on its own wall.</summary>
        void ReferenceUnit404()
        {
            var root = OwnedRoot(ZoneIds.Unit404);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Unit404);
            var red = BuildWorldLabel(root, "Red404", new Vector3(-half.x + BuildingSpec.WallThickness * 0.5f + 0.005f, 1.55f, 0.35f), 0.9f, 0.5f, 140, -90f);
            red.text = "404";
            red.color = new Color(0.40f, 0.04f, 0.03f, 0.85f);
            red.alignment = TextAnchor.MiddleCenter;
            red.transform.localRotation = Quaternion.Euler(0f, 0f, 3f);       // relative to its canvas
        }

        /// <summary>An alarm bell with its red lamp beside every landing door, as on each sheet's 계단실.</summary>
        void ReferenceStairwell()
        {
            var root = OwnedRoot(ZoneIds.Stairwell);
            if (root == null) return;
            var half = HalfOf(ZoneIds.Stairwell);
            float wall = half.y - BuildingSpec.WallThickness * 0.5f;
            foreach (var floor in FloorPlan.All)
            {
                bool north = StairShaft.IsNorth(floor.FloorId);
                float h = StairShaft.HeightOf(floor.FloorId);
                AlarmBell(root, "AlarmBell_" + floor.FloorId, new Vector3(-1.0f, h + 1.35f, north ? wall : -wall), north ? 0f : 180f);
            }
        }

        // =================================================================
        // building blocks
        // =================================================================

        /// <summary>A pipe along x. Rusted steel, no collider.</summary>
        GameObject PipeX(Transform root, string name, Vector3 centre, float length, float diameter, Color colour)
        {
            var go = Part(root, PrimitiveType.Cylinder, name, centre, new Vector3(diameter, length, diameter), colour, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (SurfaceArt.Enabled) go.GetComponent<MeshRenderer>().sharedMaterial = SurfaceArt.Lit(S.Metal, colour * 1.6f);
            return go;
        }

        /// <summary>A vertical pipe.</summary>
        GameObject PipeY(Transform root, string name, Vector3 centre, float length, float diameter, Color colour)
        {
            var go = Part(root, PrimitiveType.Cylinder, name, centre, new Vector3(diameter, length, diameter), colour, false);
            if (SurfaceArt.Enabled) go.GetComponent<MeshRenderer>().sharedMaterial = SurfaceArt.Lit(S.Metal, colour * 1.6f);
            return go;
        }

        /// <summary>One of the subquest kits as plain dressing: same look, no interaction, no collider.</summary>
        GameObject Fixture(Transform root, string name, Vector3 position, Kit kit, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BuildKit(go.transform, kit);
            return go;
        }

        /// <summary>An empty parent, so a built-up thing can be turned as one.</summary>
        static Transform Group(Transform root, string name, Vector3 position, float yaw)
        {
            var go = new GameObject(name).transform;
            go.SetParent(root, false);
            go.localPosition = position;
            go.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <summary>
        /// A sign: a plate on the wall with its text in front of it. <paramref name="wallPoint"/>
        /// is on the wall face; yaw follows the label convention (the way the reader looks).
        /// </summary>
        void SignPlate(Transform root, string name, Vector3 wallPoint, float width, float height, float yaw,
                       string key, Color plate, Color ink)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var front = rot * Vector3.back;
            var box = Trim(root, name, wallPoint + front * 0.012f, new Vector3(width, height, 0.02f), plate);
            box.transform.localRotation = rot;
            var label = BuildWorldLabel(root, name + "_Text", wallPoint + front * 0.024f, width * 0.92f, height * 0.8f, 96, yaw);
            label.text = Loc.T(key);
            label.color = ink;
            label.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>A sheet of paper taped to a wall with something written on it.</summary>
        void Paper(Transform root, string name, Vector3 wallPoint, float yaw, float width, float height, string key, Color ink)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var front = rot * Vector3.back;
            var sheet = Trim(root, name, wallPoint + front * 0.004f, new Vector3(width, height, 0.004f), PaperWhite);
            sheet.transform.localRotation = rot * Quaternion.Euler(0f, 0f, 1.5f);
            var label = BuildWorldLabel(root, name + "_Text", wallPoint + front * 0.009f, width * 0.86f, height * 0.8f, 64, yaw);
            label.text = Loc.T(key);
            label.color = ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.transform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);   // relative to its canvas
        }

        /// <summary>A printed poster: a light sheet, a dark band, the slogan.</summary>
        void Poster(Transform root, string name, Vector3 wallPoint, float yaw, string key, float width, float height)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var front = rot * Vector3.back;
            Trim(root, name, wallPoint + front * 0.004f, new Vector3(width, height, 0.004f), new Color(0.74f, 0.73f, 0.66f)).transform.localRotation = rot;
            Trim(root, name + "_Band", wallPoint + front * 0.007f + Vector3.up * (height * 0.38f), new Vector3(width, height * 0.12f, 0.003f), new Color(0.16f, 0.30f, 0.40f))
                .transform.localRotation = rot;
            var label = BuildWorldLabel(root, name + "_Text", wallPoint + front * 0.01f - Vector3.up * (height * 0.06f), width * 0.82f, height * 0.62f, 72, yaw);
            label.text = Loc.T(key);
            label.color = new Color(0.14f, 0.20f, 0.26f);
            label.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>A cork board in a wooden frame, a header, and papers pinned to it.</summary>
        void NoticeBoard(Transform root, string name, Vector3 wallPoint, float yaw, string headerKey, float width, int papers)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var front = rot * Vector3.back;
            var right = rot * Vector3.right;
            float height = width * 0.68f;
            var frame = Trim(root, name, wallPoint + front * 0.015f, new Vector3(width, height, 0.03f), new Color(0.30f, 0.24f, 0.18f));
            frame.transform.localRotation = rot;
            SurfaceArt.Apply(frame, new Vector3(width, height, 0.03f), S.Wood, new Color(0.55f, 0.45f, 0.35f), 0.006f);
            Trim(root, name + "_Cork", wallPoint + front * 0.031f - Vector3.up * 0.03f, new Vector3(width - 0.08f, height - 0.14f, 0.004f), new Color(0.40f, 0.31f, 0.22f))
                .transform.localRotation = rot;
            var header = BuildWorldLabel(root, name + "_Header", wallPoint + front * 0.034f + Vector3.up * (height * 0.5f - 0.06f), width * 0.6f, 0.08f, 48, yaw);
            header.text = Loc.T(headerKey);
            header.color = new Color(0.82f, 0.80f, 0.72f);
            header.alignment = TextAnchor.MiddleCenter;

            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < papers; i++)
            {
                float px = -width * 0.36f + (width * 0.72f) * (papers == 1 ? 0.5f : i / (float)(papers - 1));
                float py = -0.05f + (float)rng.NextDouble() * 0.08f - (i % 2) * 0.06f;
                float pw = 0.16f + (float)rng.NextDouble() * 0.06f, ph = 0.22f + (float)rng.NextDouble() * 0.06f;
                var sheet = Trim(root, name + "_Paper_" + i, wallPoint + front * 0.036f + right * px + Vector3.up * py,
                                 new Vector3(pw, ph, 0.003f), i % 3 == 1 ? new Color(0.80f, 0.74f, 0.55f) : PaperWhite);
                sheet.transform.localRotation = rot * Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 8f - 4f);
                for (int l = 0; l < 4; l++)
                    Trim(root, name + "_Paper_" + i + "_L" + l, wallPoint + front * 0.039f + right * px + Vector3.up * (py + ph * 0.3f - l * 0.045f),
                         new Vector3(pw * 0.7f, 0.008f, 0.002f), new Color(0.32f, 0.32f, 0.34f)).transform.localRotation = rot;
            }
        }

        /// <summary>The 관리사무소's notice on the partition: 출입금지 in red, why, and who says so.</summary>
        void NoEntryNotice(Transform root, Vector3 at, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var front = rot * Vector3.back;
            var sheet = Trim(root, "NoEntryNotice", at, new Vector3(0.9f, 0.62f, 0.004f), new Color(0.80f, 0.78f, 0.70f));
            sheet.transform.localRotation = rot * Quaternion.Euler(0f, 0f, -2f);
            var title = BuildWorldLabel(root, "NoEntryNotice_Title", at + front * 0.006f + Vector3.up * 0.13f, 0.82f, 0.26f, 110, yaw);
            title.text = Loc.T("world.sign.no_entry");
            title.color = WarningRed;
            title.alignment = TextAnchor.MiddleCenter;
            var detail = BuildWorldLabel(root, "NoEntryNotice_Detail", at + front * 0.006f - Vector3.up * 0.08f, 0.78f, 0.14f, 40, yaw);
            detail.text = Loc.T("world.sign.no_entry_detail");
            detail.color = SignInk;
            detail.alignment = TextAnchor.MiddleCenter;
            var by = BuildWorldLabel(root, "NoEntryNotice_By", at + front * 0.006f - Vector3.up * 0.22f, 0.5f, 0.08f, 32, yaw);
            by.text = Loc.T("world.sign.signed_office");
            by.color = SignInk;
            by.alignment = TextAnchor.MiddleCenter;
            // Relative to each label's canvas, which already faces the reader.
            foreach (var t in new[] { title, detail, by }) t.transform.localRotation = Quaternion.Euler(0f, 0f, -2f);
        }

        /// <summary>
        /// Red and white barrier tape from a to b, lying flat against a wall whose normal is
        /// <paramref name="wallNormal"/>.
        /// </summary>
        void Tape(Transform root, string name, Vector3 a, Vector3 b, Vector3 wallNormal)
        {
            var d = b - a;
            float length = d.magnitude;
            int n = Mathf.Max(1, Mathf.RoundToInt(length / 0.18f));
            var rot = Quaternion.LookRotation(d.normalized, wallNormal);
            for (int i = 0; i < n; i++)
            {
                var piece = Trim(root, name + "_" + i, a + d * ((i + 0.5f) / n), new Vector3(0.075f, 0.003f, length / n),
                                 i % 2 == 0 ? new Color(0.70f, 0.08f, 0.06f) : new Color(0.82f, 0.82f, 0.78f));
                piece.transform.localRotation = rot;
            }
        }

        /// <summary>A sign hung from the ceiling on two rods.</summary>
        void HangingSign(Transform root, string name, Vector3 centre, float yaw, float width, string key, Color plate, Color ink, float ceiling)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var right = rot * Vector3.right;
            var box = Trim(root, name, centre, new Vector3(width, 0.26f, 0.035f), plate);
            box.transform.localRotation = rot;
            float rod = ceiling - (centre.y + 0.13f);
            foreach (var side in new[] { -1f, 1f })
                Trim(root, name + "_Rod" + side, centre + right * (side * width * 0.4f) + Vector3.up * (0.13f + rod * 0.5f), new Vector3(0.012f, rod, 0.012f), new Color(0.2f, 0.2f, 0.2f));
            foreach (var facing in new[] { 0f, 180f })
            {
                var r = Quaternion.Euler(0f, yaw + facing, 0f);
                var label = BuildWorldLabel(root, name + "_Text" + facing, centre + (r * Vector3.back) * 0.02f, width * 0.9f, 0.2f, 72, yaw + facing);
                label.text = Loc.T(key);
                label.color = ink;
                label.alignment = TextAnchor.MiddleCenter;
            }
        }

        /// <summary>The emergency bell box on the wall, with its red lamp lit.</summary>
        void AlarmBell(Transform root, string name, Vector3 wallPoint, float yaw)
        {
            var g = Group(root, name, wallPoint, yaw);
            Piece(g, "Box", new Vector3(0f, 0f, -0.05f), new Vector3(0.3f, 0.4f, 0.1f), new Color(0.55f, 0.10f, 0.08f));
            Round(g, "Bell", new Vector3(0f, -0.05f, -0.11f), new Vector3(0.16f, 0.03f, 0.16f), new Color(0.65f, 0.62f, 0.55f), 90f);
            Piece(g, "Plate", new Vector3(0f, 0.12f, -0.102f), new Vector3(0.2f, 0.06f, 0.004f), new Color(0.80f, 0.78f, 0.72f));
            var lamp = Part(g, PrimitiveType.Sphere, "Lamp", new Vector3(0f, 0.26f, -0.06f), new Vector3(0.08f, 0.08f, 0.08f), Color.white, false);
            SurfaceArt.ApplyGlow(lamp, new Color(1f, 0.08f, 0.05f) * 2.2f);
        }

        /// <summary>The fifth floor's red bulkhead lamp: a cage, a red glass, a little red light.</summary>
        void EmergencyLamp(Transform root, string name, Vector3 wallPoint)
        {
            var g = Group(root, name, wallPoint, 0f);
            Piece(g, "Base", new Vector3(0f, 0f, -0.03f), new Vector3(0.18f, 0.24f, 0.06f), new Color(0.15f, 0.15f, 0.15f));
            var glass = Part(g, PrimitiveType.Cylinder, "Glass", new Vector3(0f, 0f, -0.1f), new Vector3(0.12f, 0.1f, 0.12f), Color.white, false);
            glass.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SurfaceArt.ApplyGlow(glass, new Color(0.9f, 0.06f, 0.04f) * 1.8f);
            for (int i = 0; i < 3; i++)
                Piece(g, "Cage" + i, new Vector3(-0.05f + i * 0.05f, 0f, -0.16f), new Vector3(0.01f, 0.16f, 0.01f), new Color(0.12f, 0.12f, 0.12f));

            var lightGo = new GameObject("RedLight");
            lightGo.transform.SetParent(g, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.05f, -0.3f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.12f, 0.08f);
            light.intensity = 0.5f;
            light.range = 2.6f;
            light.shadows = LightShadows.None;
        }

        /// <summary>A meter cabinet: grey steel, a dark window onto the dials.</summary>
        void MeterBox(Transform root, float x, float doorZ)
        {
            float face = doorZ - 0.04f;
            var box = Trim(root, "MeterBox_" + x, new Vector3(x, 1.7f, face - 0.06f), new Vector3(0.32f, 0.42f, 0.12f), new Color(0.50f, 0.52f, 0.50f));
            SurfaceArt.Apply(box, new Vector3(0.32f, 0.42f, 0.12f), S.Metal, new Color(0.8f, 0.82f, 0.8f), 0.008f);
            var window = Trim(root, "MeterWindow_" + x, new Vector3(x, 1.76f, face - 0.122f), new Vector3(0.16f, 0.12f, 0.004f), Color.black);
            SurfaceArt.ApplyGlow(window, new Color(0.03f, 0.035f, 0.035f));
            Trim(root, "MeterConduit_" + x, new Vector3(x, 2.15f, face - 0.03f), new Vector3(0.03f, 0.48f, 0.03f), new Color(0.38f, 0.38f, 0.36f));
        }

        void Bin(Transform root, string name, Vector3 floorPoint)
        {
            var body = Part(root, PrimitiveType.Cylinder, name, floorPoint + Vector3.up * 0.3f, new Vector3(0.36f, 0.6f, 0.36f), BinBlue, false);
            if (SurfaceArt.Enabled) body.GetComponent<MeshRenderer>().sharedMaterial = SurfaceArt.Lit(S.Metal, BinBlue * 2f);
            Part(root, PrimitiveType.Cylinder, name + "_Lid", floorPoint + Vector3.up * 0.62f, new Vector3(0.38f, 0.04f, 0.38f), new Color(0.20f, 0.26f, 0.30f), false);
        }

        void PottedPlant(Transform root, string name, Vector3 floorPoint)
        {
            Part(root, PrimitiveType.Cylinder, name + "_Pot", floorPoint + Vector3.up * 0.16f, new Vector3(0.3f, 0.32f, 0.3f), new Color(0.36f, 0.24f, 0.18f), false);
            Part(root, PrimitiveType.Cylinder, name + "_Soil", floorPoint + Vector3.up * 0.315f, new Vector3(0.27f, 0.01f, 0.27f), new Color(0.10f, 0.08f, 0.06f), false);
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < 9; i++)
            {
                float yaw = i * 40f + (float)rng.NextDouble() * 20f;
                float lean = 18f + (float)rng.NextDouble() * 30f;
                var leaf = Trim(root, name + "_Leaf" + i, floorPoint + Vector3.up * 0.52f, new Vector3(0.07f, 0.42f + (float)rng.NextDouble() * 0.2f, 0.006f),
                                new Color(0.13f + (float)rng.NextDouble() * 0.05f, 0.22f, 0.10f));
                leaf.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(lean, 0f, 0f);
                leaf.transform.localPosition += leaf.transform.localRotation * new Vector3(0f, 0.12f, 0f);
            }
        }

        void UmbrellaStand(Transform root, string name, Vector3 floorPoint)
        {
            Part(root, PrimitiveType.Cylinder, name, floorPoint + Vector3.up * 0.25f, new Vector3(0.22f, 0.5f, 0.22f), new Color(0.22f, 0.26f, 0.28f), false);
            for (int i = 0; i < 2; i++)
            {
                var u = Part(root, PrimitiveType.Cylinder, name + "_Umbrella" + i, floorPoint + new Vector3(i * 0.04f - 0.02f, 0.5f, 0f),
                             new Vector3(0.06f, 0.85f, 0.06f), i == 0 ? new Color(0.08f, 0.08f, 0.09f) : new Color(0.20f, 0.16f, 0.30f), false);
                u.transform.localRotation = Quaternion.Euler(i == 0 ? 6f : -4f, 0f, i == 0 ? 8f : -10f);
            }
        }

        void ShoeRack(Transform root, string name, Vector3 floorPoint)
        {
            var frame = new Color(0.24f, 0.28f, 0.30f);
            for (int side = -1; side <= 1; side += 2)
                Trim(root, name + "_Side" + side, floorPoint + new Vector3(side * 0.33f, 0.4f, 0f), new Vector3(0.025f, 0.8f, 0.28f), frame);
            var rng = new System.Random(303);
            for (int level = 0; level < 3; level++)
            {
                float y = 0.1f + level * 0.27f;
                Trim(root, name + "_Shelf" + level, floorPoint + new Vector3(0f, y, 0f), new Vector3(0.66f, 0.02f, 0.28f), frame);
                for (int k = 0; k < 3; k++)
                {
                    if (rng.NextDouble() < 0.25) continue;
                    var c = new[] { new Color(0.10f, 0.10f, 0.10f), new Color(0.45f, 0.38f, 0.30f), new Color(0.55f, 0.55f, 0.52f) }[rng.Next(3)];
                    Trim(root, name + "_Shoe" + level + "_" + k, floorPoint + new Vector3(-0.2f + k * 0.2f, y + 0.045f, 0.02f), new Vector3(0.1f, 0.07f, 0.24f), c);
                }
            }
        }

        void TrashBag(Transform root, string name, Vector3 floorPoint, float scale)
        {
            var bag = Part(root, PrimitiveType.Sphere, name, floorPoint + Vector3.up * 0.19f * scale, new Vector3(0.46f, 0.4f, 0.42f) * scale, new Color(0.05f, 0.05f, 0.06f), false);
            bag.transform.localRotation = Quaternion.Euler(0f, scale * 90f, 8f);
            Part(root, PrimitiveType.Cylinder, name + "_Knot", floorPoint + Vector3.up * 0.42f * scale, new Vector3(0.07f, 0.08f, 0.07f) * scale, new Color(0.05f, 0.05f, 0.06f), false);
        }

        /// <summary>An old school-style chair; fallen over if asked.</summary>
        void Chair(Transform root, string name, Vector3 floorPoint, float yaw, bool fallen)
        {
            var g = Group(root, name, floorPoint, yaw);
            if (fallen) g.localRotation *= Quaternion.Euler(-82f, 0f, 0f);
            var seat = Piece(g, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.03f, 0.4f), new Color(0.42f, 0.32f, 0.22f));
            SurfaceArt.Apply(seat, new Vector3(0.42f, 0.03f, 0.4f), S.Wood, new Color(0.7f, 0.55f, 0.4f), 0.004f);
            var back = Piece(g, "Back", new Vector3(0f, 0.75f, 0.18f), new Vector3(0.4f, 0.22f, 0.025f), new Color(0.42f, 0.32f, 0.22f));
            SurfaceArt.Apply(back, new Vector3(0.4f, 0.22f, 0.025f), S.Wood, new Color(0.7f, 0.55f, 0.4f), 0.004f);
            var steel = new Color(0.22f, 0.24f, 0.25f);
            foreach (var p in new[] { new Vector2(-0.18f, -0.17f), new Vector2(0.18f, -0.17f), new Vector2(-0.18f, 0.17f), new Vector2(0.18f, 0.17f) })
                Piece(g, "Leg" + p.x + p.y, new Vector3(p.x, 0.22f, p.y), new Vector3(0.025f, 0.45f, 0.025f), steel);
            Piece(g, "BackPostL", new Vector3(-0.18f, 0.66f, 0.18f), new Vector3(0.025f, 0.42f, 0.025f), steel);
            Piece(g, "BackPostR", new Vector3(0.18f, 0.66f, 0.18f), new Vector3(0.025f, 0.42f, 0.025f), steel);
        }

        /// <summary>A step ladder leaning on whatever is behind it.</summary>
        void Ladder(Transform root, string name, Vector3 floorPoint, float length, float lean, float yaw)
        {
            var g = Group(root, name, floorPoint, yaw);
            g.localRotation *= Quaternion.Euler(-lean, 0f, 0f);
            var wood = new Color(0.50f, 0.42f, 0.30f);
            foreach (var side in new[] { -0.2f, 0.2f })
                Piece(g, "Rail" + side, new Vector3(side, length * 0.5f, 0f), new Vector3(0.04f, length, 0.06f), wood);
            for (int i = 0; i < 6; i++)
                Piece(g, "Rung" + i, new Vector3(0f, 0.3f + i * 0.3f, 0f), new Vector3(0.4f, 0.03f, 0.04f), wood);
        }

        void Drum(Transform root, string name, Vector3 floorPoint)
        {
            var body = Part(root, PrimitiveType.Cylinder, name, floorPoint + Vector3.up * 0.44f, new Vector3(0.56f, 0.88f, 0.56f), new Color(0.24f, 0.32f, 0.38f), false);
            if (SurfaceArt.Enabled) body.GetComponent<MeshRenderer>().sharedMaterial = SurfaceArt.Lit(S.Metal, new Color(0.7f, 0.8f, 0.9f));
            for (int i = 0; i < 2; i++)
                Part(root, PrimitiveType.Cylinder, name + "_Hoop" + i, floorPoint + Vector3.up * (0.3f + i * 0.3f), new Vector3(0.58f, 0.02f, 0.58f), new Color(0.2f, 0.22f, 0.24f), false);
        }

        // ---- the car park's things ----------------------------------------------------

        static Mesh _cone;

        /// <summary>A unit cone, base at y = -0.5, apex at +0.5 (Unity has no cone primitive).</summary>
        static Mesh ConeMesh()
        {
            if (_cone != null) return _cone;
            const int Seg = 20;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i <= Seg; i++)
            {
                float a = i / (float)Seg * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var normal = (dir + Vector3.up * 0.5f).normalized;
                v.Add(dir * 0.5f + Vector3.down * 0.5f); n.Add(normal); uv.Add(new Vector2(i / (float)Seg, 0f));
                v.Add(dir * 0.08f + Vector3.up * 0.5f); n.Add(normal); uv.Add(new Vector2(i / (float)Seg, 1f));
            }
            for (int i = 0; i < Seg; i++)
            {
                int a = i * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 2);
                t.Add(a + 1); t.Add(a + 3); t.Add(a + 2);
            }
            _cone = new Mesh { name = "Cone" };
            _cone.SetVertices(v); _cone.SetNormals(n); _cone.SetUVs(0, uv); _cone.SetTriangles(t, 0);
            _cone.RecalculateBounds();
            return _cone;
        }

        void Cone(Transform root, string name, Vector3 floorPoint, float yaw)
        {
            var g = Group(root, name, floorPoint, yaw);
            Piece(g, "Base", new Vector3(0f, 0.015f, 0f), new Vector3(0.36f, 0.03f, 0.36f), new Color(0.12f, 0.12f, 0.12f));
            var body = new GameObject("Body");
            body.transform.SetParent(g, false);
            body.transform.localPosition = new Vector3(0f, 0.31f, 0f);
            body.transform.localScale = new Vector3(0.3f, 0.56f, 0.3f);
            body.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            body.AddComponent<MeshRenderer>().sharedMaterial = SurfaceArt.Enabled
                ? SurfaceArt.Lit(S.Grain, new Color(0.85f, 0.32f, 0.08f)) : MaterialFor(new Color(0.85f, 0.32f, 0.08f));
            Part(g, PrimitiveType.Cylinder, "Band", new Vector3(0f, 0.37f, 0f), new Vector3(0.16f, 0.05f, 0.16f), new Color(0.85f, 0.85f, 0.82f), false);
        }

        void ConvexMirror(Transform root, Vector3 wallPoint, float yaw)
        {
            var g = Group(root, "ConvexMirror", wallPoint, yaw);
            Piece(g, "Arm", new Vector3(0f, 0f, -0.12f), new Vector3(0.04f, 0.04f, 0.24f), new Color(0.2f, 0.2f, 0.2f));
            var rim = Round(g, "Rim", new Vector3(0f, 0f, -0.25f), new Vector3(0.66f, 0.05f, 0.66f), new Color(0.85f, 0.35f, 0.08f), 90f);
            var glass = Round(g, "Glass", new Vector3(0f, 0f, -0.28f), new Vector3(0.58f, 0.03f, 0.58f), new Color(0.35f, 0.38f, 0.40f), 90f);
            if (SurfaceArt.Enabled) glass.GetComponent<MeshRenderer>().sharedMaterial = SurfaceArt.Lit(S.Metal, new Color(1.2f, 1.25f, 1.3f));
        }

        void SpeedSign(Transform root, Vector3 faceCentre, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var front = rot * Vector3.back;
            Trim(root, "SlowSign_Border", faceCentre + front * 0.006f, new Vector3(0.4f, 0.62f, 0.008f), new Color(0.62f, 0.08f, 0.06f)).transform.localRotation = rot;
            Trim(root, "SlowSign", faceCentre + front * 0.012f, new Vector3(0.34f, 0.56f, 0.006f), new Color(0.82f, 0.82f, 0.78f)).transform.localRotation = rot;
            var slow = BuildWorldLabel(root, "SlowSign_Word", faceCentre + front * 0.017f + Vector3.up * 0.15f, 0.3f, 0.14f, 64, yaw);
            slow.text = Loc.T("world.sign.slow");
            slow.color = WarningRed;
            slow.alignment = TextAnchor.MiddleCenter;
            var limit = BuildWorldLabel(root, "SlowSign_Limit", faceCentre + front * 0.017f - Vector3.up * 0.08f, 0.3f, 0.24f, 90, yaw);
            limit.text = Loc.T("world.sign.speed_limit");
            limit.color = SignInk;
            limit.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>A roller shutter on an east wall: the drum box, the curtain, its slats, its rails.</summary>
        void Shutter(Transform root, Vector3 wallPoint, float width, float height)
        {
            float x = wallPoint.x;
            var curtain = Trim(root, "Shutter", new Vector3(x - 0.03f, height * 0.5f, wallPoint.z), new Vector3(0.04f, height, width), Color.white);
            SurfaceArt.Apply(curtain, new Vector3(0.04f, height, width), S.Metal, new Color(0.85f, 0.9f, 0.9f), 0f);
            for (int i = 0; i < 26; i++)
                Trim(root, "ShutterSlat_" + i, new Vector3(x - 0.055f, 0.08f + i * (height - 0.16f) / 25f, wallPoint.z), new Vector3(0.012f, 0.018f, width - 0.02f), new Color(0.22f, 0.24f, 0.24f));
            var drum = Trim(root, "ShutterHead", new Vector3(x - 0.12f, height + 0.08f, wallPoint.z), new Vector3(0.24f, 0.18f, width + 0.2f), Color.white);
            SurfaceArt.Apply(drum, new Vector3(0.24f, 0.18f, width + 0.2f), S.Metal, new Color(0.7f, 0.75f, 0.75f), 0.01f);
            foreach (var side in new[] { -1f, 1f })
                Trim(root, "ShutterRail" + side, new Vector3(x - 0.06f, height * 0.5f, wallPoint.z + side * (width * 0.5f + 0.04f)), new Vector3(0.1f, height, 0.07f), new Color(0.30f, 0.32f, 0.32f));
            Trim(root, "ShutterBottom", new Vector3(x - 0.07f, 0.04f, wallPoint.z), new Vector3(0.08f, 0.08f, width), new Color(0.2f, 0.2f, 0.2f));
        }

        /// <summary>A 주차 방지봉: two posts and a rail in hazard paint.</summary>
        void Barrier(Transform root, string name, Vector3 floorPoint, float width)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var post = Trim(root, name + "_Post" + side, floorPoint + new Vector3(0f, 0.38f, side * width * 0.5f), new Vector3(0.06f, 0.76f, 0.06f), Color.white);
                SurfaceArt.Apply(post, new Vector3(0.06f, 0.76f, 0.06f), S.Hazard, Color.white, 0.006f);
            }
            var rail = Trim(root, name + "_Rail", floorPoint + new Vector3(0f, 0.77f, 0f), new Vector3(0.06f, 0.06f, width + 0.06f), Color.white);
            SurfaceArt.Apply(rail, new Vector3(0.06f, 0.06f, width + 0.06f), S.Hazard, Color.white, 0.006f);
        }

        /// <summary>A trench drain across the floor: the slot and its bars.</summary>
        void Grating(Transform root, string name, Vector3 floorPoint, float length)
        {
            var slot = Decal(root, name, floorPoint + Vector3.up * 0.004f, new Vector3(length, 0.008f, 0.3f), Color.black);
            SurfaceArt.ApplyGlow(slot, new Color(0.01f, 0.012f, 0.012f));
            Decal(root, name + "_EdgeA", floorPoint + new Vector3(0f, 0.006f, 0.16f), new Vector3(length, 0.01f, 0.025f), new Color(0.32f, 0.33f, 0.32f));
            Decal(root, name + "_EdgeB", floorPoint + new Vector3(0f, 0.006f, -0.16f), new Vector3(length, 0.01f, 0.025f), new Color(0.32f, 0.33f, 0.32f));
            int bars = Mathf.RoundToInt(length / 0.12f);
            for (int i = 0; i < bars; i++)
                Decal(root, name + "_Bar" + i, floorPoint + new Vector3(-length * 0.5f + (i + 0.5f) * length / bars, 0.008f, 0f),
                      new Vector3(0.02f, 0.012f, 0.3f), new Color(0.28f, 0.29f, 0.28f));
        }
    }
}
