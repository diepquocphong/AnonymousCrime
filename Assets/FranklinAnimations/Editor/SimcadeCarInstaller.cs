using System;
using Ashsvp;
using FranklinGame.Vehicles;
using GameCreator.Runtime.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Vehicles.Editor
{
    /// <summary>
    /// Converts only RapidTemplate's concrete Car prefab. Other car templates,
    /// bikes and hover vehicles are deliberately outside this installer's scope.
    /// </summary>
    [InitializeOnLoad]
    public static class SimcadeCarInstaller
    {
        private const string CarPrefabPath =
            "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Car.prefab";
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

        static SimcadeCarInstaller()
        {
            EditorApplication.update += TryInstallAutomatically;
        }

        [MenuItem("Tools/Franklin Game/Install Sim-Cade Car")]
        public static void Install()
        {
            GameObject carAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            GameObject presetAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SedanPresetPath);
            GameObject chaseCamera = AssetDatabase.LoadAssetAtPath<GameObject>(ChaseCameraPath);
            GameObject mobileInput = AssetDatabase.LoadAssetAtPath<GameObject>(MobileInputPath);
            AudioClip engineClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EngineAudioPath);
            AudioClip gearClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GearAudioPath);

            if (carAsset == null) throw new InvalidOperationException($"Missing car prefab: {CarPrefabPath}");
            if (presetAsset == null) throw new InvalidOperationException($"Missing Sim-Cade preset: {SedanPresetPath}");
            if (chaseCamera == null) throw new InvalidOperationException($"Missing Sim-Cade chase camera: {ChaseCameraPath}");
            if (mobileInput == null) throw new InvalidOperationException($"Missing Sim-Cade mobile UI: {MobileInputPath}");

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
                    entry.onEnter = new InstructionList();
                    entry.onExit = new InstructionList();
                }

                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(audioSystem);
                EditorUtility.SetDirty(gearSystem);
                EditorUtility.SetDirty(driver);
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
            GearSystem gearSystem = car.GetComponent<GearSystem>();
            AudioSystem audioSystem = car.GetComponent<AudioSystem>();
            CarEntry entry = car.GetComponent<CarEntry>();

            if (controller == null || driver == null || gearSystem == null || audioSystem == null)
                throw new InvalidOperationException("The complete Sim-Cade runtime stack is not installed");
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
                entry.entryAnimation == null || entry.exitAnimation == null)
            {
                throw new InvalidOperationException(
                    "CarEntry door or character entry/exit animation references were lost"
                );
            }

            if (!HasEntryAlignmentSetup(entry))
            {
                throw new InvalidOperationException(
                    "Entry standing, doorway step or exterior door-handle target is not assigned"
                );
            }

            Debug.Log("Sim-Cade Car validation passed: isolated controller, four wheels, camera/mobile assets, door animations and live-edit anchors are wired.");
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
                entry.entryStepPoint != null && entry.doorHandleTarget != null;
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
