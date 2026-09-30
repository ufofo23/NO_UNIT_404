using System.Text;
using UnityEngine;
using UnityEngine.UI;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>
    /// Credits and third-party notices (GDD 16.15).
    ///
    /// Two jobs, and only one of them is optional. The names are courtesy; the licence
    /// notices are not - the game embeds Pretendard under SIL OFL 1.1, which requires the
    /// licence to travel with the font, and any Steamworks or engine notice added later
    /// belongs on the same screen. Shipping without somewhere to put them is a legal problem,
    /// not a polish problem, which is why this exists before the art pass rather than after.
    ///
    /// The notice bodies are deliberately not in the string table: a licence is not
    /// translatable, and Loc.T would only invite someone to translate one.
    /// </summary>
    public sealed class CreditsView : MonoBehaviour
    {
        /// <summary>The OFL text shipped alongside the font it covers.</summary>
        public const string FontLicenseResource = "NO404/Fonts/Pretendard-OFL";

        public System.Action OnClose;

        Text _body;

        public static CreditsView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("Credits", parent, 360);
            var view = canvas.gameObject.AddComponent<CreditsView>();
            view.Build((RectTransform)canvas.transform);
            view.gameObject.SetActive(false);
            return view;
        }

        void Build(RectTransform root)
        {
            var background = UiFactory.CreatePanel("Background", root, UiFactory.Background);
            UiFactory.Stretch(background.rectTransform, 0f, 0f);

            var title = UiFactory.CreateText("Title", root, Loc.T("ui.credits.title"), 34,
                                             TextAnchor.MiddleLeft);
            UiFactory.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(120f, -110f), new Vector2(900f, 46f));

            ScrollRect scroll;
            var content = UiFactory.CreateScrollView("Body", root, out scroll);
            UiFactory.SetAnchoredRect((RectTransform)scroll.transform,
                                      new Vector2(0f, 0f), new Vector2(1f, 1f),
                                      new Vector2(120f, 110f), new Vector2(-120f, -160f));

            _body = UiFactory.CreateText("Text", content, string.Empty, 16, TextAnchor.UpperLeft,
                                         UiFactory.TextMuted);
            UiFactory.SetHeight(_body.gameObject, 40f);

            var close = UiFactory.CreateButton("Close", root, Loc.T("ui.common.back"), 18,
                                               () => { var cb = OnClose; if (cb != null) cb(); });
            UiFactory.Pin(close.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(120f, 48f), new Vector2(200f, 44f));
        }

        public void SetOpen(bool open)
        {
            gameObject.SetActive(open);
            if (!open) return;

            _body.text = Compose();

            // uGUI will not size a Text to its own content, and this one is as long as the
            // licence it carries; without this the scroll view has nothing to scroll.
            UiFactory.SetHeight(_body.gameObject, _body.preferredHeight + 40f);
        }

        static string Compose()
        {
            var sb = new StringBuilder();

            sb.AppendLine(Loc.T("ui.credits.made_by"));
            sb.AppendLine();
            sb.AppendLine(Loc.T("ui.credits.built_with", Application.unityVersion));
            sb.AppendLine();
            sb.AppendLine(Loc.T("ui.credits.version", Application.version));
            sb.AppendLine();
            sb.AppendLine();

            sb.AppendLine(Loc.T("ui.credits.third_party"));
            sb.AppendLine();
            sb.AppendLine("── Pretendard (SIL Open Font License 1.1) ──");
            sb.AppendLine();
            sb.AppendLine(LicenseText(FontLicenseResource));

            return sb.ToString();
        }

        /// <summary>
        /// Reads a notice out of Resources. A missing licence file is reported in the credits
        /// themselves rather than swallowed: it is the one failure here that has to be visible,
        /// because the build is not distributable without it.
        /// </summary>
        static string LicenseText(string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            if (asset != null) return asset.text;

            Log.Error("Credits", "licence text missing at Resources/" + resource);
            return "[missing: Resources/" + resource + "]";
        }
    }
}
