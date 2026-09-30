using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NO404.Evidence;

namespace NO404.UI
{
    /// <summary>
    /// A freely placed evidence card on the board (GDD 16.13). Dragging the body moves the
    /// card; dragging the small connector on its edge starts a link to another card.
    /// </summary>
    public sealed class BoardCard : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public string EvidenceId { get; private set; }

        public Action<BoardCard> OnSelected;
        public Action<BoardCard> OnMoved;

        RectTransform _rect;
        RectTransform _board;
        Canvas _canvas;
        Image _frame;
        Color _baseColor;

        public RectTransform Rect { get { return _rect; } }

        public void Bind(string evidenceId, RectTransform board, Canvas canvas, Image frame)
        {
            EvidenceId = evidenceId;
            _rect = (RectTransform)transform;
            _board = board;
            _canvas = canvas;
            _frame = frame;
            _baseColor = frame != null ? frame.color : Color.white;
        }

        public void SetSelected(bool selected)
        {
            if (_frame == null) return;
            _frame.color = selected ? UiFactory.Accent * 0.75f : _baseColor;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
            if (OnSelected != null) OnSelected(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;

            _rect.anchoredPosition += eventData.delta / scale;
            ClampToBoard();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ClampToBoard();
            if (OnMoved != null) OnMoved(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return;
            if (OnSelected != null) OnSelected(this);
        }

        void ClampToBoard()
        {
            if (_board == null) return;

            var boardSize = _board.rect.size;
            var cardSize = _rect.rect.size;

            var position = _rect.anchoredPosition;
            position.x = Mathf.Clamp(position.x, 0f, Mathf.Max(0f, boardSize.x - cardSize.x));
            position.y = Mathf.Clamp(position.y, -Mathf.Max(0f, boardSize.y - cardSize.y), 0f);
            _rect.anchoredPosition = position;
        }
    }

    /// <summary>
    /// The connector on a card's edge. Dragging it out and releasing over another card is how
    /// the player proposes a link; the relation is chosen afterwards.
    /// </summary>
    public sealed class LinkHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<BoardCard, BoardCard> OnLinkProposed;

        BoardCard _owner;
        RectTransform _preview;

        public void Bind(BoardCard owner, RectTransform preview)
        {
            _owner = owner;
            _preview = preview;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_preview != null) _preview.gameObject.SetActive(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_preview == null || _owner == null) return;
            BoardLinkLayer.StretchBetween(_preview, _owner.Rect, ScreenToBoard(eventData));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_preview != null) _preview.gameObject.SetActive(false);

            var hit = eventData.pointerCurrentRaycast.gameObject;
            if (hit == null || _owner == null) return;

            var target = hit.GetComponentInParent<BoardCard>();
            if (target == null || target == _owner) return;

            if (OnLinkProposed != null) OnLinkProposed(_owner, target);
        }

        Vector2 ScreenToBoard(PointerEventData eventData)
        {
            var board = (RectTransform)_preview.parent;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                board, eventData.position, eventData.pressEventCamera, out local);
            return local;
        }
    }

    /// <summary>
    /// Draws the connections between placed cards. uGUI has no line primitive, so each link
    /// is a thin rotated image; relation is encoded in colour *and* thickness so the board
    /// stays readable in the colour-blind mode required by GDD 16.13.
    /// </summary>
    public static class BoardLinkLayer
    {
        public static Color ColorFor(EvidenceRelation relation)
        {
            switch (relation)
            {
                case EvidenceRelation.SameTime: return new Color(0.35f, 0.62f, 0.68f);
                case EvidenceRelation.SamePerson: return new Color(0.78f, 0.66f, 0.36f);
                case EvidenceRelation.LocationContradiction: return new Color(0.72f, 0.45f, 0.42f);
                case EvidenceRelation.Cause: return new Color(0.42f, 0.70f, 0.52f);
                default: return new Color(0.60f, 0.50f, 0.70f);
            }
        }

        public static string LabelKeyFor(EvidenceRelation relation)
        {
            switch (relation)
            {
                case EvidenceRelation.SameTime: return "ui.evidence.link.same_time";
                case EvidenceRelation.SamePerson: return "ui.evidence.link.same_person";
                case EvidenceRelation.LocationContradiction: return "ui.evidence.link.location";
                case EvidenceRelation.Cause: return "ui.evidence.link.cause";
                default: return "ui.evidence.link.testimony";
            }
        }

        public static float ThicknessFor(EvidenceRelation relation)
        {
            switch (relation)
            {
                case EvidenceRelation.SameTime: return 2f;
                case EvidenceRelation.SamePerson: return 4f;
                case EvidenceRelation.LocationContradiction: return 6f;
                case EvidenceRelation.Cause: return 8f;
                default: return 10f;
            }
        }

        /// <summary>Positions a rect as a line between two card centres.</summary>
        public static void Stretch(RectTransform line, RectTransform from, RectTransform to, float thickness)
        {
            var a = from.anchoredPosition + new Vector2(from.rect.width * 0.5f, -from.rect.height * 0.5f);
            var b = to.anchoredPosition + new Vector2(to.rect.width * 0.5f, -to.rect.height * 0.5f);
            Apply(line, a, b, thickness);
        }

        public static void StretchBetween(RectTransform line, RectTransform from, Vector2 target)
        {
            var a = from.anchoredPosition + new Vector2(from.rect.width * 0.5f, -from.rect.height * 0.5f);
            Apply(line, a, target, 2f);
        }

        static void Apply(RectTransform line, Vector2 a, Vector2 b, float thickness)
        {
            var delta = b - a;
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(0f, 1f);
            line.pivot = new Vector2(0f, 0.5f);
            line.anchoredPosition = a;
            line.sizeDelta = new Vector2(delta.magnitude, thickness);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
