using System.Collections.Generic;
using NO404.CCTV;
using NO404.Core;

namespace NO404.ContentData
{
    /// <summary>
    /// The complete anomaly catalogue from GDD 12.3 - all 36 types, scheduled across the
    /// seven shifts.
    ///
    /// A type may be scheduled more than once (the elevator reading 16 is a quiet tease on
    /// night 1 and the whole point of the case on night 3), so instances carry a unique id
    /// while the catalogue number drives the description and the family.
    /// </summary>
    public static partial class SeedContent
    {
        public static AnomalyDefinition[] BuildAnomalies()
        {
            var list = new List<AnomalyDefinition>();

            // ---- night 1: the game opens here (GDD 9.1 / 9.2) ----
            //
            // Night 0 had two anomalies and no longer exists. Only one of them was worth
            // keeping and it is the first one below: a child standing outside the office door
            // who is not there when the real door opens (GDD 4.4). CAM-03 is that camera
            // (GDD 12.1) and the slipper it leaves inside the door is the trace.
            //
            // Its window now opens at 22:00 rather than 22:20, which reads as earlier and is
            // in fact later: CctvService cannot fire anything while the wall is cut, so this
            // waits out the blackout and lands on the first thing the player sees when the
            // basement board brings the feeds back. They restore the cameras, open the app,
            // and there is a child outside the room they just left empty.
            //
            // The other night-0 anomaly - a quiet object change on the lobby feed - is simply
            // deleted. Night 1 now runs five, one more than before, and adding a sixth would
            // put an unattended night 1 above night 2 on the ladder and break the ramp.
            list.Add(Anomaly(14, "CAM-03", 1, At(22, 0), At(23, 30), 9f,
                             evidenceId: "EV_RED_SLIPPER", caseId: "N1-M01",
                             setsFlagId: FlagIds.SlipperDropped));

            list.Add(Anomaly(5, "CAM-06", 1, At(22, 40), At(23, 30), 6f,
                             evidenceId: "EV_CAM06_SNAP", caseId: "N1-M01"));
            list.Add(Anomaly(26, "CAM-04", 1, At(23, 30), At(1, 0), 9f, evidenceId: "EV_404_DOOR"));
            list.Add(Anomaly(13, "CAM-03", 1, At(0, 0), At(1, 0), 6f));
            list.Add(Anomaly(4, "CAM-08", 1, At(1, 0), At(3, 0), 7f, instanceSuffix: "TEASE"));

            // ---- night 2: the couriers ----
            list.Add(Anomaly(9, "CAM-02", 2, At(22, 18), At(22, 40), 10f,
                             caseId: "N2-M01"));
            list.Add(Anomaly(17, "CAM-02", 2, At(22, 45), At(23, 30), 8f,
                             evidenceId: "E06_CCTV_WEATHER_MISMATCH", caseId: "N2-M01"));
            list.Add(Anomaly(2, "CAM-02", 2, At(23, 40), At(0, 40), 6f));
            list.Add(Anomaly(8, "CAM-09", 2, At(0, 20), At(1, 20), 7f));
            list.Add(Anomaly(6, "CAM-03", 2, At(1, 30), At(3, 0), 5f));
            // Type 1 used to be night 0's quiet opener. GDD 12.3 wants all 36 types scheduled
            // and night 1 is already carrying the child at the office door, so the lobby's
            // object change moves here - late, on a channel nothing else is using after 00:50,
            // and on the night whose whole case is about what the lobby feed is showing.
            list.Add(Anomaly(1, "CAM-02", 2, At(0, 50), At(2, 0), 8f));

            // ---- night 3: the sixteenth floor ----
            list.Add(Anomaly(4, "CAM-08", 3, At(22, 30), At(23, 30), 12f,
                             caseId: "N3-M01"));
            list.Add(Anomaly(28, "CAM-08", 3, At(23, 10), At(0, 30), 9f));
            list.Add(Anomaly(11, "CAM-12", 3, At(23, 0), At(0, 0), 7f));
            list.Add(Anomaly(3, "CAM-04", 3, At(0, 10), At(1, 10), 6f));
            list.Add(Anomaly(18, "CAM-01", 3, At(1, 0), At(2, 0), 6f));
            list.Add(Anomaly(21, "CAM-07", 3, At(2, 0), At(3, 0), 7f));
            list.Add(Anomaly(27, "CAM-05", 3, At(0, 40), At(2, 0), 8f));

            // ---- night 4: the record ----
            list.Add(Anomaly(16, "CAM-03", 4, At(22, 10), At(23, 0), 7f));
            list.Add(Anomaly(7, "CAM-04", 4, At(23, 0), At(0, 0), 8f));
            list.Add(Anomaly(19, "CAM-04", 4, At(0, 20), At(1, 30), 10f));
            list.Add(Anomaly(14, "CAM-06", 4, At(1, 0), At(2, 0), 7f));
            list.Add(Anomaly(10, "CAM-05", 4, At(23, 20), At(0, 20), 6f));
            list.Add(Anomaly(25, "CAM-07", 4, At(1, 40), At(3, 0), 9f));
            list.Add(Anomaly(29, "CAM-02", 4, At(2, 0), At(3, 30), 8f));

            // ---- night 5: the outage and the replay ----
            list.Add(Anomaly(15, "CAM-10", 5, At(23, 18), At(0, 20), 9f));
            list.Add(Anomaly(32, "CAM-10", 5, At(23, 30), At(0, 40), 7f));
            list.Add(Anomaly(30, "CAM-10", 5, At(0, 10), At(1, 10), 6f));
            list.Add(Anomaly(35, "CAM-03", 5, At(23, 40), At(0, 30), 8f));
            list.Add(Anomaly(36, "CAM-04", 5, At(0, 40), At(1, 40), 14f,
                             evidenceId: "E17_2009_CCTV_REPLAY", caseId: "N5-M01"));
            list.Add(Anomaly(34, "CAM-03", 5, At(1, 50), At(3, 0), 6f, feedState: FeedState.SignalLost));
            list.Add(Anomaly(12, "CAM-05", 5, At(2, 0), At(3, 20), 7f));
            list.Add(Anomaly(20, "CAM-03", 5, At(3, 0), At(4, 0), 6f));
            list.Add(Anomaly(22, "CAM-08", 5, At(2, 30), At(3, 40), 6f));

            // ---- night 6: the last shift ----
            list.Add(Anomaly(31, "CAM-04", 6, At(23, 0), At(0, 0), 6f));
            list.Add(Anomaly(33, "CAM-04", 6, At(1, 0), At(2, 30), 9f));
            list.Add(Anomaly(23, "CAM-03", 6, At(0, 0), At(1, 0), 8f));
            list.Add(Anomaly(24, "CAM-03", 6, At(2, 30), At(3, 30), 8f));
            list.Add(Anomaly(36, "CAM-04", 6, At(3, 30), At(5, 0), 14f, instanceSuffix: "FINALE"));

            return list.ToArray();
        }

        /// <summary>
        /// Every anomaly stays on screen at least four seconds and stays reviewable, which is
        /// the fairness floor in GDD 12.4. The helper enforces it rather than trusting data.
        /// </summary>
        static AnomalyDefinition Anomaly(int typeNumber, string cameraId, int night,
                                         int windowBegin, int windowEnd, float duration,
                                         string evidenceId = null, string objectiveId = null,
                                         string caseId = null, FeedState feedState = FeedState.Live,
                                         string instanceSuffix = null, string setsFlagId = null)
        {
            var id = "ANOMALY_" + typeNumber.ToString("00") + "_N" + night;
            if (!string.IsNullOrEmpty(instanceSuffix)) id += "_" + instanceSuffix;

            return new AnomalyDefinition
            {
                anomalyId = id,
                typeNumber = typeNumber,
                cameraId = cameraId,
                category = AnomalyCatalogue.CategoryOf(typeNumber),
                descriptionKey = AnomalyCatalogue.DescriptionKey(typeNumber),
                nightIndex = night,
                windowBegin = windowBegin,
                windowEnd = windowEnd,
                durationSeconds = duration < 4f ? 4f : duration,
                reviewable = true,
                evidenceId = evidenceId,
                objectiveId = objectiveId,
                setsFlagId = setsFlagId,
                caseId = caseId,
                feedState = feedState
            };
        }
    }
}
