// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PampelGames.Shared.Utility
{
    /// <summary>
    ///     Duplicates fields and properties of the source object to the target objects.
    /// </summary>
    [PGEditorAuto]
    public class PGCopyObjectFields : MonoBehaviour
    {
        public Object source;

        [Tooltip("List: Uses the target objects list.\n\n" +
                 "Children: Finds all targets from children.\n\n" +
                 "Project Folder: Finds all targets in the project folder of the source.")]
        public TargetType target = TargetType.List;

        public List<Object> targetObjects;

        [Tooltip("Optional: Only copy fields and properties of these names.")]
        public List<string> fieldNames;

        [PGButtonMethod(nameof(CopyFields))] public string copyFields;

        public enum TargetType
        {
            List,
            Children,
            ProjectFolder
        }


        public void CopyFields()
        {
            if (target == TargetType.List)
            {
                for (var i = 0; i < targetObjects.Count; i++)
                {
                    PGClassUtility.CopyClassValues(source, targetObjects[i], fieldNames);
                    EditorUtility.SetDirty(targetObjects[i]);
                }
            }
            else if (target == TargetType.Children)
            {
                targetObjects.Clear();
                var sourceType = source.GetType();
                if (!sourceType.IsSubclassOf(typeof(Component)) && sourceType != typeof(Component))
                {
                    EditorUtility.DisplayDialog("Invalid Component", "Target type Children requires a Component as source.", "Ok");
                    return;
                }

                var components = GetComponentsInChildren(sourceType, true);
                targetObjects = new List<Object>();
                foreach (var component in components)
                    if (component != source)
                        targetObjects.Add(component);

                for (var i = 0; i < targetObjects.Count; i++)
                {
                    PGClassUtility.CopyClassValues(source, targetObjects[i], fieldNames);
                    EditorUtility.SetDirty(targetObjects[i]);
                }

                targetObjects.Clear();
            }
            else if (target == TargetType.ProjectFolder)
            {
                targetObjects.Clear();
                var sourcePath = AssetDatabase.GetAssetPath(source);
                var folderPath = Path.GetDirectoryName(sourcePath);
                var sourceType = source.GetType();
                var guids = AssetDatabase.FindAssets($"t:{sourceType.Name}", new[] {folderPath});
                targetObjects = new List<Object>();
                for (var i = 0; i < guids.Length; i++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var asset = AssetDatabase.LoadAssetAtPath(assetPath, sourceType);
                    if (asset != source) targetObjects.Add(asset);
                }

                for (var i = 0; i < targetObjects.Count; i++)
                {
                    PGClassUtility.CopyClassValues(source, targetObjects[i], fieldNames);
                    EditorUtility.SetDirty(targetObjects[i]);
                }

                targetObjects.Clear();
            }
        }
    }
}
#endif