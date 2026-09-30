using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NO404.Anomalies;
using NO404.Core;

namespace NO404.UI
{
    /// <summary>
    /// The panel one of the A01..A05 machines puts up while the caretaker is using it
    /// (v2.1 spec 23).
    ///
    /// Deliberately not the dialogue view. A conversation caps at four choices because a
    /// person offers four things to say (GDD 16.9); a machine has a list, and spec 23 wants
    /// that list to be visibly a list - seven keywords on a POS, six parcels in a locker, five
    /// drawers in a vending machine. Reusing the dialogue graph would have meant paging a
    /// menu that is not a conversation and cannot be interrupted, timed out, or held.
    ///
    /// What it does borrow is the shape: a body, a column of buttons, and no state of its own.
    /// Every question about what is on the menu is answered by AnomalyToolService, so a menu
    /// that changes underneath the panel - a token spent, a tool taken out - is one Refresh
    /// away from being right, including after a load.
    /// </summary>
    public sealed class AnomalyToolView : MonoBehaviour
    {
        Text _title;
        Text _body;
        Text _result;
        RectTransform _options;

        string _builtForTool;
        string _builtForMenu;
        string _builtForResult;
        int _builtOptionCount;

        readonly List<AnomalyToolOptionDefinition> _visible = new List<AnomalyToolOptionDefinition>(8);

        public bool IsOpen
        {
            get
            {
                var tools = ServiceHub.AnomalyTools;
                return tools != null && tools.SessionOpen;
            }
        }

        public static AnomalyToolView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("AnomalyTool", parent, 240);
            var view = canvas.gameObject.AddComponent<AnomalyToolView>();
            view.Build((RectTransform)canvas.transform);
            return view;
        }

        void Build(RectTransform root)
        {
            var panel = UiFactory.CreatePanel("Panel", root, new Color(0.05f, 0.05f, 0.06f, 0.94f));
            UiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.28f, 0.5f), new Vector2(0.72f, 0.5f),
                                      new Vector2(0f, -260f), new Vector2(0f, 260f));

            _title = UiFactory.CreateText("Title", panel.transform, string.Empty, 19, TextAnchor.UpperLeft,
                                          UiFactory.Accent);
            UiFactory.SetAnchoredRect(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(24f, -46f), new Vector2(-24f, -16f));

            _body = UiFactory.CreateText("Body", panel.transform, string.Empty, 16, TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(_body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(24f, -96f), new Vector2(-24f, -50f));

            // What the machine printed last. Monospaced in spirit if not in font: spec 23 A01
            // lays a receipt out as a receipt, and the alignment is most of what sells it.
            _result = UiFactory.CreateText("Result", panel.transform, string.Empty, 15, TextAnchor.UpperLeft,
                                           UiFactory.Warning);
            UiFactory.SetAnchoredRect(_result.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      new Vector2(24f, -260f), new Vector2(-24f, -100f));

            _options = UiFactory.CreateRect("Options", panel.transform);
            UiFactory.SetAnchoredRect(_options, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(24f, 16f), new Vector2(-24f, 250f));
            UiFactory.AddVerticalLayout(_options, 4f);

            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            var tools = ServiceHub.AnomalyTools;
            if (tools == null || !tools.SessionOpen)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                _builtForTool = null;
                _builtForMenu = null;
                _builtForResult = null;
                return;
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            var definition = tools.OpenTool.Definition;
            var menu = tools.OpenMenu;
            if (menu == null) return;

            tools.CollectVisibleOptions(_visible);

            // Rebuilding only when the screen actually changed keeps the buttons from being
            // destroyed and recreated under the cursor on the frame a click lands. The option
            // count is part of the identity because a menu can lose a line without changing
            // menu - a token spent, a tool taken out.
            bool changed = _builtForTool != definition.toolId
                        || _builtForMenu != menu.menuId
                        || _builtForResult != tools.LastResultKey
                        || _builtOptionCount != _visible.Count;
            if (!changed) return;

            _builtForTool = definition.toolId;
            _builtForMenu = menu.menuId;
            _builtForResult = tools.LastResultKey;
            _builtOptionCount = _visible.Count;

            _title.text = Loc.T(definition.nameKey) + "  -  " + Loc.T(menu.titleKey);
            _body.text = string.IsNullOrEmpty(menu.bodyKey) ? string.Empty : Loc.T(menu.bodyKey);
            _result.text = string.IsNullOrEmpty(tools.LastResultKey)
                ? string.Empty
                : Loc.T(tools.LastResultKey);

            RebuildOptions();
        }

        void RebuildOptions()
        {
            UiFactory.ClearChildren(_options);

            for (int i = 0; i < _visible.Count; i++)
            {
                var option = _visible[i];
                var optionId = option.optionId;
                var button = UiFactory.CreateButton("Option_" + optionId, _options,
                    Loc.T(option.labelKey), 16, () => Choose(optionId));
                UiFactory.SetHeight(button.gameObject, 28f);
            }

            // A machine the caretaker can never step away from would be a trap rather than a
            // choice, and spec 23 has no machine that holds anyone at its own screen.
            var close = UiFactory.CreateButton("Close", _options, Loc.T("tool.common.step_back"), 16,
                                               () => ServiceHub.AnomalyTools.Close());
            UiFactory.SetHeight(close.gameObject, 28f);
        }

        static void Choose(string optionId)
        {
            var tools = ServiceHub.AnomalyTools;
            if (tools != null) tools.Choose(optionId);
        }

        public void Close()
        {
            var tools = ServiceHub.AnomalyTools;
            if (tools != null) tools.Close();
        }
    }
}
