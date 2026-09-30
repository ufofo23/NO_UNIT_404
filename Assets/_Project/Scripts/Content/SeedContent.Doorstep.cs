using System.Collections.Generic;
using UnityEngine;
using NO404.Cases;
using NO404.Core;
using NO404.Dialogue;
using NO404.Visitors;

namespace NO404.ContentData
{
    /// <summary>
    /// The people at the front door.
    ///
    /// This roster used to be built out of three archetypes - Courier, Service, Guest - and
    /// stamped out nine times a night. Every courier had the same name ("배달원"), the same
    /// face, the same three questions and the same three database lookups, and the honest
    /// answer to "who was the fourth caller on night 4" was that there is no such person. The
    /// door was a queue of identical strangers, and a queue is not a game: the player was not
    /// meeting anyone, they were clearing a work item forty-nine times.
    ///
    /// Two things changed.
    ///
    /// Volume came down to five a night, and never more (GDD 13.4). Nine ordinary callers do
    /// not teach a player what ordinary looks like - they teach them to stop looking. Five
    /// people the player can actually hold in their head is what makes the wrong one land.
    ///
    /// And everyone left is somebody. Oh Mi-ran comes three times across the week and is a
    /// different person by the third; Han Jae-seok's story does not change but his state does;
    /// Jeong Su-a keeps coming back because nobody upstairs is answering her calls, and the
    /// caretaker will work out why before she does. Recurring callers are what turns the door
    /// from a filter into the part of the building the player knows best.
    ///
    /// The verification layer they hang off is in <see cref="DoorReadService"/>: what the
    /// records say, and what the person in front of you is doing while they say it.
    /// </summary>
    public static partial class SeedContent
    {
        // =====================================================================
        // The observation catalogue (GDD 13.5)
        // =====================================================================
        //
        // One shared list of things a caretaker can notice, reused across everybody - and this
        // is the whole design, not an economy measure. The same observation carries a
        // different weight on a different person: a courier who never looks at the lens is
        // hiding a face, and Oh Mi-ran who never looks at the lens has been doing twelve-hour
        // care shifts for nine years and stopped caring what the camera thinks. Weight is set
        // per caller. There is no lookup table for the player to memorise, which means the
        // only way through is to actually read the person.

        const string ObsRehearsed     = "obs_rehearsed";
        const string ObsPause         = "obs_pause";
        const string ObsOvertalks     = "obs_overtalks";
        const string ObsBreath        = "obs_breath";
        const string ObsFullName      = "obs_full_name";
        const string ObsAnnoyed       = "obs_annoyed";
        const string ObsApologises    = "obs_apologises";
        const string ObsCorrectsYou   = "obs_corrects_you";
        const string ObsDeflects      = "obs_deflects";

        const string ObsAvoidsLens    = "obs_avoids_lens";
        const string ObsStaresLens    = "obs_stares_lens";
        const string ObsChecksStreet  = "obs_checks_street";
        const string ObsReadsPalm     = "obs_reads_palm";
        const string ObsInvoiceHidden = "obs_invoice_hidden";
        const string ObsNoVehicle     = "obs_no_vehicle";
        const string ObsEngineRunning = "obs_engine_running";
        const string ObsWrongUniform  = "obs_wrong_uniform";
        const string ObsLiftIndicator = "obs_lift_indicator";
        const string ObsGloves        = "obs_gloves";

        const string ObsHandsShake    = "obs_hands_shake";
        const string ObsSmellsOfDrink = "obs_smells_of_drink";
        const string ObsBasementDust  = "obs_basement_dust";
        const string ObsEmptyBag      = "obs_empty_bag";
        const string ObsBlankBoard    = "obs_blank_board";
        const string ObsExpiredBadge  = "obs_expired_badge";
        const string ObsBeenCrying    = "obs_been_crying";
        const string ObsSecondPerson  = "obs_second_person";
        const string ObsSleptInCar    = "obs_slept_in_car";
        const string ObsWatchesDoor   = "obs_watches_door";

        // =====================================================================
        // Interphone scripts
        // =====================================================================

        /// <summary>
        /// Four shared interphone conversations. Asking a question is what marks the caller's
        /// own account as consulted (GDD 13.2), so a caller with nothing to say could never be
        /// judged correctly - and the caller's account alone was never enough anyway.
        /// </summary>
        public static DialogueDefinition[] BuildDoorstepDialogues()
        {
            return new[]
            {
                Doorstep("D_DOOR_COURIER", "speaker.courier", "dlg.door.courier",
                         "ask_unit", "ask_invoice", "ask_company"),
                Doorstep("D_DOOR_SERVICE", "speaker.contractor", "dlg.door.service",
                         "ask_order", "ask_unit", "ask_company"),
                Doorstep("D_DOOR_GUEST", "speaker.guest", "dlg.door.guest",
                         "ask_resident", "ask_purpose", "ask_id"),
                Doorstep("D_DOOR_KNOWN", "speaker.guest", "dlg.door.known",
                         "ask_tonight", "ask_purpose", "ask_id")
            };
        }

        static DialogueDefinition Doorstep(string id, string speaker, string prefix,
                                           string a, string b, string c)
        {
            return Conversation(id, DialogueChannel.Interphone, 0.25f, 12f,
                Node("start", speaker, prefix + ".start", null,
                    Choices(
                        Choice(a, prefix + ".choice." + a, a),
                        Choice(b, prefix + ".choice." + b, b),
                        Choice(c, prefix + ".choice." + c, c))),
                Node(a, speaker, prefix + "." + a, "await"),
                Node(b, speaker, prefix + "." + b, "await"),
                Node(c, speaker, prefix + "." + c, "await"),
                Node("await", speaker, prefix + ".await", null));
        }

        // =====================================================================
        // The roster
        // =====================================================================

        public static VisitorDefinition[] BuildDoorstepVisitors()
        {
            var list = new List<VisitorDefinition>();

            // Volume, and why it is this much.
            //
            // Five callers a night is the ceiling and it is a hard one (GDD 13.4). The old
            // roster ran eight or nine on the argument that a player needs to see enough
            // ordinary callers to know what ordinary looks like. That argument is only true
            // while the callers are distinguishable; nine interchangeable ones a night taught
            // players to stop reading the panel and start pressing the button, which is the
            // exact failure the volume was supposed to prevent.
            //
            // The counts, per night, story callers included:
            //   night 1  5  (four of them scripted: the cold open and Min-seo)
            //   night 2  5  (two Junhos)
            //   night 3  5
            //   night 4  5
            //   night 5  5
            //   night 6  4  - the building is being emptied and the quiet is the point
            //
            // One or two a night are refusable, which is fewer in absolute terms than the old
            // roster's nine across the week. GDD 15.4 charges pressure per intruder, so the
            // curve tracks the absolute number of wrong admissions rather than the share; the
            // relief per correct call went from -2 to -4 in the same change to keep night 3 on
            // the normal profile where GDD 25.4 measured it. Simulate Nights is what says
            // whether it still holds.

            // ---- night 1: one caller, because four are already scripted -------------
            //
            // The cold open runs two legitimate callers and one parcel for a vacant unit
            // inside the first nine minutes, and Min-seo arrives with C01. Oh Mi-ran is the
            // fifth and she is deliberately the easiest person in the game: nothing to find,
            // nothing hidden, a woman who has done this route for nine years. She is the
            // control sample. Everything the player learns to distrust later, they learn by
            // remembering that she did it too and was fine.
            list.Add(Caregiver("vis_miran_n1", 1, At(23, 20)));

            // ---- night 2: entries on camera must also be in the access log ----------
            list.Add(Brother("vis_jaeseok_n2", 2, At(22, 40), VisitorTruth.Legitimate));
            list.Add(Sua("vis_sua_n2", 2, At(23, 55), first: true));
            list.Add(MeterReader("vis_meter_n2", 2, At(1, 10)));

            // ---- night 3: contractors must quote a work order ----------------------
            list.Add(Caregiver("vis_miran_n3", 3, At(22, 30)));
            list.Add(Son101("vis_dohyeon_n3", 3, At(23, 15)));
            list.Add(FakeContractor("vis_minho_n3", 3, At(0, 5)));
            list.Add(LiftEngineer("vis_engineer_n3", 3, At(1, 20)));
            list.Add(Brother("vis_jaeseok_n3", 3, At(2, 30), VisitorTruth.Legitimate));

            // ---- night 4: residency status is the new tell -------------------------
            list.Add(JunhoAfraid("vis_junho_n4", 4, At(22, 45)));
            list.Add(Surveyor("vis_suhyeon_n4", 4, At(23, 30)));
            list.Add(NightFood("vis_food_n4", 4, At(0, 40)));
            list.Add(ChairmanDriver("vis_driver_n4", 4, At(1, 30)));
            list.Add(Sua("vis_sua_n4", 4, At(2, 40), first: false));

            // ---- night 5: the readers lag, so the card alone proves nothing ---------
            list.Add(LockedOut("vis_jeongmin_n5", 5, At(22, 25)));
            list.Add(Caregiver("vis_miran_n5", 5, At(23, 10)));
            list.Add(FakePolice("vis_police_n5", 5, At(0, 20)));
            list.Add(RecordsCollector("vis_records_n5", 5, At(1, 45)));
            list.Add(Brother("vis_jaeseok_n5", 5, At(3, 5), VisitorTruth.Shaken));

            // ---- night 6: nobody has a reason to be here ---------------------------
            list.Add(Taeho("vis_taeho_n6", 6, At(22, 30)));
            list.Add(Press("vis_press_n6", 6, At(23, 20)));
            list.Add(Demolition("vis_demolition_n6", 6, At(0, 40)));
            list.Add(Sua("vis_sua_n6", 6, At(2, 0), first: false));

            return list.ToArray();
        }

        // =====================================================================
        // The people
        // =====================================================================

        /// <summary>
        /// Oh Mi-ran, night carer for 303. Nights 1, 3 and 5, and a different woman each time.
        ///
        /// Nights 1 and 3 she is exactly what she appears to be, and she is here so that the
        /// player has one person in this building they are sure of. Night 5 she is Shaken: Lee
        /// Seon-ja got worse, Mi-ran has been signing for hours she did not work so the agency
        /// would keep sending her to the same address, and she is terrified of being asked
        /// about it by anyone in a uniform. Every deception signal is there. The correct call
        /// is still to open the door, and a caretaker who breaks her loses the third floor.
        /// </summary>
        static VisitorDefinition Caregiver(string id, int night, int second)
        {
            bool shaken = night >= 5;

            var v = Person(id, night, second, "303", "visitor.miran.name",
                           shaken ? "visitor.miran.purpose.late" : "visitor.miran.purpose",
                           "D_DOOR_KNOWN", VisitorAccessLevel.FloorPass,
                           shaken ? VisitorTruth.Shaken : VisitorTruth.Legitimate,
                           breaking: shaken ? 55 : 85);

            v.remembersVisitorId = night == 3 ? "vis_miran_n1" : (night == 5 ? "vis_miran_n3" : null);
            v.hiddenReasonKey = shaken ? "visitor.miran.hidden" : null;

            v.checks = Checks(
                Check("visitor.check.pre_registered", "visitor.check.value.registered_303", false, AppIds.Residents),
                Check("visitor.check.recent_visits", "visitor.check.value.same_route_nightly", false, AppIds.Access),
                Check("visitor.check.id_name", "visitor.check.value.id_matches", false, AppIds.Residents));

            // Nights 1 and 3: a tired woman with nothing on her mind, and the tells say so if
            // the player bothers to walk down and check.
            if (!shaken)
            {
                v.tells = Tells(
                    Tell(ObsAvoidsLens, ReadChannel.Camera, TellWeight.Noise),
                    Tell(ObsAnnoyed, ReadChannel.Voice, TellWeight.Noise),
                    Tell(ObsFullName, ReadChannel.Voice, TellWeight.Innocent),
                    Tell(ObsWatchesDoor, ReadChannel.Glass, TellWeight.Innocent));

                v.responses = Responses(
                    Reply(PressureTactic.AskAgain, "visitor.miran.reply.ask_again", 6),
                    Reply(PressureTactic.Silence, "visitor.miran.reply.silence", 4),
                    Reply(PressureTactic.ShowMe, "visitor.miran.reply.show_me", 5, ObsFullName),
                    Reply(PressureTactic.Reassure, "visitor.miran.reply.reassure", -10));
                return v;
            }

            // Night 5: everything a liar does, done by somebody telling the truth.
            v.tells = Tells(
                Tell(ObsPause, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsOvertalks, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsAvoidsLens, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsApologises, ReadChannel.Voice, TellWeight.Innocent),
                Tell(ObsHandsShake, ReadChannel.Glass, TellWeight.Noise),
                Tell(ObsBeenCrying, ReadChannel.Glass, TellWeight.Innocent),
                // She still knows the flat better than the caretaker does. A stranger running
                // this story could not do that, and it is on the far side of a kind word.
                Tell(ObsCorrectsYou, ReadChannel.Voice, TellWeight.Innocent, PressureTactic.Reassure));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.miran.reply.ask_again_late", 18),
                Reply(PressureTactic.Confront, "visitor.miran.reply.confront", 30, null, AppIds.Access),
                Reply(PressureTactic.Silence, "visitor.miran.reply.silence_late", 22),
                Reply(PressureTactic.ShowMe, "visitor.miran.reply.show_me_late", 20),
                Reply(PressureTactic.Reassure, "visitor.miran.reply.reassure_late", -14, ObsCorrectsYou));
            return v;
        }

        /// <summary>
        /// Han Jae-seok, 405's brother-in-law. Nights 2, 3 and 5.
        ///
        /// The same man, the same reason, three times - and on night 5 he has been sleeping in
        /// the car park for a week and would rather be refused than have anyone find that out.
        /// He is the second half of the lesson Mi-ran teaches: agitation is a fact about a
        /// person's week, not about their intentions.
        /// </summary>
        static VisitorDefinition Brother(string id, int night, int second, VisitorTruth truth)
        {
            bool shaken = truth == VisitorTruth.Shaken;

            var v = Person(id, night, second, "405", "visitor.jaeseok.name",
                           shaken ? "visitor.jaeseok.purpose.late" : "visitor.jaeseok.purpose",
                           "D_DOOR_GUEST", VisitorAccessLevel.FloorPass, truth,
                           breaking: shaken ? 60 : 80);

            v.remembersVisitorId = night == 3 ? "vis_jaeseok_n2" : (night == 5 ? "vis_jaeseok_n3" : null);
            v.hiddenReasonKey = shaken ? "visitor.jaeseok.hidden" : null;

            v.checks = Checks(
                Check("visitor.check.pre_registered", "visitor.check.value.resident_confirms", false, AppIds.Residents),
                Check("visitor.check.id_name", "visitor.check.value.id_matches", false, AppIds.Residents),
                Check("visitor.check.recent_visits", "visitor.check.value.visited_before", false, AppIds.Access));

            if (!shaken)
            {
                v.tells = Tells(
                    Tell(ObsChecksStreet, ReadChannel.Camera, TellWeight.Noise),
                    Tell(ObsAnnoyed, ReadChannel.Voice, TellWeight.Noise),
                    Tell(ObsCorrectsYou, ReadChannel.Voice, TellWeight.Innocent),
                    // Worth nothing tonight. It is the same observation that finally explains
                    // him on night 5, and only a caretaker who came down to look at him this
                    // week will recognise it when it starts to matter.
                    Tell(ObsSleptInCar, ReadChannel.Glass, TellWeight.Noise),
                    Tell(ObsSmellsOfDrink, ReadChannel.Glass, TellWeight.Noise));

                v.responses = Responses(
                    Reply(PressureTactic.AskAgain, "visitor.jaeseok.reply.ask_again", 12),
                    Reply(PressureTactic.Silence, "visitor.jaeseok.reply.silence", 10),
                    Reply(PressureTactic.ShowMe, "visitor.jaeseok.reply.show_me", 8, ObsCorrectsYou),
                    Reply(PressureTactic.Reassure, "visitor.jaeseok.reply.reassure", -12));
                return v;
            }

            v.tells = Tells(
                Tell(ObsBreath, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsAvoidsLens, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsNoVehicle, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsSmellsOfDrink, ReadChannel.Glass, TellWeight.Noise),
                // The car he says he came in is the car he has been living in, and it is
                // parked in B1 where it has been all week. Only visible from the lobby.
                Tell(ObsSleptInCar, ReadChannel.Glass, TellWeight.Innocent),
                Tell(ObsCorrectsYou, ReadChannel.Voice, TellWeight.Innocent, PressureTactic.Reassure));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.jaeseok.reply.ask_again_late", 20),
                Reply(PressureTactic.Confront, "visitor.jaeseok.reply.confront", 28, null, AppIds.Access),
                Reply(PressureTactic.Silence, "visitor.jaeseok.reply.silence_late", 24),
                Reply(PressureTactic.ShowMe, "visitor.jaeseok.reply.show_me_late", 16),
                Reply(PressureTactic.Reassure, "visitor.jaeseok.reply.reassure_late", -16, ObsCorrectsYou));
            return v;
        }

        /// <summary>
        /// Jeong Su-a, here for Choi Ji-woo in 602. Nights 2, 4 and 6.
        ///
        /// She is the only caller in the game whose reason for being at the door gets worse
        /// every time she uses it. Night 2 Ji-woo is not picking up. Night 4 it has been four
        /// days. Night 6 she is not asking to be let in any more, she is asking the caretaker
        /// whether they have seen her - and the caretaker, by then, has read 602's file note
        /// about the roof.
        ///
        /// Always correct to admit, and the game never says so.
        /// </summary>
        static VisitorDefinition Sua(string id, int night, int second, bool first)
        {
            var v = Person(id, night, second, "602", "visitor.sua.name",
                           first ? "visitor.sua.purpose" : "visitor.sua.purpose.later",
                           "D_DOOR_GUEST", VisitorAccessLevel.LobbyOnly,
                           first ? VisitorTruth.Legitimate : VisitorTruth.Shaken,
                           breaking: first ? 75 : 45);

            v.remembersVisitorId = night == 4 ? "vis_sua_n2" : (night == 6 ? "vis_sua_n4" : null);
            v.hiddenReasonKey = first ? null : "visitor.sua.hidden";

            v.checks = Checks(
                Check("visitor.check.pre_registered", "visitor.check.value.registered_602", false, AppIds.Residents),
                Check("visitor.check.id_name", "visitor.check.value.id_matches", false, AppIds.Residents),
                Check("visitor.check.recent_visits", "visitor.check.value.visited_before", false, AppIds.Access));

            var tells = new List<VisitorTell>
            {
                // The single most misread observation in the game. She is not watching the
                // panel because she is watching to see whether the lift comes down from six.
                Tell(ObsLiftIndicator, ReadChannel.Camera, TellWeight.Innocent),
                Tell(ObsOvertalks, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsBreath, ReadChannel.Voice, TellWeight.Noise),
                // Two nights before she says any of it out loud.
                Tell(ObsWatchesDoor, ReadChannel.Glass, TellWeight.Innocent)
            };

            if (!first)
            {
                tells.Add(Tell(ObsBeenCrying, ReadChannel.Glass, TellWeight.Innocent));
                tells.Add(Tell(ObsHandsShake, ReadChannel.Glass, TellWeight.Noise));
                tells.Add(Tell(ObsFullName, ReadChannel.Voice, TellWeight.Innocent, PressureTactic.Reassure));
            }
            v.tells = tells.ToArray();

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, first ? "visitor.sua.reply.ask_again" : "visitor.sua.reply.ask_again_late", first ? 12 : 22),
                Reply(PressureTactic.Confront, "visitor.sua.reply.confront", 26, null, AppIds.Access),
                Reply(PressureTactic.Silence, first ? "visitor.sua.reply.silence" : "visitor.sua.reply.silence_late", first ? 10 : 20),
                Reply(PressureTactic.ShowMe, "visitor.sua.reply.show_me", 12),
                Reply(PressureTactic.Reassure, first ? "visitor.sua.reply.reassure" : "visitor.sua.reply.reassure_late", -14,
                      first ? null : ObsFullName));
            return v;
        }

        /// <summary>
        /// Kim Do-hyeon, here for his father in 101, who works nights. Night 3, legitimate,
        /// and rude about being asked - which is the point. Rudeness is not a tell.
        /// </summary>
        static VisitorDefinition Son101(string id, int night, int second)
        {
            var v = Person(id, night, second, "101", "visitor.dohyeon.name", "visitor.dohyeon.purpose",
                           "D_DOOR_GUEST", VisitorAccessLevel.LobbyOnly, VisitorTruth.Legitimate,
                           breaking: 88);

            v.checks = Checks(
                Check("visitor.check.pre_registered", "visitor.check.value.registered_101", false, AppIds.Residents),
                Check("visitor.check.id_name", "visitor.check.value.id_matches", false, AppIds.Residents),
                Check("visitor.check.recent_visits", "visitor.check.value.visited_before", false, AppIds.Access));

            v.tells = Tells(
                Tell(ObsAnnoyed, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsStaresLens, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsDeflects, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsCorrectsYou, ReadChannel.Voice, TellWeight.Innocent),
                // His father's night-shift roster is in his hand because his father texted it
                // to him. Nobody running a story carries the thing that disproves it.
                Tell(ObsFullName, ReadChannel.Glass, TellWeight.Innocent));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.dohyeon.reply.ask_again", 14),
                Reply(PressureTactic.Silence, "visitor.dohyeon.reply.silence", 16),
                Reply(PressureTactic.ShowMe, "visitor.dohyeon.reply.show_me", 6, ObsCorrectsYou),
                Reply(PressureTactic.Reassure, "visitor.dohyeon.reply.reassure", -8));
            return v;
        }

        /// <summary>
        /// The lift engineer, night 3. C05 is the elevator case; refusing him is a legitimate
        /// caller turned away with the lift still doing what it does, and the night notices.
        /// </summary>
        static VisitorDefinition LiftEngineer(string id, int night, int second)
        {
            var v = Person(id, night, second, "common", "visitor.engineer.name", "visitor.engineer.purpose",
                           "D_DOOR_SERVICE", VisitorAccessLevel.Escorted, VisitorTruth.Legitimate,
                           breaking: 90);

            v.checks = Checks(
                Check("visitor.check.work_order", "visitor.check.value.order_ok", false, AppIds.Access, 3),
                Check("visitor.check.vehicle", "visitor.check.value.van_logged", false, AppIds.Cctv),
                Check("visitor.check.target_unit", "visitor.check.value.common_area", false, AppIds.Residents));

            v.tells = Tells(
                Tell(ObsGloves, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsEngineRunning, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsAnnoyed, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsCorrectsYou, ReadChannel.Voice, TellWeight.Innocent),
                Tell(ObsExpiredBadge, ReadChannel.Glass, TellWeight.Noise));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.engineer.reply.ask_again", 8),
                Reply(PressureTactic.Confront, "visitor.engineer.reply.confront", 14, ObsCorrectsYou, AppIds.Access),
                Reply(PressureTactic.Silence, "visitor.engineer.reply.silence", 6),
                Reply(PressureTactic.ShowMe, "visitor.engineer.reply.show_me", 4, ObsCorrectsYou),
                Reply(PressureTactic.Reassure, "visitor.engineer.reply.reassure", -6));
            return v;
        }

        /// <summary>
        /// Seo Jun-ho, night 4. The courier from the prologue, back for the fourth time, and
        /// by now he will not come past the lobby door.
        ///
        /// He is Shaken, and what he is hiding is not a crime - it is that he saw something on
        /// the fourth floor on his last delivery and has told nobody because saying it out
        /// loud makes it real. Reassure is the only tactic that gets it, and what he says is
        /// worth more than any record in the building.
        /// </summary>
        static VisitorDefinition JunhoAfraid(string id, int night, int second)
        {
            var v = Person(id, night, second, "303", "visitor.junho.name", "visitor.junho.purpose.afraid",
                           "D_DOOR_COURIER", VisitorAccessLevel.LobbyOnly, VisitorTruth.Shaken,
                           breaking: 50);

            v.remembersVisitorId = "vis_n0_courier";
            v.hiddenReasonKey = "visitor.junho.hidden";

            v.checks = Checks(
                Check("visitor.check.target_unit", "visitor.check.value.unit_303_confirmed", false, AppIds.Residents),
                Check("visitor.check.invoice", "visitor.check.value.invoice_ok", false, AppIds.Access, 1),
                Check("visitor.check.recent_visits", "visitor.check.value.visited_before", false, AppIds.Access));

            v.tells = Tells(
                Tell(ObsBreath, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsChecksStreet, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsAvoidsLens, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsHandsShake, ReadChannel.Glass, TellWeight.Noise),
                // He is not looking at the street. He is looking at the fourth-floor windows.
                Tell(ObsWatchesDoor, ReadChannel.Glass, TellWeight.Innocent),
                Tell(ObsFullName, ReadChannel.Voice, TellWeight.Innocent, PressureTactic.Reassure));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.junho.reply.ask_again", 20),
                Reply(PressureTactic.Confront, "visitor.junho.reply.confront", 30, null, AppIds.Access),
                Reply(PressureTactic.Silence, "visitor.junho.reply.silence", 26),
                Reply(PressureTactic.ShowMe, "visitor.junho.reply.show_me", 10),
                Reply(PressureTactic.Reassure, "visitor.junho.reply.reassure", -18, ObsFullName));
            return v;
        }

        /// <summary>Late food delivery for 602, night 4. Ordinary, and the only easy thing that night.</summary>
        static VisitorDefinition NightFood(string id, int night, int second)
        {
            var v = Person(id, night, second, "602", "visitor.food.name", "visitor.food.purpose",
                           "D_DOOR_COURIER", VisitorAccessLevel.LobbyOnly, VisitorTruth.Legitimate,
                           breaking: 85);

            v.checks = Checks(
                Check("visitor.check.target_unit", "visitor.check.value.unit_confirmed", false, AppIds.Residents),
                Check("visitor.check.invoice", "visitor.check.value.invoice_ok", false, AppIds.Access, 1),
                Check("visitor.check.camera_route", "visitor.check.value.from_street", false, AppIds.Cctv));

            v.tells = Tells(
                Tell(ObsAnnoyed, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsEngineRunning, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsInvoiceHidden, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsCorrectsYou, ReadChannel.Voice, TellWeight.Innocent),
                Tell(ObsHandsShake, ReadChannel.Glass, TellWeight.Noise));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.food.reply.ask_again", 12),
                Reply(PressureTactic.Silence, "visitor.food.reply.silence", 14),
                Reply(PressureTactic.ShowMe, "visitor.food.reply.show_me", 4, ObsCorrectsYou),
                Reply(PressureTactic.Reassure, "visitor.food.reply.reassure", -10));
            return v;
        }

        /// <summary>Park Jeong-min of 405, locked out, night 5. A resident at his own door.</summary>
        static VisitorDefinition LockedOut(string id, int night, int second)
        {
            var v = Person(id, night, second, "405", "resident.405.name", "visitor.jeongmin.purpose",
                           "D_DOOR_KNOWN", VisitorAccessLevel.FloorPass, VisitorTruth.Legitimate,
                           breaking: 80);

            v.checks = Checks(
                Check("visitor.check.id_name", "visitor.check.value.id_matches", false, AppIds.Residents),
                Check("visitor.check.vehicle", "visitor.check.value.plate_match_405", false, AppIds.Cctv),
                Check("visitor.check.recent_visits", "visitor.check.value.lives_here", false, AppIds.Access));

            v.tells = Tells(
                Tell(ObsAnnoyed, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsSmellsOfDrink, ReadChannel.Glass, TellWeight.Noise),
                Tell(ObsOvertalks, ReadChannel.Voice, TellWeight.Noise),
                // He knows which of the two lifts has been out since March. Nobody learns that
                // from a doorstep.
                Tell(ObsCorrectsYou, ReadChannel.Voice, TellWeight.Innocent));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.jeongmin.reply.ask_again", 16),
                Reply(PressureTactic.Silence, "visitor.jeongmin.reply.silence", 18),
                Reply(PressureTactic.ShowMe, "visitor.jeongmin.reply.show_me", 8, ObsCorrectsYou),
                Reply(PressureTactic.Reassure, "visitor.jeongmin.reply.reassure", -12));
            return v;
        }

        /// <summary>
        /// Kang Tae-ho, the relief guard, at the front door on night 6 - with a card that
        /// opens it. He is Shaken and what he knows is why he did not use the card.
        /// </summary>
        static VisitorDefinition Taeho(string id, int night, int second)
        {
            var v = Person(id, night, second, "B02", "resident.taeho.name", "visitor.taeho.purpose",
                           "D_DOOR_KNOWN", VisitorAccessLevel.FloorPass, VisitorTruth.Shaken,
                           breaking: 55);

            v.hiddenReasonKey = "visitor.taeho.hidden";

            v.checks = Checks(
                Check("visitor.check.id_name", "visitor.check.value.staff_07", false, AppIds.Residents),
                Check("visitor.check.access_log", "visitor.check.value.card_unused", true, AppIds.Access, 2),
                Check("visitor.check.recent_visits", "visitor.check.value.on_shift_tonight", false, AppIds.Access));

            v.tells = Tells(
                Tell(ObsPause, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsAvoidsLens, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsSecondPerson, ReadChannel.Glass, TellWeight.Noise),
                Tell(ObsBasementDust, ReadChannel.Glass, TellWeight.Innocent),
                Tell(ObsFullName, ReadChannel.Voice, TellWeight.Innocent, PressureTactic.Reassure));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.taeho.reply.ask_again", 18),
                Reply(PressureTactic.Confront, "visitor.taeho.reply.confront", 26, ObsBasementDust, AppIds.Access),
                Reply(PressureTactic.Silence, "visitor.taeho.reply.silence", 22),
                Reply(PressureTactic.ShowMe, "visitor.taeho.reply.show_me", 12),
                Reply(PressureTactic.Reassure, "visitor.taeho.reply.reassure", -16, ObsFullName));
            return v;
        }

        // ---- the ones who should not be let in -------------------------------
        //
        // Six across six nights, and each one is refusable on the checks their own night has
        // taught (GDD 13.4). They are also the only callers whose deception tells are real:
        // the read layer is not a second opinion on the database, it is a second route to the
        // same answer for a caretaker who is standing in their lobby rather than at their desk.

        /// <summary>Night 2. A water-meter reading at one in the morning, six days after the last one.</summary>
        static VisitorDefinition MeterReader(string id, int night, int second)
        {
            var v = Person(id, night, second, "common", "visitor.meter.name", "visitor.meter.purpose",
                           "D_DOOR_SERVICE", VisitorAccessLevel.Reject, VisitorTruth.Deceptive,
                           breaking: 65);

            v.checks = Checks(
                Check("visitor.check.work_order", "visitor.check.value.order_missing", true, AppIds.Access),
                Check("visitor.check.delivery_hours", "visitor.check.value.after_hours", true, AppIds.Access, 1),
                Check("visitor.check.vehicle", "visitor.check.value.no_vehicle_log", true, AppIds.Cctv),
                Check("visitor.check.target_unit", "visitor.check.value.common_area", false, AppIds.Residents));

            v.tells = Tells(
                Tell(ObsRehearsed, ReadChannel.Voice, TellWeight.Deception, PressureTactic.AskAgain),
                Tell(ObsNoVehicle, ReadChannel.Camera, TellWeight.Deception),
                Tell(ObsApologises, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsBlankBoard, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsDeflects, ReadChannel.Voice, TellWeight.Deception, PressureTactic.None, 60));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.meter.reply.ask_again", 18, ObsRehearsed),
                Reply(PressureTactic.Confront, "visitor.meter.reply.confront", 32, null, AppIds.Facility),
                Reply(PressureTactic.Silence, "visitor.meter.reply.silence", 24),
                Reply(PressureTactic.ShowMe, "visitor.meter.reply.show_me", 30, ObsBlankBoard),
                Reply(PressureTactic.Reassure, "visitor.meter.reply.reassure", -6));
            return v;
        }

        /// <summary>Night 3. A boiler call for 304, which has been empty since March.</summary>
        static VisitorDefinition FakeContractor(string id, int night, int second)
        {
            var v = Person(id, night, second, "304", "visitor.minho.name", "visitor.minho.purpose",
                           "D_DOOR_SERVICE", VisitorAccessLevel.Reject, VisitorTruth.Deceptive,
                           breaking: 75);

            v.checks = Checks(
                Check("visitor.check.work_order", "visitor.check.value.order_missing", true, AppIds.Access, 3),
                Check("visitor.check.target_unit", "visitor.check.value.unit_vacant", true, AppIds.Residents),
                Check("visitor.check.vehicle", "visitor.check.value.no_vehicle_log", true, AppIds.Cctv));

            v.tells = Tells(
                Tell(ObsWrongUniform, ReadChannel.Camera, TellWeight.Deception),
                Tell(ObsAnnoyed, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsStaresLens, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsEmptyBag, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsRehearsed, ReadChannel.Voice, TellWeight.Deception, PressureTactic.AskAgain),
                Tell(ObsPause, ReadChannel.Voice, TellWeight.Deception, PressureTactic.None, 70));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.minho.reply.ask_again", 14, ObsRehearsed),
                Reply(PressureTactic.Confront, "visitor.minho.reply.confront", 34, null, AppIds.Residents),
                Reply(PressureTactic.Silence, "visitor.minho.reply.silence", 20),
                Reply(PressureTactic.ShowMe, "visitor.minho.reply.show_me", 28, ObsEmptyBag),
                Reply(PressureTactic.Reassure, "visitor.minho.reply.reassure", -4));
            return v;
        }

        /// <summary>Night 4. A housing survey, at half eleven at night, with a blank clipboard.</summary>
        static VisitorDefinition Surveyor(string id, int night, int second)
        {
            var v = Person(id, night, second, "common", "visitor.suhyeon.name", "visitor.suhyeon.purpose",
                           "D_DOOR_GUEST", VisitorAccessLevel.Reject, VisitorTruth.Deceptive,
                           breaking: 85);

            v.checks = Checks(
                Check("visitor.check.pre_registered", "visitor.check.value.no_survey_notice", true, AppIds.Residents),
                Check("visitor.check.delivery_hours", "visitor.check.value.after_hours", true, AppIds.Access, 1),
                Check("visitor.check.access_log", "visitor.check.value.no_entry_logged", true, AppIds.Access, 2),
                Check("visitor.check.camera_route", "visitor.check.value.from_street", false, AppIds.Cctv));

            // The hardest caller in the game to catch from a chair. She is composed, her story
            // is internally consistent, and nothing on the camera is wrong. What is wrong is
            // that in twenty minutes of standing there she has not written a single line.
            v.tells = Tells(
                Tell(ObsStaresLens, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsOvertalks, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsGloves, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsBlankBoard, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsSecondPerson, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsReadsPalm, ReadChannel.Camera, TellWeight.Deception, PressureTactic.AskAgain));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.suhyeon.reply.ask_again", 10, ObsReadsPalm),
                Reply(PressureTactic.Confront, "visitor.suhyeon.reply.confront", 26, null, AppIds.Residents),
                Reply(PressureTactic.Silence, "visitor.suhyeon.reply.silence", 8),
                Reply(PressureTactic.ShowMe, "visitor.suhyeon.reply.show_me", 30, ObsBlankBoard),
                Reply(PressureTactic.Reassure, "visitor.suhyeon.reply.reassure", -8));
            return v;
        }

        /// <summary>
        /// Night 4. Somebody collecting documents for 1501, on the night the chairman is
        /// deleting them (C07). The chairman is upstairs; he has not sent anyone.
        /// </summary>
        static VisitorDefinition ChairmanDriver(string id, int night, int second)
        {
            var v = Person(id, night, second, "1501", "visitor.driver.name", "visitor.driver.purpose",
                           "D_DOOR_GUEST", VisitorAccessLevel.Reject, VisitorTruth.Deceptive,
                           breaking: 70);

            v.checks = Checks(
                Check("visitor.check.pre_registered", "visitor.check.value.resident_home", true, AppIds.Residents),
                Check("visitor.check.access_log", "visitor.check.value.no_entry_logged", true, AppIds.Access, 2),
                Check("visitor.check.vehicle", "visitor.check.value.no_vehicle_log", true, AppIds.Cctv),
                Check("visitor.check.id_name", "visitor.check.value.id_mismatch", true, AppIds.Residents));

            v.tells = Tells(
                Tell(ObsFullName, ReadChannel.Voice, TellWeight.Deception),
                Tell(ObsEngineRunning, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsApologises, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsEmptyBag, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsDeflects, ReadChannel.Voice, TellWeight.Deception, PressureTactic.Confront));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.driver.reply.ask_again", 16),
                Reply(PressureTactic.Confront, "visitor.driver.reply.confront", 30, ObsDeflects, AppIds.Residents),
                Reply(PressureTactic.Silence, "visitor.driver.reply.silence", 22),
                Reply(PressureTactic.ShowMe, "visitor.driver.reply.show_me", 26, ObsEmptyBag),
                Reply(PressureTactic.Reassure, "visitor.driver.reply.reassure", -6));
            return v;
        }

        /// <summary>Night 5. Says he is police. There is no call, no number, and no badge.</summary>
        static VisitorDefinition FakePolice(string id, int night, int second)
        {
            var v = Person(id, night, second, "common", "visitor.police.name", "visitor.police.purpose",
                           "D_DOOR_GUEST", VisitorAccessLevel.Reject, VisitorTruth.Deceptive,
                           breaking: 80);

            v.checks = Checks(
                Check("visitor.check.pre_registered", "visitor.check.value.no_call_logged", true, AppIds.Access),
                Check("visitor.check.id_name", "visitor.check.value.no_badge_number", true, AppIds.Residents),
                Check("visitor.check.vehicle", "visitor.check.value.no_vehicle_log", true, AppIds.Cctv),
                Check("visitor.check.delivery_hours", "visitor.check.value.after_hours", true, AppIds.Access, 1));

            // He leans on authority instead of paperwork, and the tell is that he never once
            // offers the one thing a real officer offers first.
            v.tells = Tells(
                Tell(ObsAnnoyed, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsStaresLens, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsDeflects, ReadChannel.Voice, TellWeight.Deception),
                Tell(ObsExpiredBadge, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsSecondPerson, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsRehearsed, ReadChannel.Voice, TellWeight.Deception, PressureTactic.AskAgain));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.police.reply.ask_again", 12, ObsRehearsed),
                Reply(PressureTactic.Confront, "visitor.police.reply.confront", 28, null, AppIds.Access),
                Reply(PressureTactic.Silence, "visitor.police.reply.silence", 6),
                Reply(PressureTactic.ShowMe, "visitor.police.reply.show_me", 34, ObsExpiredBadge),
                Reply(PressureTactic.Reassure, "visitor.police.reply.reassure", -4));
            return v;
        }

        /// <summary>
        /// Night 5. A records-disposal firm, for an archive that C11 is about to prove matters.
        /// The order is signed by a management office that stopped existing in 2010.
        /// </summary>
        static VisitorDefinition RecordsCollector(string id, int night, int second)
        {
            var v = Person(id, night, second, "common", "visitor.records.name", "visitor.records.purpose",
                           "D_DOOR_SERVICE", VisitorAccessLevel.Reject, VisitorTruth.Deceptive,
                           breaking: 72);

            v.checks = Checks(
                Check("visitor.check.work_order", "visitor.check.value.order_expired_office", true, AppIds.Access, 3),
                Check("visitor.check.delivery_hours", "visitor.check.value.after_hours", true, AppIds.Access, 1),
                Check("visitor.check.vehicle", "visitor.check.value.unmarked_truck", true, AppIds.Cctv),
                Check("visitor.check.target_unit", "visitor.check.value.common_area", false, AppIds.Residents));

            v.tells = Tells(
                Tell(ObsWrongUniform, ReadChannel.Camera, TellWeight.Deception),
                Tell(ObsEngineRunning, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsGloves, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsSecondPerson, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsBasementDust, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsPause, ReadChannel.Voice, TellWeight.Deception, PressureTactic.Confront));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.records.reply.ask_again", 14),
                Reply(PressureTactic.Confront, "visitor.records.reply.confront", 32, ObsPause, AppIds.Access),
                Reply(PressureTactic.Silence, "visitor.records.reply.silence", 20),
                Reply(PressureTactic.ShowMe, "visitor.records.reply.show_me", 24),
                Reply(PressureTactic.Reassure, "visitor.records.reply.reassure", -6));
            return v;
        }

        /// <summary>
        /// Night 6. A reporter with a resident's surname and none of their details. She is
        /// not dangerous, and she is still the wrong person to let into an empty building at
        /// half eleven on the last night.
        /// </summary>
        static VisitorDefinition Press(string id, int night, int second)
        {
            var v = Person(id, night, second, "1501", "visitor.press.name", "visitor.press.purpose",
                           "D_DOOR_GUEST", VisitorAccessLevel.Reject, VisitorTruth.Deceptive,
                           breaking: 60);

            v.checks = Checks(
                Check("visitor.check.pre_registered", "visitor.check.value.resident_moved_out", true, AppIds.Residents),
                Check("visitor.check.id_name", "visitor.check.value.id_mismatch", true, AppIds.Residents),
                Check("visitor.check.recent_visits", "visitor.check.value.never_visited", true, AppIds.Access),
                Check("visitor.check.camera_route", "visitor.check.value.from_street", false, AppIds.Cctv));

            v.tells = Tells(
                Tell(ObsOvertalks, ReadChannel.Voice, TellWeight.Noise),
                Tell(ObsReadsPalm, ReadChannel.Camera, TellWeight.Deception),
                Tell(ObsChecksStreet, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsBlankBoard, ReadChannel.Glass, TellWeight.Noise),
                Tell(ObsSecondPerson, ReadChannel.Glass, TellWeight.Deception),
                // She gives it up almost immediately, and what she gives up is true.
                Tell(ObsDeflects, ReadChannel.Voice, TellWeight.Deception, PressureTactic.Silence));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.press.reply.ask_again", 20),
                Reply(PressureTactic.Confront, "visitor.press.reply.confront", 30, null, AppIds.Residents),
                Reply(PressureTactic.Silence, "visitor.press.reply.silence", 26, ObsDeflects),
                Reply(PressureTactic.ShowMe, "visitor.press.reply.show_me", 18),
                Reply(PressureTactic.Reassure, "visitor.press.reply.reassure", -10));
            return v;
        }

        /// <summary>
        /// Night 6. A demolition survey crew, holding an order for a building that is still
        /// occupied and dated three days from now.
        /// </summary>
        static VisitorDefinition Demolition(string id, int night, int second)
        {
            var v = Person(id, night, second, "common", "visitor.demolition.name", "visitor.demolition.purpose",
                           "D_DOOR_SERVICE", VisitorAccessLevel.Reject, VisitorTruth.Deceptive,
                           breaking: 78);

            v.checks = Checks(
                Check("visitor.check.work_order", "visitor.check.value.order_future_dated", true, AppIds.Access, 3),
                Check("visitor.check.delivery_hours", "visitor.check.value.after_hours", true, AppIds.Access, 1),
                Check("visitor.check.vehicle", "visitor.check.value.unmarked_truck", true, AppIds.Cctv),
                Check("visitor.check.target_unit", "visitor.check.value.common_area", false, AppIds.Residents));

            v.tells = Tells(
                Tell(ObsRehearsed, ReadChannel.Voice, TellWeight.Deception, PressureTactic.AskAgain),
                Tell(ObsGloves, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsEngineRunning, ReadChannel.Camera, TellWeight.Noise),
                Tell(ObsSecondPerson, ReadChannel.Glass, TellWeight.Deception),
                Tell(ObsWrongUniform, ReadChannel.Camera, TellWeight.Deception),
                Tell(ObsDeflects, ReadChannel.Voice, TellWeight.Deception, PressureTactic.None, 70));

            v.responses = Responses(
                Reply(PressureTactic.AskAgain, "visitor.demolition.reply.ask_again", 12, ObsRehearsed),
                Reply(PressureTactic.Confront, "visitor.demolition.reply.confront", 30, null, AppIds.Access),
                Reply(PressureTactic.Silence, "visitor.demolition.reply.silence", 18),
                Reply(PressureTactic.ShowMe, "visitor.demolition.reply.show_me", 26),
                Reply(PressureTactic.Reassure, "visitor.demolition.reply.reassure", -6));
            return v;
        }

        // =====================================================================
        // construction helpers
        // =====================================================================

        static VisitorDefinition Person(string id, int night, int second, string unit,
                                        string nameKey, string purposeKey, string conversationId,
                                        VisitorAccessLevel correct, VisitorTruth truth, int breaking)
        {
            var v = ScriptableObject.CreateInstance<VisitorDefinition>();
            v.name = id.ToUpperInvariant();
            v.visitorId = id;
            v.nameKey = nameKey;
            v.purposeKey = purposeKey;
            v.idCardNameKey = nameKey;
            v.targetUnit = unit;
            v.cameraId = "CAM-01";
            v.conversationId = conversationId;
            v.nightIndex = night;
            v.arrivalGameSecond = second;
            v.correctAccess = correct;
            v.truth = truth;
            v.breakingPoint = breaking;

            // Where they say they are going, and the way an honest one would walk it
            // (v3.0 38.5). A caller with no route cannot be seen leaving it, so this is not
            // decoration - it is the thing the whole pursuit half is measured against.
            v.destinationZone = ZoneOfUnit(unit);
            v.expectedRoute = RouteTo(v.destinationZone);

            // Routine callers move the visible stats only. The story visitors keep the flags,
            // the evidence and the resonance - the door must not dilute what the plot pays for.
            //
            // The numbers went up with the roster cut. Five callers a night carrying +2 each
            // is a third of the performance the old nine carried, and a night's work has to be
            // worth the same whether it is spread over nine strangers or five people.
            bool legitimate = correct != VisitorAccessLevel.Reject;
            v.onCorrect = new[]
            {
                ConsequenceDefinition.Stat(StatIds.Performance, 4, "reason.correct_visitor"),
                ConsequenceDefinition.Stat(legitimate ? StatIds.CommunityTrust : StatIds.BuildingSafety,
                                           4, "reason.correct_visitor")
            };
            v.onWrong = new[]
            {
                ConsequenceDefinition.Stat(StatIds.Performance, -4, "reason.wrong_visitor"),
                ConsequenceDefinition.Stat(legitimate ? StatIds.CommunityTrust : StatIds.BuildingSafety,
                                           -5, "reason.wrong_visitor")
            };
            v.onHold = new[] { ConsequenceDefinition.Stat(StatIds.Performance, -1, "reason.held_visitor") };
            return v;
        }

        /// <summary>
        /// The floor a claimed unit sits on. "common" callers have business in the lobby and
        /// the plant rooms rather than in anybody's flat.
        /// </summary>
        static string ZoneOfUnit(string unit)
        {
            if (string.IsNullOrEmpty(unit) || unit == "common") return ZoneIds.Lobby;

            // 1501 is the penthouse and the building only has six storeys of flats, so the
            // chairman's floor is the top one. Anything else beginning with 1 is the ground.
            if (unit.Length >= 4) return ZoneIds.Floor06;

            switch (unit[0])
            {
                case '2': return ZoneIds.Floor02;
                case '3': return ZoneIds.Floor03;
                case '4': return ZoneIds.Floor04;
                case '5': return ZoneIds.Floor05;
                case '6': return ZoneIds.Floor06;

                // Staff numbers. They are coming to the office, which is where their shift is.
                case 'B': return ZoneIds.Office;
            }

            // Everything left is a ground-floor flat, and the building models the ground floor
            // as the lobby and the rooms off it. There is no Floor01 zone because there is
            // nothing on that storey a visitor walks to except the lobby itself.
            return ZoneIds.Lobby;
        }

        /// <summary>
        /// Lobby, lift, floor. Three legs, which at ninety game seconds each is four and a half
        /// minutes of somebody being somewhere the caretaker is not.
        /// </summary>
        static string[] RouteTo(string destination)
        {
            if (string.IsNullOrEmpty(destination) || destination == ZoneIds.Lobby)
                return new[] { ZoneIds.Lobby };

            // Somewhere else on the ground floor is a walk across the lobby, not a lift ride.
            if (destination == ZoneIds.Office || destination == ZoneIds.Laundry ||
                destination == ZoneIds.ConvenienceStore || destination == ZoneIds.Playground)
                return new[] { ZoneIds.Lobby, destination };

            return new[] { ZoneIds.Lobby, ZoneIds.Elevator, destination };
        }

        static VisitorTell[] Tells(params VisitorTell[] items) { return items; }

        static VisitorTell Tell(string tellId, ReadChannel channel, TellWeight weight,
                                PressureTactic unlockedBy = PressureTactic.None, int fromAgitation = 0)
        {
            return new VisitorTell
            {
                tellId = tellId,
                labelKey = "door.obs." + tellId,
                channel = channel,
                weight = weight,
                unlockedBy = unlockedBy,
                fromAgitation = fromAgitation
            };
        }

        static TacticResponse[] Responses(params TacticResponse[] items) { return items; }

        static TacticResponse Reply(PressureTactic tactic, string replyKey, int agitationDelta,
                                    string revealsTellId = null, string requiresAppId = null)
        {
            return new TacticResponse
            {
                tactic = tactic,
                replyKey = replyKey,
                agitationDelta = agitationDelta,
                revealsTellId = revealsTellId,
                requiresAppId = requiresAppId
            };
        }

        static VisitorCheck[] Checks(params VisitorCheck[] items) { return items; }

        static VisitorCheck Check(string labelKey, string valueKey, bool contradicts, string appId, int fromNight)
        {
            var check = Check(labelKey, valueKey, contradicts, appId);
            check.fromNight = fromNight;
            return check;
        }
    }
}
