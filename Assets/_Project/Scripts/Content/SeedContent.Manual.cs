using System.Collections.Generic;
using UnityEngine;
using NO404.Core;
using NO404.Manual;

namespace NO404.ContentData
{
    /// <summary>
    /// 야간 특이상황 대응 지침 - the night response manual (v2.1 spec 0.9, 22).
    ///
    /// One page per anomaly event, plus the three general rules already in the desk drawer
    /// when the shift starts. Every page follows the spec 0.9.3 layout, and every page obeys
    /// spec 0.9.4: it says what to do and never says why the building is doing this.
    ///
    /// The text itself is in Resources/NO404/strings.csv. What lives here is the shape - how
    /// many lines each section has and which prohibitions the manual marks lethal - because
    /// that shape is what the UI lays out and what spec 0.10.5 reads before it is allowed to
    /// let an event kill anyone.
    /// </summary>
    public static partial class SeedContent
    {
        /// <summary>
        /// Builds a page. Section keys are generated from the event id and a 1-based index,
        /// so a page is described by its counts rather than by twelve near-identical string
        /// literals; the string table uses exactly the same scheme and an EditMode test walks
        /// every generated key to prove the two agree.
        /// </summary>
        static ManualPage Page(string eventId, ManualUnlockSource source, int night,
                               int observations, int prohibitions, int steps, int notes,
                               int[] lethalProhibitions = null, string unlockFlagId = null)
        {
            var page = ScriptableObject.CreateInstance<ManualPage>();
            string slug = eventId.ToLowerInvariant();

            page.pageId = ManualEventIds.PageOf(eventId);
            page.name = page.pageId;
            page.eventId = eventId;
            page.titleKey = "manual." + slug + ".title";
            page.observationKeys = Keys(slug, "observe", observations);
            page.prohibitionKeys = Keys(slug, "forbid", prohibitions);
            page.stepKeys = Keys(slug, "step", steps);
            page.noteKeys = Keys(slug, "note", notes);
            page.source = source;
            page.nightIndex = night;
            page.unlockFlagId = unlockFlagId;
            page.lethalProhibitions = lethalProhibitions ?? new int[0];
            return page;
        }

        static string[] Keys(string slug, string section, int count)
        {
            if (count <= 0) return new string[0];
            var keys = new string[count];
            for (int i = 0; i < count; i++)
                keys[i] = "manual." + slug + "." + section + "." + (i + 1);
            return keys;
        }

        public static ManualPage[] BuildManualPages()
        {
            var pages = new List<ManualPage>(20);

            // Spec 0.9.1: the prologue opens with the first three rules and nothing else.
            // Numbered R00 so it sorts ahead of every M page in the binder.
            pages.Add(Page("R00", ManualUnlockSource.Preloaded, 0,
                observations: 1, prohibitions: 1, steps: 3, notes: 1));

            // --- night 1 -----------------------------------------------------
            //
            // Spec 28 builds M06 first and M14 second, and both pages are in the binder from
            // the start: the first anomaly the player meets must be one the manual already
            // covers, or the whole system reads as arbitrary.

            // M06 명부에 없는 수취인의 택배 - do not open, verify against two records, return.
            pages.Add(Page(ManualEventIds.M06_LostParcel, ManualUnlockSource.Preloaded, 1,
                observations: 2, prohibitions: 3, steps: 5, notes: 1));

            // M14 무인 운동기구 - the wall breaker is the obvious answer and the wrong one.
            pages.Add(Page(ManualEventIds.M14_Treadmills, ManualUnlockSource.Preloaded, 1,
                observations: 1, prohibitions: 1, steps: 4, notes: 2));

            // M13 기억 엽서 - a temptation, not a hazard. Available from the first night.
            pages.Add(Page(ManualEventIds.M13_Postcards, ManualUnlockSource.Preloaded, 1,
                observations: 2, prohibitions: 1, steps: 2, notes: 1));

            // --- night 2 -----------------------------------------------------

            // M02 후진 보행자 - Dongsik wrote this one out on the back of a meter card.
            pages.Add(Page(ManualEventIds.M02_BackwardsWalker, ManualUnlockSource.DongsikNote, 2,
                observations: 3, prohibitions: 4, steps: 4, notes: 1));

            // M03 아직 작성하지 않은 기록 - learned from the recycling yard itself.
            pages.Add(Page(ManualEventIds.M03_TomorrowsLog, ManualUnlockSource.FieldHint, 2,
                observations: 1, prohibitions: 2, steps: 3, notes: 1));

            // M12 예약되지 않은 세탁 - the laundry room's own posted notice.
            pages.Add(Page(ManualEventIds.M12_NightLaundry, ManualUnlockSource.Preloaded, 2,
                observations: 2, prohibitions: 2, steps: 4, notes: 1));

            // --- night 3 -----------------------------------------------------

            // M01 표시되지 않는 층. Spec 22 M01: a player who solved M03 already knows this
            // from tomorrow's patrol log; everyone else finds the page inserted at 21:55.
            pages.Add(Page(ManualEventIds.M01_PhantomFloor, ManualUnlockSource.PriorCase, 3,
                observations: 2, prohibitions: 2, steps: 4, notes: 2));

            // M05 재활용 불가 부산물 - printed on the seal sticker sheet in the desk drawer.
            pages.Add(Page(ManualEventIds.M05_NotRecyclable, ManualUnlockSource.Preloaded, 3,
                observations: 2, prohibitions: 3, steps: 4, notes: 1));

            // M18 난간 인물. The only page in the binder with a lethal prohibition, and the
            // only event permitted to end a shift (spec 0.10.5, 22 M18).
            pages.Add(Page(ManualEventIds.M18_RoofFigure, ManualUnlockSource.Preloaded, 3,
                observations: 1, prohibitions: 3, steps: 4, notes: 1,
                lethalProhibitions: new[] { 0, 1 }));

            // --- night 4 -----------------------------------------------------

            // M16 폐쇄 벽면 응답 - a note folded into the 4F riser cover.
            pages.Add(Page(ManualEventIds.M16_KnockEcho, ManualUnlockSource.DongsikNote, 4,
                observations: 2, prohibitions: 2, steps: 4, notes: 1));

            // M17 철거된 세대의 반복 민원 - the old complaint form has the procedure on its back.
            pages.Add(Page(ManualEventIds.M17_Unit504Noise, ManualUnlockSource.Preloaded, 4,
                observations: 1, prohibitions: 2, steps: 3, notes: 1));

            // M15 형상 변이 식재 - the terrace planting card.
            pages.Add(Page(ManualEventIds.M15_PlanterGrowth, ManualUnlockSource.Preloaded, 4,
                observations: 1, prohibitions: 2, steps: 3, notes: 1));

            // M04 존재하지 않는 세대의 청소 민원 - Dongsik's longest note.
            pages.Add(Page(ManualEventIds.M04_CeilingStain, ManualUnlockSource.DongsikNote, 4,
                observations: 2, prohibitions: 3, steps: 4, notes: 2));

            // --- night 5 -----------------------------------------------------

            // M09 수조 색상 이상 - laminated to the pump room door.
            pages.Add(Page(ManualEventIds.M09_BlackWater, ManualUnlockSource.Preloaded, 5,
                observations: 2, prohibitions: 1, steps: 4, notes: 1));

            // M10 비등록 배관 - the second half of what M09 taught.
            pages.Add(Page(ManualEventIds.M10_PipeGrowth, ManualUnlockSource.PriorCase, 5,
                observations: 2, prohibitions: 2, steps: 4, notes: 2));

            // M11 감지만 되는 차량 - in the barrier operator's folder.
            pages.Add(Page(ManualEventIds.M11_GhostParking, ManualUnlockSource.Preloaded, 5,
                observations: 2, prohibitions: 2, steps: 4, notes: 1));

            // M07 동일 복도 반복 - half of it was learned on an earlier stairwell (spec 0.9.2).
            pages.Add(Page(ManualEventIds.M07_CorridorLoop, ManualUnlockSource.PriorCase, 5,
                observations: 2, prohibitions: 2, steps: 3, notes: 1));

            // --- night 6 -----------------------------------------------------

            // M08 소리 없는 놀이기구.
            pages.Add(Page(ManualEventIds.M08_ShadowChildren, ManualUnlockSource.PriorCase, 6,
                observations: 2, prohibitions: 2, steps: 4, notes: 1));

            return pages.ToArray();
        }
    }
}
