#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
internal static class FranklinBikeImpactInstaller
{
    private const string BikesFolder =
        "Assets/Ash Assets/Arcade Bike Physics Pro/Prefabs/Bikes";
    private const string LightImpactAudioPath =
        "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Audio/SFX/car_impact_light.wav";
    private const string HeavyImpactAudioPath =
        "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Audio/SFX/car_impact_heavy.wav";
    private const string CollisionEffectPath =
        "Assets/Ash Assets/Vehicle Integration/ThirdParty/Sim-Cade Vehicle Physics/Prefabs/Collision Spark.prefab";
    private const string MetalDebrisMaterialPath =
        "Assets/Ash Assets/Arcade Bike Physics Pro/Materials/Effects/MetalDebris.mat";
    private const string ZeroFrictionMaterialPath =
        "Assets/Ash Assets/Arcade Bike Physics Pro/Materials/Physics/ZeroFriction.physicMaterial";
    private const string RagdollBodyMaterialPath =
        "Assets/Ash Assets/Arcade Bike Physics Pro/Materials/Physics/BikeRagdollBody.physicMaterial";
    private const string RecoveryAnimationPath =
        "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/3D/Animations/Locomotion/Human@Crouch_Idle.anim";
    private const string PassengerPosePath =
        "Assets/Plugins/GameCreator/Installs/GameCreator.Examples@1.11.28/1 - Characters/3_States/State@Sit.anim";
    private const string ExplosionAudioPath =
        "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Audio/SFX/car_explosion_test_vehicle_cc0.wav";
    private const string SmokeLoopAudioPath =
        "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Audio/SFX/car_smoke_hiss_loop_cc0.wav";
    private const string FireLoopAudioPath =
        "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Audio/SFX/car_fire_crackle_loop_cc0.wav";
    private const string DamageVfxRoot =
        "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/VFX/ThirdParty/Hovl Studio/3D Fire and Explosions/Prefabs";
    private const string SmokePrefabPath = DamageVfxRoot + "/Smoke1.prefab";
    private const string FirePrefabPath = DamageVfxRoot + "/Fire3.prefab";
    private const string ExplosionPrefabPath = DamageVfxRoot + "/Explosion11.prefab";
    private static readonly int[] ExplosionParticleCaps = { 18, 18, 24 };

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
        Material metalDebrisMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            MetalDebrisMaterialPath
        );
        if (lightClip == null || heavyClip == null || impactEffect == null ||
            metalDebrisMaterial == null)
        {
            throw new InvalidOperationException(
                "Bike impact setup requires both clips, Collision Spark and Metal Debris."
            );
        }

        int configured = 0;
        foreach (string prefabPath in GetBikePrefabPaths())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ConfigurePrefab(
                    root,
                    lightClip,
                    heavyClip,
                    impactEffect,
                    metalDebrisMaterial
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
            $"[Arcade Bikes] Configured light/heavy collision audio and pooled " +
            $"spark/flash/debris FX, fuel, smoke/fire/explosion, Player damage, visual " +
            $"deformation, rider ejection and in-place " +
            $"ABP bike ragdoll on " +
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
        Material metalDebrisMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            MetalDebrisMaterialPath
        );
        if (lightClip == null || heavyClip == null || impactEffect == null ||
            metalDebrisMaterial == null)
        {
            throw new InvalidOperationException("Bike collision audio/FX assets are missing.");
        }

        ConfigurePrefab(root, lightClip, heavyClip, impactEffect, metalDebrisMaterial);
    }

    private static void ConfigurePrefab(
        GameObject root,
        AudioClip lightClip,
        AudioClip heavyClip,
        GameObject impactEffect,
        Material metalDebrisMaterial)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));

        AudioSource source = EnsureImpactAudioSource(root);
        FranklinBikeImpactAudio impact = root.GetComponent<FranklinBikeImpactAudio>();
        if (impact == null) impact = root.AddComponent<FranklinBikeImpactAudio>();
        impact.ConfigureForBike(source, lightClip, heavyClip, impactEffect);
        impact.ConfigureMetalDebris(metalDebrisMaterial);

        FranklinArcadeBikeDriver driver =
            root.GetComponent<FranklinArcadeBikeDriver>();
        Traits traits = root.GetComponent<Traits>();
        ArcadeBP_Pro.ArcadeBikeControllerPro controller =
            root.GetComponent<ArcadeBP_Pro.ArcadeBikeControllerPro>();
        BikeEntry entry = root.GetComponent<BikeEntry>();
        Rigidbody body = root.GetComponent<Rigidbody>();
        PhysicsMaterial zeroFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(
            ZeroFrictionMaterialPath
        );
        PhysicsMaterial bodyFriction = EnsureRagdollBodyMaterial();
        if (driver == null || traits == null || controller == null ||
            entry == null || body == null)
        {
            throw new InvalidOperationException(
                $"{root.name} must be integrated before crash ragdoll is configured."
            );
        }

        FranklinBikeHealth health = root.GetComponent<FranklinBikeHealth>();
        if (health == null) health = root.AddComponent<FranklinBikeHealth>();
        health.Configure(traits, impact, driver, entry, "health-attribute-id");
        health.ConfigureReducedBikeDamageProfile();

        FranklinBikeFuel fuel = root.GetComponent<FranklinBikeFuel>();
        if (fuel == null) fuel = root.AddComponent<FranklinBikeFuel>();
        fuel.Configure(traits, driver, "fuel-attribute-id");
        driver.ConfigureFuel(fuel);

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
        FranklinBikePassengerSeat passengerSeat = ConfigurePassengerSeat(
            root,
            renderedBody,
            renderedBodyBounds,
            entry
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

        MeshFilter[] deformablePanels = ResolveDeformableBodyPanels(renderedBody);
        FranklinBikeDeformation deformation =
            root.GetComponent<FranklinBikeDeformation>();
        if (deformation == null)
            deformation = root.AddComponent<FranklinBikeDeformation>();
        deformation.Configure(impact, renderedBody, deformablePanels);

        ConfigureDamagePipeline(
            root,
            renderedBody,
            renderedBodyBounds,
            health,
            driver,
            bikeRagdoll,
            entry,
            body
        );

        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(impact);
        EditorUtility.SetDirty(health);
        EditorUtility.SetDirty(fuel);
        EditorUtility.SetDirty(driver);
        EditorUtility.SetDirty(frontWheelCollider);
        EditorUtility.SetDirty(rearWheelCollider);
        foreach (MeshCollider meshCollider in bodyMeshColliders)
            EditorUtility.SetDirty(meshCollider);
        EditorUtility.SetDirty(entry);
        EditorUtility.SetDirty(passengerSeat);
        EditorUtility.SetDirty(bikeRagdoll);
        EditorUtility.SetDirty(crash);
        EditorUtility.SetDirty(deformation);
        EditorUtility.SetDirty(root);
    }

    private static void InstallIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += InstallIfNeeded;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string[] paths = GetBikePrefabPaths();
        if (paths.Length == 0) return;
        bool complete = paths.All(path =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            FranklinBikeImpactAudio impact =
                prefab != null ? prefab.GetComponent<FranklinBikeImpactAudio>() : null;
            FranklinBikeCrashRagdoll crash =
                prefab != null ? prefab.GetComponent<FranklinBikeCrashRagdoll>() : null;
            FranklinBikeHealth health =
                prefab != null ? prefab.GetComponent<FranklinBikeHealth>() : null;
            FranklinBikeFuel fuel =
                prefab != null ? prefab.GetComponent<FranklinBikeFuel>() : null;
            FranklinArcadeBikeRagdoll bikeRagdoll =
                prefab != null ? prefab.GetComponent<FranklinArcadeBikeRagdoll>() : null;
            FranklinBikeDamageEffects damageEffects =
                prefab != null ? prefab.GetComponent<FranklinBikeDamageEffects>() : null;
            FranklinBikeDestruction destruction =
                prefab != null ? prefab.GetComponent<FranklinBikeDestruction>() : null;
            FranklinBikeDeformation deformation =
                prefab != null ? prefab.GetComponent<FranklinBikeDeformation>() : null;
            FranklinBikePassengerSeat passengerSeat =
                prefab != null ? prefab.GetComponent<FranklinBikePassengerSeat>() : null;
            BikeEntry entry = prefab != null ? prefab.GetComponent<BikeEntry>() : null;
            return impact != null && impact.IsConfigured &&
                   impact.HasMetalDebrisConfiguration &&
                   impact.HasCurrentMetalDebrisConfiguration &&
                   health != null && health.IsConfigured &&
                   fuel != null && fuel.IsConfigured &&
                   health.HasReducedBikeDamageProfile &&
                   bikeRagdoll != null && bikeRagdoll.IsConfigured &&
                   crash != null && crash.IsConfigured &&
                   crash.HasCurrentConfiguration &&
                   damageEffects != null && damageEffects.IsConfigured &&
                   damageEffects.HasCurrentConfiguration &&
                   destruction != null && destruction.IsConfigured &&
                   deformation != null && deformation.IsConfigured &&
                   deformation.HasCurrentConfiguration &&
                   passengerSeat != null && passengerSeat.IsConfigured &&
                   passengerSeat.HasCurrentConfiguration &&
                   entry != null && entry.HasCurrentBlockedRecoveryConfiguration &&
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

    private static void ConfigureDamagePipeline(
        GameObject root,
        Transform renderedBody,
        Bounds renderedBodyBounds,
        FranklinBikeHealth health,
        FranklinArcadeBikeDriver driver,
        FranklinArcadeBikeRagdoll bikeRagdoll,
        BikeEntry entry,
        Rigidbody body)
    {
        AudioClip explosionClip = RequireAsset<AudioClip>(ExplosionAudioPath);
        AudioClip smokeLoopClip = RequireAsset<AudioClip>(SmokeLoopAudioPath);
        AudioClip fireLoopClip = RequireAsset<AudioClip>(FireLoopAudioPath);

        Transform effectsRoot = GetOrCreateChild(renderedBody, "Bike Damage Effects");
        ResetLocalTransform(effectsRoot, Vector3.zero, Vector3.one);
        Vector3 rootLocalEffectPosition = renderedBodyBounds.center + new Vector3(
            0f,
            renderedBodyBounds.extents.y * 0.12f,
            0f
        );
        Vector3 worldEffectPosition = root.transform.TransformPoint(
            rootLocalEffectPosition
        );
        Vector3 effectPosition = effectsRoot.InverseTransformPoint(worldEffectPosition);

        Transform loopAudioRoot = GetOrCreateChild(effectsRoot, "Loop Audio");
        ResetLocalTransform(loopAudioRoot, effectPosition, Vector3.one);
        AudioSource smokeAudio = EnsureLoopAudioSource(
            GetOrCreateChild(loopAudioRoot, "Smoke Hiss").gameObject,
            smokeLoopClip,
            0.18f,
            2.5f,
            30f,
            96
        );
        AudioSource fireAudio = EnsureLoopAudioSource(
            GetOrCreateChild(loopAudioRoot, "Fire Crackle").gameObject,
            fireLoopClip,
            0.5f,
            3.5f,
            48f,
            72
        );

        Transform smokeRoot = GetOrCreateChild(effectsRoot, "Weak Health Smoke");
        ParticleSystem smokeParticles = ConfigureLoopEffect(
            ReplaceWithImportedEffect(
                smokeRoot,
                SmokePrefabPath,
                effectPosition,
                0.46f
            ),
            22,
            6f
        );

        Transform warningFireRoot = GetOrCreateChild(
            effectsRoot,
            "Critical Warning Fire"
        );
        ParticleSystem warningFireParticles = ConfigureLoopEffect(
            ReplaceWithImportedEffect(
                warningFireRoot,
                FirePrefabPath,
                effectPosition + new Vector3(0f, 0.02f, -0.05f),
                0.3f
            ),
            12,
            6f
        );

        Transform destroyedFireRoot = GetOrCreateChild(
            effectsRoot,
            "Destroyed Fire"
        );
        ParticleSystem destroyedFireParticles = ConfigureLoopEffect(
            ReplaceWithImportedEffect(
                destroyedFireRoot,
                FirePrefabPath,
                effectPosition,
                0.4f
            ),
            18,
            9f
        );

        Transform explosionRoot = GetOrCreateChild(effectsRoot, "Explosion Burst");
        ParticleSystem[] explosionParticles = ConfigureExplosion(
            ReplaceWithImportedEffect(
                explosionRoot,
                ExplosionPrefabPath,
                effectPosition,
                0.52f
            )
        );
        AudioSource explosionAudio = EnsureExplosionAudioSource(
            explosionRoot.gameObject,
            explosionClip
        );

        Transform occupantFireRoot = GetOrCreateChild(
            effectsRoot,
            "Occupant Burn Fire"
        );
        ParticleSystem occupantFireParticles = ConfigureLoopEffect(
            ReplaceWithImportedEffect(
                occupantFireRoot,
                FirePrefabPath,
                Vector3.zero,
                0.3f
            ),
            10,
            5f
        );

        smokeRoot.gameObject.SetActive(false);
        warningFireRoot.gameObject.SetActive(false);
        destroyedFireRoot.gameObject.SetActive(false);
        explosionRoot.gameObject.SetActive(true);
        occupantFireRoot.gameObject.SetActive(false);

        Renderer[] bikeRenderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer != null &&
                renderer is not ParticleSystemRenderer &&
                renderer is not TrailRenderer &&
                renderer is not LineRenderer &&
                !renderer.transform.IsChildOf(effectsRoot))
            .ToArray();

        FranklinBikeDestruction destruction =
            root.GetComponent<FranklinBikeDestruction>();
        if (destruction == null)
            destruction = root.AddComponent<FranklinBikeDestruction>();
        destruction.Configure(
            health,
            driver,
            bikeRagdoll,
            entry,
            body,
            bikeRenderers,
            occupantFireRoot.gameObject,
            "hp"
        );

        FranklinBikeParticleWind particleWind =
            root.GetComponent<FranklinBikeParticleWind>();
        if (particleWind == null)
            particleWind = root.AddComponent<FranklinBikeParticleWind>();
        particleWind.Configure(
            body,
            new[]
            {
                smokeParticles,
                warningFireParticles,
                destroyedFireParticles,
                occupantFireParticles
            }
        );

        FranklinBikeDamageEffects damageEffects =
            root.GetComponent<FranklinBikeDamageEffects>();
        if (damageEffects == null)
            damageEffects = root.AddComponent<FranklinBikeDamageEffects>();
        damageEffects.Configure(
            health,
            smokeRoot.gameObject,
            warningFireRoot.gameObject,
            destroyedFireRoot.gameObject,
            particleWind,
            smokeAudio,
            smokeLoopClip,
            fireAudio,
            fireLoopClip,
            explosionParticles,
            explosionAudio,
            explosionClip,
            destruction
        );

        EditorUtility.SetDirty(smokeAudio);
        EditorUtility.SetDirty(fireAudio);
        EditorUtility.SetDirty(explosionAudio);
        EditorUtility.SetDirty(destruction);
        EditorUtility.SetDirty(particleWind);
        EditorUtility.SetDirty(damageEffects);
    }

    private static MeshFilter[] ResolveDeformableBodyPanels(Transform renderedBody)
    {
        if (renderedBody == null) return Array.Empty<MeshFilter>();
        MeshFilter[] panels = renderedBody.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter != null && filter.sharedMesh != null &&
                filter.GetComponent<MeshRenderer>() != null &&
                !IsUnderNamedAncestor(filter.transform, "Bike Damage Effects") &&
                !ContainsIgnoreCase(filter.name, "wheel") &&
                !ContainsIgnoreCase(filter.name, "glass") &&
                !ContainsIgnoreCase(filter.name, "collider"))
            .ToArray();
        if (panels.Length == 0)
        {
            throw new InvalidOperationException(
                $"{renderedBody.root.name} has no readable render-body panels for deformation."
            );
        }
        return panels;
    }

    private static bool ContainsIgnoreCase(string value, string fragment)
    {
        return value != null && value.IndexOf(
            fragment,
            StringComparison.OrdinalIgnoreCase
        ) >= 0;
    }

    private static bool IsUnderNamedAncestor(Transform target, string ancestorName)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (string.Equals(current.name, ancestorName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static T RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException("Missing Bike damage asset: " + path);
        return asset;
    }

    private static GameObject ReplaceWithImportedEffect(
        Transform wrapper,
        string prefabPath,
        Vector3 localPosition,
        float localScale)
    {
        for (int i = wrapper.childCount - 1; i >= 0; --i)
            UnityEngine.Object.DestroyImmediate(wrapper.GetChild(i).gameObject);
        ResetLocalTransform(wrapper, localPosition, Vector3.one);

        GameObject source = RequireAsset<GameObject>(prefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(source, wrapper, false);
        instance.name = source.name;
        ResetLocalTransform(instance.transform, Vector3.zero, Vector3.one * localScale);
        Light[] lights = instance.GetComponentsInChildren<Light>(true);
        for (int i = lights.Length - 1; i >= 0; --i)
            UnityEngine.Object.DestroyImmediate(lights[i]);
        return instance;
    }

    private static ParticleSystem ConfigureLoopEffect(
        GameObject root,
        int maxParticles,
        float maxEmissionRate)
    {
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length != 1)
            throw new InvalidOperationException(root.name + " requires one ParticleSystem.");

        ParticleSystem.MainModule main = systems[0].main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.None;
        ParticleSystem.EmissionModule emission = systems[0].emission;
        emission.rateOverTime = Mathf.Min(
            emission.rateOverTime.constantMax,
            maxEmissionRate
        );
        ParticleSystem.ForceOverLifetimeModule force = systems[0].forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.World;
        force.x = new ParticleSystem.MinMaxCurve(0f);
        force.y = new ParticleSystem.MinMaxCurve(0f);
        force.z = new ParticleSystem.MinMaxCurve(0f);
        ConfigureParticleRenderer(systems[0]);
        systems[0].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return systems[0];
    }

    private static ParticleSystem[] ConfigureExplosion(GameObject root)
    {
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length != ExplosionParticleCaps.Length)
            throw new InvalidOperationException("Explosion11 requires three ParticleSystems.");
        for (int i = 0; i < systems.Length; ++i)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = ExplosionParticleCaps[i];
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;
            ConfigureParticleRenderer(systems[i]);
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        return systems;
    }

    private static void ConfigureParticleRenderer(ParticleSystem particles)
    {
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            throw new InvalidOperationException(particles.name + " requires a renderer.");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
        renderer.sortingOrder = 5;
    }

    private static AudioSource EnsureLoopAudioSource(
        GameObject target,
        AudioClip clip,
        float volume,
        float minDistance,
        float maxDistance,
        int priority)
    {
        ResetLocalTransform(target.transform, Vector3.zero, Vector3.one);
        AudioSource source = target.GetComponent<AudioSource>();
        if (source == null) source = target.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.priority = priority;
        source.Stop();
        return source;
    }

    private static AudioSource EnsureExplosionAudioSource(
        GameObject target,
        AudioClip clip)
    {
        AudioSource source = target.GetComponent<AudioSource>();
        if (source == null) source = target.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 5f;
        source.maxDistance = 90f;
        source.volume = 1f;
        source.priority = 24;
        return source;
    }

    private static void ResetLocalTransform(
        Transform target,
        Vector3 localPosition,
        Vector3 localScale)
    {
        target.localPosition = localPosition;
        target.localRotation = Quaternion.identity;
        target.localScale = localScale;
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
                             !IsUnderNamedAncestor(
                                 filter.transform,
                                 "Bike Damage Effects"
                             ) &&
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
        entry.ConfigureBlockedFallenBikeRecovery();
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

    private static FranklinBikePassengerSeat ConfigurePassengerSeat(
        GameObject root,
        Transform renderedBody,
        Bounds bounds,
        BikeEntry entry)
    {
        if (root == null || renderedBody == null || entry == null)
            throw new InvalidOperationException("Bike passenger setup is missing its body or entry.");

        Transform passengerRoot = GetOrCreateChild(renderedBody, "Bike Passenger Targets");
        passengerRoot.localPosition = Vector3.zero;
        passengerRoot.localRotation = Quaternion.identity;
        passengerRoot.localScale = Vector3.one;

        Vector3 riderSeat = entry.entryParent != null
            ? renderedBody.InverseTransformPoint(entry.entryParent.position)
            : bounds.center;
        float rearDistance = Mathf.Clamp(bounds.extents.z * 0.48f, 0.32f, 0.55f);
        float sideDistance = Mathf.Clamp(bounds.extents.x + 0.42f, 0.72f, 1.1f);
        float entryY = Mathf.Max(bounds.min.y, riderSeat.y - 0.28f);

        Transform seat = GetOrCreateChild(passengerRoot, "Passenger Seat");
        seat.localPosition = riderSeat + new Vector3(0f, 0.02f, -rearDistance);
        seat.localRotation = Quaternion.identity;
        float rootHalfHeight = 0.95f;
        Transform entryLeft = GetOrCreateChild(passengerRoot, "Passenger Entry Left");
        entryLeft.localPosition = new Vector3(
            -sideDistance,
            entryY + rootHalfHeight,
            seat.localPosition.z
        );
        entryLeft.localRotation = Quaternion.identity;
        Transform entryRight = GetOrCreateChild(passengerRoot, "Passenger Entry Right");
        entryRight.localPosition = new Vector3(
            sideDistance,
            entryY + rootHalfHeight,
            seat.localPosition.z
        );
        entryRight.localRotation = Quaternion.identity;

        Transform leftHand = GetOrCreateChild(passengerRoot, "Passenger Left Hand");
        Transform rightHand = GetOrCreateChild(passengerRoot, "Passenger Right Hand");
        leftHand.localPosition = riderSeat + new Vector3(-0.24f, 0.28f, -0.08f);
        rightHand.localPosition = riderSeat + new Vector3(0.24f, 0.28f, -0.08f);
        leftHand.localRotation = Quaternion.identity;
        rightHand.localRotation = Quaternion.identity;

        Transform leftFoot = GetOrCreateChild(passengerRoot, "Passenger Left Foot");
        Transform rightFoot = GetOrCreateChild(passengerRoot, "Passenger Right Foot");
        leftFoot.localPosition = seat.localPosition + new Vector3(-0.3f, -0.48f, 0.14f);
        rightFoot.localPosition = seat.localPosition + new Vector3(0.3f, -0.48f, 0.14f);
        leftFoot.localRotation = Quaternion.identity;
        rightFoot.localRotation = Quaternion.identity;

        FranklinBikePassengerSeat passenger =
            root.GetComponent<FranklinBikePassengerSeat>();
        if (passenger == null) passenger = root.AddComponent<FranklinBikePassengerSeat>();
        passenger.Configure(
            seat,
            entryLeft,
            entryRight,
            leftHand,
            rightHand,
            leftFoot,
            rightFoot,
            AssetDatabase.LoadAssetAtPath<AnimationClip>(PassengerPosePath),
            entry.animationMask
        );
        return passenger;
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
        MoveDirectRootChild(root.transform, renderedBody, "Bike Damage Effects");
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
        Transform damageEffects = hierarchy.FirstOrDefault(
            item => item.name == "Bike Damage Effects"
        );
        if (front == null || rear == null || front.parent != body || rear.parent != body ||
            damageEffects == null || damageEffects.parent != body ||
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
