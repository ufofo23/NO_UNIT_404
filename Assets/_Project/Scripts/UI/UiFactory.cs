using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>
    /// Placeholder widget kit.
    ///
    /// Everything is built in code with legacy uGUI and one embedded font so the project has
    /// no art dependency and needs no TMP Essentials import. The layout follows the GDD
    /// dimensions (16.2 / 16.6) so replacing these widgets with the real art later is a
    /// visual swap, not a restructure. No string here is user-visible - all text comes from
    /// the localization table.
    /// </summary>
    public static class UiFactory
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        // GDD 16.3 palette, kept intentionally flat and neutral for the greybox pass.
        public static readonly Color Background = new Color(0.086f, 0.094f, 0.106f, 1f);
        public static readonly Color Panel = new Color(0.137f, 0.149f, 0.161f, 1f);
        public static readonly Color PanelAlt = new Color(0.176f, 0.192f, 0.204f, 1f);
        public static readonly Color Line = new Color(0.267f, 0.286f, 0.298f, 1f);
        public static readonly Color TextPrimary = new Color(0.878f, 0.890f, 0.894f, 1f);
        public static readonly Color TextMuted = new Color(0.588f, 0.612f, 0.627f, 1f);
        public static readonly Color Accent = new Color(0.298f, 0.643f, 0.643f, 1f);
        public static readonly Color Warning = new Color(0.851f, 0.694f, 0.302f, 1f);
        public static readonly Color Danger = new Color(0.788f, 0.353f, 0.325f, 1f);
        public static readonly Color Ok = new Color(0.435f, 0.702f, 0.541f, 1f);

        /// <summary>
        /// Pretendard, the GDD 16.4 first choice for Korean, shipped inside the build under
        /// SIL OFL 1.1 (Resources/NO404/Fonts/Pretendard-OFL.txt).
        /// </summary>
        public const string FontResource = "NO404/Fonts/Pretendard-Regular";

        static Font _font;

        /// <summary>
        /// The UI font.
        ///
        /// This used to ask the OS for Malgun Gothic. That is a Windows system font: it is not
        /// ours to redistribute, it is absent on a machine without the Korean supplemental
        /// fonts installed, and when it is absent every Korean glyph in the game renders as a
        /// box. Shipping the face inside the build is the only version of this that is both
        /// licensed and certain.
        ///
        /// The OS lookup survives only as a fallback for a broken install, and the built-in
        /// legacy font behind it has no Hangul at all - if the game ever gets that far the
        /// text is already wrong, and it is better to be wrong and readable in English than
        /// to be a screen of boxes.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                _font = Resources.Load<Font>(FontResource);
                if (_font != null) return _font;

                Log.Error("UI", "embedded font missing at Resources/" + FontResource +
                                "; falling back to an OS face");

                try
                {
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR", "Arial Unicode MS", "Segoe UI", "Arial" },
                        16);
                }
                catch (Exception)
                {
                    _font = null;
                }

                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(parent, false);
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(string name, Transform parent, string content, int size,
                                      TextAnchor anchor = TextAnchor.UpperLeft, Color? color = null)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.text = content;
            text.alignment = anchor;
            text.color = color ?? TextPrimary;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, int size, Action onClick)
        {
            var image = CreatePanel(name, parent, PanelAlt);
            var button = image.gameObject.AddComponent<Button>();

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.06f;   // GDD 16.18: fast, no bouncy motion
            button.colors = colors;

            var text = CreateText("Label", image.transform, label, size, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8f, 2f);

            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public static InputField CreateInputField(string name, Transform parent, string placeholder, int size)
        {
            var image = CreatePanel(name, parent, Background);

            var text = CreateText("Text", image.transform, string.Empty, size, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 8f, 2f);
            text.supportRichText = false;

            var hint = CreateText("Placeholder", image.transform, placeholder, size, TextAnchor.MiddleLeft, TextMuted);
            Stretch(hint.rectTransform, 8f, 2f);

            var field = image.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        public static Slider CreateSlider(string name, Transform parent, float min, float max, float value,
                                          Action<float> onChanged)
        {
            var root = CreateRect(name, parent);
            var background = CreatePanel("Background", root, Background);
            Stretch(background.rectTransform, 0f, 0f);

            var fillArea = CreateRect("FillArea", root);
            Stretch(fillArea, 2f, 2f);
            var fill = CreatePanel("Fill", fillArea, Accent);
            Stretch(fill.rectTransform, 0f, 0f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = background;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.transition = Selectable.Transition.None;

            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        public static Toggle CreateToggle(string name, Transform parent, string label, bool value, Action<bool> onChanged)
        {
            var root = CreateRect(name, parent);

            var box = CreatePanel("Box", root, Background);
            box.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            box.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            box.rectTransform.pivot = new Vector2(0f, 0.5f);
            box.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            box.rectTransform.sizeDelta = new Vector2(22f, 22f);

            var check = CreatePanel("Check", box.transform, Accent);
            Stretch(check.rectTransform, 4f, 4f);

            var text = CreateText("Label", root, label, 18, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(30f, 0f);
            text.rectTransform.offsetMax = new Vector2(0f, 0f);

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = value;
            toggle.transition = Selectable.Transition.None;

            if (onChanged != null) toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        /// <summary>Vertical scroll view. Returns the content rect callers should fill.</summary>
        public static RectTransform CreateScrollView(string name, Transform parent, out ScrollRect scrollRect)
        {
            // RectMask2D, not Mask: a stencil Mask whose graphic is hidden switches the shader
            // to alpha clipping, so a near-transparent viewport image is discarded before it can
            // write the stencil and every row inside the list disappears. RectMask2D clips by
            // rectangle instead, needs no graphic of its own, and costs less.
            // The image stays purely as the ScrollRect's raycast target for the scroll wheel.
            var viewport = CreatePanel(name, parent, new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateRect("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.spacing = 4f;
            layout.padding = new RectOffset(6, 6, 6, 6);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = content;
            scrollRect.viewport = viewport.rectTransform;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            return content;
        }

        /// <summary>
        /// Removes every child of a rect. Children are unparented first because Destroy is
        /// deferred to the end of the frame - without this, a rebuilt list would show the
        /// old and new rows stacked for one frame.
        /// </summary>
        public static void ClearChildren(RectTransform rect)
        {
            if (rect == null) return;

            for (int i = rect.childCount - 1; i >= 0; i--)
            {
                var child = rect.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        // ---- layout helpers ------------------------------------------------

        public static void Stretch(RectTransform rect, float horizontalPadding, float verticalPadding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        }

        public static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
                                           Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>Pins a rect to a corner with an explicit pixel size.</summary>
        public static void Pin(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void AddVerticalLayout(RectTransform rect, float spacing, RectOffset padding = null)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
        }

        public static void AddHorizontalLayout(RectTransform rect, float spacing, RectOffset padding = null)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
        }

        public static LayoutElement SetHeight(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return element;
        }

        public static LayoutElement SetWidth(GameObject go, float width)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
            return element;
        }
    }
}
