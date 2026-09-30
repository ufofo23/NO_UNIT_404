using UnityEngine;
using NO404.Core;

namespace NO404.Visitors
{
    /// <summary>Stable identity binding shared by the caller and admitted visitor.</summary>
    public static class VisitorAppearance
    {
        public const string FirstGuestId = "vis_n0_guest_jiwoo";
        public const string FirstGuestResource = "NO404/Characters/FirstGuest/FirstGuestCharacter";

        public static GameObject Build(Transform parent, string visitorId, string name)
        {
            if (visitorId == FirstGuestId)
            {
                var prefab = Resources.Load<GameObject>(FirstGuestResource);
                if (prefab != null)
                {
                    var instance = Object.Instantiate(prefab, parent, false);
                    instance.name = name;
                    return instance;
                }
                Log.Warn("Visitors", "First guest character asset missing: " + FirstGuestResource);
            }
            return DummyBody.Build(parent, DummyBody.VisitorCap, false, false, name);
        }
    }
}
