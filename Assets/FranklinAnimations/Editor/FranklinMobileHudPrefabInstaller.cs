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
        private const string ON_FOOT_GROUP = "Franklin On Foot Controls";
        private const string VEHICLE_GROUP = "Franklin Vehicle Controls";

        private readonly struct ButtonDefinition
        {
            public readonly string Name;
            public readonly string SpriteName;
            public readonly int Action;
            public readonly Vector2 Anchor;
            public readonly Vector2 Position;
            public readonly Vector2 Size;

            public ButtonDefinition(
                string name,
                string spriteName,
                int action,
                Vector2 anchor,
                Vector2 position,
                Vector2 size)
            {
                this.Name = name;
                this.SpriteName = spriteName;
                this.Action = action;
                this.Anchor = anchor;
                this.Position = position;
                this.Size = size;
            }
        }

        private static readonly ButtonDefinition[] ON_FOOT_BUTTONS =
        {
            new("Jog", "player-movement-0", 0, new Vector2(1f, 0f),
                new Vector2(-305f, 170f), new Vector2(165f, 165f)),
            new("Sprint", "player-movement-1", 1, new Vector2(1f, 0f),
                new Vector2(-130f, 195f), new Vector2(215f, 215f)),
            new("Jump", "player-jump", 8, new Vector2(1f, 0f),
                new Vector2(-505f, 170f), new Vector2(165f, 165f)),
            new("Enter Vehicle", "vehicle-enter", 7, new Vector2(1f, 0.5f),
                new Vector2(-90f, 0f), new Vector2(125f, 125f))
        };

        private static readonly ButtonDefinition[] VEHICLE_BUTTONS =
        {
            new("Steer Left", "vehicle-control-0", 2, new Vector2(0f, 0f),
                new Vector2(160f, 175f), new Vector2(210f, 210f)),
            new("Steer Right", "vehicle-control-1", 3, new Vector2(0f, 0f),
                new Vector2(365f, 175f), new Vector2(210f, 210f)),
            new("Accelerate", "vehicle-control-2", 4, new Vector2(1f, 0f),
                new Vector2(-130f, 205f), new Vector2(230f, 230f)),
            new("Brake Reverse", "vehicle-control-3", 5, new Vector2(1f, 0f),
                new Vector2(-345f, 165f), new Vector2(180f, 180f)),
            new("Handbrake", "vehicle-control-4", 6, new Vector2(1f, 0f),
                new Vector2(-340f, 355f), new Vector2(135f, 135f)),
            new("Exit Vehicle", "vehicle-control-5", 7, new Vector2(1f, 1f),
                new Vector2(-90f, -95f), new Vector2(125f, 125f))
        };

        static FranklinMobileHudPrefabInstaller()
        {
            EditorApplication.delayCall += InstallIfNeeded;
        }

        [MenuItem("Tools/Franklin Game/Install Player Mobile HUD")]
        public static void Install()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"Franklin mobile HUD could not find {PLAYER_CANVAS_PATH}");
                return;
            }

            GameObject canvasRoot = PrefabUtility.LoadPrefabContents(PLAYER_CANVAS_PATH);
            try
            {
                _ = canvasRoot.GetComponent<FranklinMobileHud>() ??
                    canvasRoot.AddComponent<FranklinMobileHud>();

                RectTransform onFoot = EnsureGroup(canvasRoot.transform, ON_FOOT_GROUP);
                RectTransform vehicle = EnsureGroup(canvasRoot.transform, VEHICLE_GROUP);

                foreach (ButtonDefinition button in ON_FOOT_BUTTONS)
                {
                    EnsureButton(onFoot, button);
                }
                foreach (ButtonDefinition button in VEHICLE_BUTTONS)
                {
                    EnsureButton(vehicle, button);
                }

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
                   HasButton(root.Find(VEHICLE_GROUP), "Exit Vehicle");
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
            serializedButton.ApplyModifiedPropertiesWithoutUndo();
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
