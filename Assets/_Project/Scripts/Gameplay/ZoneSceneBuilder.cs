using UnityEngine;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// The only thing a streamed zone scene contains.
    ///
    /// Keeping the scene asset this thin means the greybox stays generated in code while the
    /// scene split is real: Unity loads and unloads these additively, memory is genuinely
    /// released, and the art pass can replace the builder in a given scene with real meshes
    /// one floor at a time without touching any other scene.
    /// </summary>
    public sealed class ZoneSceneBuilder : MonoBehaviour
    {
        [SerializeField] string _groupName;

        public string GroupName { get { return _groupName; } }

        public void SetGroup(string groupName) { _groupName = groupName; }

        void Awake()
        {
            if (string.IsNullOrEmpty(_groupName))
            {
                Log.Error("Stream", "ZoneSceneBuilder has no group assigned");
                return;
            }

            var zones = ZoneGroups.ZonesIn(_groupName);
            if (zones.Length == 0)
            {
                Log.Error("Stream", "unknown zone group " + _groupName);
                return;
            }

            var builder = gameObject.AddComponent<WorldBuilder>();
            builder.Build(zones);
        }
    }
}
