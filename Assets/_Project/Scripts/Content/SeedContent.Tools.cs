using System.Collections.Generic;
using UnityEngine;
using NO404.Anomalies;
using NO404.Cases;
using NO404.Core;
using NO404.Gameplay;

namespace NO404.ContentData
{
    /// <summary>
    /// The five anomalous tools A01..A05 (v2.1 spec 23), placed on the nights spec 21 gives
    /// them.
    ///
    /// Two rules run through all of this content and are worth stating once rather than
    /// eighteen times in comments below:
    ///
    /// - Nothing a machine says is an answer. Spec 23 forbids a receipt that reads "the valve
    ///   is V4" and asks instead for "a valve at the speed of breathing" - one inference
    ///   short of the answer, so that using the machine is help rather than a solution. Every
    ///   result key here is written to that bar.
    /// - Nothing a machine gives is free, and the price is always a debt rather than a stat
    ///   the ending reads. A caretaker who used every machine every night reaches exactly the
    ///   same five endings as one who never touched them, and has a harder time on night 6
    ///   getting out of the building (spec 31 C14, spec 32).
    /// </summary>
    public static partial class SeedContent
    {
        // Spec 23 A04 balances the return timer in real play time rather than in game time,
        // because the in-fiction half hour is two real minutes and no caretaker could cross
        // the building and come back inside it.
        //
        // Derived from the clock rather than multiplied by a number written here: the shift
        // rate is a design dial (GDD 6.2) and it has moved once already. Eight real minutes
        // has to stay eight real minutes when it moves again.
        static readonly int ToolReturnGameSeconds = GameClock.RealMinutes(8);

        // Spec 23 A02 gives the locker five real seconds to be shut, then beats at six, eight
        // and ten.
        static readonly int LockerCloseGameSeconds = GameClock.RealSeconds(5);

        public static AnomalyToolDefinition[] BuildAnomalyTools()
        {
            var tools = new List<AnomalyToolDefinition>(5);
            tools.Add(BuildReceiptPrinter());
            tools.Add(BuildWishLocker());
            tools.Add(BuildLoungeVcr());
            tools.Add(BuildEndlessToolbox());
            tools.Add(BuildLostAndFoundMachine());
            return tools.ToArray();
        }

        // =====================================================================
        // A01 - 1F 편의점 만능 영수증 프린터 (spec 23 A01, unlocked night 4 after C07)
        // =====================================================================

        /// <summary>
        /// The printer that will itemise anything, and charges in memory.
        ///
        /// Spec 23 A01 rules out a free-text box and asks for a combination of keywords the
        /// caretaker has already met, which is what the two-step menu is: the first screen is
        /// the list of things they have seen enough of to ask about, and a target that is not
        /// on it is not a locked feature - it is a subject they have no business knowing.
        ///
        /// Prices follow the spec exactly: the past and a location cost one memory, a cause or
        /// a danger costs one, and what to do next costs two. The deepest question is the
        /// dearest because it is the one that most nearly does the caretaker's job for them.
        /// </summary>
        static AnomalyToolDefinition BuildReceiptPrinter()
        {
            var tool = Tool(ManualEventIds.A01_ReceiptPrinter, ZoneIds.ConvenienceStore, FloorPlan.F1,
                            night: 4, rootMenuId: "a01.root");

            // Spec 21.5 puts A01 after C07, and the 404 resident database is what C07 leaves
            // behind on every branch of it - including the one where the record is deleted,
            // because deleting it is still having had it.
            tool.unlockConditions = new[] { ConditionDefinition.Evidence("E11_404_RESIDENT_DATABASE") };

            tool.menus = new[]
            {
                Menu("a01.root", "tool.a01.menu.root", "tool.a01.body.root", new[]
                {
                    Opt("t_404", "tool.a01.target.404").Opens("a01.404"),
                    Opt("t_lift", "tool.a01.target.lift").Opens("a01.lift"),
                    Opt("t_playground", "tool.a01.target.playground").Opens("a01.playground"),
                    Opt("t_dongsik", "tool.a01.target.dongsik").Opens("a01.dongsik")
                        .When(ConditionDefinition.Evidence("E10_DONGSIK_RADIO_SIGNAL")),
                    Opt("t_chairman", "tool.a01.target.chairman").Opens("a01.chairman"),
                    Opt("t_504", "tool.a01.target.unit504").Opens("a01.unit504")
                        .When(ConditionDefinition.Night(4)),
                    Opt("t_harin", "tool.a01.target.harin").Opens("a01.harin")
                        .When(ConditionDefinition.Stat(StatIds.HarinResonance, 1)),
                    // Spec 23 A04: what the printer is for once the toolbox has taken something.
                    Opt("t_tool_debt", "tool.a01.target.tool_debt").Opens("a01.tool_debt")
                        .When(ConditionDefinition.Stat(StatIds.ToolDebt, 1)),
                    Leave("a01_leave")
                }),

                Menu("a01.404", "tool.a01.menu.question", "tool.a01.body.question", new[]
                {
                    // The spec prints this receipt in full as its worked example.
                    Ask("q_404_cause", "tool.a01.q.cause", "tool.a01.r.404.cause", 1),
                    Ask("q_404_danger", "tool.a01.q.danger", "tool.a01.r.404.danger", 1),
                    Ask("q_404_next", "tool.a01.q.next", "tool.a01.r.404.next", 2),
                    Back("a01.root")
                }),

                Menu("a01.lift", "tool.a01.menu.question", "tool.a01.body.question", new[]
                {
                    Ask("q_lift_past", "tool.a01.q.past", "tool.a01.r.lift.past", 1),
                    Ask("q_lift_danger", "tool.a01.q.danger", "tool.a01.r.lift.danger", 1),
                    Back("a01.root")
                }),

                Menu("a01.playground", "tool.a01.menu.question", "tool.a01.body.question", new[]
                {
                    Ask("q_pg_past", "tool.a01.q.past", "tool.a01.r.playground.past", 1),
                    Ask("q_pg_next", "tool.a01.q.next", "tool.a01.r.playground.next", 2),
                    Back("a01.root")
                }),

                Menu("a01.dongsik", "tool.a01.menu.question", "tool.a01.body.question", new[]
                {
                    Ask("q_ds_where", "tool.a01.q.where", "tool.a01.r.dongsik.where", 1),
                    Ask("q_ds_past", "tool.a01.q.past", "tool.a01.r.dongsik.past", 1),
                    Back("a01.root")
                }),

                Menu("a01.chairman", "tool.a01.menu.question", "tool.a01.body.question", new[]
                {
                    Ask("q_ch_past", "tool.a01.q.past", "tool.a01.r.chairman.past", 1),
                    Ask("q_ch_next", "tool.a01.q.next", "tool.a01.r.chairman.next", 2),
                    Back("a01.root")
                }),

                Menu("a01.unit504", "tool.a01.menu.question", "tool.a01.body.question", new[]
                {
                    Ask("q_504_cause", "tool.a01.q.cause", "tool.a01.r.unit504.cause", 1),
                    Back("a01.root")
                }),

                Menu("a01.harin", "tool.a01.menu.question", "tool.a01.body.question", new[]
                {
                    Ask("q_harin_past", "tool.a01.q.past", "tool.a01.r.harin.past", 1),
                    Ask("q_harin_danger", "tool.a01.q.danger", "tool.a01.r.harin.danger", 1),
                    Back("a01.root")
                }),

                Menu("a01.tool_debt", "tool.a01.menu.question", "tool.a01.body.question", new[]
                {
                    // Finding out where it went is the first half of spec 23 A04's recovery.
                    // The second half is walking back to B2 and asking for it, which is why
                    // this pays no debt off by itself.
                    Ask("q_debt_where", "tool.a01.q.where", "tool.a01.r.tool_debt.where", 1)
                        .Costs(ConsequenceDefinition.MemoryDebt(1),
                               ConsequenceDefinition.Flag(FlagIds.ToolRecoveryKnown)),
                    Back("a01.root")
                })
            };

            return tool;
        }

        // =====================================================================
        // A02 - 1F 번호 없는 택배함 (spec 23 A02, night 2)
        // =====================================================================

        /// <summary>
        /// The locker at the bottom of the rack with its number worn off, which contains
        /// whatever was asked for.
        ///
        /// This is the failsafe of last resort for the whole game: spec 23 A02 says outright
        /// that a caretaker who has lost a piece of progression can come here for it, and that
        /// the only thing doing so costs them is the claim to have managed alone. Everything
        /// on its list is something another route also provides.
        ///
        /// The price is not the object. The price is the five seconds afterwards.
        /// </summary>
        static AnomalyToolDefinition BuildWishLocker()
        {
            var tool = Tool(ManualEventIds.A02_WishParcelLocker, ZoneIds.Lobby, FloorPlan.F1,
                            night: 2, rootMenuId: "a02.root");

            tool.hold = AnomalyToolHold.CloseDoor;
            tool.holdGameSeconds = LockerCloseGameSeconds;
            tool.holdPromptKey = "tool.a02.hold.prompt";
            tool.holdReleaseKey = "ui.prompt.a02.close";
            tool.holdReleasedKey = "tool.a02.hold.closed";
            tool.holdLateKey = "tool.a02.hold.closed_late";
            tool.holdSurvivesExpiry = true;
            tool.holdWarnKeys = new[]
            {
                "tool.a02.hold.warn1", "tool.a02.hold.warn2", "tool.a02.hold.warn3"
            };
            // Spec 23 A02: six, eight and ten seconds, after the five it allows.
            tool.holdWarnAtGameSeconds = new[]
            {
                GameClock.RealSeconds(6), GameClock.RealSeconds(8), GameClock.RealSeconds(10)
            };
            tool.onHoldExpired = new ConsequenceDefinition[0];
            tool.onLateRelease = new[]
            {
                ConsequenceDefinition.Distortion(10),
                ConsequenceDefinition.FloorRisk(FloorPlan.F1, 1)
            };

            tool.menus = new[]
            {
                Menu("a02.root", "tool.a02.menu.root", "tool.a02.body.root", new[]
                {
                    Parcel("p_pipe_key", "tool.a02.item.pipe_key", "tool.a02.r.pipe_key",
                           ItemIds.PipeRoomKey),
                    Parcel("p_remote", "tool.a02.item.remote", "tool.a02.r.remote",
                           ItemIds.OfficeRemote),
                    Parcel("p_seals", "tool.a02.item.seals", "tool.a02.r.seals",
                           ItemIds.HazardSeals),
                    Parcel("p_neutraliser", "tool.a02.item.neutraliser", "tool.a02.r.neutraliser",
                           ItemIds.Neutraliser),
                    Parcel("p_ball", "tool.a02.item.ball", "tool.a02.r.ball",
                           ItemIds.RedBall),
                    Parcel("p_stethoscope", "tool.a02.item.stethoscope", "tool.a02.r.stethoscope",
                           ItemIds.Stethoscope),
                    Leave("a02_leave")
                })
            };

            return tool;
        }

        // =====================================================================
        // A03 - 2F 주민 휴게실 VCR (spec 23 A03, night 3 after M18)
        // =====================================================================

        /// <summary>
        /// The set in the residents lounge, and the four tapes that fit it.
        ///
        /// Spec 23 A03 is the only machine that shows the caretaker the past rather than
        /// describing it, and the only one where the thing on the other side can tell that it
        /// is being watched. Two of the tapes stop and ask a question; lying to them costs
        /// exposure, and saying nothing costs nothing and ends the tape.
        ///
        /// The family tape is the only route in the game that pays memory back, which is why
        /// it is not on the menu until there is a debt for it to pay.
        /// </summary>
        static AnomalyToolDefinition BuildLoungeVcr()
        {
            var tool = Tool(ManualEventIds.A03_LoungeVcr, ZoneIds.Lounge, FloorPlan.F2,
                            night: 3, rootMenuId: "a03.root");

            // The tape M18 leaves on the landing. Without it the set is a set.
            tool.unlockConditions = new[] { ConditionDefinition.Flag(ItemIds.BlackTape) };

            tool.menus = new[]
            {
                Menu("a03.root", "tool.a03.menu.root", "tool.a03.body.root", new[]
                {
                    Opt("v01", "tool.a03.tape.01", "tool.a03.r.01").Opens("a03.q01"),
                    Opt("v02", "tool.a03.tape.02", "tool.a03.r.02"),
                    Opt("v03", "tool.a03.tape.03", "tool.a03.r.03"),
                    Opt("v04", "tool.a03.tape.04", "tool.a03.r.04").Opens("a03.q04"),
                    Opt("v_family", "tool.a03.tape.family", "tool.a03.r.family")
                        .When(ConditionDefinition.Stat(StatIds.MemoryDebt, 1))
                        .Costs(ConsequenceDefinition.MemoryDebt(-1)),
                    Leave("a03_power_off", "tool.a03.leave")
                }),

                // VHS_01 is 2009 footage, so the caretaker is being asked from inside 2009.
                Menu("a03.q01", "tool.a03.menu.question", "tool.a03.q.year", new[]
                {
                    Opt("a01_2009", "tool.a03.answer.2009", "tool.a03.r.01.true"),
                    Opt("a01_2026", "tool.a03.answer.2026", "tool.a03.r.01.lie")
                        .Costs(ConsequenceDefinition.Distortion(10)),
                    Opt("a01_silent", "tool.a03.answer.silence", "tool.a03.r.01.silent")
                }),

                // VHS_04 was recorded in the management office at no time at all, and the
                // figure in it is the caretaker. Here the true answer is the present one.
                Menu("a03.q04", "tool.a03.menu.question", "tool.a03.q.year", new[]
                {
                    Opt("a04_2009", "tool.a03.answer.2009", "tool.a03.r.04.lie")
                        .Costs(ConsequenceDefinition.Distortion(10)),
                    Opt("a04_2026", "tool.a03.answer.2026", "tool.a03.r.04.true"),
                    Opt("a04_silent", "tool.a03.answer.silence", "tool.a03.r.04.silent")
                })
            };

            return tool;
        }

        // =====================================================================
        // A04 - B2 무한 연장 공구함 (spec 23 A04, night 5)
        // =====================================================================

        /// <summary>
        /// The red steel toolbox that has whatever the job needs, on loan.
        ///
        /// Spec 23 A04 exists so that a puzzle tool the caretaker never found cannot stop the
        /// night, and its cost is the one that is felt rather than read: an unreturned tool
        /// leaves the body lighter by its weight, which the game spends as sprint speed and a
        /// tremor in the hands.
        ///
        /// One thing out at a time. That is enforced by the service rather than written here,
        /// because it is a fact about the box and not about any one tool in it.
        /// </summary>
        static AnomalyToolDefinition BuildEndlessToolbox()
        {
            var tool = Tool(ManualEventIds.A04_EndlessToolbox, ZoneIds.Toolroom, FloorPlan.B2,
                            night: 5, rootMenuId: "a04.root");

            tool.hold = AnomalyToolHold.ReturnTool;
            tool.holdGameSeconds = ToolReturnGameSeconds;
            tool.holdPromptKey = "tool.a04.hold.prompt";
            tool.holdReleaseKey = "ui.prompt.a04.return";
            tool.holdReleasedKey = "tool.a04.hold.returned";
            tool.holdLateKey = "tool.a04.hold.returned_late";
            tool.holdSurvivesExpiry = false;
            tool.holdWarnKeys = new[] { "tool.a04.hold.warn1" };
            tool.holdWarnAtGameSeconds = new[] { ToolReturnGameSeconds / 2 };
            tool.onHoldExpired = new[]
            {
                ConsequenceDefinition.ToolDebt(1),
                ConsequenceDefinition.Notify("tool.a04.hold.taken")
            };

            tool.menus = new[]
            {
                Menu("a04.root", "tool.a04.menu.root", "tool.a04.body.root", new[]
                {
                    Loan("l_stethoscope", "tool.a04.item.stethoscope", "tool.a04.r.stethoscope",
                         ItemIds.Stethoscope),
                    Loan("l_shears", "tool.a04.item.shears", "tool.a04.r.shears",
                         ItemIds.PruningShears),
                    Loan("l_roller", "tool.a04.item.roller", "tool.a04.r.roller",
                         ItemIds.StickerRoller),
                    Loan("l_rod", "tool.a04.item.rod", "tool.a04.r.rod",
                         ItemIds.InsulatedRod),
                    Loan("l_wall_camera", "tool.a04.item.wall_camera", "tool.a04.r.wall_camera",
                         ItemIds.WallCamera),
                    Loan("l_cutter_head", "tool.a04.item.cutter_head", "tool.a04.r.cutter_head",
                         ItemIds.CutterHead),

                    // Only on the menu while something is out. The service knows that; content
                    // cannot, so the line is written unconditionally and filtered at runtime.
                    Opt("l_return", "tool.a04.item.return").Releases(),

                    // Spec 23 A04: having asked A01 where the tool went, the caretaker can go
                    // and get it back. This is the walk, and it costs nothing but the walk.
                    Opt("l_recover", "tool.a04.item.recover", "tool.a04.r.recover")
                        .When(ConditionDefinition.Flag(FlagIds.ToolRecoveryKnown),
                              ConditionDefinition.Stat(StatIds.ToolDebt, 1))
                        .Costs(ConsequenceDefinition.ToolDebt(-1),
                               ConsequenceDefinition.Flag(FlagIds.ToolRecoveryKnown, false)),

                    Leave("a04_leave")
                })
            };

            return tool;
        }

        // =====================================================================
        // A05 - 옥상 계단 분실물 자판기 (spec 23 A05, night 6)
        // =====================================================================

        /// <summary>
        /// The machine on the last landing, lit for one night only.
        ///
        /// Spec 23 A05 is explicit that the game does not ask the player to hurt themselves to
        /// use it: the slot takes a token picked up in the building, and the blood scratched
        /// into the panel is set dressing with no interaction behind it. A token is spent per
        /// item, and there are two of them in the building, so the machine is a choice about
        /// what to take rather than a shelf to empty.
        ///
        /// Four of the five drawers hold something that was actually lost. The fifth holds a
        /// photograph of people the caretaker cannot place, and it is the only thing in the
        /// game that gives a memory back - at a price high enough that taking it is a decision
        /// rather than an obvious yes.
        /// </summary>
        static AnomalyToolDefinition BuildLostAndFoundMachine()
        {
            var tool = Tool(ManualEventIds.A05_LostAndFoundMachine, ZoneIds.Rooftop, FloorPlan.Roof,
                            night: 6, rootMenuId: "a05.root");

            tool.menus = new[]
            {
                Menu("a05.root", "tool.a05.menu.root", "tool.a05.body.root", new[]
                {
                    Lost("f_ball", "tool.a05.item.ball", "tool.a05.r.ball", ItemIds.RedBall),
                    Lost("f_key", "tool.a05.item.key", "tool.a05.r.key", ItemIds.BrassKeyCopy),
                    Lost("f_eraser", "tool.a05.item.eraser", "tool.a05.r.eraser", ItemIds.SchoolEraser),
                    Lost("f_lighter", "tool.a05.item.lighter", "tool.a05.r.lighter", ItemIds.DongsikLighter),

                    // Spec 23 A05: exposure up twenty, one memory back, and ten seconds of the
                    // parapet pulling. Nothing forces the caretaker over it.
                    Lost("f_photo", "tool.a05.item.photo", "tool.a05.r.photo", ItemIds.UnknownPhoto)
                        .Costs(ConsequenceDefinition.Distortion(20),
                               ConsequenceDefinition.MemoryDebt(-1))
                        .Drifts(GameClock.RealSeconds(10)),

                    Leave("a05_leave")
                })
            };

            return tool;
        }

        // ---- builders ---------------------------------------------------------

        static AnomalyToolDefinition Tool(string toolId, string zoneId, string floorId,
                                          int night, string rootMenuId)
        {
            var tool = ScriptableObject.CreateInstance<AnomalyToolDefinition>();
            string slug = toolId.ToLowerInvariant();

            tool.name = toolId;
            tool.toolId = toolId;
            tool.nameKey = "tool." + slug + ".name";
            tool.dormantKey = "tool." + slug + ".dormant";
            tool.zoneId = zoneId;
            tool.floorId = floorId;
            tool.unlockNight = night;
            tool.unlockNotifyKey = "tool." + slug + ".awake";
            tool.rootMenuId = rootMenuId;
            return tool;
        }

        static AnomalyToolMenuDefinition Menu(string menuId, string titleKey, string bodyKey,
                                              AnomalyToolOptionDefinition[] options)
        {
            return new AnomalyToolMenuDefinition
            {
                menuId = menuId, titleKey = titleKey, bodyKey = bodyKey, options = options
            };
        }

        static AnomalyToolOptionDefinition Opt(string optionId, string labelKey, string resultKey = null)
        {
            return AnomalyToolOptionDefinition.Option(optionId, labelKey, resultKey);
        }

        /// <summary>An A01 question, priced by the depth spec 23 A01 gives it.</summary>
        static AnomalyToolOptionDefinition Ask(string optionId, string labelKey, string resultKey,
                                               int memoryCost)
        {
            return Opt(optionId, labelKey, resultKey)
                   .Costs(ConsequenceDefinition.MemoryDebt(memoryCost));
        }

        /// <summary>An A02 parcel: it arrives, and then the door is open.</summary>
        static AnomalyToolOptionDefinition Parcel(string optionId, string labelKey, string resultKey,
                                                  string itemId)
        {
            return Opt(optionId, labelKey, resultKey).Grants(itemId, opensHoldWindow: true);
        }

        /// <summary>An A04 loan: it comes out, and the clock starts.</summary>
        static AnomalyToolOptionDefinition Loan(string optionId, string labelKey, string resultKey,
                                                string itemId)
        {
            return Opt(optionId, labelKey, resultKey).Grants(itemId, opensHoldWindow: true);
        }

        /// <summary>An A05 drawer: one token in, one thing out.</summary>
        static AnomalyToolOptionDefinition Lost(string optionId, string labelKey, string resultKey,
                                                string itemId)
        {
            return Opt(optionId, labelKey, resultKey)
                   .Grants(itemId)
                   .Spends(ItemIds.InsectToken);
        }

        static AnomalyToolOptionDefinition Back(string menuId)
        {
            return Opt("back", "tool.common.back").Opens(menuId);
        }

        static AnomalyToolOptionDefinition Leave(string optionId, string labelKey = "tool.common.leave")
        {
            return Opt(optionId, labelKey);
        }
    }
}
