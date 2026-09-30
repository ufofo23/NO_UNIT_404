using UnityEngine;

namespace NO404.Core
{
    /// <summary>
    /// Entry point. Creates the persistent root, initializes services in the GDD 20.6 order
    /// and hands control to GameLoop.
    ///
    /// It self-installs before the first scene loads, so the game runs from any open scene
    /// and a fresh clone needs no manual scene wiring. SCN_Bootstrap exists as the shipping
    /// entry scene (build index 0) and simply contains nothing else.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        public static Bootstrap Instance { get; private set; }

        Transform _persistentRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Instance != null) return;

            var go = new GameObject("[NO404 Bootstrap]");
            DontDestroyOnLoad(go);
            go.AddComponent<Bootstrap>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _persistentRoot = transform;

            ServiceHub.Initialize(this, _persistentRoot);

            // Steam has launched its own copy of the game and this one is closing.
            // Application.Quit is deferred to the end of the frame, so without this the
            // dying process still builds a world, starts a shift and writes settings over
            // the top of the copy that is starting up.
            if (ServiceHub.Steam.IsRestarting) return;

            // Built before the loop so a session can be opened from the menu without the
            // world having to be torn down and rebuilt around it. It gets no parent on
            // purpose - see NetSession.Create.
            Net.NetSession.Create();

            GameLoop.Create(_persistentRoot);

            Log.Info("Boot", "NO UNIT 404 " + Application.version + " ready");
        }

        void OnApplicationQuit()
        {
            // A process handing off to a Steam-launched copy must not write settings on the
            // way out: the copy is already reading that file, and the loser of the race is
            // whichever one wrote last.
            if (!ServiceHub.Steam.IsRestarting) ServiceHub.Settings.Save();

            ServiceHub.Shutdown();
        }
    }
}
