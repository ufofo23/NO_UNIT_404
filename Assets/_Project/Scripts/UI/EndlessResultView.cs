using UnityEngine;
using UnityEngine.UI;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>
    /// What an endless run leaves behind.
    ///
    /// The run used to end with a single notification and a cut to the main menu, which is a
    /// poor deal for a mode whose entire proposition is a number: if the score is the reason
    /// to play again, the score has to be shown. Nights survived is the headline; the report
    /// and door accuracy underneath it are what the player will try to fix next time.
    /// </summary>
    public sealed class EndlessResultView : MonoBehaviour
    {
        public System.Action OnContinue;

        Text _headline;
        Text _body;
        Text _record;

        public static EndlessResultView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("EndlessResult", parent, 330);
            var view = canvas.gameObject.AddComponent<EndlessResultView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var background = UiFactory.CreatePanel("Background", root, UiFactory.Background);
            UiFactory.Stretch(background.rectTransform, 0f, 0f);

            var title = UiFactory.CreateText("Title", root, Loc.T("ui.endless.title"), 34, TextAnchor.UpperLeft);
            UiFactory.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(120f, -120f), new Vector2(1200f, 44f));

            _headline = UiFactory.CreateText("Headline", root, string.Empty, 46, TextAnchor.UpperLeft);
            UiFactory.Pin(_headline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(120f, -200f), new Vector2(1200f, 60f));

            _body = UiFactory.CreateText("Body", root, string.Empty, 19, TextAnchor.UpperLeft);
            UiFactory.Pin(_body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(120f, -290f), new Vector2(1200f, 300f));

            _record = UiFactory.CreateText("Record", root, string.Empty, 20, TextAnchor.UpperLeft,
                                           UiFactory.Accent);
            UiFactory.Pin(_record.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(120f, 180f), new Vector2(1200f, 30f));

            var button = UiFactory.CreateButton("Continue", root, Loc.T("ui.endless.back"), 20,
                                                () => { var cb = OnContinue; if (cb != null) cb(); });
            UiFactory.Pin(button.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(120f, 100f), new Vector2(320f, 52f));

            gameObject.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            gameObject.SetActive(open);
            if (!open) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Populate(int nightsSurvived, int bestNights, bool isRecord)
        {
            var state = ServiceHub.State;

            _headline.text = Loc.T("ui.endless.nights", nightsSurvived);

            int reports = state.ReportsCorrect + state.ReportsWrong;
            int visitors = state.VisitorsCorrect + state.VisitorsWrong;

            _body.text =
                Loc.T("ui.endless.reports", state.ReportsCorrect, reports, Percent(state.ReportsCorrect, reports)) + "\n" +
                Loc.T("ui.endless.visitors", state.VisitorsCorrect, visitors, Percent(state.VisitorsCorrect, visitors)) + "\n\n" +
                Loc.T("ui.stat.trust") + "  " + state.GetStat(StatIds.CommunityTrust) + "\n" +
                Loc.T("ui.stat.safety") + "  " + state.GetStat(StatIds.BuildingSafety) + "\n\n" +
                Loc.T("ui.endless.dismissed");

            _record.text = isRecord ? Loc.T("ui.endless.record_new")
                                    : Loc.T("ui.endless.record_best", bestNights);
        }

        /// <summary>Nothing attempted reads as zero, not as a divide by zero.</summary>
        static int Percent(int part, int total)
        {
            return total <= 0 ? 0 : Mathf.RoundToInt(part * 100f / total);
        }
    }
}
