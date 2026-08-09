using System;
using GameCreator.Runtime.Characters;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Vehicles.Editor
{
    public static class SimcadeCarjackingInstaller
    {
        private const string CarPrefabPath =
            "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Car.prefab";
        private const string NpcPrefabPath = "Assets/Prefab/NPC.prefab";
        private const string AnimationFolder =
            "Assets/FranklinAnimations/Animations/Vehicles/Carjacking/";

        [MenuItem("Tools/Franklin Game/Install NPC Carjacking on Sim-Cade Car", priority = 103)]
        public static void Install()
        {
            AssetDatabase.Refresh();

            GameObject npcPrefab = RequireAsset<GameObject>(NpcPrefabPath);
            AnimationClip attacker = RequireAsset<AnimationClip>(
                AnimationFolder + "CarKickOutL.anim"
            );
            AnimationClip victim = RequireAsset<AnimationClip>(
                AnimationFolder + "CarGetKickedOutL.anim"
            );

            Character npcCharacter = npcPrefab.GetComponent<Character>();
            Animator npcAnimator = npcPrefab.GetComponentInChildren<Animator>(true);
            if (npcCharacter == null || npcCharacter.IsPlayer || npcAnimator?.avatar?.isHuman != true)
            {
                throw new InvalidOperationException(
                    "NPC.prefab must be a non-player Humanoid GC2 Character"
                );
            }

            GameObject carRoot = PrefabUtility.LoadPrefabContents(CarPrefabPath);
            try
            {
                CarEntry entry = carRoot.GetComponent<CarEntry>();
                SimcadeCarDriver driver = carRoot.GetComponent<SimcadeCarDriver>();
                if (entry == null || driver == null)
                    throw new InvalidOperationException(
                        "Car.prefab must have CarEntry and SimcadeCarDriver before carjacking setup"
                    );

                SimcadeCarjacking carjacking = carRoot.GetComponent<SimcadeCarjacking>();
                if (carjacking == null) carjacking = carRoot.AddComponent<SimcadeCarjacking>();

                Transform landing = FindChild(carRoot.transform, "Carjack Victim Landing Point");
                if (landing == null)
                {
                    landing = new GameObject("Carjack Victim Landing Point").transform;
                    landing.SetParent(carRoot.transform, false);
                    SetDefaultLandingTransform(carRoot.transform, entry, landing);
                }

                SerializedObject serialized = new SerializedObject(carjacking);
                serialized.FindProperty("m_CarEntry").objectReferenceValue = entry;
                serialized.FindProperty("m_NpcDriverPrefab").objectReferenceValue = npcPrefab;
                serialized.FindProperty("m_SpawnNpcDriverOnStart").boolValue = true;
                serialized.FindProperty("m_AttackerKickOut").objectReferenceValue = attacker;
                serialized.FindProperty("m_VictimGetKickedOut").objectReferenceValue = victim;
                serialized.FindProperty("m_VictimLandingPoint").objectReferenceValue = landing;
                serialized.FindProperty("m_TransitionIn").floatValue = 0.05f;
                serialized.FindProperty("m_TransitionOut").floatValue = 0.1f;
                serialized.FindProperty("m_CarjackingAnimationSpeed").floatValue = 1.7f;
                serialized.FindProperty("m_EnterHandoffNormalizedTime").floatValue = 0.78f;
                serialized.FindProperty("m_PrimaryGrabIKWeight").animationCurveValue =
                    CreatePrimaryGrabCurve();
                serialized.FindProperty("m_SecondaryGrabIKWeight").animationCurveValue =
                    CreateSecondaryGrabCurve();
                serialized.FindProperty("m_MaxGrabReach").floatValue = 1.35f;
                serialized.FindProperty("m_MaxPullYawFromEntry").floatValue = 55f;
                serialized.FindProperty("m_PullFacingSharpness").floatValue = 18f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                entry.entryAnimationTransitionIn = 0.06f;
                entry.entryAnimationTransitionOut = 0.12f;
                entry.exitAnimationTransitionIn = 0.06f;
                entry.exitAnimationTransitionOut = 0.12f;
                entry.entryAnimationSpeed = 1.7f;
                entry.exitAnimationSpeed = 1.7f;
                entry.movingExitAnimationSpeed = 1.75f;
                entry.movingExitLandingSpeed = 1.9f;
                entry.movingExitDoorLeadTime = 0.18f;
                entry.movingExitLandingClipDuration = 1.6f;
                entry.doorRotationDuration = 0.32f;
                entry.doorRotationStartDelay = 0.1f;
                entry.doorResetDelay = 0.65f;

                EditorUtility.SetDirty(carjacking);
                EditorUtility.SetDirty(entry);
                EditorUtility.SetDirty(landing);
                PrefabUtility.SaveAsPrefabAsset(carRoot, CarPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(carRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateInstallation();
            Debug.Log(
                "NPC carjacking installed on Car.prefab: NPC shares the CarEntry driving " +
                "state with Player; paired CarKickOutL/CarGetKickedOutL gestures and " +
                "victim landing anchor are wired."
            );
        }

        [MenuItem("Tools/Franklin Game/Validate NPC Carjacking")]
        public static void ValidateInstallation()
        {
            GameObject car = RequireAsset<GameObject>(CarPrefabPath);
            SimcadeCarjacking carjacking = car.GetComponent<SimcadeCarjacking>();
            if (carjacking == null || carjacking.VictimLandingPoint == null)
                throw new InvalidOperationException(
                    "Car.prefab is missing SimcadeCarjacking or its victim landing point"
                );

            SerializedObject serialized = new SerializedObject(carjacking);
            string[] requiredProperties =
            {
                "m_CarEntry",
                "m_NpcDriverPrefab",
                "m_AttackerKickOut",
                "m_VictimGetKickedOut",
                "m_VictimLandingPoint"
            };
            foreach (string propertyName in requiredProperties)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property?.objectReferenceValue == null)
                    throw new InvalidOperationException(
                        $"SimcadeCarjacking is missing {propertyName}"
                    );
            }

            CarEntry entry = car.GetComponent<CarEntry>();
            if (entry == null || entry.entryAnimationSpeed < 1.69f ||
                entry.exitAnimationSpeed < 1.69f ||
                serialized.FindProperty("m_CarjackingAnimationSpeed").floatValue < 1.69f)
            {
                throw new InvalidOperationException(
                    "Car entry, exit and carjacking animation speeds are not configured"
                );
            }

            Debug.Log(
                "NPC carjacking validation passed: exact Car.prefab, GC2 NPC, paired " +
                "left-door animations, faster interaction timing and landing point are assigned."
            );
        }

        private static AnimationCurve CreatePrimaryGrabCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.1f, 0f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.92f, 1f),
                new Keyframe(1f, 0f)
            );
        }

        private static AnimationCurve CreateSecondaryGrabCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.1f, 0f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.34f, 1f),
                new Keyframe(0.5f, 0f),
                new Keyframe(1f, 0f)
            );
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Missing asset: {path}");
            return asset;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        private static void SetDefaultLandingTransform(
            Transform carRoot,
            CarEntry entry,
            Transform landing)
        {
            if (entry.entryStandingPoint == null)
            {
                landing.localPosition = new Vector3(-3.25f, -0.4f, 0.2f);
                landing.localRotation = Quaternion.identity;
                return;
            }

            Vector3 standingLocal = carRoot.InverseTransformPoint(
                entry.entryStandingPoint.position
            );
            landing.localPosition = standingLocal + new Vector3(-0.75f, 0f, 0.6f);
            landing.rotation = entry.entryStandingPoint.rotation;
        }
    }
}
