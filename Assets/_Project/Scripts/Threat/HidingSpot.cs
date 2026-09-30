using UnityEngine;
using NO404.Core;
using NO404.Interaction;

namespace NO404.Threat
{
    /// <summary>
    /// A place the player can wait out a patrol (GDD 9.6: two cabinets and one dark utility
    /// corner). GDD 15.2 requires hiding places to read clearly, so a spot lifts its own
    /// brightness whenever a threat is active rather than relying on the player guessing.
    /// </summary>
    public sealed class HidingSpot : InteractableBase
    {
        [SerializeField] Transform _anchor;

        Renderer _renderer;
        MaterialPropertyBlock _block;
        Color _baseColor;

        public Transform Anchor { get { return _anchor != null ? _anchor : transform; } }
        public bool Occupied { get; private set; }

        public void Setup(Transform anchor, string labelKey)
        {
            _anchor = anchor;
            _labelKey = labelKey;
            _range = 1.8f;
            _repeatable = true;
        }

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();

            if (_renderer != null && _renderer.sharedMaterial != null &&
                _renderer.sharedMaterial.HasProperty(ShaderIds.BaseColor))
                _baseColor = _renderer.sharedMaterial.GetColor(ShaderIds.BaseColor);
            else
                _baseColor = Color.gray;
        }

        public override InteractionPrompt GetPrompt(in PlayerContext context)
        {
            string reasonKey;
            if (!CanInteract(context, out reasonKey)) return InteractionPrompt.Blocked(_labelKey, reasonKey);

            return InteractionPrompt.Simple(Occupied ? "ui.prompt.leave_hiding" : _labelKey);
        }

        protected override void OnInteract(in PlayerContext context)
        {
            if (Occupied) ServiceHub.Threat.LeaveHiding();
            else ServiceHub.Threat.EnterHiding(this);
        }

        public void SetOccupied(bool occupied) { Occupied = occupied; }

        void Update()
        {
            if (_renderer == null || ServiceHub.Threat == null) return;

            // Visible only while it matters. Outside a threat window this is just furniture.
            bool highlight = ServiceHub.Threat.IsActive;
            var target = highlight
                ? new Color(_baseColor.r * 1.9f, _baseColor.g * 2.1f, _baseColor.b * 2.4f, _baseColor.a)
                : _baseColor;

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ShaderIds.BaseColor, target);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
