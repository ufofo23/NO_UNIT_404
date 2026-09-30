using UnityEngine;
using UnityEngine.UI;
using NO404.Core;
using NO404.Endings;

namespace NO404.UI
{
    /// <summary>
    /// The ending screen (GDD 10). Deliberately quiet: title, epilogue, one way out.
    /// Placeholder styling; the art pass replaces the panel, not the flow.
    /// </summary>
    public sealed class EndingView : MonoBehaviour
    {
        public System.Action OnContinue;

        Text _title;
        Text _body;
        Text _footer;

        public static EndingView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("Ending", parent, 360);
            var view = canvas.gameObject.AddComponent<EndingView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var background = UiFactory.CreatePanel("Background", root, new Color(0.04f, 0.04f, 0.05f, 1f));
            UiFactory.Stretch(background.rectTransform, 0f, 0f);

            _title = UiFactory.CreateText("Title", root, string.Empty, 44, TextAnchor.UpperLeft);
            UiFactory.Pin(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(140f, -160f), new Vector2(1300f, 60f));

            _body = UiFactory.CreateText("Body", root, string.Empty, 20, TextAnchor.UpperLeft,
                                         UiFactory.TextPrimary);
            UiFactory.Pin(_body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(140f, -250f), new Vector2(1300f, 520f));

            _footer = UiFactory.CreateText("Footer", root, string.Empty, 16, TextAnchor.UpperLeft,
                                           UiFactory.TextMuted);
            UiFactory.Pin(_footer.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(140f, 180f), new Vector2(1300f, 30f));

            var button = UiFactory.CreateButton("Continue", root, Loc.T("ui.ending.continue"), 20,
                                                () => { var cb = OnContinue; if (cb != null) cb(); });
            UiFactory.Pin(button.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(140f, 100f), new Vector2(340f, 52f));

            gameObject.SetActive(false);
        }

        public void Show(EndingDefinition ending)
        {
            gameObject.SetActive(true);

            if (ending == null)
            {
                _title.text = Loc.T("ui.ending.unknown");
                _body.text = string.Empty;
                _footer.text = string.Empty;
                return;
            }

            _title.text = Loc.T(ending.titleKey);
            _body.text = Loc.T(ending.summaryKey) + "\n\n" + Loc.T(ending.bodyKey);
            _footer.text = Loc.T("ui.ending.gallery_progress",
                                 ServiceHub.Endings.UnlockedCount, EndingIds.All.Length);
        }

        public void SetOpen(bool open) { gameObject.SetActive(open); }
    }

    /// <summary>Ending gallery reachable from the main menu (GDD 6.5 / 16.15).</summary>
    public sealed class EndingGalleryView : MonoBehaviour
    {
        public System.Action OnClose;

        RectTransform _list;

        public static EndingGalleryView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("EndingGallery", parent, 355);
            var view = canvas.gameObject.AddComponent<EndingGalleryView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var background = UiFactory.CreatePanel("Background", root, UiFactory.Background);
            UiFactory.Stretch(background.rectTransform, 0f, 0f);

            var title = UiFactory.CreateText("Title", root, Loc.T("ui.gallery.title"), 34, TextAnchor.UpperLeft);
            UiFactory.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(140f, -120f), new Vector2(1000f, 44f));

            ScrollRect scroll;
            _list = UiFactory.CreateScrollView("Endings", root, out scroll);
            UiFactory.SetAnchoredRect((RectTransform)scroll.transform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(140f, 160f), new Vector2(-140f, -180f));

            var close = UiFactory.CreateButton("Close", root, Loc.T("ui.gallery.close"), 20,
                                               () => { var cb = OnClose; if (cb != null) cb(); });
            UiFactory.Pin(close.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(140f, 80f), new Vector2(300f, 48f));

            gameObject.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            gameObject.SetActive(open);
            if (open) Rebuild();
        }

        void Rebuild()
        {
            UiFactory.ClearChildren(_list);

            var endings = ServiceHub.Endings.AllOrdered;
            for (int i = 0; i < endings.Count; i++)
            {
                var ending = endings[i];
                bool unlocked = ServiceHub.Endings.IsUnlocked(ending.endingId);

                var card = UiFactory.CreatePanel("Ending_" + ending.endingId, _list,
                                                 unlocked ? UiFactory.Panel : UiFactory.Background);
                UiFactory.SetHeight(card.gameObject, 96f);

                var heading = UiFactory.CreateText("Title", card.transform,
                    unlocked ? Loc.T(ending.titleKey) : Loc.T("ui.gallery.locked_entry"),
                    22, TextAnchor.UpperLeft, unlocked ? UiFactory.TextPrimary : UiFactory.TextMuted);
                UiFactory.SetAnchoredRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                          new Vector2(16f, -36f), new Vector2(-16f, -8f));

                var body = UiFactory.CreateText("Body", card.transform,
                    unlocked ? Loc.T(ending.summaryKey) : Loc.T(ending.lockedHintKey),
                    16, TextAnchor.UpperLeft, UiFactory.TextMuted);
                UiFactory.SetAnchoredRect(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                          new Vector2(16f, 10f), new Vector2(-16f, -40f));
            }
        }
    }
}
