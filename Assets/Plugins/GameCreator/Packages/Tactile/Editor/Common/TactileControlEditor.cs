using System;
using System.Text;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;

using GameCreator.Runtime.Common;
using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomEditor(typeof(TactileControl), true)]
    public class TactileControlEditor : UnityEditor.Editor
    {
        private const string PATH_USS = EditorPaths.PACKAGES + "Tactile/Editor/StyleSheets/tactile-control";
        private const string INFO_NO_TOUCHSREEN = "No Touchscreen device found. Interaction to this Component is not available.";
        private const string ERR_DUPLICATE_ID = "Another Tactile Control component has the same ID.";

        // MEMBERS: -------------------------------------------------------------------------------
        
        private TactileControl m_Control;
        
        private WarningMessage m_InfoTouch;
        private ErrorMessage m_ErrorID;
        
        private readonly StringBuilder m_ErrorID_SB = new StringBuilder();
        private static readonly List<TactileControlEditor> s_Editors = new();

        // INITIALIZERS: --------------------------------------------------------------------------

        private void OnEnable()
        {
            this.m_Control = this.target as TactileControl;
            InputSystem.onDeviceChange += this.RefreshInfoTouch;
            s_Editors.Add(this);
        }
        
        private void OnDisable()
        {
            InputSystem.onDeviceChange -= this.RefreshInfoTouch;
            s_Editors.Remove(this);
        }

        // EVENTS: --------------------------------------------------------------------------------

        public static event Action<object> EventSceneGUI;

        // PAINT: ---------------------------------------------------------------------------------

        public override VisualElement CreateInspectorGUI()
        {
            this.m_InfoTouch = new WarningMessage(INFO_NO_TOUCHSREEN);
            this.m_ErrorID = new ErrorMessage(ERR_DUPLICATE_ID);

            SerializedProperty interactable = this.serializedObject.FindProperty("m_Interactable");
            SerializedProperty touchableArea = this.serializedObject.FindProperty("m_TouchableArea");
            SerializedProperty controlType = this.serializedObject.FindProperty("m_ControlType");
            SerializedProperty uniqueId = this.serializedObject.FindProperty("m_UniqueID");

            var root = new VisualElement();
            var fieldInteractable = new PropertyField(interactable);
            var fieldTouchableArea = new PropertyField(touchableArea);
            var fieldControlType = new PropertyField(controlType);
            var fieldUniqueId = new PropertyField(uniqueId);

            root.Add(new SpaceCustom(3));
            root.Add(fieldInteractable);

            root.Add(new SpaceCustom(4));
            root.Add(fieldTouchableArea);
            root.Add(fieldControlType);

            root.Add(new SpaceSmaller());
            root.Add(this.m_ErrorID);
            root.Add(fieldUniqueId);
            root.Add(this.m_InfoTouch);

            this.RefreshInfoTouch(null, 0);
            this.RefreshErrorID();
            
            fieldInteractable.RegisterValueChangeCallback(callback => 
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
                this.m_Control.Interactable = callback.changedProperty.boolValue;
            });

            fieldUniqueId.RegisterValueChangeCallback(_ =>
            {
                foreach (TactileControlEditor editor in s_Editors)
                {
                    if (editor == null) continue;
                    editor.RefreshErrorID();
                }
            });

            StyleSheet[] styleSheets = StyleSheetUtils.Load(PATH_USS);
            foreach (StyleSheet styleSheet in styleSheets) root.styleSheets.Add(styleSheet);

            return root;
        }

        void OnSceneGUI() => EventSceneGUI?.Invoke(this.target);

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private void RefreshInfoTouch(InputDevice device, InputDeviceChange change)
        {
            if (this.m_InfoTouch == null) return;

            this.m_InfoTouch.style.display = Touchscreen.current == null && Application.isPlaying
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshErrorID()
        {
            if (this.m_ErrorID == null) return;
            if (this.m_Control == null) return;
            if (serializedObject.targetObject == null) return;
            
            this.m_ErrorID.style.display = DisplayStyle.None;

            var instance = this.m_Control.gameObject;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.IsPartOfPrefabContents(instance)) return;
            if (PrefabUtility.IsPartOfPrefabAsset(instance)) return;
            
            string id = this.serializedObject.FindProperty("m_UniqueID")
                                             .FindPropertyRelative(UniqueIDDrawer.SERIALIZED_ID)
                                             .FindPropertyRelative(IdStringDrawer.NAME_STRING)
                                             .stringValue;

            this.m_ErrorID_SB.Clear();
            this.m_ErrorID_SB.Append(ERR_DUPLICATE_ID);

            bool hasDuplicates = false;
            
            #if UNITY_6000_0_OR_NEWER
            var controls = FindObjectsByType<TactileControl>(
                FindObjectsInactive.Include, FindObjectsSortMode.InstanceID
            );
            #else
            var controls = FindObjectsOfType<TactileControl>(true);
            #endif
            
            foreach (TactileControl control in controls)
            {
                if (control.UniqueID != id || control == this.m_Control) continue;
                
                this.m_ErrorID_SB.Append($"\n• {this.GetPath(control.transform)}");
                hasDuplicates = true;
            }

            if (hasDuplicates)
            {
                this.m_ErrorID.Text = this.m_ErrorID_SB.ToString();
                this.m_ErrorID.style.display = DisplayStyle.Flex;
                this.m_ErrorID.style.marginTop = 0f;
                return;
            }
        }

        private string GetPath(Transform current) 
        {
            if (current.parent == null) return $"{current.name}";
            return $"{this.GetPath(current.parent)}/{current.name}";
        }

    }
}