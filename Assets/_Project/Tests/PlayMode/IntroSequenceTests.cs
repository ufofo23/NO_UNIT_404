using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.UI;

namespace NO404.Tests
{
    /// <summary>
    /// The intro has to end.
    ///
    /// This exists because it did not. The sequence read the skip key before it advanced its
    /// own clock, and reading it threw every frame - the project runs on the Input System
    /// package alone, where <c>UnityEngine.Input</c> raises rather than returns - so the first
    /// line was drawn and the whole thing stopped there. No key could rescue it, because the
    /// thing that read keys was the thing throwing. It shipped in a build.
    ///
    /// Both halves of that are worth a test and neither is provable by looking at the screen
    /// for a moment: an intro that runs to its end takes fifteen seconds to be wrong, and a
    /// player who cannot get out of it has no way to tell you which frame broke.
    /// </summary>
    public sealed class IntroSequenceTests
    {
        GameObject _host;
        IntroSequenceView _intro;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("IntroTestHost") { hideFlags = HideFlags.HideAndDontSave };
            _intro = IntroSequenceView.Create(_host.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>
        /// Left alone, it finishes - and calls back, because the campaign does not start until
        /// it does. Allowed a second of slack over its own stated length and no more.
        /// </summary>
        [UnityTest]
        public IEnumerator ItRunsToTheEndOnItsOwn()
        {
            bool finished = false;
            _intro.OnFinished = () => finished = true;
            _intro.Play();

            Assert.IsTrue(_intro.IsRunning, "it should be running the moment it is played");

            float deadline = Time.unscaledTime + IntroSequenceView.TotalSeconds + 1f;
            while (!finished && Time.unscaledTime < deadline) yield return null;

            Assert.IsTrue(finished,
                "the intro never handed control back - the campaign can never start");
            Assert.IsFalse(_intro.IsRunning);
            Assert.IsFalse(_intro.gameObject.activeSelf, "it should take itself off the screen");
        }

        /// <summary>
        /// It reaches every line rather than sitting on the first one.
        ///
        /// The failure that shipped looked exactly like a working intro for three and a half
        /// seconds, which is why this samples the text rather than only the ending.
        /// </summary>
        [UnityTest]
        public IEnumerator ItAdvancesThroughEveryLine()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            var label = _host.GetComponentInChildren<UnityEngine.UI.Text>(true);

            _intro.Play();

            float deadline = Time.unscaledTime + IntroSequenceView.TotalSeconds + 1f;
            while (_intro.IsRunning && Time.unscaledTime < deadline)
            {
                foreach (var text in _host.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    if (!string.IsNullOrEmpty(text.text)) seen.Add(text.text);

                yield return null;
            }

            Assert.IsNotNull(label);
            Assert.GreaterOrEqual(seen.Count, IntroSequenceView.LineKeys.Length,
                "the intro stopped on a line instead of running through all of them");
        }

        /// <summary>
        /// Cancelling drops the callback rather than firing it, so a caretaker who backs out
        /// of the menu does not silently get a new campaign started under them.
        /// </summary>
        [UnityTest]
        public IEnumerator CancellingDoesNotStartWhatItWasGoingToStart()
        {
            bool finished = false;
            _intro.OnFinished = () => finished = true;

            _intro.Play();
            yield return null;

            _intro.Cancel();
            yield return null;

            Assert.IsFalse(finished, "cancel is not the same as reaching the end");
            Assert.IsFalse(_intro.IsRunning);
        }
    }
}
