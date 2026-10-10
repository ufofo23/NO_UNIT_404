using UnityEngine;
using NO404.Core;

namespace NO404.Gameplay
{
    /// <summary>
    /// Soft shadows from a light only while the caretaker is near it.
    ///
    /// Every fitting in the building is a real light now, and a point light's shadow is six
    /// renders. The floors are islands far apart in world space, so distance alone is enough
    /// to keep the cost to the room the player is actually standing in.
    /// </summary>
    public sealed class ProximityShadow : MonoBehaviour
    {
        public const float Range = 11f;

        Light _light;
        float _next;

        void Awake() { _light = GetComponent<Light>(); }

        void Update()
        {
            if (_light == null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.4f + Random.value * 0.2f;

            var player = GameLoop.Instance != null ? GameLoop.Instance.PlayerTransform : null;
            bool near = player != null && (player.position - transform.position).sqrMagnitude < Range * Range;
            var wanted = near ? LightShadows.Soft : LightShadows.None;
            if (_light.shadows != wanted) _light.shadows = wanted;
        }
    }

    /// <summary>
    /// A fluorescent tube on its way out: on for a while, then a stutter of dropouts.
    /// Drives its light and its tube's glow together, per renderer, so two dying tubes on
    /// two floors never blink in step.
    /// </summary>
    public sealed class FlickerLight : MonoBehaviour
    {
        Light _light;
        Renderer _tube;
        Color _glow;
        float _base, _until;
        bool _on = true;
        MaterialPropertyBlock _block;

        public void Setup(Light light, Renderer tube, Color glow)
        {
            _light = light;
            _tube = tube;
            _glow = glow;
            _base = light != null ? light.intensity : 0f;
            _block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (Time.time < _until) return;

            // Mostly lit; when it goes, it goes in short bursts.
            _on = !_on || Random.value < 0.15f;
            _until = Time.time + (_on ? Random.Range(0.6f, 4.5f) : Random.Range(0.04f, 0.18f));

            float k = _on ? Random.Range(0.75f, 1f) : Random.Range(0f, 0.12f);
            if (_light != null) _light.intensity = _base * k;
            if (_tube != null)
            {
                _tube.GetPropertyBlock(_block);
                _block.SetColor(Interaction.ShaderIds.BaseColor, _glow * Mathf.Max(0.08f, k));
                _tube.SetPropertyBlock(_block);
            }
        }
    }
}
