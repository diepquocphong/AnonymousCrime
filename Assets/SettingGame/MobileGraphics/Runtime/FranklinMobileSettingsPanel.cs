using System;
using FranklinGame.Rendering;
using FranklinGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FranklinGame.Settings
{
    /// <summary>
    /// Lightweight runtime-built Settings panel. It reuses the existing player
    /// Canvas and settings-button event, so it adds no extra Canvas or camera.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(650)]
    [AddComponentMenu("Franklin Game/Settings/Mobile Settings Panel")]
    public sealed class FranklinMobileSettingsPanel : MonoBehaviour
    {
        private static readonly Color BackdropColor = new(0.015f, 0.025f, 0.03f, 0.74f);
        private static readonly Color PanelBorderColor = new(0.12f, 0.25f, 0.29f, 1f);
        private static readonly Color PanelColor = new(0.025f, 0.055f, 0.07f, 1f);
        private static readonly Color HeaderColor = new(0.035f, 0.085f, 0.105f, 1f);
        private static readonly Color CardColor = new(0.055f, 0.105f, 0.125f, 1f);
        private static readonly Color CyanColor = new(0.12f, 0.82f, 0.84f, 1f);
        private static readonly Color GreenColor = new(0.28f, 0.82f, 0.52f, 1f);
        private static readonly Color AmberColor = new(0.96f, 0.67f, 0.18f, 1f);
        private static readonly Color TextColor = new(0.96f, 0.98f, 0.97f, 1f);
        private static readonly Color MutedTextColor = new(0.58f, 0.69f, 0.71f, 1f);
        private static readonly Color OffColor = new(0.12f, 0.18f, 0.2f, 1f);
        private static readonly Color DarkTextColor = new(0.015f, 0.055f, 0.065f, 1f);

        private static readonly int[] QualityValues =
        {
            FranklinMobileGraphicsSettings.QualityAuto,
            FranklinMobileGraphicsSettings.QualityLow,
            FranklinMobileGraphicsSettings.QualityBalanced,
            FranklinMobileGraphicsSettings.QualityHigh
        };

        private static readonly int[] AntiAliasingValues =
        {
            FranklinMobileGraphicsSettings.AntiAliasingOff,
            FranklinMobileGraphicsSettings.AntiAliasingFxaa,
            FranklinMobileGraphicsSettings.AntiAliasingSmaa
        };

        [Header("Visual Assets")]
        [SerializeField] private Font m_Font;
        [SerializeField] private Sprite m_GearSprite;
        [SerializeField] private Sprite m_SolidSprite;
        private Sprite m_RoundedSprite;

        private FranklinMobileHud m_Hud;
        private GameObject m_Root;
        private Text m_FbsStateText;
        private Text m_SsaoStateText;
        private Text m_SsaoAvailabilityText;
        private Image m_FbsTrack;
        private Image m_SsaoTrack;
        private Text m_FbsKnob;
        private Text m_SsaoKnob;
        private Button[] m_QualityButtons;
        private Text[] m_QualityLabels;
        private Text m_QualityStatusText;
        private Button[] m_AntiAliasingButtons;
        private Text[] m_AntiAliasingLabels;
        private Image m_DepthOfFieldButtonImage;
        private Text m_DepthOfFieldButtonLabel;
        private bool m_IsOpen;

        public bool IsOpen => this.m_IsOpen;

        private void Awake()
        {
            this.ResolveAssets();
            this.BindHud();
            this.BuildInterface();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            SceneManager.sceneLoaded += this.OnSceneLoaded;
            this.BindHud();
            if (this.m_Root == null)
            {
                // Enter Play Mode can run with scene reload disabled in this
                // project, so build lazily as well as from Awake.
                this.ResolveAssets();
                this.BuildInterface();
            }
            FranklinBlobShadow.GlobalEnabledChanged += this.OnFbsChanged;
            FranklinMobileGraphicsSettings.SsaoEnabledChanged += this.OnSsaoChanged;
            FranklinMobileGraphicsSettings.QualityLevelChanged += this.OnQualityChanged;
            FranklinMobileGraphicsSettings.AntiAliasingModeChanged +=
                this.OnAntiAliasingChanged;
            FranklinMobileGraphicsSettings.DepthOfFieldModeChanged +=
                this.OnDepthOfFieldChanged;
        }

        private void Start()
        {
            this.BindHud();
            if (this.m_Root == null) this.BuildInterface();
            this.RefreshAll();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            this.UnbindHud();
            FranklinBlobShadow.GlobalEnabledChanged -= this.OnFbsChanged;
            FranklinMobileGraphicsSettings.SsaoEnabledChanged -= this.OnSsaoChanged;
            FranklinMobileGraphicsSettings.QualityLevelChanged -= this.OnQualityChanged;
            FranklinMobileGraphicsSettings.AntiAliasingModeChanged -=
                this.OnAntiAliasingChanged;
            FranklinMobileGraphicsSettings.DepthOfFieldModeChanged -=
                this.OnDepthOfFieldChanged;
            if (this.m_IsOpen) this.Close();
        }

        private void Update()
        {
            if (!this.m_IsOpen) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                this.Close();
            }
        }

        public void Toggle()
        {
            if (this.m_IsOpen) this.Close();
            else this.Open();
        }

        public void Open()
        {
            if (this.m_IsOpen) return;
            if (this.m_Root == null)
            {
                this.ResolveAssets();
                this.BuildInterface();
            }
            if (this.m_Root == null) return;
            this.m_IsOpen = true;
            this.RefreshAll();
            this.m_Root.SetActive(true);
            this.m_Root.transform.SetAsLastSibling();
            FranklinMobileHud.AcquireControlsSuppression(this);
        }

        public void Close()
        {
            if (!this.m_IsOpen) return;
            this.m_IsOpen = false;
            if (this.m_Root != null) this.m_Root.SetActive(false);
            FranklinMobileHud.ReleaseControlsSuppression(this);
        }

        private void ResolveAssets()
        {
            if (this.m_Font == null)
            {
                this.m_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (this.m_GearSprite == null)
            {
                this.m_GearSprite = Resources.Load<Sprite>(
                    "FranklinPlayerHud/settings-button"
                );
            }

            if (this.m_SolidSprite == null)
            {
                this.m_SolidSprite = Resources.Load<Sprite>(
                    "FranklinPlayerHud/ui-solid"
                );
            }

            // Unity 6 no longer ships UI/Skin/UISprite.psd. Leave the optional
            // rounded sprite null and use the installed solid HUD sprite instead.
        }

        private void BuildInterface()
        {
            if (this.m_Root != null) return;

            RectTransform canvasRect = ResolveTargetCanvas();
            if (canvasRect == null) return;
            this.m_Root = new GameObject(
                "Franklin Mobile Settings Screen",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            RectTransform root = this.m_Root.GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            Stretch(root);

            Image backdrop = this.m_Root.GetComponent<Image>();
            backdrop.sprite = this.m_SolidSprite;
            backdrop.color = BackdropColor;
            backdrop.raycastTarget = true;
            Button backdropButton = this.m_Root.GetComponent<Button>();
            ConfigureButton(backdropButton, backdrop, BackdropColor);
            backdropButton.onClick.AddListener(this.Close);

            RectTransform panelShadow = this.CreateImage(
                root,
                "Panel Shadow",
                new Vector2(12f, -16f),
                new Vector2(1138f, 738f),
                new Color(0f, 0f, 0f, 0.62f),
                this.m_RoundedSprite
            ).rectTransform;
            panelShadow.SetAsLastSibling();

            Image border = this.CreateImage(
                root,
                "Settings Panel Border",
                Vector2.zero,
                new Vector2(1120f, 720f),
                PanelBorderColor,
                this.m_RoundedSprite
            );
            border.raycastTarget = true;
            // Consume taps inside the panel so only the dark backdrop closes it.
            Button panelInputBlocker = border.gameObject.AddComponent<Button>();
            panelInputBlocker.targetGraphic = border;
            panelInputBlocker.transition = Selectable.Transition.None;
            panelInputBlocker.navigation = new Navigation
            {
                mode = Navigation.Mode.None
            };

            RectTransform panel = this.CreateImage(
                border.rectTransform,
                "Settings Panel",
                Vector2.zero,
                new Vector2(1108f, 708f),
                PanelColor,
                this.m_RoundedSprite
            ).rectTransform;

            this.CreateImage(
                panel,
                "Top Accent",
                new Vector2(0f, 351f),
                new Vector2(1108f, 6f),
                CyanColor,
                this.m_SolidSprite
            );

            this.CreateImage(
                panel,
                "Header",
                new Vector2(0f, 289f),
                new Vector2(1108f, 118f),
                HeaderColor,
                this.m_SolidSprite
            );

            Image gear = this.CreateImage(
                panel,
                "Gear Medallion",
                new Vector2(-477f, 290f),
                new Vector2(82f, 82f),
                Color.white,
                this.m_GearSprite
            );
            gear.preserveAspect = true;

            this.CreateLabel(
                panel,
                "Title",
                "CÀI ĐẶT",
                new Vector2(-320f, 309f),
                new Vector2(270f, 48f),
                38,
                TextColor,
                TextAnchor.MiddleLeft,
                FontStyle.Bold
            );
            this.CreateLabel(
                panel,
                "Subtitle",
                "ĐỒ HỌA  •  HIỆU NĂNG",
                new Vector2(-293f, 270f),
                new Vector2(330f, 32f),
                18,
                MutedTextColor,
                TextAnchor.MiddleLeft,
                FontStyle.Bold
            );

            Button closeButton = this.CreateTextButton(
                panel,
                "Close Button",
                "×",
                new Vector2(497f, 290f),
                new Vector2(68f, 68f),
                OffColor,
                AmberColor,
                46,
                this.Close
            );
            closeButton.image.raycastTarget = true;

            this.BuildQualityRow(panel);
            this.BuildToggleRow(
                panel,
                "FBS Card",
                "BÓNG NHÂN VẬT",
                "FBS • Bóng mềm, chi phí thấp",
                new Vector2(-258f, -126f),
                GreenColor,
                this.OnFbsButton,
                out this.m_FbsTrack,
                out this.m_FbsStateText,
                out this.m_FbsKnob
            );
            this.CreateLabel(
                panel,
                "FBS Performance",
                "NHẸ  •  ƯU TIÊN MOBILE",
                new Vector2(-357f, -177f),
                new Vector2(190f, 24f),
                14,
                GreenColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold
            );
            this.BuildToggleRow(
                panel,
                "SSAO Card",
                "ĐỔ BÓNG MÔI TRƯỜNG",
                "SSAO • Tăng chiều sâu khung cảnh",
                new Vector2(258f, -126f),
                CyanColor,
                this.OnSsaoButton,
                out this.m_SsaoTrack,
                out this.m_SsaoStateText,
                out this.m_SsaoKnob
            );

            this.m_SsaoAvailabilityText = this.CreateLabel(
                panel,
                "SSAO Availability",
                string.Empty,
                new Vector2(158f, -177f),
                new Vector2(200f, 24f),
                14,
                MutedTextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold
            );

            this.CreateImage(
                panel,
                "Footer Divider",
                new Vector2(0f, -248f),
                new Vector2(1016f, 2f),
                new Color(0.16f, 0.31f, 0.34f, 1f),
                this.m_SolidSprite
            );
            this.CreateLabel(
                panel,
                "Autosave Hint",
                "✓  THAY ĐỔI ĐƯỢC LƯU TỰ ĐỘNG",
                new Vector2(-322f, -299f),
                new Vector2(380f, 40f),
                17,
                MutedTextColor,
                TextAnchor.MiddleLeft,
                FontStyle.Bold
            );
            Button doneButton = this.CreateTextButton(
                panel,
                "Done Button",
                "HOÀN TẤT",
                new Vector2(382f, -299f),
                new Vector2(270f, 72f),
                CyanColor,
                AmberColor,
                25,
                this.ApplyAndClose
            );
            Text doneLabel = doneButton.GetComponentInChildren<Text>();
            if (doneLabel != null) doneLabel.color = DarkTextColor;

            this.m_Root.SetActive(false);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.BindHud();
            if (this.m_Root == null) this.BuildInterface();
        }

        private void BindHud()
        {
            FranklinMobileHud nextHud =
                FindFirstObjectByType<FranklinMobileHud>();
            if (this.m_Hud == nextHud) return;

            this.UnbindHud();
            this.m_Hud = nextHud;
            if (this.m_Hud != null)
            {
                this.m_Hud.EventSettingsRequested += this.Toggle;
            }
        }

        private void UnbindHud()
        {
            if (this.m_Hud != null)
            {
                this.m_Hud.EventSettingsRequested -= this.Toggle;
            }
            this.m_Hud = null;
        }

        private static RectTransform ResolveTargetCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
            RectTransform fallback = null;
            int fallbackOrder = int.MinValue;
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled ||
                    canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                RectTransform rect = canvas.transform as RectTransform;
                if (rect == null) continue;
                if (canvas.gameObject.name == "CanvasPlayerControl") return rect;
                if (fallback == null || canvas.sortingOrder > fallbackOrder)
                {
                    fallback = rect;
                    fallbackOrder = canvas.sortingOrder;
                }
            }

            return fallback;
        }

        private void BuildQualityRow(RectTransform panel)
        {
            RectTransform row = this.CreateImage(
                panel,
                "Quality Card",
                new Vector2(0f, 93f),
                new Vector2(1016f, 228f),
                CardColor,
                this.m_RoundedSprite
            ).rectTransform;
            this.CreateImage(
                row,
                "Accent",
                new Vector2(-504f, 0f),
                new Vector2(8f, 196f),
                AmberColor,
                this.m_SolidSprite
            );
            this.CreateLabel(
                row,
                "Quality Label",
                "CHẤT LƯỢNG",
                new Vector2(-377f, 56f),
                new Vector2(230f, 45f),
                27,
                TextColor,
                TextAnchor.MiddleLeft,
                FontStyle.Bold
            );
            this.CreateLabel(
                row,
                "Quality Subtitle",
                "Độ phân giải và FPS",
                new Vector2(-350f, 17f),
                new Vector2(285f, 34f),
                17,
                MutedTextColor,
                TextAnchor.MiddleLeft,
                FontStyle.Normal
            );

            string[] labels = { "AUTO", "THẤP", "VỪA", "CAO" };
            this.m_QualityButtons = new Button[labels.Length];
            this.m_QualityLabels = new Text[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int level = QualityValues[i];
                Button button = this.CreateTextButton(
                    row,
                    $"Quality {labels[i]}",
                    labels[i],
                    new Vector2(-145f + i * 156f, 49f),
                    new Vector2(144f, 66f),
                    OffColor,
                    i == 0 ? AmberColor : CyanColor,
                    20,
                    () => FranklinMobileGraphicsSettings.SetQualityLevel(level)
                );
                this.m_QualityButtons[i] = button;
                this.m_QualityLabels[i] = button.GetComponentInChildren<Text>();
            }

            this.m_QualityStatusText = this.CreateLabel(
                row,
                "Quality Status",
                string.Empty,
                new Vector2(120f, 2f),
                new Vector2(650f, 32f),
                15,
                MutedTextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold
            );

            this.CreateLabel(
                row,
                "Anti-Aliasing Label",
                "KHỬ RĂNG CƯA",
                new Vector2(-381f, -73f),
                new Vector2(222f, 34f),
                18,
                TextColor,
                TextAnchor.MiddleLeft,
                FontStyle.Bold
            );

            string[] antiAliasingLabels = { "TẮT", "FXAA", "SMAA LOW" };
            this.m_AntiAliasingButtons = new Button[antiAliasingLabels.Length];
            this.m_AntiAliasingLabels = new Text[antiAliasingLabels.Length];
            for (int i = 0; i < antiAliasingLabels.Length; i++)
            {
                int mode = AntiAliasingValues[i];
                Button button = this.CreateTextButton(
                    row,
                    $"Anti-Aliasing {antiAliasingLabels[i]}",
                    antiAliasingLabels[i],
                    new Vector2(-104f + i * 166f, -73f),
                    new Vector2(154f, 50f),
                    OffColor,
                    CyanColor,
                    16,
                    () => FranklinMobileGraphicsSettings.SetAntiAliasingMode(mode)
                );
                this.m_AntiAliasingButtons[i] = button;
                this.m_AntiAliasingLabels[i] = button.GetComponentInChildren<Text>();
            }

            this.CreateLabel(
                row,
                "Depth Of Field Label",
                "LẤY NÉT DOF",
                new Vector2(400f, -42f),
                new Vector2(174f, 24f),
                13,
                MutedTextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold
            );
            Button depthOfFieldButton = this.CreateTextButton(
                row,
                "Depth Of Field Mode",
                "TẮT",
                new Vector2(400f, -82f),
                new Vector2(174f, 48f),
                OffColor,
                AmberColor,
                16,
                this.OnDepthOfFieldButton
            );
            this.m_DepthOfFieldButtonImage = depthOfFieldButton.image;
            this.m_DepthOfFieldButtonLabel =
                depthOfFieldButton.GetComponentInChildren<Text>();
        }

        private void BuildToggleRow(
            RectTransform panel,
            string objectName,
            string title,
            string subtitle,
            Vector2 position,
            Color accent,
            Action callback,
            out Image track,
            out Text stateText,
            out Text knob)
        {
            RectTransform row = this.CreateImage(
                panel,
                objectName,
                position,
                new Vector2(500f, 192f),
                CardColor,
                this.m_RoundedSprite
            ).rectTransform;
            this.CreateImage(
                row,
                "Accent",
                new Vector2(-246f, 0f),
                new Vector2(8f, 152f),
                accent,
                this.m_SolidSprite
            );
            this.CreateLabel(
                row,
                "Title",
                title,
                new Vector2(-62f, 49f),
                new Vector2(340f, 44f),
                23,
                TextColor,
                TextAnchor.MiddleLeft,
                FontStyle.Bold
            );
            this.CreateLabel(
                row,
                "Subtitle",
                subtitle,
                new Vector2(-48f, 12f),
                new Vector2(365f, 30f),
                16,
                MutedTextColor,
                TextAnchor.MiddleLeft,
                FontStyle.Normal
            );

            Button button = this.CreateTextButton(
                row,
                "Toggle",
                string.Empty,
                new Vector2(122f, -51f),
                new Vector2(198f, 62f),
                OffColor,
                accent,
                21,
                callback
            );
            track = button.image;
            stateText = this.CreateLabel(
                button.transform as RectTransform,
                "State",
                "TẮT",
                new Vector2(22f, 0f),
                new Vector2(105f, 52f),
                20,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold
            );
            knob = this.CreateLabel(
                button.transform as RectTransform,
                "Knob",
                "●",
                new Vector2(-62f, -1f),
                new Vector2(54f, 54f),
                52,
                new Color(0.58f, 0.62f, 0.66f, 1f),
                TextAnchor.MiddleCenter,
                FontStyle.Normal
            );
        }

        private void OnFbsButton()
        {
            FranklinMobileGraphicsSettings.ToggleFbs();
        }

        private void OnSsaoButton()
        {
            if (!FranklinMobileGraphicsSettings.HasSsaoFeature) return;
            FranklinMobileGraphicsSettings.ToggleSsao();
        }

        private void OnDepthOfFieldButton()
        {
            FranklinMobileGraphicsSettings.ToggleDepthOfFieldMode();
        }

        private void ApplyAndClose()
        {
            FranklinMobileGraphicsSettings.ReapplyCurrentSettings();
            this.Close();
        }

        private void OnFbsChanged(bool enabled)
        {
            this.RefreshToggle(enabled, this.m_FbsTrack, this.m_FbsStateText,
                this.m_FbsKnob, GreenColor);
        }

        private void OnSsaoChanged(bool enabled)
        {
            this.RefreshToggle(enabled, this.m_SsaoTrack, this.m_SsaoStateText,
                this.m_SsaoKnob, CyanColor);
        }

        private void OnQualityChanged(int qualityLevel)
        {
            this.RefreshQuality(qualityLevel);
        }

        private void OnAntiAliasingChanged(int antiAliasingMode)
        {
            this.RefreshAntiAliasing(antiAliasingMode);
        }

        private void OnDepthOfFieldChanged(int depthOfFieldMode)
        {
            this.RefreshDepthOfField(depthOfFieldMode);
        }

        private void RefreshAll()
        {
            this.OnFbsChanged(FranklinMobileGraphicsSettings.FbsEnabled);
            this.OnSsaoChanged(FranklinMobileGraphicsSettings.SsaoEnabled);
            this.RefreshQuality(FranklinMobileGraphicsSettings.QualityLevel);
            this.RefreshAntiAliasing(
                FranklinMobileGraphicsSettings.AntiAliasingMode
            );
            this.RefreshDepthOfField(
                FranklinMobileGraphicsSettings.DepthOfFieldMode
            );

            if (this.m_SsaoAvailabilityText != null)
            {
                bool available = FranklinMobileGraphicsSettings.HasSsaoFeature;
                this.m_SsaoAvailabilityText.text = available
                    ? "TỐI ƯU MOBILE • HALF RES"
                    : "SSAO chưa được cài vào Renderer";
                this.m_SsaoAvailabilityText.color = available
                    ? MutedTextColor
                    : AmberColor;
            }
        }

        private void RefreshQuality(int selectedLevel)
        {
            if (this.m_QualityButtons == null) return;
            for (int i = 0; i < this.m_QualityButtons.Length; i++)
            {
                bool selected = QualityValues[i] == selectedLevel;
                Color selectedColor = selectedLevel ==
                    FranklinMobileGraphicsSettings.QualityAuto
                        ? AmberColor
                        : CyanColor;
                Image image = this.m_QualityButtons[i] != null
                    ? this.m_QualityButtons[i].image
                    : null;
                if (image != null) image.color = selected ? selectedColor : OffColor;
                if (this.m_QualityLabels != null && this.m_QualityLabels[i] != null)
                {
                    this.m_QualityLabels[i].color = selected
                        ? DarkTextColor
                        : TextColor;
                }
            }

            if (this.m_QualityStatusText != null)
            {
                bool automatic = selectedLevel ==
                    FranklinMobileGraphicsSettings.QualityAuto;
                bool low = selectedLevel ==
                    FranklinMobileGraphicsSettings.QualityLow;
                bool balanced = selectedLevel ==
                    FranklinMobileGraphicsSettings.QualityBalanced;
                bool high = selectedLevel ==
                    FranklinMobileGraphicsSettings.QualityHigh;
                string profile = RobotAstro.DynamicResolutionScaler.ActiveProfileName
                    .ToUpperInvariant();
                if (low)
                {
                    this.m_QualityStatusText.text =
                        "CHẾ ĐỘ THẤP: TẮT FBS • SSAO • AA • DOF • POST";
                    this.m_QualityStatusText.color = GreenColor;
                }
                else if (balanced)
                {
                    this.m_QualityStatusText.text =
                        "CHẾ ĐỘ VỪA: CHỈ BẬT FBS";
                    this.m_QualityStatusText.color = CyanColor;
                }
                else if (high)
                {
                    this.m_QualityStatusText.text =
                        "CHẾ ĐỘ CAO: FULL HIỆU ỨNG • DOF CÓ THỂ TẮT";
                    this.m_QualityStatusText.color = AmberColor;
                }
                else if (automatic)
                {
                    this.m_QualityStatusText.text =
                        $"AUTO ĐANG DÙNG: {profile}  •  TỰ CÂN BẰNG THEO FPS";
                    this.m_QualityStatusText.color = AmberColor;
                }
                else
                {
                    this.m_QualityStatusText.text =
                        "DYNAMIC RESOLUTION VẪN GIỮ FPS ỔN ĐỊNH";
                    this.m_QualityStatusText.color = MutedTextColor;
                }
            }
        }

        private void RefreshAntiAliasing(int selectedMode)
        {
            if (this.m_AntiAliasingButtons == null) return;
            for (int i = 0; i < this.m_AntiAliasingButtons.Length; i++)
            {
                bool selected = AntiAliasingValues[i] == selectedMode;
                Image image = this.m_AntiAliasingButtons[i] != null
                    ? this.m_AntiAliasingButtons[i].image
                    : null;
                if (image != null) image.color = selected ? CyanColor : OffColor;
                if (this.m_AntiAliasingLabels != null &&
                    this.m_AntiAliasingLabels[i] != null)
                {
                    this.m_AntiAliasingLabels[i].color = selected
                        ? DarkTextColor
                        : TextColor;
                }
            }

        }

        private void RefreshDepthOfField(int mode)
        {
            if (this.m_DepthOfFieldButtonImage == null ||
                this.m_DepthOfFieldButtonLabel == null)
            {
                return;
            }

            switch (mode)
            {
                case FranklinMobileGraphicsSettings.DepthOfFieldNear:
                    this.m_DepthOfFieldButtonImage.color = CyanColor;
                    this.m_DepthOfFieldButtonLabel.color = DarkTextColor;
                    this.m_DepthOfFieldButtonLabel.text = "GẦN";
                    break;
                case FranklinMobileGraphicsSettings.DepthOfFieldFar:
                    this.m_DepthOfFieldButtonImage.color = AmberColor;
                    this.m_DepthOfFieldButtonLabel.color = DarkTextColor;
                    this.m_DepthOfFieldButtonLabel.text = "XA";
                    break;
                default:
                    this.m_DepthOfFieldButtonImage.color = OffColor;
                    this.m_DepthOfFieldButtonLabel.color = TextColor;
                    this.m_DepthOfFieldButtonLabel.text = "TẮT";
                    break;
            }
        }

        private void RefreshToggle(
            bool enabled,
            Image track,
            Text state,
            Text knob,
            Color accent)
        {
            if (track != null) track.color = enabled ? accent : OffColor;
            if (state != null)
            {
                state.text = enabled ? "BẬT" : "TẮT";
                state.rectTransform.anchoredPosition = enabled
                    ? new Vector2(-24f, 0f)
                    : new Vector2(24f, 0f);
            }
            if (knob != null)
            {
                knob.rectTransform.anchoredPosition = enabled
                    ? new Vector2(66f, -1f)
                    : new Vector2(-66f, -1f);
                knob.color = enabled ? Color.white : new Color(0.58f, 0.62f, 0.66f, 1f);
            }
        }

        private Image CreateImage(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 size,
            Color color,
            Sprite sprite = null)
        {
            GameObject imageObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Center(rect, position, size);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite != null ? sprite : this.m_SolidSprite;
            image.type = sprite != null && sprite == this.m_RoundedSprite
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateLabel(
            Transform parent,
            string objectName,
            string value,
            Vector2 position,
            Vector2 size,
            int fontSize,
            Color color,
            TextAnchor alignment,
            FontStyle fontStyle)
        {
            GameObject textObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Center(rect, position, size);
            Text label = textObject.GetComponent<Text>();
            label.font = this.m_Font;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color;
            label.text = value;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private Button CreateTextButton(
            Transform parent,
            string objectName,
            string value,
            Vector2 position,
            Vector2 size,
            Color backgroundColor,
            Color accentColor,
            int fontSize,
            Action callback)
        {
            GameObject buttonObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Center(rect, position, size);
            Image image = buttonObject.GetComponent<Image>();
            image.sprite = this.m_RoundedSprite != null
                ? this.m_RoundedSprite
                : this.m_SolidSprite;
            image.type = this.m_RoundedSprite != null
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.color = backgroundColor;
            image.raycastTarget = true;
            Button button = buttonObject.GetComponent<Button>();
            ConfigureButton(button, image, accentColor);
            if (callback != null) button.onClick.AddListener(() => callback());
            this.CreateLabel(
                rect,
                "Label",
                value,
                Vector2.zero,
                size,
                fontSize,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold
            );
            return button;
        }

        private static void ConfigureButton(Button button, Image image, Color accent)
        {
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.18f);
            colors.pressedColor = Color.Lerp(Color.white, accent, 0.42f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        private static void Center(RectTransform rect, Vector2 position, Vector2 size)
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
