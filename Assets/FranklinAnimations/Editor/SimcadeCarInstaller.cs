using System;
using Ashsvp;
using FranklinGame.Vehicles;
using GameCreator.Runtime.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Vehicles.Editor
{
    /// <summary>
    /// Maintains only the migrated concrete Car prefab. Other vehicle templates
    /// are deliberately outside this installer's scope.
    /// </summary>
    [InitializeOnLoad]
    public static class SimcadeCarInstaller
    {
        private const string CarPrefabPath =
            "Assets/Ash Assets/Vehicle Integration/Prefabs/Vehicles/Car.prefab";
        private const string SedanPresetPath =
            "Assets/Ash Assets/Sim-Cade Vehicle Physics/Prefabs/Car Presets/Ash_Sedan Prefab.prefab";
        private const string ChaseCameraPath =
            "Assets/Ash Assets/Sim-Cade Vehicle Physics/Prefabs/Camera Rigs/CinemachineCamera_Chase.prefab";
        private const string MobileInputPath =
            "Assets/Ash Assets/Sim-Cade Vehicle Physics/Prefabs/Mobile Input Buttons.prefab";
        private const string EngineAudioPath =
            "Assets/Ash Assets/Sim-Cade Vehicle Physics/Audios/Engines/simple rev.wav";
        private const string GearAudioPath =
            "Assets/Ash Assets/Sim-Cade Vehicle Physics/Audios/Car Gear switch 2.wav";
        private const string DoorOpenAudioPath =
            "Assets/FranklinAnimations/Audio/Vehicles/door_opening.wav";
        private const string DoorCloseAudioPath =
            "Assets/FranklinAnimations/Audio/Vehicles/door_closing.wav";
        private const string LightImpactAudioPath =
            "Assets/FranklinAnimations/Audio/Vehicles/car_impact_light.wav";
        private const string HeavyImpactAudioPath =
            "Assets/FranklinAnimations/Audio/Vehicles/car_impact_heavy.wav";
        private const string CollisionEffectPath =
            "Assets/Ash Assets/Sim-Cade Vehicle Physics/Prefabs/Collision Spark.prefab";
        private const string MovingExitAnimationPath =
            "Assets/FranklinAnimations/Animations/Vehicles/Carjacking/CarGetKickedOutL.anim";
        private const string MovingExitLandingAnimationPath =
            "Assets/FranklinAnimations/Animations/Vehicles/CarExitLanding_L.anim";
        private const string EntryAnimationPath =
            "Assets/Ash Assets/Vehicle Integration/Animations/Vehicles/Character_Enter_Car.anim";
        private const string MirroredEntryAnimationPath =
            "Assets/FranklinAnimations/Generated/CarEntry/Character_Enter_Car_Mirrored.anim";

        static SimcadeCarInstaller()
        {
            EditorApplication.update += TryInstallAutomatically;
        }

        [MenuItem("Tools/Franklin Game/Install Sim-Cade Car")]
        public static void Install()
        {
            EnsureMirroredEntryAnimation();
            GameObject carAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            GameObject presetAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SedanPresetPath);
            GameObject chaseCamera = AssetDatabase.LoadAssetAtPath<GameObject>(ChaseCameraPath);
            GameObject mobileInput = AssetDatabase.LoadAssetAtPath<GameObject>(MobileInputPath);
            AudioClip engineClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EngineAudioPath);
            AudioClip gearClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GearAudioPath);
            AudioClip doorOpenClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorOpenAudioPath);
            AudioClip doorCloseClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorCloseAudioPath);
            AudioClip lightImpactClip = AssetDatabase.LoadAssetAtPath<AudioClip>(LightImpactAudioPath);
            AudioClip heavyImpactClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HeavyImpactAudioPath);
            GameObject collisionEffect = AssetDatabase.LoadAssetAtPath<GameObject>(CollisionEffectPath);
            AnimationClip movingExit = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                MovingExitAnimationPath
            );
            AnimationClip movingExitLanding = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                MovingExitLandingAnimationPath
            );
            AnimationClip mirroredEntry = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                MirroredEntryAnimationPath
            );

            if (carAsset == null) throw new InvalidOperationException($"Missing car prefab: {CarPrefabPath}");
            if (presetAsset == null) throw new InvalidOperationException($"Missing Sim-Cade preset: {SedanPresetPath}");
            if (chaseCamera == null) throw new InvalidOperationException($"Missing Sim-Cade chase camera: {ChaseCameraPath}");
            if (mobileInput == null) throw new InvalidOperationException($"Missing Sim-Cade mobile UI: {MobileInputPath}");
            if (doorOpenClip == null || doorCloseClip == null)
                throw new InvalidOperationException(
                    "CC0 car-door open/close audio is missing from FranklinAnimations"
                );
            if (lightImpactClip == null || heavyImpactClip == null)
                throw new InvalidOperationException(
                    "Light/heavy car-impact audio is missing from FranklinAnimations"
                );
            if (collisionEffect == null)
                throw new InvalidOperationException(
                    "The Sim-Cade pooled collision spark/debris effect is missing"
                );
            if (movingExit == null || movingExitLanding == null)
                throw new InvalidOperationException(
                    "Moving-car exit animations are missing from FranklinAnimations"
                );
            if (mirroredEntry == null)
                throw new InvalidOperationException(
                    "The Humanoid-mirrored car-entry animation could not be created"
                );

            GameObject presetRoot = PrefabUtility.LoadPrefabContents(SedanPresetPath);
            GameObject carRoot = PrefabUtility.LoadPrefabContents(CarPrefabPath);

            try
            {
                SimcadeVehicleController presetController =
                    presetRoot.GetComponent<SimcadeVehicleController>();
                GearSystem presetGearSystem = presetRoot.GetComponent<GearSystem>();
                AudioSystem presetAudioSystem = presetRoot.GetComponent<AudioSystem>();
                Rigidbody presetRigidbody = presetRoot.GetComponent<Rigidbody>();

                if (presetController == null || presetGearSystem == null || presetAudioSystem == null)
                {
                    throw new InvalidOperationException("The Sim-Cade sedan preset is incomplete");
                }

                PhysicsCarController oldController = carRoot.GetComponent<PhysicsCarController>();
                if (oldController == null && carRoot.GetComponent<SimcadeCarDriver>() == null)
                {
                    throw new InvalidOperationException(
                        "Car.prefab has neither the source RVR controller nor an installed Sim-Cade driver"
                    );
                }

                SimcadeVehicleController existingController =
                    carRoot.GetComponent<SimcadeVehicleController>();
                Transform[] wheels = ResolveWheels(carRoot, oldController, existingController);

                WheelCollider[] sourceWheelColliders = oldController != null
                    ? new[]
                    {
                        oldController.frontLeftWheelCollider,
                        oldController.frontRightWheelCollider,
                        oldController.rearLeftWheelCollider,
                        oldController.rearRightWheelCollider
                    }
                    : carRoot.GetComponentsInChildren<WheelCollider>(true);

                Transform[] hardPoints = ResolveHardPoints(carRoot, sourceWheelColliders);
                Transform vehicleBody = oldController != null && oldController.carBody != null
                    ? oldController.carBody
                    : existingController != null && existingController.VehicleBody != null
                        ? existingController.VehicleBody
                        : FindTransform(carRoot, "MainBody");
                Transform centerOfMassAir = oldController != null && oldController.centerOfMass != null
                    ? oldController.centerOfMass
                    : existingController != null && existingController.CenterOfMass_air != null
                        ? existingController.CenterOfMass_air
                        : FindTransform(carRoot, "CenterOfGravity");
                float wheelRadius = ResolveWheelRadius(sourceWheelColliders, presetController.wheelRadius);

                VehicleAudioPhysics oldAudio = carRoot.GetComponent<VehicleAudioPhysics>();
                AudioSource engineSource = oldAudio != null
                    ? oldAudio.engineAudio
                    : FindAudioSource(carRoot, "AudioSource-Engine");
                AudioSource gearSource = oldAudio != null
                    ? oldAudio.driftAudio
                    : FindAudioSource(carRoot, "AudioSource-Drifting");

                SimcadeVehicleController controller = GetOrAdd<SimcadeVehicleController>(carRoot);
                EditorUtility.CopySerialized(presetController, controller);
                controller.Wheels = wheels;
                controller.HardPoints = hardPoints;
                controller.VehicleBody = vehicleBody;
                controller.CenterOfMass_air = centerOfMassAir;
                controller.wheelRadius = wheelRadius;
                controller.adaptTireSmokeBySpeed = true;
                controller.lowSpeedSmokeEndKph = 20f;
                controller.fullSmokeSpeedKph = 35f;
                controller.lowSpeedBrakeSmokeIntensity = 0.18f;

                AudioSystem audioSystem = GetOrAdd<AudioSystem>(carRoot);
                EditorUtility.CopySerialized(presetAudioSystem, audioSystem);
                audioSystem.engineSound = engineSource;
                audioSystem.GearSound = gearSource;
                ConfigureAudioSource(engineSource, engineClip, true);
                ConfigureAudioSource(gearSource, gearClip, false);

                GearSystem gearSystem = GetOrAdd<GearSystem>(carRoot);
                EditorUtility.CopySerialized(presetGearSystem, gearSystem);
                gearSystem.AudioSystem = audioSystem;

                SimcadeCarDriver driver = GetOrAdd<SimcadeCarDriver>(carRoot);
                Transform steeringWheel = FindTransform(carRoot, "Steering");
                driver.Configure(
                    controller,
                    gearSystem,
                    audioSystem,
                    chaseCamera,
                    mobileInput,
                    steeringWheel
                );

                AudioSource impactSource = EnsureImpactAudioSource(carRoot);
                SimcadeCarImpactAudio impactAudio = GetOrAdd<SimcadeCarImpactAudio>(carRoot);
                impactAudio.Configure(
                    impactSource,
                    lightImpactClip,
                    heavyImpactClip,
                    collisionEffect
                );

                Rigidbody rigidbody = carRoot.GetComponent<Rigidbody>();
                if (rigidbody != null && presetRigidbody != null)
                {
                    rigidbody.mass = presetRigidbody.mass;
                    rigidbody.linearDamping = presetRigidbody.linearDamping;
                    rigidbody.angularDamping = presetRigidbody.angularDamping;
                    rigidbody.interpolation = presetRigidbody.interpolation;
                    rigidbody.collisionDetectionMode = presetRigidbody.collisionDetectionMode;
                }

                RemoveLegacyCarDriving(carRoot, oldController);
                DisableLegacyHud(carRoot);

                CarEntry entry = carRoot.GetComponent<CarEntry>();
                if (entry != null)
                {
                    // Door, character entry/exit clips, seat and IK remain on CarEntry.
                    // Its former HUD instructions belonged to the removed RVR controller.
                    EnsureEntryAlignmentAnchors(carRoot, entry);
                    entry.mirroredEntryAnimation = mirroredEntry;
                    entry.entrySideMode = CarEntrySideMode.Automatic;
                    EnsurePassengerDoorEntry(carRoot, entry);
                    entry.movingExitAnimation = movingExit;
                    entry.movingExitLandingAnimation = movingExitLanding;
                    entry.movingExitLaunchFrameCount = 50;
                    entry.movingExitAnimationSpeed = 2.5f;
                    entry.fastExitSpeedKph = 50f;
                    entry.movingExitDoorReachIKCurve = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.15f, 1f),
                        new Keyframe(0.8f, 1f),
                        new Keyframe(1f, 0f)
                    );
                    entry.movingExitRagdollDuration = 2.25f;
                    entry.movingExitRagdollTumbleVelocity = 5.5f;
                    entry.movingExitAutoRecover = true;
                    entry.movingExitPlayerCameraDelay = 2f;
                    entry.movingExitDoorPartialCloseDuration = 2f;
                    entry.movingExitDoorRemainingOpen = 0.15f;
                    entry.doorAudioSource = EnsureDoorAudioSource(carRoot, entry);
                    entry.doorOpenSound = doorOpenClip;
                    entry.doorCloseSound = doorCloseClip;
                    entry.doorSoundVolume = 0.7f;
                    entry.onEnter = new InstructionList();
                    entry.onExit = new InstructionList();
                }

                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(audioSystem);
                EditorUtility.SetDirty(gearSystem);
                EditorUtility.SetDirty(driver);
                EditorUtility.SetDirty(impactAudio);
                if (entry != null) EditorUtility.SetDirty(entry);

                PrefabUtility.SaveAsPrefabAsset(carRoot, CarPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(carRoot);
                PrefabUtility.UnloadPrefabContents(presetRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateInstallation();
            Debug.Log(
                "Sim-Cade v1.8 installed on Car.prefab only. RVR car physics/WheelColliders were removed; " +
                "CarEntry door and entry/exit animation references were preserved."
            );
        }

        [MenuItem("Tools/Franklin Game/Validate Sim-Cade Car")]
        public static void ValidateInstallation()
        {
            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (car == null) throw new InvalidOperationException("Car.prefab is missing");

            SimcadeVehicleController controller = car.GetComponent<SimcadeVehicleController>();
            SimcadeCarDriver driver = car.GetComponent<SimcadeCarDriver>();
            SimcadeCarImpactAudio impactAudio = car.GetComponent<SimcadeCarImpactAudio>();
            GearSystem gearSystem = car.GetComponent<GearSystem>();
            AudioSystem audioSystem = car.GetComponent<AudioSystem>();
            CarEntry entry = car.GetComponent<CarEntry>();

            if (controller == null || driver == null || gearSystem == null || audioSystem == null)
                throw new InvalidOperationException("The complete Sim-Cade runtime stack is not installed");
            if (!HasImpactAudioSetup(impactAudio))
                throw new InvalidOperationException(
                    "Light/heavy collision audio is not configured on the exact Car.prefab"
                );
            if (car.GetComponent<PhysicsCarController>() != null)
                throw new InvalidOperationException("Legacy PhysicsCarController is still attached");
            if (car.GetComponent<SimcadeRvrCarPhysics>() != null)
                throw new InvalidOperationException("The old Sim-Cade approximation adapter is still attached");
            if (car.GetComponentsInChildren<WheelCollider>(true).Length != 0)
                throw new InvalidOperationException("Legacy WheelColliders are still attached");
            if (!HasCompleteSimcadeSetup(controller))
                throw new InvalidOperationException(
                    "Sim-Cade needs four non-null wheel targets and suspension hard points"
                );
            if (!controller.adaptTireSmokeBySpeed ||
                controller.lowSpeedSmokeEndKph < 19.9f ||
                controller.fullSmokeSpeedKph < 34.9f)
            {
                throw new InvalidOperationException(
                    "Adaptive low-speed tire smoke is not configured on Car.prefab"
                );
            }

            SerializedObject serializedDriver = new SerializedObject(driver);
            if (serializedDriver.FindProperty("m_ChaseCameraPrefab").objectReferenceValue == null ||
                serializedDriver.FindProperty("m_MobileInputPrefab").objectReferenceValue == null ||
                serializedDriver.FindProperty("m_SteeringWheel").objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    "Sim-Cade camera, mobile UI or steering-wheel visual is not assigned"
                );
            }

            if (entry == null || entry.doorTransform == null ||
                entry.entryAnimation == null || entry.exitAnimation == null ||
                entry.movingExitAnimation == null ||
                entry.movingExitAnimation.name != "CarGetKickedOutL" ||
                entry.movingExitLaunchFrameCount != 50 ||
                entry.movingExitAnimationSpeed < 2.49f ||
                entry.fastExitSpeedKph < 49.9f ||
                entry.movingExitDoorReachIKCurve == null ||
                entry.movingExitDoorReachIKCurve.length < 4 ||
                entry.movingExitRagdollDuration < 2f ||
                entry.movingExitRagdollTumbleVelocity <= 0f ||
                entry.movingExitPlayerCameraDelay < 1.99f ||
                entry.movingExitDoorPartialCloseDuration < 1.99f ||
                entry.movingExitDoorRemainingOpen < 0.1f)
            {
                throw new InvalidOperationException(
                    "CarEntry door, 50 km/h exit threshold, moving exit or ragdoll are not configured"
                );
            }

            if (!HasDoorAudioSetup(entry))
                throw new InvalidOperationException(
                    "CarEntry door open/close audio is not assigned to its dedicated 3D source"
                );

            if (!HasEntryAlignmentSetup(entry))
            {
                throw new InvalidOperationException(
                    "Driver-door standing, doorway or exterior handle targets are incomplete"
                );
            }

            if (!HasMirroredEntrySetup(entry))
                throw new InvalidOperationException(
                    "The passenger-door mirrored entry, door or cabin path is not configured"
                );

            Debug.Log(
                "Sim-Cade Car validation passed: isolated controller, four wheels, " +
                "adaptive low-speed smoke, light/heavy collision audio and pooled impact FX, " +
                "camera/mobile assets, " +
                "driver/passenger door entry, door animations/audio and " +
                "live-edit anchors are wired."
            );
        }

        private static void TryInstallAutomatically()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            GameObject preset = AssetDatabase.LoadAssetAtPath<GameObject>(SedanPresetPath);
            GameObject camera = AssetDatabase.LoadAssetAtPath<GameObject>(ChaseCameraPath);
            GameObject mobile = AssetDatabase.LoadAssetAtPath<GameObject>(MobileInputPath);
            if (car == null || preset == null || camera == null || mobile == null) return;

            bool installed = car.GetComponent<SimcadeCarDriver>() != null &&
                HasCompleteSimcadeSetup(car.GetComponent<SimcadeVehicleController>()) &&
                HasCompleteDriverSetup(car.GetComponent<SimcadeCarDriver>()) &&
                HasEntryAlignmentSetup(car.GetComponent<CarEntry>()) &&
                HasMirroredEntrySetup(car.GetComponent<CarEntry>()) &&
                HasDoorAudioSetup(car.GetComponent<CarEntry>()) &&
                HasImpactAudioSetup(car.GetComponent<SimcadeCarImpactAudio>()) &&
                car.GetComponent<PhysicsCarController>() == null &&
                car.GetComponentsInChildren<WheelCollider>(true).Length == 0;

            EditorApplication.update -= TryInstallAutomatically;
            if (installed)
            {
                ValidateInstallation();
                return;
            }

            try
            {
                Install();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static Transform[] ResolveHardPoints(
            GameObject carRoot,
            WheelCollider[] sourceWheelColliders)
        {
            return new[]
            {
                ResolveHardPoint(carRoot, sourceWheelColliders, 0, "FrontLeftWheelCollider"),
                ResolveHardPoint(carRoot, sourceWheelColliders, 1, "FrontRightWheelCollider"),
                ResolveHardPoint(carRoot, sourceWheelColliders, 2, "RearLeftWheelCollider"),
                ResolveHardPoint(carRoot, sourceWheelColliders, 3, "RearRightWheelCollider")
            };
        }

        private static Transform[] ResolveWheels(
            GameObject carRoot,
            PhysicsCarController oldController,
            SimcadeVehicleController existingController)
        {
            Transform[] existing = existingController != null
                ? existingController.Wheels
                : null;

            return new[]
            {
                ResolveWheel(
                    carRoot,
                    oldController != null ? oldController.frontLeftWheelTransform : null,
                    existing,
                    0,
                    "FLTarget"
                ),
                ResolveWheel(
                    carRoot,
                    oldController != null ? oldController.frontRightWheelTransform : null,
                    existing,
                    1,
                    "FRTarget"
                ),
                ResolveWheel(
                    carRoot,
                    oldController != null ? oldController.rearLeftWheelTransform : null,
                    existing,
                    2,
                    "RLTarget"
                ),
                ResolveWheel(
                    carRoot,
                    oldController != null ? oldController.rearRightWheelTransform : null,
                    existing,
                    3,
                    "RRTarget"
                )
            };
        }

        private static Transform ResolveWheel(
            GameObject carRoot,
            Transform oldWheel,
            Transform[] existingWheels,
            int index,
            string fallbackName)
        {
            if (oldWheel != null) return oldWheel;
            if (existingWheels != null && index < existingWheels.Length &&
                existingWheels[index] != null)
            {
                return existingWheels[index];
            }

            return FindTransform(carRoot, fallbackName);
        }

        private static Transform ResolveHardPoint(
            GameObject carRoot,
            WheelCollider[] sourceWheelColliders,
            int index,
            string fallbackName)
        {
            if (sourceWheelColliders != null && index < sourceWheelColliders.Length &&
                sourceWheelColliders[index] != null)
            {
                return sourceWheelColliders[index].transform;
            }

            return FindTransform(carRoot, fallbackName);
        }

        private static bool HasCompleteSimcadeSetup(SimcadeVehicleController controller)
        {
            if (controller == null || controller.Wheels == null ||
                controller.HardPoints == null || controller.Wheels.Length != 4 ||
                controller.HardPoints.Length != 4)
            {
                return false;
            }

            for (int index = 0; index < 4; index++)
            {
                if (controller.Wheels[index] == null || controller.HardPoints[index] == null)
                    return false;
            }

            return true;
        }

        private static bool HasCompleteDriverSetup(SimcadeCarDriver driver)
        {
            if (driver == null) return false;

            SerializedObject serializedDriver = new SerializedObject(driver);
            return serializedDriver.FindProperty("m_ChaseCameraPrefab").objectReferenceValue != null &&
                serializedDriver.FindProperty("m_MobileInputPrefab").objectReferenceValue != null &&
                serializedDriver.FindProperty("m_SteeringWheel").objectReferenceValue != null;
        }

        private static bool HasEntryAlignmentSetup(CarEntry entry)
        {
            return entry != null && entry.entryStandingPoint != null &&
                entry.entryStepPoint != null && entry.entryParent != null &&
                entry.doorHandleTarget != null;
        }

        private static bool HasMirroredEntrySetup(CarEntry entry)
        {
            if (entry == null || entry.mirroredEntryAnimation == null ||
                entry.passengerEntryStandingPoint == null ||
                entry.passengerEntryStepPoint == null ||
                entry.passengerEntryCabinPoint == null ||
                entry.passengerDoorTransform == null ||
                entry.passengerDoorHandleTarget == null ||
                entry.passengerDoorAudioSource == null)
            {
                return false;
            }

            SerializedObject serializedClip = new SerializedObject(
                entry.mirroredEntryAnimation
            );
            SerializedProperty mirror = serializedClip.FindProperty(
                "m_AnimationClipSettings.m_Mirror"
            );
            Transform symmetryRoot = entry.doorTransform != null
                ? entry.doorTransform.parent
                : null;
            return mirror?.boolValue == true && symmetryRoot != null &&
                IsMirroredPosition(
                    symmetryRoot,
                    entry.entryStandingPoint,
                    entry.passengerEntryStandingPoint
                ) &&
                IsMirroredPosition(
                    symmetryRoot,
                    entry.entryStepPoint,
                    entry.passengerEntryStepPoint
                ) &&
                IsMirroredPosition(
                    symmetryRoot,
                    entry.entryParent,
                    entry.passengerEntryCabinPoint
                ) &&
                IsMirroredPosition(
                    symmetryRoot,
                    entry.doorHandleTarget,
                    entry.passengerDoorHandleTarget
                ) &&
                IsMirroredPosition(
                    symmetryRoot,
                    FindTransform(entry.gameObject, "Triggers_Enter/Exit"),
                    FindTransform(entry.gameObject, "Triggers_Enter/Exit Passenger")
                );
        }

        private static bool IsMirroredPosition(
            Transform symmetryRoot,
            Transform source,
            Transform candidate)
        {
            if (symmetryRoot == null || source == null || candidate == null) return false;
            Vector3 expected = symmetryRoot.InverseTransformPoint(source.position);
            expected.x = -expected.x;
            Vector3 actual = symmetryRoot.InverseTransformPoint(candidate.position);
            return Vector3.SqrMagnitude(expected - actual) <= 0.0004f;
        }

        private static bool HasDoorAudioSetup(CarEntry entry)
        {
            return entry != null && entry.doorAudioSource != null &&
                entry.doorOpenSound != null && entry.doorCloseSound != null;
        }

        private static AudioSource EnsureDoorAudioSource(GameObject carRoot, CarEntry entry)
        {
            return EnsureDoorAudioSource(
                carRoot,
                entry.doorTransform != null ? entry.doorTransform : carRoot.transform,
                "AudioSource-Door"
            );
        }

        private static AudioSource EnsureDoorAudioSource(
            GameObject carRoot,
            Transform parent,
            string objectName)
        {
            Transform audioTransform = FindTransform(carRoot, objectName);
            if (audioTransform == null)
            {
                GameObject audioObject = new GameObject(objectName);
                audioTransform = audioObject.transform;
                audioTransform.SetParent(parent, false);
            }

            AudioSource source = GetOrAdd<AudioSource>(audioTransform.gameObject);
            source.clip = null;
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = 1.5f;
            source.maxDistance = 35f;
            return source;
        }

        private static bool HasImpactAudioSetup(SimcadeCarImpactAudio impactAudio)
        {
            return impactAudio != null && impactAudio.IsConfigured;
        }

        private static AudioSource EnsureImpactAudioSource(GameObject carRoot)
        {
            Transform audioTransform = FindTransform(carRoot, "AudioSource-Impact");
            if (audioTransform == null)
            {
                GameObject audioObject = new GameObject("AudioSource-Impact");
                audioTransform = audioObject.transform;
                audioTransform.SetParent(carRoot.transform, false);
            }

            AudioSource source = GetOrAdd<AudioSource>(audioTransform.gameObject);
            source.clip = null;
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.priority = 96;
            source.minDistance = 6f;
            source.maxDistance = 70f;
            return source;
        }

        private static void EnsureEntryAlignmentAnchors(GameObject carRoot, CarEntry entry)
        {
            if (entry.entryStandingPoint == null)
            {
                Transform standingPoint = FindTransform(carRoot, "Entry Standing Point");
                if (standingPoint == null)
                {
                    GameObject pointObject = new GameObject("Entry Standing Point");
                    standingPoint = pointObject.transform;
                    standingPoint.SetParent(carRoot.transform, false);

                    Transform interaction = FindTransform(carRoot, "Triggers_Enter/Exit");
                    if (interaction != null)
                    {
                        standingPoint.SetPositionAndRotation(
                            interaction.position,
                            interaction.rotation
                        );
                    }
                    else
                    {
                        standingPoint.localPosition = new Vector3(-1.75f, -0.4f, -0.45f);
                    }
                }

                entry.entryStandingPoint = standingPoint;
            }

            if (entry.entryStepPoint == null)
            {
                Transform stepPoint = FindTransform(carRoot, "Entry Step Point");
                if (stepPoint == null)
                {
                    GameObject stepObject = new GameObject("Entry Step Point");
                    stepPoint = stepObject.transform;
                    stepPoint.SetParent(carRoot.transform, false);

                    Transform start = entry.entryStandingPoint;
                    Transform seat = entry.entryParent;
                    if (start != null && seat != null)
                    {
                        float stepTime = 0.52f;
                        stepPoint.SetPositionAndRotation(
                            Vector3.Lerp(start.position, seat.position, stepTime),
                            Quaternion.Slerp(start.rotation, seat.rotation, stepTime)
                        );
                    }
                }

                entry.entryStepPoint = stepPoint;
            }

            entry.useAuthoredEntryPath = true;
            entry.entryStepNormalizedTime = 0.52f;
            RemoveObsoleteEntryAnchor(carRoot, "Entry Cabin Point");
            RemoveObsoleteEntryAnchor(carRoot, "Entry Left Foot Target");
            RemoveObsoleteEntryAnchor(carRoot, "Entry Right Foot Target");

            if (entry.doorHandleTarget == null && entry.doorTransform != null)
            {
                Transform handleTarget = FindTransform(entry.doorTransform.gameObject, "Door Handle Target");
                if (handleTarget == null)
                {
                    GameObject targetObject = new GameObject("Door Handle Target");
                    handleTarget = targetObject.transform;
                    handleTarget.SetParent(entry.doorTransform, false);
                    handleTarget.SetPositionAndRotation(
                        GuessDoorHandlePosition(carRoot.transform, entry.doorTransform),
                        Quaternion.LookRotation(-carRoot.transform.right, carRoot.transform.up)
                    );
                }

                entry.doorHandleTarget = handleTarget;
                entry.doorHandleHand = AvatarIKGoal.RightHand;
            }
        }

        private static void EnsurePassengerDoorEntry(GameObject carRoot, CarEntry entry)
        {
            if (entry.entryStandingPoint == null || entry.entryStepPoint == null ||
                entry.entryParent == null || entry.doorTransform == null)
            {
                return;
            }

            // The imported sedan mesh is offset from the prefab root. MainBody's
            // local YZ plane is the real left/right symmetry plane of the cabin.
            Transform mainBody = FindTransform(carRoot, "MainBody") ?? carRoot.transform;
            entry.passengerEntryStandingPoint = EnsureMirroredEntryAnchor(
                carRoot,
                "Passenger Entry Standing Point",
                entry.entryStandingPoint,
                mainBody
            );
            entry.passengerEntryStepPoint = EnsureMirroredEntryAnchor(
                carRoot,
                "Passenger Entry Step Point",
                entry.entryStepPoint,
                mainBody
            );
            entry.passengerEntryCabinPoint = EnsureMirroredEntryAnchor(
                carRoot,
                "Passenger Entry Cabin Point",
                entry.entryParent,
                mainBody
            );
            entry.passengerCabinNormalizedTime = 0.78f;

            Transform passengerHook = FindTransform(carRoot, "Passenger Door Hook");
            if (passengerHook == null)
            {
                GameObject hookObject = new GameObject("Passenger Door Hook");
                passengerHook = hookObject.transform;
                passengerHook.SetParent(mainBody, false);

                Vector3 leftHookLocal = mainBody.InverseTransformPoint(
                    entry.doorTransform.position
                );
                leftHookLocal.x = -leftHookLocal.x;
                passengerHook.localPosition = leftHookLocal;
                passengerHook.localRotation = Quaternion.identity;
            }

            Transform passengerDoorMesh = FindTransform(carRoot, "DoorFR");
            if (passengerDoorMesh != null && passengerDoorMesh != passengerHook &&
                passengerDoorMesh.parent != passengerHook)
            {
                passengerDoorMesh.SetParent(passengerHook, true);
            }

            entry.passengerDoorTransform = passengerHook;
            entry.passengerDoorOpenRotation = new Vector3(
                entry.doorOpenRotation.x,
                -entry.doorOpenRotation.y,
                -entry.doorOpenRotation.z
            );

            Transform handle = FindTransform(passengerHook.gameObject, "Passenger Door Handle Target");
            if (handle == null)
            {
                GameObject handleObject = new GameObject("Passenger Door Handle Target");
                handle = handleObject.transform;
                handle.SetParent(passengerHook, false);
            }
            MirrorTransformAcrossCar(mainBody, entry.doorHandleTarget, handle);
            entry.passengerDoorHandleTarget = handle;
            entry.passengerDoorAudioSource = EnsureDoorAudioSource(
                carRoot,
                passengerHook,
                "AudioSource-Door-Passenger"
            );
            EnsurePassengerInteractionTrigger(carRoot, mainBody);
        }

        private static void EnsurePassengerInteractionTrigger(
            GameObject carRoot,
            Transform symmetryRoot)
        {
            Transform passengerTrigger = FindTransform(
                carRoot,
                "Triggers_Enter/Exit Passenger"
            );
            Transform driverTrigger = FindTransform(carRoot, "Triggers_Enter/Exit");
            if (passengerTrigger == null && driverTrigger != null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(
                    driverTrigger.gameObject,
                    carRoot.transform
                );
                clone.name = "Triggers_Enter/Exit Passenger";
                passengerTrigger = clone.transform;
            }

            if (driverTrigger != null && passengerTrigger != null)
            {
                MirrorTransformAcrossCar(symmetryRoot, driverTrigger, passengerTrigger);
            }
        }

        private static Transform EnsureMirroredEntryAnchor(
            GameObject carRoot,
            string name,
            Transform source,
            Transform symmetryRoot)
        {
            Transform anchor = FindTransform(carRoot, name);
            if (anchor == null)
            {
                GameObject anchorObject = new GameObject(name);
                anchor = anchorObject.transform;
                anchor.SetParent(carRoot.transform, false);
            }
            MirrorTransformAcrossCar(symmetryRoot, source, anchor);
            return anchor;
        }

        private static void MirrorTransformAcrossCar(
            Transform carRoot,
            Transform source,
            Transform target)
        {
            if (carRoot == null || source == null || target == null) return;

            Vector3 localPosition = carRoot.InverseTransformPoint(source.position);
            localPosition.x = -localPosition.x;

            Vector3 localForward = carRoot.InverseTransformDirection(source.forward);
            Vector3 localUp = carRoot.InverseTransformDirection(source.up);
            localForward.x = -localForward.x;
            localUp.x = -localUp.x;

            target.SetPositionAndRotation(
                carRoot.TransformPoint(localPosition),
                Quaternion.LookRotation(
                    carRoot.TransformDirection(localForward),
                    carRoot.TransformDirection(localUp)
                )
            );
        }

        private static void RemoveObsoleteEntryAnchor(GameObject carRoot, string name)
        {
            Transform anchor = FindTransform(carRoot, name);
            if (anchor != null) UnityEngine.Object.DestroyImmediate(anchor.gameObject);
        }

        private static Vector3 GuessDoorHandlePosition(Transform carRoot, Transform door)
        {
            Renderer[] renderers = door.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Vector3 minimum = Vector3.zero;
            Vector3 maximum = Vector3.zero;

            foreach (Renderer renderer in renderers)
            {
                Bounds localBounds = renderer.localBounds;
                Vector3 center = localBounds.center;
                Vector3 extents = localBounds.extents;

                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(
                        extents,
                        new Vector3(x, y, z)
                    );
                    Vector3 carLocal = carRoot.InverseTransformPoint(
                        renderer.transform.TransformPoint(corner)
                    );

                    if (!hasBounds)
                    {
                        minimum = carLocal;
                        maximum = carLocal;
                        hasBounds = true;
                    }
                    else
                    {
                        minimum = Vector3.Min(minimum, carLocal);
                        maximum = Vector3.Max(maximum, carLocal);
                    }
                }
            }

            if (!hasBounds)
                return door.position + carRoot.up + -carRoot.right * 0.05f;

            Vector3 guess = new Vector3(
                minimum.x - 0.03f,
                Mathf.Lerp(minimum.y, maximum.y, 0.65f),
                Mathf.Lerp(minimum.z, maximum.z, 0.3f)
            );
            return carRoot.TransformPoint(guess);
        }

        private static float ResolveWheelRadius(WheelCollider[] wheelColliders, float fallback)
        {
            if (wheelColliders != null)
            {
                foreach (WheelCollider wheelCollider in wheelColliders)
                {
                    if (wheelCollider != null && wheelCollider.radius > 0f)
                        return wheelCollider.radius;
                }
            }

            return fallback;
        }

        private static void EnsureMirroredEntryAnimation()
        {
            AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                EntryAnimationPath
            );
            if (source == null)
                throw new InvalidOperationException(
                    $"Car-entry animation is missing: {EntryAnimationPath}"
                );

            EnsureAssetFolder("Assets/FranklinAnimations", "Generated");
            EnsureAssetFolder("Assets/FranklinAnimations/Generated", "CarEntry");
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    MirroredEntryAnimationPath
                ) == null && !AssetDatabase.CopyAsset(
                    EntryAnimationPath,
                    MirroredEntryAnimationPath
                ))
            {
                throw new InvalidOperationException(
                    "Unity could not create the mirrored car-entry animation"
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
                throw new InvalidOperationException(
                    "Unity did not expose the Humanoid animation mirror setting"
                );

            mirrorProperty.boolValue = true;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            mirrored.name = "Character_Enter_Car_Mirrored";
            EditorUtility.SetDirty(mirrored);
        }

        private static void EnsureAssetFolder(string parent, string child)
        {
            string assetPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void RemoveLegacyCarDriving(
            GameObject carRoot,
            PhysicsCarController oldController)
        {
            DestroyComponent(carRoot.GetComponent<SimcadeRvrCarPhysics>());
            DestroyComponent(oldController);
            DestroyComponent(carRoot.GetComponent<VehicleAudioPhysics>());
            DestroyComponent(carRoot.GetComponent<VehicleVFX>());
            DestroyComponent(carRoot.GetComponent<VehicleDeformation>());

            foreach (RPMDisplayUI display in carRoot.GetComponentsInChildren<RPMDisplayUI>(true))
            {
                DestroyComponent(display);
            }

            foreach (WheelCollider wheelCollider in carRoot.GetComponentsInChildren<WheelCollider>(true))
            {
                DestroyComponent(wheelCollider);
            }
        }

        private static void DisableLegacyHud(GameObject carRoot)
        {
            Transform hud = FindTransform(carRoot, "HUD_Car");
            if (hud != null) hud.gameObject.SetActive(false);
        }

        private static void ConfigureAudioSource(AudioSource source, AudioClip clip, bool loop)
        {
            if (source == null) return;
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1f;
            source.maxDistance = 80f;
        }

        private static AudioSource FindAudioSource(GameObject root, string name)
        {
            Transform transform = FindTransform(root, name);
            return transform != null ? transform.GetComponent<AudioSource>() : null;
        }

        private static Transform FindTransform(GameObject root, string name)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name) return transform;
            }

            return null;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void DestroyComponent(Component component)
        {
            if (component != null) UnityEngine.Object.DestroyImmediate(component, true);
        }
    }
}
