using System;
using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.Combat
{
    /// <summary>
    /// ImageGen-mockup-driven, mobile-safe death respawn selector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinDeathRespawnPanel : MonoBehaviour
    {
        private const float FADE_DURATION = 0.3f;
        private const string REWARDED_ICON_RESOURCE =
            "FranklinDeathUI/respawn_rewarded";
        private const string HOSPITAL_ICON_RESOURCE =
            "FranklinDeathUI/respawn_hospital";
        private const string REWARDED_FRAME_RESOURCE =
            "FranklinDeathUI/frame_rewarded";
        private const string HOSPITAL_FRAME_RESOURCE =
            "FranklinDeathUI/frame_hospital";

        private static readonly Color CARD_COLOR = new Color32(20, 21, 24, 248);
        private static readonly Color RED = new Color32(201, 52, 45, 255);
        private static readonly Color AMBER = new Color32(242, 184, 75, 255);
        private static readonly Color SILVER = new Color32(222, 226, 229, 255);
        private static readonly Color MUTED = new Color32(158, 161, 165, 255);

        private CanvasGroup m_Group;
        private RectTransform m_Content;
        private Button m_ReviveHereButton;
        private Button m_HospitalButton;
        private Text m_HospitalSubtitle;
        private Text m_Status;
        private Sprite m_RewardedSprite;
        private Sprite m_HospitalSprite;
        private Sprite m_RewardedFrameSprite;
        private Sprite m_HospitalFrameSprite;
        private float m_ShownAt;
        private bool m_Visible;
        private bool m_Busy;
        private bool m_HospitalAvailable;

        public event Action EventReviveHere;
        public event Action EventHospital;

        public bool IsVisible => this.m_Visible && this.m_Group != null &&
                                 this.m_Group.alpha > 0.95f;

        public static FranklinDeathRespawnPanel Create(
            RectTransform parent,
            Font font,
            Color accent)
        {
            GameObject root = new GameObject(
                "Franklin Respawn Popup",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(FranklinDeathRespawnPanel)
            );
            root.transform.SetParent(parent, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Image dimmer = root.GetComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.34f);
            dimmer.raycastTarget = true;

            FranklinDeathRespawnPanel panel = root.GetComponent<
                FranklinDeathRespawnPanel
            >();
            panel.Build(rootRect, font, accent);
            panel.HideImmediate();
            return panel;
        }

        public void Show(bool hospitalAvailable)
        {
            this.m_HospitalAvailable = hospitalAvailable;
            this.m_HospitalSubtitle.text = hospitalAvailable
                ? "HỒI ĐẦY MÁU"
                : "KHÔNG TÌM THẤY MARKER";
            this.m_Visible = true;
            this.m_Busy = false;
            this.m_ShownAt = Time.unscaledTime;
            this.m_Group.alpha = 0f;
            this.m_Group.blocksRaycasts = true;
            this.m_Group.interactable = true;
            this.m_Content.localScale = Vector3.one * 0.93f;
            this.SetButtonsInteractable(true);
            this.SetStatus(
                hospitalAvailable
                    ? "CHỌN MỘT PHƯƠNG ÁN ĐỂ TIẾP TỤC"
                    : "MARKER BỆNH VIỆN CHƯA SẴN SÀNG",
                hospitalAvailable ? MUTED : AMBER
            );
        }

        public void SetBusy(bool busy, string message)
        {
            this.m_Busy = busy;
            this.SetButtonsInteractable(!busy);
            this.SetStatus(message, busy ? AMBER : MUTED);
        }

        public void SetError(string message)
        {
            this.m_Busy = false;
            this.SetButtonsInteractable(true);
            this.SetStatus(message, new Color32(255, 105, 94, 255));
        }

        private void Update()
        {
            if (!this.m_Visible || this.m_Group == null) return;

            float t = Mathf.Clamp01((Time.unscaledTime - this.m_ShownAt) / FADE_DURATION);
            float eased = t * t * (3f - 2f * t);
            this.m_Group.alpha = eased;
            this.m_Content.localScale = Vector3.one * Mathf.Lerp(0.93f, 1f, eased);
        }

        private void OnDestroy()
        {
            if (this.m_RewardedSprite != null) Destroy(this.m_RewardedSprite);
            if (this.m_HospitalSprite != null) Destroy(this.m_HospitalSprite);
            if (this.m_RewardedFrameSprite != null) Destroy(this.m_RewardedFrameSprite);
            if (this.m_HospitalFrameSprite != null) Destroy(this.m_HospitalFrameSprite);
        }

        private void Build(RectTransform root, Font font, Color accent)
        {
            this.m_Group = this.GetComponent<CanvasGroup>();
            this.m_Content = CreateRect("Respawn Selector", root);
            SetRect(this.m_Content, Vector2.zero, new Vector2(1560f, 760f));

            this.BuildHeader(font, accent);

            this.m_RewardedSprite = LoadRuntimeSprite(REWARDED_ICON_RESOURCE);
            this.m_HospitalSprite = LoadRuntimeSprite(HOSPITAL_ICON_RESOURCE);
            this.m_RewardedFrameSprite = LoadRuntimeSprite(REWARDED_FRAME_RESOURCE);
            this.m_HospitalFrameSprite = LoadRuntimeSprite(HOSPITAL_FRAME_RESOURCE);

            this.m_ReviveHereButton = this.CreateChoiceCard(
                "Revive Here (Rewarded)",
                font,
                new Vector2(-360f, -70f),
                RED,
                this.m_RewardedFrameSprite,
                this.m_RewardedSprite,
                "HỒI SINH TẠI ĐÂY",
                "REWARDED VIDEO",
                AMBER
            );
            this.m_ReviveHereButton.onClick.AddListener(
                () => this.EventReviveHere?.Invoke()
            );

            this.m_HospitalButton = this.CreateChoiceCard(
                "Respawn At Hospital",
                font,
                new Vector2(360f, -70f),
                SILVER,
                this.m_HospitalFrameSprite,
                this.m_HospitalSprite,
                "BỆNH VIỆN",
                "HỒI ĐẦY MÁU",
                AMBER
            );
            this.m_HospitalSubtitle = this.m_HospitalButton.transform
                .Find("Card Surface/Subtitle")
                ?.GetComponent<Text>();
            this.m_HospitalButton.onClick.AddListener(
                () => this.EventHospital?.Invoke()
            );

            this.BuildFooter(font, accent);
        }

        private void BuildHeader(Font font, Color accent)
        {
            Text wasted = CreateText(
                "WASTED Label",
                this.m_Content,
                font,
                "WASTED",
                47,
                FontStyle.Bold,
                RED
            );
            SetRect(wasted.rectTransform, new Vector2(0f, 308f), new Vector2(380f, 65f));
            AddOutline(wasted.gameObject, new Color(0f, 0f, 0f, 0.9f), 3f);

            CreateLine(
                "Wasted Line Left",
                this.m_Content,
                new Vector2(-290f, 308f),
                new Vector2(220f, 2f),
                new Color(accent.r, accent.g, accent.b, 0.7f)
            );
            CreateLine(
                "Wasted Line Right",
                this.m_Content,
                new Vector2(290f, 308f),
                new Vector2(220f, 2f),
                new Color(accent.r, accent.g, accent.b, 0.7f)
            );

            Text title = CreateText(
                "Header",
                this.m_Content,
                font,
                "CHỌN ĐIỂM HỒI SINH",
                67,
                FontStyle.Bold,
                Color.white
            );
            SetRect(title.rectTransform, new Vector2(0f, 222f), new Vector2(1160f, 92f));
            AddOutline(title.gameObject, new Color(0f, 0f, 0f, 0.92f), 4f);

            CreateLine(
                "Header Rule Left",
                this.m_Content,
                new Vector2(-375f, 158f),
                new Vector2(530f, 3f),
                new Color32(195, 199, 202, 220)
            );
            CreateLine(
                "Header Rule Right",
                this.m_Content,
                new Vector2(375f, 158f),
                new Vector2(530f, 3f),
                new Color(accent.r, accent.g, accent.b, 0.9f)
            );

            CreateLine(
                "Chevron Left",
                this.m_Content,
                new Vector2(-13f, 149f),
                new Vector2(28f, 4f),
                accent,
                -45f
            );
            CreateLine(
                "Chevron Right",
                this.m_Content,
                new Vector2(13f, 149f),
                new Vector2(28f, 4f),
                accent,
                45f
            );
        }

        private Button CreateChoiceCard(
            string name,
            Font font,
            Vector2 position,
            Color borderColor,
            Sprite frame,
            Sprite icon,
            string title,
            string subtitle,
            Color subtitleColor)
        {
            GameObject cardObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Shadow)
            );
            cardObject.transform.SetParent(this.m_Content, false);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            SetRect(cardRect, position, new Vector2(680f, 285f));

            Image frameImage = cardObject.GetComponent<Image>();
            frameImage.sprite = frame;
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = false;
            frameImage.color = frame != null ? Color.white : CARD_COLOR;
            frameImage.raycastTarget = true;

            Shadow glow = cardObject.GetComponent<Shadow>();
            glow.effectColor = new Color(
                borderColor.r,
                borderColor.g,
                borderColor.b,
                borderColor == RED ? 0.52f : 0.2f
            );
            glow.effectDistance = new Vector2(6f, -6f);

            Button button = cardObject.GetComponent<Button>();
            button.targetGraphic = frameImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.28f, 0.28f, 0.28f, 0.72f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            GameObject surfaceObject = new GameObject(
                "Card Surface",
                typeof(RectTransform)
            );
            surfaceObject.transform.SetParent(cardRect, false);
            RectTransform surfaceRect = surfaceObject.GetComponent<RectTransform>();
            Stretch(surfaceRect);
            surfaceRect.offsetMin = new Vector2(18f, 14f);
            surfaceRect.offsetMax = new Vector2(-18f, -14f);

            Image iconImage = CreateImage("Icon", surfaceRect, Color.white, false);
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.enabled = icon != null;
            SetRect(
                iconImage.rectTransform,
                new Vector2(-210f, 0f),
                new Vector2(205f, 205f)
            );

            if (icon == null)
            {
                Text fallback = CreateText(
                    "Icon Fallback",
                    surfaceRect,
                    font,
                    borderColor == RED ? "↻▶" : "✚",
                    72,
                    FontStyle.Bold,
                    borderColor
                );
                SetRect(
                    fallback.rectTransform,
                    new Vector2(-210f, 0f),
                    new Vector2(205f, 205f)
                );
            }

            Text titleText = CreateText(
                "Title",
                surfaceRect,
                font,
                title,
                35,
                FontStyle.Bold,
                Color.white
            );
            titleText.alignment = TextAnchor.MiddleLeft;
            SetRect(titleText.rectTransform, new Vector2(105f, 39f), new Vector2(380f, 70f));
            AddOutline(titleText.gameObject, new Color(0f, 0f, 0f, 0.9f), 2f);

            CreateLine(
                "Label Rule",
                surfaceRect,
                new Vector2(105f, -5f),
                new Vector2(375f, 2f),
                borderColor
            );

            Text subtitleText = CreateText(
                "Subtitle",
                surfaceRect,
                font,
                subtitle,
                25,
                FontStyle.Bold,
                subtitleColor
            );
            subtitleText.alignment = TextAnchor.MiddleLeft;
            SetRect(
                subtitleText.rectTransform,
                new Vector2(105f, -57f),
                new Vector2(380f, 58f)
            );

            CreateLine(
                "Corner Detail Top",
                surfaceRect,
                new Vector2(307f, 116f),
                new Vector2(46f, 3f),
                borderColor
            );
            CreateLine(
                "Corner Detail Bottom",
                surfaceRect,
                new Vector2(-307f, -116f),
                new Vector2(46f, 3f),
                borderColor
            );

            return button;
        }

        private void BuildFooter(Font font, Color accent)
        {
            CreateLine(
                "Footer Rule Left",
                this.m_Content,
                new Vector2(-410f, -265f),
                new Vector2(560f, 2f),
                new Color32(135, 138, 141, 170)
            );
            CreateLine(
                "Footer Rule Right",
                this.m_Content,
                new Vector2(410f, -265f),
                new Vector2(560f, 2f),
                new Color32(135, 138, 141, 170)
            );
            CreateLine(
                "Pulse Center",
                this.m_Content,
                new Vector2(0f, -265f),
                new Vector2(82f, 4f),
                accent
            );

            this.m_Status = CreateText(
                "Status",
                this.m_Content,
                font,
                string.Empty,
                23,
                FontStyle.Bold,
                MUTED
            );
            SetRect(this.m_Status.rectTransform, new Vector2(0f, -315f), new Vector2(1100f, 54f));

            Text leftArrow = CreateText(
                "Left Marker",
                this.m_Content,
                font,
                "‹",
                42,
                FontStyle.Bold,
                RED
            );
            SetRect(leftArrow.rectTransform, new Vector2(-555f, -315f), new Vector2(42f, 54f));
            Text rightArrow = CreateText(
                "Right Marker",
                this.m_Content,
                font,
                "›",
                42,
                FontStyle.Bold,
                RED
            );
            SetRect(rightArrow.rectTransform, new Vector2(555f, -315f), new Vector2(42f, 54f));
        }

        private void HideImmediate()
        {
            this.m_Visible = false;
            this.m_Group.alpha = 0f;
            this.m_Group.blocksRaycasts = false;
            this.m_Group.interactable = false;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            this.m_ReviveHereButton.interactable = interactable && !this.m_Busy;
            this.m_HospitalButton.interactable = interactable && !this.m_Busy &&
                                                 this.m_HospitalAvailable;
        }

        private void SetStatus(string message, Color color)
        {
            if (this.m_Status == null) return;
            this.m_Status.text = message;
            this.m_Status.color = color;
        }

        private static Sprite LoadRuntimeSprite(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect
            );
            sprite.name = texture.name + " (Runtime Sprite)";
            return sprite;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            return value.GetComponent<RectTransform>();
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int size,
            FontStyle style,
            Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = font != null
                ? font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color,
            bool raycastTarget)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static void CreateLine(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            float rotation = 0f)
        {
            Image line = CreateImage(name, parent, color, false);
            SetRect(line.rectTransform, position, size);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static void AddOutline(GameObject target, Color color, float distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

}
