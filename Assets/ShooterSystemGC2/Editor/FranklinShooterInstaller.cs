using System;
using System.Collections.Generic;
using System.Reflection;
using FranklinGame.Shooter;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
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
        private const string ANIMATION_ROOT = RESOURCE_ROOT + "/Animations";
        private const string UPPER_BODY_MASK_PATH =
            ANIMATION_ROOT + "/Franklin Shooter Upper Body.mask";
        private const string BIKE_DRIVER_MASK_PATH =
            ANIMATION_ROOT + "/Franklin Bike Driver Seat And Left Hand.mask";
        private const string SHOOTER_LOCOMOTION_PATH =
            ANIMATION_ROOT + "/Franklin Shooter Upper Body Locomotion.asset";
        private const string MATERIAL_ROOT = RESOURCE_ROOT + "/Materials";
        private const string EFFECT_ROOT = RESOURCE_ROOT + "/Effects";
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
        private const int SHOOTER_LOCOMOTION_LAYER = 7;

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
            "Assets/Plugins/GameCreator/Installs/GameCreator.Characters@1.8.25/" +
            "Assets/Materials/Poof_Default.mat";
        private const string SOURCE_IMPACT_EFFECT =
            "Assets/Plugins/GameCreator/Installs/GameCreator.Characters@1.8.25/" +
            "Assets/Particles/FX_Footstep.prefab";

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
            public readonly bool UseShooterLocomotion;

            public Definition(
                string id, string displayName, string category,
                string sourceWeapon, string lowPrefab, string icon,
                int startingMagazine,
                Vector3? position = null, Vector3? rotation = null, Vector3? scale = null,
                Vector2? recoilX = null, Vector2? recoilY = null,
                bool useShooterLocomotion = true)
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
                this.UseShooterLocomotion = useShooterLocomotion;
            }
        }

        private sealed class UrpRenderingAssets
        {
            public Material Tracer;
            public GameObject MuzzleEffect;
            public GameObject ImpactEffect;
            public GameObject ExplosionEffect;
            public GameObject SourceExplosionEffect;
        }

        private static readonly Definition[] DEFINITIONS =
        {
            new("m1911", "M1911", "Pistol", "Pistol", "M1911", "m1911", 10),
            new("uzi", "UZI", "Submachine Gun", "AK", "Uzi", "uzi", 30),
            new("ak74", "AK-74", "Assault Rifle", "AK", "AK74", "ak74", 30),
            new("m4", "M4 Carbine", "Assault Rifle", "AK", "M4_8", "m4", 30),
            new("benelli-m4", "Benelli M4", "Shotgun", "Shotgun", "Bennelli_M4", "benelli-m4", 8,
                recoilY: new Vector2(5f, 7.5f)),
            new("m249", "M249", "Light Machine Gun", "AK", "M249", "m249", 30),
            new("m107", "M107", "Sniper Rifle", "Sniper", "M107", "m107", 5,
                recoilX: new Vector2(-2.5f, 2.5f), recoilY: new Vector2(-2.5f, 2.5f)),
            new("rpg7", "RPG-7", "Heavy Weapon", "Grenade", "RPG7", "rpg7", 1,
                new Vector3(-0.06f, 0.12f, 0.16f), new Vector3(-90f, 0f, 90f),
                recoilY: new Vector2(1f, 2f), useShooterLocomotion: false)
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
                !NeedsRenderingRepair()) return;
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
                RepairShooterSampleMaterialsForUrp();
                ConfigureUiSprites();
                StateBasicLocomotion shooterLocomotion =
                    CreateOrRepairShooterLocomotion();
                CreateOrRepairBikeDriverMask();
                UrpRenderingAssets renderingAssets = CreateOrRepairUrpRenderingAssets();

                List<FranklinShooterCatalog.Entry> entries = new(DEFINITIONS.Length);
                foreach (Definition definition in DEFINITIONS)
                {
                    ShooterWeapon weapon = CreateOrRepairWeapon(
                        definition,
                        shooterLocomotion,
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
                    SetField(entry, "m_LocalPosition", definition.Position);
                    SetField(entry, "m_LocalRotation", definition.Rotation);
                    SetField(entry, "m_LocalScale", definition.Scale);
                    entries.Add(entry);
                }

                FranklinShooterCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<FranklinShooterCatalog>(CATALOG_PATH);
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

            StateData weaponState = definition.UseShooterLocomotion
                ? new StateData(shooterLocomotion)
                : new StateData(StateData.StateType.State);
            SetField(weapon, "m_State", weaponState);
            SetField(
                weapon,
                "m_Layer",
                new PropertyGetInteger(SHOOTER_LOCOMOTION_LAYER)
            );

            if (definition.UseShooterLocomotion && renderingAssets.MuzzleEffect != null)
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

            if (definition.SourceWeapon == "AK" && renderingAssets.ImpactEffect != null)
            {
                SetField(
                    weapon.Projectile,
                    "m_ImpactEffect",
                    CreateInstantiateReference(
                        renderingAssets.ImpactEffect,
                        true,
                        5,
                        true,
                        5f
                    )
                );
            }

            Shot projectileShot = (Shot) GetField(weapon.Projectile, "m_Shot");
            if (projectileShot?.Value is ShotRaycast raycast &&
                GetField(raycast, "m_LineMaterial") is PropertyGetMaterial lineMaterial &&
                lineMaterial.EditorValue != null)
            {
                SetField(
                    raycast,
                    "m_LineMaterial",
                    new PropertyGetMaterial(renderingAssets.Tracer)
                );
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

        private static StateBasicLocomotion CreateOrRepairShooterLocomotion()
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
                upperBodyMask;
            serializedLocomotion.FindProperty("m_Properties.m_Speed.m_IsEnabled")
                .boolValue = false;
            serializedLocomotion.ApplyModifiedPropertiesWithoutUndo();
            locomotion.name = "Franklin Shooter Upper Body Locomotion";
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
                   AssetDatabase.LoadAssetAtPath<AvatarMask>(BIKE_DRIVER_MASK_PATH) == null ||
                   !MaterialUsesShader(TRACER_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   !MaterialUsesShader(MUZZLE_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   !MaterialUsesShader(IMPACT_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   !MaterialUsesShader(EXPLOSION_MATERIAL_PATH, URP_PARTICLE_SHADER) ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(MUZZLE_EFFECT_PATH) == null ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_EFFECT_PATH) == null ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(EXPLOSION_EFFECT_PATH) == null;
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

            return new UrpRenderingAssets
            {
                Tracer = tracer,
                MuzzleEffect = muzzleEffect,
                ImpactEffect = impactEffect,
                ExplosionEffect = explosionEffect,
                SourceExplosionEffect = AssetDatabase.LoadAssetAtPath<GameObject>(
                    SOURCE_EXPLOSION_EFFECT
                )
            };
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

            Texture texture = source.HasProperty("_MainTex")
                ? source.GetTexture("_MainTex")
                : null;
            Color color = source.HasProperty("_Color")
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
            EnsureFolder(ROOT + "/Resources", "FranklinShooter");
            EnsureFolder(RESOURCE_ROOT, "Weapons");
            EnsureFolder(RESOURCE_ROOT, "Animations");
            EnsureFolder(RESOURCE_ROOT, "Materials");
            EnsureFolder(RESOURCE_ROOT, "Effects");
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
    }
}
