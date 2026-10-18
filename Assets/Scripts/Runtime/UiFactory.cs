using UnityEngine;
using UnityEngine.UI;

namespace LastWish.Presentation
{
    public sealed class UiFactory
    {
        public static readonly Color Gold = new Color32(215, 180, 75, 255);
        public static readonly Color Ink = new Color32(220, 215, 200, 255);
        public Font Font { get; set; }
        public UiFactory(Font font) { Font = font; }
        public RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        public Image Panel(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            image.raycastTarget = false; return image;
        }
        public Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size = 24, Color? color = null)
        {
            var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = Font; text.fontSize = size; text.color = color ?? Ink;
            text.alignment = TextAnchor.MiddleCenter; text.text = value;
            text.raycastTarget = false; text.supportRichText = false;
            return text;
        }
        public Button Button(Transform parent, string name, string value, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
        {
            Panel(parent, name + "Border", x, y, width + 4, height + 4, Gold);
            var image = Panel(parent, name, x, y, width, height, new Color32(37, 37, 52, 255));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.6f, .7f, .8f); colors.pressedColor = new Color(.4f, .6f, .7f); button.colors = colors;
            button.onClick.AddListener(action);
            Label(button.transform, name + "Text", value, width / 2, height / 2, width - 8, height - 4);
            return button;
        }
        public RawImage Art(Transform parent, string name, Texture texture, float x, float y, float width, float height)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            return image;
        }
    }
}
