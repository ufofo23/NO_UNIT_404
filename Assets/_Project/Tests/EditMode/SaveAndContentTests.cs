using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;
using NO404.Residents;
using NO404.Save;
using NO404.Visitors;

namespace NO404.Tests
{
    public sealed class SaveIntegrityTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "no404_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void ChecksumIsStableAndSensitive()
        {
            Assert.AreEqual(SaveService.Checksum("hello"), SaveService.Checksum("hello"));
            Assert.AreNotEqual(SaveService.Checksum("hello"), SaveService.Checksum("hellp"));
            Assert.AreNotEqual(SaveService.Checksum("hello"), SaveService.Checksum("hello "));
        }

        [Test]
        public void SerializedPayloadStartsWithItsChecksum()
        {
            var data = new SaveData { nightIndex = 3, gameSecond = 81000 };
            var payload = SaveService.Serialize(data);

            int newline = payload.IndexOf('\n');
            Assert.Greater(newline, 0);

            var head = payload.Substring(0, newline);
            var body = payload.Substring(newline + 1);
            Assert.AreEqual(SaveService.Checksum(body), head);
        }

        [Test]
        public void ReadVerifiedAcceptsAnIntactFile()
        {
            var path = Path.Combine(_dir, "ok.json");
            var data = new SaveData { nightIndex = 2 };
            File.WriteAllText(path, SaveService.Serialize(data));

            var body = SaveService.ReadVerified(path);

            Assert.IsNotNull(body);
            var restored = UnityEngine.JsonUtility.FromJson<SaveData>(body);
            Assert.AreEqual(2, restored.nightIndex);
        }

        [Test]
        public void ReadVerifiedRejectsATamperedFile()
        {
            var path = Path.Combine(_dir, "bad.json");
            var payload = SaveService.Serialize(new SaveData { nightIndex = 2 });
            File.WriteAllText(path, payload.Replace("\"nightIndex\":2", "\"nightIndex\":9"));

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var body = SaveService.ReadVerified(path);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.IsNull(body, "a checksum mismatch must be reported as unreadable");
        }

        [Test]
        public void ReadVerifiedRejectsATruncatedFile()
        {
            var path = Path.Combine(_dir, "short.json");
            File.WriteAllText(path, "no-newline-here");

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.IsNull(SaveService.ReadVerified(path));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void MissingFileIsNotAnError()
        {
            Assert.IsNull(SaveService.ReadVerified(Path.Combine(_dir, "nope.json")));
        }
    }

    public sealed class SaveMigrationTests
    {
        [Test]
        public void SchemaOneUpgradesAllTheWayToCurrent()
        {
            var data = new SaveData { schemaVersion = 1 };
            data.stats.Clear();

            Assert.IsTrue(SaveMigrations.TryMigrate(data));
            Assert.AreEqual(SaveData.CurrentSchemaVersion, data.schemaVersion);

            Assert.IsTrue(HasStat(data, StatIds.ArchiveIntegrity));
            Assert.IsTrue(HasStat(data, StatIds.ChairmanAlert));
            Assert.IsTrue(HasStat(data, StatIds.HarinResonance));
            Assert.Contains((int)AccessLevel.Staff1, data.accessLevels);
        }

        [Test]
        public void CurrentSchemaIsLeftAlone()
        {
            var data = new SaveData { schemaVersion = SaveData.CurrentSchemaVersion };
            int statCount = data.stats.Count;

            Assert.IsTrue(SaveMigrations.TryMigrate(data));
            Assert.AreEqual(statCount, data.stats.Count);
        }

        [Test]
        public void SaveFromANewerBuildIsRefused()
        {
            var data = new SaveData { schemaVersion = SaveData.CurrentSchemaVersion + 1 };

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.IsFalse(SaveMigrations.TryMigrate(data));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        static bool HasStat(SaveData data, string statId)
        {
            for (int i = 0; i < data.stats.Count; i++) if (data.stats[i].key == statId) return true;
            return false;
        }
    }

    /// <summary>
    /// What tonight owes tomorrow, across a save (v2.1 spec 30.3).
    ///
    /// The scenario spec 30.3 opens with is a caretaker who ignores the car in the fire lane
    /// on night 1, saves, and comes back to night 5 expecting to find it there. Until the
    /// queue was written to the file it was not there: the deferred consequence lived only in
    /// CaseService and every reload quietly forgave it.
    /// </summary>
    public sealed class DeferredConsequenceTests
    {
        static ConsequenceDefinition Deferred(string flagId)
        {
            var consequence = ConsequenceDefinition.Flag(flagId);
            consequence.nextNight = true;
            return consequence;
        }

        [Test]
        public void ADeferredConsequenceSurvivesTheJsonRoundTrip()
        {
            var data = new SaveData();
            data.deferredConsequences.Add(Deferred(FlagIds.FireLaneBlocked));

            var restored = UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(data, false));

            Assert.AreEqual(1, restored.deferredConsequences.Count);
            var back = restored.deferredConsequences[0];
            Assert.AreEqual(ConsequenceType.SetFlag, back.type);
            Assert.AreEqual(FlagIds.FireLaneBlocked, back.targetId);
            Assert.IsTrue(back.boolValue);

            // The flag has to survive too. Without it the entry would be applied on the next
            // night and then queued again by ApplyConsequences the night after that.
            Assert.IsTrue(back.nextNight);
        }

        [Test]
        public void ItSurvivesTheChecksummedPayloadAsWell()
        {
            var data = new SaveData { nightIndex = 1 };
            data.deferredConsequences.Add(Deferred(FlagIds.WaterOutage));

            string payload = SaveService.Serialize(data);
            string body = payload.Substring(payload.IndexOf('\n') + 1);
            var restored = UnityEngine.JsonUtility.FromJson<SaveData>(body);

            Assert.AreEqual(1, restored.deferredConsequences.Count);
            Assert.AreEqual(FlagIds.WaterOutage, restored.deferredConsequences[0].targetId);
        }

        [Test]
        public void AnOlderSaveComesBackWithNothingOwedRatherThanRefusingToLoad()
        {
            var data = new SaveData { schemaVersion = 8 };
            data.deferredConsequences = null;

            Assert.IsTrue(SaveMigrations.TryMigrate(data));
            Assert.AreEqual(SaveData.CurrentSchemaVersion, data.schemaVersion);
            Assert.IsNotNull(data.deferredConsequences);
            Assert.AreEqual(0, data.deferredConsequences.Count);
        }
    }

    /// <summary>
    /// Data-integrity tests that mirror the editor validator, so a broken content edit fails
    /// CI instead of only showing up when someone runs the menu item.
    /// </summary>
    public sealed class ContentIntegrityTests
    {
        ContentDatabase _content;
        LocalizationService _loc;

        [SetUp]
        public void SetUp()
        {
            _loc = new LocalizationService();
            _loc.Initialize("ko");

            _content = new ContentDatabase();
            _content.Load();
        }

        [Test]
        public void StringTableLoaded()
        {
            Assert.IsTrue(_loc.HasKey("ui.menu.title"), "Resources/NO404/strings.csv must be importable");
            Assert.AreEqual("404호는 없습니다", _loc.Get("ui.menu.title"));

            _loc.SetLanguage("en");
            Assert.AreEqual("NO UNIT 404", _loc.Get("ui.menu.title"));
        }

        [Test]
        public void FormattedKeysSubstituteArguments()
        {
            Assert.IsTrue(_loc.Get("ui.hud.evidence_count", 7).Contains("7"));
        }

        [Test]
        public void EveryCaseCanBeResolved()
        {
            foreach (var definition in _content.Cases)
            {
                Assert.IsNotNull(definition.decisions, definition.caseId + " has no decisions array");
                Assert.Greater(definition.decisions.Length, 0, definition.caseId + " can never be resolved");
                Assert.Greater(definition.objectives.Length, 0, definition.caseId + " has no objectives");
            }
        }

        [Test]
        public void EveryP0CaseHasAFailSafe()
        {
            foreach (var definition in _content.Cases)
            {
                if (definition.priority != Priority.P0) continue;
                Assert.IsNotNull(definition.failSafe, definition.caseId + " has no fail-safe");
                Assert.IsTrue(definition.failSafe.enabled, definition.caseId + " fail-safe is disabled");
            }
        }

        [Test]
        public void EveryReferencedEvidenceIdExists()
        {
            foreach (var definition in _content.Cases)
            {
                for (int i = 0; i < definition.objectives.Length; i++)
                {
                    var objective = definition.objectives[i];
                    if (objective.type != ObjectiveType.AcquireEvidence) continue;
                    Assert.IsNotNull(_content.FindEvidence(objective.targetId),
                                     definition.caseId + " -> unknown evidence " + objective.targetId);
                }

                for (int d = 0; d < definition.decisions.Length; d++)
                {
                    var required = definition.decisions[d].requiredEvidenceIds;
                    for (int e = 0; e < required.Length; e++)
                        Assert.IsNotNull(_content.FindEvidence(required[e]),
                                         definition.caseId + " -> unknown required evidence " + required[e]);
                }
            }
        }

        [Test]
        public void EveryDialogueLinkResolves()
        {
            foreach (var definition in _content.Dialogues)
            {
                Assert.IsNotNull(definition.FindNode(definition.startNodeId),
                                 definition.conversationId + " has no start node");

                for (int i = 0; i < definition.nodes.Length; i++)
                {
                    var node = definition.nodes[i];

                    if (!string.IsNullOrEmpty(node.nextNodeId))
                        Assert.IsNotNull(definition.FindNode(node.nextNodeId),
                                         definition.conversationId + "/" + node.nodeId + " -> " + node.nextNodeId);

                    Assert.LessOrEqual(node.choices.Length, 4,
                                       definition.conversationId + "/" + node.nodeId + " exceeds 4 choices");

                    for (int c = 0; c < node.choices.Length; c++)
                    {
                        var choice = node.choices[c];
                        if (choice.endsConversation || string.IsNullOrEmpty(choice.nextNodeId)) continue;
                        Assert.IsNotNull(definition.FindNode(choice.nextNodeId),
                                         definition.conversationId + "/" + choice.choiceId +
                                         " -> " + choice.nextNodeId);
                    }
                }
            }
        }

        [Test]
        public void EveryVisitorOffersTwoIndependentChecks()
        {
            foreach (var definition in _content.Visitors)
                Assert.GreaterOrEqual(definition.checks.Length, 2,
                                      definition.visitorId + " cannot be judged with two facts (GDD 13.2)");
        }

        [Test]
        public void AnomaliesStayVisibleLongEnoughToBeSeen()
        {
            foreach (var anomaly in _content.Anomalies)
                Assert.GreaterOrEqual(anomaly.durationSeconds, 4f,
                                      anomaly.anomalyId + " is shorter than the 4s fairness floor (GDD 12.4)");
        }

        [Test]
        public void AllTwelveCamerasExistAndAreUnique()
        {
            var seen = new HashSet<string>();
            var channels = _content.CctvChannels;

            Assert.AreEqual(12, channels.Count, "GDD 12.1 defines exactly 12 channels");
            for (int i = 0; i < channels.Count; i++)
                Assert.IsTrue(seen.Add(channels[i].cameraId), "duplicate camera " + channels[i].cameraId);
        }

        [Test]
        public void Unit404IsHiddenUntilItsRevealFlag()
        {
            var resident = _content.FindResident("res_404");

            Assert.IsNotNull(resident);
            Assert.IsTrue(resident.hiddenUntilSync);
            Assert.AreEqual(FlagIds.Knows404, resident.revealFlagId);
            Assert.AreEqual("2009-11-07", resident.lastSyncDate,
                            "the sync date is the only abnormal field on the 404 row (GDD 16.10)");
        }

        /// <summary>
        /// GDD 9.1 / 13.4. The cold open runs three callers inside its first nine minutes and
        /// the third of them is the first thing in the game that can be got wrong.
        ///
        /// This used to guard the prologue's roster. The prologue is gone, and with it the
        /// idea that the door game could wait: it is half of what this game sells and it now
        /// runs, including a judgement that can go either way, before the player has decided
        /// whether to keep playing.
        ///
        /// The three are hand-placed in GameLoop rather than carried on the roster (their
        /// nightIndex is -1) because they are timed against the blackout, so this asserts the
        /// definitions themselves: two ordinary callers to set a baseline, then the one that
        /// contradicts it.
        /// </summary>
        [Test]
        public void TheColdOpenSchedulesOneCallerWhoShouldBeTurnedAway()
        {
            var first = _content.FindVisitor("vis_n0_guest_jiwoo");
            var second = _content.FindVisitor("vis_n0_courier");
            var wrong = _content.FindVisitor("vis_courier_late");

            Assert.IsNotNull(first, "the cold open's 22:02 caller is missing");
            Assert.IsNotNull(second, "the cold open's 22:05 caller is missing");
            Assert.IsNotNull(wrong, "the cold open's 22:09 caller is missing");

            // A wrong answer teaches nothing on its own: without ordinary callers first the
            // player has never seen what a caller who checks out looks like, so refusing the
            // wrong one is a guess rather than a comparison (GDD 13.4).
            // Asserted as "should this person be kept outside" rather than against a specific
            // level: the baseline the cold open teaches is that ordinary callers get let in
            // somewhere, and how far in is a separate lesson the week goes on to teach.
            Assert.IsFalse(first.ShouldBeRefused,
                "the game's first caller has to be a legitimate one, or the lesson has no baseline");
            Assert.IsFalse(second.ShouldBeRefused,
                "the second caller is the other half of the baseline");
            Assert.IsTrue(wrong.ShouldBeRefused,
                "the cold open has to contain one wrong answer to be a lesson");

            // GDD 13.2: two independent facts, both visible on the first shift.
            int contradictions = 0;
            for (int i = 0; i < wrong.checks.Length; i++)
                if (wrong.checks[i].contradicts && wrong.checks[i].fromNight <= 1) contradictions++;

            Assert.GreaterOrEqual(contradictions, 2,
                "the refusable caller must be catchable with what the first shift shows");
        }

        /// <summary>
        /// GDD 13.4. Night 1's own roster picks up after the cold open, and it still has to
        /// open on a legitimate caller and still has to contain something refusable.
        /// </summary>
        [Test]
        public void NightOnesRosterStillCarriesABaselineAndARefusal()
        {
            int scheduled = 0, refusable = 0;
            int firstArrival = int.MaxValue;
            VisitorAccessLevel firstDecision = VisitorAccessLevel.Reject;

            foreach (var visitor in _content.Visitors)
            {
                if (visitor.nightIndex != 1) continue;
                scheduled++;
                if (visitor.correctAccess == VisitorAccessLevel.Reject) refusable++;

                if (visitor.arrivalGameSecond >= firstArrival) continue;
                firstArrival = visitor.arrivalGameSecond;
                firstDecision = visitor.correctAccess;
            }

            Assert.GreaterOrEqual(scheduled, 2, "night 1's roster is empty");
            Assert.GreaterOrEqual(refusable, 1, "night 1's roster has nothing to refuse");
            Assert.AreEqual(VisitorAccessLevel.FloorPass, firstDecision,
                "the roster resumes on an ordinary caller, not on a second trap");
        }

        /// <summary>
        /// GDD 13.2 + 13.4. A caller who should be refused has to be refusable with what the
        /// player has been taught by that night: the checklist grows with the threat, so a
        /// contradiction hidden behind a later night's briefing is not evidence, it is a trap.
        /// </summary>
        [Test]
        public void EveryRefusableCallerCanBeCaughtWithThatNightsChecklist()
        {
            foreach (var visitor in _content.Visitors)
            {
                if (visitor.nightIndex < 0 || visitor.correctAccess != VisitorAccessLevel.Reject) continue;

                int visible = 0;
                for (int i = 0; i < visitor.checks.Length; i++)
                {
                    var check = visitor.checks[i];
                    if (check != null && check.contradicts && check.fromNight <= visitor.nightIndex) visible++;
                }

                Assert.GreaterOrEqual(visible, 2,
                    visitor.visitorId + " cannot be caught with the checks taught by night " +
                    visitor.nightIndex + " (GDD 13.2)");
            }
        }

        /// <summary>
        /// GDD 16.10 / 2.3 hook #1. The 404 row is still unsearchable before night 4; the
        /// glimpse is the one way it can be on screen earlier, and it has to be a moment
        /// rather than a state - queued once, shown once, gone.
        /// </summary>
        [Test]
        public void TheGlimpseShowsTheHiddenRowOnceAndOnlyOnce()
        {
            var database = new ResidentDatabase(_content);
            var unit404 = _content.FindResident("res_404");

            Assert.IsFalse(database.GlimpseQueued);
            Assert.IsFalse(database.StartQueuedGlimpse(), "nothing to show before it is queued");

            database.QueueGlimpse("res_404");
            Assert.IsTrue(database.GlimpseQueued);

            Assert.IsTrue(database.StartQueuedGlimpse());
            Assert.IsFalse(database.GlimpseQueued, "starting it consumes it");
            Assert.IsFalse(database.StartQueuedGlimpse(), "it does not come back");
            Assert.IsTrue(database.IsVisible(unit404), "the row is on the list while it shows");

            database.Reset();
            Assert.IsFalse(database.GlimpseQueued);
        }

        [Test]
        public void UnitNumbersAreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var resident in _content.Residents)
                Assert.IsTrue(seen.Add(resident.unitNumber), "duplicate unit " + resident.unitNumber);
        }
    }

    public sealed class AccessLogTests
    {
        [Test]
        public void TravelTimeIsSymmetric()
        {
            int forward = Residents.AccessLogService.TravelSeconds("log.location.lobby", "log.location.floor08");
            int backward = Residents.AccessLogService.TravelSeconds("log.location.floor08", "log.location.lobby");

            Assert.AreEqual(forward, backward);
            Assert.Greater(forward, 0);
        }

        [Test]
        public void SameLocationCostsNothing()
        {
            Assert.AreEqual(0, Residents.AccessLogService.TravelSeconds("log.location.lobby", "log.location.lobby"));
        }

        [Test]
        public void ComparisonFlagsAnImpossibleWalk()
        {
            var service = new Residents.AccessLogService();

            var a = new Residents.AccessLogEntry
            {
                GameSecond = 1000, CardId = "RES-303", LocationKey = "log.location.lobby", Inbound = true
            };
            var b = new Residents.AccessLogEntry
            {
                GameSecond = 1010, CardId = "RES-303", LocationKey = "log.location.floor13", Inbound = true
            };

            service.Add(a);
            service.Add(b);

            var result = service.Compare(a, b);

            Assert.AreEqual(10, result.TimeDeltaSeconds);
            Assert.IsTrue(result.SameCard);
            Assert.IsFalse(result.PhysicallyPossible,
                           "ten seconds is not enough to walk from the lobby to the 13th floor");
        }
    }
}
