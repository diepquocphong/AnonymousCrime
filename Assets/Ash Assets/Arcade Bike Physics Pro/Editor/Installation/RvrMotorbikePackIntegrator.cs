#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using AnimatorControllerAsset = UnityEditor.Animations.AnimatorController;
using AnimatorControllerLayerAsset = UnityEditor.Animations.AnimatorControllerLayer;

/// <summary>
/// Installs RapidTemplate's motorbike stack on the DQP motorbike prefabs while
/// preserving each prefab's GUID and original root Transform/file ID.
/// </summary>
[InitializeOnLoad]
public static class RvrMotorbikePackIntegrator
{
    private const string TemplatePath =
        "Assets/Ash Assets/Vehicle Integration/Vehicles/Bike/Prefabs/Empty-Motorbike.prefab";

    private const string BikesFolder =
        "Assets/Ash Assets/Arcade Bike Physics Pro/Prefabs/Bikes";

    private const string PlayerAnimatorControllerPath =
        "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/Controllers/CompleteLocomotion.controller";

    private const string AutoRunSessionKey =
        "FranklinGame.RvrMotorbikePackIntegrator.AutoRun.v6";

    static RvrMotorbikePackIntegrator()
    {
        // ArcadeBikePackIntegrator now owns bike setup and validation. Keep this
        // legacy tool available only as a compatibility menu entry.
    }

    [MenuItem("Tools/Franklin/RVR/Integrate DQP Motorbike Pack")]
    public static void IntegrateAllFromMenu()
    {
        ArcadeBikePackIntegrator.IntegrateAllFromMenu();
    }

    [MenuItem("Tools/Franklin/RVR/Validate DQP Motorbike Pack")]
    public static void ValidateAllFromMenu()
    {
        ArcadeBikePackIntegrator.ValidateAllFromMenu();
    }

    /// <summary>
    /// Batch-mode entry point for CI or for rebuilding the pack from the command line.
    /// </summary>
    public static void IntegrateAllBatchMode()
    {
        ArcadeBikePackIntegrator.IntegrateAllBatchMode();
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

        SessionState.SetBool(AutoRunSessionKey, true);

        if (!AllPrefabsExist() || AllPrefabsIntegrated()) return;

        try
        {
            IntegrateAll(forceRebuild: false);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void IntegrateAll(bool forceRebuild)
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
        if (template == null)
        {
            throw new FileNotFoundException("RVR Empty-Motorbike template was not found", TemplatePath);
        }

        int integrated = 0;
        int skipped = 0;

        try
        {
            EnsurePlayerAnimatorIkPass();

            for (int index = 1; index <= 10; index++)
            {
                string prefabPath = GetBikePath(index);
                if (!File.Exists(prefabPath))
                {
                    Debug.LogError($"[RVR Bikes] Missing prefab: {prefabPath}");
                    continue;
                }

                bool changed = IntegratePrefab(prefabPath, template, forceRebuild);
                if (changed) integrated++;
                else skipped++;
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        bool valid = ValidateAll(logSuccess: false);
        string message =
            $"[RVR Bikes] Integration complete: {integrated} updated, {skipped} already configured. " +
            $"Validation: {(valid ? "PASS" : "FAIL")}.";

        if (valid) Debug.Log(message);
        else Debug.LogError(message);
    }

    private static bool IntegratePrefab(string prefabPath, GameObject template, bool forceRebuild)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            PhysicsBikeController existingController = root.GetComponent<PhysicsBikeController>();
            if (existingController != null && !forceRebuild)
            {
                bool upgraded = ConfigureFranklinCarInteraction(root);
                upgraded |= ConfigureSilentAudioSources(root);
                upgraded |= EnsureValidRiderPose(root);
                if (!upgraded) return false;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool upgradedSaved);
                if (!upgradedSaved)
                {
                    throw new InvalidOperationException(
                        $"Unity could not save upgraded prefab: {prefabPath}"
                    );
                }

                Debug.Log($"[RVR Bikes] Upgraded shared car UI: {root.name}", root);
                return true;
            }

            if (existingController != null)
            {
                throw new InvalidOperationException(
                    $"Force rebuild is not supported for an already integrated prefab: {prefabPath}"
                );
            }

            Transform[] originalChildren = GetDirectChildren(root.transform);
            GameObject templateInstance = PrefabUtility.InstantiatePrefab(template, root.scene) as GameObject;
            if (templateInstance == null)
            {
                throw new InvalidOperationException($"Could not instantiate RVR template for {prefabPath}");
            }

            PrefabUtility.UnpackPrefabInstance(
                templateInstance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction
            );

            MergeTemplateRoot(root, templateInstance);
            ConfigureBike(root, originalChildren, prefabPath);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
            if (!saved)
            {
                throw new InvalidOperationException($"Unity could not save integrated prefab: {prefabPath}");
            }

            Debug.Log($"[RVR Bikes] Integrated {root.name}", root);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void MergeTemplateRoot(GameObject destinationRoot, GameObject templateRoot)
    {
        templateRoot.transform.SetParent(destinationRoot.transform, false);
        templateRoot.transform.localPosition = Vector3.zero;
        templateRoot.transform.localRotation = Quaternion.identity;
        templateRoot.transform.localScale = Vector3.one;

        var replacementMap = new Dictionary<UnityEngine.Object, UnityEngine.Object>
        {
            { templateRoot, destinationRoot },
            { templateRoot.transform, destinationRoot.transform }
        };

        Component[] sourceComponents = templateRoot.GetComponents<Component>();

        foreach (Component source in sourceComponents)
        {
            if (source == null || source is Transform) continue;

            if (!ComponentUtility.CopyComponent(source) ||
                !ComponentUtility.PasteComponentAsNew(destinationRoot))
            {
                throw new InvalidOperationException(
                    $"Could not copy {source.GetType().Name} from the RVR motorbike template."
                );
            }

            Component destination = destinationRoot.GetComponents(source.GetType()).Last();
            replacementMap[source] = destination;
        }

        Transform[] templateChildren = GetDirectChildren(templateRoot.transform);
        foreach (Transform child in templateChildren)
        {
            child.SetParent(destinationRoot.transform, false);
        }

        Component[] mergedComponents = destinationRoot.GetComponentsInChildren<Component>(true);
        foreach (Component component in mergedComponents)
        {
            if (component == null || component is Transform) continue;
            RemapObjectReferences(component, replacementMap);
        }

        UnityEngine.Object.DestroyImmediate(templateRoot);

        // InitialSetupMotorbike is an authoring helper. The references below are
        // configured directly, so the completed prefabs match the shipped sample.
        InitialSetupMotorbike setup = destinationRoot.GetComponent<InitialSetupMotorbike>();
        if (setup != null) UnityEngine.Object.DestroyImmediate(setup);
    }

    private static void ConfigureBike(
        GameObject root,
        IReadOnlyCollection<Transform> originalChildren,
        string prefabPath
    )
    {
        MeshFilter[] originalMeshes = originalChildren
            .SelectMany(child => child.GetComponentsInChildren<MeshFilter>(true))
            .Where(filter => filter != null && filter.sharedMesh != null)
            .ToArray();

        MeshFilter[] wheelMeshes = originalMeshes
            .Where(filter => IsWheelMesh(filter.sharedMesh.name))
            .OrderBy(filter => root.transform.InverseTransformPoint(filter.transform.position).z)
            .ToArray();

        if (wheelMeshes.Length != 2)
        {
            throw new InvalidOperationException(
                $"Expected exactly two wheel meshes in {prefabPath}, found {wheelMeshes.Length}."
            );
        }

        MeshFilter rearWheelMesh = wheelMeshes[0];
        MeshFilter frontWheelMesh = wheelMeshes[1];

        Bounds modelBounds = CalculateLocalBounds(root.transform, originalMeshes.Select(x => x.GetComponent<Renderer>()));
        Bounds frontBounds = CalculateLocalBounds(root.transform, new[] { frontWheelMesh.GetComponent<Renderer>() });
        Bounds rearBounds = CalculateLocalBounds(root.transform, new[] { rearWheelMesh.GetComponent<Renderer>() });

        float frontRadius = CalculateWheelRadius(frontBounds);
        float rearRadius = CalculateWheelRadius(rearBounds);
        float averageRadius = (frontRadius + rearRadius) * 0.5f;

        Vector3 frontPosition = root.transform.InverseTransformPoint(frontWheelMesh.transform.position);
        Vector3 rearPosition = root.transform.InverseTransformPoint(rearWheelMesh.transform.position);
        float wheelBase = Mathf.Abs(frontPosition.z - rearPosition.z);
        float axleY = (frontPosition.y + rearPosition.y) * 0.5f;
        float middleZ = (frontPosition.z + rearPosition.z) * 0.5f;

        Transform bikeBody = CreateTransform(root.transform, "BikeBody", Vector3.zero);
        foreach (Transform child in originalChildren)
        {
            child.SetParent(bikeBody, true);
        }

        Transform frontTarget = CreateTransform(bikeBody, "FrontWheelTarget", frontPosition);
        Transform rearTarget = CreateTransform(bikeBody, "RearWheelTarget", rearPosition);
        frontWheelMesh.transform.SetParent(frontTarget, true);
        rearWheelMesh.transform.SetParent(rearTarget, true);

        Transform handlebar = CreateTransform(
            bikeBody,
            "Handlebar",
            new Vector3(
                0f,
                axleY + averageRadius * 2.15f,
                frontPosition.z - Mathf.Max(averageRadius * 1.15f, wheelBase * 0.2f)
            )
        );

        float handSpacing = Mathf.Clamp(modelBounds.size.x * 0.42f, 0.18f, 0.34f);
        Transform leftHand = CreateTransform(handlebar, "LeftHand", Vector3.left * handSpacing);
        Transform rightHand = CreateTransform(handlebar, "RightHand", Vector3.right * handSpacing);

        float pegSpacing = Mathf.Clamp(modelBounds.size.x * 0.38f, 0.14f, 0.28f);
        Vector3 pegCenter = new Vector3(0f, axleY + averageRadius * 0.85f, middleZ - wheelBase * 0.04f);
        Transform leftFoot = CreateTransform(bikeBody, "LeftFoot", pegCenter + Vector3.left * pegSpacing);
        Transform rightFoot = CreateTransform(bikeBody, "RightFoot", pegCenter + Vector3.right * pegSpacing);
        Transform groundFoot = CreateTransform(
            bikeBody,
            "GroundLeftFoot",
            new Vector3(-modelBounds.extents.x - 0.12f, axleY - averageRadius * 0.45f, middleZ)
        );

        Transform seat = FindDirectChild(root.transform, "Parent");
        if (seat == null)
        {
            throw new InvalidOperationException($"RVR seat target is missing in {prefabPath}");
        }

        seat.SetParent(bikeBody, false);
        seat.localPosition = new Vector3(0f, axleY + averageRadius * 2.2f, middleZ - wheelBase * 0.11f);
        seat.localRotation = Quaternion.identity;

        Transform centerOfMass = FindDirectChild(root.transform, "CenterOfGravity");
        if (centerOfMass == null)
        {
            throw new InvalidOperationException($"RVR center-of-mass target is missing in {prefabPath}");
        }

        centerOfMass.localPosition = new Vector3(0f, axleY + averageRadius * 0.2f, middleZ);

        WheelCollider frontCollider = CreateWheelCollider(root.transform, "FrontWheelCollider", frontPosition, frontRadius);
        WheelCollider rearCollider = CreateWheelCollider(root.transform, "RearWheelCollider", rearPosition, rearRadius);

        ConfigureBodyCollider(root.GetComponent<BoxCollider>(), modelBounds, axleY, averageRadius, wheelBase, middleZ);

        PhysicsBikeController controller = root.GetComponent<PhysicsBikeController>();
        BikeEntry bikeEntry = root.GetComponent<BikeEntry>();
        if (controller == null || bikeEntry == null)
        {
            throw new InvalidOperationException($"RVR controller components are missing in {prefabPath}");
        }

        controller.bikeBody = bikeBody;
        controller.frontWheelTransform = frontTarget;
        controller.rearWheelTransform = rearTarget;
        controller.frontWheelCollider = frontCollider;
        controller.rearWheelCollider = rearCollider;
        controller.centerOfMass = centerOfMass;
        controller.steeringWheelMesh = handlebar;
        controller.steeringWheelRotationAxis = SteeringWheelRotationAxis.Y;
        controller.inputMode = PhysicsBikeController.InputMode.Both;

#if ENABLE_INPUT_SYSTEM
        string inputActionsPath = AssetDatabase.GUIDToAssetPath("4bd5ea3322443944a9e42931b1778f89");
        controller.inputActionAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(inputActionsPath);
        controller.moveActionName = "Move";
#endif

        bikeEntry.entryParent = seat;
        bikeEntry.steeringWheelLeftHandTarget = leftHand;
        bikeEntry.steeringWheelRightHandTarget = rightHand;
        bikeEntry.leftFootTarget = leftFoot;
        bikeEntry.rightFootTarget = rightFoot;
        bikeEntry.groundLeftFootTarget = groundFoot;

        ConfigureLights(root, bikeBody, frontPosition, axleY, averageRadius, modelBounds);
        ConfigureEntryTrigger(root, modelBounds, axleY, averageRadius, middleZ);
        ConfigureVfx(root, modelBounds, axleY, averageRadius, rearPosition);
        ConfigureFranklinCarInteraction(root);
        ConfigureSilentAudioSources(root);
        ConfigureDefaultRiderPose(root, prefabPath);

        root.name = Path.GetFileNameWithoutExtension(prefabPath);
        EditorUtility.SetDirty(root);
    }

    private static void ConfigureBodyCollider(
        BoxCollider collider,
        Bounds modelBounds,
        float axleY,
        float wheelRadius,
        float wheelBase,
        float middleZ
    )
    {
        if (collider == null) return;

        float bottom = axleY - wheelRadius * 0.12f;
        float top = Mathf.Max(modelBounds.max.y, bottom + 0.65f);
        float height = top - bottom;

        collider.center = new Vector3(modelBounds.center.x, (bottom + top) * 0.5f, middleZ);
        collider.size = new Vector3(
            Mathf.Clamp(modelBounds.size.x * 0.82f, 0.42f, 0.85f),
            height,
            Mathf.Max(0.9f, wheelBase * 1.05f)
        );
    }

    private static WheelCollider CreateWheelCollider(
        Transform root,
        string name,
        Vector3 localPosition,
        float radius
    )
    {
        Transform target = CreateTransform(root, name, localPosition);
        WheelCollider collider = target.gameObject.AddComponent<WheelCollider>();
        collider.radius = radius;
        collider.suspensionDistance = 0.15f;

        JointSpring spring = collider.suspensionSpring;
        spring.spring = 15000f;
        spring.damper = 600f;
        collider.suspensionSpring = spring;
        return collider;
    }

    private static void ConfigureLights(
        GameObject root,
        Transform bikeBody,
        Vector3 frontPosition,
        float axleY,
        float wheelRadius,
        Bounds modelBounds
    )
    {
        Light[] lights = root.GetComponentsInChildren<Light>(true)
            .OrderBy(light => light.transform.localPosition.x)
            .ToArray();

        float lightY = Mathf.Lerp(axleY + wheelRadius, modelBounds.max.y, 0.55f);
        float lightZ = frontPosition.z + wheelRadius * 0.45f;
        float spacing = Mathf.Clamp(modelBounds.size.x * 0.22f, 0.08f, 0.18f);

        for (int index = 0; index < lights.Length; index++)
        {
            Transform lightTransform = lights[index].transform;
            lightTransform.SetParent(bikeBody, false);
            float side = index == 0 ? -1f : 1f;
            lightTransform.localPosition = new Vector3(side * spacing, lightY, lightZ);
        }
    }

    private static void ConfigureEntryTrigger(
        GameObject root,
        Bounds modelBounds,
        float axleY,
        float wheelRadius,
        float middleZ
    )
    {
        Transform trigger = FindDirectChild(root.transform, "Triggers_Enter/Exit");
        if (trigger == null) return;

        trigger.localPosition = new Vector3(
            -modelBounds.extents.x - 0.35f,
            axleY + wheelRadius,
            middleZ
        );
    }

    private static bool ConfigureFranklinCarInteraction(GameObject root)
    {
        bool changed = false;

        Transform bikeHud = FindDirectChild(root.transform, "HUD_Bike");
        if (bikeHud != null && bikeHud.gameObject.activeSelf)
        {
            bikeHud.gameObject.SetActive(false);
            changed = true;
        }

        Transform trigger = FindDirectChild(root.transform, "Triggers_Enter/Exit");
        Component hotspot = trigger != null ? trigger.GetComponent("Hotspot") : null;
        if (hotspot != null)
        {
            var serializedHotspot = new SerializedObject(hotspot);
            SerializedProperty property = serializedHotspot.GetIterator();
            bool hotspotChanged = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.Boolean ||
                    property.name != "m_IsEnabled" ||
                    !property.propertyPath.StartsWith("m_Spots.m_Spots", StringComparison.Ordinal) ||
                    !property.boolValue) continue;

                property.boolValue = false;
                hotspotChanged = true;
            }

            if (hotspotChanged)
            {
                serializedHotspot.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
        }

        if (changed) EditorUtility.SetDirty(root);
        return changed;
    }

    private static bool UsesFranklinCarInteraction(GameObject root)
    {
        Transform bikeHud = FindDirectChild(root.transform, "HUD_Bike");
        if (bikeHud != null && bikeHud.gameObject.activeSelf) return false;

        Transform trigger = FindDirectChild(root.transform, "Triggers_Enter/Exit");
        Component hotspot = trigger != null ? trigger.GetComponent("Hotspot") : null;
        if (hotspot == null) return false;

        var serializedHotspot = new SerializedObject(hotspot);
        SerializedProperty property = serializedHotspot.GetIterator();
        bool foundSpot = false;
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.Boolean ||
                property.name != "m_IsEnabled" ||
                !property.propertyPath.StartsWith("m_Spots.m_Spots", StringComparison.Ordinal))
            {
                continue;
            }

            foundSpot = true;
            if (property.boolValue) return false;
        }

        return foundSpot;
    }

    private static bool ConfigureSilentAudioSources(GameObject root)
    {
        bool changed = false;
        foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
        {
            if (source == null || !source.playOnAwake) continue;
            source.playOnAwake = false;
            EditorUtility.SetDirty(source);
            changed = true;
        }

        if (changed) EditorUtility.SetDirty(root);
        return changed;
    }

    private static bool HasSilentAudioDefaults(GameObject root)
    {
        return root.GetComponentsInChildren<AudioSource>(true)
            .All(source => source == null || !source.playOnAwake);
    }

    private static void ConfigureDefaultRiderPose(GameObject root, string prefabPath)
    {
        BikeEntry entry = root.GetComponent<BikeEntry>();
        if (entry == null) return;

        string bikeName = Path.GetFileNameWithoutExtension(prefabPath);
        RiderFitStyle style = bikeName.IndexOf(
            "Sport",
            StringComparison.OrdinalIgnoreCase
        ) >= 0
            ? RiderFitStyle.Sport
            : RiderFitStyle.Standard;

        RvrBikeRiderFitClipTool.ApplyPreset(entry, style);
        RvrBikeRiderFitClipTool.GeneratePoseClip(entry, bikeName);
        EditorUtility.SetDirty(entry);
        EditorUtility.SetDirty(root);
    }

    private static bool EnsureValidRiderPose(GameObject root)
    {
        BikeEntry entry = root.GetComponent<BikeEntry>();
        if (entry == null) return false;

        if (entry.riderPoseClip != null && entry.riderPoseSourceClip != null) return false;

        string bikeName = root.name;
        if (entry.riderPoseClip == null)
        {
            RiderFitStyle style = bikeName.IndexOf(
                "Sport",
                StringComparison.OrdinalIgnoreCase
            ) >= 0
                ? RiderFitStyle.Sport
                : RiderFitStyle.Standard;
            RvrBikeRiderFitClipTool.ApplyPreset(entry, style);
        }

        RvrBikeRiderFitClipTool.GeneratePoseClip(entry, bikeName);
        EditorUtility.SetDirty(entry);
        EditorUtility.SetDirty(root);
        return true;
    }

    private static bool HasValidRiderPose(BikeEntry entry)
    {
        return entry != null &&
               entry.riderPoseSourceClip != null &&
               entry.riderPoseClip != null;
    }

    private static bool EnsurePlayerAnimatorIkPass()
    {
        AnimatorControllerAsset controller =
            AssetDatabase.LoadAssetAtPath<AnimatorControllerAsset>(PlayerAnimatorControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[RVR Bikes] Player Animator Controller missing: {PlayerAnimatorControllerPath}");
            return false;
        }

        AnimatorControllerLayerAsset[] layers = controller.layers;
        bool changed = false;
        for (int index = 0; index < layers.Length; index++)
        {
            if (layers[index].iKPass) continue;
            layers[index].iKPass = true;
            changed = true;
        }

        if (!changed) return false;

        controller.layers = layers;
        EditorUtility.SetDirty(controller);
        Debug.Log("[RVR Bikes] Enabled IK Pass on the player Animator Controller.", controller);
        return true;
    }

    private static bool HasPlayerAnimatorIkPass()
    {
        AnimatorControllerAsset controller =
            AssetDatabase.LoadAssetAtPath<AnimatorControllerAsset>(PlayerAnimatorControllerPath);
        return controller != null &&
               controller.layers.Length > 0 &&
               controller.layers.All(layer => layer.iKPass);
    }

    private static void ConfigureVfx(
        GameObject root,
        Bounds modelBounds,
        float axleY,
        float wheelRadius,
        Vector3 rearPosition
    )
    {
        VehicleVFX vfx = root.GetComponent<VehicleVFX>();
        if (vfx == null) return;

        vfx.damageVFXOffset = modelBounds.center;
        vfx.exhaustVFXOffset = new Vector3(
            modelBounds.center.x,
            axleY + wheelRadius * 0.6f,
            rearPosition.z - wheelRadius * 0.75f
        );
    }

    private static bool ValidateAll(bool logSuccess)
    {
        bool allValid = HasPlayerAnimatorIkPass();
        if (!allValid)
        {
            Debug.LogError("[RVR Bikes] Validation: player Animator Controller IK Pass is disabled.");
        }

        for (int index = 1; index <= 10; index++)
        {
            string prefabPath = GetBikePath(index);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RVR Bikes] Validation: missing {prefabPath}");
                allValid = false;
                continue;
            }

            PhysicsBikeController controller = prefab.GetComponent<PhysicsBikeController>();
            BikeEntry entry = prefab.GetComponent<BikeEntry>();
            bool valid =
                controller != null &&
                controller.bikeBody != null &&
                controller.frontWheelTransform != null &&
                controller.rearWheelTransform != null &&
                controller.frontWheelCollider != null &&
                controller.rearWheelCollider != null &&
                controller.centerOfMass != null &&
                prefab.GetComponent<Rigidbody>() != null &&
                prefab.GetComponent<BoxCollider>() != null &&
                entry != null &&
                entry.entryParent != null &&
                entry.steeringWheelLeftHandTarget != null &&
                entry.steeringWheelRightHandTarget != null &&
                entry.leftFootTarget != null &&
                entry.rightFootTarget != null &&
                entry.groundLeftFootTarget != null &&
                controller is IRvrVehicleInputController &&
                UsesFranklinCarInteraction(prefab) &&
                HasSilentAudioDefaults(prefab) &&
                HasValidRiderPose(entry) &&
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0;

            if (!valid)
            {
                Debug.LogError($"[RVR Bikes] Validation failed: {prefabPath}", prefab);
                allValid = false;
            }
            else if (logSuccess)
            {
                Debug.Log($"[RVR Bikes] Validation passed: {prefab.name}", prefab);
            }
        }

        return allValid;
    }

    private static void RemapObjectReferences(
        Component component,
        IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> replacementMap
    )
    {
        var serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        bool changed = false;

        while (property.Next(enterChildren))
        {
            enterChildren = true;
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (property.propertyPath == "m_GameObject" || property.propertyPath == "m_Script") continue;

            UnityEngine.Object current = property.objectReferenceValue;
            if (current == null || !replacementMap.TryGetValue(current, out UnityEngine.Object replacement)) continue;

            property.objectReferenceValue = replacement;
            changed = true;
        }

        if (changed) serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Bounds CalculateLocalBounds(Transform root, IEnumerable<Renderer> renderers)
    {
        bool initialized = false;
        Bounds result = default;

        foreach (Renderer renderer in renderers.Where(renderer => renderer != null))
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                Vector3 localCorner = root.InverseTransformPoint(worldCorner);

                if (!initialized)
                {
                    result = new Bounds(localCorner, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(localCorner);
                }
            }
        }

        if (!initialized)
        {
            throw new InvalidOperationException($"No render bounds were found below {root.name}");
        }

        return result;
    }

    private static float CalculateWheelRadius(Bounds bounds)
    {
        return Mathf.Clamp(Mathf.Max(bounds.size.y, bounds.size.z) * 0.5f, 0.2f, 0.55f);
    }

    private static bool IsWheelMesh(string meshName)
    {
        return meshName.IndexOf("_Wheel_", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Transform CreateTransform(Transform parent, string name, Vector3 localPosition)
    {
        var gameObject = new GameObject(name);
        Transform transform = gameObject.transform;
        transform.SetParent(parent, false);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        return transform;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        return GetDirectChildren(parent).FirstOrDefault(child => child.name == name);
    }

    private static Transform[] GetDirectChildren(Transform parent)
    {
        var children = new Transform[parent.childCount];
        for (int index = 0; index < parent.childCount; index++)
        {
            children[index] = parent.GetChild(index);
        }

        return children;
    }

    private static bool AllPrefabsExist()
    {
        return Enumerable.Range(1, 10).All(index => File.Exists(GetBikePath(index)));
    }

    private static bool AllPrefabsIntegrated()
    {
        return HasPlayerAnimatorIkPass() && Enumerable.Range(1, 10).All(index =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetBikePath(index));
            return prefab != null &&
                   prefab.GetComponent<PhysicsBikeController>() != null &&
                   UsesFranklinCarInteraction(prefab) &&
                   HasSilentAudioDefaults(prefab) &&
                   HasValidRiderPose(prefab.GetComponent<BikeEntry>());
        });
    }

    private static string GetBikePath(int index)
    {
        string originalPath = $"{BikesFolder}/Bike_{index:00}.prefab";
        if (File.Exists(originalPath)) return originalPath;

        string sportPath = $"{BikesFolder}/Bike_{index:00}_Sport.prefab";
        return File.Exists(sportPath) ? sportPath : originalPath;
    }
}
#endif
