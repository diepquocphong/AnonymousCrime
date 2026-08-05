using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(ControlTypeGesturePad))]
    public class ControlTypeGesturePadDrawer : TControlTypeDrawer
    {
        private const string SIMULATE = "Input Simulate"; 
        private const string FINGERS = "Fingers"; 
        private const string THRESHOLD = "Threshold"; 
        private const string SENSITIVITY = "Sensitivity"; 

        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            SerializedProperty controlPath = property.FindPropertyRelative("m_ControlPath");
            SerializedProperty inputBind = property.FindPropertyRelative("m_BindInput");
            SerializedProperty multitask = property.FindPropertyRelative("m_Multitask");

            var fieldControlPath = new PropertyField(controlPath);
            var fieldInputBind = new PropertyField(inputBind);
            var fieldMultitask = new PropertyField(multitask, "Multi-task");

            body.Add(fieldControlPath);
            body.Add(fieldInputBind);
            body.Add(fieldMultitask);

            this.CreatePanDrawer(body, property);
            this.CreatePinchDrawer(body, property);
            this.CreateTwistDrawer(body, property);
        }

        private void CreatePanDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty panInputSimulate = property.FindPropertyRelative("m_PanInputSimulate");
            SerializedProperty panFingers = property.FindPropertyRelative("m_PanFingers");
            SerializedProperty panThreshold = property.FindPropertyRelative("m_PanThreshold");
            SerializedProperty panSensitivity = property.FindPropertyRelative("m_PanSensitivity");

            var panBox = new Foldbox("Pan", "tactile:control-pad-pan");
            var fieldPanInputSimulate = new PropertyField(panInputSimulate, SIMULATE);
            var fieldPanFingers = new PropertyField(panFingers, FINGERS);
            var fieldPanThreshold = new PropertyField(panThreshold, THRESHOLD);

            body.Add(panBox);
            panBox.Add(fieldPanInputSimulate); 
            panBox.Add(fieldPanFingers); 
            panBox.Add(fieldPanThreshold); 

            if (panSensitivity != null)
            {
                var fieldPanSensitivity = new Vector2Field(SENSITIVITY);
                fieldPanSensitivity.BindProperty(panSensitivity);
                fieldPanSensitivity.Q(className: "unity-base-field__input").RemoveAt(2);
                fieldPanSensitivity.AddToClassList(AlignLabel.CLASS_UNITY_ALIGN_LABEL);
                panBox.Add(fieldPanSensitivity); 
            }

            fieldPanInputSimulate.style.marginBottom = 1f;
            fieldPanInputSimulate.style.paddingLeft = 1f;

            fieldPanFingers.RegisterCallback<FocusOutEvent>(callback => 
            {
                property.serializedObject.Update();
                if (panFingers.intValue < 0) panFingers.intValue = 0;
                else if (panFingers.intValue > 10) panFingers.intValue = 10;
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
               
            fieldPanThreshold.RegisterCallback<FocusOutEvent>(callback => 
            {
                property.serializedObject.Update();
                if (panThreshold.floatValue < 0) panThreshold.floatValue = 0;
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private void CreatePinchDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty pinchInputSimulate = property.FindPropertyRelative("m_PinchInputSimulate");
            SerializedProperty pinchFingers = property.FindPropertyRelative("m_PinchFingers");
            SerializedProperty pinchThreshold = property.FindPropertyRelative("m_PinchThreshold");
            SerializedProperty pinchSensitivity = property.FindPropertyRelative("m_PinchSensitivity");

            var pinchBox = new Foldbox("Pinch", "tactile:control-pad-pinch");
            var fieldPinchInputSimulate = new PropertyField(pinchInputSimulate, SIMULATE);
            var fieldPinchFingers = new PropertyField(pinchFingers, FINGERS);
            var fieldPinchThreshold = new PropertyField(pinchThreshold, THRESHOLD);
            var fieldPinchSensitivity = new PropertyField(pinchSensitivity, SENSITIVITY);

            body.Add(pinchBox);
            pinchBox.Add(fieldPinchInputSimulate); 
            pinchBox.Add(fieldPinchFingers); 
            pinchBox.Add(fieldPinchThreshold); 
            pinchBox.Add(fieldPinchSensitivity); 

            fieldPinchInputSimulate.style.marginBottom = 1f;
            fieldPinchInputSimulate.style.paddingLeft = 1f;

            fieldPinchFingers.RegisterCallback<FocusOutEvent>(callback => 
            {
                property.serializedObject.Update();
                if (pinchFingers.intValue < 2) pinchFingers.intValue = 0;
                else if (pinchFingers.intValue > 10) pinchFingers.intValue = 10;
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
            
            fieldPinchThreshold.RegisterCallback<FocusOutEvent>(callback => 
            {
                property.serializedObject.Update();
                if (pinchThreshold.floatValue < 0) pinchThreshold.floatValue = 0;
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private void CreateTwistDrawer(VisualElement body, SerializedProperty property)
        {
            SerializedProperty twistInputSimulate = property.FindPropertyRelative("m_TwistInputSimulate");
            SerializedProperty twistFingers = property.FindPropertyRelative("m_TwistFingers");
            SerializedProperty twistThreshold = property.FindPropertyRelative("m_TwistThreshold");
            SerializedProperty twistSensitivity = property.FindPropertyRelative("m_TwistSensitivity");

            var twistBox = new Foldbox("Twist", "tactile:control-pad-twist");
            var fieldTwistInputSimulate = new PropertyField(twistInputSimulate, SIMULATE);
            var fieldTwistFingers = new PropertyField(twistFingers, FINGERS);
            var fieldTwistThreshold = new PropertyField(twistThreshold, THRESHOLD);
            var fieldPwistSensitivity = new PropertyField(twistSensitivity, SENSITIVITY);

            body.Add(twistBox);
            twistBox.Add(fieldTwistInputSimulate); 
            twistBox.Add(fieldTwistFingers); 
            twistBox.Add(fieldTwistThreshold); 
            twistBox.Add(fieldPwistSensitivity); 
            
            fieldTwistInputSimulate.style.marginBottom = 1f;
            fieldTwistInputSimulate.style.paddingLeft = 1f;

            fieldTwistFingers.RegisterCallback<FocusOutEvent>(callback => 
            {
                property.serializedObject.Update();
                if (twistFingers.intValue < 2) twistFingers.intValue = 0;
                else if (twistFingers.intValue > 10) twistFingers.intValue = 10;
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });

            fieldTwistThreshold.RegisterCallback<FocusOutEvent>(callback => 
            {
                property.serializedObject.Update();
                if (twistThreshold.floatValue < 0) twistThreshold.floatValue = 0;
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
        }

    }
}