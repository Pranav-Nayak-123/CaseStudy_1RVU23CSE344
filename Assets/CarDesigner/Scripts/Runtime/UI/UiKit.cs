using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CarDesigner.UI
{
    /// <summary>Studio palette: graphite surfaces, bone type, a single amber accent.</summary>
    public static class Palette
    {
        public static readonly Color Panel = Hex("16171AEE");
        public static readonly Color Raised = Hex("222327");
        public static readonly Color RaisedHover = Hex("2D2E33");
        public static readonly Color Line = Hex("34353A");
        public static readonly Color Text = Hex("ECE6DA");
        public static readonly Color Muted = Hex("8E8A82");
        public static readonly Color Amber = Hex("E3A23B");
        public static readonly Color AmberPressed = Hex("B9822C");
        public static readonly Color AmberShadow = Hex("3B2C16");
        public static readonly Color Ink = Hex("121214");
        public static readonly Color Live = Hex("C8553D");

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }
    }

    /// <summary>Small factory for code-built uGUI so the layout is reproducible without hand-authored prefabs.</summary>
    public static class UiKit
    {
        const int UiLayer = 5;
        static Font font;

        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Anchor(this RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public static RectTransform Fill(this RectTransform rect) =>
            rect.Anchor(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static Image Surface(string name, Transform parent, Color color)
        {
            Image image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            Text label = Rect("Label", parent).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button Button(Transform parent, string text, UnityAction onClick, int fontSize = 20)
        {
            Image background = Surface("Button", parent, Color.white);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);
            Text label = Label(background.transform, text, fontSize, Palette.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.Fill();
            SetActive(button, false);
            return button;
        }

        /// <summary>Toggles the amber "engaged" state used for selected chips, axes, modes and swatches.</summary>
        public static void SetActive(Button button, bool active)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = active ? Palette.Amber : Palette.Raised;
            colors.highlightedColor = active ? Palette.Amber : Palette.RaisedHover;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = active ? Palette.AmberPressed : Palette.AmberShadow;
            // Disabled panels are already dimmed by their CanvasGroup; keep the engaged state readable.
            colors.disabledColor = colors.normalColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = active ? Palette.Ink : Palette.Text;
        }

        public static LayoutElement Size(Component target, float width = -1f, float height = -1f, float flexibleWidth = -1f)
        {
            if (!target.TryGetComponent(out LayoutElement element)) element = target.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleWidth = flexibleWidth;
            return element;
        }

        public static VerticalLayoutGroup Column(Component target, float spacing, RectOffset padding)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup Row(Component target, float spacing, RectOffset padding)
        {
            var layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return layout;
        }

        public static Image Divider(Transform parent)
        {
            Image line = Surface("Divider", parent, Palette.Line);
            line.raycastTarget = false;
            Size(line, height: 1f);
            return line;
        }

        public static Slider Slider(Transform parent, float min, float max, bool wholeNumbers, Action<float> onChanged)
        {
            RectTransform root = Rect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();

            Image track = Surface("Track", root, Palette.Line);
            track.rectTransform.Anchor(new Vector2(0f, 0.42f), new Vector2(1f, 0.58f), Vector2.zero, Vector2.zero);

            RectTransform fillArea = Rect("Fill Area", root).Anchor(new Vector2(0f, 0.42f), new Vector2(1f, 0.58f), Vector2.zero, Vector2.zero);
            Image fill = Surface("Fill", fillArea, Palette.Amber);
            fill.raycastTarget = false;
            fill.rectTransform.sizeDelta = Vector2.zero;

            RectTransform handleArea = Rect("Handle Area", root).Anchor(Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            Image handle = Surface("Handle", handleArea, Palette.Text);
            handle.rectTransform.sizeDelta = new Vector2(24f, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;

            ColorBlock colors = slider.colors;
            colors.normalColor = Palette.Text;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Palette.Amber;
            colors.selectedColor = Palette.Text;
            slider.colors = colors;

            slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }
    }
}
