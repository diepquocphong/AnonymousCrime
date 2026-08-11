#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.Shared.Utility
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class PGEditorAutoInspector : Editor
    {
        private VisualElement container;

        public override VisualElement CreateInspectorGUI()
        {
            var targetType = target.GetType();

            if (!Attribute.IsDefined(targetType, typeof(PGEditorAutoAttribute))) return base.CreateInspectorGUI();

            container = new VisualElement();
            CreateElements(targetType);
            return container;
        }

        private void CreateElements(Type targetType)
        {
            var method = typeof(PGEditorAutoSetup)
                .GetMethod(
                    "CreateAndBindClassElements",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] {typeof(SerializedObject), typeof(VisualElement)},
                    null
                );

            if (method == null) return;
            var genericMethod = method.MakeGenericMethod(targetType);
            genericMethod.Invoke(null, new object[] {serializedObject, container});
        }
    }
}
#endif