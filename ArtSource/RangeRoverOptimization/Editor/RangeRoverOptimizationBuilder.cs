using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

public static class RangeRoverOptimizationBuilder
{
    private const string RootFolder =
        "Assets/Ash Assets/VehicleCollection/Range_Rover_Material_Fixed";
    private const string ModelPath = RootFolder + "/Range_Rover_Model.obj";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string BodyMaterialPath = MaterialsFolder + "/M_RangeRover_BodyGloss.mat";
    private const string BlackMaterialPath = MaterialsFolder + "/M_RangeRover_BlackGloss.mat";
    private const string TailMaterialPath = MaterialsFolder + "/M_RangeRover_TailLightRed.mat";
    private const string MirrorMaterialPath = MaterialsFolder + "/M_RangeRover_MirrorAlpha.mat";
    private const string WindowGlassMaterialPath = MaterialsFolder + "/M_RangeRover_WindowGlass.mat";
    private const string HeadlampGlassMaterialPath = MaterialsFolder + "/M_RangeRover_HeadlampGlass.mat";
    private const string MeshesFolder = RootFolder + "/Meshes_Optimized";
    private const string PrefabPath = RootFolder + "/Range_Rover_Centered_Optimized.prefab";

    private const int SourceRendererCount = 75;
    private const int ExpectedOptimizedRendererCount = 37;

    private static readonly string[] DoorPrefixes = { "Door_FL_", "Door_FR_" };
    private static readonly string[] WheelPrefixes =
    {
        "Wheel_FL_",
        "Wheel_FR_",
        "Wheel_RL_",
        "Wheel_RR_"
    };

    private static readonly Vector3 SourceBoundsCenter =
        new Vector3(0.000001f, 0.57118499f, -1.55982013f);
    private static readonly Vector3 VisualOffset = -SourceBoundsCenter;
    private static readonly Vector3 ExpectedBoundsSize =
        new Vector3(1.3587774f, 1.1523440f, 3.1173237f);

    private static readonly string[] BodyRendererNames =
    {
        "Body_ExteriorPaint",
        "Door_FL_BodyPaint",
        "Door_FR_BodyPaint"
    };

    private static readonly string[] BlackRendererNames =
    {
        "Body_BlackPaint",
        "Door_FL_BlackPaint"
    };

    private static readonly string[] TailRendererNames =
    {
        "Body_GlassLens",
        "RearLamp_RedHousing",
        "RearLamp_RedLens"
    };

    private static readonly string[] MirrorRendererNames =
    {
        "Door_FL_SideMirror",
        "Door_FR_SideMirror",
        "Interior_RearViewMirror"
    };

    private static readonly string[] WindowGlassRendererNames =
    {
        "Body_WindowGlass",
        "Door_FL_WindowGlass",
        "Door_FR_WindowGlass"
    };

    [Serializable]
    private sealed class TextureReport
    {
        public string path;
        public int defaultMaxSize;
        public string defaultCompression;
        public string androidFormat;
        public int androidMaxSize;
        public string iosFormat;
        public int iosMaxSize;
        public bool mipmaps;
        public bool readable;
    }

    [Serializable]
    private sealed class OptimizationReport
    {
        public bool passed;
        public string unityVersion;
        public string modelPath;
        public string prefabPath;
        public string meshCompression;
        public bool modelReadable;
        public string tangentMode;
        public int rendererCount;
        public int meshCount;
        public int uniqueMaterialCount;
        public int sourceRendererCount;
        public int optimizedRendererCount;
        public int sourceEstimatedDrawCalls;
        public int optimizedEstimatedDrawCalls;
        public int combinedDoorMeshCount;
        public int combinedWheelMeshCount;
        public bool frontWheelYawBaked;
        public float frontLeftWheelYawCorrection;
        public float frontRightWheelYawCorrection;
        public int vertexCount;
        public int triangleCount;
        public Vector3 centeredBoundsCenter;
        public Vector3 centeredBoundsSize;
        public Vector3 visualOffset;
        public string bodyMaterial;
        public string blackMaterial;
        public string tailMaterial;
        public string mirrorMaterial;
        public string windowGlassMaterial;
        public string headlampGlassMaterial;
        public string bodyShader;
        public bool bodyClearCoatEnabled;
        public string mirrorBlendMode;
        public int bodyRendererCount;
        public int blackRendererCount;
        public int tailRendererCount;
        public int mirrorRendererCount;
        public int windowGlassRendererCount;
        public float windowGlassAlpha;
        public int headlampGlassRendererCount;
        public float headlampGlassAlpha;
        public TextureReport[] textures;
    }

    private sealed class TextureSpec
    {
        public string Path;
        public int MaxSize;
        public TextureImporterFormat MobileFormat;

        public TextureSpec(string path, int maxSize, TextureImporterFormat mobileFormat)
        {
            Path = path;
            MaxSize = maxSize;
            MobileFormat = mobileFormat;
        }
    }

    private static readonly TextureSpec[] TextureSpecs =
    {
        new TextureSpec(RootFolder + "/Textures/BlackLeather.jpg", 512, TextureImporterFormat.ASTC_6x6),
        new TextureSpec(RootFolder + "/Textures/BrakeDisc.jpg", 256, TextureImporterFormat.ASTC_4x4),
        new TextureSpec(RootFolder + "/Textures/Dashboard.png", 512, TextureImporterFormat.ASTC_4x4),
        new TextureSpec(RootFolder + "/Textures/Lamp.png", 512, TextureImporterFormat.ASTC_4x4),
        new TextureSpec(RootFolder + "/Textures/LightLeather.jpg", 512, TextureImporterFormat.ASTC_6x6),
        new TextureSpec(RootFolder + "/Textures/Wood.jpg", 512, TextureImporterFormat.ASTC_6x6)
    };

    public static void BuildAndValidate()
    {
        try
        {
            Build();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static void Build()
    {
        RequireAsset(ModelPath);
        foreach (TextureSpec texture in TextureSpecs) RequireAsset(texture.Path);

        EnsureFolder(MaterialsFolder);
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        Shader complexLitShader = Shader.Find("Universal Render Pipeline/Complex Lit");
        if (litShader == null) throw new InvalidOperationException("URP Lit shader is unavailable");
        if (complexLitShader == null)
            throw new InvalidOperationException("URP Complex Lit shader is unavailable");

        Material body = LoadOrCreateMaterial(BodyMaterialPath, complexLitShader);
        ConfigureOpaqueLit(
            body,
            new Color(0.674f, 0.674f, 0.674f, 1f),
            metallic: 0.32f,
            smoothness: 0.93f,
            enableClearCoat: true,
            clearCoatMask: 1f,
            clearCoatSmoothness: 0.97f
        );

        Material black = LoadOrCreateMaterial(BlackMaterialPath, complexLitShader);
        ConfigureOpaqueLit(
            black,
            new Color(0.008f, 0.009f, 0.011f, 1f),
            metallic: 0.42f,
            smoothness: 0.90f,
            enableClearCoat: true,
            clearCoatMask: 0.85f,
            clearCoatSmoothness: 0.95f
        );

        Material tail = LoadOrCreateMaterial(TailMaterialPath, litShader);
        ConfigureOpaqueLit(
            tail,
            new Color(0.55f, 0.005f, 0.005f, 1f),
            metallic: 0.05f,
            smoothness: 0.86f,
            enableClearCoat: false,
            clearCoatMask: 0f,
            clearCoatSmoothness: 0f
        );
        tail.SetColor("_EmissionColor", new Color(2.5f, 0.02f, 0.01f, 1f));
        tail.EnableKeyword("_EMISSION");
        tail.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(tail);

        Material mirror = LoadOrCreateMaterial(MirrorMaterialPath, litShader);
        ConfigureTransparentMirror(mirror);

        Material windowGlass = LoadOrCreateMaterial(WindowGlassMaterialPath, litShader);
        ConfigureTransparentWindowGlass(windowGlass);

        Material headlampGlass = LoadOrCreateMaterial(HeadlampGlassMaterialPath, litShader);
        ConfigureTransparentHeadlampGlass(headlampGlass);

        AssetDatabase.SaveAssets();
        ConfigureTextures();
        ConfigureModelImporter(body, black, tail, mirror, windowGlass, headlampGlass);
        CreateCenteredPrefab(black);
        SetModelReadability(false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        OptimizationReport report = Validate();
        WriteReport(report);
        Debug.Log(
            $"RANGE_ROVER_OPTIMIZATION_QA_SUCCESS renderers={report.rendererCount} "
                + $"meshes={report.meshCount} tris={report.triangleCount} "
                + $"center={report.centeredBoundsCenter} compression={report.meshCompression}"
        );
    }

    private static void ConfigureOpaqueLit(
        Material material,
        Color color,
        float metallic,
        float smoothness,
        bool enableClearCoat,
        float clearCoatMask,
        float clearCoatSmoothness
    )
    {
        NormalizeLitMaterial(material);
        material.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(material));
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_WorkflowMode", 1f);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_ClearCoat"))
            material.SetFloat("_ClearCoat", enableClearCoat ? 1f : 0f);
        material.SetFloat("_ClearCoatMask", clearCoatMask);
        material.SetFloat("_ClearCoatSmoothness", clearCoatSmoothness);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
        material.SetFloat("_ZWrite", 1f);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.SetFloat("_SpecularHighlights", 1f);
        BaseShaderGUI.SetupMaterialBlendMode(material);
        LitGUI.SetMaterialKeywords(material);
        material.SetShaderPassEnabled("ShadowCaster", true);
        material.SetShaderPassEnabled("DepthOnly", true);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureTransparentMirror(Material material)
    {
        NormalizeLitMaterial(material);
        Color tint = new Color(0.55f, 0.65f, 0.75f, 0.52f);
        material.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(material));
        material.SetColor("_BaseColor", tint);
        material.SetColor("_Color", tint);
        material.SetFloat("_WorkflowMode", 1f);
        material.SetFloat("_Metallic", 0.68f);
        material.SetFloat("_Smoothness", 0.97f);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_BlendModePreserveSpecular", 1f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.SetFloat("_SpecularHighlights", 1f);
        BaseShaderGUI.SetupMaterialBlendMode(material);
        LitGUI.SetMaterialKeywords(material);
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetShaderPassEnabled("DepthOnly", false);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureTransparentWindowGlass(Material material)
    {
        NormalizeLitMaterial(material);
        Color tint = new Color(0.60f, 0.72f, 0.82f, 0.20f);
        material.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(material));
        material.SetColor("_BaseColor", tint);
        material.SetColor("_Color", tint);
        material.SetFloat("_WorkflowMode", 1f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.90f);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_BlendModePreserveSpecular", 1f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_ReceiveShadows", 0f);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.SetFloat("_SpecularHighlights", 1f);
        BaseShaderGUI.SetupMaterialBlendMode(material);
        LitGUI.SetMaterialKeywords(material);
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetShaderPassEnabled("DepthOnly", false);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureTransparentHeadlampGlass(Material material)
    {
        NormalizeLitMaterial(material);
        Color tint = new Color(0.78f, 0.86f, 0.95f, 0.26f);
        material.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(material));
        material.SetColor("_BaseColor", tint);
        material.SetColor("_Color", tint);
        material.SetFloat("_WorkflowMode", 1f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.94f);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_BlendModePreserveSpecular", 1f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_ReceiveShadows", 0f);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.SetFloat("_SpecularHighlights", 1f);
        BaseShaderGUI.SetupMaterialBlendMode(material);
        LitGUI.SetMaterialKeywords(material);
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetShaderPassEnabled("DepthOnly", false);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static void NormalizeLitMaterial(Material material)
    {
        material.SetTexture("_BaseMap", null);
        material.SetTexture("_MetallicGlossMap", null);
        material.SetTexture("_SpecGlossMap", null);
        material.SetTexture("_BumpMap", null);
        material.SetTexture("_ParallaxMap", null);
        material.SetTexture("_OcclusionMap", null);
        material.SetTexture("_EmissionMap", null);
        if (material.HasProperty("_ClearCoatMap")) material.SetTexture("_ClearCoatMap", null);
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_QueueOffset", 0f);
        material.SetFloat("_ReceiveShadows", 1f);
        material.SetFloat("_BlendModePreserveSpecular", 0f);
        material.DisableKeyword("_SPECULAR_SETUP");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.DisableKeyword("_NORMALMAP");
        material.DisableKeyword("_PARALLAXMAP");
        material.DisableKeyword("_OCCLUSIONMAP");
        material.DisableKeyword("_METALLICSPECGLOSSMAP");
        material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        material.DisableKeyword("_CLEARCOAT");
        material.DisableKeyword("_CLEARCOATMAP");
        material.DisableKeyword("_EMISSION");
    }

    private static void ConfigureModelImporter(
        Material body,
        Material black,
        Material tail,
        Material mirror,
        Material windowGlass,
        Material headlampGlass
    )
    {
        AssetDatabase.ImportAsset(
            ModelPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate
        );

        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Range Rover ModelImporter is missing");

        importer.meshCompression = ModelImporterMeshCompression.Low;
        // Readable only during the editor build so Mesh.CombineMeshes can consume
        // imported submeshes. Build() switches it back off before validation.
        importer.isReadable = true;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
        importer.generateSecondaryUV = false;
        importer.keepQuads = false;
        importer.weldVertices = true;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.preserveHierarchy = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "paint"), body);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "paint_bl"), black);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "red"), tail);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "red2"), tail);
        importer.AddRemap(
            new AssetImporter.SourceAssetIdentifier(typeof(Material), "glass_l.001"),
            tail
        );
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "mirror"), mirror);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "glass"), windowGlass);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "glass_l"), headlampGlass);
        importer.SaveAndReimport();
    }

    private static void ConfigureTextures()
    {
        foreach (TextureSpec spec in TextureSpecs)
        {
            TextureImporter importer = AssetImporter.GetAtPath(spec.Path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"TextureImporter missing: {spec.Path}");

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.isReadable = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 2;
            importer.maxTextureSize = spec.MaxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 70;
            importer.crunchedCompression = false;

            ApplyMobileTextureSettings(importer, "Android", spec.MaxSize, spec.MobileFormat);
            ApplyMobileTextureSettings(importer, "iPhone", spec.MaxSize, spec.MobileFormat);
            importer.SaveAndReimport();
        }
    }

    private static void ApplyMobileTextureSettings(
        TextureImporter importer,
        string platform,
        int maxSize,
        TextureImporterFormat format
    )
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
        settings.name = platform;
        settings.overridden = true;
        settings.maxTextureSize = maxSize;
        settings.format = format;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.compressionQuality = 70;
        importer.SetPlatformTextureSettings(settings);
    }

    private static void CreateCenteredPrefab(Material consolidatedDoorDarkMaterial)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null) throw new InvalidOperationException("Range Rover model asset could not be loaded");

        EnsureFolder(MeshesFolder);

        GameObject root = new GameObject("Range_Rover_Centered_Optimized");
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        PrefabUtility.UnpackPrefabInstance(
            visual,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction
        );
        visual.name = "Range_Rover_Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = VisualOffset;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        CombineRendererPrefix(
            visual,
            "Door_FL_",
            "Door_Left_Combined",
            MeshesFolder + "/RangeRover_Door_Left.asset",
            expectedSourceCount: 10,
            isWheel: false,
            consolidatedDoorDarkMaterial
        );
        CombineRendererPrefix(
            visual,
            "Door_FR_",
            "Door_Right_Combined",
            MeshesFolder + "/RangeRover_Door_Right.asset",
            expectedSourceCount: 10,
            isWheel: false,
            consolidatedDoorDarkMaterial
        );

        foreach (string prefix in WheelPrefixes)
        {
            string wheelId = prefix.Substring("Wheel_".Length).TrimEnd('_');
            CombineRendererPrefix(
                visual,
                prefix,
                $"Wheel_{wheelId}_Combined",
                MeshesFolder + $"/RangeRover_Wheel_{wheelId}.asset",
                expectedSourceCount: 6,
                isWheel: true,
                consolidatedDoorDarkMaterial
            );
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
        UnityEngine.Object.DestroyImmediate(root);
        if (!success || saved == null)
        {
            throw new InvalidOperationException($"Could not save centered prefab: {PrefabPath}");
        }
    }

    private static void CombineRendererPrefix(
        GameObject visual,
        string prefix,
        string outputName,
        string meshAssetPath,
        int expectedSourceCount,
        bool isWheel,
        Material consolidatedDoorDarkMaterial
    )
    {
        List<MeshRenderer> sources = visual
            .GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer => renderer.name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(renderer => renderer.name, StringComparer.Ordinal)
            .ToList();

        if (sources.Count != expectedSourceCount)
            throw new InvalidOperationException(
                $"Expected {expectedSourceCount} renderers for {prefix}, found {sources.Count}"
            );

        Transform parent = sources[0].transform.parent;
        if (sources.Any(renderer => renderer.transform.parent != parent))
            throw new InvalidOperationException($"Combined group {prefix} does not share one parent");

        Bounds groupBounds = CalculateBoundsInSpace(parent, sources.Cast<Renderer>());
        Vector3 pivot = isWheel
            ? groupBounds.center
            : new Vector3(groupBounds.center.x, groupBounds.center.y, groupBounds.max.z);

        GameObject combinedObject = new GameObject(outputName);
        combinedObject.transform.SetParent(parent, false);
        combinedObject.transform.localPosition = pivot;
        combinedObject.transform.localRotation = Quaternion.identity;
        combinedObject.transform.localScale = Vector3.one;

        float bakedYawCorrection = GetBakedWheelYawCorrection(prefix);
        Matrix4x4 bakedGeometryCorrection = Matrix4x4.Rotate(
            Quaternion.Euler(0f, bakedYawCorrection, 0f)
        );

        Dictionary<string, List<CombineInstance>> combinesByMaterial =
            new Dictionary<string, List<CombineInstance>>(StringComparer.Ordinal);
        Dictionary<string, Material> materialByKey =
            new Dictionary<string, Material>(StringComparer.Ordinal);
        List<Mesh> sourceTemporaryMeshes = new List<Mesh>();

        foreach (MeshRenderer source in sources)
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException($"Renderer has no readable mesh: {source.name}");

            Mesh mesh = filter.sharedMesh;
            if (source.name == "Door_FL_SideMirror")
            {
                mesh = CreateFlippedFacingMesh(mesh, source.name);
                sourceTemporaryMeshes.Add(mesh);
            }
            Material[] materials = source.sharedMaterials;
            if (materials.Length == 0)
                throw new InvalidOperationException($"Renderer has no material: {source.name}");

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                Material sourceMaterial = materials[Math.Min(submesh, materials.Length - 1)];
                if (sourceMaterial == null)
                    throw new InvalidOperationException($"Renderer has a null material: {source.name}");

                string materialKey = GetCombinedMaterialKey(
                    sourceMaterial,
                    isWheel,
                    out bool useConsolidatedDoorDark
                );
                Material outputMaterial = useConsolidatedDoorDark
                    ? consolidatedDoorDarkMaterial
                    : sourceMaterial;

                if (!combinesByMaterial.TryGetValue(materialKey, out List<CombineInstance> bucket))
                {
                    bucket = new List<CombineInstance>();
                    combinesByMaterial.Add(materialKey, bucket);
                    materialByKey.Add(materialKey, outputMaterial);
                }

                bucket.Add(
                    new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = submesh,
                        transform = bakedGeometryCorrection
                            * combinedObject.transform.worldToLocalMatrix
                            * source.transform.localToWorldMatrix
                    }
                );
            }
        }

        List<Mesh> temporaryMeshes = new List<Mesh>();
        List<CombineInstance> finalCombines = new List<CombineInstance>();
        List<Material> finalMaterials = new List<Material>();
        foreach (string materialKey in combinesByMaterial.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            Mesh materialMesh = new Mesh
            {
                name = outputName + "_" + materialKey,
                indexFormat = IndexFormat.UInt32
            };
            materialMesh.CombineMeshes(combinesByMaterial[materialKey].ToArray(), true, true, false);
            temporaryMeshes.Add(materialMesh);
            finalCombines.Add(
                new CombineInstance
                {
                    mesh = materialMesh,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                }
            );
            finalMaterials.Add(materialByKey[materialKey]);
        }

        Mesh combinedMesh = new Mesh { name = outputName, indexFormat = IndexFormat.UInt32 };
        combinedMesh.CombineMeshes(finalCombines.ToArray(), false, false, false);
        combinedMesh.RecalculateBounds();
        MeshUtility.Optimize(combinedMesh);
        MeshUtility.SetMeshCompression(combinedMesh, ModelImporterMeshCompression.Low);

        Mesh persistedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
        if (persistedMesh == null)
        {
            AssetDatabase.CreateAsset(combinedMesh, meshAssetPath);
            persistedMesh = combinedMesh;
        }
        else
        {
            EditorUtility.CopySerialized(combinedMesh, persistedMesh);
            UnityEngine.Object.DestroyImmediate(combinedMesh);
            EditorUtility.SetDirty(persistedMesh);
        }

        MeshFilter combinedFilter = combinedObject.AddComponent<MeshFilter>();
        combinedFilter.sharedMesh = persistedMesh;
        MeshRenderer combinedRenderer = combinedObject.AddComponent<MeshRenderer>();
        combinedRenderer.sharedMaterials = finalMaterials.ToArray();
        combinedRenderer.shadowCastingMode = ShadowCastingMode.On;
        combinedRenderer.receiveShadows = true;
        combinedRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        combinedRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

        foreach (MeshRenderer source in sources)
            UnityEngine.Object.DestroyImmediate(source.gameObject);
        foreach (Mesh temporaryMesh in temporaryMeshes)
            UnityEngine.Object.DestroyImmediate(temporaryMesh);
        foreach (Mesh temporaryMesh in sourceTemporaryMeshes)
            UnityEngine.Object.DestroyImmediate(temporaryMesh);

        persistedMesh.UploadMeshData(true);
        EditorUtility.SetDirty(persistedMesh);
    }

    private static float GetBakedWheelYawCorrection(string prefix)
    {
        // The source OBJ has both front wheels steered about 17 degrees in its
        // vertex data even though every Transform is zero. OBJ-to-Unity handedness
        // conversion makes the corrective local yaw negative. Baking this into
        // vertices straightens the wheels while preserving inspector rotation 0/0/0.
        if (prefix == "Wheel_FL_") return -16.87f;
        if (prefix == "Wheel_FR_") return -17.13f;
        return 0f;
    }

    private static Mesh CreateFlippedFacingMesh(Mesh source, string outputName)
    {
        Mesh flipped = UnityEngine.Object.Instantiate(source);
        flipped.name = outputName + "_FacingFixed";

        for (int submesh = 0; submesh < flipped.subMeshCount; submesh++)
        {
            int[] indices = flipped.GetIndices(submesh);
            MeshTopology topology = flipped.GetTopology(submesh);
            if (topology != MeshTopology.Triangles)
                throw new InvalidOperationException(
                    $"Cannot flip non-triangle mirror submesh: {outputName} ({topology})"
                );
            for (int index = 0; index < indices.Length; index += 3)
            {
                int swap = indices[index];
                indices[index] = indices[index + 1];
                indices[index + 1] = swap;
            }
            flipped.SetIndices(indices, topology, submesh, false);
        }

        Vector3[] normals = flipped.normals;
        if (normals != null && normals.Length == flipped.vertexCount)
        {
            for (int index = 0; index < normals.Length; index++) normals[index] = -normals[index];
            flipped.normals = normals;
        }
        else flipped.RecalculateNormals();

        flipped.RecalculateBounds();
        return flipped;
    }

    private static string GetCombinedMaterialKey(
        Material material,
        bool isWheel,
        out bool useConsolidatedDoorDark
    )
    {
        string name = material.name.Replace(" (Instance)", string.Empty).ToLowerInvariant();
        useConsolidatedDoorDark = false;

        if (isWheel)
        {
            if (name == "bhrome" || name == "chrome") return "20_bright_metal";
            if (name == "bhrome_d") return "30_dark_metal";
            if (name == "disc") return "40_brake_disc";
            if (name == "cali") return "50_caliper";
            if (name == "tire") return "10_tire";
        }
        else
        {
            if (name == "bhrome" || name == "chrome") return "20_chrome";
            if (
                name == "paint_bl"
                || name == "glack"
                || name == "bumpa"
                || name == "bleather"
                || name == "mlack"
                || name.Contains("ranger_over_blackgloss")
                || name.Contains("rangerover_blackgloss")
            )
            {
                useConsolidatedDoorDark = true;
                return "30_door_dark";
            }
        }

        string path = AssetDatabase.GetAssetPath(material);
        return "90_" + (string.IsNullOrEmpty(path) ? name : path + "#" + name);
    }

    private static Bounds CalculateBoundsInSpace(Transform space, IEnumerable<Renderer> renderers)
    {
        bool initialized = false;
        Bounds result = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                Vector3 meshCorner = new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z
                );
                Vector3 point = space.InverseTransformPoint(
                    renderer.transform.TransformPoint(meshCorner)
                );
                if (!initialized)
                {
                    result = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else result.Encapsulate(point);
            }
        }
        if (!initialized) throw new InvalidOperationException("No source bounds found for combined mesh");
        return result;
    }

    private static void SetModelReadability(bool readable)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Range Rover ModelImporter is missing");
        if (importer.isReadable == readable) return;
        importer.isReadable = readable;
        importer.SaveAndReimport();
    }

    private static OptimizationReport Validate()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("ModelImporter disappeared during QA");
        if (importer.meshCompression != ModelImporterMeshCompression.Low)
            throw new InvalidOperationException("Mesh compression is not Low");
        if (importer.isReadable) throw new InvalidOperationException("Range Rover meshes are still readable");
        if (importer.importTangents != ModelImporterTangents.None)
            throw new InvalidOperationException("Unused tangents are still imported");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Centered prefab could not be loaded");
        if (prefab.transform.localPosition != Vector3.zero
            || prefab.transform.localRotation != Quaternion.identity
            || prefab.transform.localScale != Vector3.one)
        {
            throw new InvalidOperationException("Centered prefab root transform is not identity");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != ExpectedOptimizedRendererCount)
                throw new InvalidOperationException(
                    $"Expected {ExpectedOptimizedRendererCount} optimized renderers, found {renderers.Length}"
                );

            Dictionary<string, MeshRenderer> byName = renderers.ToDictionary(renderer => renderer.name);
            Material body = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
            Material black = AssetDatabase.LoadAssetAtPath<Material>(BlackMaterialPath);
            Material tail = AssetDatabase.LoadAssetAtPath<Material>(TailMaterialPath);
            Material mirror = AssetDatabase.LoadAssetAtPath<Material>(MirrorMaterialPath);
            Material windowGlass = AssetDatabase.LoadAssetAtPath<Material>(WindowGlassMaterialPath);
            Material headlampGlass = AssetDatabase.LoadAssetAtPath<Material>(HeadlampGlassMaterialPath);

            AssertRendererMaterials(byName, new[] { "Body_ExteriorPaint" }, body);
            AssertRendererMaterials(byName, new[] { "Body_BlackPaint" }, black);
            AssertRendererMaterials(byName, TailRendererNames, tail);
            AssertRendererMaterials(byName, new[] { "Interior_RearViewMirror" }, mirror);
            AssertRendererMaterials(byName, new[] { "Body_WindowGlass" }, windowGlass);
            AssertRendererMaterials(byName, new[] { "Headlamp_Glass" }, headlampGlass);

            AssertCombinedRenderer(
                byName,
                "Door_Left_Combined",
                expectedSubmeshCount: 7,
                new[] { body, black, mirror, windowGlass }
            );
            AssertCombinedRenderer(
                byName,
                "Door_Right_Combined",
                expectedSubmeshCount: 7,
                new[] { body, black, mirror, windowGlass }
            );
            foreach (string wheelId in new[] { "FL", "FR", "RL", "RR" })
                AssertCombinedRenderer(
                    byName,
                    $"Wheel_{wheelId}_Combined",
                    expectedSubmeshCount: 5,
                    Array.Empty<Material>()
                );

            foreach (
                string combinedName in new[]
                {
                    "Door_Left_Combined",
                    "Door_Right_Combined",
                    "Wheel_FL_Combined",
                    "Wheel_FR_Combined",
                    "Wheel_RL_Combined",
                    "Wheel_RR_Combined"
                }
            )
            {
                Transform combinedTransform = byName[combinedName].transform;
                if (
                    combinedTransform.localRotation != Quaternion.identity
                    || combinedTransform.localScale != Vector3.one
                )
                {
                    throw new InvalidOperationException(
                        $"{combinedName} must keep inspector rotation 0/0/0 and scale 1/1/1"
                    );
                }
            }

            foreach (string frontWheelName in new[] { "Wheel_FL_Combined", "Wheel_FR_Combined" })
            {
                Mesh frontWheelMesh = byName[frontWheelName]
                    .GetComponent<MeshFilter>()
                    .sharedMesh;
                if (frontWheelMesh.bounds.size.x > 0.19f)
                    throw new InvalidOperationException(
                        $"{frontWheelName} is still steered/tilted in baked geometry: "
                            + $"local X size={frontWheelMesh.bounds.size.x}"
                    );
            }

            AssertApproximately(body.GetFloat("_Smoothness"), 0.93f, "body smoothness");
            AssertApproximately(body.GetFloat("_ClearCoatMask"), 1f, "body clear coat");
            if (body.shader.name != "Universal Render Pipeline/Complex Lit"
                || !body.HasProperty("_ClearCoat")
                || body.GetFloat("_ClearCoat") < 0.5f
                || !body.IsKeywordEnabled("_CLEARCOAT"))
            {
                throw new InvalidOperationException("Body clear coat is not enabled on URP Complex Lit");
            }
            if (body.renderQueue > (int)RenderQueue.GeometryLast)
                throw new InvalidOperationException("Body material is not opaque");
            if (!tail.IsKeywordEnabled("_EMISSION"))
                throw new InvalidOperationException("Tail material emission is disabled");
            Color tailEmission = tail.GetColor("_EmissionColor");
            if (tailEmission.r < 2f || tailEmission.g > 0.1f)
                throw new InvalidOperationException("Tail material is not strongly red emissive");
            if (mirror.renderQueue != (int)RenderQueue.Transparent
                || mirror.GetFloat("_Surface") < 0.5f
                || mirror.GetFloat("_Blend") != 0f
                || mirror.GetFloat("_BlendModePreserveSpecular") < 0.5f
                || !mirror.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")
                || mirror.GetColor("_BaseColor").a >= 0.99f)
            {
                throw new InvalidOperationException("Mirror material is not alpha transparent");
            }
            Color windowTint = windowGlass.GetColor("_BaseColor");
            if (windowGlass.renderQueue != (int)RenderQueue.Transparent
                || windowGlass.GetFloat("_Surface") < 0.5f
                || windowGlass.GetFloat("_ZWrite") > 0.5f
                || windowGlass.GetFloat("_ReceiveShadows") > 0.5f
                || windowTint.a < 0.15f
                || windowTint.a > 0.25f
                || windowTint.r < 0.5f)
            {
                throw new InvalidOperationException("Window glass is not configured as light realistic transparency");
            }
            Color headlampTint = headlampGlass.GetColor("_BaseColor");
            if (headlampGlass.renderQueue != (int)RenderQueue.Transparent
                || headlampGlass.GetFloat("_Surface") < 0.5f
                || headlampGlass.GetFloat("_ZWrite") > 0.5f
                || headlampGlass.GetFloat("_ReceiveShadows") > 0.5f
                || headlampTint.a < 0.20f
                || headlampTint.a > 0.32f
                || headlampTint.r < 0.70f)
            {
                throw new InvalidOperationException("Headlamp glass is not configured as clear transparency");
            }

            HashSet<Mesh> meshes = new HashSet<Mesh>();
            HashSet<Material> materials = new HashSet<Material>();
            int vertexCount = 0;
            int triangleCount = 0;
            foreach (MeshRenderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException($"Renderer has no mesh: {renderer.name}");
                if (meshes.Add(filter.sharedMesh))
                {
                    vertexCount += filter.sharedMesh.vertexCount;
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                        triangleCount += (int)filter.sharedMesh.GetIndexCount(submesh) / 3;
                }
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null) materials.Add(material);
            }
            if (triangleCount < 58690 || triangleCount > 58710)
                throw new InvalidOperationException($"Unexpected topology: {triangleCount} triangles");

            int estimatedDrawCalls = renderers.Sum(
                renderer => renderer.sharedMaterials.Count(material => material != null)
            );
            if (estimatedDrawCalls >= SourceRendererCount)
                throw new InvalidOperationException(
                    $"Draw calls were not reduced: {estimatedDrawCalls} >= {SourceRendererCount}"
                );

            Bounds centeredBounds = CalculateLocalBounds(instance.transform, renderers);
            if (centeredBounds.center.magnitude > 0.001f)
                throw new InvalidOperationException($"Prefab pivot is not centered: {centeredBounds.center}");
            if ((centeredBounds.size - ExpectedBoundsSize).magnitude > 0.005f)
                throw new InvalidOperationException($"Centered bounds size changed: {centeredBounds.size}");

            TextureReport[] textureReports = TextureSpecs.Select(BuildTextureReport).ToArray();

            return new OptimizationReport
            {
                passed = true,
                unityVersion = Application.unityVersion,
                modelPath = ModelPath,
                prefabPath = PrefabPath,
                meshCompression = importer.meshCompression.ToString(),
                modelReadable = importer.isReadable,
                tangentMode = importer.importTangents.ToString(),
                rendererCount = renderers.Length,
                meshCount = meshes.Count,
                uniqueMaterialCount = materials.Count,
                sourceRendererCount = SourceRendererCount,
                optimizedRendererCount = renderers.Length,
                sourceEstimatedDrawCalls = SourceRendererCount,
                optimizedEstimatedDrawCalls = estimatedDrawCalls,
                combinedDoorMeshCount = 2,
                combinedWheelMeshCount = 4,
                frontWheelYawBaked = true,
                frontLeftWheelYawCorrection = -16.87f,
                frontRightWheelYawCorrection = -17.13f,
                vertexCount = vertexCount,
                triangleCount = triangleCount,
                centeredBoundsCenter = centeredBounds.center,
                centeredBoundsSize = centeredBounds.size,
                visualOffset = VisualOffset,
                bodyMaterial = BodyMaterialPath,
                blackMaterial = BlackMaterialPath,
                tailMaterial = TailMaterialPath,
                mirrorMaterial = MirrorMaterialPath,
                windowGlassMaterial = WindowGlassMaterialPath,
                headlampGlassMaterial = HeadlampGlassMaterialPath,
                bodyShader = body.shader.name,
                bodyClearCoatEnabled = body.IsKeywordEnabled("_CLEARCOAT"),
                mirrorBlendMode = "Alpha (preserve specular)",
                bodyRendererCount = BodyRendererNames.Length,
                blackRendererCount = BlackRendererNames.Length,
                tailRendererCount = TailRendererNames.Length,
                mirrorRendererCount = MirrorRendererNames.Length,
                windowGlassRendererCount = WindowGlassRendererNames.Length,
                windowGlassAlpha = windowTint.a,
                headlampGlassRendererCount = 1,
                headlampGlassAlpha = headlampTint.a,
                textures = textureReports
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static Bounds CalculateLocalBounds(Transform root, Renderer[] renderers)
    {
        bool initialized = false;
        Bounds result = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            Vector3 min = world.min;
            Vector3 max = world.max;
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Vector3 local = root.InverseTransformPoint(corner);
                if (!initialized)
                {
                    result = new Bounds(local, Vector3.zero);
                    initialized = true;
                }
                else result.Encapsulate(local);
            }
        }
        if (!initialized) throw new InvalidOperationException("No renderer bounds were found");
        return result;
    }

    private static TextureReport BuildTextureReport(TextureSpec spec)
    {
        TextureImporter importer = AssetImporter.GetAtPath(spec.Path) as TextureImporter;
        TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
        TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings("iPhone");
        if (!android.overridden || android.format != spec.MobileFormat || android.maxTextureSize != spec.MaxSize)
            throw new InvalidOperationException($"Android texture settings mismatch: {spec.Path}");
        if (!ios.overridden || ios.format != spec.MobileFormat || ios.maxTextureSize != spec.MaxSize)
            throw new InvalidOperationException($"iOS texture settings mismatch: {spec.Path}");

        return new TextureReport
        {
            path = spec.Path,
            defaultMaxSize = importer.maxTextureSize,
            defaultCompression = importer.textureCompression.ToString(),
            androidFormat = android.format.ToString(),
            androidMaxSize = android.maxTextureSize,
            iosFormat = ios.format.ToString(),
            iosMaxSize = ios.maxTextureSize,
            mipmaps = importer.mipmapEnabled,
            readable = importer.isReadable
        };
    }

    private static void AssertRendererMaterials(
        IReadOnlyDictionary<string, MeshRenderer> renderers,
        IEnumerable<string> names,
        Material expected
    )
    {
        foreach (string name in names)
        {
            if (!renderers.TryGetValue(name, out MeshRenderer renderer))
                throw new InvalidOperationException($"Renderer not found: {name}");
            if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial != expected)
                throw new InvalidOperationException($"Material remap failed for {name}");
        }
    }

    private static void AssertCombinedRenderer(
        IReadOnlyDictionary<string, MeshRenderer> renderers,
        string name,
        int expectedSubmeshCount,
        IEnumerable<Material> requiredMaterials
    )
    {
        if (!renderers.TryGetValue(name, out MeshRenderer renderer))
            throw new InvalidOperationException($"Combined renderer not found: {name}");
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException($"Combined renderer has no mesh: {name}");
        if (filter.sharedMesh.subMeshCount != expectedSubmeshCount)
            throw new InvalidOperationException(
                $"{name} has {filter.sharedMesh.subMeshCount} submeshes; expected {expectedSubmeshCount}"
            );
        if (renderer.sharedMaterials.Length != expectedSubmeshCount)
            throw new InvalidOperationException($"Combined material count mismatch: {name}");
        foreach (Material required in requiredMaterials)
            if (!renderer.sharedMaterials.Contains(required))
                throw new InvalidOperationException($"{name} is missing required material {required.name}");
    }

    private static void AssertApproximately(float actual, float expected, string label)
    {
        if (Mathf.Abs(actual - expected) > 0.0001f)
            throw new InvalidOperationException($"Unexpected {label}: {actual}, expected {expected}");
    }

    private static Material LoadOrCreateMaterial(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else material.shader = shader;
        return material;
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string name = Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            throw new InvalidOperationException($"Invalid asset folder: {assetPath}");
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void RequireAsset(string assetPath)
    {
        string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        if (!File.Exists(absolute)) throw new FileNotFoundException($"Required asset missing: {assetPath}", absolute);
    }

    private static void WriteReport(OptimizationReport report)
    {
        string output = GetCommandLineArgument("-rangeRoverReport");
        if (string.IsNullOrWhiteSpace(output))
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Range_Rover_Optimization_QA.json"));
        string directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
    }

    private static string GetCommandLineArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
            if (arguments[index] == name) return arguments[index + 1];
        return null;
    }
}
