using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastWish.Presentation
{
    public sealed class TooltipPresenter
    {
        private readonly RectTransform root, viewport;
        private readonly Text title, body;
        public TooltipPresenter(UiFactory ui, RectTransform parent)
        {
            viewport = parent; root = ui.Rect(parent, "CardTooltip", 640, 360, 380, 420);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 30;
            ui.Panel(root, "Border", 190, 210, 380, 420, UiFactory.Gold);
            ui.Panel(root, "Background", 190, 210, 376, 416, new Color32(18, 20, 32, 255));
            title = ui.Label(root, "Title", "", 190, 45, 350, 70, 24, UiFactory.Gold);
            body = ui.Label(root, "Body", "", 190, 245, 340, 310, 18);
            body.alignment = TextAnchor.UpperLeft; body.resizeTextForBestFit = true; body.resizeTextMinSize = 12; body.resizeTextMaxSize = 18;
            Hide();
        }
        public void Show(string name, string description, Vector2 pointer)
        {
            title.text = name; body.text = description;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer, null, out var local);
            root.anchoredPosition = new Vector2(Mathf.Clamp(local.x + 206, -442, 442), Mathf.Clamp(local.y + 220, -142, 142));
            root.gameObject.SetActive(true);
        }
        public void Hide() { if (root != null) root.gameObject.SetActive(false); }
    }
    public sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private TooltipPresenter presenter;
        private string title, description;
        private Vector2 pointer;
        private bool hovering;
        public void Configure(TooltipPresenter tooltip) => presenter = tooltip;
        public void SetText(string name, string body)
        { title = name; description = body; if (hovering) presenter?.Show(title, description, pointer); }
        public void OnPointerEnter(PointerEventData data) { hovering = true; pointer = data.position; presenter?.Show(title, description, pointer); }
        public void OnPointerMove(PointerEventData data) { pointer = data.position; if (hovering) presenter?.Show(title, description, pointer); }
        public void OnPointerExit(PointerEventData data) { hovering = false; presenter?.Hide(); }
        private void OnDisable() { if (hovering) presenter?.Hide(); hovering = false; }
    }
}
