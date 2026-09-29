using UnityEngine;
using UnityEngine.UI;

namespace Rally.UI
{
    /// <summary>Tiny helper for building uGUI hierarchies in code with a consistent style.</summary>
    public static class UIFactory
    {
        public static readonly Color Accent = new Color(1f, 0.42f, 0.1f, 1f);
        public static readonly Color PanelDark = new Color(0.04f, 0.05f, 0.06f, 0.72f);
        public static readonly Color PanelLight = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color TextMain = new Color(0.97f, 0.97f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(0.75f, 0.77f, 0.78f, 1f);
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static Font font;
        private static Sprite circle;

        /// <summary>Anti-aliased white disc, generated once (for round on-screen buttons and sticks).</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UICircle" };
                var pixels = new Color32[size * size];
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return circle;
            }
        }

        public static Font Font
        {
            get
            {
                if (font != null) return font;
#if UNITY_WEBGL && !UNITY_EDITOR
                // Browsers expose no OS fonts to the player: go straight to the built-in font.
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
#else
                string[] preferred = { "Avenir Next Condensed", "Avenir Next", "Helvetica Neue", "Segoe UI", "Arial" };
                foreach (var name in preferred)
                {
                    foreach (var installed in Font.GetOSInstalledFontNames())
                    {
                        if (installed != name) continue;
                        font = Font.CreateDynamicFontFromOSFont(name, 64);
                        return font;
                    }
                }
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
#endif
            }
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Anchored rect; the pivot defaults to the anchor (so position is an offset from that corner).</summary>
        public static RectTransform Anchored(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null) =>
            Rect(name, parent, anchor, anchor, pivot ?? anchor, position, size);

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, Vector2? pivot = null)
        {
            var rt = Anchored(name, parent, anchor, position, size, pivot);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, TextAnchor alignment, Color color,
            FontStyle style = FontStyle.Bold)
        {
            var rt = Stretch(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text Label(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string text,
            int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Bold, Vector2? pivot = null)
        {
            var rt = Anchored(name, parent, anchor, position, size, pivot);
            var t = Label("Text", rt, text, fontSize, alignment, color, style);
            return t;
        }

        public static Outline AddShadow(Graphic graphic, float distance = 2f, float alpha = 0.55f)
        {
            var shadow = graphic.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = new Vector2(distance, -distance);
            return shadow;
        }

        public static Button Button(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string label,
            UnityEngine.Events.UnityAction onClick)
        {
            var bg = Panel(name, parent, anchor, position, size, Color.white);
            bg.raycastTarget = true;
            var button = bg.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.13f, 0.15f, 0.85f);
            colors.highlightedColor = Accent;
            colors.selectedColor = Accent;
            colors.pressedColor = new Color(1f, 0.6f, 0.3f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);
            Label("Label", bg.transform, label, 30, TextAnchor.MiddleCenter, TextMain);
            return button;
        }
    }
}
