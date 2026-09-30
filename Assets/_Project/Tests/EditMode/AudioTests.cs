using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// The loudness policy from GDD 19.3 and the completeness of the cue bank.
    ///
    /// Both are the kind of rule that is true on the day it is written and quietly false three
    /// months later: someone adds a cue and forgets the file, or masters a replacement clip
    /// hot because it sounded better in isolation. Neither shows up as an error at runtime -
    /// the game just gets louder, or one moment goes silent - so they are asserted here.
    /// </summary>
    public sealed class AudioPolicyTests
    {
        const string CueFolder = "Assets/_Project/Resources/NO404/Audio/";

        static IEnumerable<AudioCue> AllCues()
        {
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
                if (cue != AudioCue.None) yield return cue;
        }

        [Test]
        public void NoCueIsLouderThanAmbiencePlusTheScareCeiling()
        {
            foreach (var cue in AllCues())
            {
                float over = AudioService.TargetLevelDb(cue) - AudioService.AmbienceLevelDb;

                Assert.LessOrEqual(over, AudioService.ScareHeadroomDb + 0.001f,
                    cue + " is mixed " + over.ToString("0.0") + " dB over ambience; GDD 19.3 caps " +
                    "the loudest thing in the game at +" + AudioService.ScareHeadroomDb + " dB");
            }
        }

        [Test]
        public void OnlyAScareUsesTheScareHeadroom()
        {
            foreach (var cue in AllCues())
            {
                if (AudioService.ClassOf(cue) == AudioCueClass.Scare) continue;

                float over = AudioService.TargetLevelDb(cue) - AudioService.AmbienceLevelDb;
                Assert.Less(over, AudioService.ScareHeadroomDb,
                    cue + " is not a scare but is mixed at the scare ceiling");
            }
        }

        [Test]
        public void EveryRoomToneSitsAtTheAmbienceLevel()
        {
            foreach (var cue in AllCues())
            {
                if (AudioService.ClassOf(cue) != AudioCueClass.Ambience) continue;

                Assert.AreEqual(AudioService.AmbienceLevelDb, AudioService.TargetLevelDb(cue), 0.001f,
                    cue + " is the bed, so it defines the reference rather than sitting above it");
            }
        }

        [Test]
        public void TheScareClassIsExactlyTheThreeMomentsTheGddNames()
        {
            var scares = new List<AudioCue>();
            foreach (var cue in AllCues())
                if (AudioService.ClassOf(cue) == AudioCueClass.Scare) scares.Add(cue);

            CollectionAssert.AreEquivalent(
                new[] { AudioCue.AnomalyStart, AudioCue.OfficeDoorForced, AudioCue.FireAlarm },
                scares,
                "Widening the scare class is how a psychological horror game turns into a " +
                "jump-scare game one cue at a time (GDD 4.4)");
        }

        [Test]
        public void EveryCueHasAShippedClip()
        {
            foreach (var cue in AllCues())
            {
                var path = CueFolder + cue + ".wav";
                Assert.IsTrue(File.Exists(path),
                    cue + " has no clip. Run: python Tools/GenerateAudio.py");
            }
        }

        [Test]
        public void EveryRoomToneIsLongEnoughToBeABed()
        {
            // 44.1 kHz 16-bit mono, so bytes are seconds. Anything shorter than this loops
            // often enough that the player hears the seam and the room stops being a room.
            const float MinimumSeconds = 8f;
            const float BytesPerSecond = 44100f * 2f;

            foreach (var cue in AllCues())
            {
                if (AudioService.ClassOf(cue) != AudioCueClass.Ambience) continue;

                var info = new FileInfo(CueFolder + cue + ".wav");
                Assert.IsTrue(info.Exists, cue + " has no clip");

                float seconds = info.Length / BytesPerSecond;
                Assert.GreaterOrEqual(seconds, MinimumSeconds,
                    cue + " is only " + seconds.ToString("0.0") + "s long");
            }
        }

        [Test]
        public void TheCueBankHasNoFilesThatNoCueNames()
        {
            if (!Directory.Exists(CueFolder)) Assert.Fail("the cue folder is missing entirely");

            foreach (var path in Directory.GetFiles(CueFolder, "*.wav"))
            {
                var name = Path.GetFileNameWithoutExtension(path);

                AudioCue parsed;
                Assert.IsTrue(Enum.TryParse(name, false, out parsed) && parsed != AudioCue.None,
                    name + ".wav is in the cue bank but no AudioCue names it, so nothing can " +
                    "ever play it");
            }
        }
    }
}
