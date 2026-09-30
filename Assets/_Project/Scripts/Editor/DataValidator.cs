using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using NO404.Cases;
using NO404.CCTV;
using NO404.ContentData;
using NO404.Core;
using NO404.Anomalies;
using NO404.Gameplay;
using NO404.Visitors;

namespace NO404.EditorTools
{
    /// <summary>
    /// Content integrity check (GDD 21.6 required editor tools). Catches the failure classes
    /// that would otherwise show up as a soft-lock during a playtest:
    /// dangling ids, unreachable dialogue nodes, cases with no way to be resolved, missing
    /// localization keys and P0 cases without a fail-safe.
    /// </summary>
    public static class DataValidator
    {
        [MenuItem("Tools/NO404/Data/Validate Content", priority = 50)]
        public static void Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            var localization = new LocalizationService();
            localization.Initialize("ko");

            var content = new ContentDatabase();
            content.Load();

            ValidateCases(content, localization, errors, warnings);
            ValidateEvidence(content, localization, errors);
            ValidateDialogues(content, localization, errors, warnings);
            ValidateVisitors(content, localization, errors);
            ValidateResidents(content, localization, errors);
            ValidateCctv(content, localization, errors, warnings);
            ValidatePhoneCalls(content, localization, errors);
            ValidateEndings(content, localization, errors, warnings);
            ValidateNightCoverage(content, errors, warnings);
            ValidateUniqueIds(content, errors);
            ValidateVisitorsAreSolvable(content, errors);
            ValidateVisitorsAreReadable(content, localization, errors, warnings);
            ValidateNightlyCallerCeiling(content, errors);
            ValidateVisitorRoutes(content, errors, warnings);
            ValidateManualPages(content, localization, errors);
            ValidateManualEvents(content, localization, errors, warnings);
            ValidateAnomalyTools(content, localization, errors, warnings);
            ValidateGrantedEvidenceExists(content, errors);

            Report(errors, warnings);
        }

        /// <summary>
        /// Every page in the night manual (v2.1 spec 0.9.3), and the rule that makes the
        /// system fair: a page has to say what the caretaker must not do and what they must
        /// do, or it is a page that cannot be followed.
        /// </summary>
        static void ValidateManualPages(ContentDatabase content, LocalizationService loc,
                                        List<string> errors)
        {
            foreach (var page in content.ManualPages)
            {
                var id = page.pageId;

                RequireKey(loc, page.titleKey, "manual page " + id + " title", errors);

                if (page.stepKeys == null || page.stepKeys.Length == 0)
                    errors.Add("manual page " + id + " has no procedure, so it cannot be followed");

                CheckKeys(loc, page.observationKeys, "manual page " + id + " observation", errors);
                CheckKeys(loc, page.prohibitionKeys, "manual page " + id + " prohibition", errors);
                CheckKeys(loc, page.stepKeys, "manual page " + id + " step", errors);
                CheckKeys(loc, page.noteKeys, "manual page " + id + " note", errors);

                // Spec 0.10.5: a lethal outcome is only allowed where the manual actually
                // printed the warning, so an index pointing past the list would let an event
                // kill on a rule that is not written anywhere.
                for (int i = 0; i < page.lethalProhibitions.Length; i++)
                {
                    int index = page.lethalProhibitions[i];
                    if (index < 0 || page.prohibitionKeys == null || index >= page.prohibitionKeys.Length)
                        errors.Add("manual page " + id + " marks prohibition " + index +
                                   " lethal, but has no such prohibition");
                }
            }
        }

        /// <summary>
        /// The M01..M18 events (spec 22), against the two promises spec 32 makes about them:
        /// every one has a page, and every one has a way back from a wrong answer.
        /// </summary>
        static void ValidateManualEvents(ContentDatabase content, LocalizationService loc,
                                         List<string> errors, List<string> warnings)
        {
            foreach (var id in ManualEventIds.All)
                if (content.FindManualEvent(id) == null)
                    errors.Add("missing manual event " + id + " (spec 22)");

            foreach (var definition in content.ManualEvents)
            {
                var id = definition.eventId;

                RequireKey(loc, definition.nameKey, "manual event " + id + " name", errors);
                RequireKey(loc, definition.summaryKey, "manual event " + id + " summary", errors);

                // Spec 32: M01..M18 each have a prior hint or a manual page.
                var page = content.FindManualPage(definition.manualPageId);
                if (page == null)
                    errors.Add("manual event " + id + " has no manual page (" +
                               definition.manualPageId + "); spec 0.9.2 forbids an unhinted answer");
                else if (page.eventId != id)
                    errors.Add("manual event " + id + " points at page " + page.pageId +
                               ", which belongs to " + page.eventId);

                if (!FloorPlan.Exists(definition.floorId))
                    errors.Add("manual event " + id + " is on '" + definition.floorId +
                               "', which is not a floor (spec 0.7.1)");

                if (definition.objectives == null || definition.objectives.Length == 0)
                    errors.Add("manual event " + id + " has no objectives");

                var seen = new HashSet<string>();
                for (int i = 0; i < definition.objectives.Length; i++)
                {
                    var objective = definition.objectives[i];
                    if (objective == null) { errors.Add("manual event " + id + " has a null objective"); continue; }
                    if (!seen.Add(objective.objectiveId))
                        errors.Add("manual event " + id + " has duplicate objective " + objective.objectiveId);
                    RequireKey(loc, objective.titleKey,
                               "manual event " + id + " objective " + objective.objectiveId, errors);
                }

                if (definition.correctConditions == null || definition.correctConditions.Length == 0)
                    warnings.Add("manual event " + id + " has no correct conditions, so any " +
                                 "completed run counts as correct");

                // Spec 32: at least one recovery route per event. M13 is the exception the
                // spec allows - a postcard cannot be got wrong twice, it is read or it is not.
                bool recoverable = definition.failSafe != null && definition.failSafe.afterWrongAttempts > 0;
                if (!recoverable && id != ManualEventIds.M13_Postcards)
                    errors.Add("manual event " + id + " has no fail-safe (spec 0.5 / 32)");

                if (recoverable) RequireKey(loc, definition.failSafe.notifyKey,
                                            "manual event " + id + " fail-safe", errors);

                // Spec 0.10.5: opting in to a lethal outcome needs the warning to exist.
                if (definition.lethalOnRepeat && (page == null || !page.HasLethalProhibition))
                    errors.Add("manual event " + id + " may be lethal, but its page marks no " +
                               "prohibition lethal (spec 0.10.5)");

                CheckConsequenceFloors(definition.onCorrect, id, errors);
                CheckConsequenceFloors(definition.onWrong, id, errors);
                CheckConsequenceFloors(definition.onPartial, id, errors);

                CheckRules(definition.correctConditions, definition, page, loc, errors);
                CheckRules(definition.partialConditions, definition, page, loc, errors);
            }

            ValidateManualChains(content, errors);
            ValidateStartConditionsAreReachable(content, errors);
        }

        /// <summary>
        /// Every event can actually be reached (v2.1 spec 0.3 / 0.5).
        ///
        /// A night is a chain: one head that opens with the shift, and each later event handed
        /// over when the one before it closes. The failure this catches is an event that is in
        /// the content, passes every other check, and is simply never started - which is what
        /// happened to M01 when it was gated on a flag nothing set. Nothing about the event
        /// itself looks wrong; only walking the chain shows it.
        /// </summary>
        static void ValidateManualChains(ContentDatabase content, List<string> errors)
        {
            var byId = new Dictionary<string, ManualEventDefinition>();
            foreach (var definition in content.ManualEvents) byId[definition.eventId] = definition;

            var headsPerNight = new Dictionary<int, List<string>>();

            foreach (var definition in content.ManualEvents)
            {
                var id = definition.eventId;

                if (definition.trigger == ManualEventTrigger.NightStart)
                {
                    List<string> heads;
                    if (!headsPerNight.TryGetValue(definition.nightIndex, out heads))
                        headsPerNight[definition.nightIndex] = heads = new List<string>();
                    heads.Add(id);
                    continue;
                }

                if (definition.trigger != ManualEventTrigger.AfterEvent) continue;

                ManualEventDefinition predecessor;
                if (!byId.TryGetValue(definition.triggerTarget ?? string.Empty, out predecessor))
                {
                    errors.Add("manual event " + id + " follows '" + definition.triggerTarget +
                               "', which is not an event");
                    continue;
                }

                if (predecessor.nightIndex != definition.nightIndex)
                    errors.Add("manual event " + id + " (night " + definition.nightIndex +
                               ") follows " + predecessor.eventId + " on night " +
                               predecessor.nightIndex + ", so it can never start");

                // Walk back to a head. A chain that loops never starts at all.
                var seen = new HashSet<string> { id };
                var step = predecessor;
                while (step != null && step.trigger == ManualEventTrigger.AfterEvent)
                {
                    if (!seen.Add(step.eventId))
                    {
                        errors.Add("manual events " + id + " and " + step.eventId +
                                   " are in a chain that loops back on itself");
                        break;
                    }
                    byId.TryGetValue(step.triggerTarget ?? string.Empty, out step);
                }
            }

            // Every night that has events needs exactly one thing that opens without being
            // asked. Two heads is a night that fires two anomalies at once; none is a night
            // where nothing ever happens.
            var nights = new HashSet<int>();
            foreach (var definition in content.ManualEvents) nights.Add(definition.nightIndex);

            foreach (var night in nights)
            {
                List<string> heads;
                if (!headsPerNight.TryGetValue(night, out heads) || heads.Count == 0)
                {
                    errors.Add("night " + night + " has manual events but nothing that starts them");
                    continue;
                }

                // M13 is a deliberate second head: spec 22 gives it one postcard a night,
                // independent of whatever else the night is doing.
                int chainHeads = 0;
                for (int i = 0; i < heads.Count; i++)
                    if (heads[i] != ManualEventIds.M13_Postcards) chainHeads++;

                if (chainHeads > 1)
                    errors.Add("night " + night + " has " + chainHeads + " chain heads (" +
                               string.Join(", ", heads.ToArray()) + "); it should have one");
            }
        }

        /// <summary>
        /// A flag an event waits on has to be one something can set.
        ///
        /// This is the check that was missing when M01 shipped unreachable. A start condition
        /// reading a flag that no consequence anywhere writes is not a gate, it is a wall, and
        /// it is invisible in every other view of the content.
        /// </summary>
        static void ValidateStartConditionsAreReachable(ContentDatabase content, List<string> errors)
        {
            var settable = new HashSet<string>();

            foreach (var definition in content.Cases)
            {
                CollectSetFlags(definition.consequences, settable);
                if (definition.decisions != null)
                    for (int i = 0; i < definition.decisions.Length; i++)
                        if (definition.decisions[i] != null)
                            CollectSetFlags(definition.decisions[i].consequences, settable);
            }

            foreach (var definition in content.ManualEvents)
            {
                CollectSetFlags(definition.onCorrect, settable);
                CollectSetFlags(definition.onPartial, settable);
                CollectSetFlags(definition.onWrong, settable);
            }

            // Flags the loop itself sets as a night opens, rather than any piece of content.
            settable.Add(FlagIds.Knows404);
            settable.Add(FlagIds.PrologueDone);

            foreach (var definition in content.ManualEvents)
            {
                var conditions = definition.startConditions;
                if (conditions == null) continue;

                for (int i = 0; i < conditions.Length; i++)
                {
                    var condition = conditions[i];
                    if (condition == null || condition.type != ConditionType.FlagEquals) continue;
                    if (!condition.boolValue) continue;   // waiting for a flag to be false is free

                    if (!settable.Contains(condition.keyA))
                        errors.Add("manual event " + definition.eventId + " waits for flag " +
                                   condition.keyA + ", which nothing ever sets");
                }
            }
        }

        static void CollectSetFlags(ConsequenceDefinition[] consequences, HashSet<string> into)
        {
            if (consequences == null) return;
            for (int i = 0; i < consequences.Length; i++)
            {
                var c = consequences[i];
                if (c != null && c.type == ConsequenceType.SetFlag && c.boolValue)
                    into.Add(c.targetId);
            }
        }

        /// <summary>
        /// A judgement clause that names a manual rule has to name one the page actually
        /// prints (spec 0.10.2 B).
        ///
        /// Getting this wrong is invisible in play and unfair in effect: the caretaker is
        /// charged with a violation, and the manual entry it refers to is not there to be
        /// read. Cross-checking the key against the page it came from is the only way to
        /// notice, because both halves look perfectly reasonable on their own.
        /// </summary>
        static void CheckRules(ManualCheckDefinition[] checks, ManualEventDefinition definition,
                               Manual.ManualPage page, LocalizationService loc, List<string> errors)
        {
            if (checks == null) return;

            for (int i = 0; i < checks.Length; i++)
            {
                var check = checks[i];
                if (check == null) continue;

                if (string.IsNullOrEmpty(check.counterId))
                    errors.Add("manual event " + definition.eventId + " has a check with no counter");

                if (string.IsNullOrEmpty(check.ruleKey)) continue;

                RequireKey(loc, check.ruleKey, "manual event " + definition.eventId + " rule", errors);

                if (page == null) continue;

                bool printed = false;
                for (int p = 0; p < page.prohibitionKeys.Length; p++)
                    if (page.prohibitionKeys[p] == check.ruleKey) printed = true;

                if (!printed)
                    errors.Add("manual event " + definition.eventId + " charges a violation of " +
                               check.ruleKey + ", which is not a prohibition on " + page.pageId);
            }
        }

        /// <summary>
        /// The five anomalous tools A01..A05 (v2.1 spec 23).
        ///
        /// What this really guards is the one promise the tools make: a caretaker who is stuck
        /// can always reach one. A menu whose every line navigates in a circle, an option
        /// pointing at a screen nobody wrote, a machine whose night never comes - none of these
        /// look wrong in the data, and every one is a dead end at the exact moment the player
        /// went looking for a way out.
        /// </summary>
        static void ValidateAnomalyTools(ContentDatabase content, LocalizationService loc,
                                         List<string> errors, List<string> warnings)
        {
            for (int i = 0; i < ManualEventIds.Tools.Length; i++)
            {
                var id = ManualEventIds.Tools[i];
                if (content.FindAnomalyTool(id) == null)
                    errors.Add("missing anomaly tool " + id + " (spec 23)");
            }

            foreach (var tool in content.AnomalyTools)
            {
                var id = tool.toolId;

                RequireKey(loc, tool.nameKey, "anomaly tool " + id + " name", errors);
                RequireKey(loc, tool.dormantKey, "anomaly tool " + id + " dormant prompt", errors);
                RequireKey(loc, tool.unlockNotifyKey, "anomaly tool " + id + " unlock notice", errors);

                if (!FloorPlan.Exists(tool.floorId))
                    errors.Add("anomaly tool " + id + " is on '" + tool.floorId +
                               "', which is not a floor (spec 0.7.1)");

                if (System.Array.IndexOf(ZoneIds.All, tool.zoneId) < 0)
                    errors.Add("anomaly tool " + id + " is in unknown zone " + tool.zoneId);

                // Spec 21 gives every machine a night. One with none can never be used, and
                // nothing else in the data would say so.
                if (tool.unlockNight <= 0)
                    errors.Add("anomaly tool " + id + " never becomes available (spec 21)");

                if (tool.RootMenu == null)
                {
                    errors.Add("anomaly tool " + id + " has no root menu " + tool.rootMenuId);
                    continue;
                }

                ValidateToolMenus(tool, loc, errors, warnings);
                ValidateToolHold(tool, loc, errors);
            }
        }

        static void ValidateToolMenus(AnomalyToolDefinition tool, LocalizationService loc,
                                      List<string> errors, List<string> warnings)
        {
            var id = tool.toolId;
            var menuIds = new HashSet<string>();

            for (int m = 0; m < tool.menus.Length; m++)
            {
                var menu = tool.menus[m];
                if (menu == null) { errors.Add("anomaly tool " + id + " has a null menu"); continue; }

                if (!menuIds.Add(menu.menuId))
                    errors.Add("anomaly tool " + id + " has duplicate menu " + menu.menuId);

                RequireKey(loc, menu.titleKey, "anomaly tool " + id + " menu " + menu.menuId, errors);
                if (!string.IsNullOrEmpty(menu.bodyKey))
                    RequireKey(loc, menu.bodyKey,
                               "anomaly tool " + id + " menu " + menu.menuId + " body", errors);

                var optionIds = new HashSet<string>();
                bool hasExit = false;

                for (int o = 0; o < menu.options.Length; o++)
                {
                    var option = menu.options[o];
                    if (option == null)
                    {
                        errors.Add("anomaly tool " + id + " menu " + menu.menuId + " has a null option");
                        continue;
                    }

                    string where = "anomaly tool " + id + " option " + menu.menuId + "/" + option.optionId;

                    if (!optionIds.Add(option.optionId))
                        errors.Add("anomaly tool " + id + " menu " + menu.menuId +
                                   " has duplicate option " + option.optionId);

                    RequireKey(loc, option.labelKey, where, errors);
                    if (!string.IsNullOrEmpty(option.resultKey))
                        RequireKey(loc, option.resultKey, where + " result", errors);

                    if (!string.IsNullOrEmpty(option.nextMenuId) && tool.FindMenu(option.nextMenuId) == null)
                        errors.Add(where + " opens menu " + option.nextMenuId + ", which does not exist");

                    if (!string.IsNullOrEmpty(option.grantsFlagId) &&
                        System.Array.IndexOf(ItemIds.All, option.grantsFlagId) < 0)
                        errors.Add(where + " grants unknown item " + option.grantsFlagId);

                    if (!string.IsNullOrEmpty(option.consumesFlagId) &&
                        System.Array.IndexOf(ItemIds.All, option.consumesFlagId) < 0)
                        errors.Add(where + " spends unknown item " + option.consumesFlagId);

                    if (option.opensHold && tool.hold == AnomalyToolHold.None)
                        errors.Add(where + " opens a hold, but the tool has none");

                    if (option.releasesHold && tool.hold == AnomalyToolHold.None)
                        errors.Add(where + " releases a hold, but the tool has none");

                    // Spec 23 is explicit that nothing these machines hand over is free.
                    if (!string.IsNullOrEmpty(option.grantsFlagId) &&
                        (option.onChosen == null || option.onChosen.Length == 0) &&
                        string.IsNullOrEmpty(option.consumesFlagId) && !option.opensHold)
                        warnings.Add(where + " gives something away with no price at all (spec 23)");

                    if (string.IsNullOrEmpty(option.nextMenuId)) hasExit = true;
                }

                // A screen whose every line opens another screen is one the caretaker cannot
                // finish at. The panel always draws its own way out, so this is a warning
                // rather than an error - but a menu that meant to end somewhere and does not
                // is worth hearing about.
                if (!hasExit)
                    warnings.Add("anomaly tool " + id + " menu " + menu.menuId +
                                 " has no option that ends the transaction");
            }
        }

        static void ValidateToolHold(AnomalyToolDefinition tool, LocalizationService loc,
                                     List<string> errors)
        {
            var id = tool.toolId;
            if (tool.hold == AnomalyToolHold.None)
            {
                if (tool.holdGameSeconds > 0)
                    errors.Add("anomaly tool " + id + " has a hold time but no hold");
                return;
            }

            if (tool.holdGameSeconds <= 0)
                errors.Add("anomaly tool " + id + " has a hold with no time on it");

            RequireKey(loc, tool.holdPromptKey, "anomaly tool " + id + " hold prompt", errors);
            RequireKey(loc, tool.holdReleaseKey, "anomaly tool " + id + " hold release prompt", errors);
            RequireKey(loc, tool.holdReleasedKey, "anomaly tool " + id + " hold released notice", errors);
            RequireKey(loc, tool.holdLateKey, "anomaly tool " + id + " late release notice", errors);

            CheckKeys(loc, tool.holdWarnKeys, "anomaly tool " + id + " hold warning", errors);

            if (tool.holdWarnKeys.Length != tool.holdWarnAtGameSeconds.Length)
                errors.Add("anomaly tool " + id + " has " + tool.holdWarnKeys.Length +
                           " hold warnings and " + tool.holdWarnAtGameSeconds.Length +
                           " times to fire them at");

            // A price for running out that nothing can collect, or a hold nothing can end.
            // Either way the caretaker is left holding something for the rest of the game.
            if (!tool.holdSurvivesExpiry && tool.onLateRelease.Length > 0)
                errors.Add("anomaly tool " + id + " charges for a late release, but its hold " +
                           "ends the moment it expires");

            if (tool.holdSurvivesExpiry && tool.onLateRelease.Length == 0 &&
                tool.onHoldExpired.Length == 0)
                errors.Add("anomaly tool " + id + " keeps the caretaker past the deadline and " +
                           "charges nothing either way (spec 23)");
        }

        /// <summary>
        /// Every GrantEvidence in the content names evidence that exists.
        ///
        /// This check is here because three of them did not. The anomaly rewards for M04, M10
        /// and M16 were authored against the spec section numbers and landed on ids night 6
        /// had already taken, so three correct answers granted nothing at all - and the only
        /// trace was one Log.Error at the moment the player earned it.
        /// </summary>
        static void ValidateGrantedEvidenceExists(ContentDatabase content, List<string> errors)
        {
            foreach (var definition in content.Cases)
            {
                CheckGrants(content, definition.consequences, "case " + definition.caseId, errors);
                if (definition.decisions == null) continue;
                for (int i = 0; i < definition.decisions.Length; i++)
                {
                    var decision = definition.decisions[i];
                    if (decision == null) continue;
                    CheckGrants(content, decision.consequences,
                                "case " + definition.caseId + " decision " + decision.decisionId, errors);
                }
            }

            foreach (var definition in content.ManualEvents)
            {
                CheckGrants(content, definition.onCorrect, "manual event " + definition.eventId, errors);
                CheckGrants(content, definition.onPartial, "manual event " + definition.eventId, errors);
                CheckGrants(content, definition.onWrong, "manual event " + definition.eventId, errors);
            }

            foreach (var tool in content.AnomalyTools)
            {
                for (int m = 0; m < tool.menus.Length; m++)
                {
                    var menu = tool.menus[m];
                    if (menu == null) continue;
                    for (int o = 0; o < menu.options.Length; o++)
                    {
                        var option = menu.options[o];
                        if (option == null) continue;
                        CheckGrants(content, option.onChosen, "anomaly tool " + tool.toolId, errors);
                    }
                }
            }
        }

        static void CheckGrants(ContentDatabase content, ConsequenceDefinition[] consequences,
                                string where, List<string> errors)
        {
            if (consequences == null) return;
            for (int i = 0; i < consequences.Length; i++)
            {
                var c = consequences[i];
                if (c == null || c.type != ConsequenceType.GrantEvidence) continue;
                if (content.FindEvidence(c.targetId) == null)
                    errors.Add(where + " grants evidence " + c.targetId + ", which does not exist");
            }
        }

        static void CheckConsequenceFloors(ConsequenceDefinition[] consequences, string id,
                                           List<string> errors)
        {
            if (consequences == null) return;
            for (int i = 0; i < consequences.Length; i++)
            {
                var c = consequences[i];
                if (c == null || c.type != ConsequenceType.AddFloorRisk) continue;
                if (!FloorPlan.Exists(c.targetId))
                    errors.Add("manual event " + id + " raises FloorRisk on '" + c.targetId +
                               "', which is not a floor");
            }
        }

        static void CheckKeys(LocalizationService loc, string[] keys, string context, List<string> errors)
        {
            if (keys == null) return;
            for (int i = 0; i < keys.Length; i++)
                RequireKey(loc, keys[i], context + " " + (i + 1), errors);
        }

        static void ValidateCases(ContentDatabase content, LocalizationService loc,
                                  List<string> errors, List<string> warnings)
        {
            foreach (var definition in content.Cases)
            {
                var id = definition.caseId;

                RequireKey(loc, definition.titleKey, "case " + id + " title", errors);
                RequireKey(loc, definition.summaryKey, "case " + id + " summary", errors);

                if (definition.objectives == null || definition.objectives.Length == 0)
                    errors.Add("case " + id + " has no objectives");

                if (definition.decisions == null || definition.decisions.Length == 0)
                    errors.Add("case " + id + " has no decisions, so it can never be resolved");

                if (definition.priority == Priority.P0 && (definition.failSafe == null || !definition.failSafe.enabled))
                    errors.Add("P0 case " + id + " has no fail-safe (GDD 11.2)");

                var seen = new HashSet<string>();
                if (definition.objectives != null)
                    for (int i = 0; i < definition.objectives.Length; i++)
                    {
                        var objective = definition.objectives[i];
                        if (objective == null) { errors.Add("case " + id + " has a null objective"); continue; }
                        if (!seen.Add(objective.objectiveId))
                            errors.Add("case " + id + " has duplicate objective id " + objective.objectiveId);

                        RequireKey(loc, objective.titleKey, "case " + id + " objective " + objective.objectiveId, errors);

                        if (objective.type == ObjectiveType.AcquireEvidence &&
                            content.FindEvidence(objective.targetId) == null)
                            errors.Add("case " + id + " objective " + objective.objectiveId +
                                       " points at unknown evidence " + objective.targetId);
                    }

                bool hasCorrect = false;
                if (definition.decisions != null)
                    for (int i = 0; i < definition.decisions.Length; i++)
                    {
                        var decision = definition.decisions[i];
                        if (decision == null) { errors.Add("case " + id + " has a null decision"); continue; }

                        RequireKey(loc, decision.labelKey, "case " + id + " decision " + decision.decisionId, errors);
                        RequireKey(loc, decision.resultKey, "case " + id + " result " + decision.decisionId, errors);

                        if (decision.quality == DecisionQuality.Correct) hasCorrect = true;

                        if (decision.requiredEvidenceIds == null) continue;
                        for (int e = 0; e < decision.requiredEvidenceIds.Length; e++)
                            if (content.FindEvidence(decision.requiredEvidenceIds[e]) == null)
                                errors.Add("case " + id + " decision " + decision.decisionId +
                                           " requires unknown evidence " + decision.requiredEvidenceIds[e]);
                    }

                if (!hasCorrect) warnings.Add("case " + id + " has no Correct decision");

                if (definition.failSafe != null && definition.failSafe.enabled &&
                    !string.IsNullOrEmpty(definition.failSafe.alternateEvidenceId) &&
                    content.FindEvidence(definition.failSafe.alternateEvidenceId) == null)
                    errors.Add("case " + id + " fail-safe points at unknown evidence " +
                               definition.failSafe.alternateEvidenceId);
            }
        }

        static void ValidateEvidence(ContentDatabase content, LocalizationService loc, List<string> errors)
        {
            foreach (var definition in content.Evidence)
            {
                RequireKey(loc, definition.displayNameKey, "evidence " + definition.evidenceId + " name", errors);
                RequireKey(loc, definition.descriptionKey, "evidence " + definition.evidenceId + " description", errors);

                if (definition.validRelations == null) continue;
                for (int i = 0; i < definition.validRelations.Length; i++)
                {
                    var rule = definition.validRelations[i];
                    if (rule != null && content.FindEvidence(rule.otherEvidenceId) == null)
                        errors.Add("evidence " + definition.evidenceId + " relates to unknown " + rule.otherEvidenceId);
                }
            }
        }

        static void ValidateDialogues(ContentDatabase content, LocalizationService loc,
                                      List<string> errors, List<string> warnings)
        {
            foreach (var definition in content.Dialogues)
            {
                var id = definition.conversationId;

                if (definition.FindNode(definition.startNodeId) == null)
                {
                    errors.Add("dialogue " + id + " start node " + definition.startNodeId + " does not exist");
                    continue;
                }

                var reachable = new HashSet<string>();
                Walk(definition, definition.startNodeId, reachable);

                for (int i = 0; i < definition.nodes.Length; i++)
                {
                    var node = definition.nodes[i];
                    if (node == null) continue;

                    RequireKey(loc, node.textKey, "dialogue " + id + " node " + node.nodeId, errors);
                    RequireKey(loc, node.speakerKey, "dialogue " + id + " speaker " + node.nodeId, errors);

                    if (!reachable.Contains(node.nodeId))
                        warnings.Add("dialogue " + id + " node " + node.nodeId + " is unreachable");

                    if (!string.IsNullOrEmpty(node.nextNodeId) && definition.FindNode(node.nextNodeId) == null)
                        errors.Add("dialogue " + id + " node " + node.nodeId +
                                   " points at missing node " + node.nextNodeId);

                    if (node.choices == null) continue;
                    for (int c = 0; c < node.choices.Length; c++)
                    {
                        var choice = node.choices[c];
                        if (choice == null) continue;

                        RequireKey(loc, choice.textKey, "dialogue " + id + " choice " + choice.choiceId, errors);

                        if (!choice.endsConversation && !string.IsNullOrEmpty(choice.nextNodeId) &&
                            definition.FindNode(choice.nextNodeId) == null)
                            errors.Add("dialogue " + id + " choice " + choice.choiceId +
                                       " points at missing node " + choice.nextNodeId);
                    }

                    if (node.choices.Length > 4)
                        errors.Add("dialogue " + id + " node " + node.nodeId +
                                   " has " + node.choices.Length + " choices (max 4, GDD 16.9)");
                }
            }
        }

        static void Walk(Dialogue.DialogueDefinition definition, string nodeId, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(nodeId) || !visited.Add(nodeId)) return;

            var node = definition.FindNode(nodeId);
            if (node == null) return;

            Walk(definition, node.nextNodeId, visited);
            if (node.choices == null) return;

            for (int i = 0; i < node.choices.Length; i++)
                if (node.choices[i] != null) Walk(definition, node.choices[i].nextNodeId, visited);
        }

        /// <summary>The target unit used by callers for the building rather than a household.</summary>
        const string CommonAreaUnit = "common";

        static void ValidateVisitors(ContentDatabase content, LocalizationService loc, List<string> errors)
        {
            // Every unit the resident directory knows about, plus the one pseudo-unit used by
            // building-wide callers.
            var knownUnits = new HashSet<string>(StringComparer.Ordinal) { CommonAreaUnit };
            foreach (var resident in content.Residents)
                if (resident != null && !string.IsNullOrEmpty(resident.unitNumber))
                    knownUnits.Add(resident.unitNumber);

            foreach (var definition in content.Visitors)
            {
                RequireKey(loc, definition.nameKey, "visitor " + definition.visitorId + " name", errors);
                RequireKey(loc, definition.purposeKey, "visitor " + definition.visitorId + " purpose", errors);

                // A caller for a unit that is not in the directory is unanswerable: the player
                // searches the residents app, finds nothing, and the authored check still says
                // the unit is confirmed. Worse, "no such unit" is the tell for a caller who
                // should be turned away, so a legitimate one pointed at a missing unit teaches
                // the opposite of the rule the whole door game runs on (GDD 13.2 / 13.4).
                // Units 801 and 406 sat here for exactly this reason, on legitimate callers,
                // and nothing was checking.
                if (!string.IsNullOrEmpty(definition.targetUnit) &&
                    !knownUnits.Contains(definition.targetUnit))
                    errors.Add("visitor " + definition.visitorId + " targets unit " +
                               definition.targetUnit + ", which is not in the resident directory");

                if (!string.IsNullOrEmpty(definition.conversationId) &&
                    content.FindDialogue(definition.conversationId) == null)
                    errors.Add("visitor " + definition.visitorId + " references unknown conversation " +
                               definition.conversationId);

                // GDD 13.2: identifying a fake needs at least two independent facts.
                if (definition.checks == null || definition.checks.Length < 2)
                    errors.Add("visitor " + definition.visitorId + " has fewer than two verifiable checks");

                if (definition.checks == null) continue;

                int visibleContradictions = 0;
                for (int i = 0; i < definition.checks.Length; i++)
                {
                    RequireKey(loc, definition.checks[i].labelKey, "visitor check label", errors);
                    RequireKey(loc, definition.checks[i].valueKey, "visitor check value", errors);

                    if (definition.checks[i].contradicts &&
                        definition.checks[i].fromNight <= definition.nightIndex) visibleContradictions++;
                }

                // GDD 13.4: the checklist grows with the threat, so a caller who should be
                // refused has to be catchable with the checks their own night has taught. A
                // contradiction hidden behind a later briefing is a trap, not evidence.
                if (definition.nightIndex >= 0 &&
                    definition.ShouldBeRefused && visibleContradictions < 2)
                    errors.Add("visitor " + definition.visitorId + " cannot be caught with the checks " +
                               "taught by night " + definition.nightIndex + " (GDD 13.2 / 13.4)");
            }
        }

        static void ValidateResidents(ContentDatabase content, LocalizationService loc, List<string> errors)
        {
            var units = new HashSet<string>();

            foreach (var definition in content.Residents)
            {
                RequireKey(loc, definition.nameKey, "resident " + definition.residentId + " name", errors);

                if (!units.Add(definition.unitNumber))
                    errors.Add("duplicate unit number " + definition.unitNumber);

                if (definition.hiddenUntilSync && string.IsNullOrEmpty(definition.revealFlagId))
                    errors.Add("resident " + definition.residentId + " is hidden but has no reveal flag");

                if (definition.notes == null) continue;
                for (int i = 0; i < definition.notes.Length; i++)
                    RequireKey(loc, definition.notes[i].noteKey, "resident note", errors);
            }
        }

        static void ValidateCctv(ContentDatabase content, LocalizationService loc,
                                 List<string> errors, List<string> warnings)
        {
            var cameras = new HashSet<string>();

            var channels = content.CctvChannels;
            for (int i = 0; i < channels.Count; i++)
            {
                RequireKey(loc, channels[i].labelKey, "camera " + channels[i].cameraId, errors);
                if (!cameras.Add(channels[i].cameraId))
                    errors.Add("duplicate camera id " + channels[i].cameraId);
            }

            // GDD 12.3 defines 36 anomaly types; the shipped game must schedule all of them.
            var coveredTypes = new HashSet<int>();
            foreach (var anomaly in content.Anomalies) coveredTypes.Add(anomaly.typeNumber);

            for (int type = 1; type <= AnomalyCatalogue.TypeCount; type++)
                if (!coveredTypes.Contains(type))
                    errors.Add("anomaly type " + type + " from GDD 12.3 is never scheduled");

            foreach (var anomaly in content.Anomalies)
            {
                if (anomaly.typeNumber < 1 || anomaly.typeNumber > AnomalyCatalogue.TypeCount)
                    errors.Add("anomaly " + anomaly.anomalyId + " has type number " + anomaly.typeNumber);

                if (anomaly.category != AnomalyCatalogue.CategoryOf(anomaly.typeNumber))
                    warnings.Add("anomaly " + anomaly.anomalyId + " category does not match its catalogue family");

                if (!cameras.Contains(anomaly.cameraId))
                    errors.Add("anomaly " + anomaly.anomalyId + " targets unknown camera " + anomaly.cameraId);

                RequireKey(loc, anomaly.descriptionKey, "anomaly " + anomaly.anomalyId, errors);

                // GDD 12.4: a core anomaly must stay visible for at least four seconds.
                if (anomaly.durationSeconds < 4f)
                    errors.Add("anomaly " + anomaly.anomalyId + " is shorter than 4 seconds");

                if (!string.IsNullOrEmpty(anomaly.evidenceId) && content.FindEvidence(anomaly.evidenceId) == null)
                    errors.Add("anomaly " + anomaly.anomalyId + " grants unknown evidence " + anomaly.evidenceId);

                if (!string.IsNullOrEmpty(anomaly.caseId) && content.FindCase(anomaly.caseId) == null)
                    errors.Add("anomaly " + anomaly.anomalyId + " belongs to unknown case " + anomaly.caseId);

                if (anomaly.windowEnd > 0 && anomaly.windowEnd <= anomaly.windowBegin)
                    warnings.Add("anomaly " + anomaly.anomalyId + " has an empty time window");
            }
        }

        static void ValidatePhoneCalls(ContentDatabase content, LocalizationService loc, List<string> errors)
        {
            var seen = new HashSet<string>();

            foreach (var call in content.PhoneCalls)
            {
                if (!seen.Add(call.callId)) errors.Add("duplicate call id " + call.callId);

                RequireKey(loc, call.callerNameKey, "call " + call.callId + " caller", errors);

                if (string.IsNullOrEmpty(call.conversationId))
                    errors.Add("call " + call.callId + " has no conversation");
                else if (content.FindDialogue(call.conversationId) == null)
                    errors.Add("call " + call.callId + " references unknown conversation " + call.conversationId);

                if (!string.IsNullOrEmpty(call.caseId) && content.FindCase(call.caseId) == null)
                    errors.Add("call " + call.callId + " references unknown case " + call.caseId);

                if (call.ringSeconds <= 0) errors.Add("call " + call.callId + " rings for no time at all");
            }
        }

        static void ValidateEndings(ContentDatabase content, LocalizationService loc,
                                    List<string> errors, List<string> warnings)
        {
            var seen = new HashSet<string>();
            var orders = new HashSet<int>();
            bool hasFallback = false;

            foreach (var ending in content.Endings)
            {
                if (!seen.Add(ending.endingId)) errors.Add("duplicate ending id " + ending.endingId);
                if (!orders.Add(ending.evaluationOrder))
                    warnings.Add("ending " + ending.endingId + " shares its evaluation order; the tie is arbitrary");

                RequireKey(loc, ending.titleKey, "ending " + ending.endingId + " title", errors);
                RequireKey(loc, ending.summaryKey, "ending " + ending.endingId + " summary", errors);
                RequireKey(loc, ending.bodyKey, "ending " + ending.endingId + " body", errors);
                RequireKey(loc, ending.lockedHintKey, "ending " + ending.endingId + " locked hint", errors);

                bool unconditional = (ending.requireAll == null || ending.requireAll.Length == 0)
                                  && (ending.requireAny == null || ending.requireAny.Length == 0)
                                  && (ending.requireAnyGroup == null || ending.requireAnyGroup.Length == 0)
                                  && (ending.forbid == null || ending.forbid.Length == 0);

                if (unconditional) hasFallback = true;

                if (!string.IsNullOrEmpty(ending.achievementId) &&
                    System.Array.IndexOf(AchievementIds.All, ending.achievementId) < 0)
                    errors.Add("ending " + ending.endingId + " unlocks unknown achievement " + ending.achievementId);
            }

            // Without a fallback the game could finish with no ending at all.
            if (!hasFallback)
                errors.Add("no unconditional fallback ending exists; some playthroughs would end with nothing");

            foreach (var id in EndingIds.All)
                if (content.FindEnding(id) == null) errors.Add("missing ending " + id + " (GDD 10)");
        }

        /// <summary>
        /// Every night must have something to do. The range starts at 1, not 0: the prologue
        /// was deleted along with GDD 9.1's cold open and night 0 is no longer a shift the
        /// game can reach, so requiring content for it would demand content nobody can play.
        /// </summary>
        static void ValidateNightCoverage(ContentDatabase content, List<string> errors, List<string> warnings)
        {
            for (int night = 1; night <= 6; night++)
            {
                int p0 = 0, total = 0;
                foreach (var definition in content.Cases)
                {
                    if (definition.nightIndex != night) continue;
                    total++;
                    if (definition.priority == Priority.P0) p0++;
                }

                if (total == 0) errors.Add("night " + night + " has no cases at all");
                else if (p0 == 0) warnings.Add("night " + night + " has no P0 case");
            }
        }

        /// <summary>
        /// An id authored twice (GDD 20.10: ids are the only handle anything has on content).
        ///
        /// This has to ask the loader rather than walk the collections, because every one of
        /// them is a dictionary keyed by id: by the time this method can see `content.Cases`
        /// the duplicate has already overwritten its twin and left no trace. That is exactly
        /// how T03 sat on two different nights from v1.5 to v1.7 without a single check, test
        /// or playthrough noticing - one of the two definitions simply never existed at
        /// runtime, and which one it was depended on the order the seed happened to build in.
        /// </summary>
        static void ValidateUniqueIds(ContentDatabase content, List<string> errors)
        {
            var duplicates = content.DuplicateIds;
            for (int i = 0; i < duplicates.Count; i++)
                errors.Add("id defined more than once: " + duplicates[i] +
                           " - one of the two definitions was silently discarded at load");
        }

        /// <summary>
        /// GDD 13.2. Two independent facts have to be *obtainable*, not just required.
        ///
        /// The door panel no longer prints the answers - a fact appears only once the player
        /// opens the record that proves it - so a caller whose checks all live in one app can
        /// never be judged correctly no matter what the player does. The caller's own answers
        /// count as one source between them, so one record plus asking questions is enough.
        /// </summary>
        static void ValidateVisitorsAreSolvable(ContentDatabase content, List<string> errors)
        {
            foreach (var visitor in content.Visitors)
            {
                if (visitor == null || visitor.checks == null) continue;

                var sources = new HashSet<string>();
                for (int i = 0; i < visitor.checks.Length; i++)
                {
                    var appId = visitor.checks[i].crossReferenceAppId;
                    if (!string.IsNullOrEmpty(appId)) sources.Add(appId);
                }

                // Asking the caller is worth one source, and every caller can be asked.
                int available = sources.Count + (string.IsNullOrEmpty(visitor.conversationId) ? 0 : 1);

                if (available < 2)
                    errors.Add("visitor " + visitor.visitorId + " offers only " + available +
                               " independent source(s); GDD 13.2 needs two, so this caller can " +
                               "never be judged correctly");
            }
        }

        /// <summary>
        /// The other half of a caller (GDD 13.5).
        ///
        /// A visitor with no observation profile is a face on a 240p feed with nothing to
        /// read, which puts them straight back to being a database row - and the point of the
        /// door is that the records are one route to an answer and the person is the other.
        /// Every caller therefore needs something worth noticing and at least one way of
        /// pushing that goes somewhere.
        ///
        /// The last two rules are the ones that matter. A Deceptive caller whose every tell
        /// sits on the camera can be caught without ever leaving the chair, which is the game
        /// this system was built to replace; and a Shaken caller with no innocent tell is a
        /// trap with no fair way out, because they read as a liar and nothing anywhere says
        /// otherwise.
        /// </summary>
        static void ValidateVisitorsAreReadable(ContentDatabase content, LocalizationService loc,
                                                List<string> errors, List<string> warnings)
        {
            foreach (var visitor in content.Visitors)
            {
                if (visitor == null) continue;

                var context = "visitor " + visitor.visitorId;

                if (visitor.tells == null || visitor.tells.Length < 3)
                {
                    errors.Add(context + " has fewer than three observable behaviours; there is " +
                               "nothing to read on them (GDD 13.5)");
                    continue;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                bool anyGlass = false;
                for (int i = 0; i < visitor.tells.Length; i++)
                {
                    var tell = visitor.tells[i];
                    if (tell == null || string.IsNullOrEmpty(tell.tellId))
                    {
                        errors.Add(context + " has an observation with no id");
                        continue;
                    }

                    if (!seen.Add(tell.tellId))
                        errors.Add(context + " lists observation " + tell.tellId + " twice");

                    RequireKey(loc, tell.labelKey, context + " observation " + tell.tellId, errors);

                    if (tell.channel == ReadChannel.Glass) anyGlass = true;
                }

                if (visitor.RealTellCount < 1)
                    errors.Add(context + " has no observation worth noting; every tell is noise, " +
                               "so the read layer can never establish a fact about them");

                // GDD 13.7: the walk down to the lobby has to be worth making.
                if (!anyGlass)
                    warnings.Add(context + " can be read completely from the desk; nothing about " +
                                 "them rewards going down to the glass");

                if (visitor.truth == VisitorTruth.Shaken && !HasWeight(visitor, TellWeight.Innocent))
                    errors.Add(context + " is Shaken but carries no innocent tell, so a caretaker " +
                               "reading them carefully still has no way to tell fear from guilt " +
                               "(GDD 13.6)");

                if (visitor.truth == VisitorTruth.Deceptive && !HasWeight(visitor, TellWeight.Deception))
                    errors.Add(context + " is Deceptive but gives nothing away; the records are the " +
                               "only route to them (GDD 13.5)");

                if (visitor.truth == VisitorTruth.Deceptive && !HasWeight(visitor, TellWeight.Noise))
                    warnings.Add(context + " is Deceptive and every one of their tells convicts " +
                                 "them, which makes noticing anything at all a verdict");

                ValidateTactics(visitor, loc, errors);
                ValidateBreakingPointIsReachable(visitor, errors);
            }
        }

        /// <summary>
        /// Where a caller says they are going, and the way an honest one walks there
        /// (v3.0 38.5).
        ///
        /// The route is not decoration. Leaving it is the only thing that makes a bad grant
        /// visible, so a caller without one can be handed the run of the building and produce
        /// no evidence of anything - the pursuit half of the system silently does nothing for
        /// them. Every rule here exists because the failure it catches is invisible in play.
        /// </summary>
        static void ValidateVisitorRoutes(ContentDatabase content, List<string> errors,
                                          List<string> warnings)
        {
            var zones = new HashSet<string>(ZoneIds.All, StringComparer.Ordinal);

            foreach (var visitor in content.Visitors)
            {
                if (visitor == null) continue;
                var context = "visitor " + visitor.visitorId;

                if (visitor.correctAccess == VisitorAccessLevel.Pending)
                    errors.Add(context + " has no correct access level; there is no answer for " +
                               "the panel to be graded against");

                // v3.0 G-01: the run of the building is very rarely the right answer, and a
                // caller for whom it IS the answer needs somebody to have decided that on
                // purpose rather than by leaving a field at its default.
                if (visitor.correctAccess == VisitorAccessLevel.FullTemporary)
                    warnings.Add(context + " is authored as a FullTemporary; that should be " +
                                 "almost never the correct answer (v3.0 G-01)");

                if (visitor.correctAccess == VisitorAccessLevel.Escorted && !visitor.canBeEscorted)
                    errors.Add(context + " should be escorted in but is marked as somebody who " +
                               "cannot be escorted, so the correct answer is not on their panel");

                if (!string.IsNullOrEmpty(visitor.destinationZone) &&
                    !zones.Contains(visitor.destinationZone))
                    errors.Add(context + " is heading for " + visitor.destinationZone +
                               ", which is not a zone in this building");

                if (visitor.expectedRoute == null || visitor.expectedRoute.Length == 0)
                {
                    // Only the ones who can actually get past the lobby need one. A courier
                    // whose correct answer is LobbyOnly has nowhere to stray to.
                    if (VisitorAccess.Unaccompanied(visitor.correctAccess))
                        errors.Add(context + " can be given a floor but has no expected route, " +
                                   "so nothing they do inside can ever read as off-route");
                }
                else
                {
                    for (int i = 0; i < visitor.expectedRoute.Length; i++)
                        if (!zones.Contains(visitor.expectedRoute[i]))
                            errors.Add(context + " routes through " + visitor.expectedRoute[i] +
                                       ", which is not a zone in this building");
                }

                if (!visitor.canDeviate) continue;

                if (!string.IsNullOrEmpty(visitor.deviationZone) &&
                    !zones.Contains(visitor.deviationZone))
                    errors.Add(context + " deviates to " + visitor.deviationZone +
                               ", which is not a zone in this building");

                if (visitor.deviateAfterStep < 1)
                    errors.Add(context + " deviates on their first step, which reads as a " +
                               "scripted trap rather than as somebody taking their chance");
            }
        }

        /// <summary>
        /// Agitation is a share of what this caller has to give, so a breaking point is
        /// reachable by construction - but only if there is anything to give in the first
        /// place. A caller whose every authored response lowers agitation has a capacity of
        /// zero, and the four pushing buttons on their panel are decoration.
        /// </summary>
        static void ValidateBreakingPointIsReachable(VisitorDefinition visitor, List<string> errors)
        {
            if (visitor.PressureCapacity <= 0)
                errors.Add("visitor " + visitor.visitorId + " has no pressure to absorb; every " +
                           "way of pushing them lowers the temperature, so they can never be " +
                           "moved at all (GDD 13.5)");

            if (visitor.breakingPoint < 1 || visitor.breakingPoint > 100)
                errors.Add("visitor " + visitor.visitorId + " has a breaking point of " +
                           visitor.breakingPoint + "; it is a percentage of the pressure " +
                           "available against them and has to sit in 1..100");
        }

        static bool HasWeight(VisitorDefinition visitor, TellWeight weight)
        {
            for (int i = 0; i < visitor.tells.Length; i++)
                if (visitor.tells[i] != null && visitor.tells[i].weight == weight) return true;
            return false;
        }

        static void ValidateTactics(VisitorDefinition visitor, LocalizationService loc, List<string> errors)
        {
            var context = "visitor " + visitor.visitorId;

            if (visitor.responses == null || visitor.responses.Length == 0)
            {
                errors.Add(context + " answers none of the five ways of pushing (GDD 13.5)");
                return;
            }

            var used = new HashSet<PressureTactic>();
            for (int i = 0; i < visitor.responses.Length; i++)
            {
                var response = visitor.responses[i];
                if (response == null) continue;

                if (!used.Add(response.tactic))
                    errors.Add(context + " answers " + response.tactic + " twice");

                RequireKey(loc, response.replyKey, context + " reply to " + response.tactic, errors);

                // A response that names a tell the caller does not have reveals nothing, and
                // does it silently.
                if (!string.IsNullOrEmpty(response.revealsTellId) &&
                    visitor.FindTell(response.revealsTellId) == null)
                    errors.Add(context + " reply to " + response.tactic + " reveals unknown " +
                               "observation " + response.revealsTellId);

                if (!string.IsNullOrEmpty(response.requiresAppId) &&
                    Array.IndexOf(AppIds.Order, response.requiresAppId) < 0)
                    errors.Add(context + " reply to " + response.tactic + " requires unknown app " +
                               response.requiresAppId);
            }

            // Easing off has to be available to everybody. It is the only tactic that lowers
            // agitation, so a caller without one can be pushed off the door and never talked
            // back onto it (GDD 13.6).
            if (!used.Contains(PressureTactic.Reassure))
                errors.Add(context + " has no answer to easing off, so there is no way back from " +
                           "having pushed them (GDD 13.6)");
        }

        /// <summary>
        /// GDD 13.4: five callers a night, and no exceptions.
        ///
        /// The number is a design ceiling, not a performance one. Above it the player stops
        /// holding callers in their head as people and starts processing them as a queue, and
        /// every caller past that point is doing damage to the ones before it.
        /// </summary>
        static void ValidateNightlyCallerCeiling(ContentDatabase content, List<string> errors)
        {
            const int Ceiling = 5;

            var perNight = new Dictionary<int, int>();
            foreach (var visitor in content.Visitors)
            {
                if (visitor == null || visitor.nightIndex < 0) continue;
                int count;
                perNight.TryGetValue(visitor.nightIndex, out count);
                perNight[visitor.nightIndex] = count + 1;
            }

            foreach (var pair in perNight)
                if (pair.Value > Ceiling)
                    errors.Add("night " + pair.Key + " rosters " + pair.Value + " callers; " +
                               "GDD 13.4 caps a night at " + Ceiling);
        }

        static void RequireKey(LocalizationService loc, string key, string context, List<string> errors)
        {
            if (string.IsNullOrEmpty(key)) { errors.Add(context + " has no localization key"); return; }
            if (!loc.HasKey(key)) errors.Add(context + " uses missing key " + key);
        }

        static void Report(List<string> errors, List<string> warnings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[NO404] Content validation: " + errors.Count + " error(s), " +
                          warnings.Count + " warning(s)");

            for (int i = 0; i < errors.Count; i++) sb.AppendLine("  ERROR   " + errors[i]);
            for (int i = 0; i < warnings.Count; i++) sb.AppendLine("  WARNING " + warnings[i]);

            if (errors.Count > 0) Debug.LogError(sb.ToString());
            else if (warnings.Count > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb + "  All content is consistent.");
        }
    }
}
