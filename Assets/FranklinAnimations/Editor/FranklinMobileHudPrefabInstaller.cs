using FranklinGame.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.UI.Editor
{
    /// <summary>
    /// Installs the Franklin touch buttons into the existing Tactile player canvas so their
    /// hierarchy and layout are directly editable in CanvasPlayerControl.prefab.
    /// </summary>
    [InitializeOnLoad]
    public static class FranklinMobileHudPrefabInstaller
    {
        private const string PLAYER_CANVAS_PATH = "Assets/Prefab/CanvasPlayerControl.prefab";
        private const string UI_ROOT = "Assets/UI/FranklinMobile/Resources/FranklinMobileUI/";
        private const string VEHICLE_GAUGE_PATH =
            "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Textures/UI/Generated/VehicleFuelArc.png";
        private const string VEHICLE_GAUGE_MATERIAL_PATH =
            "Assets/FranklinAnimations/Generated/FranklinBikeGaugeAlphaTint.mat";
        // Alpha-only tint keeps the curved sprite's antialiased edge intact.
        private const string VEHICLE_GAUGE_SHADER = "UI/Franklin Alpha Tint";
        private const string HUD_FONT_PATH =
            "Assets/Plugins/GameCreator/Installs/GameCreator.Blockout@1.6.12/UI/_Fonts/JosefinSans-Bold.ttf";
        private const string PLAYER_HUD_ASSET_ROOT =
            "Assets/UI/FranklinPlayerHud/Resources/FranklinPlayerHud/";
        private const string ON_FOOT_GROUP = "Franklin On Foot Controls";
        private const string VEHICLE_GROUP = "Franklin Vehicle Controls";
        private const string PLAYER_STATUS_HUD = "Franklin Player Status HUD";
        private const string BIKE_HEALTH_UI = "Bike Health UI";
        private const string BIKE_FUEL_UI = "Bike Fuel UI";
        private const string BIKE_SPEED_UI = "Bike Speed UI";

        private static readonly Color PLAYER_HUD_TEXT =
            new(0.96f, 0.98f, 1f, 1f);
        private static readonly Color PLAYER_HUD_MUTED =
            new(0.58f, 0.64f, 0.68f, 1f);
        private static readonly Color PLAYER_HUD_MINT =
            new(0.22f, 0.91f, 0.72f, 1f);
        private static readonly Color PLAYER_HUD_HEALTH =
            new(0.95f, 0.08f, 0.2f, 1f);
        private static readonly Vector2 STATUS_MAP_POSITION =
            new(160f, -160f);
        private static readonly Vector2 STATUS_HEALTH_POSITION =
            new(550f, -84f);
        private static readonly Vector2 STATUS_MONEY_POSITION =
            new(431.25f, -158f);
        private static readonly Vector3 STATUS_MONEY_SCALE =
            new(0.525f, 0.625f, 1f);
        private static readonly Vector2 STATUS_WEAPON_POSITION =
            new(-255f, -83f);
        private static readonly Vector2 STATUS_UNARMED_WEAPON_POSITION =
            new(-170f, -83f);
        private static readonly Vector2 STATUS_QUICK_RAIL_POSITION =
            new(-235f, -162f);
        private const int MONEY_VALUE_FONT_SIZE = 46;
        private const float QUICK_ARMOR_X = -130f;
        private const float QUICK_GRENADE_X = 0f;
        private const float QUICK_MOLOTOV_X = 130f;
        private const float QUICK_LEFT_DIVIDER_X = -65f;
        private const float QUICK_RIGHT_DIVIDER_X = 65f;

        private readonly struct ButtonDefinition
        {
            public readonly string Name;
            public readonly string SpriteName;
            public readonly int Action;
            public readonly Vector2 Anchor;
            public readonly Vector2 Position;
            public readonly Vector2 Size;
            public readonly bool IsToggle;

            public ButtonDefinition(
                string name,
                string spriteName,
                int action,
                Vector2 anchor,
                Vector2 position,
                Vector2 size,
                bool isToggle = false)
            {
                this.Name = name;
                this.SpriteName = spriteName;
                this.Action = action;
                this.Anchor = anchor;
                this.Position = position;
                this.Size = size;
                this.IsToggle = isToggle;
            }
        }

        private static readonly ButtonDefinition[] ON_FOOT_BUTTONS =
        {
            new("Jog", "player-movement-0", 0, new Vector2(0f, 0f),
                new Vector2(369.2f, 568.43f), new Vector2(165f, 165f)),
            new("Sprint", "player-movement-1", 1, new Vector2(0f, 0f),
                new Vector2(173.4f, 561.13f), new Vector2(165f, 165f)),
            new("Jump", "player-jump", 8, new Vector2(1f, 0f),
                new Vector2(-505f, 170f), new Vector2(165f, 165f)),
            new("Enter Vehicle", "vehicle-enter", 7, new Vector2(0.72f, 0.4f),
                Vector2.zero, new Vector2(190f, 190f)),
            new("Bike Helmet On Foot", "vehicle-control-helmet", 16,
                new Vector2(1f, 0f), new Vector2(-690f, 190f),
                new Vector2(165f, 165f))
        };

        private static readonly ButtonDefinition[] VEHICLE_BUTTONS =
        {
            new("Steer Left", "vehicle-control-0", 2, new Vector2(0f, 0f),
                new Vector2(180f, 190f), new Vector2(263f, 263f)),
            new("Steer Right", "vehicle-control-1", 3, new Vector2(0f, 0f),
                new Vector2(450f, 190f), new Vector2(263f, 263f)),
            new("Accelerate", "vehicle-control-2", 4, new Vector2(1f, 0f),
                new Vector2(-155f, 305f), new Vector2(288f, 288f)),
            new("Brake Reverse", "vehicle-control-3", 5, new Vector2(1f, 0f),
                new Vector2(-455f, 190f), new Vector2(225f, 225f)),
            new("Handbrake", "vehicle-control-4", 6, new Vector2(1f, 0f),
                new Vector2(-455f, 430f), new Vector2(169f, 169f)),
            new("Exit Vehicle", "vehicle-control-5", 7, new Vector2(1f, 1f),
                new Vector2(-125f, -385f), new Vector2(170f, 170f)),
            new("Slow Drive", "vehicle-control-slow", 9, new Vector2(1f, 0f),
                new Vector2(-155f, 82f), new Vector2(163f, 163f)),
            new("Bike Headlight", "vehicle-control-headlight", 10, new Vector2(1f, 1f),
                new Vector2(-115f, -575f), new Vector2(155f, 155f), true),
            new("Bike Wheelie", "vehicle-control-wheelie", 11, new Vector2(1f, 1f),
                new Vector2(-285f, -575f), new Vector2(155f, 155f)),
            new("Bike Burnout", "vehicle-control-burnout", 12, new Vector2(1f, 1f),
                new Vector2(-455f, -575f), new Vector2(155f, 155f)),
            new("Bike Helmet", "vehicle-control-helmet", 16, new Vector2(1f, 0f),
                new Vector2(-690f, 190f), new Vector2(165f, 165f))
        };

        static FranklinMobileHudPrefabInstaller()
        {
            EditorApplication.delayCall += InstallIfNeeded;
        }

        [MenuItem("Tools/Franklin Game/Install Player Mobile HUD")]
        public static void Install()
        {
            EnsureSpriteImporter(UI_ROOT + "vehicle-control-slow.png");
            EnsureSpriteImporter(UI_ROOT + "vehicle-control-headlight.png");
            EnsureSpriteImporter(UI_ROOT + "vehicle-control-wheelie.png");
            EnsureSpriteImporter(UI_ROOT + "vehicle-control-burnout.png");
            EnsureSpriteImporter(UI_ROOT + "vehicle-control-helmet.png");
            EnsureSpriteImporter(VEHICLE_GAUGE_PATH);
            EnsurePlayerHudSpriteImporters();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"Franklin mobile HUD could not find {PLAYER_CANVAS_PATH}");
                return;
            }

            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(PLAYER_CANVAS_PATH);
            try
            {
                FranklinMobileHud hud = canvasRoot.GetComponent<FranklinMobileHud>() ??
                    canvasRoot.AddComponent<FranklinMobileHud>();

                RectTransform onFoot = EnsureGroup(canvasRoot.transform, ON_FOOT_GROUP);
                RectTransform vehicle = EnsureGroup(canvasRoot.transform, VEHICLE_GROUP);
                Sprite vehicleGauge = AssetDatabase.LoadAssetAtPath<Sprite>(VEHICLE_GAUGE_PATH);
                Material vehicleGaugeMaterial = EnsureGaugeMaterial();
                Font hudFont = AssetDatabase.LoadAssetAtPath<Font>(HUD_FONT_PATH);

                foreach (ButtonDefinition button in ON_FOOT_BUTTONS)
                {
                    EnsureButton(onFoot, button);
                }
                foreach (ButtonDefinition button in VEHICLE_BUTTONS)
                {
                    EnsureButton(vehicle, button);
                }
                EnsureBikeSpeedUi(vehicle, hudFont);
                EnsureBikeHealthUi(vehicle, vehicleGauge, vehicleGaugeMaterial);
                EnsureBikeFuelUi(vehicle, vehicleGauge, hudFont);
                EnsurePlayerStatusHud(canvasRoot.transform, hudFont);

                SerializedObject serializedHud = new SerializedObject(hud);
                serializedHud.FindProperty("m_BikeGaugeSprite").objectReferenceValue =
                    vehicleGauge;
                serializedHud.FindProperty("m_BikeGaugeAlphaTintMaterial").objectReferenceValue =
                    vehicleGaugeMaterial;
                serializedHud.FindProperty("m_BikeSpeedFont").objectReferenceValue = hudFont;
                serializedHud.ApplyModifiedPropertiesWithoutUndo();

                onFoot.gameObject.SetActive(true);
                FindDirectChild(onFoot, "Enter Vehicle")?.gameObject.SetActive(false);
                FindDirectChild(onFoot, "Bike Helmet On Foot")?.gameObject.SetActive(false);
                vehicle.gameObject.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(canvasRoot, PLAYER_CANVAS_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvasRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Franklin mobile HUD is now embedded in CanvasPlayerControl.prefab.");
        }

        [MenuItem("Tools/Franklin Game/Add Phone Button Only")]
        public static void InstallPhoneButtonOnly()
        {
            InstallMapUtilityButtons(true);
        }

        [MenuItem("Tools/Franklin Game/Add Map Utility Buttons Only")]
        public static void InstallMapUtilityButtonsOnly()
        {
            InstallMapUtilityButtons(false);
        }

        [MenuItem("Tools/Franklin Game/Arrange Top Left Player Status Only")]
        public static void ArrangeTopLeftPlayerStatusOnly()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"Franklin status layout could not find {PLAYER_CANVAS_PATH}");
                return;
            }

            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(PLAYER_CANVAS_PATH);
            try
            {
                Transform statusHud = FindDirectChild(canvasRoot.transform, PLAYER_STATUS_HUD);
                RectTransform moneyCard =
                    FindDirectChild(statusHud, "Money Card") as RectTransform;
                RectTransform healthCard =
                    FindDirectChild(statusHud, "Health Card") as RectTransform;
                RectTransform mapMask =
                    FindDirectChild(statusHud, "Mini Map Mask") as RectTransform;
                RectTransform mapFrame =
                    FindDirectChild(statusHud, "Mini Map Frame") as RectTransform;
                if (moneyCard == null || healthCard == null || mapMask == null ||
                    mapFrame == null)
                {
                    Debug.LogWarning("Franklin status layout is missing a required HUD element.");
                    return;
                }

                ConfigureHudCard(
                    healthCard,
                    new Vector2(0f, 1f),
                    STATUS_HEALTH_POSITION,
                    healthCard.sizeDelta
                );
                ConfigureHudCard(
                    moneyCard,
                    new Vector2(0f, 1f),
                    STATUS_MONEY_POSITION,
                    healthCard.sizeDelta
                );
                moneyCard.localScale = STATUS_MONEY_SCALE;
                ConfigureRect(
                    mapMask,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0.5f, 0.5f),
                    STATUS_MAP_POSITION,
                    mapMask.sizeDelta
                );
                ConfigureRect(
                    mapFrame,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0.5f, 0.5f),
                    STATUS_MAP_POSITION,
                    mapFrame.sizeDelta
                );

                PrefabUtility.SaveAsPrefabAsset(canvasRoot, PLAYER_CANVAS_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvasRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Franklin map/health/money layout was updated without changing other HUD elements."
            );
        }

        [MenuItem("Tools/Franklin Game/Adjust Money And Weapon Only")]
        public static void AdjustMoneyAndWeaponOnly()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"Franklin HUD adjustment could not find {PLAYER_CANVAS_PATH}");
                return;
            }

            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(PLAYER_CANVAS_PATH);
            try
            {
                Transform statusHud = FindDirectChild(canvasRoot.transform, PLAYER_STATUS_HUD);
                RectTransform moneyCard =
                    FindDirectChild(statusHud, "Money Card") as RectTransform;
                RectTransform weaponCard =
                    FindDirectChild(statusHud, "Weapon Card") as RectTransform;
                Text moneyValue = FindDirectChild(moneyCard, "Money Value")?.GetComponent<Text>();
                if (moneyCard == null || weaponCard == null || moneyValue == null)
                {
                    Debug.LogWarning("Franklin HUD adjustment is missing Money or Weapon UI.");
                    return;
                }

                FranklinPlayerStatusHud statusComponent =
                    statusHud.GetComponent<FranklinPlayerStatusHud>();
                if (statusComponent == null)
                {
                    Debug.LogWarning("Franklin HUD adjustment is missing its status component.");
                    return;
                }
                SerializedObject serializedStatus = new SerializedObject(statusComponent);
                serializedStatus.FindProperty("m_ArmedCardPosition").vector2Value =
                    STATUS_WEAPON_POSITION;
                serializedStatus.FindProperty("m_UnarmedCardPosition").vector2Value =
                    STATUS_UNARMED_WEAPON_POSITION;
                serializedStatus.ApplyModifiedPropertiesWithoutUndo();

                moneyCard.anchoredPosition = STATUS_MONEY_POSITION;
                moneyCard.localScale = STATUS_MONEY_SCALE;
                moneyValue.text = "12,480";
                ConfigureHudCard(
                    weaponCard,
                    Vector2.one,
                    STATUS_WEAPON_POSITION,
                    weaponCard.sizeDelta
                );

                PrefabUtility.SaveAsPrefabAsset(canvasRoot, PLAYER_CANVAS_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvasRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Franklin Money size/value and Weapon position were updated without changing other HUD elements."
            );
        }

        [MenuItem("Tools/Franklin Game/Add Armor Display Only")]
        public static void AddArmorDisplayOnly()
        {
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "armor.png");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"Franklin Armor display could not find {PLAYER_CANVAS_PATH}");
                return;
            }

            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(PLAYER_CANVAS_PATH);
            try
            {
                Transform statusHud = FindDirectChild(canvasRoot.transform, PLAYER_STATUS_HUD);
                RectTransform quickRail =
                    FindDirectChild(statusHud, "Quick Item Rail") as RectTransform;
                if (quickRail == null)
                {
                    Debug.LogWarning("Franklin Armor display could not find Quick Item Rail.");
                    return;
                }

                quickRail.sizeDelta = new Vector2(390f, quickRail.sizeDelta.y);
                Sprite solid = LoadHudSprite("ui-solid.png");
                Image armorDivider = EnsureHudImage(
                    quickRail,
                    "Quick Item Divider Armor",
                    solid,
                    new Color(0.36f, 0.4f, 0.43f, 0.38f)
                );
                ConfigureCenteredRect(
                    armorDivider.rectTransform,
                    new Vector2(QUICK_LEFT_DIVIDER_X, 0f),
                    new Vector2(2f, 46f)
                );
                EnsureArmorDisplay(
                    quickRail,
                    solid,
                    LoadHudSprite("armor.png"),
                    AssetDatabase.LoadAssetAtPath<Font>(HUD_FONT_PATH)
                );
                RectTransform grenade =
                    FindDirectChild(quickRail, "Grenade Button") as RectTransform;
                RectTransform molotov =
                    FindDirectChild(quickRail, "Molotov Button") as RectTransform;
                RectTransform rightDivider =
                    FindDirectChild(quickRail, "Quick Item Divider") as RectTransform;
                if (grenade != null)
                    grenade.anchoredPosition = new Vector2(QUICK_GRENADE_X, 0f);
                if (molotov != null)
                    molotov.anchoredPosition = new Vector2(QUICK_MOLOTOV_X, 0f);
                if (rightDivider != null)
                    rightDivider.anchoredPosition =
                        new Vector2(QUICK_RIGHT_DIVIDER_X, 0f);

                PrefabUtility.SaveAsPrefabAsset(canvasRoot, PLAYER_CANVAS_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvasRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Franklin Armor display was added and all three quick slots were distributed evenly."
            );
        }

        [MenuItem("Tools/Franklin Game/Align Quick Items With Weapon Only")]
        public static void AlignQuickItemsWithWeaponOnly()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"Franklin quick items could not find {PLAYER_CANVAS_PATH}");
                return;
            }

            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(PLAYER_CANVAS_PATH);
            try
            {
                Transform statusHud = FindDirectChild(canvasRoot.transform, PLAYER_STATUS_HUD);
                RectTransform quickRail =
                    FindDirectChild(statusHud, "Quick Item Rail") as RectTransform;
                if (quickRail == null)
                {
                    Debug.LogWarning("Franklin quick items could not find Quick Item Rail.");
                    return;
                }

                quickRail.anchoredPosition = STATUS_QUICK_RAIL_POSITION;
                PrefabUtility.SaveAsPrefabAsset(canvasRoot, PLAYER_CANVAS_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvasRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Franklin Armor/Grenade/Molotov rail is aligned below the Weapon card."
            );
        }

        [MenuItem("Tools/Franklin Game/Increase Money Text Only")]
        public static void IncreaseMoneyTextOnly()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"Franklin Money text could not find {PLAYER_CANVAS_PATH}");
                return;
            }

            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(PLAYER_CANVAS_PATH);
            try
            {
                Transform statusHud = FindDirectChild(canvasRoot.transform, PLAYER_STATUS_HUD);
                Transform moneyCard = FindDirectChild(statusHud, "Money Card");
                Text moneyValue = FindDirectChild(moneyCard, "Money Value")?.GetComponent<Text>();
                if (moneyValue == null)
                {
                    Debug.LogWarning("Franklin Money text could not find Money Value.");
                    return;
                }

                moneyValue.fontSize = MONEY_VALUE_FONT_SIZE;
                PrefabUtility.SaveAsPrefabAsset(canvasRoot, PLAYER_CANVAS_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvasRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Franklin Money value font size was increased to 46 only.");
        }

        [MenuItem("Tools/Franklin Game/Distribute Quick Items Evenly Only")]
        public static void DistributeQuickItemsEvenlyOnly()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"Franklin quick items could not find {PLAYER_CANVAS_PATH}");
                return;
            }

            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(PLAYER_CANVAS_PATH);
            try
            {
                Transform statusHud = FindDirectChild(canvasRoot.transform, PLAYER_STATUS_HUD);
                Transform quickRail = FindDirectChild(statusHud, "Quick Item Rail");
                RectTransform armor = FindDirectChild(quickRail, "Armor Display") as RectTransform;
                RectTransform grenade =
                    FindDirectChild(quickRail, "Grenade Button") as RectTransform;
                RectTransform molotov =
                    FindDirectChild(quickRail, "Molotov Button") as RectTransform;
                RectTransform leftDivider =
                    FindDirectChild(quickRail, "Quick Item Divider Armor") as RectTransform;
                RectTransform rightDivider =
                    FindDirectChild(quickRail, "Quick Item Divider") as RectTransform;
                if (armor == null || grenade == null || molotov == null ||
                    leftDivider == null || rightDivider == null)
                {
                    Debug.LogWarning("Franklin quick items are missing a slot or divider.");
                    return;
                }

                armor.anchoredPosition = new Vector2(QUICK_ARMOR_X, 0f);
                grenade.anchoredPosition = new Vector2(QUICK_GRENADE_X, 0f);
                molotov.anchoredPosition = new Vector2(QUICK_MOLOTOV_X, 0f);
                leftDivider.anchoredPosition = new Vector2(QUICK_LEFT_DIVIDER_X, 0f);
                rightDivider.anchoredPosition = new Vector2(QUICK_RIGHT_DIVIDER_X, 0f);
                PrefabUtility.SaveAsPrefabAsset(canvasRoot, PLAYER_CANVAS_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvasRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Franklin Armor/Grenade/Molotov slots were distributed evenly.");
        }

        private static void InstallMapUtilityButtons(bool phoneOnly)
        {
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "phone-button.png");
            if (!phoneOnly)
            {
                EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "home-button.png");
                EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "settings-button.png");
                EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "compass-ns.png");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"Franklin phone button could not find {PLAYER_CANVAS_PATH}");
                return;
            }

            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(PLAYER_CANVAS_PATH);
            try
            {
                Transform statusHud = FindDirectChild(canvasRoot.transform, PLAYER_STATUS_HUD);
                Transform mapFrame = statusHud != null
                    ? FindDirectChild(statusHud, "Mini Map Frame")
                    : null;
                if (mapFrame == null)
                {
                    Debug.LogWarning("Franklin phone button could not find the Mini Map Frame.");
                    return;
                }

                EnsureMapUtilityButton(
                    mapFrame,
                    "Phone Button",
                    LoadHudSprite("phone-button.png"),
                    new Vector2(-86f, -86f),
                    13
                );
                if (!phoneOnly)
                {
                    MigrateMenuButtonToHome(mapFrame);
                    EnsureMapUtilityButton(
                        mapFrame,
                        "Home Button",
                        LoadHudSprite("home-button.png"),
                        new Vector2(86f, 86f),
                        14
                    );
                    EnsureMapUtilityButton(
                        mapFrame,
                        "Settings Button",
                        LoadHudSprite("settings-button.png"),
                        new Vector2(86f, -86f),
                        15
                    );
                    EnsureCompassIndicator(
                        mapFrame,
                        LoadHudSprite("compass-ns.png")
                    );
                }
                PrefabUtility.SaveAsPrefabAsset(canvasRoot, PLAYER_CANVAS_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvasRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(phoneOnly
                ? "Franklin phone button was updated without changing its position or size."
                : "Franklin map utility buttons were added without changing existing HUD positions or sizes."
            );
        }

        private static void InstallIfNeeded()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += InstallIfNeeded;
                return;
            }

            GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (canvas == null || HasInstalledLayout(canvas)) return;

            Install();
        }

        private static bool HasInstalledLayout(GameObject canvas)
        {
            Transform root = canvas.transform;
            return canvas.GetComponent<FranklinMobileHud>() != null &&
                   root.Find(ON_FOOT_GROUP) != null &&
                   root.Find(VEHICLE_GROUP) != null &&
                   HasButton(root.Find(ON_FOOT_GROUP), "Jump") &&
                   HasButton(root.Find(ON_FOOT_GROUP), "Enter Vehicle") &&
                   HasButton(root.Find(ON_FOOT_GROUP), "Bike Helmet On Foot") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Exit Vehicle") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Slow Drive") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Bike Headlight") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Bike Wheelie") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Bike Burnout") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Bike Helmet") &&
                   HasBikeHealthUi(root.Find(VEHICLE_GROUP)) &&
                   HasBikeFuelUi(root.Find(VEHICLE_GROUP)) &&
                   HasBikeSpeedUi(root.Find(VEHICLE_GROUP)) &&
                   HasPlayerStatusHud(root);
        }

        private static void EnsurePlayerHudSpriteImporters()
        {
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "hud-card-frame.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "mini-map-generated.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "mini-map-mask.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "mini-map-ring.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "heart.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "weapon-pistol.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "weapon-fists.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "phone-button.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "home-button.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "settings-button.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "compass-ns.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "player-marker.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "waypoint-dot.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "ui-solid.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "hud-square-frame.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "grenade.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "molotov.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "armor.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "hud-panel-urban-v2.png");
            EnsureSpriteImporter(PLAYER_HUD_ASSET_ROOT + "hud-quick-rail-urban-v2.png");
        }

        private static void EnsureSpriteImporter(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) return;
            if (importer.textureType == TextureImporterType.Sprite &&
                importer.alphaIsTransparency && !importer.mipmapEnabled)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static RectTransform EnsureGroup(Transform parent, string groupName)
        {
            Transform existing = parent.Find(groupName);
            RectTransform group = existing as RectTransform;
            if (group == null)
            {
                GameObject groupObject = new GameObject(groupName, typeof(RectTransform));
                groupObject.layer = 5;
                group = groupObject.GetComponent<RectTransform>();
                group.SetParent(parent, false);
            }

            group.anchorMin = Vector2.zero;
            group.anchorMax = Vector2.one;
            group.offsetMin = Vector2.zero;
            group.offsetMax = Vector2.zero;
            return group;
        }

        private static void EnsureButton(RectTransform parent, ButtonDefinition definition)
        {
            Transform existing = FindDirectChild(parent, definition.Name);
            GameObject buttonObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    definition.Name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(FranklinHudButton)
                );
            buttonObject.layer = 5;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = definition.Anchor;
            rect.anchorMax = definition.Anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = definition.Position;
            rect.sizeDelta = definition.Size;

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                UI_ROOT + definition.SpriteName + ".png"
            );
            image.preserveAspect = true;
            image.raycastTarget = true;

            FranklinHudButton hudButton = buttonObject.GetComponent<FranklinHudButton>();
            if (hudButton == null)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(buttonObject);
                hudButton = buttonObject.AddComponent<FranklinHudButton>();
            }
            SerializedObject serializedButton = new SerializedObject(hudButton);
            serializedButton.FindProperty("m_Action").intValue = definition.Action;
            serializedButton.FindProperty("m_Image").objectReferenceValue = image;
            serializedButton.FindProperty("m_IsToggle").boolValue = definition.IsToggle;
            serializedButton.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material EnsureGaugeMaterial()
        {
            Shader shader = Shader.Find(VEHICLE_GAUGE_SHADER);
            if (shader == null)
            {
                Debug.LogError($"Missing Bike UI shader: {VEHICLE_GAUGE_SHADER}");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                VEHICLE_GAUGE_MATERIAL_PATH
            );
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "Franklin Bike Gauge Alpha Tint"
                };
                AssetDatabase.CreateAsset(material, VEHICLE_GAUGE_MATERIAL_PATH);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureBikeHealthUi(
            RectTransform parent,
            Sprite gaugeSprite,
            Material gaugeMaterial)
        {
            RectTransform healthRoot = RebuildGaugeRoot(
                parent,
                BIKE_HEALTH_UI,
                new Vector2(104f, 230f)
            );
            Image fill = CreateGaugeImage(
                healthRoot,
                "Health Arc Fill",
                new Vector2(18f, -23f),
                new Vector2(40f, 184f),
                gaugeSprite,
                new Color(0.16f, 0.72f, 1f, 1f)
            );
            fill.material = gaugeMaterial;
            fill.preserveAspect = false;
            fill.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            ConfigureVerticalFill(fill);

            RectTransform icon = CreateGaugeRect(
                healthRoot,
                "Bike Health Icon",
                new Vector2(18f, 91f),
                new Vector2(34f, 34f)
            );
            CreateGaugeImage(
                icon,
                "Health Icon Vertical",
                Vector2.zero,
                new Vector2(9f, 30f),
                null,
                new Color(0.16f, 0.72f, 1f, 1f)
            );
            CreateGaugeImage(
                icon,
                "Health Icon Horizontal",
                Vector2.zero,
                new Vector2(30f, 9f),
                null,
                new Color(0.16f, 0.72f, 1f, 1f)
            );
            healthRoot.gameObject.SetActive(false);
        }

        private static void EnsureBikeFuelUi(
            RectTransform parent,
            Sprite gaugeSprite,
            Font hudFont)
        {
            RectTransform fuelRoot = RebuildGaugeRoot(
                parent,
                BIKE_FUEL_UI,
                new Vector2(74f, 152f)
            );
            Image fill = CreateGaugeImage(
                fuelRoot,
                "Fuel Arc Fill",
                Vector2.zero,
                new Vector2(74f, 152f),
                gaugeSprite,
                Color.white
            );
            fill.preserveAspect = true;
            ConfigureVerticalFill(fill);
            CreateGaugeLabel(
                fuelRoot,
                "Full",
                "F",
                new Vector2(29f, 59f),
                new Color(1f, 1f, 1f, 0.78f),
                hudFont
            );
            CreateGaugeLabel(
                fuelRoot,
                "Empty",
                "E",
                new Vector2(29f, -59f),
                new Color(1f, 1f, 1f, 0.62f),
                hudFont
            );
            fuelRoot.gameObject.SetActive(false);
        }

        private static RectTransform RebuildGaugeRoot(
            RectTransform parent,
            string objectName,
            Vector2 size)
        {
            Transform existing = FindDirectChild(parent, objectName);
            GameObject rootObject = existing != null
                ? existing.gameObject
                : new GameObject(objectName, typeof(RectTransform));
            rootObject.layer = 5;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = size;
            for (int i = root.childCount - 1; i >= 0; --i)
                Object.DestroyImmediate(root.GetChild(i).gameObject);
            return root;
        }

        private static RectTransform CreateGaugeRect(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 size)
        {
            GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
            rectObject.layer = 5;
            RectTransform rect = rectObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateGaugeImage(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 size,
            Sprite sprite,
            Color color)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            imageObject.layer = 5;
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            return image;
        }

        private static void ConfigureVerticalFill(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Vertical;
            image.fillOrigin = (int)Image.OriginVertical.Bottom;
            image.fillClockwise = true;
            image.fillAmount = 1f;
        }

        private static void CreateGaugeLabel(
            Transform parent,
            string objectName,
            string value,
            Vector2 position,
            Color color,
            Font font)
        {
            GameObject labelObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            labelObject.layer = 5;
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(24f, 22f);
            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = 15;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = value;
            label.raycastTarget = false;
        }

        private static bool HasBikeHealthUi(Transform vehicleGroup)
        {
            Transform root = FindDirectChild(vehicleGroup, BIKE_HEALTH_UI);
            Image fill = FindDirectChild(root, "Health Arc Fill")?.GetComponent<Image>();
            return root != null && fill != null &&
                   fill.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(VEHICLE_GAUGE_PATH) &&
                   fill.material ==
                   AssetDatabase.LoadAssetAtPath<Material>(VEHICLE_GAUGE_MATERIAL_PATH) &&
                   FindDirectChild(root, "Bike Health Icon") != null;
        }

        private static bool HasBikeFuelUi(Transform vehicleGroup)
        {
            Transform root = FindDirectChild(vehicleGroup, BIKE_FUEL_UI);
            Image fill = FindDirectChild(root, "Fuel Arc Fill")?.GetComponent<Image>();
            return root != null && fill != null &&
                   fill.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(VEHICLE_GAUGE_PATH) &&
                   FindDirectChild(root, "Full")?.GetComponent<Text>() != null &&
                   FindDirectChild(root, "Empty")?.GetComponent<Text>() != null;
        }

        private static void EnsureBikeSpeedUi(RectTransform parent, Font hudFont)
        {
            Transform existing = FindDirectChild(parent, BIKE_SPEED_UI);
            GameObject speedObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    BIKE_SPEED_UI,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text)
                );
            speedObject.layer = 5;

            RectTransform rect = speedObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(286f, 78f);

            Text text = speedObject.GetComponent<Text>() ?? speedObject.AddComponent<Text>();
            text.font = hudFont;
            text.fontSize = 56;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = "0<size=29> km/h</size>";

            Shadow shadow = speedObject.GetComponent<Shadow>() ??
                            speedObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.34f);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
            rect.SetAsLastSibling();
            speedObject.SetActive(false);
        }

        private static bool HasBikeSpeedUi(Transform vehicleGroup)
        {
            Transform speed = FindDirectChild(vehicleGroup, BIKE_SPEED_UI);
            return speed != null && speed.GetComponent<Text>() != null;
        }

        private static void EnsurePlayerStatusHud(Transform canvasRoot, Font hudFont)
        {
            RectTransform root = EnsureHudRect(canvasRoot, PLAYER_STATUS_HUD);
            ConfigureRect(
                root,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero
            );

            Sprite cardFrame = LoadHudSprite("hud-panel-urban-v2.png");
            Sprite miniMap = LoadHudSprite("mini-map-generated.png");
            Sprite miniMapMask = LoadHudSprite("mini-map-mask.png");
            Sprite miniMapRing = LoadHudSprite("mini-map-ring.png");
            Sprite heart = LoadHudSprite("heart.png");
            Sprite weapon = LoadHudSprite("weapon-pistol.png");
            Sprite fists = LoadHudSprite("weapon-fists.png");
            Sprite playerMarker = LoadHudSprite("player-marker.png");
            Sprite waypoint = LoadHudSprite("waypoint-dot.png");
            Sprite solid = LoadHudSprite("ui-solid.png");
            Sprite quickRailFrame = LoadHudSprite("hud-quick-rail-urban-v2.png");
            Sprite grenade = LoadHudSprite("grenade.png");
            Sprite molotov = LoadHudSprite("molotov.png");
            Sprite armor = LoadHudSprite("armor.png");
            Sprite phone = LoadHudSprite("phone-button.png");
            Sprite home = LoadHudSprite("home-button.png");
            Sprite settings = LoadHudSprite("settings-button.png");
            Sprite compass = LoadHudSprite("compass-ns.png");

            RectTransform moneyCard = EnsureHudImage(root, "Money Card", cardFrame, Color.white)
                .rectTransform;
            ConfigureHudCard(
                moneyCard,
                new Vector2(0f, 1f),
                STATUS_MONEY_POSITION,
                new Vector2(500f, 88f)
            );
            moneyCard.localScale = STATUS_MONEY_SCALE;
            Text moneyIcon = EnsureHudText(
                moneyCard,
                "Money Icon",
                hudFont,
                "$",
                42,
                TextAnchor.MiddleCenter,
                PLAYER_HUD_MINT
            );
            ConfigureCenteredRect(moneyIcon.rectTransform, new Vector2(-207f, 0f),
                new Vector2(54f, 66f));
            Text moneyText = EnsureHudText(
                moneyCard,
                "Money Value",
                hudFont,
                "12,480",
                MONEY_VALUE_FONT_SIZE,
                TextAnchor.MiddleLeft,
                PLAYER_HUD_TEXT
            );
            ConfigureCenteredRect(moneyText.rectTransform, new Vector2(30f, 0f),
                new Vector2(396f, 66f));

            RectTransform healthCard = EnsureHudImage(root, "Health Card", cardFrame, Color.white)
                .rectTransform;
            ConfigureHudCard(
                healthCard,
                new Vector2(0f, 1f),
                STATUS_HEALTH_POSITION,
                new Vector2(500f, 88f)
            );
            Image healthIcon = EnsureHudImage(healthCard, "Health Icon", heart, Color.white);
            healthIcon.preserveAspect = true;
            ConfigureCenteredRect(healthIcon.rectTransform, new Vector2(-207f, 0f),
                new Vector2(42f, 42f));

            Image healthTrack = EnsureHudImage(
                healthCard,
                "Health Track",
                solid,
                new Color(0.1f, 0.12f, 0.14f, 0.96f)
            );
            ConfigureCenteredRect(healthTrack.rectTransform, new Vector2(-24f, 0f),
                new Vector2(308f, 22f));
            Image healthFill = EnsureHudImage(
                healthCard,
                "Health Fill",
                solid,
                PLAYER_HUD_HEALTH
            );
            ConfigureCenteredRect(healthFill.rectTransform, new Vector2(-24f, 0f),
                new Vector2(308f, 22f));
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            healthFill.fillAmount = 0.78f;
            Text healthText = EnsureHudText(
                healthCard,
                "Health Value",
                hudFont,
                "78%",
                29,
                TextAnchor.MiddleCenter,
                PLAYER_HUD_TEXT
            );
            ConfigureCenteredRect(healthText.rectTransform, new Vector2(198f, 0f),
                new Vector2(72f, 60f));

            RectTransform weaponCard = EnsureHudImage(root, "Weapon Card", cardFrame, Color.white)
                .rectTransform;
            ConfigureHudCard(
                weaponCard,
                Vector2.one,
                STATUS_WEAPON_POSITION,
                new Vector2(430f, 86f)
            );
            Image weaponIcon = EnsureHudImage(weaponCard, "Weapon Icon", weapon, Color.white);
            weaponIcon.preserveAspect = true;
            ConfigureCenteredRect(weaponIcon.rectTransform, new Vector2(-169f, 0f),
                new Vector2(88f, 50f));
            Text weaponName = EnsureHudText(
                weaponCard,
                "Weapon Name",
                hudFont,
                "PISTOL",
                21,
                TextAnchor.MiddleLeft,
                PLAYER_HUD_MUTED
            );
            ConfigureCenteredRect(weaponName.rectTransform, new Vector2(-66f, 0f),
                new Vector2(122f, 54f));
            Text ammoText = EnsureHudText(
                weaponCard,
                "Ammo Value",
                hudFont,
                "12 / 48",
                32,
                TextAnchor.MiddleRight,
                PLAYER_HUD_TEXT
            );
            ConfigureCenteredRect(ammoText.rectTransform, new Vector2(124f, 0f),
                new Vector2(158f, 58f));

            DestroyDirectChild(root, "Grenade Button");
            DestroyDirectChild(root, "Molotov Button");
            Image quickRail = EnsureHudImage(
                root,
                "Quick Item Rail",
                quickRailFrame,
                Color.white
            );
            ConfigureRect(
                quickRail.rectTransform,
                Vector2.one,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                STATUS_QUICK_RAIL_POSITION,
                new Vector2(390f, 72f)
            );
            Image quickDivider = EnsureHudImage(
                quickRail.rectTransform,
                "Quick Item Divider",
                solid,
                new Color(0.36f, 0.4f, 0.43f, 0.38f)
            );
            ConfigureCenteredRect(quickDivider.rectTransform,
                new Vector2(QUICK_RIGHT_DIVIDER_X, 0f),
                new Vector2(2f, 46f));
            Image armorDivider = EnsureHudImage(
                quickRail.rectTransform,
                "Quick Item Divider Armor",
                solid,
                new Color(0.36f, 0.4f, 0.43f, 0.38f)
            );
            ConfigureCenteredRect(armorDivider.rectTransform,
                new Vector2(QUICK_LEFT_DIVIDER_X, 0f),
                new Vector2(2f, 46f));
            EnsureArmorDisplay(
                quickRail.rectTransform,
                solid,
                armor,
                hudFont
            );

            Button grenadeButton = EnsureQuickItemButton(
                quickRail.rectTransform,
                "Grenade Button",
                solid,
                grenade,
                hudFont,
                "3",
                new Vector2(QUICK_GRENADE_X, 0f),
                out Image grenadeSelection
            );
            Button molotovButton = EnsureQuickItemButton(
                quickRail.rectTransform,
                "Molotov Button",
                solid,
                molotov,
                hudFont,
                "2",
                new Vector2(QUICK_MOLOTOV_X, 0f),
                out Image molotovSelection
            );
            grenadeSelection.enabled = true;
            molotovSelection.enabled = false;

            RectTransform mapMaskRoot = EnsureHudRect(root, "Mini Map Mask");
            ConfigureRect(
                mapMaskRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f),
                STATUS_MAP_POSITION,
                new Vector2(222f, 222f)
            );
            Image maskImage = mapMaskRoot.GetComponent<Image>() ??
                              mapMaskRoot.gameObject.AddComponent<Image>();
            maskImage.sprite = miniMapMask;
            maskImage.color = Color.white;
            maskImage.preserveAspect = true;
            maskImage.raycastTarget = false;
            Mask mask = mapMaskRoot.GetComponent<Mask>() ??
                        mapMaskRoot.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            Image mapTexture = EnsureHudImage(mapMaskRoot, "Generated Mini Map", miniMap, Color.white);
            mapTexture.preserveAspect = true;
            ConfigureCenteredRect(mapTexture.rectTransform, Vector2.zero, new Vector2(246f, 246f));
            Image markerImage = EnsureHudImage(
                mapMaskRoot,
                "Player Marker",
                playerMarker,
                Color.white
            );
            markerImage.preserveAspect = true;
            ConfigureCenteredRect(markerImage.rectTransform, new Vector2(10f, -12f),
                new Vector2(42f, 42f));
            Image waypointImage = EnsureHudImage(
                mapMaskRoot,
                "Waypoint",
                waypoint,
                Color.white
            );
            waypointImage.preserveAspect = true;
            ConfigureCenteredRect(waypointImage.rectTransform, new Vector2(62f, 57f),
                new Vector2(26f, 26f));

            Image ringImage = EnsureHudImage(root, "Mini Map Frame", miniMapRing, Color.white);
            ringImage.preserveAspect = true;
            ConfigureRect(
                ringImage.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f),
                STATUS_MAP_POSITION,
                new Vector2(240f, 240f)
            );
            ringImage.rectTransform.SetAsLastSibling();
            MigrateMenuButtonToHome(ringImage.rectTransform);
            EnsureMapUtilityButton(
                ringImage.rectTransform,
                "Phone Button",
                phone,
                new Vector2(-86f, -86f),
                13
            );
            EnsureMapUtilityButton(
                ringImage.rectTransform,
                "Home Button",
                home,
                new Vector2(86f, 86f),
                14
            );
            EnsureMapUtilityButton(
                ringImage.rectTransform,
                "Settings Button",
                settings,
                new Vector2(86f, -86f),
                15
            );
            EnsureCompassIndicator(ringImage.rectTransform, compass);

            FranklinPlayerStatusHud controller = root.GetComponent<FranklinPlayerStatusHud>() ??
                                                  root.gameObject.AddComponent<FranklinPlayerStatusHud>();
            SerializedObject serializedHud = new SerializedObject(controller);
            serializedHud.FindProperty("m_Money").intValue = 12480;
            serializedHud.FindProperty("m_NormalizedHealth").floatValue = 0.78f;
            serializedHud.FindProperty("m_WeaponName").stringValue = "PISTOL";
            serializedHud.FindProperty("m_AmmoInClip").intValue = 12;
            serializedHud.FindProperty("m_AmmoReserve").intValue = 48;
            serializedHud.FindProperty("m_HealthAttributeId").stringValue = "hp";
            serializedHud.FindProperty("m_PlayerLookupInterval").floatValue = 0.5f;
            serializedHud.FindProperty("m_MoneyText").objectReferenceValue = moneyText;
            serializedHud.FindProperty("m_HealthFill").objectReferenceValue = healthFill;
            serializedHud.FindProperty("m_HealthText").objectReferenceValue = healthText;
            serializedHud.FindProperty("m_WeaponCard").objectReferenceValue = weaponCard;
            serializedHud.FindProperty("m_WeaponIcon").objectReferenceValue = weaponIcon;
            serializedHud.FindProperty("m_ArmedWeaponSprite").objectReferenceValue = weapon;
            serializedHud.FindProperty("m_UnarmedWeaponSprite").objectReferenceValue = fists;
            serializedHud.FindProperty("m_WeaponNameText").objectReferenceValue = weaponName;
            serializedHud.FindProperty("m_AmmoText").objectReferenceValue = ammoText;
            serializedHud.FindProperty("m_GrenadeButton").objectReferenceValue = grenadeButton;
            serializedHud.FindProperty("m_MolotovButton").objectReferenceValue = molotovButton;
            serializedHud.FindProperty("m_GrenadeSelection").objectReferenceValue =
                grenadeSelection;
            serializedHud.FindProperty("m_MolotovSelection").objectReferenceValue =
                molotovSelection;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }

        private static void ConfigureHudCard(
            RectTransform rect,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            ConfigureRect(
                rect,
                anchor,
                anchor,
                new Vector2(0.5f, 0.5f),
                position,
                size
            );
        }

        private static Sprite LoadHudSprite(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(PLAYER_HUD_ASSET_ROOT + fileName);
        }

        private static Button EnsureQuickItemButton(
            Transform parent,
            string objectName,
            Sprite solidSprite,
            Sprite iconSprite,
            Font font,
            string count,
            Vector2 position,
            out Image selection)
        {
            Image hitArea = EnsureHudImage(
                parent,
                objectName,
                solidSprite,
                new Color(1f, 1f, 1f, 0.01f)
            );
            hitArea.raycastTarget = true;
            ConfigureRect(
                hitArea.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                new Vector2(130f, 72f)
            );

            Button button = hitArea.GetComponent<Button>() ??
                            hitArea.gameObject.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.01f);
            colors.highlightedColor = new Color(0.9f, 1f, 0.98f, 0.08f);
            colors.pressedColor = new Color(0.22f, 0.91f, 0.72f, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.02f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            selection = EnsureHudImage(
                hitArea.rectTransform,
                "Selected Accent",
                solidSprite,
                PLAYER_HUD_MINT
            );
            ConfigureCenteredRect(selection.rectTransform, new Vector2(0f, -32f),
                new Vector2(108f, 4f));
            selection.raycastTarget = false;
            selection.rectTransform.SetAsFirstSibling();

            Image icon = EnsureHudImage(hitArea.rectTransform, "Icon", iconSprite, Color.white);
            icon.preserveAspect = true;
            ConfigureCenteredRect(icon.rectTransform, new Vector2(-28f, 0f),
                new Vector2(46f, 46f));

            Text quantity = EnsureHudText(
                hitArea.rectTransform,
                "Quantity",
                font,
                count,
                28,
                TextAnchor.MiddleCenter,
                PLAYER_HUD_TEXT
            );
            ConfigureCenteredRect(quantity.rectTransform, new Vector2(36f, 0f),
                new Vector2(42f, 42f));
            return button;
        }

        private static void EnsureArmorDisplay(
            Transform parent,
            Sprite solidSprite,
            Sprite armorSprite,
            Font font)
        {
            Image armorRoot = EnsureHudImage(
                parent,
                "Armor Display",
                solidSprite,
                new Color(1f, 1f, 1f, 0.01f)
            );
            armorRoot.raycastTarget = false;
            ConfigureRect(
                armorRoot.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(QUICK_ARMOR_X, 0f),
                new Vector2(130f, 72f)
            );

            Image armorFill = EnsureHudImage(
                armorRoot.rectTransform,
                "Armor Fill",
                solidSprite,
                new Color(0.16f, 0.72f, 1f, 1f)
            );
            ConfigureCenteredRect(
                armorFill.rectTransform,
                new Vector2(0f, -32f),
                new Vector2(108f, 4f)
            );

            Image icon = EnsureHudImage(
                armorRoot.rectTransform,
                "Icon",
                armorSprite,
                Color.white
            );
            icon.preserveAspect = true;
            ConfigureCenteredRect(
                icon.rectTransform,
                new Vector2(-28f, 0f),
                new Vector2(46f, 46f)
            );

            Text value = EnsureHudText(
                armorRoot.rectTransform,
                "Value",
                font,
                "100",
                25,
                TextAnchor.MiddleCenter,
                PLAYER_HUD_TEXT
            );
            ConfigureCenteredRect(
                value.rectTransform,
                new Vector2(36f, 0f),
                new Vector2(48f, 42f)
            );
        }

        private static FranklinHudButton EnsureMapUtilityButton(
            Transform mapFrame,
            string buttonName,
            Sprite sprite,
            Vector2 defaultPosition,
            int action)
        {
            Transform existing = FindDirectChild(mapFrame, buttonName);
            bool isNew = existing == null;
            GameObject buttonObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    buttonName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(FranklinHudButton)
                );
            buttonObject.layer = 5;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            if (rect.parent != mapFrame) rect.SetParent(mapFrame, false);
            if (isNew)
            {
                ConfigureCenteredRect(
                    rect,
                    defaultPosition,
                    new Vector2(78f, 78f)
                );
            }

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;

            FranklinHudButton hudButton = buttonObject.GetComponent<FranklinHudButton>();
            SerializedObject serializedButton = new SerializedObject(hudButton);
            serializedButton.FindProperty("m_Action").intValue = action;
            serializedButton.FindProperty("m_Image").objectReferenceValue = image;
            serializedButton.FindProperty("m_IsToggle").boolValue = false;
            serializedButton.FindProperty("m_UseOpaqueVisual").boolValue = true;
            serializedButton.ApplyModifiedPropertiesWithoutUndo();

            rect.SetAsLastSibling();
            return hudButton;
        }

        private static void MigrateMenuButtonToHome(Transform mapFrame)
        {
            if (FindDirectChild(mapFrame, "Home Button") != null) return;

            Transform legacyMenu = FindDirectChild(mapFrame, "Menu Button");
            if (legacyMenu == null) return;

            legacyMenu.name = "Home Button";
            if (legacyMenu is RectTransform rect)
            {
                // This is the one requested layout change: move the former center Menu control
                // to the upper-right map rim. Its existing size remains untouched.
                rect.anchoredPosition = new Vector2(86f, 86f);
            }
        }

        private static Image EnsureCompassIndicator(Transform mapFrame, Sprite sprite)
        {
            Transform existing = FindDirectChild(mapFrame, "North South Compass");
            bool isNew = existing == null;
            GameObject compassObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    "North South Compass",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
            compassObject.layer = 5;

            RectTransform rect = compassObject.GetComponent<RectTransform>();
            if (rect.parent != mapFrame) rect.SetParent(mapFrame, false);
            Image image = compassObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;

            if (isNew)
            {
                ConfigureCenteredRect(
                    rect,
                    new Vector2(0f, -122f),
                    new Vector2(78f, 78f)
                );
            }

            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.SetAsLastSibling();
            return image;
        }

        private static void DestroyDirectChild(Transform parent, string childName)
        {
            Transform child = FindDirectChild(parent, childName);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static RectTransform EnsureHudRect(Transform parent, string objectName)
        {
            Transform existing = FindDirectChild(parent, objectName);
            GameObject hudObject = existing != null
                ? existing.gameObject
                : new GameObject(objectName, typeof(RectTransform));
            hudObject.layer = 5;

            RectTransform rect = hudObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image EnsureHudImage(
            Transform parent,
            string objectName,
            Sprite sprite,
            Color color)
        {
            RectTransform rect = EnsureHudRect(parent, objectName);
            Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private static Text EnsureHudText(
            Transform parent,
            string objectName,
            Font font,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            RectTransform rect = EnsureHudRect(parent, objectName);
            Text text = rect.GetComponent<Text>() ?? rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Shadow shadow = rect.GetComponent<Shadow>() ?? rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
            return text;
        }

        private static void ConfigureCenteredRect(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            ConfigureRect(
                rect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size
            );
        }

        private static void ConfigureRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static bool HasPlayerStatusHud(Transform root)
        {
            Transform statusHud = FindDirectChild(root, PLAYER_STATUS_HUD);
            Transform quickRail = FindDirectChild(statusHud, "Quick Item Rail");
            return statusHud != null &&
                   statusHud.GetComponent<FranklinPlayerStatusHud>() != null &&
                   FindDirectChild(statusHud, "Money Card")?.GetComponent<Image>() != null &&
                   FindDirectChild(statusHud, "Health Card")?.GetComponent<Image>() != null &&
                   FindDirectChild(statusHud, "Weapon Card")?.GetComponent<Image>() != null &&
                   quickRail?.GetComponent<Image>() != null &&
                   FindDirectChild(quickRail, "Grenade Button")?.GetComponent<Button>() != null &&
                   FindDirectChild(quickRail, "Molotov Button")?.GetComponent<Button>() != null &&
                   FindDirectChild(statusHud, "Mini Map Mask")?.GetComponent<Mask>() != null &&
                   FindDirectChild(statusHud, "Mini Map Frame")?.GetComponent<Image>() != null;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int index = 0; index < parent.childCount; index += 1)
            {
                Transform child = parent.GetChild(index);
                if (child.name == childName) return child;
            }

            return null;
        }

        private static bool HasButton(Transform parent, string buttonName)
        {
            Transform button = FindDirectChild(parent, buttonName);
            return button != null && button.GetComponent<FranklinHudButton>() != null;
        }
    }
}
