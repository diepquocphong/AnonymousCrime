using System;
using System.Collections.Generic;
using System.Reflection;
using FranklinGame.Shooter;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using GameCreator.Runtime.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Shooter.Editor
{
    /// <summary>Creates/repairs all generated GC2 shooter assets from project-local sources.</summary>
    [InitializeOnLoad]
    internal static class FranklinShooterInstaller
    {
        private const string ROOT = "Assets/ShooterSystemGC2";
        private const string RESOURCE_ROOT = ROOT + "/Resources/FranklinShooter";
        private const string CATALOG_PATH = RESOURCE_ROOT + "/Franklin Shooter Catalog.asset";
        private const string CAUTIOUS_WALK_ICON_PATH =
            RESOURCE_ROOT + "/UI/Controls/cautious-walk.png";
        private const string FIRST_PERSON_ICON_PATH =
            RESOURCE_ROOT + "/UI/Controls/first-person-camera.png";
        private const string ANIMATION_ROOT = RESOURCE_ROOT + "/Animations";
        private const string UPPER_BODY_MASK_PATH =
            ANIMATION_ROOT + "/Franklin Shooter Upper Body.mask";
        private const string BIKE_DRIVER_MASK_PATH =
            ANIMATION_ROOT + "/Franklin Bike Driver Seat And Left Hand.mask";
        private const string BIKE_DRIVER_SHOOTER_MASK_PATH =
            ANIMATION_ROOT + "/Franklin Bike Driver Shooter Upper Body.mask";
        private const string SHOOTER_LOCOMOTION_PATH =
            ANIMATION_ROOT + "/Franklin Shooter Upper Body Locomotion.asset";
        private const string BIKE_SIGHT_ROOT = RESOURCE_ROOT + "/Sights/Bike";
        private const string THROWABLE_SIGHT_ROOT = RESOURCE_ROOT + "/Sights/Throwable";
        private const string THROWABLE_IDLE_SIGHT_PATH =
            THROWABLE_SIGHT_ROOT + "/Franklin Throwable Idle.asset";
        private const string THROWABLE_THROW_SIGHT_PATH =
            THROWABLE_SIGHT_ROOT + "/Franklin Throwable Throw.asset";
        private const string RPG_SIGHT_ROOT = RESOURCE_ROOT + "/Sights/RPG7";
        private const string RPG_IDLE_SIGHT_PATH =
            RPG_SIGHT_ROOT + "/Franklin RPG7 Idle.asset";
        private const string RPG_AIM_SIGHT_PATH =
            RPG_SIGHT_ROOT + "/Franklin RPG7 Aim.asset";
        private const string RPG_HOLD_ANIMATION_PATH =
            ROOT + "/Animations/RPG7/HumanM@WeaponHold_Bazooka01.fbx";
        private const string RPG_AIM_ANIMATION_PATH =
            ROOT + "/Animations/RPG7/HumanM@Bazooka_Aim01.fbx";
        private const string RPG_SHOOT_ANIMATION_PATH =
            ROOT + "/Animations/RPG7/HumanM@Bazooka_Aim01_Shoot01.fbx";
        private const string RPG_PROJECTILE_PATH =
            PROJECTILE_ROOT + "/rpg7-rocket-projectile.prefab";
        private const string RPG_AUDIO_ROOT = ROOT + "/Audio/RPG7";
        private const string RPG_LAUNCH_AUDIO_PATH =
            RPG_AUDIO_ROOT + "/RPG7_Launch_CC0.wav";
        private const string RPG_FLIGHT_AUDIO_PATH =
            RPG_AUDIO_ROOT + "/RPG7_Flight_Loop_CC0.wav";
        private const string RPG_EXPLOSION_CLOSE_AUDIO_PATH =
            RPG_AUDIO_ROOT + "/RPG7_Explosion_Close_CC0.mp3";
        private const string RPG_EXPLOSION_DISTANT_AUDIO_PATH =
            RPG_AUDIO_ROOT + "/RPG7_Explosion_Distant_CC0.mp3";
        private static readonly string[] RPG_AUDIO_PATHS =
        {
            RPG_LAUNCH_AUDIO_PATH,
            RPG_FLIGHT_AUDIO_PATH,
            RPG_EXPLOSION_CLOSE_AUDIO_PATH,
            RPG_EXPLOSION_DISTANT_AUDIO_PATH
        };
        private const string BIKE_DRIVER_AIM_ID = "bike-driver-aim";
        private const string MATERIAL_SOUNDS_ROOT = RESOURCE_ROOT + "/MaterialSounds";
        private const string IMPACT_AUDIO_ONLY_PATH =
            MATERIAL_SOUNDS_ROOT + "/Franklin Shooter Impact Audio.asset";
        private const string MATERIAL_ROOT = RESOURCE_ROOT + "/Materials";
        private const string EFFECT_ROOT = RESOURCE_ROOT + "/Effects";
        private const string AMMO_ROOT = RESOURCE_ROOT + "/Ammo";
        private const string PROJECTILE_ROOT = RESOURCE_ROOT + "/Projectiles";
        private const string TEXTURE_ROOT = RESOURCE_ROOT + "/Textures";
        private const string VEHICLE_BULLET_TEXTURE_PATH =
            TEXTURE_ROOT + "/vehicle-bullet-hole.png";
        private const string VEHICLE_BULLET_MATERIAL_PATH =
            MATERIAL_ROOT + "/Vehicle Bullet Hole URP.mat";
        private const string WALL_BULLET_TEXTURE_PATH =
            TEXTURE_ROOT + "/wall-bullet-hole.png";
        private const string WALL_BULLET_MATERIAL_PATH =
            MATERIAL_ROOT + "/Wall Bullet Hole URP.mat";
        private const string RPG_EXPLOSION_DECAL_TEXTURE_PATH =
            TEXTURE_ROOT + "/rpg-explosion-scorch.png";
        private const string RPG_EXPLOSION_DECAL_MATERIAL_PATH =
            MATERIAL_ROOT + "/RPG Explosion Scorch URP.mat";
        private const string RPG_EXPLOSION_VEHICLE_DECAL_TEXTURE_PATH =
            TEXTURE_ROOT + "/rpg-explosion-vehicle-scorch.png";
        private const string RPG_EXPLOSION_VEHICLE_DECAL_MATERIAL_PATH =
            MATERIAL_ROOT + "/RPG Explosion Vehicle Scorch URP.mat";
        private const string VEHICLE_BULLET_SHADER_PATH =
            ROOT + "/Shaders/FranklinVehicleBulletDecal.shader";
        private const string VEHICLE_BULLET_SHADER =
            "Franklin Game/Vehicle Bullet Decal Mobile";
        private const string TRACER_MATERIAL_PATH = MATERIAL_ROOT + "/Raycast Trace URP.mat";
        private const string MUZZLE_MATERIAL_PATH = MATERIAL_ROOT + "/Muzzle Smoke URP.mat";
        private const string IMPACT_MATERIAL_PATH = MATERIAL_ROOT + "/Impact Poof URP.mat";
        private const string EXPLOSION_MATERIAL_PATH = MATERIAL_ROOT + "/Explosion Smoke URP.mat";
        private const string MUZZLE_EFFECT_PATH = EFFECT_ROOT + "/Muzzle Flash URP.prefab";
        private const string IMPACT_EFFECT_PATH = EFFECT_ROOT + "/Bullet Impact URP.prefab";
        private const string EXPLOSION_EFFECT_PATH = EFFECT_ROOT + "/Grenade Impact URP.prefab";
        private const string SAMPLE_ROOT = "Assets/Plugins/GameCreator/Installs/Shooter.Weapons@1.1.4";
        private const string CHARGE_GRENADE_FADE_MATERIAL =
            "Assets/Plugins/GameCreator/Installs/GameCreator.Blockout@1.6.12/Materials/Prototype_Fade_Green.mat";
        private const string CHARGE_GRENADE_GUIDE_MATERIAL =
            "Assets/Plugins/GameCreator/Installs/GameCreator.Blockout@1.6.12/Materials/Prototype_Guide_Red.mat";
        private const string CHARGE_GRENADE_JOINTS_MATERIAL =
            "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/3D/Materials/Joints.mat";
        private const string CHARGE_GRENADE_SURFACE_MATERIAL =
            "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/3D/Materials/Surface.mat";
        private const string LOW_WEAPON_MATERIAL = ROOT + "/WeaponsLow/Gun_MAT.mat";
        private const string SAMPLE_SHOOTER_LOCOMOTION_PATH =
            SAMPLE_ROOT + "/States/Shooter_Locomotion.asset";
        private const string LOW_PREFABS = ROOT + "/WeaponsLow/Prefabs";
        private const string PLAYER_PREFAB_PATH = "Assets/Prefab/Player.prefab";
        private const int SHOOTER_LOCOMOTION_LAYER = 7;
        // The project assigns vehicles to Bike (12), Car (13), and some nested physical
        // colliders to Ignore Raycast (2). GC2's sample mask excludes all three. An explicit
        // all-layer mask keeps projectile, trajectory, laser and aim raycasts consistent.
        private const int SHOOTER_ALL_LAYER_MASK = ~0;
        private const uint SHOOTER_ALL_LAYER_BITS = uint.MaxValue;
        private const float RAYCAST_TRACER_WIDTH_LIGHT = 0.015f;
        private const float RAYCAST_TRACER_WIDTH_RIFLE = 0.0375f;
        private const float KINEMATIC_TRACER_WIDTH = 0.0075f;

        private const string SOURCE_TRACER_MATERIAL =
            SAMPLE_ROOT + "/Materials/Raycast_Trace.mat";
        private const string SOURCE_MUZZLE_MATERIAL =
            SAMPLE_ROOT + "/Effects/Muzzle_Gun/Muzzle_Smoke.mat";
        private const string SOURCE_EXPLOSION_MATERIAL =
            SAMPLE_ROOT + "/Effects/Flames/Flame_Smoke.mat";
        private const string SOURCE_MUZZLE_EFFECT =
            SAMPLE_ROOT + "/Effects/Muzzle_Gun/Muzzle_Flash.prefab";
        private const string SOURCE_EXPLOSION_EFFECT =
            SAMPLE_ROOT + "/Effects/Hits/Hit_Grenade.prefab";
        private const string SOURCE_IMPACT_MATERIAL =
            SAMPLE_ROOT + "/Effects/Hits/Hit_Smoke.mat";
        private const string SOURCE_IMPACT_EFFECT =
            SAMPLE_ROOT + "/Effects/Hits/Hit_Gun.prefab";
        private const string SOURCE_IMPACT_MATERIAL_SOUNDS =
            SAMPLE_ROOT + "/MaterialSounds/Shooter_MaterialSounds.asset";
        private const string SOURCE_BLOOD_HIT_EFFECT =
            "Assets/PampelGames/BloodFactory/Content/Prefabs/Splash/BloodSplash01.prefab";
        private const string SOURCE_SNIPER_PROJECTILE =
            SAMPLE_ROOT + "/Prefabs/Sniper_Projectile.prefab";
        private const string SOURCE_GRENADE_SIGHT =
            SAMPLE_ROOT + "/Sights/Grenade_Sight.asset";
        private const string SOURCE_GRENADE_EXPLOSION_AUDIO =
            SAMPLE_ROOT + "/Audio/Grenade_Explode.wav";
        private const string SOURCE_SMOKE_HISS_AUDIO =
            SAMPLE_ROOT + "/Audio/Flamethrower_Gas.wav";
        private const float BLOOD_HIT_EFFECT_LIFETIME = 2f;
        private const int MUZZLE_POOL_SIZE = 4;
        private const float MUZZLE_POOL_DURATION = 0.2f;
        private const int SHELL_POOL_SIZE = 12;
        private const float SHELL_POOL_DURATION = 0.75f;
        private const int SURFACE_IMPACT_POOL_SIZE = 8;
        private const float SURFACE_IMPACT_POOL_DURATION = 0.75f;
        private const int EXPLOSION_IMPACT_POOL_SIZE = 2;
        private const float EXPLOSION_IMPACT_POOL_DURATION = 5f;
        private const float RAYCAST_TRACER_DURATION = 0.1f;
        private const float AUTOMATIC_CAMERA_SHAKE_DURATION = 0.1f;
        private const int IMPACT_PARTICLE_BUDGET = 32;
        private const int MUZZLE_SMOKE_PARTICLE_BUDGET = 16;
        private const int MUZZLE_FLASH_PARTICLE_BUDGET = 32;
        private const int EXPLOSION_FIREBALL_PARTICLE_BUDGET = 64;
        private const int EXPLOSION_SMOKE_PARTICLE_BUDGET = 128;

        private const string URP_LIT_SHADER = "Universal Render Pipeline/Lit";
        private const string URP_UNLIT_SHADER = "Universal Render Pipeline/Unlit";
        private const string URP_PARTICLE_SHADER =
            "Universal Render Pipeline/Particles/Unlit";

        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class Definition
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string Category;
            public readonly string SourceWeapon;
            public readonly string LowPrefab;
            public readonly string Icon;
            public readonly int StartingMagazine;
            public readonly Vector3 Position;
            public readonly Vector3 Rotation;
            public readonly Vector3 Scale;
            public readonly Vector2 RecoilX;
            public readonly Vector2 RecoilY;
            public readonly float VehicleDamage;
            public readonly float CharacterDamage;
            public readonly float HeadshotMultiplier;
            public readonly float HelmetMultiplier;
            public readonly float HelmetImpulse;
            public readonly float ArmorAbsorption;
            public readonly bool UseMuzzleEffect;
            public readonly FranklinShooterCatalog.WeaponKind Kind;

            public Definition(
                string id, string displayName, string category,
                string sourceWeapon, string lowPrefab, string icon,
                int startingMagazine,
                Vector3? position = null, Vector3? rotation = null, Vector3? scale = null,
                Vector2? recoilX = null, Vector2? recoilY = null,
                float vehicleDamage = 8f,
                float characterDamage = 25f,
                float headshotMultiplier = 3f,
                float helmetMultiplier = 0.25f,
                float helmetImpulse = 8f,
                float armorAbsorption = 0.65f,
                bool useMuzzleEffect = true,
                FranklinShooterCatalog.WeaponKind kind =
                    FranklinShooterCatalog.WeaponKind.Firearm)
            {
                this.Id = id;
                this.DisplayName = displayName;
                this.Category = category;
                this.SourceWeapon = sourceWeapon;
                this.LowPrefab = lowPrefab;
                this.Icon = icon;
                this.StartingMagazine = startingMagazine;
                this.Position = position ?? new Vector3(-0.04f, 0.09f, 0.04f);
                this.Rotation = rotation ?? new Vector3(-90f, 0f, 90f);
                this.Scale = scale ?? Vector3.one;
                this.RecoilX = recoilX ?? new Vector2(-0.5f, 0.5f);
                this.RecoilY = recoilY ?? new Vector2(0.5f, 1f);
                this.VehicleDamage = Mathf.Max(0f, vehicleDamage);
                this.CharacterDamage = Mathf.Max(0f, characterDamage);
                this.HeadshotMultiplier = Mathf.Max(1f, headshotMultiplier);
                this.HelmetMultiplier = Mathf.Clamp01(helmetMultiplier);
                this.HelmetImpulse = Mathf.Max(0f, helmetImpulse);
                this.ArmorAbsorption = Mathf.Clamp01(armorAbsorption);
                this.UseMuzzleEffect = useMuzzleEffect;
                this.Kind = kind;
            }
        }

        private sealed class UrpRenderingAssets
        {
            public Material Tracer;
            public GameObject MuzzleEffect;
            public GameObject ImpactEffect;
            public GameObject ExplosionEffect;
            public GameObject SourceExplosionEffect;
            public GameObject BloodHitEffect;
        }

        private static readonly Definition[] DEFINITIONS =
        {
            new("m1911", "M1911", "Pistol", "Pistol", "M1911", "m1911", 10,
                vehicleDamage: 4.8f, characterDamage: 25f,
                headshotMultiplier: 3f, helmetImpulse: 7f, armorAbsorption: 0.8f),
            new("uzi", "UZI", "Submachine Gun", "AK", "Uzi", "uzi", 30,
                vehicleDamage: 2f, characterDamage: 12f,
                headshotMultiplier: 3f, helmetImpulse: 6f, armorAbsorption: 0.65f),
            new("ak74", "AK-74", "Assault Rifle", "AK", "AK74", "ak74", 30,
                vehicleDamage: 3.2f, characterDamage: 30f,
                headshotMultiplier: 3.5f, helmetImpulse: 9f, armorAbsorption: 0.5f),
            new("m4", "M4 Carbine", "Assault Rifle", "AK", "M4_8", "m4", 30,
                vehicleDamage: 3.2f, characterDamage: 28f,
                headshotMultiplier: 3.75f, helmetImpulse: 9f, armorAbsorption: 0.48f),
            new("benelli-m4", "Benelli M4", "Shotgun", "Shotgun", "Bennelli_M4", "benelli-m4", 8,
                recoilY: new Vector2(5f, 7.5f), vehicleDamage: 1.2f,
                characterDamage: 14f, headshotMultiplier: 2f, helmetImpulse: 12f,
                armorAbsorption: 0.7f),
            new("m249", "M249", "Light Machine Gun", "AK", "M249", "m249", 30,
                vehicleDamage: 2.4f, characterDamage: 22f,
                headshotMultiplier: 4.5f, helmetImpulse: 10f, armorAbsorption: 0.45f),
            new("m107", "M107", "Sniper Rifle", "Sniper", "M107", "m107", 5,
                recoilX: new Vector2(-2.5f, 2.5f), recoilY: new Vector2(-2.5f, 2.5f),
                vehicleDamage: 14f, characterDamage: 100f,
                headshotMultiplier: 2f, helmetImpulse: 18f, armorAbsorption: 0.2f),
            new("rpg7", "RPG-7", "Heavy Weapon", "Grenade", "RPG7", "rpg7", 5,
                new Vector3(-0.06f, 0.12f, 0.16f), new Vector3(-90f, 0f, 90f),
                recoilY: new Vector2(1f, 2f), vehicleDamage: 60f,
                characterDamage: 250f, headshotMultiplier: 2f, helmetImpulse: 20f,
                armorAbsorption: 0.1f, useMuzzleEffect: false),
            new("rgd5", "RGD-5", "Explosive", "Grenade", "RGD-5", "rgd5", 3,
                new Vector3(-0.025f, 0.075f, 0.025f), new Vector3(-90f, 0f, 90f),
                recoilX: Vector2.zero, recoilY: Vector2.zero,
                vehicleDamage: 18f, characterDamage: 100f,
                headshotMultiplier: 1f, helmetImpulse: 0f, armorAbsorption: 0.25f,
                useMuzzleEffect: false,
                kind: FranklinShooterCatalog.WeaponKind.ExplosiveGrenade),
            new("smoke", "Smoke Grenade", "Tactical", "Grenade", "Smoke", "smoke", 2,
                new Vector3(-0.025f, 0.075f, 0.025f), new Vector3(-90f, 0f, 90f),
                recoilX: Vector2.zero, recoilY: Vector2.zero,
                vehicleDamage: 0f, characterDamage: 0f,
                headshotMultiplier: 1f, helmetImpulse: 0f, armorAbsorption: 0f,
                useMuzzleEffect: false,
                kind: FranklinShooterCatalog.WeaponKind.SmokeGrenade),
            new("flash", "Flash Grenade", "Tactical", "Grenade", "Flash", "flash", 2,
                new Vector3(-0.025f, 0.075f, 0.025f), new Vector3(-90f, 0f, 90f),
                recoilX: Vector2.zero, recoilY: Vector2.zero,
                vehicleDamage: 0f, characterDamage: 0f,
                headshotMultiplier: 1f, helmetImpulse: 0f, armorAbsorption: 0f,
                useMuzzleEffect: false,
                kind: FranklinShooterCatalog.WeaponKind.FlashGrenade)
        };

        static FranklinShooterInstaller()
        {
            EditorApplication.delayCall += AutoInstallWhenNeeded;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Franklin Game/Shooter System GC2/Install or Repair", false, 1510)]
        private static void InstallOrRepairMenu()
        {
            InstallOrRepair(true);
        }

        [MenuItem("Tools/Franklin Game/Shooter System GC2/Select Catalog", false, 1511)]
        private static void SelectCatalog()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<FranklinShooterCatalog>(CATALOG_PATH);
        }

        private static void AutoInstallWhenNeeded()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += AutoInstallWhenNeeded;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!NeedsLocalMainAssetNameRepair() &&
                !NeedsCatalogRepair() &&
                !NeedsUiSpriteRepair() &&
                !NeedsRenderingRepair() &&
                !NeedsShooterLocomotionRepair() &&
                !NeedsShooterDamageRepair() &&
                !NeedsShooterLayerMaskRepair() &&
                !NeedsImpactAudioRepair() &&
                !NeedsCharacterImpactFilterRepair() &&
                !NeedsTracerVisualRepair() &&
                !NeedsPlayerArmorRepair() &&
                !NeedsPlayerFirstPersonCameraRepair() &&
                !NeedsVehicleBulletDecalRepair() &&
                !NeedsVehicleImpactForceRepair() &&
                !NeedsAimSightRepair() &&
                !NeedsRpgRepair() &&
                !NeedsThrowableRepair()) return;
            InstallOrRepair(false);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += AutoInstallWhenNeeded;
        }

        private static void InstallOrRepair(bool logSuccess)
        {
            try
            {
                EnsureFolders();
                ConfigureRpgAudioImporters();
                RepairLocalMainAssetNames();
                EnsurePlayerArmor();
                EnsurePlayerFirstPersonCameraManager();
                RepairShooterSampleMaterialsForUrp();
                MaterialSoundsAsset impactAudioOnly =
                    CreateOrRepairImpactAudioOnly();
                ConfigureUiSprites();
                CreateOrRepairVehicleBulletDecalAssets();
                AvatarMask upperBodyMask = CreateOrRepairUpperBodyMask();
                StateBasicLocomotion shooterLocomotion =
                    CreateOrRepairShooterLocomotion();
                CreateOrRepairBikeDriverMask();
                AvatarMask bikeDriverShooterMask =
                    CreateOrRepairBikeDriverShooterMask();
                RepairSniperProjectileTracer();
                UrpRenderingAssets renderingAssets = CreateOrRepairUrpRenderingAssets();

                FranklinShooterCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<FranklinShooterCatalog>(CATALOG_PATH);
                Dictionary<string, FranklinShooterCatalog.Entry> existingEntries =
                    new(StringComparer.OrdinalIgnoreCase);
                if (catalog != null)
                {
                    foreach (FranklinShooterCatalog.Entry existingEntry in catalog.Weapons)
                    {
                        if (existingEntry == null || string.IsNullOrWhiteSpace(existingEntry.Id))
                            continue;
                        existingEntries[existingEntry.Id] = existingEntry;
                    }
                }

                List<FranklinShooterCatalog.Entry> entries = new(DEFINITIONS.Length);
                foreach (Definition definition in DEFINITIONS)
                {
                    ShooterWeapon weapon = CreateOrRepairWeapon(
                        definition,
                        shooterLocomotion,
                        upperBodyMask,
                        bikeDriverShooterMask,
                        renderingAssets,
                        impactAudioOnly
                    );
                    GameObject prop = AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"{LOW_PREFABS}/{definition.LowPrefab}.prefab"
                    );
                    Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(
                        $"{RESOURCE_ROOT}/UI/Weapons/{definition.Icon}.png"
                    );

                    if (weapon == null || prop == null || icon == null)
                    {
                        throw new InvalidOperationException(
                            $"Missing generated shooter dependency for {definition.DisplayName}: " +
                            $"weapon={weapon != null}, prop={prop != null}, icon={icon != null}"
                        );
                    }

                    FranklinShooterCatalog.Entry entry = new();
                    SetField(entry, "m_Id", definition.Id);
                    SetField(entry, "m_DisplayName", definition.DisplayName);
                    SetField(entry, "m_Category", definition.Category);
                    SetField(entry, "m_Weapon", weapon);
                    SetField(entry, "m_PropPrefab", prop);
                    SetField(entry, "m_Icon", icon);
                    SetField(entry, "m_StartingMagazine", definition.StartingMagazine);
                    SetField(entry, "m_Kind", definition.Kind);
                    if (existingEntries.TryGetValue(definition.Id, out FranklinShooterCatalog.Entry saved))
                    {
                        SetField(entry, "m_LocalPosition", saved.LocalPosition);
                        SetField(entry, "m_LocalRotation", saved.LocalEulerAngles);
                        SetField(entry, "m_LocalScale", saved.LocalScale);
                        SetField(entry, "m_ModelLocalPosition", saved.ModelLocalPosition);
                        SetField(entry, "m_ModelLocalRotation", saved.ModelLocalEulerAngles);
                        SetField(entry, "m_ModelLocalScale", saved.ModelLocalScale);
                    }
                    else
                    {
                        SetField(entry, "m_LocalPosition", definition.Position);
                        SetField(entry, "m_LocalRotation", definition.Rotation);
                        SetField(entry, "m_LocalScale", definition.Scale);
                        SetField(entry, "m_ModelLocalPosition", Vector3.zero);
                        SetField(entry, "m_ModelLocalRotation", Vector3.zero);
                        SetField(entry, "m_ModelLocalScale", Vector3.one);
                    }
                    entries.Add(entry);
                }

                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<FranklinShooterCatalog>();
                    catalog.name = GetAssetMainName(CATALOG_PATH);
                    AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
                }

                SetField(catalog, "m_Weapons", entries.ToArray());
                SetField(catalog, "m_DefaultWeaponIndex", 0);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (logSuccess)
                {
                    Debug.Log(
                        "Franklin Shooter GC2 installed: 11 Weapons Low props, GC2 weapon assets, " +
                        "ImageGen weapon menu and mobile shooter controls."
                    );
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Franklin Shooter GC2 installation failed: {exception}");
            }
        }

        private static ShooterWeapon CreateOrRepairWeapon(
            Definition definition,
            StateBasicLocomotion shooterLocomotion,
            AvatarMask upperBodyMask,
            AvatarMask bikeDriverShooterMask,
            UrpRenderingAssets renderingAssets,
            MaterialSoundsAsset impactAudioOnly)
        {
            string sourcePath = $"{SAMPLE_ROOT}/Weapons/{definition.SourceWeapon}_Weapon.asset";
            string destinationPath = $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset";
            ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(destinationPath);
            bool created = false;

            if (weapon == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    throw new InvalidOperationException($"Could not copy GC2 weapon template: {sourcePath}");
                weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(destinationPath);
                created = true;
            }

            if (weapon == null)
                throw new InvalidOperationException($"Could not load generated weapon: {destinationPath}");

            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{RESOURCE_ROOT}/UI/Weapons/{definition.Icon}.png"
            );
            SetBaseField(weapon, "m_Title", new PropertyGetString(definition.DisplayName));
            SetBaseField(
                weapon,
                "m_Description",
                new PropertyGetString($"{definition.Category} using the Weapons Low {definition.DisplayName} model.")
            );
            SetBaseField(weapon, "m_Icon", new PropertyGetSprite(icon));

            if (created)
            {
                SetBaseField(weapon, "m_Id", new UniqueID());
            }

            bool isRpg = string.Equals(
                definition.Id,
                "rpg7",
                StringComparison.OrdinalIgnoreCase
            );

            // Regular firearms keep the exact GC2 Shooter_Locomotion dependency.
            // RPG-7 and throwables stay on the normal Character locomotion when merely
            // equipped; their layer-7/8 pose and biomechanics begin on Fire hold only.
            SetField(
                weapon,
                "m_State",
                definition.Kind == FranklinShooterCatalog.WeaponKind.Firearm && !isRpg
                    ? new StateData(shooterLocomotion)
                    : new StateData(StateData.StateType.State)
            );
            SetField(
                weapon,
                "m_Layer",
                new PropertyGetInteger(SHOOTER_LOCOMOTION_LAYER)
            );
            if (isRpg)
                ConfigureRpgSightsAndFire(weapon, upperBodyMask);
            else
                RepairAimAdsSightReference(weapon, definition.SourceWeapon, upperBodyMask);
            RepairBikeDriverAimSight(weapon, definition, bikeDriverShooterMask);
            RepairWeaponSightLayerMasks(weapon);
            SetField(
                weapon.Fire,
                "m_FireAvatarMask",
                definition.Kind == FranklinShooterCatalog.WeaponKind.Firearm
                    ? upperBodyMask
                    : bikeDriverShooterMask
            );
            // GC2's Fire Force applies a Rigidbody impulse before on-hit instructions.
            // Vehicle damage is health-only, so disable that impulse on generated weapons.
            SetField(weapon.Fire, "m_Force", new EnablerFloat(false, 0f));
            if (isRpg)
            {
                ConfigureRpgProjectileAndDamage(
                    weapon,
                    definition,
                    renderingAssets
                );
            }
            else if (definition.Kind == FranklinShooterCatalog.WeaponKind.Firearm)
            {
                SetDamageInstructions(weapon, definition, renderingAssets.BloodHitEffect);
            }
            else
            {
                ConfigureThrowableWeapon(weapon, definition, renderingAssets);
            }
            ConfigureAutomaticCameraShake(weapon, definition);
            RepairProjectileLayerMask(weapon);

            // ShotRaycast invokes MaterialSounds independently from m_ImpactEffect. The
            // GC2 sample MaterialSounds also spawns Hit_Gun, which duplicates Franklin's
            // pooled impact/decal and leaves an old transparent quad on the surface. Keep
            // the authored impact audio but route visual impacts exclusively through the
            // Franklin effect. Grenade/RPG intentionally preserves its source null value.
            SetField(
                weapon.Projectile,
                "m_ImpactSound",
                definition.SourceWeapon == "Grenade" ? null : impactAudioOnly
            );

            if (definition.UseMuzzleEffect && renderingAssets.MuzzleEffect != null)
            {
                SetField(
                    weapon.Fire,
                    "m_MuzzleEffect",
                    CreateInstantiateReference(
                        renderingAssets.MuzzleEffect,
                        true,
                        MUZZLE_POOL_SIZE,
                        true,
                        MUZZLE_POOL_DURATION
                    )
                );
            }

            if (definition.SourceWeapon != "Grenade" &&
                renderingAssets.ImpactEffect != null)
            {
                SetField(
                    weapon.Projectile,
                    "m_ImpactEffect",
                    CreateSurfaceImpactReference(
                        renderingAssets.ImpactEffect,
                        true,
                        SURFACE_IMPACT_POOL_SIZE,
                        true,
                        SURFACE_IMPACT_POOL_DURATION
                    )
                );
            }

            PropertyGetInstantiate shellEffect = (PropertyGetInstantiate) GetField(
                weapon.Shell,
                "m_Prefab"
            );
            if (shellEffect != null)
            {
                shellEffect.usePooling = true;
                shellEffect.size = SHELL_POOL_SIZE;
                shellEffect.hasDuration = true;
                shellEffect.duration = SHELL_POOL_DURATION;
            }

            Shot projectileShot = (Shot) GetField(weapon.Projectile, "m_Shot");
            if (projectileShot?.Value is ShotRaycast raycast)
            {
                SetField(
                    raycast,
                    "m_Duration",
                    new PropertyGetDecimal(RAYCAST_TRACER_DURATION)
                );
                SetField(raycast, "m_Color", new PropertyGetColor(Color.white));
                SetField(
                    raycast,
                    "m_Width",
                    new PropertyGetDecimal(
                        definition.SourceWeapon == "AK"
                            ? RAYCAST_TRACER_WIDTH_RIFLE
                            : RAYCAST_TRACER_WIDTH_LIGHT
                    )
                );

                if (GetField(raycast, "m_LineMaterial") is PropertyGetMaterial lineMaterial &&
                    lineMaterial.EditorValue != null)
                {
                    SetField(
                        raycast,
                        "m_LineMaterial",
                        new PropertyGetMaterial(renderingAssets.Tracer)
                    );
                }
            }

            SetField(
                weapon.Recoil,
                "m_RecoilX",
                GetDecimalRandomRange.Create(
                    new PropertyGetDecimal(definition.RecoilX.x),
                    new PropertyGetDecimal(definition.RecoilX.y)
                )
            );
            SetField(
                weapon.Recoil,
                "m_RecoilY",
                GetDecimalRandomRange.Create(
                    new PropertyGetDecimal(definition.RecoilY.x),
                    new PropertyGetDecimal(definition.RecoilY.y)
                )
            );
            weapon.EditorModelPath = $"{LOW_PREFABS}/{definition.LowPrefab}.prefab";
            weapon.name = GetAssetMainName(destinationPath);
            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static void ConfigureThrowableWeapon(
            ShooterWeapon weapon,
            Definition definition,
            UrpRenderingAssets renderingAssets)
        {
            ConfigureThrowableSights(weapon);

            Ammo ammo = CreateOrRepairThrowableAmmo(weapon, definition);
            SetField(weapon.Magazine, "m_Ammo", ammo);

            GameObject projectile = CreateOrRepairThrowableProjectile(definition);
            Shot shot = (Shot) GetField(weapon.Projectile, "m_Shot");
            if (shot?.Value is not ShotRigidbody rigidbodyShot)
            {
                throw new InvalidOperationException(
                    $"Throwable {definition.DisplayName} does not use GC2 ShotRigidbody"
                );
            }

            SetBaseField(
                rigidbodyShot,
                "m_Prefab",
                GetGameObjectInstance.Create(projectile)
            );
            SetField(rigidbodyShot, "m_Timeout", new PropertyGetDecimal(3f));

            AudioClip explosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                SOURCE_GRENADE_EXPLOSION_AUDIO
            );
            AudioClip smokeHissClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                SOURCE_SMOKE_HISS_AUDIO
            );

            if (definition.Kind == FranklinShooterCatalog.WeaponKind.ExplosiveGrenade)
            {
                SetField(
                    weapon,
                    "m_OnHit",
                    new RunInstructionsList(
                        new InstructionFranklinExplosionDamage(
                            4f,
                            definition.CharacterDamage,
                            definition.VehicleDamage,
                            0.35f,
                            definition.ArmorAbsorption,
                            3.2f,
                            0.35f,
                            1.45f,
                            explosionClip
                        )
                    )
                );
                SetField(
                    weapon.Projectile,
                    "m_ImpactEffect",
                    CreateInstantiateReference(
                        renderingAssets.ExplosionEffect,
                        true,
                        EXPLOSION_IMPACT_POOL_SIZE,
                        true,
                        EXPLOSION_IMPACT_POOL_DURATION
                    )
                );
            }
            else if (definition.Kind == FranklinShooterCatalog.WeaponKind.SmokeGrenade)
            {
                SetField(
                    weapon,
                    "m_OnHit",
                    new RunInstructionsList(
                        new InstructionFranklinSmokeGrenade(
                            18f,
                            7.5f,
                            smokeHissClip
                        )
                    )
                );
                SetField(
                    weapon.Projectile,
                    "m_ImpactEffect",
                    CreateInstantiateReference(null, false, 0, false, 0f)
                );
            }
            else
            {
                SetField(
                    weapon,
                    "m_OnHit",
                    new RunInstructionsList(
                        new InstructionFranklinFlashGrenade(
                            0.9f,
                            12f,
                            explosionClip
                        )
                    )
                );
                SetField(
                    weapon.Projectile,
                    "m_ImpactEffect",
                    CreateInstantiateReference(null, false, 0, false, 0f)
                );
            }
        }

        private static void ConfigureRpgSightsAndFire(
            ShooterWeapon weapon,
            AvatarMask upperBodyMask)
        {
            Sight source = AssetDatabase.LoadAssetAtPath<Sight>(SOURCE_GRENADE_SIGHT);
            AnimationClip holdClip = LoadAnimationClip(RPG_HOLD_ANIMATION_PATH);
            AnimationClip aimClip = LoadAnimationClip(RPG_AIM_ANIMATION_PATH);
            AnimationClip shootClip = LoadAnimationClip(RPG_SHOOT_ANIMATION_PATH);
            if (source == null || holdClip == null || aimClip == null || shootClip == null)
            {
                throw new InvalidOperationException(
                    "RPG-7 requires the local Grenade Sight and Bazooka Hold/Aim/Shoot clips"
                );
            }

            Sight idleSight = CreateOrRepairRpgSight(
                source,
                RPG_IDLE_SIGHT_PATH,
                holdClip,
                upperBodyMask,
                false
            );
            Sight aimSight = CreateOrRepairRpgSight(
                source,
                RPG_AIM_SIGHT_PATH,
                aimClip,
                upperBodyMask,
                true
            );

            SightItem idleItem = new();
            SetField(idleItem, "m_Id", new IdString("idle"));
            SetField(idleItem, "m_Sight", idleSight);
            SetField(idleItem, "m_ScopeThrough", false);
            SetBaseField(idleItem, "m_IsEnabled", true);

            SightItem aimItem = new();
            SetField(aimItem, "m_Id", new IdString("aim-ads"));
            SetField(aimItem, "m_Sight", aimSight);
            SetField(aimItem, "m_ScopeThrough", false);
            SetBaseField(aimItem, "m_IsEnabled", true);
            SetField(weapon.Sights, "m_Sights", new[] { idleItem, aimItem });

            // RPG is a single, camera-centred projectile. Do not inherit the random
            // grenade/firearm spread that can move a rocket metres away from the reticle
            // at long range or while the Character is moving.
            SetField(weapon.Accuracy, "m_MaxSpreadX", new PropertyGetDecimal(0f));
            SetField(weapon.Accuracy, "m_MaxSpreadY", new PropertyGetDecimal(0f));
            SetField(weapon.Accuracy, "m_MotionAccuracy", new PropertyGetDecimal(0f));
            SetField(weapon.Accuracy, "m_AirborneAccuracy", new PropertyGetDecimal(0f));
            SetField(weapon.Accuracy, "m_AccuracyKick", new PropertyGetDecimal(0f));

            SetField(
                weapon.Fire,
                "m_FireAnimation",
                new PropertyGetAnimation(new GetAnimationInstance(shootClip))
            );
            SetField(weapon.Fire, "m_FireAvatarMask", upperBodyMask);
            SetField(weapon.Fire, "m_Mode", ShootMode.Charge);
            // A launcher is deliberately slow even if a future ammo pickup grants more
            // than the authored single rocket. This also bounds the GC2 projectile pool.
            SetField(weapon.Fire, "m_FireRate", new PropertyGetDecimal(0.5f));
            SetField(weapon.Fire, "m_MinChargeTime", new PropertyGetDecimal(0f));
            SetField(weapon.Fire, "m_MaxChargeTime", new PropertyGetDecimal(1f));
            SetField(weapon.Fire, "m_AutoRelease", new PropertyGetBool(false));
            // The pooled projectile component plays the launch transient at the exact
            // muzzle position with an authored long-distance 3D falloff.
            SetField(weapon.Fire, "m_FireAudio", GetAudioNone.Create);
            SetField(weapon.Fire, "m_TransitionIn", 0.08f);
            SetField(weapon.Fire, "m_TransitionOut", 0.2f);
            SetField(weapon.Fire, "m_RootMotion", false);
            EditorUtility.SetDirty(weapon);
        }

        private static Sight CreateOrRepairRpgSight(
            Sight source,
            string path,
            AnimationClip pose,
            AvatarMask upperBodyMask,
            bool aiming)
        {
            Sight sight = AssetDatabase.LoadAssetAtPath<Sight>(path);
            if (sight == null)
            {
                if (!AssetDatabase.CopyAsset(SOURCE_GRENADE_SIGHT, path))
                    throw new InvalidOperationException($"Could not create RPG Sight: {path}");
                sight = AssetDatabase.LoadAssetAtPath<Sight>(path);
            }
            if (sight == null)
                throw new InvalidOperationException($"Could not load RPG Sight: {path}");

            // The asset is already cloned from SOURCE_GRENADE_SIGHT when it is created.
            // CopySerialized is unsafe here because Sight contains several SerializeReference
            // graphs; Unity 6 can crash in ManagedReferencesTransferState.PerformFixups when
            // copying those graphs between persistent ScriptableObjects. Repair only the
            // fields Franklin owns below so the local asset keeps its GUID and remains valid.
            sight.name = GetAssetMainName(path);
            SetField(
                sight,
                "m_State",
                pose != null
                    ? new StateData(pose, upperBodyMask)
                    : new StateData(StateData.StateType.State)
            );
            SetField(sight, "m_Layer", new PropertyGetInteger(8));
            SetField(sight, "m_SmoothTime", new EnablerFloat(false, 0f));
            SetField(sight.Trajectory, "m_UseTrajectory", new PropertyGetBool(false));
            SetField(sight, "m_ShootingUsesFK", aiming);
            SetField(sight, "m_ShootingUsesIK", aiming);

            if (aiming)
            {
                // Use the same native GC2 camera-centre raycast used by the firearm ADS
                // sights. The all-layer mask is normalized by RepairWeaponSightLayerMasks.
                Aim aim = new();
                SetField(aim, "m_Aim", new AimCameraRaycast());
                SetField(sight, "m_Aim", aim);
            }
            else
            {
                Biomechanics biomechanics = new();
                SetField(biomechanics, "m_Value", new BiomechanicsNone());
                SetField(sight, "m_Biomechanics", biomechanics);
            }

            EditorUtility.SetDirty(sight);
            return sight;
        }

        private static void ConfigureRpgProjectileAndDamage(
            ShooterWeapon weapon,
            Definition definition,
            UrpRenderingAssets renderingAssets)
        {
            // The GC2 Grenade template uses infinite ammo. RPG-7 owns a finite local
            // reserve instead. The launcher still fires one rocket at a time, while the
            // starting reserve provides enough shots for normal gameplay.
            Ammo ammo = CreateOrRepairThrowableAmmo(weapon, definition);
            SetField(weapon.Magazine, "m_Ammo", ammo);
            SetField(weapon.Magazine, "m_HasMagazine", new PropertyGetBool(false));
            SetField(weapon.Magazine, "m_MagazineSize", new PropertyGetInteger(1));
            SetField(weapon.Magazine, "m_AutoReload", new PropertyGetBool(false));

            AudioClip launchClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                RPG_LAUNCH_AUDIO_PATH
            );
            AudioClip flightLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(
                RPG_FLIGHT_AUDIO_PATH
            );
            AudioClip explosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                RPG_EXPLOSION_CLOSE_AUDIO_PATH
            );
            AudioClip distantExplosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                RPG_EXPLOSION_DISTANT_AUDIO_PATH
            );
            if (launchClip == null || flightLoop == null || explosionClip == null ||
                distantExplosionClip == null)
            {
                throw new InvalidOperationException(
                    "RPG-7 CC0 launch, flight and explosion audio assets are required"
                );
            }

            GameObject projectile = CreateOrRepairRpgProjectile(
                renderingAssets.Tracer,
                launchClip,
                flightLoop
            );
            Shot shot = (Shot) GetField(weapon.Projectile, "m_Shot");
            if (shot?.Value is not ShotRigidbody rigidbodyShot)
                throw new InvalidOperationException("RPG-7 must use GC2 ShotRigidbody");

            SetBaseField(
                rigidbodyShot,
                "m_Prefab",
                GetGameObjectInstance.Create(projectile)
            );
            // Ignore projectile mass so every pooled rocket leaves the tube at
            // the same authored speed regardless of future Rigidbody tuning.
            SetEnumField(rigidbodyShot, "m_Impulse", 2);
            SetField(rigidbodyShot, "m_ImpulseForce", new PropertyGetDecimal(42f));
            SetField(rigidbodyShot, "m_Mass", new PropertyGetDecimal(1f));
            SetField(rigidbodyShot, "m_AirResistance", new PropertyGetDecimal(0f));
            SetField(rigidbodyShot, "m_WindInfluence", new PropertyGetDecimal(0f));
            SetField(rigidbodyShot, "m_AttractionForce", new PropertyGetDecimal(0f));
            SetField(rigidbodyShot, "m_MaxDistance", new PropertyGetDecimal(150f));
            SetEnumField(rigidbodyShot, "m_Hit", 0); // ShotRigidbody.HitMode.OnImpact
            SetField(rigidbodyShot, "m_Timeout", new PropertyGetDecimal(4f));
            SetBaseField(rigidbodyShot, "m_AimAtSightPoint", true);

            GameObject prop = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{LOW_PREFABS}/{definition.LowPrefab}.prefab"
            );
            Vector3 muzzlePosition = GetRpgMuzzlePosition(prop);
            SetField(weapon.Muzzle, "m_Position", muzzlePosition);
            // Weapons Low authors the rocket nose along local -Z.
            SetField(weapon.Muzzle, "m_Rotation", new Vector3(0f, 180f, 0f));

            SetField(
                weapon,
                "m_OnHit",
                new RunInstructionsList(
                    new InstructionFranklinExplosionDamage(
                        5f,
                        definition.CharacterDamage,
                        definition.VehicleDamage,
                        0.3f,
                        definition.ArmorAbsorption,
                        3.8f,
                        0.35f,
                        1.5f,
                        explosionClip,
                        distantExplosionClip,
                        110f,
                        320f,
                        true
                    )
                )
            );
            SetField(
                weapon.Projectile,
                "m_ImpactEffect",
                CreateInstantiateReference(
                    renderingAssets.ExplosionEffect,
                    true,
                    EXPLOSION_IMPACT_POOL_SIZE,
                    true,
                    EXPLOSION_IMPACT_POOL_DURATION
                )
            );
            SetField(weapon.Projectile, "m_ImpactSound", null);
            EditorUtility.SetDirty(weapon);
        }

        private static GameObject CreateOrRepairRpgProjectile(
            Material trailMaterial,
            AudioClip launchClip,
            AudioClip flightLoop)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                LOW_PREFABS + "/RPG7.prefab"
            );
            Transform sourceRocket = source != null ? source.transform.Find("Rocket") : null;
            MeshFilter sourceFilter = sourceRocket != null
                ? sourceRocket.GetComponent<MeshFilter>()
                : null;
            MeshRenderer sourceRenderer = sourceRocket != null
                ? sourceRocket.GetComponent<MeshRenderer>()
                : null;
            if (sourceFilter == null || sourceFilter.sharedMesh == null ||
                sourceRenderer == null)
                throw new InvalidOperationException("Weapons Low RPG7/Rocket mesh is missing");

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(RPG_PROJECTILE_PATH) != null;
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(RPG_PROJECTILE_PATH)
                : new GameObject("RPG-7 Rocket Projectile");
            try
            {
                root.name = "RPG-7 Rocket Projectile";
                for (int i = root.transform.childCount - 1; i >= 0; --i)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                foreach (Component component in root.GetComponents<Component>())
                {
                    if (component is Transform or Rigidbody or CapsuleCollider or Bullet or
                        AudioSource or FranklinRpgFlightAudio)
                        continue;
                    UnityEngine.Object.DestroyImmediate(component);
                }

                // Do not use ?? with UnityEngine.Object. GetComponent can return Unity's
                // destroyed/missing-object sentinel, which is only null through its overloaded
                // equality operator; ?? would keep that invalid wrapper and throw below.
                Rigidbody body = root.GetComponent<Rigidbody>();
                if (body == null) body = root.AddComponent<Rigidbody>();
                if (body == null)
                    throw new InvalidOperationException("Could not create RPG Rigidbody");
                body.mass = 1f;
                body.useGravity = false;
                body.linearDamping = 0f;
                body.angularDamping = 0f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.constraints = RigidbodyConstraints.FreezeRotation;

                Bullet bullet = root.GetComponent<Bullet>();
                if (bullet == null) bullet = root.AddComponent<Bullet>();
                if (bullet == null)
                    throw new InvalidOperationException("Could not create RPG Bullet");

                AudioSource flightSource = root.GetComponent<AudioSource>();
                if (flightSource == null) flightSource = root.AddComponent<AudioSource>();
                if (flightSource == null)
                    throw new InvalidOperationException("Could not create RPG flight AudioSource");
                FranklinRpgFlightAudio flightAudio =
                    root.GetComponent<FranklinRpgFlightAudio>();
                if (flightAudio == null)
                    flightAudio = root.AddComponent<FranklinRpgFlightAudio>();
                if (flightAudio == null)
                    throw new InvalidOperationException("Could not create RPG flight audio");
                flightAudio.Configure(launchClip, flightLoop);

                GameObject visual = new("Rocket");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                visual.transform.localScale = sourceRocket.localScale;
                visual.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                Bounds bounds = sourceFilter.sharedMesh.bounds;
                Vector3 scaledSize = Vector3.Scale(bounds.size, sourceRocket.localScale);
                Vector3 scaledCenter = Vector3.Scale(bounds.center, sourceRocket.localScale);
                Quaternion visualRotation = visual.transform.localRotation;
                Vector3 scaledTail = Vector3.Scale(
                    new Vector3(bounds.center.x, bounds.center.y, bounds.max.z),
                    sourceRocket.localScale
                );
                // The source mesh points its nose down -Z. After the 180 degree turn,
                // normalize its tail to the projectile origin so the entire rocket begins
                // outside the launch tube instead of intersecting the Player/launcher.
                visual.transform.localPosition = -(visualRotation * scaledTail);

                CapsuleCollider collider = root.GetComponent<CapsuleCollider>();
                if (collider == null) collider = root.AddComponent<CapsuleCollider>();
                if (collider == null)
                    throw new InvalidOperationException("Could not create RPG Collider");
                collider.direction = 2;
                collider.center = visual.transform.localPosition + visualRotation * scaledCenter;
                collider.radius = Mathf.Clamp(
                    Mathf.Max(Mathf.Abs(scaledSize.x), Mathf.Abs(scaledSize.y)) * 0.42f,
                    0.035f,
                    0.09f
                );
                collider.height = Mathf.Max(
                    Mathf.Abs(scaledSize.z),
                    collider.radius * 2.1f
                );
                collider.isTrigger = false;

                GameObject exhaust = new("Exhaust Trail");
                exhaust.transform.SetParent(root.transform, false);
                exhaust.transform.localPosition = Vector3.zero;
                TrailRenderer trail = exhaust.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMaterial;
                // Keep a readable exhaust plume without a ParticleSystem. The increased
                // vertex distance offsets the longer/larger trail on mobile GPUs.
                trail.time = 0.55f;
                trail.minVertexDistance = 0.12f;
                trail.widthMultiplier = 0.085f;
                trail.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0.15f),
                    new Keyframe(1f, 1f)
                );
                trail.startColor = new Color(1f, 0.68f, 0.24f, 0.9f);
                trail.endColor = new Color(0.9f, 0.94f, 1f, 0f);
                trail.numCornerVertices = 0;
                trail.numCapVertices = 0;
                trail.textureMode = LineTextureMode.Stretch;
                trail.alignment = LineAlignment.View;
                trail.generateLightingData = false;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                trail.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                trail.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                trail.autodestruct = false;
                trail.emitting = true;

                PrefabUtility.SaveAsPrefabAsset(root, RPG_PROJECTILE_PATH);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(RPG_PROJECTILE_PATH);
        }

        private static Vector3 GetRpgMuzzlePosition(GameObject prop)
        {
            Transform rocket = prop != null ? prop.transform.Find("Rocket") : null;
            MeshFilter filter = rocket != null ? rocket.GetComponent<MeshFilter>() : null;
            MeshFilter launcher = prop != null ? prop.GetComponent<MeshFilter>() : null;
            if (filter?.sharedMesh == null || launcher?.sharedMesh == null)
                return new Vector3(0f, 0f, -0.75f);

            Bounds rocketBounds = filter.sharedMesh.bounds;
            Vector3 rocketAxis = prop.transform.InverseTransformPoint(
                rocket.TransformPoint(
                    new Vector3(rocketBounds.center.x, rocketBounds.center.y, 0f)
                )
            );
            Bounds launcherBounds = launcher.sharedMesh.bounds;
            // Place the normalized rocket tail just beyond the front tube mouth.
            return new Vector3(
                rocketAxis.x,
                rocketAxis.y,
                launcherBounds.min.z - 0.06f
            );
        }

        private static AnimationClip LoadAnimationClip(string path)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            return null;
        }

        private static void ConfigureThrowableSights(ShooterWeapon weapon)
        {
            Sight source = AssetDatabase.LoadAssetAtPath<Sight>(SOURCE_GRENADE_SIGHT);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"GC2 Grenade Sight is missing: {SOURCE_GRENADE_SIGHT}"
                );
            }

            Sight idleSight = CreateOrRepairThrowableSight(
                source,
                THROWABLE_IDLE_SIGHT_PATH,
                false
            );
            Sight throwSight = CreateOrRepairThrowableSight(
                source,
                THROWABLE_THROW_SIGHT_PATH,
                true
            );

            SightItem[] current =
                (SightItem[]) GetField(weapon.Sights, "m_Sights") ??
                Array.Empty<SightItem>();
            SightItem idleItem = null;
            SightItem throwItem = null;
            List<SightItem> preserved = new(current.Length + 1);

            foreach (SightItem item in current)
            {
                if (item == null) continue;
                if (string.Equals(item.Id.String, "idle", StringComparison.Ordinal))
                {
                    idleItem ??= item;
                    continue;
                }
                if (string.Equals(item.Id.String, "throw", StringComparison.Ordinal))
                {
                    throwItem ??= item;
                    continue;
                }
                preserved.Add(item);
            }

            idleItem ??= new SightItem();
            throwItem ??= new SightItem();
            SetField(idleItem, "m_Id", new IdString("idle"));
            SetField(idleItem, "m_Sight", idleSight);
            SetField(throwItem, "m_Id", new IdString("throw"));
            SetField(throwItem, "m_Sight", throwSight);

            List<SightItem> repaired = new(current.Length + 1) { idleItem };
            repaired.AddRange(preserved);
            repaired.Add(throwItem);
            SetField(weapon.Sights, "m_Sights", repaired.ToArray());
        }

        private static Sight CreateOrRepairThrowableSight(
            Sight source,
            string path,
            bool useThrowBiomechanics)
        {
            Sight sight = AssetDatabase.LoadAssetAtPath<Sight>(path);
            if (sight == null)
            {
                if (!AssetDatabase.CopyAsset(SOURCE_GRENADE_SIGHT, path))
                {
                    throw new InvalidOperationException(
                        $"Could not create local throwable Sight: {path}"
                    );
                }

                sight = AssetDatabase.LoadAssetAtPath<Sight>(path);
            }

            if (sight == null)
                throw new InvalidOperationException($"Could not load throwable Sight: {path}");

            // Do not CopySerialized between Sight assets. Sight owns SerializeReference
            // graphs and Unity 6 may crash natively while fixing their managed-reference IDs.
            // New assets are already cloned above; existing assets only need the explicit
            // Franklin-owned fields repaired below.
            sight.name = GetAssetMainName(path);
            SetField(
                sight,
                "m_SmoothTime",
                new EnablerFloat(useThrowBiomechanics, 0.14f)
            );
            if (!useThrowBiomechanics)
            {
                Biomechanics biomechanics = new();
                SetField(biomechanics, "m_Value", new BiomechanicsNone());
                SetField(sight, "m_Biomechanics", biomechanics);
            }
            EditorUtility.SetDirty(sight);
            return sight;
        }

        private static Ammo CreateOrRepairThrowableAmmo(
            ShooterWeapon weapon,
            Definition definition)
        {
            string path = $"{AMMO_ROOT}/{definition.Id}-ammo.asset";
            Ammo ammo = AssetDatabase.LoadAssetAtPath<Ammo>(path);
            if (ammo == null)
            {
                ammo = ScriptableObject.CreateInstance<Ammo>();
                ammo.name = GetAssetMainName(path);
                SetField(ammo, "m_Id", new UniqueID());
                AssetDatabase.CreateAsset(ammo, path);
            }

            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{RESOURCE_ROOT}/UI/Weapons/{definition.Icon}.png"
            );
            SetField(ammo, "m_Title", new PropertyGetString(definition.DisplayName));
            SetField(
                ammo,
                "m_Description",
                new PropertyGetString($"{definition.DisplayName} reserve")
            );
            SetField(ammo, "m_Icon", new PropertyGetSprite(icon));
            SetField(ammo, "m_Infinite", false);

            SetNumberMunition value = new();
            SetField(
                value,
                "m_Weapon",
                GetWeaponShooterInstance.Create(weapon)
            );
            SetField(ammo, "m_Value", new PropertySetNumber(value));
            ammo.name = GetAssetMainName(path);
            EditorUtility.SetDirty(ammo);
            return ammo;
        }

        private static GameObject CreateOrRepairThrowableProjectile(Definition definition)
        {
            string path = $"{PROJECTILE_ROOT}/{definition.Id}-projectile.prefab";
            GameObject lowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{LOW_PREFABS}/{definition.LowPrefab}.prefab"
            );
            if (lowPrefab == null)
                throw new InvalidOperationException($"Missing throwable model: {definition.LowPrefab}");

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(path)
                : new GameObject(definition.DisplayName + " Projectile");
            try
            {
                root.name = definition.DisplayName + " Projectile";
                for (int i = root.transform.childCount - 1; i >= 0; --i)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                Rigidbody body = root.GetComponent<Rigidbody>();
                if (body == null) body = root.AddComponent<Rigidbody>();
                body.mass = 1f;
                body.useGravity = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                SphereCollider collider = root.GetComponent<SphereCollider>();
                if (collider == null) collider = root.AddComponent<SphereCollider>();
                collider.radius = definition.Kind !=
                                  FranklinShooterCatalog.WeaponKind.ExplosiveGrenade
                    ? 0.055f
                    : 0.07f;

                if (root.GetComponent<Bullet>() == null) root.AddComponent<Bullet>();

                GameObject visual = new("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.AddComponent<FranklinThrowableVisualSpin>();
                GameObject model = (GameObject) PrefabUtility.InstantiatePrefab(
                    lowPrefab,
                    visual.transform
                );
                model.name = lowPrefab.name;
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = Vector3.one;

                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void RepairProjectileLayerMask(ShooterWeapon weapon)
        {
            Shot shot = (Shot) GetField(weapon.Projectile, "m_Shot");
            TShot shotType = shot?.Value;
            if (shotType is not ShotRaycast && shotType is not ShotKinematic) return;

            LayerMask layerMask = (LayerMask) GetField(shotType, "m_LayerMask");
            if (layerMask.value != SHOOTER_ALL_LAYER_MASK)
                SetField(shotType, "m_LayerMask", (LayerMask) SHOOTER_ALL_LAYER_MASK);
        }

        private static void RepairSniperProjectileTracer()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                SOURCE_SNIPER_PROJECTILE
            );
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Sniper projectile prefab is missing: {SOURCE_SNIPER_PROJECTILE}"
                );
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(SOURCE_SNIPER_PROJECTILE);
            try
            {
                TrailRenderer[] trails = contents.GetComponentsInChildren<TrailRenderer>(true);
                if (trails.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Sniper projectile has no TrailRenderer: {SOURCE_SNIPER_PROJECTILE}"
                    );
                }

                foreach (TrailRenderer trail in trails)
                {
                    trail.widthMultiplier = KINEMATIC_TRACER_WIDTH;
                    Gradient gradient = trail.colorGradient;
                    gradient.SetKeys(
                        new[]
                        {
                            new GradientColorKey(Color.white, 0f),
                            new GradientColorKey(Color.white, 1f)
                        },
                        gradient.alphaKeys
                    );
                    trail.colorGradient = gradient;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, SOURCE_SNIPER_PROJECTILE);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void RepairAimAdsSightReference(
            ShooterWeapon weapon,
            string sourceWeapon,
            AvatarMask upperBodyMask)
        {
            SightItem aimAds = weapon.Sights.Get(new IdString("aim-ads"));
            if (aimAds == null) return;

            string sightPath = $"{SAMPLE_ROOT}/Sights/{sourceWeapon}_Sight_Aim_Ads.asset";
            Sight sourceSight = AssetDatabase.LoadAssetAtPath<Sight>(sightPath);
            if (sourceSight == null)
            {
                throw new InvalidOperationException(
                    $"Could not load GC2 ADS sight template: {sightPath}"
                );
            }

            if (aimAds.Sight != sourceSight)
                SetField(aimAds, "m_Sight", sourceSight);

            // GC2 normally fades the aiming rig by the global Gesture weight while the
            // fire clip plays. With a moving locomotion state underneath, that makes the
            // shoulders and spine alternate between two poses on every shot. Keep both
            // aiming solvers active; recoil is still applied by RigShooterHuman afterwards.
            SetField(sourceSight, "m_ShootingUsesFK", true);
            SetField(sourceSight, "m_ShootingUsesIK", true);
            EditorUtility.SetDirty(sourceSight);

            StateData aimStateData = (StateData) GetField(sourceSight, "m_State");
            State aimState = aimStateData.State;
            if (aimState != null && upperBodyMask != null &&
                aimState.StateMask != upperBodyMask)
            {
                // Match GC2's intended stack: full-body Shooter_Locomotion at layer 7,
                // then the weapon-specific ADS upper body pose at layer 8.
                SetBaseField(aimState, "m_StateMask", upperBodyMask);
                EditorUtility.SetDirty(aimState);
            }
        }

        private static void RepairBikeDriverAimSight(
            ShooterWeapon weapon,
            Definition definition,
            AvatarMask bikeDriverShooterMask)
        {
            if (!IsBikeDriverWeapon(definition) || bikeDriverShooterMask == null)
                return;

            string sourceSightPath =
                $"{SAMPLE_ROOT}/Sights/{definition.SourceWeapon}_Sight_Aim_Ads.asset";
            string sourceStatePath =
                $"{SAMPLE_ROOT}/States/{definition.SourceWeapon}_Aim.asset";
            string localStatePath =
                $"{BIKE_SIGHT_ROOT}/{definition.Id}-bike-driver-aim.asset";
            string localSightPath =
                $"{BIKE_SIGHT_ROOT}/{definition.Id}-bike-driver-ads.asset";

            CopyAssetIfMissing(sourceStatePath, localStatePath);
            CopyAssetIfMissing(sourceSightPath, localSightPath);
            StateOverrideAnimator driverState =
                AssetDatabase.LoadAssetAtPath<StateOverrideAnimator>(localStatePath);
            Sight driverSight = AssetDatabase.LoadAssetAtPath<Sight>(localSightPath);
            if (driverState == null || driverSight == null)
            {
                throw new InvalidOperationException(
                    $"Could not create Bike driver Shooter pose for {definition.DisplayName}"
                );
            }

            driverState.name = GetAssetMainName(localStatePath);
            SetBaseField(driverState, "m_StateMask", bikeDriverShooterMask);

            driverSight.name = GetAssetMainName(localSightPath);
            SetField(driverSight, "m_State", new StateData(driverState));
            SetField(driverSight, "m_ShootingUsesFK", true);
            SetField(driverSight, "m_ShootingUsesIK", true);

            TBiomechanics biomechanics = driverSight.Biomechanics.Value;
            HumanFreeHand freeHand = biomechanics?.HumanFreeHand;
            if (freeHand != null)
                SetField(freeHand, "m_UseFreeHand", HumanHand.None);

            IdString driverAimId = new(BIKE_DRIVER_AIM_ID);
            SightItem driverItem = weapon.Sights.Get(driverAimId);
            if (driverItem == null)
            {
                SightItem[] items =
                    (SightItem[]) GetField(weapon.Sights, "m_Sights") ??
                    Array.Empty<SightItem>();
                driverItem = new SightItem();
                Array.Resize(ref items, items.Length + 1);
                items[^1] = driverItem;
                SetField(weapon.Sights, "m_Sights", items);
            }

            SetField(driverItem, "m_Id", driverAimId);
            SetField(driverItem, "m_Sight", driverSight);
            SetField(driverItem, "m_ScopeThrough", false);
            SetField(driverItem, "m_ScopePosition", Vector3.zero);
            SetField(driverItem, "m_ScopeRotation", Vector3.zero);
            SetField(driverItem, "m_ScopeDistance", 0f);
            SetBaseField(driverItem, "m_IsEnabled", true);

            EditorUtility.SetDirty(driverState);
            EditorUtility.SetDirty(driverSight);
            EditorUtility.SetDirty(weapon);
        }

        private static bool IsBikeDriverWeapon(Definition definition)
        {
            return definition != null &&
                   (string.Equals(
                        definition.Id,
                        "m1911",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        definition.Id,
                        "uzi",
                        StringComparison.OrdinalIgnoreCase
                    ));
        }

        private static void CopyAssetIfMissing(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null) return;
            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                throw new InvalidOperationException(
                    $"Could not copy Shooter asset: {sourcePath} -> {destinationPath}"
                );
            }
        }

        private static string GetAssetMainName(string assetPath)
        {
            return System.IO.Path.GetFileNameWithoutExtension(assetPath);
        }

        private static void RepairLocalMainAssetNames()
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { RESOURCE_ROOT });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(
                        System.IO.Path.GetExtension(path),
                        ".asset",
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    continue;
                }

                UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                if (mainAsset == null) continue;

                string expectedName = GetAssetMainName(path);
                if (string.Equals(mainAsset.name, expectedName, StringComparison.Ordinal))
                    continue;

                mainAsset.name = expectedName;
                EditorUtility.SetDirty(mainAsset);
            }
        }

        private static bool NeedsLocalMainAssetNameRepair()
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { RESOURCE_ROOT });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(
                        System.IO.Path.GetExtension(path),
                        ".asset",
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    continue;
                }

                UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                if (mainAsset != null && !string.Equals(
                        mainAsset.name,
                        GetAssetMainName(path),
                        StringComparison.Ordinal
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RepairWeaponSightLayerMasks(ShooterWeapon weapon)
        {
            SightItem[] sightItems = (SightItem[]) GetField(weapon.Sights, "m_Sights");
            HashSet<Sight> repaired = new();
            foreach (SightItem sightItem in sightItems)
            {
                Sight sight = sightItem?.Sight;
                if (sight == null || !repaired.Add(sight)) continue;

                SerializedObject serializedSight = new(sight);
                SerializedProperty property = serializedSight.GetIterator();
                bool changed = false;
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (!IsSightRaycastMask(property)) continue;

                    if (property.uintValue == SHOOTER_ALL_LAYER_BITS) continue;
                    property.uintValue = SHOOTER_ALL_LAYER_BITS;
                    changed = true;
                }

                if (!changed) continue;
                serializedSight.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sight);
            }
        }

        private static bool WeaponSightsNeedLayerMaskRepair(ShooterWeapon weapon)
        {
            SightItem[] sightItems = (SightItem[]) GetField(weapon.Sights, "m_Sights");
            HashSet<Sight> checkedSights = new();
            foreach (SightItem sightItem in sightItems)
            {
                Sight sight = sightItem?.Sight;
                if (sight == null || !checkedSights.Add(sight)) continue;

                SerializedObject serializedSight = new(sight);
                SerializedProperty property = serializedSight.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (!IsSightRaycastMask(property)) continue;
                    if (property.uintValue != SHOOTER_ALL_LAYER_BITS) return true;
                }
            }

            return false;
        }

        private static bool IsSightRaycastMask(SerializedProperty property)
        {
            if (property.name != "m_Bits") return false;
            string path = property.propertyPath;
            return path.Contains("m_LayerMask", StringComparison.Ordinal) ||
                   path.Contains("m_UseRaycast", StringComparison.Ordinal);
        }

        private static void SetDamageInstructions(
            ShooterWeapon weapon,
            Definition definition,
            GameObject bloodHitEffect)
        {
            RunInstructionsList onHit =
                (RunInstructionsList) GetField(weapon, "m_OnHit");
            InstructionList list =
                (InstructionList) GetField(onHit, "m_Instructions");
            Instruction[] current =
                (Instruction[]) GetField(list, "m_Instructions");

            List<Instruction> instructions = new(current.Length + 2);
            for (int i = 0; i < current.Length; ++i)
            {
                if (current[i] is InstructionFranklinVehicleDamage or
                    InstructionFranklinCharacterDamage)
                {
                    continue;
                }
                instructions.Add(current[i]);
            }

            instructions.Add(new InstructionFranklinCharacterDamage(
                definition.CharacterDamage,
                definition.HeadshotMultiplier,
                definition.HelmetMultiplier,
                definition.HelmetImpulse,
                definition.ArmorAbsorption,
                bloodHitEffect
            ));
            instructions.Add(new InstructionFranklinVehicleDamage(definition.VehicleDamage));
            SetField(
                weapon,
                "m_OnHit",
                new RunInstructionsList(instructions.ToArray())
            );
        }

        private static void ConfigureExplosionInstructionPool(
            ShooterWeapon weapon,
            GameObject explosionEffect)
        {
            RunInstructionsList onHit =
                (RunInstructionsList) GetField(weapon, "m_OnHit");
            InstructionList list =
                (InstructionList) GetField(onHit, "m_Instructions");
            Instruction[] instructions =
                (Instruction[]) GetField(list, "m_Instructions");

            foreach (Instruction instruction in instructions)
            {
                if (instruction is not InstructionGameObjectInstantiate instantiate)
                    continue;

                PropertyGetInstantiate effect = (PropertyGetInstantiate) GetField(
                    instantiate,
                    "m_GameObject"
                );
                if (effect == null || effect.EditorValue != explosionEffect) continue;

                effect.usePooling = true;
                effect.size = EXPLOSION_IMPACT_POOL_SIZE;
                effect.hasDuration = true;
                effect.duration = EXPLOSION_IMPACT_POOL_DURATION;
            }
        }

        private static void ConfigureAutomaticCameraShake(
            ShooterWeapon weapon,
            Definition definition)
        {
            if (definition.SourceWeapon != "AK") return;

            RunInstructionsList onShoot =
                (RunInstructionsList) GetField(weapon, "m_OnShoot");
            InstructionList list =
                (InstructionList) GetField(onShoot, "m_Instructions");
            Instruction[] instructions =
                (Instruction[]) GetField(list, "m_Instructions");

            foreach (Instruction instruction in instructions)
            {
                if (instruction is InstructionCameraShakeBurst cameraShake)
                {
                    SetField(
                        cameraShake,
                        "m_Duration",
                        AUTOMATIC_CAMERA_SHAKE_DURATION
                    );
                }
            }
        }

        private static void EnsurePlayerArmor()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            if (prefab == null) return;
            if (prefab.GetComponent<FranklinArmor>() != null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);
            try
            {
                FranklinArmor armor = root.GetComponent<FranklinArmor>();
                if (armor == null) armor = root.AddComponent<FranklinArmor>();
                SerializedObject serializedArmor = new(armor);
                serializedArmor.FindProperty("m_MaxArmor").floatValue = 100f;
                serializedArmor.FindProperty("m_StartingArmor").floatValue = 100f;
                serializedArmor.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsurePlayerFirstPersonCameraManager()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            if (prefab == null) return;

            Character prefabPlayer = prefab.GetComponent<Character>();
            Transform prefabManager = prefab.transform.Find("ManagerCameraFPS");
            FranklinFirstPersonCameraManager prefabComponent =
                prefabManager != null
                    ? prefabManager.GetComponent<FranklinFirstPersonCameraManager>()
                    : null;
            if (prefabComponent != null)
            {
                SerializedObject serialized = new(prefabComponent);
                if (serialized.FindProperty("m_Player").objectReferenceValue == prefabPlayer)
                    return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);
            try
            {
                Transform managerTransform = root.transform.Find("ManagerCameraFPS");
                if (managerTransform == null)
                {
                    GameObject managerObject = new("ManagerCameraFPS");
                    managerTransform = managerObject.transform;
                    managerTransform.SetParent(root.transform, false);
                }

                FranklinFirstPersonCameraManager manager =
                    managerTransform.GetComponent<FranklinFirstPersonCameraManager>();
                if (manager == null)
                {
                    manager = managerTransform.gameObject
                        .AddComponent<FranklinFirstPersonCameraManager>();
                }
                SerializedObject serializedManager = new(manager);
                serializedManager.FindProperty("m_Player").objectReferenceValue =
                    root.GetComponent<Character>();
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureUiSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { RESOURCE_ROOT + "/UI" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                int maximumSize = GetUiTextureMaximumSize(path);
                bool fallbackPhysicsShape =
                    IsUiFallbackPhysicsShapeEnabled(importer);
                bool changed = importer.textureType != TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Single ||
                               importer.alphaIsTransparency == false ||
                               importer.mipmapEnabled ||
                               importer.isReadable ||
                               importer.wrapMode != TextureWrapMode.Clamp ||
                               importer.filterMode != FilterMode.Bilinear ||
                               importer.maxTextureSize != maximumSize ||
                               fallbackPhysicsShape ||
                               importer.textureCompression ==
                               TextureImporterCompression.Uncompressed;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 1;
                importer.maxTextureSize = maximumSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                changed |= DisableUiFallbackPhysicsShape(importer);
                changed |= ConfigureMobileUiTexture(importer, "Android", maximumSize);
                changed |= ConfigureMobileUiTexture(importer, "iPhone", maximumSize);
                if (changed) importer.SaveAndReimport();
            }
        }

        private static bool NeedsUiSpriteRepair()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { RESOURCE_ROOT + "/UI" }
            );
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    return true;

                int maximumSize = GetUiTextureMaximumSize(path);
                if (importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single ||
                    !importer.alphaIsTransparency ||
                    importer.mipmapEnabled ||
                    importer.isReadable ||
                    importer.wrapMode != TextureWrapMode.Clamp ||
                    importer.filterMode != FilterMode.Bilinear ||
                    importer.maxTextureSize != maximumSize ||
                    IsUiFallbackPhysicsShapeEnabled(importer) ||
                    importer.textureCompression ==
                    TextureImporterCompression.Uncompressed ||
                    NeedsMobileUiTexture(importer, "Android", maximumSize) ||
                    NeedsMobileUiTexture(importer, "iPhone", maximumSize))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUiFallbackPhysicsShapeEnabled(TextureImporter importer)
        {
            SerializedObject serializedImporter = new(importer);
            SerializedProperty property = serializedImporter.FindProperty(
                "m_SpriteGenerateFallbackPhysicsShape"
            );
            return property != null && property.boolValue;
        }

        private static bool DisableUiFallbackPhysicsShape(TextureImporter importer)
        {
            SerializedObject serializedImporter = new(importer);
            SerializedProperty property = serializedImporter.FindProperty(
                "m_SpriteGenerateFallbackPhysicsShape"
            );
            if (property == null || !property.boolValue) return false;

            property.boolValue = false;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static int GetUiTextureMaximumSize(string path)
        {
            return path.EndsWith(
                "/weapon-wheel-background.png",
                StringComparison.OrdinalIgnoreCase
            ) ? 1024 : 256;
        }

        private static bool ConfigureMobileUiTexture(
            TextureImporter importer,
            string platform,
            int maximumSize)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platform);
            bool changed = NeedsMobileUiTexture(importer, platform, maximumSize);
            if (!changed) return false;

            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = maximumSize;
            settings.format = TextureImporterFormat.Automatic;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 50;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
            return true;
        }

        private static bool NeedsMobileUiTexture(
            TextureImporter importer,
            string platform,
            int maximumSize)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platform);
            return settings.name != platform ||
                   !settings.overridden ||
                   settings.maxTextureSize != maximumSize ||
                   settings.format != TextureImporterFormat.Automatic ||
                   settings.textureCompression ==
                   TextureImporterCompression.Uncompressed ||
                   settings.compressionQuality > 50 ||
                   settings.crunchedCompression;
        }

        private static AvatarMask CreateOrRepairUpperBodyMask()
        {
            AvatarMask upperBodyMask =
                AssetDatabase.LoadAssetAtPath<AvatarMask>(UPPER_BODY_MASK_PATH);
            if (upperBodyMask == null)
            {
                upperBodyMask = new AvatarMask
                {
                    name = "Franklin Shooter Upper Body"
                };
                AssetDatabase.CreateAsset(upperBodyMask, UPPER_BODY_MASK_PATH);
            }

            for (int i = 0; i < (int) AvatarMaskBodyPart.LastBodyPart; ++i)
            {
                upperBodyMask.SetHumanoidBodyPartActive(
                    (AvatarMaskBodyPart) i,
                    false
                );
            }

            AvatarMaskBodyPart[] activeBodyParts =
            {
                AvatarMaskBodyPart.Body,
                AvatarMaskBodyPart.Head,
                AvatarMaskBodyPart.LeftArm,
                AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.LeftFingers,
                AvatarMaskBodyPart.RightFingers,
                AvatarMaskBodyPart.LeftHandIK,
                AvatarMaskBodyPart.RightHandIK
            };
            foreach (AvatarMaskBodyPart bodyPart in activeBodyParts)
                upperBodyMask.SetHumanoidBodyPartActive(bodyPart, true);
            EditorUtility.SetDirty(upperBodyMask);
            return upperBodyMask;
        }

        private static StateBasicLocomotion CreateOrRepairShooterLocomotion()
        {
            StateBasicLocomotion locomotion =
                AssetDatabase.LoadAssetAtPath<StateBasicLocomotion>(
                    SHOOTER_LOCOMOTION_PATH
                );
            if (locomotion == null)
            {
                if (!AssetDatabase.CopyAsset(
                        SAMPLE_SHOOTER_LOCOMOTION_PATH,
                        SHOOTER_LOCOMOTION_PATH))
                {
                    throw new InvalidOperationException(
                        $"Could not copy GC2 Shooter locomotion: " +
                        SAMPLE_SHOOTER_LOCOMOTION_PATH
                    );
                }

                locomotion = AssetDatabase.LoadAssetAtPath<StateBasicLocomotion>(
                    SHOOTER_LOCOMOTION_PATH
                );
            }

            if (locomotion == null)
            {
                throw new InvalidOperationException(
                    $"Could not load local Shooter locomotion: {SHOOTER_LOCOMOTION_PATH}"
                );
            }

            SerializedObject serializedLocomotion = new(locomotion);
            serializedLocomotion.FindProperty("m_StateMask").objectReferenceValue =
                null;
            serializedLocomotion.FindProperty("m_Properties.m_Speed.m_IsEnabled")
                .boolValue = true;
            serializedLocomotion.ApplyModifiedPropertiesWithoutUndo();
            locomotion.name = GetAssetMainName(SHOOTER_LOCOMOTION_PATH);
            EditorUtility.SetDirty(locomotion);
            return locomotion;
        }

        private static void CreateOrRepairBikeDriverMask()
        {
            AvatarMask mask =
                AssetDatabase.LoadAssetAtPath<AvatarMask>(BIKE_DRIVER_MASK_PATH);
            if (mask == null)
            {
                mask = new AvatarMask
                {
                    name = "Franklin Bike Driver Seat And Left Hand"
                };
                AssetDatabase.CreateAsset(mask, BIKE_DRIVER_MASK_PATH);
            }

            for (int i = 0; i < (int) AvatarMaskBodyPart.LastBodyPart; ++i)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart) i, true);

            // Bike owns the seated body and the steering hand only. Shooter owns
            // the complete gun-hand chain, including fingers and Humanoid hand IK.
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
            EditorUtility.SetDirty(mask);
        }

        private static AvatarMask CreateOrRepairBikeDriverShooterMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                BIKE_DRIVER_SHOOTER_MASK_PATH
            );
            if (mask == null)
            {
                mask = new AvatarMask
                {
                    name = "Franklin Bike Driver Shooter Upper Body"
                };
                AssetDatabase.CreateAsset(mask, BIKE_DRIVER_SHOOTER_MASK_PATH);
            }

            for (int i = 0; i < (int) AvatarMaskBodyPart.LastBodyPart; ++i)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart) i, false);

            // The Bike state owns root/pelvis/legs and the left steering chain.
            // Shooter only supplies the authored torso, look and gun-side arm.
            AvatarMaskBodyPart[] shooterParts =
            {
                AvatarMaskBodyPart.Body,
                AvatarMaskBodyPart.Head,
                AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.RightFingers,
                AvatarMaskBodyPart.RightHandIK
            };
            foreach (AvatarMaskBodyPart bodyPart in shooterParts)
                mask.SetHumanoidBodyPartActive(bodyPart, true);

            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static bool NeedsCatalogRepair()
        {
            FranklinShooterCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FranklinShooterCatalog>(CATALOG_PATH);
            if (catalog == null || catalog.Count != DEFINITIONS.Length) return true;

            for (int i = 0; i < DEFINITIONS.Length; ++i)
            {
                Definition definition = DEFINITIONS[i];
                FranklinShooterCatalog.Entry entry = catalog.Get(i);
                if (entry == null || entry.Id != definition.Id ||
                    entry.Kind != definition.Kind || entry.Weapon == null ||
                    entry.PropPrefab == null || entry.Icon == null ||
                    entry.StartingMagazine != definition.StartingMagazine)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NeedsRpgRepair()
        {
            if (NeedsRpgAudioImporterRepair()) return true;

            AnimationClip holdClip = LoadAnimationClip(RPG_HOLD_ANIMATION_PATH);
            AnimationClip aimClip = LoadAnimationClip(RPG_AIM_ANIMATION_PATH);
            AnimationClip shootClip = LoadAnimationClip(RPG_SHOOT_ANIMATION_PATH);
            AudioClip launchClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                RPG_LAUNCH_AUDIO_PATH
            );
            AudioClip flightLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(
                RPG_FLIGHT_AUDIO_PATH
            );
            AudioClip explosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                RPG_EXPLOSION_CLOSE_AUDIO_PATH
            );
            AudioClip distantExplosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                RPG_EXPLOSION_DISTANT_AUDIO_PATH
            );
            AvatarMask upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                UPPER_BODY_MASK_PATH
            );
            Ammo ammo = AssetDatabase.LoadAssetAtPath<Ammo>(
                AMMO_ROOT + "/rpg7-ammo.asset"
            );
            Sight idleSight = AssetDatabase.LoadAssetAtPath<Sight>(RPG_IDLE_SIGHT_PATH);
            Sight aimSight = AssetDatabase.LoadAssetAtPath<Sight>(RPG_AIM_SIGHT_PATH);
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(
                RPG_PROJECTILE_PATH
            );
            ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                RESOURCE_ROOT + "/Weapons/rpg7.asset"
            );
            if (holdClip == null || aimClip == null || shootClip == null ||
                launchClip == null || flightLoop == null || explosionClip == null ||
                distantExplosionClip == null ||
                upperBodyMask == null || ammo == null || ammo.IsInfinite ||
                idleSight == null || aimSight == null || projectile == null || weapon == null)
            {
                return true;
            }

            StateData equipState = (StateData) GetField(weapon, "m_State");
            StateData idleState = (StateData) GetField(idleSight, "m_State");
            StateData aimState = (StateData) GetField(aimSight, "m_State");
            PropertyGetAnimation idleAnimation =
                (PropertyGetAnimation) GetField(idleState, "m_AnimationClip");
            PropertyGetAnimation aimAnimation =
                (PropertyGetAnimation) GetField(aimState, "m_AnimationClip");
            PropertyGetInteger idleLayer =
                (PropertyGetInteger) GetField(idleSight, "m_Layer");
            PropertyGetInteger aimLayer =
                (PropertyGetInteger) GetField(aimSight, "m_Layer");
            PropertyGetBool hasMagazine =
                (PropertyGetBool) GetField(weapon.Magazine, "m_HasMagazine");
            PropertyGetInteger magazineSize =
                (PropertyGetInteger) GetField(weapon.Magazine, "m_MagazineSize");
            PropertyGetBool autoReload =
                (PropertyGetBool) GetField(weapon.Magazine, "m_AutoReload");
            PropertyGetDecimal fireRate =
                (PropertyGetDecimal) GetField(weapon.Fire, "m_FireRate");
            PropertyGetDecimal minCharge =
                (PropertyGetDecimal) GetField(weapon.Fire, "m_MinChargeTime");
            PropertyGetDecimal maxCharge =
                (PropertyGetDecimal) GetField(weapon.Fire, "m_MaxChargeTime");
            PropertyGetBool autoRelease =
                (PropertyGetBool) GetField(weapon.Fire, "m_AutoRelease");
            AimCameraRaycast cameraAim = aimSight.Aim.Value as AimCameraRaycast;
            LayerMask cameraAimMask = cameraAim != null
                ? (LayerMask) GetField(cameraAim, "m_LayerMask")
                : default;
            PropertyGetDecimal maxSpreadX =
                (PropertyGetDecimal) GetField(weapon.Accuracy, "m_MaxSpreadX");
            PropertyGetDecimal maxSpreadY =
                (PropertyGetDecimal) GetField(weapon.Accuracy, "m_MaxSpreadY");
            PropertyGetDecimal motionAccuracy =
                (PropertyGetDecimal) GetField(weapon.Accuracy, "m_MotionAccuracy");
            PropertyGetDecimal airborneAccuracy =
                (PropertyGetDecimal) GetField(weapon.Accuracy, "m_AirborneAccuracy");
            PropertyGetDecimal accuracyKick =
                (PropertyGetDecimal) GetField(weapon.Accuracy, "m_AccuracyKick");
            if (equipState.Type != StateData.StateType.State || equipState.State != null ||
                idleState.Type != StateData.StateType.AnimationClip ||
                idleAnimation?.EditorValue != holdClip ||
                idleState.AvatarMask != upperBodyMask ||
                aimState.Type != StateData.StateType.AnimationClip ||
                aimAnimation?.EditorValue != aimClip ||
                aimState.AvatarMask != upperBodyMask ||
                Convert.ToInt32(idleLayer.EditorValue) != 8 ||
                Convert.ToInt32(aimLayer.EditorValue) != 8 ||
                idleSight.Biomechanics.Value is not BiomechanicsNone ||
                aimSight.Biomechanics.Value is BiomechanicsNone ||
                !aimSight.ShootingUsesFK || !aimSight.ShootingUsesIK ||
                !Mathf.Approximately(aimSight.SmoothTime, 0f) ||
                cameraAim == null || cameraAimMask.value != SHOOTER_ALL_LAYER_MASK ||
                !Mathf.Approximately((float) maxSpreadX.EditorValue, 0f) ||
                !Mathf.Approximately((float) maxSpreadY.EditorValue, 0f) ||
                !Mathf.Approximately((float) motionAccuracy.EditorValue, 0f) ||
                !Mathf.Approximately((float) airborneAccuracy.EditorValue, 0f) ||
                !Mathf.Approximately((float) accuracyKick.EditorValue, 0f) ||
                weapon.Sights.Get(new IdString("idle"))?.Sight != idleSight ||
                weapon.Sights.Get(new IdString("aim-ads"))?.Sight != aimSight ||
                (Ammo) GetField(weapon.Magazine, "m_Ammo") != ammo ||
                (bool) hasMagazine.EditorValue ||
                Convert.ToInt32(magazineSize.EditorValue) != 1 ||
                (bool) autoReload.EditorValue ||
                weapon.Fire.Mode != ShootMode.Charge ||
                !Mathf.Approximately((float) fireRate.EditorValue, 0.5f) ||
                !Mathf.Approximately((float) minCharge.EditorValue, 0f) ||
                !Mathf.Approximately((float) maxCharge.EditorValue, 1f) ||
                (bool) autoRelease.EditorValue ||
                weapon.Fire.FireAnimation(null) != shootClip ||
                weapon.Fire.FireAvatarMask != upperBodyMask)
            {
                return true;
            }

            Rigidbody body = projectile.GetComponent<Rigidbody>();
            CapsuleCollider collider = projectile.GetComponent<CapsuleCollider>();
            TrailRenderer trail = projectile.GetComponentInChildren<TrailRenderer>(true);
            FranklinRpgFlightAudio flightAudio =
                projectile.GetComponent<FranklinRpgFlightAudio>();
            if (body == null || body.useGravity || collider == null ||
                body.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative ||
                projectile.GetComponent<Bullet>() == null ||
                flightAudio == null || flightAudio.LaunchClip != launchClip ||
                flightAudio.FlightLoop != flightLoop ||
                !Mathf.Approximately(flightAudio.FlightVolume, 0.72f) ||
                trail == null || !Mathf.Approximately(trail.time, 0.55f) ||
                !Mathf.Approximately(trail.widthMultiplier, 0.085f) ||
                !Mathf.Approximately(trail.minVertexDistance, 0.12f))
            {
                return true;
            }

            Shot shot = (Shot) GetField(weapon.Projectile, "m_Shot");
            if (shot?.Value is not ShotRigidbody rigidbodyShot ||
                GetBaseField(rigidbodyShot, "m_Prefab") is not
                    PropertyGetGameObject prefab ||
                prefab.EditorValue != projectile ||
                GetBaseField(rigidbodyShot, "m_AimAtSightPoint") is not true ||
                Convert.ToInt32(GetField(rigidbodyShot, "m_Impulse")) != 2 ||
                Convert.ToInt32(GetField(rigidbodyShot, "m_Hit")) != 0 ||
                GetField(rigidbodyShot, "m_ImpulseForce") is not
                    PropertyGetDecimal impulse ||
                !Mathf.Approximately((float) impulse.EditorValue, 42f) ||
                GetField(rigidbodyShot, "m_AirResistance") is not
                    PropertyGetDecimal resistance ||
                !Mathf.Approximately((float) resistance.EditorValue, 0f) ||
                GetField(rigidbodyShot, "m_WindInfluence") is not
                    PropertyGetDecimal wind ||
                !Mathf.Approximately((float) wind.EditorValue, 0f) ||
                GetField(rigidbodyShot, "m_MaxDistance") is not
                    PropertyGetDecimal maxDistance ||
                !Mathf.Approximately((float) maxDistance.EditorValue, 150f))
            {
                return true;
            }

            GameObject prop = AssetDatabase.LoadAssetAtPath<GameObject>(
                LOW_PREFABS + "/RPG7.prefab"
            );
            Vector3 muzzlePosition = GetRpgMuzzlePosition(prop);
            Vector3 muzzleRotation = (Vector3) GetField(weapon.Muzzle, "m_Rotation");
            PropertyGetInstantiate impact = (PropertyGetInstantiate) GetField(
                weapon.Projectile,
                "m_ImpactEffect"
            );
            GameObject expectedExplosion = AssetDatabase.LoadAssetAtPath<GameObject>(
                EXPLOSION_EFFECT_PATH
            );
            if ((weapon.Muzzle.LocalPosition - muzzlePosition).sqrMagnitude > 0.000001f ||
                (muzzleRotation - new Vector3(0f, 180f, 0f)).sqrMagnitude > 0.000001f ||
                impact == null || impact.EditorValue != expectedExplosion ||
                !impact.usePooling || impact.size != EXPLOSION_IMPACT_POOL_SIZE ||
                !impact.hasDuration ||
                !Mathf.Approximately(impact.duration, EXPLOSION_IMPACT_POOL_DURATION))
            {
                return true;
            }

            RunInstructionsList onHit = (RunInstructionsList) GetField(weapon, "m_OnHit");
            InstructionList list = (InstructionList) GetField(onHit, "m_Instructions");
            Instruction[] instructions =
                (Instruction[]) GetField(list, "m_Instructions");
            return instructions == null || instructions.Length != 1 ||
                   instructions[0] is not InstructionFranklinExplosionDamage damage ||
                   !Mathf.Approximately(damage.Radius, 5f) ||
                   !Mathf.Approximately(damage.CharacterDamage, 250f) ||
                   !Mathf.Approximately(damage.VehicleDamage, 60f) ||
                   !damage.DestroyGroundVehiclesOnDirectHit ||
                   !Mathf.Approximately(damage.DirectVehicleForwardVelocity, 2.8f) ||
                   !Mathf.Approximately(damage.DirectVehicleUpwardVelocity, 0.35f) ||
                   !Mathf.Approximately(damage.RagdollVelocity, 3.8f) ||
                   damage.ExplosionClip != explosionClip ||
                   damage.DistantExplosionClip != distantExplosionClip ||
                   !Mathf.Approximately(damage.NearAudioDistance, 110f) ||
                   !Mathf.Approximately(damage.DistantAudioDistance, 320f);
        }

        private static bool NeedsThrowableRepair()
        {
            GameObject explosion = AssetDatabase.LoadAssetAtPath<GameObject>(
                EXPLOSION_EFFECT_PATH
            );
            AvatarMask throwableRightArmMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                BIKE_DRIVER_SHOOTER_MASK_PATH
            );
            if (throwableRightArmMask == null) return true;
            AudioClip explosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                SOURCE_GRENADE_EXPLOSION_AUDIO
            );
            AudioClip smokeHissClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                SOURCE_SMOKE_HISS_AUDIO
            );
            if (explosionClip == null || smokeHissClip == null) return true;

            foreach (Definition definition in DEFINITIONS)
            {
                if (definition.Kind == FranklinShooterCatalog.WeaponKind.Firearm)
                    continue;

                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                Ammo ammo = AssetDatabase.LoadAssetAtPath<Ammo>(
                    $"{AMMO_ROOT}/{definition.Id}-ammo.asset"
                );
                GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{PROJECTILE_ROOT}/{definition.Id}-projectile.prefab"
                );
                if (weapon == null || ammo == null || ammo.IsInfinite ||
                    weapon.Fire.Mode != ShootMode.Charge ||
                    weapon.Fire.FireAvatarMask != throwableRightArmMask ||
                    projectile == null || projectile.GetComponent<Bullet>() == null ||
                    projectile.GetComponent<Rigidbody>() == null ||
                    projectile.GetComponent<Collider>() == null ||
                    projectile.GetComponentInChildren<FranklinThrowableVisualSpin>(true) == null)
                {
                    return true;
                }

                if ((Ammo) GetField(weapon.Magazine, "m_Ammo") != ammo)
                    return true;

                SightItem idleSight = weapon.Sights.Get(new IdString("idle"));
                SightItem throwSight = weapon.Sights.Get(new IdString("throw"));
                if (idleSight?.Sight == null || throwSight?.Sight == null ||
                    idleSight.Sight.Biomechanics.Value is not BiomechanicsNone ||
                    throwSight.Sight.Biomechanics.Value is BiomechanicsNone ||
                    !Mathf.Approximately(throwSight.Sight.SmoothTime, 0.14f))
                {
                    return true;
                }

                Shot shot = (Shot) GetField(weapon.Projectile, "m_Shot");
                if (shot?.Value is not ShotRigidbody rigidbodyShot ||
                    GetBaseField(rigidbodyShot, "m_Prefab") is not
                        PropertyGetGameObject projectileProperty ||
                    projectileProperty.EditorValue != projectile)
                {
                    return true;
                }

                RunInstructionsList onHit =
                    (RunInstructionsList) GetField(weapon, "m_OnHit");
                InstructionList instructionList =
                    (InstructionList) GetField(onHit, "m_Instructions");
                Instruction[] instructions =
                    (Instruction[]) GetField(instructionList, "m_Instructions");
                if (instructions == null || instructions.Length != 1) return true;

                PropertyGetInstantiate impact = (PropertyGetInstantiate) GetField(
                    weapon.Projectile,
                    "m_ImpactEffect"
                );
                if (definition.Kind ==
                    FranklinShooterCatalog.WeaponKind.ExplosiveGrenade)
                {
                    if (instructions[0] is not InstructionFranklinExplosionDamage damage ||
                        !Mathf.Approximately(damage.Radius, 4f) ||
                        !Mathf.Approximately(
                            damage.CharacterDamage,
                            definition.CharacterDamage
                        ) ||
                        !Mathf.Approximately(
                            damage.VehicleDamage,
                            definition.VehicleDamage
                        ) || damage.DestroyGroundVehiclesOnDirectHit ||
                        !Mathf.Approximately(damage.RagdollVelocity, 3.2f) ||
                        !Mathf.Approximately(damage.RagdollDuration, 1.45f) ||
                        damage.ExplosionClip != explosionClip ||
                        impact == null || impact.EditorValue != explosion ||
                        !impact.usePooling || impact.size != EXPLOSION_IMPACT_POOL_SIZE ||
                        !impact.hasDuration || !Mathf.Approximately(
                            impact.duration,
                            EXPLOSION_IMPACT_POOL_DURATION
                        ))
                    {
                        return true;
                    }
                }
                else if (definition.Kind == FranklinShooterCatalog.WeaponKind.SmokeGrenade)
                {
                    if (instructions[0] is not InstructionFranklinSmokeGrenade smoke ||
                        !Mathf.Approximately(smoke.Duration, 18f) ||
                        !Mathf.Approximately(smoke.Radius, 7.5f) ||
                        smoke.HissClip != smokeHissClip || impact?.EditorValue != null)
                    {
                        return true;
                    }
                }
                else if (instructions[0] is not InstructionFranklinFlashGrenade flash ||
                         !Mathf.Approximately(flash.Duration, 0.9f) ||
                         !Mathf.Approximately(flash.Radius, 12f) ||
                         flash.FlashClip != explosionClip || impact?.EditorValue != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NeedsRenderingRepair()
        {
            if (HasLegacyShooterSampleMaterials()) return true;

            return !MaterialUsesShader(
                       CHARGE_GRENADE_FADE_MATERIAL,
                       URP_PARTICLE_SHADER
                   ) ||
                   !MaterialUsesShader(CHARGE_GRENADE_GUIDE_MATERIAL, URP_LIT_SHADER) ||
                   !MaterialUsesShader(CHARGE_GRENADE_JOINTS_MATERIAL, URP_LIT_SHADER) ||
                   !MaterialUsesShader(CHARGE_GRENADE_SURFACE_MATERIAL, URP_LIT_SHADER) ||
                   !MaterialUsesShader(LOW_WEAPON_MATERIAL, URP_LIT_SHADER) ||
                   AssetDatabase.LoadAssetAtPath<Sprite>(CAUTIOUS_WALK_ICON_PATH) == null ||
                   AssetDatabase.LoadAssetAtPath<Sprite>(FIRST_PERSON_ICON_PATH) == null ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_BLOOD_HIT_EFFECT) == null ||
                   AssetDatabase.LoadAssetAtPath<AvatarMask>(BIKE_DRIVER_MASK_PATH) == null ||
                   !MaterialUsesShader(TRACER_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   !MaterialUsesShader(MUZZLE_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   !MaterialUsesShader(IMPACT_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   !MaterialUsesShader(EXPLOSION_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(MUZZLE_EFFECT_PATH) == null ||
                   EffectParticleBudgetNeedsRepair(MUZZLE_EFFECT_PATH) ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_EFFECT_PATH) == null ||
                   ImpactEffectHasPlanarDecal() ||
                   EffectParticleBudgetNeedsRepair(IMPACT_EFFECT_PATH) ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(EXPLOSION_EFFECT_PATH) == null ||
                   EffectParticleBudgetNeedsRepair(EXPLOSION_EFFECT_PATH);
        }

        private static bool EffectParticleBudgetNeedsRepair(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return false;

            foreach (ParticleSystem particle in
                     prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle.main.maxParticles != GetEffectParticleBudget(
                        prefabPath,
                        particle.gameObject.name
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetEffectParticleBudget(
            string prefabPath,
            string particleName)
        {
            if (prefabPath == IMPACT_EFFECT_PATH) return IMPACT_PARTICLE_BUDGET;
            if (prefabPath == MUZZLE_EFFECT_PATH)
            {
                return particleName.Equals("Flash", StringComparison.OrdinalIgnoreCase)
                    ? MUZZLE_FLASH_PARTICLE_BUDGET
                    : MUZZLE_SMOKE_PARTICLE_BUDGET;
            }
            if (prefabPath == EXPLOSION_EFFECT_PATH)
            {
                return particleName.Equals("Smoke", StringComparison.OrdinalIgnoreCase)
                    ? EXPLOSION_SMOKE_PARTICLE_BUDGET
                    : EXPLOSION_FIREBALL_PARTICLE_BUDGET;
            }

            return IMPACT_PARTICLE_BUDGET;
        }

        private static bool ImpactEffectHasPlanarDecal()
        {
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_EFFECT_PATH);
            if (impact == null) return false;

            foreach (Transform child in impact.GetComponentsInChildren<Transform>(true))
            {
                if (child != impact.transform &&
                    (child.name.Equals("Footprint", StringComparison.OrdinalIgnoreCase) ||
                     child.name.Equals("Decal", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NeedsVehicleBulletDecalRepair()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                VEHICLE_BULLET_SHADER_PATH
            );
            if (shader == null || shader.name != VEHICLE_BULLET_SHADER) return true;

            return BulletDecalAssetsNeedRepair(
                       VEHICLE_BULLET_TEXTURE_PATH,
                       VEHICLE_BULLET_MATERIAL_PATH,
                       shader,
                       256
                   ) ||
                   BulletDecalAssetsNeedRepair(
                       WALL_BULLET_TEXTURE_PATH,
                       WALL_BULLET_MATERIAL_PATH,
                       shader,
                       256
                   ) ||
                   BulletDecalAssetsNeedRepair(
                       RPG_EXPLOSION_DECAL_TEXTURE_PATH,
                       RPG_EXPLOSION_DECAL_MATERIAL_PATH,
                       shader,
                       512,
                       0.82f,
                       0.66f,
                       0.326f
                   ) ||
                   BulletDecalAssetsNeedRepair(
                       RPG_EXPLOSION_VEHICLE_DECAL_TEXTURE_PATH,
                       RPG_EXPLOSION_VEHICLE_DECAL_MATERIAL_PATH,
                       shader,
                       512,
                       0.7f,
                       0.56f,
                       0.348f
                   );
        }

        private static bool BulletDecalAssetsNeedRepair(
            string texturePath,
            string materialPath,
            Shader shader,
            int maxTextureSize,
            float sootStrength = 0f,
            float sootOpacity = 0f,
            float impactCoreScale = 1f)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (texture == null || material == null || material.shader != shader ||
                material.GetTexture("_BaseMap") != texture || !material.enableInstancing ||
                !material.HasProperty("_SootStrength") ||
                !Mathf.Approximately(
                    material.GetFloat("_SootStrength"),
                    sootStrength
                ) ||
                !material.HasProperty("_SootOpacity") ||
                !Mathf.Approximately(
                    material.GetFloat("_SootOpacity"),
                    sootOpacity
                ) ||
                !material.HasProperty("_ImpactCoreScale") ||
                !Mathf.Approximately(
                    material.GetFloat("_ImpactCoreScale"),
                    impactCoreScale
                ))
            {
                return true;
            }

            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
                return true;

            return importer.textureType != TextureImporterType.Default ||
                   importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                   !importer.alphaIsTransparency ||
                   !importer.mipmapEnabled ||
                   importer.maxTextureSize != maxTextureSize ||
                   importer.wrapMode != TextureWrapMode.Clamp ||
                   importer.isReadable;
        }

        private static bool NeedsVehicleImpactForceRepair()
        {
            foreach (Definition definition in DEFINITIONS)
            {
                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null || weapon.Fire.ForceEnabled) return true;
            }

            return false;
        }

        private static bool NeedsShooterLocomotionRepair()
        {
            StateBasicLocomotion locomotion =
                AssetDatabase.LoadAssetAtPath<StateBasicLocomotion>(
                    SHOOTER_LOCOMOTION_PATH
                );
            AvatarMask upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                UPPER_BODY_MASK_PATH
            );
            if (locomotion == null || upperBodyMask == null ||
                locomotion.StateMask != null ||
                locomotion.name != GetAssetMainName(SHOOTER_LOCOMOTION_PATH))
            {
                return true;
            }

            SerializedObject serializedLocomotion = new(locomotion);
            SerializedProperty speedEnabled = serializedLocomotion.FindProperty(
                "m_Properties.m_Speed.m_IsEnabled"
            );
            if (speedEnabled == null || !speedEnabled.boolValue) return true;

            foreach (Definition definition in DEFINITIONS)
            {
                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null) return true;

                StateData weaponState = (StateData) GetField(weapon, "m_State");
                bool isRpg = string.Equals(
                    definition.Id,
                    "rpg7",
                    StringComparison.OrdinalIgnoreCase
                );
                if (definition.Kind == FranklinShooterCatalog.WeaponKind.Firearm && !isRpg)
                {
                    if (weaponState.State != locomotion) return true;
                }
                else if (weaponState.Type != StateData.StateType.State ||
                         weaponState.State != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NeedsShooterDamageRepair()
        {
            GameObject bloodHitEffect = AssetDatabase.LoadAssetAtPath<GameObject>(
                SOURCE_BLOOD_HIT_EFFECT
            );
            if (bloodHitEffect == null) return true;

            foreach (Definition definition in DEFINITIONS)
            {
                if (definition.Kind != FranklinShooterCatalog.WeaponKind.Firearm)
                    continue;
                if (string.Equals(
                        definition.Id,
                        "rpg7",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null) return true;

                RunInstructionsList onHit =
                    (RunInstructionsList) GetField(weapon, "m_OnHit");
                if (onHit == null) return true;
                InstructionList list =
                    (InstructionList) GetField(onHit, "m_Instructions");
                if (list == null) return true;
                Instruction[] instructions =
                    (Instruction[]) GetField(list, "m_Instructions");
                if (instructions == null) return true;

                int characterDamageCount = 0;
                int vehicleDamageCount = 0;
                foreach (Instruction instruction in instructions)
                {
                    if (instruction is InstructionFranklinCharacterDamage characterDamage)
                    {
                        ++characterDamageCount;
                        if (!Mathf.Approximately(
                                characterDamage.BodyDamage,
                                definition.CharacterDamage) ||
                            !Mathf.Approximately(
                                characterDamage.HeadMultiplier,
                                definition.HeadshotMultiplier) ||
                            !Mathf.Approximately(
                                characterDamage.HelmetMultiplier,
                                definition.HelmetMultiplier) ||
                            !Mathf.Approximately(
                                characterDamage.HelmetImpulse,
                                definition.HelmetImpulse) ||
                            !Mathf.Approximately(
                                characterDamage.ArmorAbsorption,
                                definition.ArmorAbsorption) ||
                            characterDamage.BloodHitEffect != bloodHitEffect ||
                            !Mathf.Approximately(
                                characterDamage.BloodEffectLifetime,
                                BLOOD_HIT_EFFECT_LIFETIME))
                        {
                            return true;
                        }
                    }
                    else if (instruction is InstructionFranklinVehicleDamage vehicleDamage)
                    {
                        ++vehicleDamageCount;
                        if (!Mathf.Approximately(
                                vehicleDamage.Damage,
                                definition.VehicleDamage))
                        {
                            return true;
                        }
                    }
                }

                if (characterDamageCount != 1 || vehicleDamageCount != 1) return true;
            }

            return false;
        }

        private static bool NeedsPlayerArmorRepair()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            return prefab != null && prefab.GetComponent<FranklinArmor>() == null;
        }

        private static bool NeedsPlayerFirstPersonCameraRepair()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            if (prefab == null) return false;

            Transform managerTransform = prefab.transform.Find("ManagerCameraFPS");
            if (managerTransform == null) return true;

            FranklinFirstPersonCameraManager manager =
                managerTransform.GetComponent<FranklinFirstPersonCameraManager>();
            if (manager == null) return true;

            SerializedObject serializedManager = new(manager);
            return serializedManager.FindProperty("m_Player").objectReferenceValue !=
                   prefab.GetComponent<Character>();
        }

        private static bool NeedsShooterLayerMaskRepair()
        {
            foreach (Definition definition in DEFINITIONS)
            {
                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null) return true;

                Shot shot = (Shot) GetField(weapon.Projectile, "m_Shot");
                TShot shotType = shot?.Value;
                if (shotType is not ShotRaycast && shotType is not ShotKinematic) continue;

                LayerMask layerMask = (LayerMask) GetField(shotType, "m_LayerMask");
                if (layerMask.value != SHOOTER_ALL_LAYER_MASK) return true;
            }

            return false;
        }

        private static bool NeedsImpactAudioRepair()
        {
            MaterialSoundsAsset source =
                AssetDatabase.LoadAssetAtPath<MaterialSoundsAsset>(
                    SOURCE_IMPACT_MATERIAL_SOUNDS
                );
            MaterialSoundsAsset audioOnly =
                AssetDatabase.LoadAssetAtPath<MaterialSoundsAsset>(
                    IMPACT_AUDIO_ONLY_PATH
                );
            if (source == null || audioOnly == null ||
                !MaterialSoundsAudioMatches(source, audioOnly) ||
                MaterialSoundsHasImpactVisual(audioOnly))
            {
                return true;
            }

            foreach (Definition definition in DEFINITIONS)
            {
                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null) return true;

                MaterialSoundsAsset actual =
                    (MaterialSoundsAsset) GetField(
                        weapon.Projectile,
                        "m_ImpactSound"
                    );
                MaterialSoundsAsset expected = definition.SourceWeapon == "Grenade"
                    ? null
                    : audioOnly;
                if (actual != expected) return true;
            }

            return false;
        }

        private static bool MaterialSoundsAudioMatches(
            MaterialSoundsAsset source,
            MaterialSoundsAsset target)
        {
            if ((string) GetField(source, "m_TextureName") !=
                (string) GetField(target, "m_TextureName"))
            {
                return false;
            }

            MaterialSoundsData sourceData = source.MaterialSounds;
            MaterialSoundsData targetData = target.MaterialSounds;
            if (sourceData == null || targetData == null ||
                sourceData.LayerMask.value != targetData.LayerMask.value ||
                !MaterialSoundAudioMatches(
                    sourceData.DefaultSounds,
                    targetData.DefaultSounds
                ) ||
                sourceData.MaterialSounds.Length != targetData.MaterialSounds.Length)
            {
                return false;
            }

            for (int i = 0; i < sourceData.MaterialSounds.Length; ++i)
            {
                MaterialSoundTexture sourceEntry = sourceData.MaterialSounds[i];
                MaterialSoundTexture targetEntry = targetData.MaterialSounds[i];
                if (!MaterialSoundAudioMatches(sourceEntry, targetEntry)) return false;
                if (sourceEntry == null || targetEntry == null) continue;
                if ((string) GetField(sourceEntry, "m_Name") !=
                    (string) GetField(targetEntry, "m_Name") ||
                    sourceEntry.Texture != targetEntry.Texture)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MaterialSoundAudioMatches(
            IMaterialSound source,
            IMaterialSound target)
        {
            if (source == null || target == null) return source == target;
            if (!Mathf.Approximately(source.Volume, target.Volume)) return false;

            AudioClip[] sourceClips =
                (AudioClip[]) GetField(source, "m_Variations");
            AudioClip[] targetClips =
                (AudioClip[]) GetField(target, "m_Variations");
            if (sourceClips == null || targetClips == null)
                return sourceClips == targetClips;
            if (sourceClips.Length != targetClips.Length) return false;

            for (int i = 0; i < sourceClips.Length; ++i)
            {
                if (sourceClips[i] != targetClips[i]) return false;
            }

            return true;
        }

        private static bool MaterialSoundsHasImpactVisual(
            MaterialSoundsAsset materialSounds)
        {
            if (MaterialSoundHasImpactVisual(
                    materialSounds.MaterialSounds.DefaultSounds
                ))
            {
                return true;
            }

            foreach (MaterialSoundTexture materialSound in
                     materialSounds.MaterialSounds.MaterialSounds)
            {
                if (MaterialSoundHasImpactVisual(materialSound)) return true;
            }

            return false;
        }

        private static bool MaterialSoundHasImpactVisual(IMaterialSound materialSound)
        {
            return materialSound?.Impact != null &&
                   (GameObject) GetField(materialSound.Impact, "m_Prefab") != null;
        }

        private static bool NeedsCharacterImpactFilterRepair()
        {
            GameObject expectedImpact = AssetDatabase.LoadAssetAtPath<GameObject>(
                IMPACT_EFFECT_PATH
            );
            if (expectedImpact == null) return true;

            foreach (Definition definition in DEFINITIONS)
            {
                if (definition.SourceWeapon == "Grenade") continue;

                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null) return true;

                PropertyGetInstantiate impact = (PropertyGetInstantiate) GetField(
                    weapon.Projectile,
                    "m_ImpactEffect"
                );
                if (impact == null || !impact.usePooling ||
                    impact.size != SURFACE_IMPACT_POOL_SIZE ||
                    !impact.hasDuration ||
                    !Mathf.Approximately(
                        impact.duration,
                        SURFACE_IMPACT_POOL_DURATION
                    ))
                {
                    return true;
                }

                object property = GetBaseField(impact, "m_Property");
                if (property is not GetGameObjectFranklinSurfaceImpact filter ||
                    filter.SurfaceImpact != expectedImpact)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NeedsTracerVisualRepair()
        {
            GameObject expectedMuzzle = AssetDatabase.LoadAssetAtPath<GameObject>(
                MUZZLE_EFFECT_PATH
            );

            foreach (Definition definition in DEFINITIONS)
            {
                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null) return true;
                if (definition.SourceWeapon == "AK" &&
                    AutomaticCameraShakeNeedsRepair(weapon))
                {
                    return true;
                }

                if (definition.UseMuzzleEffect)
                {
                    PropertyGetInstantiate muzzle = (PropertyGetInstantiate) GetField(
                        weapon.Fire,
                        "m_MuzzleEffect"
                    );
                    if (muzzle == null || muzzle.EditorValue != expectedMuzzle ||
                        !muzzle.usePooling || muzzle.size != MUZZLE_POOL_SIZE ||
                        !muzzle.hasDuration ||
                        !Mathf.Approximately(muzzle.duration, MUZZLE_POOL_DURATION))
                    {
                        return true;
                    }
                }

                PropertyGetInstantiate shell = (PropertyGetInstantiate) GetField(
                    weapon.Shell,
                    "m_Prefab"
                );
                if (shell == null || !shell.usePooling ||
                    shell.size != SHELL_POOL_SIZE || !shell.hasDuration ||
                    !Mathf.Approximately(shell.duration, SHELL_POOL_DURATION))
                {
                    return true;
                }

                Shot shot = (Shot) GetField(weapon.Projectile, "m_Shot");
                if (shot?.Value is not ShotRaycast raycast) continue;

                PropertyGetDecimal duration = (PropertyGetDecimal) GetField(
                    raycast,
                    "m_Duration"
                );
                PropertyGetColor color = (PropertyGetColor) GetField(raycast, "m_Color");
                PropertyGetDecimal width = (PropertyGetDecimal) GetField(raycast, "m_Width");
                float expectedWidth = definition.SourceWeapon == "AK"
                    ? RAYCAST_TRACER_WIDTH_RIFLE
                    : RAYCAST_TRACER_WIDTH_LIGHT;
                if (duration == null ||
                    !Mathf.Approximately(
                        (float) duration.EditorValue,
                        RAYCAST_TRACER_DURATION
                    ) || color == null || color.EditorValue != Color.white ||
                    width == null ||
                    !Mathf.Approximately((float) width.EditorValue, expectedWidth))
                {
                    return true;
                }
            }

            GameObject sniperProjectile = AssetDatabase.LoadAssetAtPath<GameObject>(
                SOURCE_SNIPER_PROJECTILE
            );
            TrailRenderer trail = sniperProjectile != null
                ? sniperProjectile.GetComponentInChildren<TrailRenderer>(true)
                : null;
            if (trail == null ||
                !Mathf.Approximately(trail.widthMultiplier, KINEMATIC_TRACER_WIDTH))
            {
                return true;
            }

            foreach (GradientColorKey colorKey in trail.colorGradient.colorKeys)
            {
                if (colorKey.color != Color.white) return true;
            }

            return false;
        }

        private static bool ExplosionInstructionNeedsRepair(
            ShooterWeapon weapon,
            GameObject expectedExplosion)
        {
            RunInstructionsList onHit =
                (RunInstructionsList) GetField(weapon, "m_OnHit");
            InstructionList list =
                (InstructionList) GetField(onHit, "m_Instructions");
            Instruction[] instructions =
                (Instruction[]) GetField(list, "m_Instructions");

            foreach (Instruction instruction in instructions)
            {
                if (instruction is not InstructionGameObjectInstantiate instantiate)
                    continue;

                PropertyGetInstantiate effect = (PropertyGetInstantiate) GetField(
                    instantiate,
                    "m_GameObject"
                );
                if (effect == null || effect.EditorValue != expectedExplosion) continue;

                return !effect.usePooling ||
                       effect.size != EXPLOSION_IMPACT_POOL_SIZE ||
                       !effect.hasDuration ||
                       !Mathf.Approximately(
                           effect.duration,
                           EXPLOSION_IMPACT_POOL_DURATION
                       );
            }

            return true;
        }

        private static bool AutomaticCameraShakeNeedsRepair(ShooterWeapon weapon)
        {
            RunInstructionsList onShoot =
                (RunInstructionsList) GetField(weapon, "m_OnShoot");
            InstructionList list =
                (InstructionList) GetField(onShoot, "m_Instructions");
            Instruction[] instructions =
                (Instruction[]) GetField(list, "m_Instructions");

            foreach (Instruction instruction in instructions)
            {
                if (instruction is not InstructionCameraShakeBurst cameraShake)
                    continue;

                float duration = (float) GetField(cameraShake, "m_Duration");
                return !Mathf.Approximately(
                    duration,
                    AUTOMATIC_CAMERA_SHAKE_DURATION
                );
            }

            return false;
        }

        private static bool NeedsAimSightRepair()
        {
            IdString aimAdsId = new("aim-ads");
            AvatarMask upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                UPPER_BODY_MASK_PATH
            );
            AvatarMask bikeDriverShooterMask =
                AssetDatabase.LoadAssetAtPath<AvatarMask>(
                    BIKE_DRIVER_SHOOTER_MASK_PATH
                );
            if (bikeDriverShooterMask == null) return true;

            foreach (Definition definition in DEFINITIONS)
            {
                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null || WeaponSightsNeedLayerMaskRepair(weapon))
                    return true;

                if (IsBikeDriverWeapon(definition) &&
                    BikeDriverAimSightNeedsRepair(
                        weapon,
                        definition,
                        bikeDriverShooterMask
                    ))
                {
                    return true;
                }

                // The Grenade template intentionally has only its default Sight.
                if (definition.SourceWeapon == "Grenade") continue;

                Sight expected = AssetDatabase.LoadAssetAtPath<Sight>(
                    $"{SAMPLE_ROOT}/Sights/{definition.SourceWeapon}_Sight_Aim_Ads.asset"
                );
                SightItem aimAds = weapon?.Sights.Get(aimAdsId);
                State aimState = null;
                if (expected != null)
                {
                    StateData aimStateData = (StateData) GetField(expected, "m_State");
                    aimState = aimStateData.State;
                }
                if (weapon == null || expected == null || aimAds?.Sight != expected ||
                    !expected.ShootingUsesFK || !expected.ShootingUsesIK ||
                    upperBodyMask == null || aimState == null ||
                    aimState.StateMask != upperBodyMask)
                    return true;
            }

            return false;
        }

        private static bool BikeDriverAimSightNeedsRepair(
            ShooterWeapon weapon,
            Definition definition,
            AvatarMask bikeDriverShooterMask)
        {
            string localStatePath =
                $"{BIKE_SIGHT_ROOT}/{definition.Id}-bike-driver-aim.asset";
            string localSightPath =
                $"{BIKE_SIGHT_ROOT}/{definition.Id}-bike-driver-ads.asset";
            StateOverrideAnimator driverState =
                AssetDatabase.LoadAssetAtPath<StateOverrideAnimator>(localStatePath);
            Sight driverSight = AssetDatabase.LoadAssetAtPath<Sight>(localSightPath);
            SightItem driverItem = weapon.Sights.Get(new IdString(BIKE_DRIVER_AIM_ID));

            if (driverState == null || driverSight == null ||
                driverItem?.Sight != driverSight ||
                driverState.StateMask != bikeDriverShooterMask ||
                !driverSight.ShootingUsesFK ||
                !driverSight.ShootingUsesIK)
            {
                return true;
            }

            StateData stateData = (StateData) GetField(driverSight, "m_State");
            if (stateData.State != driverState) return true;

            HumanFreeHand freeHand = driverSight.Biomechanics.Value?.HumanFreeHand;
            return freeHand == null ||
                   (HumanHand) GetField(freeHand, "m_UseFreeHand") != HumanHand.None;
        }

        private static bool MaterialUsesShader(string path, string shaderName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            return material != null &&
                   material.shader != null &&
                   material.shader.name == shaderName &&
                   material.shader.isSupported;
        }

        private static bool HasLegacyShooterSampleMaterials()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Material",
                new[] { SAMPLE_ROOT }
            );
            foreach (string guid in guids)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    AssetDatabase.GUIDToAssetPath(guid)
                );
                if (material == null || material.shader == null) return true;
                if (material.shader.name == "Standard" || !material.shader.isSupported)
                    return true;
            }

            return false;
        }

        private static MaterialSoundsAsset CreateOrRepairImpactAudioOnly()
        {
            MaterialSoundsAsset source =
                AssetDatabase.LoadAssetAtPath<MaterialSoundsAsset>(
                    SOURCE_IMPACT_MATERIAL_SOUNDS
                );
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Shooter impact MaterialSounds is missing: " +
                    SOURCE_IMPACT_MATERIAL_SOUNDS
                );
            }

            MaterialSoundsAsset audioOnly =
                AssetDatabase.LoadAssetAtPath<MaterialSoundsAsset>(
                    IMPACT_AUDIO_ONLY_PATH
                );
            if (audioOnly == null)
            {
                if (!AssetDatabase.CopyAsset(
                        SOURCE_IMPACT_MATERIAL_SOUNDS,
                        IMPACT_AUDIO_ONLY_PATH
                    ))
                {
                    throw new InvalidOperationException(
                        $"Could not create local impact audio: {IMPACT_AUDIO_ONLY_PATH}"
                    );
                }

                audioOnly = AssetDatabase.LoadAssetAtPath<MaterialSoundsAsset>(
                    IMPACT_AUDIO_ONLY_PATH
                );
            }

            if (audioOnly == null)
            {
                throw new InvalidOperationException(
                    $"Could not load local impact audio: {IMPACT_AUDIO_ONLY_PATH}"
                );
            }

            // Synchronize the sample's authored audio, volume and surface entries while
            // preserving this asset's own GUID. Visual impact prefabs are then removed
            // from both the default entry and any future material-specific entries.
            EditorUtility.CopySerialized(source, audioOnly);
            audioOnly.name = GetAssetMainName(IMPACT_AUDIO_ONLY_PATH);
            ClearImpactVisual(audioOnly.MaterialSounds.DefaultSounds);
            foreach (MaterialSoundTexture materialSound in
                     audioOnly.MaterialSounds.MaterialSounds)
            {
                ClearImpactVisual(materialSound);
            }

            EditorUtility.SetDirty(audioOnly);
            return audioOnly;
        }

        private static void ClearImpactVisual(IMaterialSound materialSound)
        {
            if (materialSound?.Impact == null) return;
            SetField(materialSound.Impact, "m_Prefab", null);
        }

        private static void RepairShooterSampleMaterialsForUrp()
        {
            Shader litShader = Shader.Find(URP_LIT_SHADER);
            Shader unlitShader = Shader.Find(URP_UNLIT_SHADER);
            Shader particleShader = Shader.Find(URP_PARTICLE_SHADER);
            if (litShader == null || unlitShader == null || particleShader == null)
            {
                throw new InvalidOperationException(
                    "Required URP Lit, Unlit or Particles/Unlit shader is missing."
                );
            }

            string modelRoot = SAMPLE_ROOT + "/Models";
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { modelRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null ||
                    material.shader != null &&
                    material.shader.name != "Standard" &&
                    material.shader.isSupported)
                {
                    continue;
                }

                UpgradeOpaqueMaterial(material, litShader);
            }

            string materialRoot = SAMPLE_ROOT + "/Materials";
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { materialRoot }))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    AssetDatabase.GUIDToAssetPath(guid)
                );
                if (material == null) continue;
                UpgradeTransparentMaterial(material, particleShader);
            }

            string effectRoot = SAMPLE_ROOT + "/Effects";
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { effectRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;

                if (path.EndsWith("/Scope/Sniper_RenderTexture.mat", StringComparison.Ordinal))
                    UpgradeOpaqueMaterial(material, unlitShader);
                else
                    UpgradeTransparentMaterial(material, particleShader);
            }

            UpgradeRequiredMaterial(
                CHARGE_GRENADE_FADE_MATERIAL,
                particleShader,
                true
            );
            UpgradeRequiredMaterial(
                CHARGE_GRENADE_GUIDE_MATERIAL,
                litShader,
                false
            );
            UpgradeRequiredMaterial(
                CHARGE_GRENADE_JOINTS_MATERIAL,
                litShader,
                false
            );
            UpgradeRequiredMaterial(
                CHARGE_GRENADE_SURFACE_MATERIAL,
                litShader,
                false
            );
            UpgradeRequiredMaterial(LOW_WEAPON_MATERIAL, litShader, false);

            AssetDatabase.SaveAssets();
        }

        private static void UpgradeRequiredMaterial(
            string path,
            Shader shader,
            bool transparent)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new InvalidOperationException(
                    $"Required material is missing: {path}"
                );
            }

            if (transparent)
                UpgradeTransparentMaterial(material, shader);
            else
                UpgradeOpaqueMaterial(material, shader);
        }

        private static void UpgradeOpaqueMaterial(Material material, Shader shader)
        {
            Texture texture = material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.HasProperty("_MainTex")
                    ? material.GetTexture("_MainTex")
                    : null;
            Color color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color")
                    ? material.GetColor("_Color")
                    : Color.white;
            float metallic = material.HasProperty("_Metallic")
                ? material.GetFloat("_Metallic")
                : 0f;
            float smoothness = material.HasProperty("_Smoothness")
                ? material.GetFloat("_Smoothness")
                : material.HasProperty("_Glossiness")
                    ? material.GetFloat("_Glossiness")
                    : 0.5f;

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
        }

        private static void UpgradeTransparentMaterial(Material material, Shader shader)
        {
            Texture texture = material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.HasProperty("_MainTex")
                    ? material.GetTexture("_MainTex")
                    : null;
            Color color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color")
                    ? material.GetColor("_Color")
                    : Color.white;
            bool additive = material.HasProperty("_DstBlend") &&
                            Mathf.Approximately(
                                material.GetFloat("_DstBlend"),
                                (float) UnityEngine.Rendering.BlendMode.One
                            );

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat(
                "_SrcBlend",
                (float) UnityEngine.Rendering.BlendMode.SrcAlpha
            );
            material.SetFloat(
                "_DstBlend",
                (float) (additive
                    ? UnityEngine.Rendering.BlendMode.One
                    : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)
            );
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
        }

        private static UrpRenderingAssets CreateOrRepairUrpRenderingAssets()
        {
            Shader particleShader = Shader.Find(URP_PARTICLE_SHADER);
            if (particleShader == null)
            {
                throw new InvalidOperationException(
                    $"URP particle shader is missing: {URP_PARTICLE_SHADER}"
                );
            }

            Material tracer = CreateOrRepairUrpMaterial(
                SOURCE_TRACER_MATERIAL,
                TRACER_MATERIAL_PATH,
                particleShader,
                false
            );
            Material muzzle = CreateOrRepairUrpMaterial(
                SOURCE_MUZZLE_MATERIAL,
                MUZZLE_MATERIAL_PATH,
                particleShader,
                true
            );
            Material impact = CreateOrRepairUrpMaterial(
                SOURCE_IMPACT_MATERIAL,
                IMPACT_MATERIAL_PATH,
                particleShader,
                false
            );
            Material explosion = CreateOrRepairUrpMaterial(
                SOURCE_EXPLOSION_MATERIAL,
                EXPLOSION_MATERIAL_PATH,
                particleShader,
                true
            );

            GameObject muzzleEffect = CopyAndRepairEffectPrefab(
                SOURCE_MUZZLE_EFFECT,
                MUZZLE_EFFECT_PATH,
                _ => muzzle
            );
            GameObject impactEffect = CopyAndRepairEffectPrefab(
                SOURCE_IMPACT_EFFECT,
                IMPACT_EFFECT_PATH,
                _ => impact
            );
            GameObject explosionEffect = CopyAndRepairEffectPrefab(
                SOURCE_EXPLOSION_EFFECT,
                EXPLOSION_EFFECT_PATH,
                source => source != null && source.name == "Flame_Smoke"
                    ? explosion
                    : impact
            );
            GameObject bloodHitEffect = AssetDatabase.LoadAssetAtPath<GameObject>(
                SOURCE_BLOOD_HIT_EFFECT
            );
            if (bloodHitEffect == null)
            {
                throw new InvalidOperationException(
                    $"BloodFactory hit effect is missing: {SOURCE_BLOOD_HIT_EFFECT}"
                );
            }

            return new UrpRenderingAssets
            {
                Tracer = tracer,
                MuzzleEffect = muzzleEffect,
                ImpactEffect = impactEffect,
                ExplosionEffect = explosionEffect,
                SourceExplosionEffect = AssetDatabase.LoadAssetAtPath<GameObject>(
                    SOURCE_EXPLOSION_EFFECT
                ),
                BloodHitEffect = bloodHitEffect
            };
        }

        private static void CreateOrRepairVehicleBulletDecalAssets()
        {
            ConfigureBulletDecalTexture(VEHICLE_BULLET_TEXTURE_PATH, 256);
            ConfigureBulletDecalTexture(WALL_BULLET_TEXTURE_PATH, 256);
            ConfigureBulletDecalTexture(RPG_EXPLOSION_DECAL_TEXTURE_PATH, 512);
            ConfigureBulletDecalTexture(
                RPG_EXPLOSION_VEHICLE_DECAL_TEXTURE_PATH,
                512
            );

            AssetDatabase.ImportAsset(
                VEHICLE_BULLET_SHADER_PATH,
                ImportAssetOptions.ForceSynchronousImport
            );
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                VEHICLE_BULLET_SHADER_PATH
            );
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Bullet decal mobile URP shader could not be loaded"
                );
            }

            CreateOrRepairBulletDecalMaterial(
                VEHICLE_BULLET_TEXTURE_PATH,
                VEHICLE_BULLET_MATERIAL_PATH,
                "Vehicle Bullet Hole URP",
                shader
            );
            CreateOrRepairBulletDecalMaterial(
                WALL_BULLET_TEXTURE_PATH,
                WALL_BULLET_MATERIAL_PATH,
                "Wall Bullet Hole URP",
                shader
            );
            CreateOrRepairBulletDecalMaterial(
                RPG_EXPLOSION_DECAL_TEXTURE_PATH,
                RPG_EXPLOSION_DECAL_MATERIAL_PATH,
                "RPG Explosion Scorch URP",
                shader,
                0.82f,
                0.66f,
                0.326f
            );
            CreateOrRepairBulletDecalMaterial(
                RPG_EXPLOSION_VEHICLE_DECAL_TEXTURE_PATH,
                RPG_EXPLOSION_VEHICLE_DECAL_MATERIAL_PATH,
                "RPG Explosion Vehicle Scorch URP",
                shader,
                0.7f,
                0.56f,
                0.348f
            );
        }

        private static void ConfigureBulletDecalTexture(
            string texturePath,
            int maxTextureSize)
        {
            AssetDatabase.ImportAsset(
                texturePath,
                ImportAssetOptions.ForceSynchronousImport
            );
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
            {
                throw new InvalidOperationException(
                    $"Bullet decal texture is missing: {texturePath}"
                );
            }

            bool importerChanged =
                importer.textureType != TextureImporterType.Default ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                !importer.alphaIsTransparency ||
                !importer.mipmapEnabled ||
                importer.maxTextureSize != maxTextureSize ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.anisoLevel != 1 ||
                importer.isReadable ||
                importer.textureCompression != TextureImporterCompression.Compressed;

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxTextureSize;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            if (importerChanged) importer.SaveAndReimport();
        }

        private static void CreateOrRepairBulletDecalMaterial(
            string texturePath,
            string materialPath,
            string materialName,
            Shader shader,
            float sootStrength = 0f,
            float sootOpacity = 0f,
            float impactCoreScale = 1f)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
                throw new InvalidOperationException($"Could not load texture: {texturePath}");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.name = materialName;
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_AlphaCutoff", 0.025f);
            material.SetFloat("_SootStrength", Mathf.Clamp01(sootStrength));
            material.SetFloat("_SootOpacity", Mathf.Clamp01(sootOpacity));
            material.SetFloat(
                "_ImpactCoreScale",
                Mathf.Clamp(impactCoreScale, 0.25f, 1f)
            );
            material.enableInstancing = true;
            material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent + 10;
            EditorUtility.SetDirty(material);
        }

        private static Material CreateOrRepairUrpMaterial(
            string sourcePath,
            string destinationPath,
            Shader shader,
            bool additive)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
                throw new InvalidOperationException($"Missing source material: {sourcePath}");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(destinationPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, destinationPath);
            }

            Texture texture = source.HasProperty("_BaseMap")
                ? source.GetTexture("_BaseMap")
                : null;
            if (texture == null && source.HasProperty("_MainTex"))
                texture = source.GetTexture("_MainTex");

            Color color = source.HasProperty("_BaseColor")
                ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color")
                    ? source.GetColor("_Color")
                    : Color.white;

            material.shader = shader;
            material.name = System.IO.Path.GetFileNameWithoutExtension(destinationPath);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(
                "_DstBlend",
                (float) (additive
                    ? UnityEngine.Rendering.BlendMode.One
                    : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)
            );
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CopyAndRepairEffectPrefab(
            string sourcePath,
            string destinationPath,
            Func<Material, Material> selectMaterial)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) == null &&
                !AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                throw new InvalidOperationException(
                    $"Could not copy Shooter effect prefab: {sourcePath}"
                );
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(destinationPath);
            try
            {
                if (destinationPath == IMPACT_EFFECT_PATH)
                {
                    // Hit_Gun contains a five-second Decal GameObject and a short Dust
                    // particle. Persistent ground/wall marks are handled by the fixed-size
                    // instanced ring buffer, so only copy Dust into the GC2 pooled prefab.
                    for (int i = contents.transform.childCount - 1; i >= 0; --i)
                    {
                        UnityEngine.Object.DestroyImmediate(
                            contents.transform.GetChild(i).gameObject
                        );
                    }

                    GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                    Transform sourceDust = source != null
                        ? source.transform.Find("Dust")
                        : null;
                    if (sourceDust == null)
                    {
                        throw new InvalidOperationException(
                            $"Shooter impact Dust is missing: {sourcePath}"
                        );
                    }

                    GameObject dust = UnityEngine.Object.Instantiate(sourceDust.gameObject);
                    dust.name = sourceDust.name;
                    dust.transform.SetParent(contents.transform, false);
                }

                foreach (Renderer renderer in contents.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; ++i)
                        materials[i] = selectMaterial(materials[i]);
                    renderer.sharedMaterials = materials;
                }

                foreach (ParticleSystem particle in
                         contents.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = particle.main;
                    main.maxParticles = GetEffectParticleBudget(
                        destinationPath,
                        particle.gameObject.name
                    );
                }

                PrefabUtility.SaveAsPrefabAsset(contents, destinationPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);
        }

        private static PropertyGetInstantiate CreateInstantiateReference(
            GameObject prefab,
            bool usePooling,
            int size,
            bool hasDuration,
            float duration)
        {
            return new PropertyGetInstantiate(new GetGameObjectInstance(prefab))
            {
                usePooling = usePooling,
                size = size,
                hasDuration = hasDuration,
                duration = duration
            };
        }

        private static PropertyGetInstantiate CreateSurfaceImpactReference(
            GameObject prefab,
            bool usePooling,
            int size,
            bool hasDuration,
            float duration)
        {
            return new PropertyGetInstantiate(
                new GetGameObjectFranklinSurfaceImpact(prefab)
            )
            {
                usePooling = usePooling,
                size = size,
                hasDuration = hasDuration,
                duration = duration
            };
        }

        private static void ConfigureRpgAudioImporters()
        {
            foreach (string path in RPG_AUDIO_PATHS)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                    throw new InvalidOperationException("RPG-7 audio asset is missing: " + path);

                bool changed = false;
                if (!importer.forceToMono)
                {
                    importer.forceToMono = true;
                    changed = true;
                }
                if (importer.loadInBackground)
                {
                    importer.loadInBackground = false;
                    changed = true;
                }
                if (importer.ambisonic)
                {
                    importer.ambisonic = false;
                    changed = true;
                }

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    changed = true;
                }
                if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
                {
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    changed = true;
                }
                if (!Mathf.Approximately(settings.quality, 0.5f))
                {
                    settings.quality = 0.5f;
                    changed = true;
                }
                if (settings.sampleRateSetting != AudioSampleRateSetting.OptimizeSampleRate)
                {
                    settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                    changed = true;
                }
                if (!settings.preloadAudioData)
                {
                    settings.preloadAudioData = true;
                    changed = true;
                }

                if (!changed) continue;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static bool NeedsRpgAudioImporterRepair()
        {
            foreach (string path in RPG_AUDIO_PATHS)
            {
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                    return true;

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (!importer.forceToMono || importer.loadInBackground || importer.ambisonic ||
                    settings.loadType != AudioClipLoadType.DecompressOnLoad ||
                    settings.compressionFormat != AudioCompressionFormat.Vorbis ||
                    !Mathf.Approximately(settings.quality, 0.5f) ||
                    settings.sampleRateSetting != AudioSampleRateSetting.OptimizeSampleRate ||
                    !settings.preloadAudioData)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ReplaceObjectReferences(
            UnityEngine.Object asset,
            UnityEngine.Object source,
            UnityEngine.Object replacement)
        {
            SerializedObject serialized = new(asset);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue != source)
                {
                    continue;
                }

                property.objectReferenceValue = replacement;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            EnsureFolder(ROOT, "Resources");
            EnsureFolder(ROOT, "Shaders");
            EnsureFolder(ROOT + "/Resources", "FranklinShooter");
            EnsureFolder(RESOURCE_ROOT, "Weapons");
            EnsureFolder(RESOURCE_ROOT, "Ammo");
            EnsureFolder(RESOURCE_ROOT, "Projectiles");
            EnsureFolder(RESOURCE_ROOT, "Animations");
            EnsureFolder(RESOURCE_ROOT, "Sights");
            EnsureFolder(RESOURCE_ROOT + "/Sights", "Bike");
            EnsureFolder(RESOURCE_ROOT + "/Sights", "Throwable");
            EnsureFolder(RESOURCE_ROOT + "/Sights", "RPG7");
            EnsureFolder(RESOURCE_ROOT, "MaterialSounds");
            EnsureFolder(RESOURCE_ROOT, "Materials");
            EnsureFolder(RESOURCE_ROOT, "Effects");
            EnsureFolder(RESOURCE_ROOT, "Textures");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetBaseField(object target, string name, object value)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, FIELD_FLAGS);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            throw new MissingFieldException(target.GetType().FullName, name);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, FIELD_FLAGS);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            field.SetValue(target, value);
        }

        private static void SetEnumField(object target, string name, int value)
        {
            FieldInfo field = target.GetType().GetField(name, FIELD_FLAGS);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            if (!field.FieldType.IsEnum)
                throw new InvalidOperationException($"{target.GetType().Name}.{name} is not an enum");
            field.SetValue(target, Enum.ToObject(field.FieldType, value));
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, FIELD_FLAGS);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            return field.GetValue(target);
        }

        private static object GetBaseField(object target, string name)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, FIELD_FLAGS);
                if (field != null) return field.GetValue(target);
                type = type.BaseType;
            }

            throw new MissingFieldException(target.GetType().FullName, name);
        }
    }
}
