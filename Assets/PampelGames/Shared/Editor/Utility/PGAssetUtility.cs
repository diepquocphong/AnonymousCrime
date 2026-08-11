// ---------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ---------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PampelGames.Shared.Editor
{
    public static class PGAssetUtility
    {
        /// <summary>
        ///     Exports a Unity texture asset of the specified type to the desktop.
        /// </summary>
        public static void ExportUnityTexture<T>() where T : Object
        {
            var texture = EditorGUIUtility.ObjectContent(null, typeof(T)).image as Texture2D;
            var readableTexture = PGExportUtility.CreateReadableTexture2D(texture);
            PGExportUtility.ExportTexture2D(readableTexture, typeof(T).Name);
        }

        /// <summary>
        ///     Get a preview Texture from an object, similar as displayed in the bottom of a Unity inspector.
        /// </summary>
        public static Texture2D GetPrefabPreview(Object obj)
        {
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var editor = UnityEditor.Editor.CreateEditor(prefab);
            var tex = editor.RenderStaticPreview(path, null, 200, 200);
            Object.DestroyImmediate(editor);
            return tex;
        }

        /// <summary>
        ///     Load an Asset from the Project folder.
        ///     Example: var mesh = PGAssetUtility.LoadAsset&lt;Mesh&gt;("Trunk", new[] { assetPath });
        /// </summary>
        /// <param name="assetName">Name of the Asset.</param>
        /// <param name="folders">Leave at null if the path could change.</param>
        public static T LoadAsset<T>(string assetName, string[] folders = null) where T : Object
        {
            var guids = AssetDatabase.FindAssets(assetName, folders);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var foundAsset = AssetDatabase.LoadAssetAtPath(path, typeof(T)) as T;
                if (foundAsset == null) continue;
                return foundAsset;
            }

            return null;
        }

        /// <summary>
        ///     Load all Assets from the Project folder that start with the name.
        ///     Example: Mesh[] meshes = PGAssetUtility.LoadAssets&lt;Mesh&gt;("Trunk", new[] { assetPath });
        /// </summary>
        /// <param name="assetName">Name of the Asset.</param>
        /// <param name="folders">Leave at null if the path could change.</param>
        public static T[] LoadAssets<T>(string assetName, string[] folders = null) where T : Object
        {
            var guids = AssetDatabase.FindAssets(assetName, folders);
            var foundAssets = new List<T>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var foundAsset = AssetDatabase.LoadAssetAtPath(path, typeof(T)) as T;
                if (foundAsset == null) continue;
                foundAssets.Add(foundAsset);
            }

            return foundAssets.ToArray();
        }

        /// <summary>
        ///     Load all assets of the specified type from the project folder.
        /// </summary>
        public static List<T> LoadAssetsOfType<T>() where T : Object
        {
            var assets = new List<T>();
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) assets.Add(asset);
            }

            return assets;
        }

        /// <summary>
        ///     Returns the GUID (Globally Unique Identifier) of the specified object.
        /// </summary>
        /// <param name="obj">The object whose GUID will be retrieved.</param>
        public static string GetObjectGUID(Object obj)
        {
            if (obj == null) return string.Empty;
            var terrainPath = GetAssetPath(obj.name);
            return AssetDatabase.AssetPathToGUID(terrainPath);
        }

        /// <summary>
        ///     Returns the first asset path found for the asset. Returns "" if nothing is found.
        /// </summary>
        public static string GetAssetPath(string assetName)
        {
            var guids = AssetDatabase.FindAssets(assetName);
            if (guids.Length <= 0) return string.Empty;
            var packageGUID = guids[0];
            var packagePath = AssetDatabase.GUIDToAssetPath(packageGUID);
            return packagePath;
        }

        /// <summary>
        ///     Checks whether the GameObject is a prefab in the project folder.
        /// </summary>
        /// <returns>True if it is a prefab.</returns>
        public static bool IsPrefab(GameObject obj)
        {
            return PrefabUtility.IsPartOfPrefabAsset(obj);
        }

        /// <summary>
        ///     Adds a scripting define symbol in ProjectSettings-Player so it can be used with #if.
        /// </summary>
        /// <param name="symbolName">Naming convention: "PROPERTY_ENGINE"</param>
        public static void AddScriptingDefineSymbol(string symbolName)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            var symbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            if (!symbols.Contains(symbolName))
            {
                symbols += ";" + symbolName;
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, symbols);
            }
        }
    }
}