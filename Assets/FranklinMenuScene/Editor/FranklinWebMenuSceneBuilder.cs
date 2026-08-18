using System.Collections.Generic;
using System.IO;
using FranklinGame.Menu.Web;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FranklinGame.Menu.Editor
{
    /// <summary>
    /// Removes the retired uGUI hierarchy and keeps the menu scene as a small,
    /// self-contained web bootstrap with its native host and runtime router.
    /// </summary>
    [InitializeOnLoad]
    public static class FranklinWebMenuSceneBuilder
    {
        private const string MenuScenePath =
            "Assets/FranklinMenuScene/FranklinMenuScene.unity";
        private const string GameplayScenePath = "Assets/Scenes/GamePlay.unity";
        private const string WebRootName = "FranklinWebMenuBridge";

        private static readonly HashSet<string> RetiredRootNames =
            new HashSet<string>
            {
                "Franklin Main Menu",
                "Franklin Menu Canvas",
                "EventSystem",
                "Franklin Mobile Settings",
                "Main Camera",
                "Directional Light"
            };

        static FranklinWebMenuSceneBuilder()
        {
            EditorApplication.delayCall += EnsureWebBootstrapScene;
        }

        [MenuItem("Tools/Franklin Game/Web Menu/Rebuild Web Bootstrap Scene")]
        public static void RebuildWebBootstrapScene()
        {
            BuildWebBootstrapScene(true);
        }

        [MenuItem("Tools/Franklin Game/Web Menu/Select Web Menu Scene")]
        public static void SelectWebMenuScene()
        {
            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
        }

        private static void EnsureWebBootstrapScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || !File.Exists(MenuScenePath))
            {
                return;
            }

            string sceneText = File.ReadAllText(MenuScenePath);
            bool hasWebRoot = sceneText.Contains("m_Name: " + WebRootName);
            bool hasWebHost = sceneText.Contains(
                "FranklinGame.Menu.Web.FranklinWebMenuHost"
            );
            bool hasWebRuntime = sceneText.Contains(
                "FranklinGame.Menu.Web.FranklinWebMenuRuntime"
            );
            bool hasRetiredRoot = false;
            foreach (string retiredRootName in RetiredRootNames)
            {
                if (!sceneText.Contains("m_Name: " + retiredRootName)) continue;
                hasRetiredRoot = true;
                break;
            }

            Scene loadedScene = SceneManager.GetSceneByPath(MenuScenePath);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                GameObject[] loadedRoots = loadedScene.GetRootGameObjects();
                for (int i = 0; i < loadedRoots.Length; ++i)
                {
                    string rootName = loadedRoots[i].name;
                    if (RetiredRootNames.Contains(rootName))
                    {
                        hasRetiredRoot = true;
                        break;
                    }
                    if (rootName == WebRootName)
                    {
                        hasWebHost = loadedRoots[i].GetComponent<FranklinWebMenuHost>() != null;
                        hasWebRuntime = loadedRoots[i].GetComponent<FranklinWebMenuRuntime>() != null;
                    }
                }
            }

            if (hasWebRoot && hasWebHost && hasWebRuntime && !hasRetiredRoot)
            {
                EnsureBuildSettings();
                return;
            }

            BuildWebBootstrapScene(false);
        }

        private static void BuildWebBootstrapScene(bool forceRecreate)
        {
            Scene menuScene = SceneManager.GetSceneByPath(MenuScenePath);
            bool closeWhenFinished = !menuScene.IsValid() || !menuScene.isLoaded;
            if (closeWhenFinished)
            {
                menuScene = EditorSceneManager.OpenScene(
                    MenuScenePath,
                    OpenSceneMode.Additive
                );
            }

            GameObject webRoot = null;
            GameObject[] roots = menuScene.GetRootGameObjects();
            for (int i = roots.Length - 1; i >= 0; --i)
            {
                GameObject root = roots[i];
                if (root.name == WebRootName)
                {
                    if (forceRecreate)
                    {
                        Object.DestroyImmediate(root);
                    }
                    else
                    {
                        webRoot = root;
                    }
                    continue;
                }

                if (RetiredRootNames.Contains(root.name))
                {
                    Object.DestroyImmediate(root);
                }
            }

            if (webRoot == null)
            {
                webRoot = new GameObject(WebRootName);
                SceneManager.MoveGameObjectToScene(webRoot, menuScene);
                webRoot.AddComponent<FranklinWebMenuBridge>();
            }
            else if (webRoot.GetComponent<FranklinWebMenuBridge>() == null)
            {
                webRoot.AddComponent<FranklinWebMenuBridge>();
            }

            if (webRoot.GetComponent<FranklinWebMenuHost>() == null)
            {
                webRoot.AddComponent<FranklinWebMenuHost>();
            }
            if (webRoot.GetComponent<FranklinWebMenuRuntime>() == null)
            {
                webRoot.AddComponent<FranklinWebMenuRuntime>();
            }

            EditorSceneManager.MarkSceneDirty(menuScene);
            EditorSceneManager.SaveScene(menuScene);
            EnsureBuildSettings();

            if (closeWhenFinished)
            {
                EditorSceneManager.CloseScene(menuScene, true);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Franklin web bootstrap scene saved with native WebView host. " +
                "Retired uGUI menu roots removed."
            );
        }

        private static void EnsureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>
                {
                    new EditorBuildSettingsScene(MenuScenePath, true),
                    new EditorBuildSettingsScene(GameplayScenePath, true)
                };

            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; ++i)
            {
                EditorBuildSettingsScene scene = existing[i];
                if (scene.path == MenuScenePath || scene.path == GameplayScenePath)
                {
                    continue;
                }
                scenes.Add(scene);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
