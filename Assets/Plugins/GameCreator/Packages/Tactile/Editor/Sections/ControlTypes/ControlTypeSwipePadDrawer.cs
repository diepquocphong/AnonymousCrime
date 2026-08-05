using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(ControlTypeSwipePad))]
    public class ControlTypeSwipePadDrawer : TControlTypeDrawer
    {
        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            SerializedProperty continuous = property.FindPropertyRelative("m_Continuous");
            SerializedProperty directions = property.FindPropertyRelative("m_Directions");

            var directionsBox = new Foldbox("Directions", "tactile:swipe-pad-directions");
            var fieldContinuous = new PropertyField(continuous); 
            var fieldDirections = new PropertyField(directions);

            directionsBox.Add(fieldDirections);
            body.Add(fieldContinuous);
            body.Add(directionsBox);

            SerializedProperty minSwipeDistance = property.FindPropertyRelative("m_MinSwipeDistance");
            SerializedProperty maxSwipeDuration = property.FindPropertyRelative("m_MaxSwipeDuration");
            SerializedProperty minSampleDistance = property.FindPropertyRelative("m_MinSampleDistance");
            SerializedProperty maxSampleDeviation = property.FindPropertyRelative("m_MaxSampleDeviation");

            var validationBox = new Foldbox("Validation", "tactile:swipe-pad-validation");
            var fieldMinSwipeDistance = new PropertyField(minSwipeDistance); 
            var fieldMaxSwipeDuration = new PropertyField(maxSwipeDuration); 
            var fieldMinSampleDistance = new PropertyField(minSampleDistance);
            var fieldMaxSampleDeviation = new PropertyField(maxSampleDeviation);

            body.Add(validationBox);
            validationBox.Add(fieldMinSwipeDistance);
            validationBox.Add(fieldMaxSwipeDuration);
            validationBox.Add(fieldMinSampleDistance);
            validationBox.Add(fieldMaxSampleDeviation);
        }

    }
}