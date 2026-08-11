#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcadeBP_Pro;
using FranklinGame.Vehicles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Rebuilds the DQP motorbike prefabs around Arcade Bike Physics Pro while
/// preserving their prefab GUIDs, model meshes, rider targets and entry flow.
/// </summary>
[InitializeOnLoad]
public static class ArcadeBikePackIntegrator
{
    private const string BikesFolder =
        "Assets/Model/DQP_MotorBikePack_URP14/Generated/Prefabs/Bikes";
    private const string ArcadeRoot =
        "Assets/Ash Assets/Arcade Bike Physics Pro";
    private const string SkidmarkPrefabPath = ArcadeRoot + "/Prefabs/Skidmark Controller.prefab";
    private const string TireSmokePrefabPath = ArcadeRoot + "/Prefabs/TireSmoke.prefab";
    private const string ZeroFrictionMaterialPath =
        ArcadeRoot + "/Materials/zero Friction.physicMaterial";
    private const string EngineClipPath = ArcadeRoot + "/Audios/motocross-engine.wav";
    private const string GearClipPath = ArcadeRoot + "/Audios/Car Gear switch 3.wav";
    private const string SkidClipPath = ArcadeRoot + "/Audios/skid loop 1.wav";
    private const string EntryAnimationPath =
        "Assets/Ash Assets/Vehicle Integration/Animations/Vehicles/Character_Enter_Bike.anim";
    private const string MirroredEntryAnimationPath =
        "Assets/FranklinAnimations/Generated/BikeEntry/Character_Enter_Bike_Mirrored.anim";
    private const string RearBrakeFlareDataPath =
        "Assets/FranklinAnimations/Generated/BikeLights/Franklin_Red_Brake_Flare.asset";
    private const string AutoRunSessionKey =
        "FranklinGame.ArcadeBikePackIntegrator.AutoRun.v1";

    static ArcadeBikePackIntegrator()
    {
        EditorApplication.delayCall += TryAutoIntegrate;
    }

    [MenuItem("Tools/Franklin/Arcade Bikes/Integrate DQP Motorbike Pack")]
    public static void IntegrateAllFromMenu()
    {
        IntegrateAll();
        ValidateAll(logSuccess: true);
    }

    [MenuItem("Tools/Franklin/Arcade Bikes/Validate DQP Motorbike Pack")]
    public static void ValidateAllFromMenu()
    {
        bool valid = ValidateAll(logSuccess: true);
        EditorUtility.DisplayDialog(
            "Arcade Bike Physics Pro",
            valid
                ? "All 10 motorbikes use Arcade Bike Physics Pro, Franklin mobile controls, and no bike HUD."
                : "Validation found errors. See the Console for details.",
            "OK"
        );
    }

    [MenuItem("Tools/Franklin/Arcade Bikes/Configure Bike Entry Standing Points")]
    public static void ConfigureEntryStandingPointsFromMenu()
    {
        int configured = 0;
        for (int index = 1; index <= 10; index++)
        {
            string prefabPath = GetBikePath(index);
            if (!File.Exists(prefabPath)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (EnsureEntryApproachAnchor(root))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    configured++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Arcade Bikes] Configured entry standing points on {configured}/10 bikes.");
    }

    [MenuItem("Tools/Franklin/Arcade Bikes/Configure Mirrored Bike Entry")]
    public static void ConfigureMirroredEntryFromMenu()
    {
        EnsureMirroredEntryAnimation();
        int configured = 0;
        for (int index = 1; index <= 10; index++)
        {
            string prefabPath = GetBikePath(index);
            if (!File.Exists(prefabPath)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                EnsureEntryApproachAnchor(root);
                EnsureMirroredEntrySetup(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                if (saved) configured++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[Arcade Bikes] Configured automatic original/mirrored entry on " +
            $"{configured}/10 bikes."
        );
    }

    [MenuItem("Tools/Franklin/Arcade Bikes/Configure Red Brake + Reverse Flare")]
    public static void ConfigureRearBrakeFlareFromMenu()
    {
        LensFlareDataSRP flareData = EnsureRearBrakeFlareData();
        int configured = 0;
        for (int index = 1; index <= 10; index++)
        {
            string prefabPath = GetBikePath(index);
            if (!File.Exists(prefabPath)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ArcadeBikeControllerPro controller =
                    root.GetComponent<ArcadeBikeControllerPro>();
                FranklinArcadeBikeDriver driver =
                    root.GetComponent<FranklinArcadeBikeDriver>();
                Rigidbody rigidbody = root.GetComponent<Rigidbody>();
                if (controller == null || driver == null || rigidbody == null)
                {
                    throw new InvalidOperationException(
                        $"{root.name} must be integrated before adding the rear flare."
                    );
                }

                ConfigureRearBrakeFlare(
                    root,
                    controller,
                    driver,
                    rigidbody,
                    flareData
                );
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                if (saved) configured++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[Arcade Bikes] Configured red brake/reverse flare on " +
            $"{configured}/10 bikes."
        );
    }

    public static void IntegrateAllBatchMode()
    {
        IntegrateAll();
        if (!ValidateAll(logSuccess: true))
        {
            throw new InvalidOperationException("Arcade motorbike prefab validation failed.");
        }
    }

    public static void IntegrateAll()
    {
        EnsureRuntimeAssets();
        EnsureMirroredEntryAnimation();
        EnsureRearBrakeFlareData();

        int integrated = 0;
        for (int index = 1; index <= 10; index++)
        {
            string prefabPath = GetBikePath(index);
            if (!File.Exists(prefabPath))
            {
                Debug.LogError($"[Arcade Bikes] Missing prefab: {prefabPath}");
                continue;
            }

            IntegratePrefab(prefabPath);
            integrated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[Arcade Bikes] Integrated {integrated}/10 bikes. " +
            "Arcade Bike Physics Pro is active; Franklin mobile controls are reused; bike HUDs were removed."
        );
    }

    public static bool ValidateAll(bool logSuccess)
    {
        bool allValid = true;
        for (int index = 1; index <= 10; index++)
        {
            string prefabPath = GetBikePath(index);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Arcade Bikes] Validation: missing {prefabPath}");
                allValid = false;
                continue;
            }

            ArcadeBikeControllerPro controller = prefab.GetComponent<ArcadeBikeControllerPro>();
            FranklinArcadeBikeDriver driver = prefab.GetComponent<FranklinArcadeBikeDriver>();
            BikeEntry entry = prefab.GetComponent<BikeEntry>();
            FranklinBikeBrakeReverseFlare rearFlare =
                prefab.GetComponentInChildren<FranklinBikeBrakeReverseFlare>(true);
            FranklinBikeImpactAudio impactAudio =
                prefab.GetComponent<FranklinBikeImpactAudio>();
            FranklinBikeCrashRagdoll crashRagdoll =
                prefab.GetComponent<FranklinBikeCrashRagdoll>();
            FranklinArcadeBikeRagdoll bikeRagdoll =
                prefab.GetComponent<FranklinArcadeBikeRagdoll>();
            bool valid =
                controller != null &&
                driver != null &&
                driver is IRvrVehicleInputController &&
                entry != null &&
                prefab.GetComponent<PhysicsBikeController>() == null &&
                prefab.GetComponentsInChildren<WheelCollider>(true).Length == 0 &&
                prefab.GetComponentsInChildren<Canvas>(true).Length == 0 &&
                !HasNamedTransform(prefab, "HUD_Bike") &&
                !HasNamedTransform(prefab, "Triggers-Camera") &&
                HasCompleteArcadeReferences(controller) &&
                HasCompleteWheelVisuals(controller) &&
                HasCompleteRearBrakeFlare(rearFlare) &&
                impactAudio != null &&
                impactAudio.IsConfigured &&
                crashRagdoll != null &&
                crashRagdoll.IsConfigured &&
                crashRagdoll.HasCurrentConfiguration &&
                bikeRagdoll != null &&
                bikeRagdoll.IsConfigured &&
                controller.bikeReferences.BikeRb == prefab.GetComponent<Rigidbody>() &&
                controller.bikeReferences.collider != null &&
                entry.entryParent != null &&
                entry.entryStandingPoint != null &&
                HasCompleteMirroredEntry(prefab, entry) &&
                entry.steeringWheelLeftHandTarget != null &&
                entry.steeringWheelRightHandTarget != null &&
                entry.leftFootTarget != null &&
                entry.rightFootTarget != null &&
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0;

            if (!valid)
            {
                Debug.LogError($"[Arcade Bikes] Validation failed: {prefabPath}", prefab);
                allValid = false;
            }
            else if (logSuccess)
            {
                Debug.Log($"[Arcade Bikes] Validation passed: {prefab.name}", prefab);
            }
        }

        return allValid;
    }

    private static void TryAutoIntegrate()
    {
        if (SessionState.GetBool(AutoRunSessionKey, false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryAutoIntegrate;
            return;
        }

        if (!AllPrefabsExist() ||
            AssetDatabase.LoadAssetAtPath<MonoScript>(
                ArcadeRoot + "/Scripts/ArcadeBikeControllerPro.cs"
            ) == null)
        {
            return;
        }

        SessionState.SetBool(AutoRunSessionKey, true);
        if (AllPrefabsIntegrated()) return;

        try
        {
            IntegrateAll();
            ValidateAll(logSuccess: true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void IntegratePrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ConfigurePrefab(root);
            root.name = Path.GetFileNameWithoutExtension(prefabPath);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
            if (!saved)
            {
                throw new InvalidOperationException($"Unity could not save {prefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigurePrefab(GameObject root)
    {
        RemoveBikeHud(root);
        RemoveLegacyBikeCamera(root);
        EnsureEntryApproachAnchor(root);
        EnsureMirroredEntrySetup(root);

        ArcadeBikeControllerPro controller = root.GetComponent<ArcadeBikeControllerPro>();
        PhysicsBikeController legacy = root.GetComponent<PhysicsBikeController>();

        Transform bikeBody = controller?.bikeReferences?.BodyMesh != null
            ? controller.bikeReferences.BodyMesh
            : legacy?.bikeBody != null
                ? legacy.bikeBody
                : FindTransform(root, "BikeBody");
        Transform frontWheel = controller?.bikeReferences?.FrontWheel != null
            ? controller.bikeReferences.FrontWheel
            : legacy?.frontWheelTransform != null
                ? legacy.frontWheelTransform
                : FindTransform(root, "FrontWheelTarget");
        Transform rearWheel = controller?.bikeReferences?.RearWheel != null
            ? controller.bikeReferences.RearWheel
            : legacy?.rearWheelTransform != null
                ? legacy.rearWheelTransform
                : FindTransform(root, "RearWheelTarget");
        Transform handlebar = legacy?.steeringWheelMesh != null
            ? legacy.steeringWheelMesh
            : FindTransform(root, "Handlebar");

        if (bikeBody == null || frontWheel == null || rearWheel == null)
        {
            throw new InvalidOperationException(
                $"{root.name} is missing BikeBody, FrontWheelTarget, or RearWheelTarget."
            );
        }

        EnsureWheelVisualCluster(root.transform, frontWheel, rearWheel, "front");
        EnsureWheelVisualCluster(root.transform, rearWheel, frontWheel, "rear");

        // Always measure the complete visual cluster. Each DQP wheel is split
        // into a rim mesh and a separate tire mesh; the old collider/rim-only
        // radius is too small for Arcade Bike Physics ground probing.
        float frontRadius = CalculateWheelRadius(root.transform, frontWheel);
        float rearRadius = CalculateWheelRadius(root.transform, rearWheel);
        float frontWidth = CalculateWheelWidth(root.transform, frontWheel);
        float rearWidth = CalculateWheelWidth(root.transform, rearWheel);
        Bounds bodyBounds = CalculateBodyBounds(root.transform, bikeBody, frontWheel, rearWheel);

        Vector3 frontPosition = frontWheel.position;
        Quaternion frontRotation = frontWheel.rotation;
        Vector3 rearPosition = rearWheel.position;
        Quaternion rearRotation = rearWheel.rotation;
        Vector3 handlePosition = handlebar != null
            ? handlebar.position
            : frontPosition + root.transform.up * frontRadius * 1.8f;
        Quaternion handleRotation = handlebar != null
            ? handlebar.rotation
            : root.transform.rotation;

        RemoveLegacyPhysics(root, legacy);

        Transform rotator = GetOrCreateChild(root.transform, "ABP Rotator");
        Transform wheelie = GetOrCreateChild(rotator, "ABP Wheelie");
        Transform lean = GetOrCreateChild(wheelie, "ABP Lean");
        Transform bikeModel = GetOrCreateChild(lean, "ABP Bike Model");

        if (wheelie.localPosition == Vector3.zero && lean.localPosition == Vector3.zero)
        {
            Vector3 rearLocal = root.transform.InverseTransformPoint(rearPosition);
            Vector3 wheeliePivot = new Vector3(0f, 0f, rearLocal.z);
            wheelie.localPosition = wheeliePivot;
            lean.localPosition = -wheeliePivot;
        }

        bikeBody.SetParent(bikeModel, true);

        Transform steeringParent = GetOrCreateChild(bikeModel, "ABP Steering Parent");
        steeringParent.SetPositionAndRotation(handlePosition, handleRotation);
        Transform steering = GetOrCreateChild(steeringParent, "ABP Steering");
        ResetLocalTransform(steering);
        Transform steeringMeshes = GetOrCreateChild(steering, "ABP Steering Meshes");
        ResetLocalTransform(steeringMeshes);
        if (handlebar != null)
        {
            handlebar.SetParent(steeringMeshes, true);
        }

        Transform frontWheelParent = GetOrCreateChild(steering, "ABP Front Wheel Parent");
        frontWheelParent.SetPositionAndRotation(frontPosition, frontRotation);
        frontWheel.SetParent(frontWheelParent, true);
        frontWheel.localPosition = Vector3.zero;
        frontWheel.localRotation = Quaternion.identity;

        Transform rearWheelParent = GetOrCreateChild(bikeModel, "ABP Rear Wheel Parent");
        rearWheelParent.SetPositionAndRotation(rearPosition, rearRotation);
        rearWheel.SetParent(rearWheelParent, true);
        rearWheel.localPosition = Vector3.zero;
        rearWheel.localRotation = Quaternion.identity;

        CapsuleCollider bodyCollider = ConfigureBodyCollider(bikeModel, bodyBounds);
        Rigidbody rigidbody = ConfigureRigidbody(root);
        ConfigureController(
            root,
            ref controller,
            rigidbody,
            bodyCollider,
            rotator,
            wheelie,
            lean,
            bikeModel,
            bikeBody,
            steeringParent,
            steering,
            steeringMeshes,
            frontWheelParent,
            rearWheelParent,
            frontWheel,
            rearWheel,
            frontRadius,
            rearRadius,
            frontWidth,
            rearWidth
        );

        FranklinArcadeBikeDriver driver = root.GetComponent<FranklinArcadeBikeDriver>();
        if (driver == null) driver = root.AddComponent<FranklinArcadeBikeDriver>();

        ConfigureRearBrakeFlare(
            root,
            controller,
            driver,
            rigidbody,
            EnsureRearBrakeFlareData(),
            bodyBounds,
            frontWheel,
            rearWheel,
            rearRadius
        );
        FranklinBikeImpactInstaller.ConfigurePrefab(root);
    }

    private static bool EnsureEntryApproachAnchor(GameObject root)
    {
        BikeEntry entry = root != null ? root.GetComponent<BikeEntry>() : null;
        if (entry == null) return false;
        if (entry.entryStandingPoint != null)
        {
            if (entry.entryStandingPoint.name == "Bike Entry Standing Point" &&
                entry.entryStandingPoint.localPosition.y < 0.43f)
            {
                Vector3 upgradedPosition = entry.entryStandingPoint.localPosition;
                upgradedPosition.y = 0.43f;
                entry.entryStandingPoint.localPosition = upgradedPosition;
                EditorUtility.SetDirty(entry.entryStandingPoint);
                EditorUtility.SetDirty(entry);
            }
            return true;
        }

        Transform trigger = FindTransform(root, "Triggers_Enter/Exit");
        Transform standingPoint = GetOrCreateChild(
            root.transform,
            "Bike Entry Standing Point"
        );
        Vector3 localPosition = trigger != null
            ? root.transform.InverseTransformPoint(trigger.position)
            : new Vector3(-0.8f, 0.25f, 0f);

        // DQP bike roots sit at y=0.7 in gameplay while the grounded GC2 Player
        // root settles at y=1.13, which gives this 0.43 m local root height.
        localPosition.y = Mathf.Max(localPosition.y, 0.43f);
        standingPoint.localPosition = localPosition;
        standingPoint.localRotation = Quaternion.identity;
        standingPoint.localScale = Vector3.one;
        entry.entryStandingPoint = standingPoint;
        EditorUtility.SetDirty(entry);
        return true;
    }

    private static void EnsureMirroredEntrySetup(GameObject root)
    {
        BikeEntry entry = root != null ? root.GetComponent<BikeEntry>() : null;
        if (entry == null || entry.entryStandingPoint == null) return;

        AnimationClip mirroredClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            MirroredEntryAnimationPath
        );
        if (mirroredClip == null)
        {
            throw new InvalidOperationException(
                $"Mirrored bike entry animation is missing: {MirroredEntryAnimationPath}"
            );
        }

        Transform mirroredPoint = GetOrCreateChild(
            root.transform,
            "Bike Entry Standing Point Mirrored"
        );
        MirrorTransformAcrossBike(root.transform, entry.entryStandingPoint, mirroredPoint);

        entry.mirroredEntryAnimation = mirroredClip;
        entry.mirroredEntryStandingPoint = mirroredPoint;

        // One centered interaction hotspot covers both side-specific standing points.
        Transform interaction = FindTransform(root, "Triggers_Enter/Exit");
        if (interaction != null)
        {
            Vector3 localPosition = root.transform.InverseTransformPoint(
                interaction.position
            );
            localPosition.x = 0f;
            interaction.position = root.transform.TransformPoint(localPosition);
            EditorUtility.SetDirty(interaction);
        }

        EditorUtility.SetDirty(mirroredPoint);
        EditorUtility.SetDirty(entry);
    }

    private static void MirrorTransformAcrossBike(
        Transform bikeRoot,
        Transform source,
        Transform destination)
    {
        Vector3 localPosition = bikeRoot.InverseTransformPoint(source.position);
        localPosition.x = -localPosition.x;

        Quaternion localRotation = Quaternion.Inverse(bikeRoot.rotation) * source.rotation;
        Quaternion mirroredLocalRotation = new Quaternion(
            localRotation.x,
            -localRotation.y,
            -localRotation.z,
            localRotation.w
        );

        destination.SetPositionAndRotation(
            bikeRoot.TransformPoint(localPosition),
            bikeRoot.rotation * mirroredLocalRotation
        );
        destination.localScale = source.localScale;
    }

    private static void ConfigureController(
        GameObject root,
        ref ArcadeBikeControllerPro controller,
        Rigidbody rigidbody,
        CapsuleCollider bodyCollider,
        Transform rotator,
        Transform wheelie,
        Transform lean,
        Transform bikeModel,
        Transform bikeBody,
        Transform steeringParent,
        Transform steering,
        Transform steeringMeshes,
        Transform frontWheelParent,
        Transform rearWheelParent,
        Transform frontWheel,
        Transform rearWheel,
        float frontRadius,
        float rearRadius,
        float frontWidth,
        float rearWidth)
    {
        if (controller == null) controller = root.AddComponent<ArcadeBikeControllerPro>();

        controller.bikeInput ??= new ArcadeBikeControllerPro.BikeInput();
        controller.bikeReferences ??= new ArcadeBikeControllerPro.BikeReferences();
        controller.bikeGeometry ??= new ArcadeBikeControllerPro.BikeGeometry();
        controller.bikeSuspension ??= new ArcadeBikeControllerPro.BikeSuspension();
        controller.bikeSettings ??= new ArcadeBikeControllerPro.BikeSettings();
        controller.bikeCurves ??= new ArcadeBikeControllerPro.BikeCurves();
        controller.bikeAudio ??= new ArcadeBikeControllerPro.BikeAudio();
        controller.bikeEvents ??= new ArcadeBikeControllerPro.BikeEvents();

        controller.bikeReferences.Rotator = rotator;
        controller.bikeReferences.WheelieTransform = wheelie;
        controller.bikeReferences.LeanTransform = lean;
        controller.bikeReferences.FrontWheelParent = frontWheelParent;
        controller.bikeReferences.RearWheelParent = rearWheelParent;
        controller.bikeReferences.FrontWheel = frontWheel;
        controller.bikeReferences.RearWheel = rearWheel;
        controller.bikeReferences.BikeSteering = steering;
        controller.bikeReferences.BikeSteeringParent = steeringParent;
        controller.bikeReferences.SteeringMeshes = steeringMeshes;
        controller.bikeReferences.BikeModel = bikeModel;
        controller.bikeReferences.BodyMesh = bikeBody;
        controller.bikeReferences.BikeRb = rigidbody;
        controller.bikeReferences.collider = bodyCollider;
        controller.bikeReferences.skidmarksPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(SkidmarkPrefabPath);
        controller.bikeReferences.tireSmokePrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(TireSmokePrefabPath);
        controller.bikeReferences.BikerAnimationTargets = null;
        controller.bikeReferences.cameraController = null;
        controller.bikeReferences.ragdollActivator = null;

        controller.bikeGeometry.FrontWheelRadius = Mathf.Max(0.05f, frontRadius);
        controller.bikeGeometry.RearWheelRadius = Mathf.Max(0.05f, rearRadius);
        controller.bikeGeometry.FrontWheelWidth = Mathf.Max(0.03f, frontWidth);
        controller.bikeGeometry.RearWheelWidth = Mathf.Max(0.03f, rearWidth);
        controller.bikeGeometry.FrontWheelAngle =
            Vector3.Angle(frontWheelParent.up, rotator.up);
        controller.bikeGeometry.RearWheelAngle =
            Vector3.Angle(rearWheelParent.up, rotator.up);

        controller.bikeSuspension.SpringForce = 250f;
        controller.bikeSuspension.DamperForce = 25f;
        controller.bikeSuspension.groundStickFactor = 0.1f;
        controller.bikeSuspension.MaxCompression = 0.6f;

        controller.bikeSettings.drivableLayerMask = ~(1 << 2);
        controller.bikeSettings.maxSpeed = 55f;
        controller.bikeSettings.reverseMaxSpeed = 4f;
        controller.bikeSettings.acceleration = 12f;
        controller.bikeSettings.reverseAcceleration = 5f;
        controller.bikeSettings.deceleration = 25f;
        controller.bikeSettings.handBrakeDeceleration = 9f;
        controller.bikeSettings.maxTurnAngle = 24f;
        controller.bikeSettings.useLerpTurning = true;
        controller.bikeSettings.turnLerpSpeed = 5f;
        controller.bikeSettings.steeringAnimationSpeed = 6f;
        controller.bikeSettings.canSteerInAir = false;
        controller.bikeSettings.canLeanInAir = true;
        controller.bikeSettings.turnSpeedInAir = 60f;
        controller.bikeSettings.maxLeanAngle = 48f;
        controller.bikeSettings.leaningAnimationSpeed = 5f;
        controller.bikeSettings.frictionCoefficient = 0.65f;
        controller.bikeSettings.driftFrictionFactor = 0.45f;
        controller.bikeSettings.driftTurnFactor = 1.75f;
        controller.bikeSettings.rollingResistance = 1f;
        controller.bikeSettings.gravity = 15f;
        controller.bikeSettings.burnoutRotationSpeed = 18f;
        controller.bikeSettings.burnoutSmoothness = 1f;
        controller.bikeSettings.maxWheelieAngle = 30f;
        controller.bikeSettings.wheelieAnimationSpeed = 3f;
        controller.bikeSettings.alignRotatorSpeed_Ground = 20f;
        controller.bikeSettings.alignRotatorSpeed_Air = 3f;

        controller.bikeCurves.AccelerationCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.55f, 0.78f),
            new Keyframe(1f, 0.35f)
        );
        controller.bikeCurves.ReverseAccelerationCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.25f)
        );
        controller.bikeCurves.SteeringCurve = new AnimationCurve(
            new Keyframe(0f, 0.9f),
            new Keyframe(0.15f, 0.7f),
            new Keyframe(0.5f, 0.3f),
            new Keyframe(1f, 0.12f)
        );
        controller.bikeCurves.FrictionCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.2f, 0.65f),
            new Keyframe(1f, 0.08f)
        );
        controller.bikeCurves.LeanCurve = new AnimationCurve(
            new Keyframe(0f, 0.05f),
            new Keyframe(0.25f, 0.75f),
            new Keyframe(1f, 1f)
        );

        AudioSource engine = FindOrCreateAudioSource(root, "AudioSource-Engine");
        AudioSource skid = FindOrCreateAudioSource(root, "AudioSource-Drifting");
        AudioSource gear = FindOrCreateAudioSource(root, "ABP Gear Shift");
        ConfigureAudioSource(
            engine,
            AssetDatabase.LoadAssetAtPath<AudioClip>(EngineClipPath),
            loop: true,
            volume: 0.5f
        );
        ConfigureAudioSource(
            skid,
            AssetDatabase.LoadAssetAtPath<AudioClip>(SkidClipPath),
            loop: true,
            volume: 0f
        );
        ConfigureAudioSource(
            gear,
            AssetDatabase.LoadAssetAtPath<AudioClip>(GearClipPath),
            loop: false,
            volume: 0.7f
        );
        controller.bikeAudio.engineSound = engine;
        controller.bikeAudio.gearShiftSound = gear;
        controller.bikeAudio.SkidSound = skid;
        controller.bikeAudio.minPitch = 0.35f;
        controller.bikeAudio.maxPitch = 2f;

        controller.gearSpeeds = new[] { 8, 16, 25, 35, 45, 52 };
        controller.currentGear = 1;
        controller.bikeEvents.OnTakeOff ??= new UnityEvent();
        controller.bikeEvents.OnGrounded ??= new UnityEvent();
        controller.bikeEvents.OnGearChange ??= new UnityEvent();
        controller.provideInput(0f, 0f, 0f, 0f, 0f, 0f);
        controller.enabled = true;
        EditorUtility.SetDirty(controller);
    }

    private static void RemoveLegacyPhysics(GameObject root, PhysicsBikeController legacy)
    {
        foreach (VehicleAudioPhysics audio in root.GetComponentsInChildren<VehicleAudioPhysics>(true))
            Object.DestroyImmediate(audio);
        foreach (VehicleVFX vfx in root.GetComponentsInChildren<VehicleVFX>(true))
            Object.DestroyImmediate(vfx);

        foreach (WheelCollider wheelCollider in root.GetComponentsInChildren<WheelCollider>(true))
        {
            GameObject target = wheelCollider.gameObject;
            if (target.GetComponents<Component>().Length == 2)
                Object.DestroyImmediate(target);
            else
                Object.DestroyImmediate(wheelCollider);
        }

        foreach (Collider collider in root.GetComponents<Collider>())
        {
            if (!collider.isTrigger) Object.DestroyImmediate(collider);
        }

        if (legacy != null) Object.DestroyImmediate(legacy);
    }

    private static CapsuleCollider ConfigureBodyCollider(Transform bikeModel, Bounds rootBounds)
    {
        Transform colliderTransform = GetOrCreateChild(bikeModel, "ABP Collider");
        colliderTransform.localPosition = rootBounds.center;
        colliderTransform.localRotation = Quaternion.identity;
        colliderTransform.localScale = Vector3.one;

        CapsuleCollider collider = colliderTransform.GetComponent<CapsuleCollider>();
        if (collider == null) collider = colliderTransform.gameObject.AddComponent<CapsuleCollider>();
        collider.direction = 2;
        collider.center = Vector3.zero;
        collider.radius = Mathf.Clamp(rootBounds.size.x * 0.42f, 0.18f, 0.48f);
        collider.height = Mathf.Max(collider.radius * 2f, rootBounds.size.z * 0.9f);
        collider.sharedMaterial =
            AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ZeroFrictionMaterialPath);
        collider.isTrigger = false;
        return collider;
    }

    private static Rigidbody ConfigureRigidbody(GameObject root)
    {
        Rigidbody rigidbody = root.GetComponent<Rigidbody>();
        if (rigidbody == null) rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.mass = 200f;
        rigidbody.useGravity = false;
        rigidbody.isKinematic = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        rigidbody.linearDamping = 0f;
        rigidbody.angularDamping = 0f;

        Transform center = FindTransform(root, "CenterOfGravity");
        if (center != null)
        {
            rigidbody.centerOfMass = root.transform.InverseTransformPoint(center.position);
        }

        return rigidbody;
    }

    private static void RemoveBikeHud(GameObject root)
    {
        foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
        }

        Transform hud = FindTransform(root, "HUD_Bike");
        if (hud != null) Object.DestroyImmediate(hud.gameObject);
    }

    private static void RemoveLegacyBikeCamera(GameObject root)
    {
        Transform cameraTriggers = FindTransform(root, "Triggers-Camera");
        if (cameraTriggers != null)
        {
            Object.DestroyImmediate(cameraTriggers.gameObject);
        }
    }

    private static AudioSource FindOrCreateAudioSource(GameObject root, string name)
    {
        Transform target = FindTransform(root, name);
        if (target == null)
        {
            target = GetOrCreateChild(root.transform, name);
        }

        AudioSource source = target.GetComponent<AudioSource>();
        return source != null ? source : target.gameObject.AddComponent<AudioSource>();
    }

    private static void ConfigureAudioSource(
        AudioSource source,
        AudioClip clip,
        bool loop,
        float volume)
    {
        source.Stop();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 45f;
    }

    private static Bounds CalculateBodyBounds(
        Transform root,
        Transform bikeBody,
        Transform frontWheel,
        Transform rearWheel)
    {
        Renderer[] renderers = bikeBody.GetComponentsInChildren<Renderer>(true)
            .Where(renderer =>
                renderer != null &&
                !renderer.transform.IsChildOf(frontWheel) &&
                !renderer.transform.IsChildOf(rearWheel))
            .ToArray();
        if (renderers.Length == 0)
        {
            renderers = bikeBody.GetComponentsInChildren<Renderer>(true);
        }
        return CalculateLocalBounds(root, renderers);
    }

    private static float CalculateWheelRadius(Transform root, Transform wheel)
    {
        Bounds bounds = CalculateLocalBounds(
            root,
            wheel.GetComponentsInChildren<Renderer>(true)
        );
        return Mathf.Clamp(Mathf.Max(bounds.size.y, bounds.size.z) * 0.5f, 0.1f, 0.8f);
    }

    private static void EnsureWheelVisualCluster(
        Transform root,
        Transform wheel,
        Transform oppositeWheel,
        string wheelLabel)
    {
        MeshRenderer[] assignedRenderers = wheel.GetComponentsInChildren<MeshRenderer>(true);
        if (assignedRenderers.Length == 0)
        {
            throw new InvalidOperationException(
                $"{root.name} {wheelLabel} wheel has no rim mesh."
            );
        }

        Bounds assignedBounds = CalculateLocalBounds(root, assignedRenderers);
        float assignedDiameter = Mathf.Max(assignedBounds.size.y, assignedBounds.size.z);
        Vector2 axle = new Vector2(assignedBounds.center.y, assignedBounds.center.z);

        MeshRenderer tireRenderer = root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer =>
                renderer != null &&
                !renderer.transform.IsChildOf(wheel) &&
                !renderer.transform.IsChildOf(oppositeWheel))
            .Select(renderer => new
            {
                Renderer = renderer,
                Bounds = CalculateLocalBounds(root, new[] { renderer })
            })
            .Where(candidate =>
            {
                float diameter = Mathf.Max(
                    candidate.Bounds.size.y,
                    candidate.Bounds.size.z
                );
                float roundness = Mathf.Abs(
                    candidate.Bounds.size.y - candidate.Bounds.size.z
                ) / Mathf.Max(diameter, 0.0001f);
                float axleDistance = Vector2.Distance(
                    axle,
                    new Vector2(candidate.Bounds.center.y, candidate.Bounds.center.z)
                );

                return axleDistance <= Mathf.Max(0.04f, assignedDiameter * 0.16f) &&
                       roundness <= 0.12f &&
                       diameter >= assignedDiameter * 1.06f &&
                       diameter <= assignedDiameter * 2.2f &&
                       candidate.Bounds.size.x <= diameter * 0.85f;
            })
            .OrderByDescending(candidate =>
                Mathf.Max(candidate.Bounds.size.y, candidate.Bounds.size.z))
            .Select(candidate => candidate.Renderer)
            .FirstOrDefault();

        if (tireRenderer == null)
        {
            // A previously integrated prefab already has the tire in this
            // cluster, so two renderers means there is nothing left to move.
            if (assignedRenderers.Length >= 2) return;

            throw new InvalidOperationException(
                $"{root.name} could not identify the {wheelLabel} tire mesh."
            );
        }

        tireRenderer.transform.SetParent(wheel, true);
        Debug.Log(
            $"[Arcade Bikes] {root.name}: paired {wheelLabel} rim with tire " +
            $"'{tireRenderer.name}'."
        );
    }

    private static float CalculateWheelWidth(Transform root, Transform wheel)
    {
        Bounds bounds = CalculateLocalBounds(
            root,
            wheel.GetComponentsInChildren<Renderer>(true)
        );
        return Mathf.Clamp(bounds.size.x, 0.03f, 0.5f);
    }

    private static Bounds CalculateLocalBounds(Transform root, IEnumerable<Renderer> renderers)
    {
        bool initialized = false;
        Bounds result = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            Bounds bounds = renderer.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = bounds.center +
                                 Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                Vector3 local = root.InverseTransformPoint(corner);
                if (!initialized)
                {
                    result = new Bounds(local, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(local);
                }
            }
        }

        if (!initialized)
            throw new InvalidOperationException($"No render bounds found below {root.name}.");
        return result;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;

        var gameObject = new GameObject(name);
        child = gameObject.transform;
        child.SetParent(parent, false);
        ResetLocalTransform(child);
        return child;
    }

    private static void ResetLocalTransform(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private static Transform FindTransform(GameObject root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.name == name);
    }

    private static bool HasNamedTransform(GameObject root, string name)
    {
        return FindTransform(root, name) != null;
    }

    private static bool HasCompleteArcadeReferences(ArcadeBikeControllerPro controller)
    {
        ArcadeBikeControllerPro.BikeReferences references = controller?.bikeReferences;
        return references != null &&
               references.Rotator != null &&
               references.WheelieTransform != null &&
               references.LeanTransform != null &&
               references.FrontWheelParent != null &&
               references.RearWheelParent != null &&
               references.FrontWheel != null &&
               references.RearWheel != null &&
               references.BikeSteering != null &&
               references.BikeModel != null &&
               references.BodyMesh != null &&
               references.BikeRb != null &&
               references.collider != null &&
               references.skidmarksPrefab != null &&
               references.tireSmokePrefab != null &&
               controller.bikeAudio != null &&
               controller.bikeAudio.engineSound != null &&
               controller.bikeAudio.gearShiftSound != null &&
               controller.bikeAudio.SkidSound != null;
    }

    private static bool HasCompleteWheelVisuals(ArcadeBikeControllerPro controller)
    {
        Transform front = controller?.bikeReferences?.FrontWheel;
        Transform rear = controller?.bikeReferences?.RearWheel;
        return front != null && rear != null &&
               front.GetComponentsInChildren<MeshRenderer>(true).Length >= 2 &&
               rear.GetComponentsInChildren<MeshRenderer>(true).Length >= 2;
    }

    private static bool HasCompleteRearBrakeFlare(
        FranklinBikeBrakeReverseFlare rearFlare)
    {
        return rearFlare != null &&
               rearFlare.RedPointLight != null &&
               rearFlare.RedPointLight.type == LightType.Point &&
               rearFlare.RedPointLight.color.r > rearFlare.RedPointLight.color.g &&
               rearFlare.RedFlare != null &&
               rearFlare.RedFlare.lensFlareData != null;
    }

    private static bool HasCompleteMirroredEntry(GameObject root, BikeEntry entry)
    {
        if (root == null || entry == null || entry.entryStandingPoint == null ||
            entry.mirroredEntryStandingPoint == null ||
            entry.mirroredEntryAnimation == null)
        {
            return false;
        }

        Vector3 original = root.transform.InverseTransformPoint(
            entry.entryStandingPoint.position
        );
        Vector3 mirrored = root.transform.InverseTransformPoint(
            entry.mirroredEntryStandingPoint.position
        );
        bool positionMirrored = Mathf.Abs(original.x + mirrored.x) <= 0.0001f &&
                                Mathf.Abs(original.y - mirrored.y) <= 0.0001f &&
                                Mathf.Abs(original.z - mirrored.z) <= 0.0001f;

        Quaternion originalRotation =
            Quaternion.Inverse(root.transform.rotation) *
            entry.entryStandingPoint.rotation;
        Quaternion expectedRotation = new Quaternion(
            originalRotation.x,
            -originalRotation.y,
            -originalRotation.z,
            originalRotation.w
        );
        Quaternion mirroredRotation =
            Quaternion.Inverse(root.transform.rotation) *
            entry.mirroredEntryStandingPoint.rotation;

        Transform interaction = FindTransform(root, "Triggers_Enter/Exit");
        bool interactionCentered = interaction != null && Mathf.Abs(
            root.transform.InverseTransformPoint(interaction.position).x
        ) <= 0.0001f;

        SerializedObject serializedClip = new SerializedObject(
            entry.mirroredEntryAnimation
        );
        SerializedProperty mirrorProperty = serializedClip.FindProperty(
            "m_AnimationClipSettings.m_Mirror"
        );
        return positionMirrored &&
               Quaternion.Angle(expectedRotation, mirroredRotation) <= 0.001f &&
               interactionCentered &&
               mirrorProperty?.boolValue == true;
    }

    private static void EnsureRuntimeAssets()
    {
        string[] required =
        {
            ArcadeRoot + "/Scripts/ArcadeBikeControllerPro.cs",
            SkidmarkPrefabPath,
            TireSmokePrefabPath,
            ZeroFrictionMaterialPath,
            EngineClipPath,
            GearClipPath,
            SkidClipPath
        };
        string missing = required.FirstOrDefault(path =>
            AssetDatabase.LoadMainAssetAtPath(path) == null);
        if (missing != null)
        {
            throw new FileNotFoundException(
                "Arcade Bike Physics Pro runtime asset is missing",
                missing
            );
        }
    }

    private static LensFlareDataSRP EnsureRearBrakeFlareData()
    {
        EnsureAssetFolder("Assets/FranklinAnimations", "Generated");
        EnsureAssetFolder("Assets/FranklinAnimations/Generated", "BikeLights");

        LensFlareDataSRP data = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(
            RearBrakeFlareDataPath
        );
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
            data.name = "Franklin_Red_Brake_Flare";
            AssetDatabase.CreateAsset(data, RearBrakeFlareDataPath);
        }

        LensFlareDataElementSRP core = new LensFlareDataElementSRP
        {
            flareType = SRPLensFlareType.Circle,
            tintColorType = SRPLensFlareColorType.Constant,
            tint = new Color(1f, 0.015f, 0.005f, 1f),
            blendMode = SRPLensFlareBlendMode.Additive,
            localIntensity = 1f,
            uniformScale = 0.18f,
            sizeXY = Vector2.one,
            modulateByLightColor = true,
            fallOff = 0.45f,
            edgeOffset = 0.12f
        };
        LensFlareDataElementSRP halo = new LensFlareDataElementSRP
        {
            flareType = SRPLensFlareType.Circle,
            tintColorType = SRPLensFlareColorType.Constant,
            tint = new Color(1f, 0.01f, 0.005f, 0.55f),
            blendMode = SRPLensFlareBlendMode.Additive,
            localIntensity = 0.38f,
            uniformScale = 0.52f,
            sizeXY = new Vector2(1.45f, 1f),
            modulateByLightColor = true,
            fallOff = 0.9f,
            edgeOffset = 0.04f
        };
        data.elements = new[] { core, halo };
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void ConfigureRearBrakeFlare(
        GameObject root,
        ArcadeBikeControllerPro controller,
        FranklinArcadeBikeDriver driver,
        Rigidbody rigidbody,
        LensFlareDataSRP flareData,
        Bounds? knownBodyBounds = null,
        Transform frontWheel = null,
        Transform rearWheel = null,
        float rearRadius = 0f)
    {
        if (root == null || controller == null || driver == null ||
            rigidbody == null || flareData == null)
        {
            throw new ArgumentNullException(
                nameof(root),
                "Rear brake flare requires a complete integrated bike."
            );
        }

        frontWheel ??= controller.bikeReferences?.FrontWheel;
        rearWheel ??= controller.bikeReferences?.RearWheel;
        Transform bikeBody = controller.bikeReferences?.BodyMesh;
        Transform bikeModel = controller.bikeReferences?.BikeModel;
        if (frontWheel == null || rearWheel == null || bikeBody == null)
        {
            throw new InvalidOperationException(
                $"{root.name} is missing body or wheel references for its rear flare."
            );
        }

        if (rearRadius <= 0f)
        {
            rearRadius = Mathf.Max(
                0.1f,
                controller.bikeGeometry?.RearWheelRadius ?? 0.3f
            );
        }

        Bounds bodyBounds = knownBodyBounds ?? CalculateBodyBounds(
            root.transform,
            bikeBody,
            frontWheel,
            rearWheel
        );
        Vector3 frontLocal = root.transform.InverseTransformPoint(frontWheel.position);
        Vector3 rearLocal = root.transform.InverseTransformPoint(rearWheel.position);
        Vector3 forwardLocal = Vector3.ProjectOnPlane(
            frontLocal - rearLocal,
            Vector3.up
        );
        if (forwardLocal.sqrMagnitude < 0.0001f) forwardLocal = Vector3.forward;
        forwardLocal.Normalize();

        Vector3 tailLocal = rearLocal - forwardLocal * Mathf.Clamp(
            rearRadius * 0.55f,
            0.14f,
            0.22f
        );
        float rearWheelTop = rearLocal.y + rearRadius;
        tailLocal.y = Mathf.Lerp(rearWheelTop, bodyBounds.max.y, 0.6f);

        Transform parent = bikeModel != null ? bikeModel : root.transform;
        Transform anchor = FindTransform(root, "Franklin Rear Brake Flare");
        if (anchor == null)
        {
            anchor = GetOrCreateChild(parent, "Franklin Rear Brake Flare");
        }
        else if (anchor.parent != parent)
        {
            anchor.SetParent(parent, true);
        }

        anchor.SetPositionAndRotation(
            root.transform.TransformPoint(tailLocal),
            root.transform.rotation
        );
        anchor.localScale = Vector3.one;

        Light redLight = anchor.GetComponent<Light>();
        if (redLight == null) redLight = anchor.gameObject.AddComponent<Light>();
        redLight.type = LightType.Point;
        redLight.color = new Color(1f, 0.015f, 0.005f, 1f);
        redLight.intensity = 0f;
        redLight.range = 3.2f;
        redLight.bounceIntensity = 0f;
        redLight.shadows = LightShadows.None;
        redLight.renderMode = LightRenderMode.ForcePixel;
        redLight.lightmapBakeType = LightmapBakeType.Realtime;

        LensFlareComponentSRP flare = anchor.GetComponent<LensFlareComponentSRP>();
        if (flare == null)
            flare = anchor.gameObject.AddComponent<LensFlareComponentSRP>();
        flare.lensFlareData = flareData;
        flare.lightOverride = redLight;
        flare.intensity = 0f;
        flare.scale = 1f;
        flare.maxAttenuationDistance = 80f;
        flare.maxAttenuationScale = 40f;
        flare.attenuationByLightShape = false;
        flare.useOcclusion = true;
        flare.occlusionRadius = 0.035f;
        flare.occlusionOffset = 0.035f;
        flare.sampleCount = 16;
        flare.allowOffScreen = false;
        flare.distanceAttenuationCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.12f)
        );
        flare.scaleByDistanceCurve = new AnimationCurve(
            new Keyframe(0f, 0.8f),
            new Keyframe(1f, 1.3f)
        );

        FranklinBikeBrakeReverseFlare behaviour =
            anchor.GetComponent<FranklinBikeBrakeReverseFlare>();
        if (behaviour == null)
            behaviour = anchor.gameObject.AddComponent<FranklinBikeBrakeReverseFlare>();
        behaviour.Configure(driver, controller, rigidbody, redLight, flare);

        EditorUtility.SetDirty(anchor);
        EditorUtility.SetDirty(redLight);
        EditorUtility.SetDirty(flare);
        EditorUtility.SetDirty(behaviour);
    }

    private static void EnsureMirroredEntryAnimation()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            EntryAnimationPath
        );
        if (source == null)
        {
            throw new FileNotFoundException(
                "Bike entry animation is missing",
                EntryAnimationPath
            );
        }

        EnsureAssetFolder("Assets/FranklinAnimations", "Generated");
        EnsureAssetFolder("Assets/FranklinAnimations/Generated", "BikeEntry");
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(
                MirroredEntryAnimationPath
            ) == null && !AssetDatabase.CopyAsset(
                EntryAnimationPath,
                MirroredEntryAnimationPath
            ))
        {
            throw new InvalidOperationException(
                "Unity could not create the mirrored bike entry animation."
            );
        }

        AnimationClip mirrored = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            MirroredEntryAnimationPath
        );
        SerializedObject serializedClip = new SerializedObject(mirrored);
        SerializedProperty mirrorProperty = serializedClip.FindProperty(
            "m_AnimationClipSettings.m_Mirror"
        );
        if (mirrorProperty == null)
        {
            throw new InvalidOperationException(
                "Unity did not expose the Humanoid animation mirror setting."
            );
        }

        mirrorProperty.boolValue = true;
        serializedClip.ApplyModifiedPropertiesWithoutUndo();
        mirrored.name = "Character_Enter_Bike_Mirrored";
        EditorUtility.SetDirty(mirrored);
    }

    private static void EnsureAssetFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static bool AllPrefabsExist()
    {
        return Enumerable.Range(1, 10).All(index => File.Exists(GetBikePath(index)));
    }

    private static bool AllPrefabsIntegrated()
    {
        return Enumerable.Range(1, 10).All(index =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetBikePath(index));
            return prefab != null &&
                   prefab.GetComponent<ArcadeBikeControllerPro>() != null &&
                   prefab.GetComponent<FranklinArcadeBikeDriver>() != null &&
                   prefab.GetComponent<PhysicsBikeController>() == null &&
                   prefab.GetComponentsInChildren<WheelCollider>(true).Length == 0 &&
                   prefab.GetComponentsInChildren<Canvas>(true).Length == 0 &&
                   !HasNamedTransform(prefab, "HUD_Bike") &&
                   HasCompleteMirroredEntry(prefab, prefab.GetComponent<BikeEntry>()) &&
                   HasCompleteRearBrakeFlare(
                       prefab.GetComponentInChildren<FranklinBikeBrakeReverseFlare>(true)
                   ) &&
                   prefab.GetComponent<FranklinBikeImpactAudio>() is
                       { IsConfigured: true } &&
                   prefab.GetComponent<FranklinBikeCrashRagdoll>() is
                       { IsConfigured: true, HasCurrentConfiguration: true } &&
                   prefab.GetComponent<FranklinArcadeBikeRagdoll>() is
                       { IsConfigured: true } &&
                   HasCompleteWheelVisuals(prefab.GetComponent<ArcadeBikeControllerPro>());
        });
    }

    private static string GetBikePath(int index)
    {
        string original = $"{BikesFolder}/Bike_{index:00}.prefab";
        if (File.Exists(original)) return original;
        string sport = $"{BikesFolder}/Bike_{index:00}_Sport.prefab";
        return File.Exists(sport) ? sport : original;
    }
}
#endif
