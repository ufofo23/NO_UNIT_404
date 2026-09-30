using UnityEngine;
using NO404.Core;

namespace NO404.Interaction
{
    /// <summary>
    /// Centre-screen raycast with the per-target distances from GDD 8.3, plus hold handling
    /// and re-entrancy protection (GDD 20.14: no duplicate input while an interaction runs).
    ///
    /// Highlighting is deliberately a small brightness lift on the target's material rather
    /// than a yellow outline (GDD 8.3).
    /// </summary>
    public sealed class InteractionRaycaster : MonoBehaviour
    {
        public const float DefaultRange = 2.4f;

        [SerializeField] Transform _rayOrigin;
        [SerializeField] LayerMask _mask = ~0;

        readonly RaycastHit[] _hits = new RaycastHit[8];

        IInteractable _current;
        Component _currentComponent;
        Renderer _highlighted;
        MaterialPropertyBlock _block;
        float _holdTimer;
        bool _busy;

        public IInteractable Current { get { return _current; } }
        public InteractionPrompt CurrentPrompt { get; private set; }
        public float HoldProgress { get; private set; }
        public bool HasTarget { get { return _current != null; } }

        public void Configure(Transform rayOrigin) { _rayOrigin = rayOrigin; }

        void Awake()
        {
            if (_rayOrigin == null) _rayOrigin = transform;
            _block = new MaterialPropertyBlock();
        }

        public void Scan(in PlayerContext template)
        {
            var origin = _rayOrigin.position;
            var direction = _rayOrigin.forward;

            int count = Physics.RaycastNonAlloc(origin, direction, _hits, DefaultRange, _mask,
                                                QueryTriggerInteraction.Collide);

            IInteractable best = null;
            Component bestComponent = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                var candidate = hit.collider.GetComponentInParent<InteractableBase>();
                if (candidate == null) continue;
                if (hit.distance > candidate.Range) continue;
                if (hit.distance >= bestDistance) continue;

                best = candidate;
                bestComponent = candidate;
                bestDistance = hit.distance;
            }

            if (!ReferenceEquals(best, _current))
            {
                ClearHighlight();
                _current = best;
                _currentComponent = bestComponent;
                _holdTimer = 0f;
                HoldProgress = 0f;
                ApplyHighlight();
            }

            if (_current == null) { CurrentPrompt = default(InteractionPrompt); return; }

            var context = template;
            context.DistanceToTarget = bestDistance;
            CurrentPrompt = _current.GetPrompt(context);
        }

        /// <summary>Returns true on the frame the interaction actually fires.</summary>
        public bool Tick(bool interactPressed, bool interactHeld, float deltaTime, in PlayerContext template)
        {
            if (_current == null || _busy) { HoldProgress = 0f; return false; }

            var context = template;
            context.DistanceToTarget = _currentComponent != null
                ? Vector3.Distance(_rayOrigin.position, ((Component)_currentComponent).transform.position)
                : 0f;

            string reasonKey;
            if (!_current.CanInteract(context, out reasonKey))
            {
                HoldProgress = 0f;
                return false;
            }

            if (CurrentPrompt.Kind == InteractionKind.Hold)
            {
                float required = CurrentPrompt.HoldSeconds <= 0f ? 1f : CurrentPrompt.HoldSeconds;
                if (interactHeld)
                {
                    _holdTimer += deltaTime;
                    HoldProgress = Mathf.Clamp01(_holdTimer / required);
                    if (_holdTimer < required) return false;
                }
                else
                {
                    _holdTimer = 0f;
                    HoldProgress = 0f;
                    return false;
                }
            }
            else if (!interactPressed)
            {
                return false;
            }

            _holdTimer = 0f;
            HoldProgress = 0f;

            _busy = true;
            try
            {
                _current.Interact(context);
            }
            catch (System.Exception e)
            {
                Log.Error("Interaction", "Interact threw: " + e);
            }
            finally
            {
                _busy = false;
            }

            return true;
        }

        void ApplyHighlight()
        {
            if (_currentComponent == null) return;
            _highlighted = ((Component)_currentComponent).GetComponentInChildren<Renderer>();
            if (_highlighted == null) return;

            _highlighted.GetPropertyBlock(_block);
            _block.SetColor(ShaderIds.BaseColor, HighlightColor(_highlighted));
            _highlighted.SetPropertyBlock(_block);
        }

        void ClearHighlight()
        {
            if (_highlighted == null) return;
            _highlighted.GetPropertyBlock(_block);
            _block.SetColor(ShaderIds.BaseColor, BaseColorOf(_highlighted));
            _highlighted.SetPropertyBlock(_block);
            _highlighted = null;
        }

        static Color BaseColorOf(Renderer renderer)
        {
            var material = renderer.sharedMaterial;
            return material != null && material.HasProperty(ShaderIds.BaseColor)
                ? material.GetColor(ShaderIds.BaseColor)
                : Color.white;
        }

        static Color HighlightColor(Renderer renderer)
        {
            var color = BaseColorOf(renderer);
            // Subtle brightness lift, not an outline (GDD 8.3).
            return new Color(
                Mathf.Min(1f, color.r * 1.35f),
                Mathf.Min(1f, color.g * 1.35f),
                Mathf.Min(1f, color.b * 1.35f),
                color.a);
        }

        void OnDisable() { ClearHighlight(); }
    }

    public static class ShaderIds
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    }
}
