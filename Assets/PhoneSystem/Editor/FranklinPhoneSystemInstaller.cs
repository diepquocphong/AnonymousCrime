using System;
using FranklinGame.PhoneSystem;
using GameCreator.Runtime.Cameras;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.PhoneSystem.Editor
{
    public static class FranklinPhoneSystemInstaller
    {
        private const string ROOT = "Assets/PhoneSystem";
        private const string PREFAB_PATH =
            ROOT + "/Resources/FranklinPhoneSystem.prefab";
        private const string ICON_ROOT = ROOT + "/Resources/Icons/GTAStyle/";
        private const string AUDIO_ROOT = ROOT + "/Audio/";
        private const string PHONE_MODEL_PATH =
            ROOT + "/Phone_A1_LP/Phone_A1_LP.prefab";
        private const string CAMERA_SHOT_PATH =
            "Assets/Prefab/Camera Shot.prefab";
        private const string FONT_PATH =
            "Assets/Plugins/GameCreator/Installs/GameCreator.Blockout@1.6.12/UI/_Fonts/JosefinSans-Bold.ttf";

        private static readonly Color PHONE_SHELL =
            new(0.19f, 0.23f, 0.25f, 1f);
        private static readonly Color PHONE_SCREEN =
            new(0.025f, 0.07f, 0.085f, 1f);
        private static readonly Color PANEL =
            new(0.075f, 0.12f, 0.13f, 0.98f);
        private static readonly Color PANEL_LIGHT =
            new(0.15f, 0.19f, 0.2f, 1f);
        private static readonly Color CYAN =
            new(0.16f, 0.68f, 0.67f, 1f);
        private static readonly Color MAGENTA =
            new(0.74f, 0.18f, 0.16f, 1f);
        private static readonly Color AMBER =
            new(0.9f, 0.62f, 0.12f, 1f);
        private static readonly Color MUTED =
            new(0.65f, 0.71f, 0.71f, 1f);

        private static Font s_Font;
        private static Sprite s_Rounded;
        private static Sprite s_Knob;

        [MenuItem("Tools/Franklin Game/Install Phone System UI")]
        public static void Install()
        {
            EnsureFolders();
            AssetDatabase.Refresh();
            Sprite[] appIcons = EnsureAppIcons();
            s_Font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
            if (s_Font == null)
                throw new InvalidOperationException("Phone font is missing: " + FONT_PATH);
            s_Rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/UISprite.psd"
            );
            s_Knob = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/Knob.psd"
            );

            GameObject root = new(
                "Franklin Phone System",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(FranklinPhoneSystem),
                typeof(FranklinPhoneHandPresentation),
                typeof(FranklinPhoneSelfieCamera)
            );

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 2400;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                Image blocker = CreateStretchPanel(
                    "Phone Interface",
                    root.transform,
                    new Color(0f, 0f, 0f, 0.06f),
                    null
                );
                blocker.raycastTarget = true;
                RectTransform safeArea = CreateStretchRect(
                    "Safe Area",
                    blocker.transform
                );

                Image phoneShell = CreatePanel(
                    "Phone Device",
                    safeArea,
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-433f, -48f),
                    new Vector2(440f, 820f),
                    PHONE_SHELL,
                    s_Rounded
                );
                Shadow shellShadow = phoneShell.gameObject.AddComponent<Shadow>();
                shellShadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
                shellShadow.effectDistance = new Vector2(-9f, -10f);
                shellShadow.useGraphicAlpha = true;
                Outline shellOutline = phoneShell.gameObject.AddComponent<Outline>();
                shellOutline.effectColor = new Color(0.04f, 0.055f, 0.06f, 1f);
                shellOutline.effectDistance = new Vector2(4f, -4f);
                shellOutline.useGraphicAlpha = true;

                Image screen = CreatePanel(
                    "Phone Screen",
                    phoneShell.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(408f, 778f),
                    PHONE_SCREEN,
                    s_Rounded
                );
                screen.raycastTarget = false;

                CreatePanel(
                    "Speaker",
                    phoneShell.rectTransform,
                    Center,
                    Center,
                    Center,
                    new Vector2(0f, 394f),
                    new Vector2(70f, 5f),
                    new Color(0.035f, 0.045f, 0.05f, 1f),
                    s_Rounded
                );
                CreatePanel(
                    "Lower Speaker Detail",
                    phoneShell.rectTransform,
                    Center,
                    Center,
                    Center,
                    new Vector2(0f, -394f),
                    new Vector2(96f, 4f),
                    new Color(0.055f, 0.07f, 0.075f, 1f),
                    s_Rounded
                );

                Text timeText = CreateText(
                    "Time",
                    screen.rectTransform,
                    new Vector2(-150f, 357f),
                    new Vector2(90f, 30f),
                    20,
                    TextAnchor.MiddleLeft,
                    FontStyle.Bold,
                    Color.white
                );
                timeText.text = "09:41";
                Text dateText = CreateText(
                    "Date",
                    screen.rectTransform,
                    new Vector2(0f, 357f),
                    new Vector2(180f, 30f),
                    14,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold,
                    MUTED
                );
                dateText.text = "WED • 12 AUG";
                Text connection = CreateText(
                    "Connection",
                    screen.rectTransform,
                    new Vector2(150f, 357f),
                    new Vector2(88f, 30f),
                    15,
                    TextAnchor.MiddleRight,
                    FontStyle.Bold,
                    CYAN
                );
                connection.text = "5G  ▮▮▮";

                CreatePanel(
                    "Header",
                    screen.rectTransform,
                    Center,
                    Center,
                    Center,
                    new Vector2(0f, 312f),
                    new Vector2(382f, 64f),
                    PANEL,
                    s_Rounded
                );
                CreatePanel(
                    "Header Accent",
                    screen.rectTransform,
                    Center,
                    Center,
                    Center,
                    new Vector2(-189f, 312f),
                    new Vector2(4f, 42f),
                    AMBER,
                    s_Rounded
                );
                Text headerTitle = CreateText(
                    "Header Title",
                    screen.rectTransform,
                    new Vector2(-76f, 321f),
                    new Vector2(210f, 28f),
                    23,
                    TextAnchor.MiddleLeft,
                    FontStyle.Bold,
                    Color.white
                );
                headerTitle.text = "HOME";
                Text headerSubtitle = CreateText(
                    "Header Subtitle",
                    screen.rectTransform,
                    new Vector2(-57f, 299f),
                    new Vector2(248f, 20f),
                    13,
                    TextAnchor.MiddleLeft,
                    FontStyle.Bold,
                    CYAN
                );
                headerSubtitle.text = "LS MOBILE • ONLINE";

                Button closeButton = CreateTextButton(
                    "Close Phone",
                    screen.rectTransform,
                    new Vector2(166f, 312f),
                    new Vector2(46f, 46f),
                    "X",
                    17,
                    new Color(0.28f, 0.07f, 0.065f, 1f),
                    Color.white
                );

                RectTransform contentRoot = CreateRect(
                    "Content",
                    screen.rectTransform,
                    Center,
                    Center,
                    Center,
                    Vector2.zero,
                    new Vector2(410f, 590f)
                );
                contentRoot.localScale = Vector3.one * 0.92f;
                Image home = CreatePanel(
                    "Home Screen",
                    contentRoot,
                    Center,
                    Center,
                    Center,
                    Vector2.zero,
                    contentRoot.sizeDelta,
                    new Color(0.045f, 0.15f, 0.18f, 1f),
                    s_Rounded
                );
                BuildHomeDecor(home.rectTransform);

                string[] labels =
                {
                    "Điện thoại", "Tin nhắn", "Ghi chú",
                    "Bản đồ", "Camera", "Ảnh"
                };
                Button[] appButtons = new Button[6];
                for (int i = 0; i < appButtons.Length; ++i)
                {
                    int column = i % 2;
                    int row = i / 2;
                    Vector2 position = new(
                        -100f + column * 200f,
                        130f - row * 174f
                    );
                    appButtons[i] = CreateAppButton(
                        home.rectTransform,
                        labels[i],
                        position,
                        appIcons[i]
                    );
                    if (i == 1)
                    {
                        Image badge = CreatePanel(
                            "Unread Badge",
                            appButtons[i].transform,
                            Center,
                            Center,
                            Center,
                            new Vector2(37f, 37f),
                            new Vector2(28f, 28f),
                            new Color(0.96f, 0.16f, 0.32f, 1f),
                            s_Knob
                        );
                        Text badgeText = CreateText(
                            "Count",
                            badge.transform,
                            Vector2.zero,
                            new Vector2(28f, 28f),
                            14,
                            TextAnchor.MiddleCenter,
                            FontStyle.Bold,
                            Color.white
                        );
                        badgeText.text = "3";
                    }
                }

                GameObject phoneScreen = BuildPhoneScreen(
                    contentRoot,
                    out Text dialNumberText,
                    out Text dialStatusText,
                    out Button[] dialKeyButtons,
                    out Text[] dialKeyLabels,
                    out Button dialDeleteButton,
                    out Button dialCallButton,
                    out Button dialModeButton,
                    out Text dialModeButtonText,
                    out GameObject dialKeypadPanel,
                    out GameObject callHistoryPanel,
                    out Button dialKeypadTabButton,
                    out Button callHistoryTabButton,
                    out Text callHistoryText,
                    out Button clearCallHistoryButton
                );
                GameObject messagesScreen = BuildMessagesScreen(
                    contentRoot,
                    out GameObject messageListPanel,
                    out GameObject messageDetailPanel,
                    out Button[] messageButtons,
                    out Text messageSenderText,
                    out Text messageBodyText,
                    out Text messageResultText,
                    out Button messageAcceptButton,
                    out Button messageCancelButton
                );
                GameObject notesScreen = BuildNotesScreen(
                    contentRoot,
                    out Text textEntryText,
                    out Text textStatusText,
                    out Button[] textKeyButtons,
                    out Button textDeleteButton,
                    out Button textSendButton
                );
                GameObject[] appScreens =
                {
                    phoneScreen,
                    messagesScreen,
                    notesScreen,
                    BuildMapScreen(contentRoot),
                    BuildCameraScreen(contentRoot),
                    BuildPhotosScreen(contentRoot)
                };
                foreach (GameObject appScreen in appScreens)
                    appScreen.SetActive(false);

                FranklinPhoneSystem system = root.GetComponent<FranklinPhoneSystem>();
                system.Configure(
                    blocker.gameObject,
                    safeArea,
                    phoneShell.rectTransform,
                    home.gameObject,
                    appScreens,
                    appButtons,
                    null,
                    closeButton,
                    null,
                    timeText,
                    dateText,
                    headerTitle,
                    headerSubtitle,
                    dialNumberText,
                    dialStatusText,
                    dialKeyButtons,
                    dialKeyLabels,
                    dialDeleteButton,
                    dialCallButton,
                    dialModeButton,
                    dialModeButtonText,
                    dialKeypadPanel,
                    callHistoryPanel,
                    dialKeypadTabButton,
                    callHistoryTabButton,
                    callHistoryText,
                    clearCallHistoryButton,
                    textEntryText,
                    textStatusText,
                    textKeyButtons,
                    textDeleteButton,
                    textSendButton,
                    messageListPanel,
                    messageDetailPanel,
                    messageButtons,
                    messageSenderText,
                    messageBodyText,
                    messageResultText,
                    messageAcceptButton,
                    messageCancelButton
                );
                system.ConfigureAudio(
                    LoadAudio("phone_open"),
                    LoadAudio("phone_close"),
                    LoadAudio("app_open"),
                    LoadAudio("app_back"),
                    LoadAudio("message_open"),
                    new[]
                    {
                        LoadAudio("key_01"),
                        LoadAudio("key_02"),
                        LoadAudio("key_03"),
                        LoadAudio("key_04")
                    },
                    LoadAudio("mode_switch"),
                    LoadAudio("delete"),
                    LoadAudio("call"),
                    LoadAudio("success"),
                    LoadAudio("error"),
                    LoadAudio("history_clear"),
                    LoadAudio("mission_cancel")
                );
                GameObject phoneModel =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PHONE_MODEL_PATH);
                if (phoneModel == null)
                {
                    throw new InvalidOperationException(
                        "Physical phone model is missing: " + PHONE_MODEL_PATH
                    );
                }
                root.GetComponent<FranklinPhoneHandPresentation>()
                    .Configure(phoneModel);

                GameObject cameraShotObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(CAMERA_SHOT_PATH);
                ShotCamera cameraShot = cameraShotObject != null
                    ? cameraShotObject.GetComponent<ShotCamera>()
                    : null;
                if (cameraShot == null)
                {
                    throw new InvalidOperationException(
                        "GC2 Camera Shot prefab is missing: " + CAMERA_SHOT_PATH
                    );
                }
                root.GetComponent<FranklinPhoneSelfieCamera>()
                    .Configure(cameraShot);

                PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out bool saved);
                if (!saved)
                    throw new InvalidOperationException("Could not save Phone System prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            ValidateInstallation();
        }

        [MenuItem("Tools/Franklin Game/Preview Phone System", true)]
        private static bool CanPreview()
        {
            return EditorApplication.isPlaying &&
                   FranklinPhoneSystem.Instance != null;
        }

        [MenuItem("Tools/Franklin Game/Preview Phone System")]
        private static void Preview()
        {
            FranklinPhoneSystem.Instance?.SetOpen(true);
        }

        [MenuItem("Tools/Franklin Game/Preview Phone Dial Pad", true)]
        [MenuItem("Tools/Franklin Game/Preview Phone Text Input", true)]
        private static bool CanPreviewInputScreens()
        {
            return CanPreview();
        }

        [MenuItem("Tools/Franklin Game/Preview Phone Dial Pad")]
        private static void PreviewDialPad()
        {
            FranklinPhoneSystem.Instance?.SetOpen(true);
            FranklinPhoneSystem.Instance?.OpenApp(0);
        }

        [MenuItem("Tools/Franklin Game/Preview Phone Text Input")]
        private static void PreviewTextInput()
        {
            FranklinPhoneSystem.Instance?.SetOpen(true);
            FranklinPhoneSystem.Instance?.OpenApp(2);
        }

        [MenuItem("Tools/Franklin Game/Preview Phone Mission Message", true)]
        private static bool CanPreviewMissionMessage()
        {
            return CanPreview();
        }

        [MenuItem("Tools/Franklin Game/Preview Phone Mission Message")]
        private static void PreviewMissionMessage()
        {
            FranklinPhoneSystem.Instance?.SetOpen(true);
            FranklinPhoneSystem.Instance?.OpenApp(1);
            FranklinPhoneSystem.Instance?.OpenMessage(0);
        }

        [MenuItem("Tools/Franklin Game/Test Phone T9 Input", true)]
        private static bool CanTestT9Input()
        {
            return CanPreview();
        }

        [MenuItem("Tools/Franklin Game/Test Phone T9 Input")]
        private static void TestT9Input()
        {
            FranklinPhoneSystem phone = FranklinPhoneSystem.Instance;
            if (phone == null) return;

            phone.SetOpen(true);
            phone.OpenApp(2);
            phone.PressTextKey(1);
            phone.PressTextKey(1);
            phone.PressTextKey(1); // C
            phone.PressTextKey(2);
            phone.PressTextKey(2); // E
        }

        [MenuItem("Tools/Franklin Game/Test Phone Accept Mission", true)]
        private static bool CanTestAcceptMission()
        {
            return CanPreview();
        }

        [MenuItem("Tools/Franklin Game/Test Phone Accept Mission")]
        private static void TestAcceptMission()
        {
            FranklinPhoneSystem phone = FranklinPhoneSystem.Instance;
            if (phone == null) return;

            phone.SetOpen(true);
            phone.OpenApp(1);
            phone.OpenMessage(0);
            phone.AcceptMission();
        }

        [MenuItem("Tools/Franklin Game/Test Phone Number Call", true)]
        private static bool CanTestNumberCall()
        {
            return CanPreview();
        }

        [MenuItem("Tools/Franklin Game/Test Phone Number Call")]
        private static void TestNumberCall()
        {
            FranklinPhoneSystem phone = FranklinPhoneSystem.Instance;
            if (phone == null) return;

            phone.SetOpen(true);
            phone.OpenApp(0);
            phone.PressDialKey(5);
            phone.PressDialKey(4);
            phone.PressDialKey(4);
            phone.PressDialKey(5);
            phone.PressDialKey(7);
            phone.CallDialedNumber();
        }

        [MenuItem("Tools/Franklin Game/Test Phone Dial Mode", true)]
        private static bool CanTestDialMode()
        {
            return CanPreview();
        }

        [MenuItem("Tools/Franklin Game/Test Phone Dial Mode")]
        private static void TestDialMode()
        {
            FranklinPhoneSystem phone = FranklinPhoneSystem.Instance;
            if (phone == null) return;

            phone.SetOpen(true);
            phone.OpenApp(0);
            phone.ToggleDialMode();
        }

        [MenuItem("Tools/Franklin Game/Preview Phone Call History", true)]
        private static bool CanPreviewCallHistory()
        {
            return CanPreview();
        }

        [MenuItem("Tools/Franklin Game/Preview Phone Call History")]
        private static void PreviewCallHistory()
        {
            FranklinPhoneSystem phone = FranklinPhoneSystem.Instance;
            if (phone == null) return;

            phone.SetOpen(true);
            phone.OpenApp(0);
            phone.ShowCallHistoryTab();
        }

        [MenuItem("Tools/Franklin Game/Test Clear Phone Call History", true)]
        private static bool CanTestClearCallHistory()
        {
            return CanPreview();
        }

        [MenuItem("Tools/Franklin Game/Test Clear Phone Call History")]
        private static void TestClearCallHistory()
        {
            FranklinPhoneSystem phone = FranklinPhoneSystem.Instance;
            if (phone == null) return;

            phone.SetOpen(true);
            phone.OpenApp(0);
            phone.ShowCallHistoryTab();
            phone.ClearCallHistory();
        }

        public static void ValidateInstallation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            FranklinPhoneSystem system =
                prefab != null ? prefab.GetComponent<FranklinPhoneSystem>() : null;
            Canvas canvas = prefab != null ? prefab.GetComponent<Canvas>() : null;
            if (system == null || !system.IsConfigured || canvas == null ||
                canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                canvas.sortingOrder < 2000)
            {
                throw new InvalidOperationException(
                    "Phone System prefab is incomplete or does not use its own overlay Canvas"
                );
            }

            foreach (string iconName in IconNames)
            {
                string path = ICON_ROOT + iconName + ".png";
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                    throw new InvalidOperationException("Phone icon is missing: " + path);
            }

            Debug.Log(
                "Phone System validation passed: standalone right-side Canvas, six app screens, " +
                "safe-area layout and minimap phone-event integration are configured."
            );
        }

        private static readonly string[] IconNames =
        {
            "phone", "messages", "notes", "map", "camera", "photos"
        };

        private static Sprite[] EnsureAppIcons()
        {
            Sprite[] icons = new Sprite[IconNames.Length];
            for (int i = 0; i < IconNames.Length; ++i)
            {
                string path = ICON_ROOT + IconNames[i] + ".png";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("Phone icon is missing: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
                icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (icons[i] == null)
                    throw new InvalidOperationException("Could not import phone icon: " + path);
            }
            return icons;
        }

        private static void BuildHomeDecor(RectTransform home)
        {
            Text greeting = CreateText(
                "Applications Label",
                home,
                new Vector2(-85f, 254f),
                new Vector2(190f, 34f),
                22,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                Color.white
            );
            greeting.text = "APPLICATIONS";
            Text district = CreateText(
                "App Count",
                home,
                new Vector2(112f, 254f),
                new Vector2(155f, 24f),
                15,
                TextAnchor.MiddleRight,
                FontStyle.Bold,
                MUTED
            );
            district.text = "6 APPS";
            CreatePanel(
                "Home Accent",
                home,
                Center,
                Center,
                Center,
                new Vector2(0f, 229f),
                new Vector2(372f, 3f),
                new Color(AMBER.r, AMBER.g, AMBER.b, 0.78f),
                s_Rounded
            );
        }

        private static GameObject BuildPhoneScreen(
            Transform parent,
            out Text numberText,
            out Text statusText,
            out Button[] keyButtons,
            out Text[] keyLabels,
            out Button deleteButton,
            out Button callButton,
            out Button modeButton,
            out Text modeButtonText,
            out GameObject keypadPanel,
            out GameObject historyPanel,
            out Button keypadTabButton,
            out Button historyTabButton,
            out Text historyText,
            out Button clearHistoryButton)
        {
            RectTransform root = CreateAppScreen(parent, "Phone App");

            keypadTabButton = CreateTextButton(
                "Keypad Tab",
                root,
                new Vector2(-96f, 250f),
                new Vector2(184f, 54f),
                "KEYPAD",
                17,
                new Color(0.13f, 0.46f, 0.48f, 1f),
                Color.white
            );
            historyTabButton = CreateTextButton(
                "History Tab",
                root,
                new Vector2(96f, 250f),
                new Vector2(184f, 54f),
                "RECENT",
                17,
                PANEL_LIGHT,
                Color.white
            );

            RectTransform keypad = CreateRect(
                "Dial Keypad Panel",
                root,
                Center,
                Center,
                Center,
                new Vector2(0f, -26f),
                new Vector2(400f, 500f)
            );
            keypadPanel = keypad.gameObject;
            Image numberPanel = CreatePanel(
                "Dial Number Display",
                keypad,
                Center,
                Center,
                Center,
                new Vector2(0f, 204f),
                new Vector2(380f, 66f),
                new Color(0.02f, 0.085f, 0.1f, 1f),
                s_Rounded
            );
            Outline numberOutline = numberPanel.gameObject.AddComponent<Outline>();
            numberOutline.effectColor = new Color(CYAN.r, CYAN.g, CYAN.b, 0.65f);
            numberOutline.effectDistance = new Vector2(2f, -2f);
            numberText = CreateText(
                "Dial Number",
                numberPanel.transform,
                Vector2.zero,
                new Vector2(350f, 56f),
                27,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                Color.white
            );
            numberText.text = "ENTER NUMBER";
            statusText = CreateText(
                "Dial Status",
                keypad,
                new Vector2(0f, 162f),
                new Vector2(380f, 22f),
                13,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                MUTED
            );
            statusText.text = "ENTER A NUMBER";

            string[] labels =
            {
                "1", "2", "3", "4", "5", "6",
                "7", "8", "9", "*", "0", "#"
            };
            keyButtons = CreateDialKeypad(keypad, labels, out keyLabels);

            modeButton = CreateTextButton(
                "Dial Input Mode",
                keypad,
                new Vector2(0f, -150f),
                new Vector2(380f, 48f),
                "ABC",
                17,
                new Color(0.15f, 0.28f, 0.31f, 1f),
                CYAN
            );
            modeButtonText = modeButton.GetComponentInChildren<Text>();
            deleteButton = CreateTextButton(
                "Dial Delete",
                keypad,
                new Vector2(-98f, -218f),
                new Vector2(184f, 66f),
                "DEL",
                18,
                new Color(0.31f, 0.12f, 0.1f, 1f),
                Color.white
            );
            callButton = CreateTextButton(
                "Dial Call",
                keypad,
                new Vector2(98f, -218f),
                new Vector2(184f, 66f),
                "CALL",
                18,
                new Color(0.15f, 0.43f, 0.23f, 1f),
                Color.white
            );
            RectTransform history = CreateRect(
                "Call History Panel",
                root,
                Center,
                Center,
                Center,
                new Vector2(0f, -26f),
                new Vector2(400f, 500f)
            );
            historyPanel = history.gameObject;
            historyPanel.SetActive(false);
            Text historyTitle = CreateText(
                "History Title",
                history,
                new Vector2(-74f, 210f),
                new Vector2(230f, 38f),
                25,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                CYAN
            );
            historyTitle.text = "CALL HISTORY";
            historyText = CreateText(
                "Call History",
                history,
                new Vector2(0f, 2f),
                new Vector2(372f, 360f),
                18,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                Color.white
            );
            historyText.text = "RECENT • NO CALLS";
            clearHistoryButton = CreateTextButton(
                "Clear Call History",
                history,
                new Vector2(0f, -218f),
                new Vector2(380f, 66f),
                "CLEAR HISTORY",
                18,
                new Color(0.31f, 0.12f, 0.1f, 1f),
                Color.white
            );
            return root.gameObject;
        }

        private static Button[] CreateDialKeypad(
            RectTransform parent,
            string[] labels,
            out Text[] keyLabels)
        {
            Button[] buttons = new Button[labels.Length];
            keyLabels = new Text[labels.Length];
            for (int i = 0; i < labels.Length; ++i)
            {
                int column = i % 3;
                int row = i / 3;
                buttons[i] = CreateTextButton(
                    "Dial Key " + i,
                    parent,
                    new Vector2(-130f + column * 130f, 115f - row * 69f),
                    new Vector2(122f, 64f),
                    labels[i],
                    21,
                    PANEL_LIGHT,
                    Color.white
                );
                keyLabels[i] = buttons[i].GetComponentInChildren<Text>();
            }
            return buttons;
        }

        private static GameObject BuildMessagesScreen(
            Transform parent,
            out GameObject listPanel,
            out GameObject detailPanel,
            out Button[] messageButtons,
            out Text senderText,
            out Text bodyText,
            out Text resultText,
            out Button acceptButton,
            out Button cancelButton)
        {
            RectTransform root = CreateAppScreen(parent, "Messages App");
            RectTransform list = CreateRect(
                "Message List",
                root,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(392f, 560f)
            );
            listPanel = list.gameObject;
            string[] senders = { "LAMAR", "LESTER", "DOWNTOWN CAB" };
            string[] previews =
            {
                "Meet me at Strawberry...",
                "Go to the marked warehouse...",
                "A VIP pickup is waiting..."
            };
            Color[] accents = { MAGENTA, CYAN, AMBER };
            messageButtons = new Button[3];
            for (int i = 0; i < messageButtons.Length; ++i)
            {
                Image row = CreatePanel(
                    "Message " + senders[i],
                    list,
                    Center,
                    Center,
                    Center,
                    new Vector2(0f, 170f - i * 125f),
                    new Vector2(368f, 108f),
                    PANEL_LIGHT,
                    s_Rounded
                );
                row.raycastTarget = true;
                Button rowButton = row.gameObject.AddComponent<Button>();
                rowButton.targetGraphic = row;
                Text sender = CreateText(
                    "Sender",
                    row.transform,
                    new Vector2(-27f, 22f),
                    new Vector2(278f, 30f),
                    22,
                    TextAnchor.MiddleLeft,
                    FontStyle.Bold,
                    Color.white
                );
                sender.text = senders[i];
                Text preview = CreateText(
                    "Preview",
                    row.transform,
                    new Vector2(-27f, -15f),
                    new Vector2(278f, 28f),
                    15,
                    TextAnchor.MiddleLeft,
                    FontStyle.Bold,
                    MUTED
                );
                preview.text = previews[i];
                Text chevron = CreateText(
                    "Open",
                    row.transform,
                    new Vector2(148f, 0f),
                    new Vector2(42f, 80f),
                    30,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold,
                    accents[i]
                );
                chevron.text = ">";
                CreatePanel(
                    "Unread",
                    row.transform,
                    Center,
                    Center,
                    Center,
                    new Vector2(-165f, 0f),
                    new Vector2(6f, 82f),
                    accents[i],
                    s_Rounded
                );
                messageButtons[i] = rowButton;
            }
            Text hint = CreateText(
                "Message Hint",
                list,
                new Vector2(0f, -225f),
                new Vector2(360f, 30f),
                14,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                MUTED
            );
            hint.text = "TAP A MESSAGE TO VIEW MISSION";

            RectTransform detail = CreateRect(
                "Message Detail",
                root,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(392f, 560f)
            );
            detailPanel = detail.gameObject;
            detailPanel.SetActive(false);
            senderText = CreateText(
                "Mission Sender",
                detail,
                new Vector2(0f, 192f),
                new Vector2(360f, 44f),
                28,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                CYAN
            );
            senderText.text = "LAMAR";
            Image missionBody = CreatePanel(
                "Mission Body",
                detail,
                Center,
                Center,
                Center,
                new Vector2(0f, 50f),
                new Vector2(368f, 220f),
                PANEL_LIGHT,
                s_Rounded
            );
            bodyText = CreateText(
                "Mission Text",
                missionBody.transform,
                Vector2.zero,
                new Vector2(324f, 180f),
                22,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                Color.white
            );
            bodyText.text = "Meet me at Strawberry. We need a fast car for the pickup.";
            resultText = CreateText(
                "Mission Result",
                detail,
                new Vector2(0f, -88f),
                new Vector2(360f, 34f),
                16,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                AMBER
            );
            resultText.text = "MISSION REQUEST";
            cancelButton = CreateTextButton(
                "Cancel Mission",
                detail,
                new Vector2(-96f, -180f),
                new Vector2(176f, 68f),
                "CANCEL",
                18,
                new Color(0.31f, 0.12f, 0.1f, 1f),
                Color.white
            );
            acceptButton = CreateTextButton(
                "Accept Mission",
                detail,
                new Vector2(96f, -180f),
                new Vector2(176f, 68f),
                "ACCEPT",
                18,
                new Color(0.15f, 0.43f, 0.23f, 1f),
                Color.white
            );
            return root.gameObject;
        }

        private static Button[] CreateKeypad(
            RectTransform parent,
            string prefix,
            string[] labels,
            float firstRowY,
            float rowSpacing)
        {
            Button[] buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; ++i)
            {
                int column = i % 3;
                int row = i / 3;
                buttons[i] = CreateTextButton(
                    prefix + " Key " + i,
                    parent,
                    new Vector2(-112f + column * 112f, firstRowY - row * rowSpacing),
                    new Vector2(106f, 62f),
                    labels[i],
                    17,
                    PANEL_LIGHT,
                    Color.white
                );
            }
            return buttons;
        }

        private static GameObject BuildNotesScreen(
            Transform parent,
            out Text entryText,
            out Text statusText,
            out Button[] keyButtons,
            out Button deleteButton,
            out Button saveButton)
        {
            RectTransform root = CreateAppScreen(parent, "Notes App");
            Image entryPanel = CreatePanel(
                "Note Entry",
                root,
                Center,
                Center,
                Center,
                new Vector2(0f, 194f),
                new Vector2(368f, 92f),
                new Color(0.08f, 0.09f, 0.075f, 1f),
                s_Rounded
            );
            entryText = CreateText(
                "Note Text",
                entryPanel.transform,
                Vector2.zero,
                new Vector2(330f, 70f),
                22,
                TextAnchor.UpperLeft,
                FontStyle.Bold,
                Color.white
            );
            entryText.text = "ENTER NOTE...";
            statusText = CreateText(
                "Note Status",
                root,
                new Vector2(0f, 134f),
                new Vector2(360f, 24f),
                13,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                MUTED
            );
            statusText.text = "MULTI-TAP T9 • TAP A KEY";
            string[] labels =
            {
                "1\n.,?", "2\nABC", "3\nDEF",
                "4\nGHI", "5\nJKL", "6\nMNO",
                "7\nPQRS", "8\nTUV", "9\nWXYZ",
                "*", "0\nSPACE", "#"
            };
            keyButtons = CreateKeypad(root, "Text", labels, 92f, 68f);
            deleteButton = CreateTextButton(
                "Note Delete",
                root,
                new Vector2(-96f, -215f),
                new Vector2(176f, 60f),
                "DEL",
                17,
                new Color(0.31f, 0.12f, 0.1f, 1f),
                Color.white
            );
            saveButton = CreateTextButton(
                "Note Save",
                root,
                new Vector2(96f, -215f),
                new Vector2(176f, 60f),
                "SAVE",
                17,
                new Color(0.15f, 0.43f, 0.23f, 1f),
                Color.white
            );
            return root.gameObject;
        }

        private static GameObject BuildMapScreen(Transform parent)
        {
            RectTransform root = CreateAppScreen(parent, "Map App");
            CreateSectionTitle(root, "LOS SANTOS MAP", CYAN);
            Image map = CreatePanel(
                "Map Preview",
                root,
                Center,
                Center,
                Center,
                new Vector2(0f, 20f),
                new Vector2(340f, 350f),
                new Color(0.025f, 0.09f, 0.12f, 1f),
                s_Rounded
            );
            Color street = new(0.2f, 0.32f, 0.38f, 0.9f);
            CreateMapLine(map.rectTransform, new Vector2(-42f, 34f), new Vector2(310f, 7f), 23f, street);
            CreateMapLine(map.rectTransform, new Vector2(15f, -15f), new Vector2(330f, 8f), -35f, street);
            CreateMapLine(map.rectTransform, new Vector2(-95f, 5f), new Vector2(270f, 6f), 82f, street);
            CreateMapLine(map.rectTransform, new Vector2(86f, 18f), new Vector2(260f, 6f), 72f, street);
            CreateMapLine(map.rectTransform, new Vector2(3f, -22f), new Vector2(170f, 8f), 18f, CYAN);
            Text north = CreateText("North", map.transform, new Vector2(0f, 145f), new Vector2(40f, 28f), 18, TextAnchor.MiddleCenter, FontStyle.Bold, MAGENTA);
            north.text = "N";
            Text marker = CreateText("Player Marker", map.transform, new Vector2(4f, -14f), new Vector2(52f, 52f), 31, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            marker.text = "◆";
            CreateActionCard(root, -205f, "CURRENT • STRAWBERRY", "SET ROUTE", CYAN);
            return root.gameObject;
        }

        private static GameObject BuildCameraScreen(Transform parent)
        {
            RectTransform root = CreateAppScreen(parent, "Camera App");
            root.anchoredPosition = new Vector2(0f, -66f);
            root.sizeDelta = new Vector2(425f, 714f);
            Image view = CreatePanel(
                "Viewfinder",
                root,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(425f, 714f),
                new Color(0.012f, 0.018f, 0.026f, 1f),
                s_Rounded
            );
            Mask viewMask = view.gameObject.AddComponent<Mask>();
            viewMask.showMaskGraphic = true;
            Outline viewOutline = view.gameObject.AddComponent<Outline>();
            viewOutline.effectColor = new Color(0.16f, 0.68f, 0.67f, 0.62f);
            viewOutline.effectDistance = new Vector2(2f, -2f);
            for (int i = -1; i <= 1; i += 2)
            {
                CreateMapLine(view.rectTransform, new Vector2(i * 70f, 0f), new Vector2(702f, 2f), 90f, new Color(1f, 1f, 1f, 0.14f));
                CreateMapLine(view.rectTransform, new Vector2(0f, i * 119f), new Vector2(413f, 2f), 0f, new Color(1f, 1f, 1f, 0.14f));
            }
            Text rec = CreateText("Recording", view.transform, new Vector2(-164f, 319f), new Vector2(70f, 24f), 14, TextAnchor.MiddleLeft, FontStyle.Bold, MAGENTA);
            rec.text = "● REC";
            Text focus = CreateText("Focus", view.transform, Vector2.zero, new Vector2(80f, 50f), 26, TextAnchor.MiddleCenter, FontStyle.Bold, CYAN);
            focus.text = "[  +  ]";
            Image shutter = CreatePanel("Shutter", root, Center, Center, Center, new Vector2(0f, -295f), new Vector2(72f, 72f), Color.white, s_Knob);
            shutter.raycastTarget = true;
            Button shutterButton = shutter.gameObject.AddComponent<Button>();
            shutterButton.targetGraphic = shutter;
            Outline shutterOutline = shutter.gameObject.AddComponent<Outline>();
            shutterOutline.effectColor = CYAN;
            shutterOutline.effectDistance = new Vector2(4f, -4f);
            return root.gameObject;
        }

        private static GameObject BuildPhotosScreen(Transform parent)
        {
            RectTransform root = CreateAppScreen(parent, "Photos App");
            root.anchoredPosition = new Vector2(0f, -57f);
            root.sizeDelta = new Vector2(425f, 696f);
            Image rootImage = root.GetComponent<Image>();
            rootImage.color = new Color(0.012f, 0.018f, 0.026f, 1f);
            Mask rootMask = root.gameObject.AddComponent<Mask>();
            rootMask.showMaskGraphic = true;
            Outline rootOutline = root.gameObject.AddComponent<Outline>();
            rootOutline.effectColor = new Color(0.16f, 0.68f, 0.67f, 0.62f);
            rootOutline.effectDistance = new Vector2(2f, -2f);

            for (int i = 0; i < 9; ++i)
            {
                int column = i % 3;
                int row = i / 3;
                Image thumbnail = CreatePanel(
                    "Photo " + (i + 1),
                    root,
                    Center,
                    Center,
                    Center,
                    new Vector2(-137f + column * 137f, 218f - row * 218f),
                    new Vector2(118f, 193f),
                    new Color(0.015f, 0.022f, 0.030f, 1f),
                    s_Rounded
                );
                thumbnail.raycastTarget = true;
                Mask thumbnailMask = thumbnail.gameObject.AddComponent<Mask>();
                thumbnailMask.showMaskGraphic = true;
                Outline thumbnailOutline = thumbnail.gameObject.AddComponent<Outline>();
                thumbnailOutline.effectColor = new Color(1f, 1f, 1f, 0.18f);
                thumbnailOutline.effectDistance = new Vector2(1f, -1f);
                Button thumbnailButton = thumbnail.gameObject.AddComponent<Button>();
                thumbnailButton.targetGraphic = thumbnail;
                thumbnail.gameObject.SetActive(false);
            }
            return root.gameObject;
        }

        private static RectTransform CreateAppScreen(Transform parent, string name)
        {
            Image screen = CreatePanel(
                name,
                parent,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(410f, 590f),
                new Color(0.028f, 0.052f, 0.078f, 1f),
                s_Rounded
            );
            return screen.rectTransform;
        }

        private static void CreateSectionTitle(RectTransform parent, string title, Color color)
        {
            Text text = CreateText("Section Title", parent, new Vector2(-82f, 226f), new Vector2(180f, 28f), 18, TextAnchor.MiddleLeft, FontStyle.Bold, color);
            text.text = title;
            CreatePanel("Section Rule", parent, Center, Center, Center, new Vector2(0f, 204f), new Vector2(330f, 3f), new Color(color.r, color.g, color.b, 0.62f), s_Rounded);
        }

        private static void CreateListRow(RectTransform parent, float y, string title, string subtitle, Color accent, string avatar)
        {
            Image row = CreatePanel("Row " + title, parent, Center, Center, Center, new Vector2(0f, y), new Vector2(334f, 70f), PANEL, s_Rounded);
            Image avatarPanel = CreatePanel("Avatar", row.transform, Center, Center, Center, new Vector2(-134f, 0f), new Vector2(46f, 46f), new Color(accent.r, accent.g, accent.b, 0.22f), s_Knob);
            Text avatarText = CreateText("Initial", avatarPanel.transform, Vector2.zero, new Vector2(46f, 46f), 21, TextAnchor.MiddleCenter, FontStyle.Bold, accent);
            avatarText.text = avatar;
            Text titleText = CreateText("Title", row.transform, new Vector2(12f, 11f), new Vector2(220f, 24f), 17, TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            titleText.text = title;
            Text subtitleText = CreateText("Subtitle", row.transform, new Vector2(12f, -14f), new Vector2(220f, 22f), 12, TextAnchor.MiddleLeft, FontStyle.Normal, MUTED);
            subtitleText.text = subtitle;
            CreatePanel("Accent", row.transform, Center, Center, Center, new Vector2(164f, 0f), new Vector2(3f, 48f), accent, s_Rounded);
        }

        private static void CreateNote(RectTransform parent, float y, string title, string body, Color accent)
        {
            Image note = CreatePanel("Note " + title, parent, Center, Center, Center, new Vector2(0f, y), new Vector2(334f, 84f), PANEL, s_Rounded);
            CreatePanel("Note Accent", note.transform, Center, Center, Center, new Vector2(-164f, 0f), new Vector2(4f, 58f), accent, s_Rounded);
            Text titleText = CreateText("Title", note.transform, new Vector2(-6f, 17f), new Vector2(292f, 24f), 17, TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            titleText.text = title;
            Text bodyText = CreateText("Body", note.transform, new Vector2(-6f, -14f), new Vector2(292f, 28f), 13, TextAnchor.MiddleLeft, FontStyle.Normal, MUTED);
            bodyText.text = body;
        }

        private static void CreateActionCard(RectTransform parent, float y, string title, string action, Color accent)
        {
            Image card = CreatePanel("Action", parent, Center, Center, Center, new Vector2(0f, y), new Vector2(334f, 70f), new Color(accent.r * 0.16f, accent.g * 0.16f, accent.b * 0.16f, 1f), s_Rounded);
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.52f);
            outline.effectDistance = new Vector2(2f, -2f);
            Text titleText = CreateText("Title", card.transform, new Vector2(-43f, 0f), new Vector2(210f, 40f), 16, TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            titleText.text = title;
            Text actionText = CreateText("Action Label", card.transform, new Vector2(116f, 0f), new Vector2(74f, 36f), 12, TextAnchor.MiddleCenter, FontStyle.Bold, accent);
            actionText.text = action;
        }

        private static Button CreateAppButton(Transform parent, string label, Vector2 position, Sprite sprite)
        {
            RectTransform cell = CreateRect("App " + label, parent, Center, Center, Center, position, new Vector2(176f, 158f));
            Image icon = CreatePanel("Icon", cell, Center, Center, Center, new Vector2(0f, 20f), new Vector2(112f, 112f), Color.white, sprite);
            icon.preserveAspect = true;
            icon.raycastTarget = true;
            Button button = icon.gameObject.AddComponent<Button>();
            button.targetGraphic = icon;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.92f, 0.78f, 1f);
            colors.pressedColor = new Color(0.76f, 0.78f, 0.72f, 1f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Text appLabel = CreateText("Label", cell, new Vector2(0f, -52f), new Vector2(174f, 32f), 18, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            appLabel.text = label;
            return button;
        }

        private static Button CreateTextButton(string name, Transform parent, Vector2 position, Vector2 size, string label, int fontSize, Color background, Color foreground)
        {
            Image image = CreatePanel(name, parent, Center, Center, Center, position, size, background, s_Rounded);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
            colors.pressedColor = new Color(0.7f, 0.84f, 0.92f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Text text = CreateText("Label", image.transform, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, FontStyle.Bold, foreground);
            text.text = label;
            return button;
        }

        private static void CreateMapLine(Transform parent, Vector2 position, Vector2 size, float rotation, Color color)
        {
            Image line = CreatePanel("Map Line", parent, Center, Center, Center, position, size, color, s_Rounded);
            line.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
        }

        private static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor, FontStyle style, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Center;
            rect.pivot = Center;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = gameObject.GetComponent<Text>();
            text.font = s_Font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.resizeTextForBestFit = false;
            return text;
        }

        private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color, Sprite sprite)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite == s_Rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateStretchPanel(string name, Transform parent, Color color, Sprite sprite)
        {
            RectTransform rect = CreateStretchRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite == s_Rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(ROOT))
                AssetDatabase.CreateFolder("Assets", "PhoneSystem");
            if (!AssetDatabase.IsValidFolder(ROOT + "/Resources"))
                AssetDatabase.CreateFolder(ROOT, "Resources");
            if (!AssetDatabase.IsValidFolder(ROOT + "/Resources/Icons"))
                AssetDatabase.CreateFolder(ROOT + "/Resources", "Icons");
        }

        private static AudioClip LoadAudio(string fileName)
        {
            string path = AUDIO_ROOT + fileName + ".ogg";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                throw new InvalidOperationException("Phone audio is missing: " + path);
            return clip;
        }

        private static Vector2 Center => new(0.5f, 0.5f);
    }
}
