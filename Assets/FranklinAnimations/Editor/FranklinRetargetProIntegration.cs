using System;
using GameCreator.Runtime.Characters;
using KINEMATION.RetargetPro.Editor.Scripts.Mapping;
using KINEMATION.RetargetPro.Editor.Scripts.Window;
using KINEMATION.RetargetPro.Runtime;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Animations.Editor
{
    /// <summary>
    /// Editor-only Retarget Pro setup for previewing and baking GC2 Humanoid clips
    /// onto Franklin. Retarget Pro runtime components are deliberately not added to
    /// the Player because GC2 already owns its Animator PlayableGraph.
    /// </summary>
    public static class FranklinRetargetProIntegration
    {
        private const string RootFolder = "Assets/FranklinAnimations/RetargetPro";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string BakedFolder = RootFolder + "/Baked";
        private const string ProfilePath = ProfileFolder + "/GC2_Mannequin_To_Franklin.asset";
        private const string PlayerPath = "Assets/Prefab/Player.prefab";
        private const string SourceModelPath =
            "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/3D/Mannequin.fbx";
        private const string TargetModelPath = "Assets/Character/Franklin/franklin 1.fbx";
        private const string ReferencePosePath =
            "Assets/KINEMATION/RetargetPro/Poses/A_TPose_Humanoid.anim";
        private const string Gc2ControllerPath =
            "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/Controllers/CompleteLocomotion.controller";

        [MenuItem("Tools/Franklin Game/Retarget Pro/Setup GC2 to Franklin profile")]
        public static void SetupProfile()
        {
            RetargetProfile profile = CreateOrUpdateProfile();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            Debug.Log(
                "Retarget Pro profile is ready. It is editor-only; Player and GC2 runtime components were not changed."
            );
        }

        [MenuItem("Tools/Franklin Game/Retarget Pro/Open GC2 to Franklin baker")]
        public static void OpenBaker()
        {
            RetargetProfile profile = CreateOrUpdateProfile();
            RetargetProWindow.ShowWindow(profile);
        }

        [MenuItem("Tools/Franklin Game/Retarget Pro/Validate safe integration")]
        public static void ValidateIntegration()
        {
            RetargetProfile profile = AssetDatabase.LoadAssetAtPath<RetargetProfile>(ProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException($"Retarget Pro profile is missing: {ProfilePath}");
            }

            if (profile.sourceCharacter == null || profile.targetCharacter == null ||
                profile.sourceRig == null || profile.targetRig == null)
            {
                throw new InvalidOperationException("Retarget Pro profile has incomplete model or rig references");
            }

            if (profile.retargetFeatures == null || profile.retargetFeatures.Count == 0)
            {
                throw new InvalidOperationException("Retarget Pro profile has no mapped retarget features");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                Character character = root.GetComponent<Character>();
                Animator animator = root.GetComponentInChildren<Animator>(true);
                DynamicRetargeter dynamicRetargeter = root.GetComponentInChildren<DynamicRetargeter>(true);

                if (character == null || animator == null || !animator.isHuman)
                {
                    throw new InvalidOperationException("Player must retain its GC2 Character and Humanoid Animator");
                }

                if (dynamicRetargeter != null)
                {
                    throw new InvalidOperationException(
                        "DynamicRetargeter must not be attached to Player because GC2 owns the Animator graph"
                    );
                }

                string controllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                if (controllerPath != Gc2ControllerPath)
                {
                    throw new InvalidOperationException("Player must retain GC2 CompleteLocomotion.controller");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log(
                $"Retarget Pro validation passed: {profile.retargetFeatures.Count} mapped features, " +
                "GC2 controller retained and no runtime DynamicRetargeter on Player."
            );
        }

        public static void SetupProfileBatch()
        {
            CreateOrUpdateProfile();
            ValidateIntegration();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        private static RetargetProfile CreateOrUpdateProfile()
        {
            EnsureFolder(ProfileFolder);
            EnsureFolder(BakedFolder);

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
            GameObject target = AssetDatabase.LoadAssetAtPath<GameObject>(TargetModelPath);
            AnimationClip referencePose = AssetDatabase.LoadAssetAtPath<AnimationClip>(ReferencePosePath);

            if (source == null) throw new InvalidOperationException($"GC2 source model is missing: {SourceModelPath}");
            if (target == null) throw new InvalidOperationException($"Franklin target model is missing: {TargetModelPath}");
            if (referencePose == null)
            {
                throw new InvalidOperationException($"Retarget Pro reference pose is missing: {ReferencePosePath}");
            }

            RetargetProfile profile = AssetDatabase.LoadAssetAtPath<RetargetProfile>(ProfilePath);
            bool created = profile == null;
            if (created)
            {
                profile = ScriptableObject.CreateInstance<RetargetProfile>();
                profile.name = "GC2_Mannequin_To_Franklin";
                profile.saveFolderPath = BakedFolder;
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            bool modelsChanged = profile.sourceCharacter != source || profile.targetCharacter != target;
            profile.sourceCharacter = source;
            profile.targetCharacter = target;
            profile.sourcePose = referencePose;
            profile.targetPose = referencePose;
            profile.saveFolderPath = BakedFolder;
            EditorUtility.SetDirty(profile);

            bool needsMapping = created || modelsChanged || profile.sourceRig == null ||
                                profile.targetRig == null || profile.retargetFeatures == null ||
                                profile.retargetFeatures.Count == 0;
            if (needsMapping &&
                !RetargetProfileModelRigUtility.TryComposeProfileRigs(profile, true, out string message))
            {
                throw new InvalidOperationException($"Could not map GC2 to Franklin: {message}");
            }

            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; ++i)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
