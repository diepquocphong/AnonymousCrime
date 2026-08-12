using System;
using Ashsvp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Vehicles.Editor
{
    public static class SimcadeCarDamageEffectsInstaller
    {
        private const string CarPrefabPath =
            "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Prefabs/Car.prefab";
        private const string ExplosionAudioPath =
            "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Audio/SFX/car_explosion_test_vehicle_cc0.wav";
        private const string SmokeLoopAudioPath =
            "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Audio/SFX/car_smoke_hiss_loop_cc0.wav";
        private const string FireLoopAudioPath =
            "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Audio/SFX/car_fire_crackle_loop_cc0.wav";
        private const string HovlRoot =
            "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/VFX/ThirdParty/Hovl Studio";
        private const string HovlPrefabRoot =
            HovlRoot + "/3D Fire and Explosions/Prefabs";
        private const string SmokePrefabPath = HovlPrefabRoot + "/Smoke1.prefab";
        private const string FirePrefabPath = HovlPrefabRoot + "/Fire3.prefab";
        private const string ExplosionPrefabPath = HovlPrefabRoot + "/Explosion11.prefab";
        private const string HovlTextureRoot = HovlRoot + "/HSFiles/Textures";

        private static readonly Vector3 HoodLocalPosition =
            new Vector3(0f, -0.42f, 1.45f);
        private static readonly int[] ExplosionParticleCaps = { 18, 18, 24 };

        public static void Install()
        {
            ConfigureImportedTextures();
            AudioClip explosionClip = EnsureExplosionAudio();
            AudioClip smokeLoopClip = EnsureLoopAudio(SmokeLoopAudioPath, 0.5f);
            AudioClip fireLoopClip = EnsureLoopAudio(FireLoopAudioPath, 0.58f);
            GameObject carRoot = PrefabUtility.LoadPrefabContents(CarPrefabPath);
            try
            {
                SimcadeCarHealth health = RequireComponent<SimcadeCarHealth>(carRoot);
                SimcadeCarDriver driver = RequireComponent<SimcadeCarDriver>(carRoot);
                CarEntry carEntry = RequireComponent<CarEntry>(carRoot);
                SimcadeVehicleController controller =
                    RequireComponent<SimcadeVehicleController>(carRoot);
                Rigidbody carBody = RequireComponent<Rigidbody>(carRoot);
                Transform effectsRoot = GetOrCreateChild(carRoot.transform, "Car Damage Effects");
                ResetTransform(effectsRoot, Vector3.zero, Vector3.one);

                Transform loopAudioRoot = GetOrCreateChild(effectsRoot, "Loop Audio");
                ResetTransform(loopAudioRoot, HoodLocalPosition, Vector3.one);
                Transform smokeAudioRoot = GetOrCreateChild(loopAudioRoot, "Smoke Hiss");
                ResetTransform(smokeAudioRoot, Vector3.zero, Vector3.one);
                Transform fireAudioRoot = GetOrCreateChild(loopAudioRoot, "Fire Crackle");
                ResetTransform(fireAudioRoot, Vector3.zero, Vector3.one);
                AudioSource smokeLoopAudio = EnsureLoopAudioSource(
                    smokeAudioRoot.gameObject,
                    smokeLoopClip,
                    0.18f,
                    2.5f,
                    30f,
                    96
                );
                AudioSource fireLoopAudio = EnsureLoopAudioSource(
                    fireAudioRoot.gameObject,
                    fireLoopClip,
                    0.5f,
                    3.5f,
                    48f,
                    72
                );

                Transform smokeRoot = GetOrCreateChild(effectsRoot, "Weak Health Smoke");
                GameObject smokeInstance = ReplaceWithImportedEffect(
                    smokeRoot,
                    SmokePrefabPath,
                    HoodLocalPosition,
                    0.7f
                );
                ParticleSystem smokeParticles =
                    ConfigureLoopEffect(smokeInstance, 28, 7f);

                Transform criticalFireRoot = GetOrCreateChild(
                    effectsRoot,
                    "Critical Warning Fire"
                );
                GameObject criticalFireInstance = ReplaceWithImportedEffect(
                    criticalFireRoot,
                    FirePrefabPath,
                    HoodLocalPosition + new Vector3(0f, 0.02f, -0.08f),
                    0.43f
                );
                ParticleSystem criticalFireParticles =
                    ConfigureLoopEffect(criticalFireInstance, 16, 8f);

                Transform fireRoot = GetOrCreateChild(effectsRoot, "Destroyed Fire");
                GameObject fireInstance = ReplaceWithImportedEffect(
                    fireRoot,
                    FirePrefabPath,
                    HoodLocalPosition,
                    0.58f
                );
                ParticleSystem destroyedFireParticles =
                    ConfigureLoopEffect(fireInstance, 24, 12f);

                Transform explosionRoot = GetOrCreateChild(effectsRoot, "Explosion Burst");
                GameObject explosionInstance = ReplaceWithImportedEffect(
                    explosionRoot,
                    ExplosionPrefabPath,
                    HoodLocalPosition,
                    0.72f
                );
                ParticleSystem[] explosionParticles = ConfigureExplosion(explosionInstance);
                AudioSource explosionAudio = EnsureExplosionAudioSource(
                    explosionRoot.gameObject,
                    explosionClip
                );

                Transform occupantFireRoot = GetOrCreateChild(
                    effectsRoot,
                    "Occupant Burn Fire"
                );
                GameObject occupantFireInstance = ReplaceWithImportedEffect(
                    occupantFireRoot,
                    FirePrefabPath,
                    Vector3.zero,
                    0.34f
                );
                ParticleSystem occupantFireParticles =
                    ConfigureLoopEffect(occupantFireInstance, 12, 6f);

                smokeRoot.gameObject.SetActive(false);
                criticalFireRoot.gameObject.SetActive(false);
                fireRoot.gameObject.SetActive(false);
                explosionRoot.gameObject.SetActive(true);
                occupantFireRoot.gameObject.SetActive(false);

                SimcadeCarDestruction destruction =
                    GetOrAdd<SimcadeCarDestruction>(carRoot);
                destruction.Configure(
                    driver,
                    carEntry,
                    controller,
                    carBody,
                    occupantFireRoot.gameObject,
                    "hp"
                );

                SimcadeCarParticleWind particleWind =
                    GetOrAdd<SimcadeCarParticleWind>(carRoot);
                particleWind.Configure(
                    carBody,
                    new[]
                    {
                        smokeParticles,
                        criticalFireParticles,
                        destroyedFireParticles,
                        occupantFireParticles
                    }
                );

                SimcadeCarDamageEffects damageEffects = GetOrAdd<SimcadeCarDamageEffects>(carRoot);
                damageEffects.Configure(
                    health,
                    smokeRoot.gameObject,
                    criticalFireRoot.gameObject,
                    fireRoot.gameObject,
                    particleWind,
                    smokeLoopAudio,
                    smokeLoopClip,
                    fireLoopAudio,
                    fireLoopClip,
                    0.18f,
                    0.5f,
                    0.8f,
                    explosionParticles,
                    explosionAudio,
                    explosionClip,
                    0.32f,
                    0.14f,
                    1.35f,
                    destruction
                );

                EditorUtility.SetDirty(destruction);
                EditorUtility.SetDirty(particleWind);
                EditorUtility.SetDirty(damageEffects);
                EditorUtility.SetDirty(smokeLoopAudio);
                EditorUtility.SetDirty(fireLoopAudio);
                EditorUtility.SetDirty(explosionAudio);
                PrefabUtility.SaveAsPrefabAsset(carRoot, CarPrefabPath, out bool saved);
                if (!saved)
                    throw new InvalidOperationException("Could not save Car damage effects");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(carRoot);
            }

            AssetDatabase.SaveAssets();
            ValidateInstallation();
        }

        public static void ValidateInstallation()
        {
            ValidateImportedAssets();
            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (car == null) throw new InvalidOperationException("Car.prefab is missing");

            SimcadeCarDamageEffects effects = car.GetComponent<SimcadeCarDamageEffects>();
            SimcadeCarDestruction destruction = car.GetComponent<SimcadeCarDestruction>();
            SimcadeCarParticleWind particleWind = car.GetComponent<SimcadeCarParticleWind>();
            if (effects == null || !effects.IsConfigured)
                throw new InvalidOperationException("Car damage effects are incomplete");
            if (particleWind == null || !particleWind.IsConfigured ||
                particleWind.UpdateRateHz > 8.01f)
            {
                throw new InvalidOperationException(
                    "Car smoke/fire world wind must be configured at <=8 Hz"
                );
            }
            if (destruction == null || !destruction.IsConfigured)
                throw new InvalidOperationException("Car terminal destruction is incomplete");
            if (destruction.PlayerHealthAttributeId != "hp")
                throw new InvalidOperationException(
                    "Explosion Player damage must target the Player Traits 'hp' Attribute"
                );
            if (Mathf.Abs(destruction.WheelRestDuration - 5f) > 0.05f)
                throw new InvalidOperationException(
                    "Detached wheels must rest without physics for 5 seconds"
                );
            if (destruction.WreckMinimumVisibleDuration < 15f ||
                destruction.WreckHideDistance < 1f)
            {
                throw new InvalidOperationException(
                    "Wreck cleanup requires a 15-second minimum and Player distance gate"
                );
            }
            if (effects.MinimumExplosionCameraHold < 0.85f ||
                effects.MaximumExplosionCameraHold < 2f ||
                destruction.ExplosionCameraReturnDuration < 0.5f)
            {
                throw new InvalidOperationException(
                    "Explosion camera must hold through the burst and blend back to Player"
                );
            }
            if (effects.SmokeHealthThreshold < 0.3f || effects.SmokeHealthThreshold > 0.35f)
                throw new InvalidOperationException("Car smoke threshold must stay near 32% health");
            if (effects.CriticalFireThreshold < 0.12f ||
                effects.CriticalFireThreshold > 0.16f ||
                effects.PreExplosionWarningDuration < 1.25f ||
                effects.CriticalBurnDuration < 5f ||
                effects.CriticalBurnDuration > 10f ||
                effects.CriticalBurnTickInterval < 0.1f ||
                effects.CriticalBurnTickInterval > 0.5f)
            {
                throw new InvalidOperationException(
                    "Critical fire must start near 14%, drain health gradually and warn before explosion"
                );
            }

            Transform root = car.transform.Find("Car Damage Effects");
            Transform smokeRoot = root?.Find("Weak Health Smoke");
            Transform criticalFireRoot = root?.Find("Critical Warning Fire");
            Transform fireRoot = root?.Find("Destroyed Fire");
            Transform explosionRoot = root?.Find("Explosion Burst");
            Transform occupantFireRoot = root?.Find("Occupant Burn Fire");
            Transform loopAudioRoot = root?.Find("Loop Audio");
            Transform smokeAudioRoot = loopAudioRoot?.Find("Smoke Hiss");
            Transform fireAudioRoot = loopAudioRoot?.Find("Fire Crackle");
            if (root == null || smokeRoot == null || criticalFireRoot == null ||
                fireRoot == null ||
                explosionRoot == null || occupantFireRoot == null ||
                loopAudioRoot == null || smokeAudioRoot == null || fireAudioRoot == null)
                throw new InvalidOperationException("Car damage VFX hierarchy is missing");

            ParticleSystem[] smoke = smokeRoot.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem[] criticalFire =
                criticalFireRoot.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem[] fire = fireRoot.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem[] explosion = explosionRoot.GetComponentsInChildren<ParticleSystem>(true);
            if (smoke.Length != 1 || criticalFire.Length != 1 || fire.Length != 1 ||
                explosion.Length != 3)
            {
                throw new InvalidOperationException(
                    "Expected Hovl Smoke1 (1), warning Fire3 (1), destroyed Fire3 (1) " +
                    "and Explosion11 (3) particle systems"
                );
            }

            ValidateLoopEffect(smoke, 28, "Smoke1");
            ValidateLoopEffect(criticalFire, 16, "Critical warning Fire3");
            ValidateLoopEffect(fire, 24, "Fire3");
            if (smokeRoot.gameObject.activeSelf || criticalFireRoot.gameObject.activeSelf ||
                fireRoot.gameObject.activeSelf)
            {
                throw new InvalidOperationException(
                    "Car damage loop effects must be inactive in the prefab"
                );
            }
            ParticleSystem[] occupantFire =
                occupantFireRoot.GetComponentsInChildren<ParticleSystem>(true);
            if (occupantFire.Length != 1 || occupantFireRoot.gameObject.activeSelf)
            {
                throw new InvalidOperationException(
                    "The prebuilt occupant burn effect must contain one inactive ParticleSystem"
                );
            }
            ValidateLoopEffect(occupantFire, 12, "Occupant Fire3");
            int explosionCapacity = 0;
            for (int i = 0; i < explosion.Length; ++i)
            {
                ParticleSystem.MainModule main = explosion[i].main;
                if (main.loop || main.playOnAwake)
                    throw new InvalidOperationException("Explosion11 must be a manual one-shot");
                explosionCapacity += main.maxParticles;
                ValidateRenderer(explosion[i], "Explosion11");
            }
            if (explosionCapacity > 60)
                throw new InvalidOperationException("Explosion11 particle budget exceeds 60");

            AudioClip smokeLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                SmokeLoopAudioPath
            );
            AudioClip fireLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                FireLoopAudioPath
            );
            ValidateLoopAudioSource(
                smokeAudioRoot.GetComponent<AudioSource>(),
                smokeLoopClip,
                2.5f,
                30f,
                "Smoke hiss"
            );
            ValidateLoopAudioSource(
                fireAudioRoot.GetComponent<AudioSource>(),
                fireLoopClip,
                3.5f,
                48f,
                "Fire crackle"
            );
            ValidateMobileLoopAudio(SmokeLoopAudioPath, smokeLoopClip, "Smoke hiss");
            ValidateMobileLoopAudio(FireLoopAudioPath, fireLoopClip, "Fire crackle");
            if (effects.SmokeLoopVolume > 0.25f ||
                effects.CriticalFireLoopVolume < 0.4f ||
                effects.DestroyedFireLoopVolume < effects.CriticalFireLoopVolume)
            {
                throw new InvalidOperationException(
                    "Smoke must stay subtle and destroyed fire must be louder than warning fire"
                );
            }

            AudioSource audioSource = explosionRoot.GetComponent<AudioSource>();
            AudioClip explosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ExplosionAudioPath);
            if (audioSource == null || audioSource.clip != explosionClip ||
                audioSource.playOnAwake || audioSource.spatialBlend < 0.99f ||
                audioSource.dopplerLevel > 0.01f || audioSource.minDistance < 6.9f ||
                audioSource.maxDistance < 109f)
            {
                throw new InvalidOperationException("Explosion 3D audio source is invalid");
            }

            AudioImporter importer = AssetImporter.GetAtPath(ExplosionAudioPath) as AudioImporter;
            AudioImporterSampleSettings settings = importer != null
                ? importer.defaultSampleSettings
                : default;
            if (importer == null || settings.loadType != AudioClipLoadType.CompressedInMemory ||
                !settings.preloadAudioData || explosionClip.channels != 1 ||
                importer.forceToMono)
            {
                throw new InvalidOperationException("Explosion audio import is not mobile-safe");
            }

            Debug.Log(
                "Car damage effects validation passed: Hovl Smoke1/Fire3/Explosion11, " +
                "world-space physical wind at 8 Hz, 32% smoke, 14% critical fire, 1.35s " +
                "guaranteed pre-explosion warning, 7s event-driven critical health drain, " +
                "delayed camera hold + Player return blend, " +
                "wheel/wreck cleanup, occupant ragdoll/fire, " +
                "3D smoke/fire/explosion audio and <=60 explosion particles are configured."
            );
        }

        private static void ValidateImportedAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SmokePrefabPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPrefabPath) == null)
            {
                throw new InvalidOperationException(
                    "Selected Hovl Studio Smoke1/Fire3/Explosion11 assets are missing"
                );
            }
        }

        private static void ValidateLoopEffect(
            ParticleSystem[] particles,
            int capacity,
            string label)
        {
            ParticleSystem.MainModule main = particles[0].main;
            ParticleSystem.ForceOverLifetimeModule force =
                particles[0].forceOverLifetime;
            if (!main.loop || main.playOnAwake || main.maxParticles > capacity ||
                main.simulationSpace != ParticleSystemSimulationSpace.World ||
                !force.enabled || force.space != ParticleSystemSimulationSpace.World)
                throw new InvalidOperationException(label + " is not mobile-safe");
            ValidateRenderer(particles[0], label);
        }

        private static void ValidateRenderer(ParticleSystem particles, string label)
        {
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (renderer == null || material == null || material.shader == null ||
                !material.shader.isSupported)
            {
                throw new InvalidOperationException(label + " has an unsupported VFX material");
            }
            if (renderer.shadowCastingMode != ShadowCastingMode.Off || renderer.receiveShadows)
                throw new InvalidOperationException(label + " particle shadows must be disabled");
        }

        private static AudioClip EnsureExplosionAudio()
        {
            AudioImporter importer = AssetImporter.GetAtPath(ExplosionAudioPath) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("Real vehicle explosion audio is missing");

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.72f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            // The authored source is already mono. Keeping forceToMono disabled avoids
            // Unity normalizing the transient again during import.
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(ExplosionAudioPath);
        }

        private static AudioClip EnsureLoopAudio(string path, float quality)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("Damage loop audio is missing: " + path);

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = Mathf.Clamp01(quality);
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.SaveAndReimport();

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || clip.channels != 1)
                throw new InvalidOperationException("Damage loop audio must be mono: " + path);
            return clip;
        }

        private static void ValidateMobileLoopAudio(
            string path,
            AudioClip clip,
            string label)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            AudioImporterSampleSettings settings = importer != null
                ? importer.defaultSampleSettings
                : default;
            if (importer == null || clip == null || clip.channels != 1 ||
                settings.loadType != AudioClipLoadType.CompressedInMemory ||
                !settings.preloadAudioData || importer.forceToMono)
            {
                throw new InvalidOperationException(label + " audio import is not mobile-safe");
            }
        }

        private static void ConfigureImportedTextures()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { HovlTextureRoot });
            for (int i = 0; i < textureGuids.Length; ++i)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer == null) continue;

                bool isPoint = texturePath.EndsWith("Point19.png", StringComparison.Ordinal);
                int maxSize = isPoint ? 256 : 512;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                ConfigureMobileTexture(importer, "Android", maxSize);
                ConfigureMobileTexture(importer, "iPhone", maxSize);
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureMobileTexture(
            TextureImporter importer,
            string platform,
            int maxSize)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.format = TextureImporterFormat.ASTC_6x6;
            settings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(settings);
        }

        private static GameObject ReplaceWithImportedEffect(
            Transform wrapper,
            string prefabPath,
            Vector3 localPosition,
            float localScale)
        {
            ClearEffectContent(wrapper);
            ResetTransform(wrapper, localPosition, Vector3.one);

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (source == null)
                throw new InvalidOperationException("Missing VFX prefab: " + prefabPath);

            GameObject instance = UnityEngine.Object.Instantiate(source, wrapper, false);
            instance.name = source.name;
            ResetTransform(instance.transform, Vector3.zero, Vector3.one * localScale);
            RemoveLights(instance);
            return instance;
        }

        private static void ClearEffectContent(Transform wrapper)
        {
            for (int i = wrapper.childCount - 1; i >= 0; --i)
                UnityEngine.Object.DestroyImmediate(wrapper.GetChild(i).gameObject);

            ParticleSystemRenderer renderer = wrapper.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
            ParticleSystem particles = wrapper.GetComponent<ParticleSystem>();
            if (particles != null) UnityEngine.Object.DestroyImmediate(particles);
        }

        private static void RemoveLights(GameObject root)
        {
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = lights.Length - 1; i >= 0; --i)
                UnityEngine.Object.DestroyImmediate(lights[i]);
        }

        private static ParticleSystem ConfigureLoopEffect(
            GameObject root,
            int maxParticles,
            float maxEmissionRate)
        {
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length != 1)
                throw new InvalidOperationException(root.name + " must contain one ParticleSystem");

            ParticleSystem.MainModule main = systems[0].main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = systems[0].emission;
            emission.rateOverTime = Mathf.Min(emission.rateOverTime.constantMax, maxEmissionRate);
            ParticleSystem.ForceOverLifetimeModule force = systems[0].forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(0f);
            force.y = new ParticleSystem.MinMaxCurve(0f);
            force.z = new ParticleSystem.MinMaxCurve(0f);
            ConfigureRenderer(systems[0]);
            systems[0].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return systems[0];
        }

        private static ParticleSystem[] ConfigureExplosion(GameObject root)
        {
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length != ExplosionParticleCaps.Length)
                throw new InvalidOperationException("Explosion11 must contain three ParticleSystems");

            for (int i = 0; i < systems.Length; ++i)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.loop = false;
                main.playOnAwake = false;
                main.maxParticles = ExplosionParticleCaps[i];
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.stopAction = ParticleSystemStopAction.None;
                ConfigureRenderer(systems[i]);
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            return systems;
        }

        private static void ConfigureRenderer(ParticleSystem particles)
        {
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                throw new InvalidOperationException(particles.name + " requires a renderer");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = 5;
        }

        private static AudioSource EnsureExplosionAudioSource(GameObject target, AudioClip clip)
        {
            AudioSource source = target.GetComponent<AudioSource>();
            if (source == null) source = target.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 7f;
            source.maxDistance = 110f;
            source.volume = 1f;
            source.priority = 24;
            return source;
        }

        private static AudioSource EnsureLoopAudioSource(
            GameObject target,
            AudioClip clip,
            float volume,
            float minDistance,
            float maxDistance,
            int priority)
        {
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

        private static void ValidateLoopAudioSource(
            AudioSource source,
            AudioClip clip,
            float minDistance,
            float maxDistance,
            string label)
        {
            if (source == null || clip == null || source.clip != clip ||
                source.playOnAwake || !source.loop || source.spatialBlend < 0.99f ||
                source.dopplerLevel > 0.01f ||
                Mathf.Abs(source.minDistance - minDistance) > 0.05f ||
                Mathf.Abs(source.maxDistance - maxDistance) > 0.05f)
            {
                throw new InvalidOperationException(label + " 3D loop source is invalid");
            }
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static void ResetTransform(
            Transform target,
            Vector3 localPosition,
            Vector3 localScale)
        {
            target.localPosition = localPosition;
            target.localRotation = Quaternion.identity;
            target.localScale = localScale;
        }

        private static T RequireComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component == null)
                throw new InvalidOperationException(
                    $"Car.prefab requires {typeof(T).Name} before damage effect setup"
                );
            return component;
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }
    }
}
