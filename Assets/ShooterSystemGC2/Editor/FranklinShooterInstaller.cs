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
        private const string SHOOTER_LOCOMOTION_PATH =
            ANIMATION_ROOT + "/Franklin Shooter Upper Body Locomotion.asset";
        private const string MATERIAL_ROOT = RESOURCE_ROOT + "/Materials";
        private const string EFFECT_ROOT = RESOURCE_ROOT + "/Effects";
        private const string TEXTURE_ROOT = RESOURCE_ROOT + "/Textures";
        private const string VEHICLE_BULLET_TEXTURE_PATH =
            TEXTURE_ROOT + "/vehicle-bullet-hole.png";
        private const string VEHICLE_BULLET_MATERIAL_PATH =
            MATERIAL_ROOT + "/Vehicle Bullet Hole URP.mat";
        private const string WALL_BULLET_TEXTURE_PATH =
            TEXTURE_ROOT + "/wall-bullet-hole.png";
        private const string WALL_BULLET_MATERIAL_PATH =
            MATERIAL_ROOT + "/Wall Bullet Hole URP.mat";
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
        private const string SOURCE_BLOOD_HIT_EFFECT =
            "Assets/PampelGames/BloodFactory/Content/Prefabs/Splash/BloodSplash01.prefab";
        private const string SOURCE_SNIPER_PROJECTILE =
            SAMPLE_ROOT + "/Prefabs/Sniper_Projectile.prefab";
        private const float BLOOD_HIT_EFFECT_LIFETIME = 4f;
        private const int SURFACE_IMPACT_POOL_SIZE = 12;
        private const float SURFACE_IMPACT_POOL_DURATION = 1f;

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
                bool useMuzzleEffect = true)
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
            new("rpg7", "RPG-7", "Heavy Weapon", "Grenade", "RPG7", "rpg7", 1,
                new Vector3(-0.06f, 0.12f, 0.16f), new Vector3(-90f, 0f, 90f),
                recoilY: new Vector2(1f, 2f), vehicleDamage: 20f,
                characterDamage: 100f, headshotMultiplier: 2f, helmetImpulse: 20f,
                armorAbsorption: 0.1f, useMuzzleEffect: false)
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
            if (AssetDatabase.LoadAssetAtPath<FranklinShooterCatalog>(CATALOG_PATH) != null &&
                !NeedsRenderingRepair() &&
                !NeedsShooterLocomotionRepair() &&
                !NeedsShooterDamageRepair() &&
                !NeedsShooterLayerMaskRepair() &&
                !NeedsCharacterImpactFilterRepair() &&
                !NeedsTracerVisualRepair() &&
                !NeedsPlayerArmorRepair() &&
                !NeedsPlayerFirstPersonCameraRepair() &&
                !NeedsVehicleBulletDecalRepair() &&
                !NeedsVehicleImpactForceRepair() &&
                !NeedsAimSightRepair()) return;
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
                EnsurePlayerArmor();
                EnsurePlayerFirstPersonCameraManager();
                RepairShooterSampleMaterialsForUrp();
                ConfigureUiSprites();
                CreateOrRepairVehicleBulletDecalAssets();
                AvatarMask upperBodyMask = CreateOrRepairUpperBodyMask();
                StateBasicLocomotion shooterLocomotion =
                    CreateOrRepairShooterLocomotion();
                CreateOrRepairBikeDriverMask();
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
                        renderingAssets
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
                        "Franklin Shooter GC2 installed: 8 Weapons Low props, GC2 weapon assets, " +
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
            UrpRenderingAssets renderingAssets)
        {
            string sourcePath = $"{SAMPLE_ROOT}/Weapons/{definition.SourceWeapon}_Weapon.asset";
            string destinationPath = $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset";
            ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(destinationPath);
            bool created = false;

            if (weapon == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    throw new InvalidOperationException($"Could not copy GC2 weapon template: {sourcePath}");
                AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
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

            // Keep the exact GC2 Shooter_Locomotion linked on every generated weapon. The
            // runtime still controls when layer 7 is visible, but the weapon can no longer
            // silently lose its locomotion dependency after an installer/asset refresh.
            SetField(weapon, "m_State", new StateData(shooterLocomotion));
            SetField(
                weapon,
                "m_Layer",
                new PropertyGetInteger(SHOOTER_LOCOMOTION_LAYER)
            );
            RepairAimAdsSightReference(weapon, definition.SourceWeapon, upperBodyMask);
            RepairWeaponSightLayerMasks(weapon);
            SetField(weapon.Fire, "m_FireAvatarMask", upperBodyMask);
            // GC2's Fire Force applies a Rigidbody impulse before on-hit instructions.
            // Vehicle damage is health-only, so disable that impulse on generated weapons.
            SetField(weapon.Fire, "m_Force", new EnablerFloat(false, 0f));
            SetDamageInstructions(weapon, definition, renderingAssets.BloodHitEffect);
            RepairProjectileLayerMask(weapon);

            if (definition.UseMuzzleEffect && renderingAssets.MuzzleEffect != null)
            {
                SetField(
                    weapon.Fire,
                    "m_MuzzleEffect",
                    CreateInstantiateReference(
                        renderingAssets.MuzzleEffect,
                        true,
                        5,
                        true,
                        3f
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

            Shot projectileShot = (Shot) GetField(weapon.Projectile, "m_Shot");
            if (projectileShot?.Value is ShotRaycast raycast)
            {
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

            if (definition.Id == "rpg7" &&
                renderingAssets.SourceExplosionEffect != null &&
                renderingAssets.ExplosionEffect != null)
            {
                ReplaceObjectReferences(
                    weapon,
                    renderingAssets.SourceExplosionEffect,
                    renderingAssets.ExplosionEffect
                );
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
            weapon.name = definition.DisplayName.Replace(" ", "_") + "_Weapon";
            EditorUtility.SetDirty(weapon);
            return weapon;
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

        private static void EnsurePlayerArmor()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            if (prefab == null) return;
            if (prefab.GetComponent<FranklinArmor>() != null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);
            try
            {
                FranklinArmor armor = root.GetComponent<FranklinArmor>() ??
                                      root.AddComponent<FranklinArmor>();
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
                    managerTransform.GetComponent<FranklinFirstPersonCameraManager>() ??
                    managerTransform.gameObject.AddComponent<FranklinFirstPersonCameraManager>();
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

                bool changed = importer.textureType != TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Single ||
                               importer.alphaIsTransparency == false ||
                               importer.mipmapEnabled;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                if (changed) importer.SaveAndReimport();
            }
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

                AssetDatabase.ImportAsset(
                    SHOOTER_LOCOMOTION_PATH,
                    ImportAssetOptions.ForceSynchronousImport
                );
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
            locomotion.name = "Shooter_Locomotion";
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

        private static bool NeedsRenderingRepair()
        {
            if (HasLegacyShooterSampleMaterials()) return true;

            return AssetDatabase.LoadAssetAtPath<Sprite>(CAUTIOUS_WALK_ICON_PATH) == null ||
                   AssetDatabase.LoadAssetAtPath<Sprite>(FIRST_PERSON_ICON_PATH) == null ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_BLOOD_HIT_EFFECT) == null ||
                   AssetDatabase.LoadAssetAtPath<AvatarMask>(BIKE_DRIVER_MASK_PATH) == null ||
                   !MaterialUsesShader(TRACER_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   !MaterialUsesShader(MUZZLE_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   !MaterialUsesShader(IMPACT_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   !MaterialUsesShader(EXPLOSION_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(MUZZLE_EFFECT_PATH) == null ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_EFFECT_PATH) == null ||
                   ImpactEffectHasPlanarDecal() ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(EXPLOSION_EFFECT_PATH) == null;
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
                       shader
                   ) ||
                   BulletDecalAssetsNeedRepair(
                       WALL_BULLET_TEXTURE_PATH,
                       WALL_BULLET_MATERIAL_PATH,
                       shader
                   );
        }

        private static bool BulletDecalAssetsNeedRepair(
            string texturePath,
            string materialPath,
            Shader shader)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (texture == null || material == null || material.shader != shader ||
                material.GetTexture("_BaseMap") != texture || !material.enableInstancing)
            {
                return true;
            }

            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
                return true;

            return importer.textureType != TextureImporterType.Default ||
                   importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                   !importer.alphaIsTransparency ||
                   !importer.mipmapEnabled ||
                   importer.maxTextureSize != 256 ||
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
                locomotion.name != "Shooter_Locomotion")
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
                if (weaponState.State != locomotion) return true;
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
            foreach (Definition definition in DEFINITIONS)
            {
                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null) return true;

                Shot shot = (Shot) GetField(weapon.Projectile, "m_Shot");
                if (shot?.Value is not ShotRaycast raycast) continue;

                PropertyGetColor color = (PropertyGetColor) GetField(raycast, "m_Color");
                PropertyGetDecimal width = (PropertyGetDecimal) GetField(raycast, "m_Width");
                float expectedWidth = definition.SourceWeapon == "AK"
                    ? RAYCAST_TRACER_WIDTH_RIFLE
                    : RAYCAST_TRACER_WIDTH_LIGHT;
                if (color == null || color.EditorValue != Color.white ||
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

        private static bool NeedsAimSightRepair()
        {
            IdString aimAdsId = new("aim-ads");
            AvatarMask upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                UPPER_BODY_MASK_PATH
            );
            foreach (Definition definition in DEFINITIONS)
            {
                ShooterWeapon weapon = AssetDatabase.LoadAssetAtPath<ShooterWeapon>(
                    $"{RESOURCE_ROOT}/Weapons/{definition.Id}.asset"
                );
                if (weapon == null || WeaponSightsNeedLayerMaskRepair(weapon))
                    return true;

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

            AssetDatabase.SaveAssets();
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
            ConfigureBulletDecalTexture(VEHICLE_BULLET_TEXTURE_PATH);
            ConfigureBulletDecalTexture(WALL_BULLET_TEXTURE_PATH);

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
        }

        private static void ConfigureBulletDecalTexture(string texturePath)
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
                importer.maxTextureSize != 256 ||
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
            importer.maxTextureSize = 256;
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
            Shader shader)
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
            EnsureFolder(RESOURCE_ROOT, "Animations");
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
