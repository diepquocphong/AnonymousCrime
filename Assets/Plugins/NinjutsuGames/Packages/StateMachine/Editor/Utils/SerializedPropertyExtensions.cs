using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace NinjutsuGames.StateMachine.Editor
{
    public static class SerializedPropertyExtensions
    {
        // Method to get the parent property
        public static SerializedProperty GetParentProperty(this SerializedProperty property)
        {
            var path = property.propertyPath;
            var lastDotIndex = path.LastIndexOf('.');
            if (lastDotIndex == -1) return null;

            var parentPath = path.Substring(0, lastDotIndex);
            return property.serializedObject.FindProperty(parentPath);
        }

        // Method to get the parent object
        public static object GetParentObject(this SerializedProperty property)
        {
            var path = property.propertyPath.Replace(".Array.data[", "[");
            var elements = path.Split('.');

            object obj = property.serializedObject.targetObject;
            foreach (var element in elements.Take(elements.Length - 1))
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("[", StringComparison.Ordinal));
                    var index = Convert.ToInt32(element.Substring(element.IndexOf("[", StringComparison.Ordinal)).Replace("[", "").Replace("]", ""));
                    obj = GetValue(obj, elementName, index);
                }
                else
                {
                    obj = GetValue(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue(object source, string name)
        {
            if (source == null) return null;
            var type = source.GetType();
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                var property = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                return property?.GetValue(source, null);
            }
            return field.GetValue(source);
        }

        private static object GetValue(object source, string name, int index)
        {
            var enumerable = GetValue(source, name) as IEnumerable;
            var enumerator = enumerable?.GetEnumerator();
            while (index-- >= 0)
            {
                if (!enumerator.MoveNext()) return null;
            }
            return enumerator.Current;
        }
    }
}