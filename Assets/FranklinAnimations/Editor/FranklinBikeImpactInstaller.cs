#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using FranklinGame.Vehicles;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class FranklinBikeImpactInstaller
{
    private const string BikesFolder =
        "Assets/Model/DQP_MotorBikePack_URP14/Generated/Prefabs/Bikes";
    private const string LightImpactAudioPath =
        "Assets/FranklinAnimations/Audio/Vehicles/car_impact_light.wav";
    private const string HeavyImpactAudioPath =
        "Assets/FranklinAnimations/Audio/Vehicles/car_impact_heavy.wav";
    private const string CollisionEffectPath =
        "Assets/Ash Assets/Sim-Cade Vehicle Physics/Prefabs/Collision Spark.prefab";
    private const string ZeroFrictionMaterialPath =
        "Assets/Ash Assets/Arcade Bike Physics Pro/Materials/zero Friction.physicMaterial";
    private const string RagdollBodyMaterialPath =
        "Assets/FranklinAnimations/Generated/BikeRagdoll/Franklin_Bike_Ragdoll_Body.physicMaterial";
    private const string RecoveryAnimationPath =
        "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/3D/Animations/Locomotion/Human@Crouch_Idle.anim";

    static FranklinBikeImpactInstaller()
    {
        EditorApplication.delayCall += InstallIfNeeded;
    }

    [MenuItem("Tools/Franklin/Arcade Bikes/Configure Impact + Crash Ragdoll")]
    public static void Install()
    {
        AudioClip lightClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            LightImpactAudioPath
        );
        AudioClip heavyClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            HeavyImpactAudioPath
        );
        GameObject impactEffect = AssetDatabase.LoadAssetAtPath<GameObject>(
            CollisionEffectPath
        );
        if (lightClip == null || heavyClip == null || impactEffect == null)
        {
            throw new InvalidOperationException(
                "Bike impact setup requires both impact clips and Collision Spark.prefab."
            );
        }

        int configured = 0;
        foreach (string prefabPath in GetBikePrefabPaths())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ConfigurePrefab(root, lightClip, heavyClip, impactEffect);
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
            $"[Arcade Bikes] Configured light/heavy collision audio and pooled " +
            $"spark/flash/debris FX plus GC2 rider and in-place ABP bike ragdoll on " +
            $"{configured}/10 bikes."
        );
    }

    [MenuItem("Tools/Franklin/Arcade Bikes/Normalize All Bike Feature Hierarchies")]
    public static void NormalizeAllBikeFeatureHierarchies()
    {
        string[] prefabPaths = GetBikePrefabPaths();
        int organized = 0;
        foreach (string prefabPath in prefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (!NormalizeFeatureHierarchy(root)) continue;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                organized++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[Arcade Bikes] Normalized shared feature hierarchy and removed obsolete " +
            $"helpers on {organized}/{prefabPaths.Length} bikes."
        );
    }

    internal static void ConfigurePrefab(GameObject root)
    {
        AudioClip lightClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            LightImpactAudioPath
        );
        AudioClip heavyClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            HeavyImpactAudioPath
        );
        GameObject impactEffect = AssetDatabase.LoadAssetAtPath<GameObject>(
            CollisionEffectPath
        );
        if (lightClip == null || heavyClip == null || impactEffect == null)
        {
            throw new InvalidOperationException("Bike collision audio/FX assets are missing.");
        }

        ConfigurePrefab(root, lightClip, heavyClip, impactEffect);
    }

    private static void ConfigurePrefab(
        GameObject root,
        AudioClip lightClip,
        AudioClip heavyClip,
        GameObject impactEffect)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));

        AudioSource source = EnsureImpactAudioSource(root);
        FranklinBikeImpactAudio impact = root.GetComponent<FranklinBikeImpactAudio>();
        if (impact == null) impact = root.AddComponent<FranklinBikeImpactAudio>();
        impact.ConfigureForBike(source, lightClip, heavyClip, impactEffect);

        FranklinArcadeBikeDriver driver =
            root.GetComponent<FranklinArcadeBikeDriver>();
        ArcadeBP_Pro.ArcadeBikeControllerPro controller =
            root.GetComponent<ArcadeBP_Pro.ArcadeBikeControllerPro>();
        BikeEntry entry = root.GetComponent<BikeEntry>();
        Rigidbody body = root.GetComponent<Rigidbody>();
        PhysicsMaterial zeroFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(
            ZeroFrictionMaterialPath
        );
        PhysicsMaterial bodyFriction = EnsureRagdollBodyMaterial();
        if (driver == null || controller == null || entry == null || body == null)
        {
            throw new InvalidOperationException(
                $"{root.name} must be integrated before crash ragdoll is configured."
            );
        }

        Transform renderedBody = controller.bikeReferences?.BodyMesh;
        SphereCollider frontWheelCollider = EnsureRagdollWheelCollider(
            root,
            renderedBody,
            controller.bikeReferences?.FrontWheel,
            "Franklin Ragdoll Front Wheel Collider",
            controller.bikeGeometry?.FrontWheelRadius ?? 0.3f,
            zeroFriction
        );
        SphereCollider rearWheelCollider = EnsureRagdollWheelCollider(
            root,
            renderedBody,
            controller.bikeReferences?.RearWheel,
            "Franklin Ragdoll Rear Wheel Collider",
            controller.bikeGeometry?.RearWheelRadius ?? 0.3f,
            zeroFriction
        );
        MeshCollider[] bodyMeshColliders = EnsureRenderedBodyMeshColliders(
            controller.bikeReferences?.BodyMesh,
            bodyFriction
        );
        if (controller.bikeReferences?.collider != null)
            controller.bikeReferences.collider.enabled = false;
        FranklinArcadeBikeRagdoll bikeRagdoll =
            root.GetComponent<FranklinArcadeBikeRagdoll>();
        if (bikeRagdoll == null)
            bikeRagdoll = root.AddComponent<FranklinArcadeBikeRagdoll>();
        Bounds renderedBodyBounds = CalculateLocalRendererBounds(
            root,
            controller.bikeReferences?.BodyMesh
        );
        ConfigureFallenBikeRecovery(
            entry,
            root,
            renderedBody,
            renderedBodyBounds
        );
        NormalizeFeatureHierarchy(root);
        bikeRagdoll.Configure(
            controller,
            driver,
            body,
            controller.bikeReferences?.collider,
            bodyMeshColliders,
            zeroFriction,
            bodyFriction,
            frontWheelCollider,
            rearWheelCollider,
            new Vector3(
                renderedBodyBounds.min.x,
                renderedBodyBounds.center.y,
                renderedBodyBounds.center.z
            ),
            new Vector3(
                renderedBodyBounds.max.x,
                renderedBodyBounds.center.y,
                renderedBodyBounds.center.z
            ),
            Mathf.Clamp(renderedBodyBounds.extents.z * 0.35f, 0.2f, 0.75f)
        );

        FranklinBikeCrashRagdoll crash =
            root.GetComponent<FranklinBikeCrashRagdoll>();
        if (crash == null) crash = root.AddComponent<FranklinBikeCrashRagdoll>();
        crash.Configure(impact, driver, bikeRagdoll, entry, body);
        crash.UpgradeConfigurationIfNeeded();

        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(impact);
        EditorUtility.SetDirty(frontWheelCollider);
        EditorUtility.SetDirty(rearWheelCollider);
        foreach (MeshCollider meshCollider in bodyMeshColliders)
            EditorUtility.SetDirty(meshCollider);
        EditorUtility.SetDirty(entry);
        EditorUtility.SetDirty(bikeRagdoll);
        EditorUtility.SetDirty(crash);
        EditorUtility.SetDirty(root);
    }

    private static void InstallIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string[] paths = GetBikePrefabPaths();
        if (paths.Length == 0) return;
        bool complete = paths.All(path =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            FranklinBikeImpactAudio impact =
                prefab != null ? prefab.GetComponent<FranklinBikeImpactAudio>() : null;
            FranklinBikeCrashRagdoll crash =
                prefab != null ? prefab.GetComponent<FranklinBikeCrashRagdoll>() : null;
            FranklinArcadeBikeRagdoll bikeRagdoll =
                prefab != null ? prefab.GetComponent<FranklinArcadeBikeRagdoll>() : null;
            return impact != null && impact.IsConfigured &&
                   bikeRagdoll != null && bikeRagdoll.IsConfigured &&
                   crash != null && crash.IsConfigured &&
                   crash.HasCurrentConfiguration &&
                   HasNormalizedFeatureHierarchy(prefab);
        });
        if (complete) return;

        try
        {
            Install();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static AudioSource EnsureImpactAudioSource(GameObject root)
    {
        Transform audioTransform = FindTransform(root, "AudioSource-Collision");
        if (audioTransform == null)
        {
            GameObject audioObject = new GameObject("AudioSource-Collision");
            audioTransform = audioObject.transform;
            audioTransform.SetParent(root.transform, false);
        }

        AudioSource source = audioTransform.GetComponent<AudioSource>();
        if (source == null) source = audioTransform.gameObject.AddComponent<AudioSource>();
        source.clip = null;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.ignoreListenerPause = true;
        source.priority = 96;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 4f;
        source.maxDistance = 55f;
        return source;
    }

    private static SphereCollider EnsureRagdollWheelCollider(
        GameObject root,
        Transform renderedBody,
        Transform wheel,
        string objectName,
        float radius,
        PhysicsMaterial zeroFriction)
    {
        if (root == null || renderedBody == null || wheel == null)
        {
            throw new InvalidOperationException(
                $"{root?.name ?? "Bike"} is missing a wheel target for bike ragdoll."
            );
        }

        Transform anchor = FindTransform(root, objectName);
        if (anchor == null)
        {
            GameObject colliderObject = new GameObject(objectName);
            anchor = colliderObject.transform;
        }

        anchor.SetParent(renderedBody, true);
        anchor.SetPositionAndRotation(wheel.position, root.transform.rotation);
        anchor.localScale = Vector3.one;
        SphereCollider collider = anchor.GetComponent<SphereCollider>();
        if (collider == null) collider = anchor.gameObject.AddComponent<SphereCollider>();
        collider.center = Vector3.zero;
        // The visual wheel radius can already touch or slightly penetrate the
        // road under ABP suspension. A smaller crash-only radius avoids creating
        // an overlapping sphere at the driving-to-ragdoll handoff.
        collider.radius = Mathf.Max(0.08f, radius * 0.86f);
        collider.sharedMaterial = zeroFriction;
        collider.contactOffset = 0.005f;
        collider.isTrigger = false;
        collider.enabled = false;
        return collider;
    }

    private static PhysicsMaterial EnsureRagdollBodyMaterial()
    {
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(
            RagdollBodyMaterialPath
        );
        if (material == null)
        {
            string folder = Path.GetDirectoryName(RagdollBodyMaterialPath)
                ?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                string name = Path.GetFileName(folder);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                    AssetDatabase.CreateFolder(parent, name);
            }

            material = new PhysicsMaterial("Franklin Bike Ragdoll Body");
            AssetDatabase.CreateAsset(material, RagdollBodyMaterialPath);
        }

        material.dynamicFriction = 0.48f;
        material.staticFriction = 0.62f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicsMaterialCombine.Maximum;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static MeshCollider[] EnsureRenderedBodyMeshColliders(
        Transform renderedBody,
        PhysicsMaterial bodyMaterial)
    {
        if (renderedBody == null)
            throw new InvalidOperationException("Bike BodyMesh is missing.");

        return renderedBody.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter != null && filter.sharedMesh != null &&
                             filter.sharedMesh.vertexCount >= 4 &&
                             filter.GetComponent<MeshRenderer>() != null)
            .Select(filter =>
            {
                MeshCollider oldCollider = filter.GetComponent<MeshCollider>();
                if (oldCollider != null)
                    UnityEngine.Object.DestroyImmediate(oldCollider);

                Transform proxy = filter.transform.Find(
                    "Franklin Body Mesh Collider"
                );
                if (proxy == null)
                {
                    GameObject proxyObject = new GameObject(
                        "Franklin Body Mesh Collider"
                    );
                    proxy = proxyObject.transform;
                    proxy.SetParent(filter.transform, false);
                }
                // Keep the mesh collider physically active, but place only its
                // proxy on Ignore Raycast. ABP excludes this layer from its
                // drivable mask, preventing wheel suspension from raycasting the
                // bike's own fairing/tank and launching the bike after mounting.
                proxy.gameObject.layer = Physics.IgnoreRaycastLayer;
                proxy.localPosition = Vector3.zero;
                proxy.localRotation = Quaternion.identity;
                proxy.localScale = Vector3.one * 0.86f;

                MeshCollider collider = proxy.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = proxy.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = true;
                collider.isTrigger = false;
                collider.sharedMaterial = bodyMaterial;
                collider.contactOffset = 0.01f;
                collider.enabled = true;
                return collider;
            })
            .ToArray();
    }

    private static void ConfigureFallenBikeRecovery(
        BikeEntry entry,
        GameObject root,
        Transform renderedBody,
        Bounds bodyBounds)
    {
        if (entry == null || root == null || renderedBody == null) return;

        Transform leftGrip = GetOrCreateChild(
            renderedBody,
            "Fallen Bike Body Grip Left"
        );
        Transform rightGrip = GetOrCreateChild(
            renderedBody,
            "Fallen Bike Body Grip Right"
        );

        Vector3 grip = new Vector3(
            Mathf.Max(0.12f, bodyBounds.extents.x * 0.72f),
            bodyBounds.center.y + bodyBounds.extents.y * 0.08f,
            bodyBounds.center.z - bodyBounds.extents.z * 0.12f
        );
        leftGrip.SetPositionAndRotation(
            root.transform.TransformPoint(new Vector3(-grip.x, grip.y, grip.z)),
            root.transform.rotation * Quaternion.Euler(0f, 0f, 90f)
        );
        rightGrip.SetPositionAndRotation(
            root.transform.TransformPoint(new Vector3(grip.x, grip.y, grip.z)),
            root.transform.rotation * Quaternion.Euler(0f, 0f, -90f)
        );
        leftGrip.localScale = Vector3.one;
        rightGrip.localScale = Vector3.one;

        entry.fallenBikeRecoveryAnimation =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(RecoveryAnimationPath);
        entry.fallenBikeBodyGripLeft = leftGrip;
        entry.fallenBikeBodyGripRight = rightGrip;
        RemoveUnusedDuplicateMarker(root, leftGrip);
        RemoveUnusedDuplicateMarker(root, rightGrip);
        EditorUtility.SetDirty(leftGrip);
        EditorUtility.SetDirty(rightGrip);
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static bool NormalizeFeatureHierarchy(GameObject root)
    {
        if (root == null) return false;
        ArcadeBP_Pro.ArcadeBikeControllerPro controller =
            root.GetComponent<ArcadeBP_Pro.ArcadeBikeControllerPro>();
        BikeEntry entry = root.GetComponent<BikeEntry>();
        Transform renderedBody = controller?.bikeReferences?.BodyMesh;
        if (entry == null || renderedBody == null) return false;

        MoveDirectRootChild(
            root.transform,
            renderedBody,
            "Franklin Ragdoll Front Wheel Collider"
        );
        MoveDirectRootChild(
            root.transform,
            renderedBody,
            "Franklin Ragdoll Rear Wheel Collider"
        );
        MoveReferencedMarker(renderedBody, entry.fallenBikeBodyGripLeft);
        MoveReferencedMarker(renderedBody, entry.fallenBikeBodyGripRight);
        MoveReferencedMarker(renderedBody, entry.entryStandingPoint);
        MoveReferencedMarker(renderedBody, entry.mirroredEntryStandingPoint);
        RemoveUnusedDuplicateMarker(root, entry.fallenBikeBodyGripLeft);
        RemoveUnusedDuplicateMarker(root, entry.fallenBikeBodyGripRight);
        RemoveObsoleteDirectRootChild(root.transform, "AudioSource-Coillision");
        VehicleDeformation obsoleteDeformation = root.GetComponent<VehicleDeformation>();
        if (obsoleteDeformation != null)
            UnityEngine.Object.DestroyImmediate(obsoleteDeformation);
        EditorUtility.SetDirty(root);
        return true;
    }

    private static bool HasNormalizedFeatureHierarchy(GameObject root)
    {
        if (root == null || root.GetComponent<VehicleDeformation>() != null ||
            root.transform.Find("AudioSource-Coillision") != null) return false;

        ArcadeBP_Pro.ArcadeBikeControllerPro controller =
            root.GetComponent<ArcadeBP_Pro.ArcadeBikeControllerPro>();
        BikeEntry entry = root.GetComponent<BikeEntry>();
        Transform body = controller?.bikeReferences?.BodyMesh;
        if (entry == null || body == null) return false;

        Transform[] required =
        {
            entry.entryStandingPoint,
            entry.mirroredEntryStandingPoint,
            entry.fallenBikeBodyGripLeft,
            entry.fallenBikeBodyGripRight
        };
        if (required.Any(item => item == null || item.parent != body)) return false;
        Transform[] hierarchy = root.GetComponentsInChildren<Transform>(true);
        Transform front = hierarchy.FirstOrDefault(
            item => item.name == "Franklin Ragdoll Front Wheel Collider"
        );
        Transform rear = hierarchy.FirstOrDefault(
            item => item.name == "Franklin Ragdoll Rear Wheel Collider"
        );
        if (front == null || rear == null || front.parent != body || rear.parent != body ||
            front.GetComponent<SphereCollider>() == null ||
            rear.GetComponent<SphereCollider>() == null) return false;

        return hierarchy.Count(item => item.name == "Fallen Bike Body Grip Left") == 1 &&
               hierarchy.Count(item => item.name == "Fallen Bike Body Grip Right") == 1 &&
               hierarchy.Count(item => item.name == "Franklin Ragdoll Front Wheel Collider") == 1 &&
               hierarchy.Count(item => item.name == "Franklin Ragdoll Rear Wheel Collider") == 1;
    }

    private static void MoveDirectRootChild(
        Transform root,
        Transform destination,
        string objectName
    )
    {
        Transform child = root != null ? root.Find(objectName) : null;
        if (child == null || destination == null) return;
        child.SetParent(destination, true);
        EditorUtility.SetDirty(child);
    }

    private static void MoveReferencedMarker(Transform destination, Transform marker)
    {
        if (destination == null || marker == null || marker.parent == destination) return;
        marker.SetParent(destination, true);
        EditorUtility.SetDirty(marker);
    }

    private static void RemoveUnusedDuplicateMarker(GameObject root, Transform retained)
    {
        if (root == null || retained == null) return;
        Transform[] candidates = root.GetComponentsInChildren<Transform>(true)
            .Where(candidate => candidate != null && candidate != retained &&
                                candidate.name == retained.name)
            .ToArray();
        foreach (Transform candidate in candidates)
        {
            Component[] components = candidate.GetComponents<Component>();
            if (components.All(component => component is Transform))
                UnityEngine.Object.DestroyImmediate(candidate.gameObject);
        }
    }

    private static void RemoveObsoleteDirectRootChild(Transform root, string objectName)
    {
        Transform obsolete = root != null ? root.Find(objectName) : null;
        if (obsolete == null) return;
        UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
    }

    private static Bounds CalculateLocalRendererBounds(GameObject root)
    {
        return CalculateLocalRendererBounds(root, root != null ? root.transform : null);
    }

    private static Bounds CalculateLocalRendererBounds(
        GameObject root,
        Transform rendererRoot)
    {
        bool initialized = false;
        Vector3 minimum = Vector3.zero;
        Vector3 maximum = Vector3.zero;
        if (root == null || rendererRoot == null)
            return new Bounds(Vector3.zero, new Vector3(0.8f, 1f, 2f));

        foreach (Renderer renderer in rendererRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)
                continue;

            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = root.transform.InverseTransformPoint(
                    center + Vector3.Scale(extents, new Vector3(x, y, z))
                );
                if (!initialized)
                {
                    minimum = maximum = corner;
                    initialized = true;
                }
                else
                {
                    minimum = Vector3.Min(minimum, corner);
                    maximum = Vector3.Max(maximum, corner);
                }
            }
        }

        if (!initialized)
            return new Bounds(Vector3.zero, new Vector3(0.8f, 1f, 2f));

        Bounds result = new Bounds();
        result.SetMinMax(minimum, maximum);
        return result;
    }

    private static string[] GetBikePrefabPaths()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { BikesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => string.Equals(
                Path.GetDirectoryName(path)?.Replace('\\', '/'),
                BikesFolder,
                StringComparison.Ordinal
            ))
            .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(
                "Bike_",
                StringComparison.OrdinalIgnoreCase
            ))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static Transform FindTransform(GameObject root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.name == name);
    }
}
#endif
