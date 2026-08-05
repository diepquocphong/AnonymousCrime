#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Veridian.BuildingSystem.Runtime;

namespace Veridian.BuildingSystem.Editor
{
    public class BuildingEditorWindow : EditorWindow
    {
        private BuildingProfileBase profileSource;
        private BuildingProfile activeProfile;

        private UnityEditor.Editor sourceEditor;
        private BuildingBuilder builder;

        private GameObject previewDummy;
        private Vector3 previewPosition = Vector3.zero;
        private Quaternion previewRotation = Quaternion.identity;
        private Vector2 scrollPos;

        // --- Phase 2: Undo Bake State Tracking ---
        private string lastBakedPrefabPath = string.Empty;
        private string lastBakedFolderPath = string.Empty;
        private GameObject lastBakedSceneObject = null;
        private List<string> lastBakedAssetPaths = new List<string>();
        private Vector3 lastPreviewIncrement = Vector3.zero;

        [MenuItem("Tools/Veridian/BuildingBuilder")]
        public static void ShowWindow()
        {
            GetWindow<BuildingEditorWindow>("Building Builder").minSize = new Vector2(300, 450);
        }

        // =========================================================================================
        // MAJOR LOGICAL BLOCK: EDITOR LIFECYCLE & INITIALIZATION
        // Hooks into the Scene GUI and maintains Editor state persistence. Cleans up ghost profiles.
        // =========================================================================================
        private void OnEnable()
        {
            builder = new BuildingBuilder();
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += UpdatePreview;

            CleanupGhostPreviews();
            CleanupGhostProfiles();

            if (profileSource != null)
            {
                RefreshActiveProfile();
                UpdatePreview();
            }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= UpdatePreview;

            DestroyPreviewGameObject(previewDummy);
            DestroyActiveProfileIfTemporary();
            if (sourceEditor != null) DestroyImmediate(sourceEditor);
        }

        private void CleanupGhostProfiles()
        {
            var ghostProfiles = Resources.FindObjectsOfTypeAll<BuildingProfile>()
                .Where(p => p.name.StartsWith("Dynamic_Temp_BuildingProfile") && (p.hideFlags & HideFlags.HideAndDontSave) != 0);
            foreach (var ghost in ghostProfiles.ToList()) DestroyImmediate(ghost);
        }

        private void CleanupGhostPreviews()
        {
            var ghosts = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.name == "[PREVIEW] Procedural Building" && (go.hideFlags & HideFlags.HideAndDontSave) != 0);
            foreach (var ghost in ghosts.ToList()) DestroyPreviewGameObject(ghost);
        }

        private void DestroyActiveProfileIfTemporary()
        {
            if (activeProfile != null && activeProfile != profileSource)
            {
                DestroyImmediate(activeProfile);
            }
            activeProfile = null;
        }

        private void RefreshActiveProfile()
        {
            DestroyActiveProfileIfTemporary();

            if (profileSource is BuildingFamilyProfile family)
            {
                activeProfile = family.GenerateRandomProfile();
            }
            else if (profileSource is BuildingProfile profile)
            {
                activeProfile = profile;
            }
            else
            {
                activeProfile = null;
            }
        }

        // =========================================================================================
        // MAJOR LOGICAL BLOCK: EDITOR UI RENDERING (ON-GUI)
        // Renders the inspector window, dynamic property scroll views, generation controls, and UX hints.
        // =========================================================================================
        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Procedural Building Builder", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            profileSource = (BuildingProfileBase)EditorGUILayout.ObjectField("Building Source", profileSource, typeof(BuildingProfileBase), false);

            if (EditorGUI.EndChangeCheck())
            {
                if (sourceEditor != null) DestroyImmediate(sourceEditor);
                if (profileSource != null) sourceEditor = UnityEditor.Editor.CreateEditor(profileSource);

                RefreshActiveProfile();
                UpdatePreview();
            }

            if (profileSource == null || activeProfile == null)
            {
                EditorGUILayout.HelpBox("Assign a deterministic Profile or a dynamic Family to generate.", MessageType.Info);
                return;
            }

            if (sourceEditor == null) sourceEditor = UnityEditor.Editor.CreateEditor(profileSource);

            EditorGUI.BeginChangeCheck();
            previewPosition = EditorGUILayout.Vector3Field("Preview Position", previewPosition);
            Vector3 euler = EditorGUILayout.Vector3Field("Preview Rotation", previewRotation.eulerAngles);

            if (EditorGUI.EndChangeCheck())
            {
                previewRotation = Quaternion.Euler(euler);
                if (previewDummy != null)
                {
                    previewDummy.transform.position = previewPosition;
                    previewDummy.transform.rotation = previewRotation;
                    SceneView.RepaintAll();
                }
            }

            if (profileSource is BuildingFamilyProfile)
            {
                GUILayout.Space(10);
                GUI.backgroundColor = new Color(0.9f, 0.7f, 0.3f);
                if (GUILayout.Button("🎲 Re-roll Variant", GUILayout.Height(30)))
                {
                    RefreshActiveProfile();
                    UpdatePreview();
                }
                GUI.backgroundColor = Color.white;
            }

            if (sourceEditor != null)
            {
                GUILayout.Space(5);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                sourceEditor.OnInspectorGUI();

                if (EditorGUI.EndChangeCheck())
                {
                    if (profileSource is BuildingProfile)
                    {
                        UpdatePreview();
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            GUILayout.Space(15);
            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button("Bake Prefab to Scene", GUILayout.Height(40)))
            {
                BakeBuilding();
            }
            GUI.backgroundColor = Color.white;

            if (!string.IsNullOrEmpty(lastBakedPrefabPath) || lastBakedSceneObject != null)
            {
                GUILayout.Space(10);
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("Undo Last Bake", GUILayout.Height(30)))
                {
                    UndoLastBake();
                }
                GUI.backgroundColor = Color.white;
            }

            // --- Editor UX Guardrail ---
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("💡 Reminder: The preview building spawns dynamically at the specified coordinates within your active scene. Once finalized, click 'Bake Prefab to Scene' above to permanently save it as an Asset.", MessageType.Info);
            GUILayout.Space(10);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (activeProfile == null || previewDummy == null) return;

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(previewPosition, previewRotation);
            Quaternion newRot = Handles.RotationHandle(previewRotation, previewPosition);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Move Preview");
                previewPosition = newPos;
                previewRotation = newRot;

                previewDummy.transform.position = previewPosition;
                previewDummy.transform.rotation = previewRotation;
                Repaint();
            }
        }

        private void UpdatePreview()
        {
            DestroyPreviewGameObject(previewDummy);
            if (activeProfile == null) return;

            var meshData = builder.Generate(activeProfile);
            previewDummy = BuildingFactory.CreateBuilding(activeProfile, meshData, "[PREVIEW] Procedural Building");
            previewDummy.transform.position = previewPosition;
            previewDummy.transform.rotation = previewRotation;

            SetHideFlagsRecursively(previewDummy, HideFlags.HideAndDontSave);
            SceneView.RepaintAll();
        }

        private void DestroyPreviewGameObject(GameObject go)
        {
            if (go == null) return;

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !EditorUtility.IsPersistent(mf.sharedMesh)) DestroyImmediate(mf.sharedMesh);

            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.sharedMaterials == null) continue;
                foreach (var mat in mr.sharedMaterials)
                    if (mat != null && !EditorUtility.IsPersistent(mat)) DestroyImmediate(mat);
            }

            DestroyImmediate(go);
        }

        private void SetHideFlagsRecursively(GameObject obj, HideFlags flags)
        {
            obj.hideFlags = flags;
            foreach (Transform child in obj.transform) SetHideFlagsRecursively(child.gameObject, flags);
        }

        private void SetStaticRecursively(GameObject obj, bool isStatic)
        {
            obj.isStatic = isStatic;
            foreach (Transform child in obj.transform) SetStaticRecursively(child.gameObject, isStatic);
        }

        // =========================================================================================
        // MAJOR LOGICAL BLOCK: BAKE BUILDING TO PREFAB
        // Orchestrates the extraction of the dynamically previewed procedural mesh into a static, 
        // persistent prefab. Automatically initiates the sub-folder asset extraction pipeline.
        // =========================================================================================
        private void BakeBuilding()
        {
            if (activeProfile == null) return;

            string defaultPath = "Assets/Buildings/";
            if (!Directory.Exists(defaultPath)) Directory.CreateDirectory(defaultPath);

            string defaultName = "NewBuilding";
            if (profileSource != null)
            {
                if (profileSource is BuildingFamilyProfile)
                    defaultName = profileSource.name + "_Variant_" + Random.Range(1000, 9999);
                else if (!string.IsNullOrEmpty(activeProfile.name))
                    defaultName = activeProfile.name;
            }

            string path = EditorUtility.SaveFilePanelInProject("Save Static Prefab", defaultName, "prefab", "Save location", defaultPath);
            if (string.IsNullOrEmpty(path)) return;

            string prefabName = Path.GetFileNameWithoutExtension(path);

            var meshData = builder.Generate(activeProfile);
            GameObject bakeObject = BuildingFactory.CreateBuilding(activeProfile, meshData, prefabName);
            bakeObject.transform.position = previewPosition;
            bakeObject.transform.rotation = previewRotation;
            SetStaticRecursively(bakeObject, true);

            // Establish the Dedicated Companion Folder path
            string parentDir = Path.GetDirectoryName(path).Replace("\\", "/");
            string assetFolderPath = parentDir + "/" + prefabName + "_Assets";

            if (!ProcessAndSaveAssets(bakeObject, assetFolderPath, prefabName))
            {
                DestroyPreviewGameObject(bakeObject);
                return;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(bakeObject, path, InteractionMode.UserAction, out bool prefabSuccess);

            if (prefabSuccess)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"<color=green>Prefab successfully baked to {path}</color>");
                EditorGUIUtility.PingObject(savedPrefab);

                Undo.RegisterCreatedObjectUndo(bakeObject, "Bake Procedural Building");
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Selection.activeGameObject = bakeObject;

                // Capture state for the Undo Bake fail-safe mechanism
                lastBakedPrefabPath = path;
                lastBakedFolderPath = assetFolderPath;
                lastBakedSceneObject = bakeObject;
                lastPreviewIncrement = new Vector3(activeProfile.width + 2f, 0, 0);

                previewPosition += lastPreviewIncrement;

                if (profileSource is BuildingFamilyProfile) RefreshActiveProfile();
                UpdatePreview();
            }
            else
            {
                DestroyPreviewGameObject(bakeObject);
            }
        }

        // =========================================================================================
        // MAJOR LOGICAL BLOCK: PHASE 1 FOLDER LOGIC & ASSET EXTRACTION
        // Traverses the baked prefab's hierarchy. Unmanaged procedural Meshes and Materials
        // are explicitly serialized into .asset and .mat files inside a clean companion folder.
        // Naming conventions are optimized to prevent Asset Directory visual bloat.
        // =========================================================================================
        private bool ProcessAndSaveAssets(GameObject cloneObject, string assetFolderPath, string prefabName)
        {
            if (!AssetDatabase.IsValidFolder(assetFolderPath))
            {
                string parentPath = Path.GetDirectoryName(assetFolderPath).Replace("\\", "/");
                string folderName = Path.GetFileName(assetFolderPath);
                AssetDatabase.CreateFolder(parentPath, folderName);
            }

            lastBakedAssetPaths.Clear();

            MeshFilter[] meshFilters = cloneObject.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters.Length > 0 && !meshFilters.All(mf => mf.sharedMesh == null))
            {
                foreach (MeshFilter mf in meshFilters)
                {
                    Mesh meshInstance = mf.sharedMesh;
                    // Ensure we do not attempt to serialize built-in Unity meshes or user project assets
                    if (meshInstance != null && !EditorUtility.IsPersistent(meshInstance))
                    {
                        // Clean UX Naming: Extract "LODx" or default to "MainMesh" to eliminate concatenation bloat
                        string objName = mf.gameObject.name;
                        string baseMeshName = "MainMesh";

                        if (objName.Contains("LOD0")) baseMeshName = "LOD0";
                        else if (objName.Contains("LOD1")) baseMeshName = "LOD1";
                        else if (objName.Contains("LOD2")) baseMeshName = "LOD2";

                        string uniqueMeshName = baseMeshName;
                        string uniqueMeshPath = assetFolderPath + "/" + uniqueMeshName + ".asset";

                        // Maintain robust collision-fallback generator with a clean D2 padded suffix
                        int suffix = 1;
                        while (lastBakedAssetPaths.Contains(uniqueMeshPath))
                        {
                            uniqueMeshName = baseMeshName + "_" + suffix.ToString("D2");
                            uniqueMeshPath = assetFolderPath + "/" + uniqueMeshName + ".asset";
                            suffix++;
                        }

                        meshInstance.name = uniqueMeshName;

                        // If file exists from a previous bake, delete it to prevent Unity errors and bloat
                        if (AssetDatabase.LoadAssetAtPath<Object>(uniqueMeshPath) != null)
                        {
                            AssetDatabase.DeleteAsset(uniqueMeshPath);
                        }

                        AssetDatabase.CreateAsset(meshInstance, uniqueMeshPath);
                        lastBakedAssetPaths.Add(uniqueMeshPath);
                    }
                }
            }

            MeshRenderer[] meshRenderers = cloneObject.GetComponentsInChildren<MeshRenderer>(true);
            Dictionary<string, Material> savedMaterials = new Dictionary<string, Material>();

            foreach (MeshRenderer mr in meshRenderers)
            {
                Material[] sharedMats = mr.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < sharedMats.Length; i++)
                {
                    Material matInstance = sharedMats[i];

                    if (matInstance != null && !EditorUtility.IsPersistent(matInstance))
                    {
                        string colorKey = "NoColor";
                        if (matInstance.HasProperty("_Color")) colorKey = matInstance.color.ToString();
                        else if (matInstance.HasProperty("_BaseColor")) colorKey = matInstance.GetColor("_BaseColor").ToString();

                        string instanceKey = matInstance.shader.name + "_" + colorKey;

                        if (savedMaterials.ContainsKey(instanceKey))
                        {
                            sharedMats[i] = savedMaterials[instanceKey];
                            DestroyImmediate(matInstance); // Cleanup duplicate from memory
                        }
                        else
                        {
                            // Clean UX Naming: Uniform, sequential material names (e.g. Mat_01, Mat_02)
                            string matName = "Mat_" + (savedMaterials.Count + 1).ToString("D2");
                            matInstance.name = matName;
                            string matPath = assetFolderPath + "/" + matName + ".mat";

                            if (AssetDatabase.LoadAssetAtPath<Object>(matPath) != null)
                            {
                                AssetDatabase.DeleteAsset(matPath);
                            }

                            AssetDatabase.CreateAsset(matInstance, matPath);
                            savedMaterials.Add(instanceKey, matInstance);
                            lastBakedAssetPaths.Add(matPath);
                        }
                        changed = true;
                    }
                }
                if (changed) mr.sharedMaterials = sharedMats;
            }

            return true;
        }

        // =========================================================================================
        // MAJOR LOGICAL BLOCK: PHASE 2 UNDO STATE TRACKING
        // Fail-safe mechanism for the user. Completely reverses the previous Bake operation
        // by deleting the generated prefab, its specific exported companion assets, and gracefully
        // sweeping the companion folder entirely if it was natively left empty.
        // =========================================================================================
        private void UndoLastBake()
        {
            if (string.IsNullOrEmpty(lastBakedPrefabPath) && lastBakedSceneObject == null) return;

            bool confirm = EditorUtility.DisplayDialog("Undo Last Bake",
                "Are you sure you want to delete the last baked prefab and its generated assets?",
                "Yes, Delete", "Cancel");

            if (!confirm) return;

            bool performedUndo = false;

            if (lastBakedSceneObject != null)
            {
                Undo.DestroyObjectImmediate(lastBakedSceneObject);
                lastBakedSceneObject = null;
                performedUndo = true;
            }

            foreach (string assetPath in lastBakedAssetPaths)
            {
                if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    performedUndo = true;
                }
            }

            if (!string.IsNullOrEmpty(lastBakedFolderPath) && AssetDatabase.IsValidFolder(lastBakedFolderPath))
            {
                string[] remainingAssets = AssetDatabase.FindAssets("", new[] { lastBakedFolderPath });
                if (remainingAssets.Length == 0)
                {
                    AssetDatabase.DeleteAsset(lastBakedFolderPath);
                    performedUndo = true;
                }
                else
                {
                    Debug.LogWarning($"<color=yellow>Companion folder '{lastBakedFolderPath}' kept alive because it contains pre-existing or user-added assets.</color>");
                }
            }

            if (!string.IsNullOrEmpty(lastBakedPrefabPath) && AssetDatabase.LoadAssetAtPath<Object>(lastBakedPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(lastBakedPrefabPath);
                performedUndo = true;
            }

            if (performedUndo)
            {
                AssetDatabase.Refresh();
                Debug.Log("<color=orange>Undo Bake Successful: Cleaned up the most recently baked prefab and its generated assets.</color>");
            }

            previewPosition -= lastPreviewIncrement;
            lastPreviewIncrement = Vector3.zero;
            UpdatePreview();

            lastBakedPrefabPath = null;
            lastBakedFolderPath = null;
            lastBakedAssetPaths.Clear();
        }
    }
}
#endif