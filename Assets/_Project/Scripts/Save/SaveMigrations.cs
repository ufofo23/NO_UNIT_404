using NO404.Core;

namespace NO404.Save
{
    /// <summary>
    /// Forward-only schema upgrades (GDD 20.17 / 22.2). Each step is small and idempotent so
    /// a save written by any shipped build keeps loading.
    /// </summary>
    public static class SaveMigrations
    {
        public static bool TryMigrate(SaveData data)
        {
            if (data == null) return false;
            if (data.schemaVersion > SaveData.CurrentSchemaVersion)
            {
                Log.Error("Save", "save is from a newer build (schema " + data.schemaVersion + ")");
                return false;
            }

            while (data.schemaVersion < SaveData.CurrentSchemaVersion)
            {
                switch (data.schemaVersion)
                {
                    case 1: MigrateV1ToV2(data); break;
                    case 2: MigrateV2ToV3(data); break;
                    case 3: MigrateV3ToV4(data); break;
                    case 4: MigrateV4ToV5(data); break;
                    case 5: MigrateV5ToV6(data); break;
                    case 6: MigrateV6ToV7(data); break;
                    case 7: MigrateV7ToV8(data); break;
                    case 8: MigrateV8ToV9(data); break;
                    case 9: MigrateV9ToV10(data); break;
                    case 10: MigrateV10ToV11(data); break;
                    default:
                        Log.Error("Save", "no migration from schema " + data.schemaVersion);
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// v10 replaced the visitor verdict with an access level (v3.0 38.2).
        ///
        /// The stored int meant VisitorDecision and now means VisitorAccessLevel, and the two
        /// enums disagree on almost every number - a save written yesterday would read every
        /// "let them in" as "turn them away" and every "hold" as "hold", which is the worst
        /// kind of migration bug because two thirds of it looks right.
        ///
        /// Admit becomes FloorPass rather than LobbyOnly: the old model had one way in, and it
        /// was the permissive one. Calling security and refusing were the same outcome for the
        /// person outside, so both land on Reject.
        /// </summary>
        static void MigrateV9ToV10(SaveData data)
        {
            if (data.visitors != null)
            {
                for (int i = 0; i < data.visitors.Count; i++)
                {
                    var entry = data.visitors[i];
                    if (entry == null) continue;

                    switch (entry.decision)
                    {
                        case 1: entry.decision = (int)Visitors.VisitorAccessLevel.FloorPass; break;   // Admit
                        case 2: entry.decision = (int)Visitors.VisitorAccessLevel.Hold; break;        // Hold
                        case 3: entry.decision = (int)Visitors.VisitorAccessLevel.Reject; break;      // Deny
                        case 4: entry.decision = (int)Visitors.VisitorAccessLevel.Reject; break;      // CallSecurity
                        default: entry.decision = (int)Visitors.VisitorAccessLevel.Pending; break;
                    }
                }
            }

            data.schemaVersion = 10;
            Log.Info("Save", "migrated save 9 -> 10");
        }

        /// <summary>v2 added the hidden stats and the fired-anomaly list.</summary>
        static void MigrateV1ToV2(SaveData data)
        {
            EnsureStat(data, StatIds.ArchiveIntegrity, 0);
            EnsureStat(data, StatIds.ChairmanAlert, 0);
            EnsureStat(data, StatIds.HarinResonance, 0);
            if (data.firedAnomalies == null) data.firedAnomalies = new System.Collections.Generic.List<string>();
            data.schemaVersion = 2;
            Log.Info("Save", "migrated save 1 -> 2");
        }

        /// <summary>v3 replaced the old per-night stat reset with persistent access levels.</summary>
        static void MigrateV2ToV3(SaveData data)
        {
            if (data.accessLevels == null) data.accessLevels = new System.Collections.Generic.List<int>();
            if (data.accessLevels.Count == 0) data.accessLevels.Add((int)AccessLevel.Staff1);
            data.schemaVersion = 3;
            Log.Info("Save", "migrated save 2 -> 3");
        }

        /// <summary>
        /// v4 added the night's counter-pressure and the night power reserve (GDD 15.4 / 15.5).
        /// An older save resumes with a calm corridor and a full reserve, which is generous but
        /// never wrong: neither value can be reconstructed from what schema 3 recorded.
        /// </summary>
        static void MigrateV3ToV4(SaveData data)
        {
            data.pressure = 0;
            data.pressureNightIndex = data.nightIndex;
            if (data.pressureIntruders == null)
                data.pressureIntruders = new System.Collections.Generic.List<string>();
            data.powerReserve = Facility.NightPowerService.Full;
            data.corridorLightsOn = true;
            data.breakerResetsUsed = 0;
            data.schemaVersion = 4;
            Log.Info("Save", "migrated save 3 -> 4");
        }

        /// <summary>
        /// v5 deleted night 0 (GDD 9.1). The prologue is gone and the game opens on night 1,
        /// so a save written mid-prologue points at a shift that no longer has any content:
        /// no cases, no visitors, no anomalies, and a case list whose only entry is a version
        /// of C00 that has been rewritten to belong to a different night.
        ///
        /// Such a save is lifted to the start of night 1 rather than discarded (CLAUDE.md: old
        /// schemas are migrated, never thrown away). The prologue was twenty-five minutes and
        /// the shift it becomes is the one the player was about to reach anyway, so what is
        /// lost is a quarter of an hour and nothing that any later night reads. Anything
        /// already earned - stats, flags, access, evidence - is left exactly as it was.
        ///
        /// Saves from night 1 onward are untouched: their night index already means what it
        /// still means.
        /// </summary>
        static void MigrateV4ToV5(SaveData data)
        {
            if (data.nightIndex <= 0)
            {
                data.nightIndex = 1;
                data.pressureNightIndex = 1;
                data.powerReserve = Facility.NightPowerService.StartingReserveFor(1);
                Log.Info("Save", "a night 0 save was lifted onto night 1; the prologue is gone");
            }

            data.schemaVersion = 5;
            Log.Info("Save", "migrated save 4 -> 5");
        }

        /// <summary>
        /// v6 added the anomaly log (GDD 12.3 / 3.2). An older save resumes with an empty one:
        /// what the caretaker filed before this existed was never recorded anywhere, so there
        /// is nothing to reconstruct it from, and an empty log costs the player nothing they
        /// can see - the entries come back the next time they file one of those types.
        /// </summary>
        static void MigrateV5ToV6(SaveData data)
        {
            if (data.cataloguedAnomalies == null)
                data.cataloguedAnomalies = new System.Collections.Generic.List<int>();

            data.schemaVersion = 6;
            Log.Info("Save", "migrated save 5 -> 6");
        }

        /// <summary>
        /// v7 is the v2.1 building compression (spec 0.7 / 0.11).
        ///
        /// Two things in an older save name floors that no longer exist. The zone ids are a
        /// rename: what was the eighth floor is now the third, because every piece of content
        /// that stood on it moved there (spec 0.11). The thirteenth is not a rename at all -
        /// it stopped being a floor and became an anomaly - so a save that caught the player
        /// standing on it is put back in the lobby rather than into a space that only exists
        /// while something is wrong. That costs a walk and cannot strand anyone.
        ///
        /// The risk counters are not defaulted here. They are absent from an older save
        /// because nothing had happened to raise them, and GameStateService reads a missing
        /// stat as zero - which is the correct answer, not a fallback.
        /// </summary>
        static void MigrateV6ToV7(SaveData data)
        {
            RenameZone(data, "Floor08", ZoneIds.Floor03);

            // The one zone that did not survive as a place.
            if (data.player != null && data.player.zoneId == "Floor13")
            {
                data.player.zoneId = ZoneIds.Lobby;
                data.player.posX = data.player.posY = data.player.posZ = 0f;
                Log.Info("Save", "a save taken on the thirteenth floor was returned to the lobby");
            }
            RenameZone(data, "Floor13", ZoneIds.PhantomFloor13);

            if (data.manualPages == null) data.manualPages = new System.Collections.Generic.List<string>();
            if (data.manualEvents == null)
                data.manualEvents = new System.Collections.Generic.List<ManualEventSaveEntry>();

            // An older save was never in a stairwell that had landings, so it resumes on the
            // floor its zone sits on, outside the shaft.
            if (data.stairs == null) data.stairs = new StairSaveEntry();
            string floor = Gameplay.FloorPlan.FloorOfZone(data.player != null ? data.player.zoneId : null);
            data.stairs.entryFloor = floor ?? Gameplay.FloorPlan.F1;
            data.stairs.currentLanding = data.stairs.entryFloor;
            data.stairs.inStairwell = false;
            data.stairs.flightsWalked = 0;

            data.schemaVersion = 7;
            Log.Info("Save", "migrated save 6 -> 7");
        }

        /// <summary>
        /// v8 adds the five anomalous tools (spec 23).
        ///
        /// Nothing is reconstructed. A save written before the machines existed is a save in
        /// which no machine had been used, so an empty list is the truth rather than a
        /// default - and the debts the machines charge live in the stats, where a missing
        /// entry already reads as zero.
        /// </summary>
        static void MigrateV7ToV8(SaveData data)
        {
            if (data.anomalyTools == null)
                data.anomalyTools = new System.Collections.Generic.List<AnomalyToolSaveEntry>();

            data.schemaVersion = 8;
            Log.Info("Save", "migrated save 7 -> 8");
        }

        /// <summary>
        /// v9 stores what a night deferred to the next one (spec 30.3).
        ///
        /// An older save cannot say what was owed - the queue only ever existed in memory, so
        /// a save written mid-shift lost it and a save written between shifts had already
        /// spent it. Coming back empty is therefore the same behaviour that save already had,
        /// and it is the only honest answer: inventing a consequence the player never earned
        /// would be worse than the one they quietly escaped.
        /// </summary>
        static void MigrateV8ToV9(SaveData data)
        {
            if (data.deferredConsequences == null)
                data.deferredConsequences = new System.Collections.Generic.List<Cases.ConsequenceDefinition>();

            data.schemaVersion = 9;
            Log.Info("Save", "migrated save 8 -> 9");
        }

        /// <summary>
        /// v11 is v5.0: the caretaker gained a body and a nerve, and three stats changed scale.
        ///
        /// ArchiveIntegrity, ChairmanAlert and HarinResonance ran 0..10 under v3.0 and run
        /// 0..100 under v5.0 5.2, so the stored numbers are multiplied by ten. That is the
        /// only reading that preserves what the save meant: a player who had pushed the
        /// chairman to 8 of 10 was two thirds of the way to being watched, and leaving the 8
        /// alone would have quietly handed them that back.
        ///
        /// ArchiveIntegrity is the exception to the exception. Its new starting value is 50
        /// rather than 0, so a fresh v3.0 save reads as zero archive where v5.0 would have
        /// begun at the middle. Old saves keep their earned amount on top of the new floor -
        /// scaled first, then offset - because the alternative is telling somebody who spent
        /// six nights preserving records that they have none.
        ///
        /// HP and SAN start full. A save from before they existed cannot say what the night
        /// cost, and inventing a wound the player never took would be worse than forgiving one.
        /// </summary>
        static void MigrateV10ToV11(SaveData data)
        {
            RescaleStat(data, StatIds.ChairmanAlert, 10, 0);
            RescaleStat(data, StatIds.HarinResonance, 10, 0);
            RescaleStat(data, StatIds.ArchiveIntegrity, 10, 50);

            EnsureStat(data, DebtIds.Access, 0);
            EnsureStat(data, DebtIds.Safety, 0);
            EnsureStat(data, DebtIds.Record, 0);
            EnsureStat(data, DebtIds.Distortion, 0);
            EnsureStat(data, DebtIds.Trust, 0);

            if (data.choices == null) data.choices = new System.Collections.Generic.List<ChoiceSaveEntry>();
            if (data.selectedQuests == null) data.selectedQuests = new System.Collections.Generic.List<string>();

            data.hp = Core.VitalService.Max;
            data.san = Core.VitalService.Max;
            data.firstAidRemaining = Core.VitalService.FirstAidKitsPerCampaign;
            data.groundingRemaining = Core.VitalService.GroundingPerNight;

            // Zero is not a seed. A campaign that predates the pool draws one on its next
            // night start rather than replaying somebody else's shuffle.
            data.campaignSeed = 0;

            data.schemaVersion = 11;
            Log.Info("Save", "migrated save 10 -> 11");
        }

        /// <summary>
        /// Moves a stat onto a new scale, clamped to what the new scale allows.
        /// </summary>
        static void RescaleStat(SaveData data, string statId, int factor, int floor)
        {
            for (int i = 0; i < data.stats.Count; i++)
            {
                if (data.stats[i].key != statId) continue;

                int scaled = floor + data.stats[i].value * factor;
                int max = StatIds.MaxOf(statId);
                data.stats[i].value = scaled < 0 ? 0 : (scaled > max ? max : scaled);
                return;
            }

            EnsureStat(data, statId, floor);
        }

        /// <summary>Rewrite one zone id everywhere a save can hold one.</summary>
        static void RenameZone(SaveData data, string from, string to)
        {
            if (data.player != null && data.player.zoneId == from) data.player.zoneId = to;

            if (data.visitedZones != null)
                for (int i = 0; i < data.visitedZones.Count; i++)
                    if (data.visitedZones[i] == from) data.visitedZones[i] = to;
        }

        static void EnsureStat(SaveData data, string statId, int defaultValue)
        {
            for (int i = 0; i < data.stats.Count; i++) if (data.stats[i].key == statId) return;
            data.stats.Add(new StatSaveEntry { key = statId, value = defaultValue });
        }
    }
}
