using System;
using System.Reflection;

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

using GameCreator.Runtime.Common;
using Niam.Runtime.Tactile;
using GameCreator.Editor.Installs;

namespace Niam.Editor.Tactile 
{
    [InitializeOnLoad]
    public static class TactileControlMenu
    {
        private static readonly Type s_MenuOptionsType;
        private static readonly MethodInfo s_PlaceUIElementRootMethod;
        private const BindingFlags FLAGS = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private const string CONTROLS_PATH = EditorPaths.DEPLOY_INSTALLS + "Tactile.Controls@";

        static TactileControlMenu()
        {
            if (s_MenuOptionsType == null)
            {
                Assembly[] assemlies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemlies.Length; i++)
                {
                    Type[] types;

                    try
                    {
                        types = assemlies[i].GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        types = e.Types;
                    }

                    for (int j = 0; j < types.Length; j++)
                    {
                        if (types[j] == null) continue;
                        if (types[j].FullName != "UnityEditor.UI.MenuOptions") continue;

                        s_MenuOptionsType = types[j];
                    }
                }
            }

            s_PlaceUIElementRootMethod ??= s_MenuOptionsType?.GetMethod("PlaceUIElementRoot", FLAGS);

            if (s_MenuOptionsType == null)
            {
                Debug.LogError("Could not find UnityEditor.UI.MenuOptions type.");
            }

            if (s_PlaceUIElementRootMethod == null)
            {
                Debug.LogError("Could not find PlaceUIElementRoot method.");
            }
        }

        // CREATION MENU: -------------------------------------------------------------------------

        [MenuItem("GameObject/Game Creator/UI/Tactile Control/Default None", false, 0)]
        public static void CreateDefaultNone(MenuCommand menuCommand)
        {
            var instance = new GameObject("Tactile");
            var transform = instance.AddComponent<RectTransform>();
            instance.AddComponent<TactileControl>();

            var background = new GameObject("Background");
            var backgroundRect = background.AddComponent<RectTransform>();
            var backgroundImg = background.AddComponent<Image>();
            backgroundRect.SetParent(transform);
            backgroundRect.sizeDelta = Vector2.zero;
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundImg.color = new Color(0f, 0f, 0f, 0.15f);

            s_PlaceUIElementRootMethod?.Invoke(null, new object[] { instance, menuCommand });
            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            Selection.activeObject = instance;
        }

        [MenuItem("GameObject/Game Creator/UI/Tactile Control/Push Button", false, 0)]
        public static void CreatePushButton(MenuCommand menuCommand)
        {
            var instance = new GameObject("PushButton");
            var transform = instance.AddComponent<RectTransform>();

            var background = new GameObject("Background");
            var backgroundRect = background.AddComponent<RectTransform>();
            var backgroundImg = background.AddComponent<Image>();
            backgroundRect.SetParent(transform);
            backgroundRect.sizeDelta = Vector2.zero;
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundImg.color = new Color(0f, 0f, 0f, 0.15f);

            var control = instance.AddComponent<TactileControl>();
            var serializedObject = new SerializedObject(control);
            var controlType = serializedObject.FindProperty("m_ControlType");
            controlType.managedReferenceValue = new ControlTypePushButton();
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            s_PlaceUIElementRootMethod?.Invoke(null, new object[] { instance, menuCommand });
            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            Selection.activeObject = instance;
        }

        [MenuItem("GameObject/Game Creator/UI/Tactile Control/Interaction Pad", false, 0)]
        public static void CreateInteractionPad(MenuCommand menuCommand)
        {
            var instance = new GameObject("InteractionPad");
            var transform = instance.AddComponent<RectTransform>();

            var background = new GameObject("Background");
            var backgroundRect = background.AddComponent<RectTransform>();
            var backgroundImg = background.AddComponent<Image>();
            backgroundRect.SetParent(transform);
            backgroundRect.sizeDelta = Vector2.zero;
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundImg.color = new Color(0f, 0f, 0f, 0.15f);

            var control = instance.AddComponent<TactileControl>();
            var serializedObject = new SerializedObject(control);
            var controlType = serializedObject.FindProperty("m_ControlType");
            controlType.managedReferenceValue = new ControlTypeInteractionPad();
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            s_PlaceUIElementRootMethod?.Invoke(null, new object[] { instance, menuCommand });
            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            Selection.activeObject = instance;
        }

        [MenuItem("GameObject/Game Creator/UI/Tactile Control/Gesture Pad", false, 0)]
        public static void CreateGesturePad(MenuCommand menuCommand)
        {
            var instance = new GameObject("GesturePad");
            var transform = instance.AddComponent<RectTransform>();

            var background = new GameObject("Background");
            var backgroundRect = background.AddComponent<RectTransform>();
            var backgroundImg = background.AddComponent<Image>();
            backgroundRect.SetParent(transform);
            backgroundRect.sizeDelta = Vector2.zero;
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundImg.color = new Color(0f, 0f, 0f, 0.15f);

            var control = instance.AddComponent<TactileControl>();
            var serializedObject = new SerializedObject(control);
            var controlType = serializedObject.FindProperty("m_ControlType");
            controlType.managedReferenceValue = new ControlTypeGesturePad();
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            s_PlaceUIElementRootMethod?.Invoke(null, new object[] { instance, menuCommand });
            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            Selection.activeObject = instance;
        }

        [MenuItem("GameObject/Game Creator/UI/Tactile Control/Swipe Pad", false, 0)]
        public static void CreateSwipePad(MenuCommand menuCommand)
        {
            var instance = new GameObject("SwipePad");
            var transform = instance.AddComponent<RectTransform>();

            var background = new GameObject("Background");
            var backgroundRect = background.AddComponent<RectTransform>();
            var backgroundImg = background.AddComponent<Image>();
            backgroundRect.SetParent(transform);
            backgroundRect.sizeDelta = Vector2.zero;
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundImg.color = new Color(0f, 0f, 0f, 0.15f);

            var control = instance.AddComponent<TactileControl>();
            var serializedObject = new SerializedObject(control);
            var controlType = serializedObject.FindProperty("m_ControlType");
            controlType.managedReferenceValue = new ControlTypeSwipePad();
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            s_PlaceUIElementRootMethod?.Invoke(null, new object[] { instance, menuCommand });
            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            Selection.activeObject = instance;
        }

        [MenuItem("GameObject/Game Creator/UI/Tactile Control/Analog Stick", false, 0)]
        public static void CreateAnalogStick(MenuCommand menuCommand)
        {
            var version = InstallManager.GetInstalledVersion("Tactile.Controls").ToString();

            var instance = new GameObject("AnalogStick");
            instance.SetActive(false);

            var transform = instance.AddComponent<RectTransform>();
            transform.sizeDelta = new Vector2(200f, 200f);

            var background = new GameObject("Background");
            var backgroundRect = background.AddComponent<RectTransform>();
            var backgroundImg = background.AddComponent<Image>();
            backgroundRect.SetParent(transform);
            backgroundRect.sizeDelta = new Vector2(200f, 200f);
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundImg.color = new Color(0f, 0f, 0f, 0.15f);

            var surface = new GameObject("Surface");
            var surfaceRect = surface.AddComponent<RectTransform>();
            var surfaceImg = surface.AddComponent<Image>();
            surfaceRect.SetParent(transform);
            surfaceRect.localPosition = Vector3.zero;
            surfaceRect.sizeDelta = new Vector2(200f, 200f);

            surfaceImg.color = new Color(0f, 0f, 0f, 0.5882353f);
            surfaceImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{CONTROLS_PATH}{version}/Textures/Stick_Surface.png"
            );

            var handle = new GameObject("Handle");
            var handleRect = handle.AddComponent<RectTransform>();
            var handleImg = handle.AddComponent<Image>();
            handleRect.SetParent(surfaceRect);
            handleRect.localPosition = Vector3.zero;
            handleRect.sizeDelta = new Vector2(90f, 90f);
            handleImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{CONTROLS_PATH}{version}/Textures/Stick_Handle.png"
            );

            var arrow = new GameObject("Arrow");
            var arrowRect = arrow.AddComponent<RectTransform>();
            var arrowImg = arrow.AddComponent<Image>();
            arrowRect.SetParent(surfaceRect);
            arrowRect.localPosition = new Vector3(0f, 100f);
            arrowRect.sizeDelta = new Vector2(130f, 45f);
            arrowImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{CONTROLS_PATH}{version}/Textures/Stick_Arrow.png"
            );

            var control = instance.AddComponent<TactileControl>();
            var serializedObject = new SerializedObject(control);
            var propTouchableArea = serializedObject.FindProperty("m_TouchableArea");
            var propControlType = serializedObject.FindProperty("m_ControlType");

            propControlType.managedReferenceValue = new ControlTypeAnalogStick();
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            propTouchableArea.FindPropertyRelative("m_MultiTouch").boolValue = false;
            propControlType.FindPropertyRelative("m_Surface").objectReferenceValue = surface;
            propControlType.FindPropertyRelative("m_Handle").objectReferenceValue = handle;
            propControlType.FindPropertyRelative("m_Arrow").objectReferenceValue = arrow;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            serializedObject.Dispose();
            
            s_PlaceUIElementRootMethod?.Invoke(null, new object[] { instance, menuCommand });
            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            Selection.activeObject = instance;
            instance.SetActive(true);
        }

        [MenuItem("GameObject/Game Creator/UI/Tactile Control/Steering Wheel", false, 0)]
        public static void CreateSteeringWheel(MenuCommand menuCommand)
        {
            var version = InstallManager.GetInstalledVersion("Tactile.Controls").ToString();
            
            var instance = new GameObject("SteeringWheel");
            instance.SetActive(false);

            var transform = instance.AddComponent<RectTransform>();
            transform.sizeDelta = new Vector2(250f, 250f);

            var wheel = new GameObject("Wheel");
            var wheelRect = wheel.AddComponent<RectTransform>();
            var wheelImg = wheel.AddComponent<Image>();
            wheelRect.SetParent(transform);
            wheelRect.localPosition = Vector3.zero;
            wheelRect.sizeDelta = new Vector2(250f, 250f);
            wheelImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{CONTROLS_PATH}{version}/Textures/Steer_Wheel.png"
            );

            var control = instance.AddComponent<TactileControl>();
            var serializedObject = new SerializedObject(control);
            var propTouchableArea = serializedObject.FindProperty("m_TouchableArea");
            var propControlType = serializedObject.FindProperty("m_ControlType");

            var touchableArea = new TouchableAreaTransformEllipse();

            typeof(TouchableAreaTransformEllipse)
                .GetField("m_RectTransfom", (BindingFlags)38)
                .SetValue(touchableArea, GetGameObjectInstance.Create(wheel));

            propTouchableArea.managedReferenceValue = touchableArea;
            propControlType.managedReferenceValue = new ControlTypeSteeringWheel();
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            propTouchableArea.FindPropertyRelative("m_MultiTouch").boolValue = false;
            propControlType.FindPropertyRelative("m_Wheel").objectReferenceValue = wheel;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            serializedObject.Dispose();
            
            s_PlaceUIElementRootMethod?.Invoke(null, new object[] { instance, menuCommand });
            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            Selection.activeObject = instance;
            instance.SetActive(true);
        }
    }
}