using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using NO404.Core;
using NO404.Save;

namespace NO404.Net
{
    /// <summary>
    /// The host's shift, as a thing that can be handed to somebody else (v3.0 46.1).
    ///
    /// The fine-grained sync in <see cref="NetShift"/> carries the four things that have to
    /// arrive within a frame - the clock, the door, the tracking board, the doors of the
    /// building. Everything else the night decides is slower and much wider: which cases are
    /// open, which objectives are ticked, what is in the evidence tray, how much reserve is
    /// left, which anomalies have already fired, what the access log says. Writing a
    /// NetworkVariable for each of those would be a hundred fields to keep in step by hand,
    /// and the first service somebody adds without touching this file would silently desync a
    /// three-handed shift in a way no test would catch.
    ///
    /// So the mirror is the save file. That set of things is exactly what a save writes down,
    /// the schema is already versioned and migrated, and a service that starts being saved
    /// starts being mirrored on the same day. The host captures one, this compresses it, and
    /// the client applies it through <see cref="SaveService.ApplyShiftMirror"/>.
    ///
    /// It is sent only when it changes, which on a quiet stretch of a night is almost never -
    /// the fields that move every second (the clock) are deliberately blanked out of the
    /// comparison because <see cref="NetShift"/> already carries them a different way.
    /// </summary>
    public static class ShiftMirror
    {
        /// <summary>Netcode's named-message channel this travels on.</summary>
        public const string MessageName = "NO404_Shift";

        /// <summary>Slowest useful rate. Anything faster is spending bandwidth on a redraw.</summary>
        public const float PushInterval = 0.5f;

        /// <summary>
        /// Compressed size past which something has gone wrong rather than a night having got
        /// busy. Named messages fragment, but not without limit.
        /// </summary>
        public const int WarnBytes = 48 * 1024;

        /// <summary>
        /// What the host is currently claiming the shift looks like.
        ///
        /// Returned as JSON rather than bytes so the caller can compare it against the last
        /// one cheaply; compression happens after that test, because compressing something
        /// only to discover it was identical is the expensive half.
        /// </summary>
        public static string CaptureJson()
        {
            if (ServiceHub.Save == null) return null;

            var data = ServiceHub.Save.Capture(SaveService.ManualSlot, SaveReason.Manual);
            Strip(data);

            return JsonUtility.ToJson(data);
        }

        /// <summary>
        /// Takes out everything that is a fact about one caretaker rather than about the shift.
        ///
        /// Leaving any of these in would be worse than useless: it would teleport a colleague,
        /// rewrite the floors they had walked and hand them somebody else's achievements. The
        /// header fields go too - a timestamp that changes every capture would defeat the
        /// change test and make the mirror send continuously for a night in which nothing
        /// whatsoever happened.
        /// </summary>
        static void Strip(SaveData data)
        {
            data.saveUtc = string.Empty;
            data.slot = 0;
            data.reason = 0;

            data.player = null;
            data.stairs = null;

            // A caretaker's body and nerve are their own (v5.0 6, 7). v5.0 is a solo document
            // and says nothing about sharing them, and the only reading that survives contact
            // with three people is one body each: mirroring the host's would mean somebody who
            // had just walked through smoke healed the moment a colleague sat down safely, and
            // somebody who had done nothing all night inherited the wound.
            //
            // The choices below it are the opposite kind of fact - what the shift decided
            // about the building - so those travel.
            data.hp = 0;
            data.san = 0;
            data.firstAidRemaining = 0;
            data.groundingRemaining = 0;
            data.doors.Clear();              // NetShift syncs these immediately, per door
            data.visitedZones.Clear();
            data.openedApps.Clear();
            data.dialogueChoices.Clear();
            data.unlockedAchievements.Clear();
            data.CurrentDialogueId = null;
            data.CurrentDialogueNodeId = null;

            // The clock is a NetworkVariable. Left in, it would change the payload every game
            // second and make every other field ride along with it.
            data.gameSecond = 0;
        }

        public static byte[] Compress(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var raw = Encoding.UTF8.GetBytes(json);

            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
                    gzip.Write(raw, 0, raw.Length);

                return output.ToArray();
            }
        }

        public static SaveData Decompress(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return null;

            try
            {
                using (var input = new MemoryStream(payload))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    var json = Encoding.UTF8.GetString(output.ToArray());
                    return JsonUtility.FromJson<SaveData>(json);
                }
            }
            catch (System.Exception e)
            {
                // A mirror that cannot be read is not worth guessing at: applying half a shift
                // is how one caretaker ends up filing a report against evidence the others
                // cannot see. The next push is half a second away.
                Log.Error("Multiplayer", "unreadable shift mirror: " + e.Message);
                return null;
            }
        }
    }
}
