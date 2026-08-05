using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FranklinGame.Animations;
using GameCreator.Runtime.Characters;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Animations.Editor
{
    public static class FranklinAnimationInstaller
    {
        private const string RootFolder = "Assets/FranklinAnimations";
        private const string StagingFolder = RootFolder + "/Editor/Staging";
        private const string AnimationFolder = RootFolder + "/Animations";
        private const string StateFolder = RootFolder + "/States";

        private const string RgIdleAPath = StagingFolder + "/RG_Idle_A.fbx";
        private const string RgIdleBPath = StagingFolder + "/RG_Idle_B.fbx";
        private const string RgLookingAroundPath = StagingFolder + "/RG_LookingAround.fbx";
        private const string SprintClipPath = AnimationFolder + "/IP-sprint-loop-smooth.anim";
        private const string LegacyLocomotionStatePath = StateFolder + "/Franklin_GTA_Locomotion.asset";
        private const string LegacyWalkClipPath = AnimationFolder + "/Franklin_Walk.anim";
        private const string LegacyJumpStartPath = AnimationFolder + "/Franklin_Jump_Start.anim";
        private const string LegacyJumpAirPath = AnimationFolder + "/Franklin_Jump_Air.anim";
        private const string LegacyJumpLandPath = AnimationFolder + "/Franklin_Jump_Land.anim";
        private const string LegacyIdlePath = AnimationFolder + "/Franklin_Idle.anim";
        private const string LegacyIdleFoldArmsPath = AnimationFolder + "/Franklin_Idle_FoldArms.anim";
        private const string LegacyIdleGesturePath = AnimationFolder + "/Franklin_Idle_Gesture.anim";
        private const string SprintStatePath = StateFolder + "/Franklin_GTA_Sprint.asset";
        private const string PlayerPath = "Assets/Prefab/Player.prefab";
        private const string Gc2ControllerPath =
            "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/Controllers/CompleteLocomotion.controller";
        private const string Gc2LocomotionClipsFolder =
            "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/3D/Animations/Locomotion";

        private static readonly ClipRequest[] ClipRequests =
        {
            new ClipRequest(RgLookingAroundPath, "LookingAround", "Franklin_Idle_LookAround", false),
            new ClipRequest(RgIdleAPath, "Idle_A", "Franklin_Idle_Natural_A", false),
            new ClipRequest(RgIdleBPath, "Idle_B", "Franklin_Idle_Natural_B", false)
        };

        [MenuItem("Tools/Franklin Game/Install GTA-style animations")]
        public static void Install()
        {
            EnsureFolder(AnimationFolder);
            EnsureFolder(StateFolder);

            ConfigureSourceModel(RgIdleAPath);
            ConfigureSourceModel(RgIdleBPath);
            ConfigureSourceModel(RgLookingAroundPath);

            Dictionary<string, AnimationClip> clips = ExtractClips();
            AnimationClip sprintClip = LoadSprintClip();
            AssetDatabase.DeleteAsset(LegacyLocomotionStatePath);
            AssetDatabase.DeleteAsset(LegacyWalkClipPath);
            AssetDatabase.DeleteAsset(LegacyJumpStartPath);
            AssetDatabase.DeleteAsset(LegacyJumpAirPath);
            AssetDatabase.DeleteAsset(LegacyJumpLandPath);
            AssetDatabase.DeleteAsset(LegacyIdlePath);
            AssetDatabase.DeleteAsset(LegacyIdleFoldArmsPath);
            AssetDatabase.DeleteAsset(LegacyIdleGesturePath);
            StateCompleteLocomotion sprintState = CreateLocomotionState(
                SprintStatePath,
                "Franklin_GTA_Sprint",
                sprintClip,
                6f
            );
            ConfigurePlayerPrefab(sprintState, clips);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateInstallation(sprintState, clips, sprintClip);

            // The extracted .anim files are self-contained. Removing the source FBX
            // files keeps the project and mobile build lean while preserving the CC0 license file.
            AssetDatabase.DeleteAsset(RgIdleAPath);
            AssetDatabase.DeleteAsset(RgIdleBPath);
            AssetDatabase.DeleteAsset(RgLookingAroundPath);
            AssetDatabase.Refresh();

            Debug.Log("Franklin animations installed: GC2 default locomotion/jump, Left Shift sprint and idle variations. GC2 files were not modified.");
        }

        [MenuItem("Tools/Franklin Game/Apply IP sprint clip")]
        public static void ApplySprintClip()
        {
            Dictionary<string, AnimationClip> clips = LoadExtractedClips();
            AnimationClip sprintClip = LoadSprintClip();
            StateCompleteLocomotion sprintState = CreateLocomotionState(
                SprintStatePath,
                "Franklin_GTA_Sprint",
                sprintClip,
                6f
            );
            ConfigurePlayerPrefab(sprintState, clips);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateInstallation(sprintState, clips, sprintClip);
            ValidatePlayerPrefab(sprintState);

            Debug.Log("IP-sprint-loop-smooth is now the Left Shift run clip. GC2 files were not modified.");
        }

        [MenuItem("Tools/Franklin Game/Validate GTA-style animations")]
        public static void ValidateProject()
        {
            Dictionary<string, AnimationClip> clips = LoadExtractedClips();
            AnimationClip sprintClip = LoadSprintClip();

            StateCompleteLocomotion sprintState = AssetDatabase.LoadAssetAtPath<StateCompleteLocomotion>(SprintStatePath);
            ValidateInstallation(sprintState, clips, sprintClip);
            ValidatePlayerPrefab(sprintState);

            Debug.Log("Franklin animation validation passed: clips, GC2 State, Humanoid rig and Player references are valid.");
        }

        private static Dictionary<string, AnimationClip> LoadExtractedClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (ClipRequest request in ClipRequests)
            {
                string path = $"{AnimationFolder}/{request.TargetName}.anim";
                clips[request.TargetName] = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }

            return clips;
        }

        private static AnimationClip LoadSprintClip()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SprintClipPath);
            if (clip == null || clip.empty)
            {
                throw new FileNotFoundException($"Sprint clip is missing or empty: {SprintClipPath}");
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (!settings.loopTime)
            {
                throw new InvalidOperationException("IP-sprint-loop-smooth must have Loop Time enabled");
            }

            return clip;
        }

        private static void ConfigureSourceModel(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Animation source is missing: {path}");
            }

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate
            );

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException($"No ModelImporter for {path}");

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;

            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation take in takes)
            {
                string shortName = ShortClipName(take.name);
                bool loop = ClipRequests.Any(request =>
                    request.SourcePath == path && request.SourceName == shortName && request.Loop
                );

                take.loopTime = loop;
                take.loopPose = loop;
                take.keepOriginalOrientation = true;
                take.keepOriginalPositionY = true;
                take.keepOriginalPositionXZ = true;
                take.lockRootRotation = true;
                take.lockRootHeightY = true;
                take.lockRootPositionXZ = true;
            }

            importer.clipAnimations = takes;
            importer.SaveAndReimport();
        }

        private static Dictionary<string, AnimationClip> ExtractClips()
        {
            var result = new Dictionary<string, AnimationClip>();

            foreach (ClipRequest request in ClipRequests)
            {
                AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(request.SourcePath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(clip => ShortClipName(clip.name) == request.SourceName);

                if (source == null)
                {
                    string available = string.Join(", ", AssetDatabase
                        .LoadAllAssetsAtPath(request.SourcePath)
                        .OfType<AnimationClip>()
                        .Select(clip => clip.name));

                    throw new InvalidOperationException(
                        $"Clip '{request.SourceName}' not found in {request.SourcePath}. Available: {available}"
                    );
                }

                string targetPath = $"{AnimationFolder}/{request.TargetName}.anim";
                AssetDatabase.DeleteAsset(targetPath);

                AnimationClip copy = UnityEngine.Object.Instantiate(source);
                copy.name = request.TargetName;
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = request.Loop;
                settings.loopBlend = request.Loop;
                settings.loopBlendOrientation = request.Loop;
                settings.loopBlendPositionY = request.Loop;
                settings.loopBlendPositionXZ = request.Loop;
                AnimationUtility.SetAnimationClipSettings(copy, settings);

                AssetDatabase.CreateAsset(copy, targetPath);
                result.Add(request.TargetName, copy);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static StateCompleteLocomotion CreateLocomotionState(
            string statePath,
            string stateName,
            AnimationClip locomotion,
            float movementSpeed)
        {
            AssetDatabase.DeleteAsset(statePath);

            RuntimeAnimatorController baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                Gc2ControllerPath
            );
            if (baseController == null)
            {
                throw new FileNotFoundException($"GC2 locomotion controller is missing: {Gc2ControllerPath}");
            }

            StateCompleteLocomotion state = ScriptableObject.CreateInstance<StateCompleteLocomotion>();
            state.name = stateName;
            AssetDatabase.CreateAsset(state, statePath);

            var overrideController = new AnimatorOverrideController(baseController)
            {
                name = stateName + "_Controller"
            };
            AssetDatabase.AddObjectToAsset(overrideController, state);

            SerializedObject serializedState = new SerializedObject(state);
            serializedState.FindProperty("m_Controller").objectReferenceValue = overrideController;

            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"{Gc2LocomotionClipsFolder}/Human@Stand_Idle.anim"
            );
            if (idle == null)
            {
                throw new FileNotFoundException("GC2 default standing idle clip is missing");
            }

            ConfigureLocomotionPoints(serializedState.FindProperty("m_Stand16Points"), idle, locomotion);
            ConfigureLocomotionPoints(serializedState.FindProperty("m_Land16Points"), idle, locomotion);
            ConfigureGc2Airborne(serializedState);
            ConfigureMovementSpeed(serializedState.FindProperty("m_Properties"), movementSpeed);
            serializedState.ApplyModifiedPropertiesWithoutUndo();

            ApplyControllerOverrides(overrideController, idle, locomotion);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(overrideController);
            AssetDatabase.SaveAssets();
            return state;
        }

        private static void ConfigureLocomotionPoints(
            SerializedProperty points,
            AnimationClip idle,
            AnimationClip run)
        {
            points.FindPropertyRelative("m_Idle").objectReferenceValue = idle;

            string[] directions =
            {
                "m_ForwardFast", "m_BackwardFast", "m_RightFast", "m_LeftFast",
                "m_ForwardRightFast", "m_ForwardLeftFast", "m_BackwardRightFast", "m_BackwardLeftFast",
                "m_ForwardSlow", "m_BackwardSlow", "m_RightSlow", "m_LeftSlow",
                "m_ForwardRightSlow", "m_ForwardLeftSlow", "m_BackwardRightSlow", "m_BackwardLeftSlow"
            };

            foreach (string direction in directions)
            {
                points.FindPropertyRelative(direction).objectReferenceValue = run;
            }
        }

        private static void ConfigureMovementSpeed(SerializedProperty properties, float movementSpeed)
        {
            string[] names =
            {
                "m_IsControllable", "m_Speed", "m_Rotation", "m_Mass", "m_Height", "m_Radius",
                "m_GravityUpwards", "m_GravityDownwards", "m_TerminalVelocity", "m_UseAcceleration",
                "m_Acceleration", "m_Deceleration", "m_CanJump", "m_AirJumps", "m_JumpForce",
                "m_JumpCooldown", "m_DashInSuccession", "m_DashInAir", "m_DashCooldown"
            };

            foreach (string name in names)
            {
                SerializedProperty enabled = properties
                    .FindPropertyRelative(name)
                    ?.FindPropertyRelative("m_IsEnabled");
                if (enabled != null) enabled.boolValue = false;
            }

            SerializedProperty speed = properties.FindPropertyRelative("m_Speed");
            speed.FindPropertyRelative("m_IsEnabled").boolValue = true;
            speed.FindPropertyRelative("m_Value").floatValue = movementSpeed;
        }

        private static void ConfigureGc2Airborne(SerializedObject serializedState)
        {
            serializedState.FindProperty("m_AirborneMode").enumValueIndex = 2; // Directional
            SerializedProperty airborne = serializedState.FindProperty("m_AirborneDirectional");

            string[] propertyNames =
            {
                "m_UpIdle", "m_UpForward", "m_UpBackward", "m_UpLeft", "m_UpRight",
                "m_DownIdle", "m_DownForward", "m_DownBackward", "m_DownLeft", "m_DownRight"
            };
            string[] clipNames =
            {
                "Human@Air_Up_I", "Human@Air_Up_F", "Human@Air_Up_B", "Human@Air_Up_L", "Human@Air_Up_R",
                "Human@Air_Down_I", "Human@Air_Down_F", "Human@Air_Down_B", "Human@Air_Down_L", "Human@Air_Down_R"
            };

            for (int i = 0; i < propertyNames.Length; ++i)
            {
                string path = $"{Gc2LocomotionClipsFolder}/{clipNames[i]}.anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) throw new FileNotFoundException($"GC2 airborne clip is missing: {path}");
                airborne.FindPropertyRelative(propertyNames[i]).objectReferenceValue = clip;
            }
        }

        private static void ApplyControllerOverrides(
            AnimatorOverrideController controller,
            AnimationClip idle,
            AnimationClip run)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; ++i)
            {
                AnimationClip original = overrides[i].Key;
                AnimationClip replacement = null;

                if (original.name == "Human@Stand_Idle" || original.name == "Human@Crouch_Idle")
                {
                    replacement = idle;
                }
                else if (original.name.Contains("@Stand_") || original.name.Contains("@Crouch_"))
                {
                    replacement = run;
                }
                if (replacement != null)
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
                }
            }

            controller.ApplyOverrides(overrides);
        }

        private static void ConfigurePlayerPrefab(
            StateCompleteLocomotion sprintState,
            IReadOnlyDictionary<string, AnimationClip> clips)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                Character character = root.GetComponent<Character>();
                if (character == null) throw new MissingComponentException("Player prefab has no GC2 Character");

                SerializedObject serializedCharacter = new SerializedObject(character);
                SerializedProperty startState = serializedCharacter.FindProperty("m_Kernel.m_Animim.m_StartState");
                if (startState == null)
                {
                    throw new InvalidOperationException("GC2 start-state property was not found on Player.prefab");
                }

                // Keep GC2's original locomotion as the normal/default behavior.
                startState.objectReferenceValue = null;
                serializedCharacter.ApplyModifiedPropertiesWithoutUndo();

                FranklinAnimationBridge bridge = root.GetComponent<FranklinAnimationBridge>();
                if (bridge == null) bridge = root.AddComponent<FranklinAnimationBridge>();

                SerializedObject serializedBridge = new SerializedObject(bridge);
                serializedBridge.FindProperty("m_SprintState").objectReferenceValue = sprintState;
                SerializedProperty idleVariations = serializedBridge.FindProperty("m_IdleVariations");
                string[] idleNames =
                {
                    "Franklin_Idle_LookAround",
                    "Franklin_Idle_Natural_A",
                    "Franklin_Idle_Natural_B"
                };
                idleVariations.arraySize = idleNames.Length;
                for (int i = 0; i < idleNames.Length; ++i)
                {
                    idleVariations.GetArrayElementAtIndex(i).objectReferenceValue = clips[idleNames[i]];
                }
                serializedBridge.ApplyModifiedPropertiesWithoutUndo();

                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                {
                    throw new InvalidOperationException("Franklin Animator must use a valid Humanoid Avatar");
                }

                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateInstallation(
            StateCompleteLocomotion sprintState,
            IReadOnlyDictionary<string, AnimationClip> clips,
            AnimationClip sprintClip)
        {
            if (sprintState == null || sprintState.StateController == null)
            {
                throw new InvalidOperationException("Franklin sprint state was not created correctly");
            }

            foreach (ClipRequest request in ClipRequests)
            {
                AnimationClip clip = clips[request.TargetName];
                if (clip == null || clip.empty)
                {
                    throw new InvalidOperationException($"Extracted clip is invalid: {request.TargetName}");
                }
            }

            SerializedObject serializedState = new SerializedObject(sprintState);
            UnityEngine.Object configuredRun = serializedState
                .FindProperty("m_Stand16Points.m_ForwardFast")
                ?.objectReferenceValue;
            if (configuredRun != sprintClip)
            {
                throw new InvalidOperationException("Franklin sprint State does not use IP-sprint-loop-smooth");
            }

            AnimatorOverrideController controller = sprintState.StateController as AnimatorOverrideController;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller?.GetOverrides(overrides);
            if (!overrides.Any(pair => pair.Value == sprintClip))
            {
                throw new InvalidOperationException("Sprint AnimatorOverrideController does not use IP-sprint-loop-smooth");
            }
        }

        private static void ValidatePlayerPrefab(StateCompleteLocomotion sprintState)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                Character character = root.GetComponent<Character>();
                FranklinAnimationBridge bridge = root.GetComponent<FranklinAnimationBridge>();
                Animator animator = root.GetComponentInChildren<Animator>(true);

                if (character == null || bridge == null)
                {
                    throw new InvalidOperationException("Player is missing Character or FranklinAnimationBridge");
                }

                SerializedObject serializedCharacter = new SerializedObject(character);
                UnityEngine.Object configuredState = serializedCharacter
                    .FindProperty("m_Kernel.m_Animim.m_StartState")
                    ?.objectReferenceValue;

                if (configuredState != null)
                {
                    throw new InvalidOperationException("Player Start State must remain empty to use GC2's default locomotion");
                }

                SerializedObject serializedBridge = new SerializedObject(bridge);
                if (serializedBridge.FindProperty("m_SprintState")?.objectReferenceValue != sprintState)
                {
                    throw new InvalidOperationException("Bridge does not reference Franklin_GTA_Sprint");
                }

                SerializedProperty idleVariations = serializedBridge.FindProperty("m_IdleVariations");
                if (idleVariations == null || idleVariations.arraySize != 3)
                {
                    throw new InvalidOperationException("Bridge must contain exactly three idle variations");
                }
                for (int i = 0; i < idleVariations.arraySize; ++i)
                {
                    if (idleVariations.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    {
                        throw new InvalidOperationException($"Bridge idle variation is missing at index {i}");
                    }
                }

                if (animator == null || !animator.isHuman)
                {
                    throw new InvalidOperationException("Franklin Animator does not have a valid Humanoid Avatar");
                }

                string animatorControllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                if (animatorControllerPath != Gc2ControllerPath)
                {
                    throw new InvalidOperationException("Player's base Animator Controller is no longer GC2's controller");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string ShortClipName(string clipName)
        {
            int separator = clipName.LastIndexOf('|');
            return separator >= 0 ? clipName.Substring(separator + 1) : clipName;
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

        private readonly struct ClipRequest
        {
            public readonly string SourcePath;
            public readonly string SourceName;
            public readonly string TargetName;
            public readonly bool Loop;

            public ClipRequest(string sourcePath, string sourceName, string targetName, bool loop)
            {
                this.SourcePath = sourcePath;
                this.SourceName = sourceName;
                this.TargetName = targetName;
                this.Loop = loop;
            }
        }
    }
}
