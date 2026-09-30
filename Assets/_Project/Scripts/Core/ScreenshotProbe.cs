using System.Collections;
using UnityEngine;

namespace NO404.Core
{
    /// <summary>
    /// Takes a picture of the running game and writes it to disk.
    ///
    /// This exists because a build was reported as "everything is magenta" and two rounds of
    /// reading code produced a fix that did not fix it. Guessing at a rendering problem from
    /// source is how that happens; the only reliable evidence is the frame itself.
    ///
    /// Development builds only - it is gated on <see cref="Debug.isDebugBuild"/> and does
    /// nothing at all without the command line flag, so it cannot fire for a player.
    ///
    ///   NO_UNIT_404.exe -no404-shot "C:\path\shot.png"          main menu
    ///   NO_UNIT_404.exe -no404-shot "C:\p\s.png" -no404-newgame  in the building
    ///   ... -no404-delay 12                                      seconds before the shutter
    /// </summary>
    public sealed class ScreenshotProbe : MonoBehaviour
    {
        const float DefaultDelay = 8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;

            var path = Argument("-no404-shot");
            if (string.IsNullOrEmpty(path)) return;

            var go = new GameObject("[NO404 Screenshot]");
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenshotProbe>().Begin(path);
        }

        void Begin(string path) { StartCoroutine(Capture(path)); }

        /// <summary>
        /// Stops anything else from pressing buttons while a picture is being taken.
        ///
        /// Not paranoia. A launched window takes focus, and stray keystrokes from whatever
        /// started it land on the menu: one capture run opened a Relay room by itself, another
        /// walked into the pause screen, and a third quit the game eight seconds in - each
        /// time producing a measurement of something other than what was being measured.
        /// </summary>
        void SilenceInput()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null) eventSystem.enabled = false;
        }

        IEnumerator Capture(string path)
        {
            float delay = DefaultDelay;
            var raw = Argument("-no404-delay");
            if (!string.IsNullOrEmpty(raw)) float.TryParse(raw, out delay);

            // Let the menu build itself before anything is asked of it.
            yield return new WaitForSecondsRealtime(2f);

            SilenceInput();

            if (HasFlag("-no404-newgame"))
            {
                if (GameLoop.Instance != null)
                {
                    // Without the intro: the probe is here to photograph the shift, and
                    // fifteen seconds of title cards would be what it photographed.
                    Log.Info("Probe", "starting a game for the screenshot");
                    GameLoop.Instance.NewGame(false);
                }
                else
                {
                    Log.Error("Probe", "no GameLoop to start a game with");
                }
            }

            // Keep silencing it: the world build creates its own EventSystem partway through.
            float until = Time.realtimeSinceStartup + Mathf.Max(1f, delay);
            while (Time.realtimeSinceStartup < until)
            {
                SilenceInput();
                yield return null;
            }

            // The frame has to be finished before it can be read.
            yield return new WaitForEndOfFrame();

            ScreenCapture.CaptureScreenshot(path);
            Log.Info("Probe", "screenshot requested at " + path);

            // CaptureScreenshot is asynchronous and writes on a later frame. Waiting for the
            // file rather than for a fixed delay: the first version guessed three seconds and
            // quit before Unity had finished, which produced no picture and no error either.
            for (int i = 0; i < 40 && !System.IO.File.Exists(path); i++)
                yield return new WaitForSecondsRealtime(0.5f);

            Log.Info("Probe", System.IO.File.Exists(path)
                ? "screenshot written to " + path
                : "screenshot never appeared at " + path);

            Application.Quit();
        }

        static bool HasFlag(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == flag) return true;
            return false;
        }

        static string Argument(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }
    }
}
