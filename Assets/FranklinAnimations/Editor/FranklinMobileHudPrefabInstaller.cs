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
        private const string HEALTH_FRAME_PATH =
            "Assets/Ash Assets/Vehicle Integration/Car/UI/Generated/VehicleHealthFrame.png";
        private const string HUD_FONT_PATH =
            "Assets/Plugins/GameCreator/Installs/GameCreator.Blockout@1.6.12/UI/_Fonts/JosefinSans-Bold.ttf";
        private const string ON_FOOT_GROUP = "Franklin On Foot Controls";
        private const string VEHICLE_GROUP = "Franklin Vehicle Controls";
        private const string BIKE_HEALTH_UI = "Bike Health UI";
        private const string BIKE_SPEED_UI = "Bike Speed UI";
        private const float BIKE_HEALTH_FILL_WIDTH = 732f;

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
                new Vector2(173.4f, 561.13f), new Vector2(215f, 215f)),
            new("Jump", "player-jump", 8, new Vector2(1f, 0f),
                new Vector2(-505f, 170f), new Vector2(165f, 165f)),
            new("Enter Vehicle", "vehicle-enter", 7, new Vector2(0.72f, 0.4f),
                Vector2.zero, new Vector2(190f, 190f))
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
                new Vector2(-115f, -120f), new Vector2(190f, 190f)),
            new("Slow Drive", "vehicle-control-slow", 9, new Vector2(1f, 0f),
                new Vector2(-155f, 82f), new Vector2(163f, 163f)),
            new("Bike Headlight", "vehicle-control-headlight", 10, new Vector2(1f, 1f),
                new Vector2(-115f, -315f), new Vector2(155f, 155f), true),
            new("Bike Wheelie", "vehicle-control-wheelie", 11, new Vector2(1f, 1f),
                new Vector2(-285f, -315f), new Vector2(155f, 155f)),
            new("Bike Burnout", "vehicle-control-burnout", 12, new Vector2(1f, 1f),
                new Vector2(-455f, -315f), new Vector2(155f, 155f))
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
                Sprite healthFrame = AssetDatabase.LoadAssetAtPath<Sprite>(HEALTH_FRAME_PATH);
                Font hudFont = AssetDatabase.LoadAssetAtPath<Font>(HUD_FONT_PATH);

                foreach (ButtonDefinition button in ON_FOOT_BUTTONS)
                {
                    EnsureButton(onFoot, button);
                }
                foreach (ButtonDefinition button in VEHICLE_BUTTONS)
                {
                    EnsureButton(vehicle, button);
                }
                EnsureBikeHealthUi(vehicle, healthFrame);
                EnsureBikeSpeedUi(vehicle, hudFont);

                SerializedObject serializedHud = new SerializedObject(hud);
                serializedHud.FindProperty("m_BikeHealthFrameSprite").objectReferenceValue =
                    healthFrame;
                serializedHud.FindProperty("m_BikeSpeedFont").objectReferenceValue = hudFont;
                serializedHud.ApplyModifiedPropertiesWithoutUndo();

                onFoot.gameObject.SetActive(true);
                FindDirectChild(onFoot, "Enter Vehicle")?.gameObject.SetActive(false);
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
                   HasButton(root.Find(VEHICLE_GROUP), "Exit Vehicle") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Slow Drive") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Bike Headlight") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Bike Wheelie") &&
                   HasButton(root.Find(VEHICLE_GROUP), "Bike Burnout") &&
                   HasBikeHealthUi(root.Find(VEHICLE_GROUP)) &&
                   HasBikeSpeedUi(root.Find(VEHICLE_GROUP));
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

        private static void EnsureBikeHealthUi(RectTransform parent, Sprite healthFrame)
        {
            Transform existing = FindDirectChild(parent, BIKE_HEALTH_UI);
            GameObject healthObject = existing != null
                ? existing.gameObject
                : new GameObject(BIKE_HEALTH_UI, typeof(RectTransform));
            healthObject.layer = 5;

            RectTransform healthRoot = healthObject.GetComponent<RectTransform>();
            healthRoot.SetParent(parent, false);
            healthRoot.anchorMin = new Vector2(0.5f, 0f);
            healthRoot.anchorMax = new Vector2(0.5f, 0f);
            healthRoot.pivot = new Vector2(0.5f, 0.5f);
            healthRoot.anchoredPosition = new Vector2(0f, 48f);
            healthRoot.sizeDelta = new Vector2(760f, 74f);

            Transform background = FindDirectChild(healthRoot, "Background");
            if (background != null) Object.DestroyImmediate(background.gameObject);

            Image fill = EnsureHealthImage(
                healthRoot,
                "Fill",
                new Vector2(BIKE_HEALTH_FILL_WIDTH, 38f),
                null,
                new Color(0.55f, 0.96f, 0.16f, 1f),
                true
            );
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(14f, 0f);

            EnsureHealthImage(
                healthRoot,
                "Generated Health Frame",
                new Vector2(760f, 74f),
                healthFrame,
                healthFrame != null ? Color.white : Color.clear,
                false
            );
            healthRoot.gameObject.SetActive(false);
        }

        private static Image EnsureHealthImage(
            RectTransform parent,
            string objectName,
            Vector2 size,
            Sprite sprite,
            Color color,
            bool leftAnchored)
        {
            Transform existing = FindDirectChild(parent, objectName);
            GameObject imageObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
            imageObject.layer = 5;
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = leftAnchored ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = leftAnchored ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            rect.SetAsLastSibling();
            return image;
        }

        private static bool HasBikeHealthUi(Transform vehicleGroup)
        {
            Transform root = FindDirectChild(vehicleGroup, BIKE_HEALTH_UI);
            return root != null &&
                   FindDirectChild(root, "Background") == null &&
                   FindDirectChild(root, "Fill")?.GetComponent<Image>() != null &&
                   FindDirectChild(root, "Generated Health Frame")?.GetComponent<Image>() != null;
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
