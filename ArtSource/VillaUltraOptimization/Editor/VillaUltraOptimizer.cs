using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fidelity-first draw-call optimizer for the original Franklin villa.
/// It keeps the complete transform/collider hierarchy, merges visuals by gameplay zone/pivot,
/// and bakes the 23 opaque Piglet materials into vertex PBR data plus one colour atlas.
/// Run only in the disposable staging project while the main Unity project is open.
/// </summary>
public static class VillaUltraOptimizer
{
    private const string SourceRoot = "Assets/Model/franklin-villa-estate-unity";
    private const string SourcePrefab = SourceRoot + "/franklin-villa-estate-unity.prefab";
    private const string BuilderShader = "Assets/Editor/VillaUltraOpaque.shader";
    private const string DefaultTargetRoot = "Assets/Model/franklin-villa-estate-ultra-optimized";
    private const string TargetPrefabName = "franklin-villa-estate-ultra-optimized.prefab";
    private const string RampRootName = "FranklinVilla_Hierarchy_ExternalVehicleRamp_001";
    private const float BoundsTolerance = 0.01f;

    private static readonly HashSet<string> DynamicAnchorNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "FranklinVilla_Interactive_GateLeaf_Left_001",
        "FranklinVilla_Interactive_GateLeaf_Right_001",
        "FranklinVilla_Interactive_GaragePanel_1_001",
        "FranklinVilla_Interactive_GaragePanel_2_001",
        "FranklinVilla_Interactive_GaragePanel_3_001",
        "FranklinVilla_Interactive_GaragePanel_4_001",
        "FranklinVilla_Interactive_cua_chinh_Pivot_001",
        "FranklinVilla_Interactive_cua_ban_cong_Pivot_001",
        // These inner groups contain each pool door's glass sheet and four frame bars.
        "FranklinVilla_Hierarchy_Group_003",
        "FranklinVilla_Hierarchy_Group_005"
    };

    private static int sMeshIndex;

    public static void BuildAndAudit()
    {
        string targetRoot = GetArgument("-villaUltraRoot");
        if (string.IsNullOrWhiteSpace(targetRoot)) targetRoot = DefaultTargetRoot;
        string reportPath = GetArgument("-villaUltraReport");
        if (string.IsNullOrWhiteSpace(reportPath))
            reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../VillaUltraOptimizationReport.json"));

        if (!targetRoot.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException("Target root must be below Assets: " + targetRoot);
        if (targetRoot == SourceRoot || targetRoot.StartsWith(SourceRoot + "/", StringComparison.Ordinal))
            throw new ArgumentException("Refusing to overwrite or nest inside the source villa folder.");

        GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (sourceAsset == null) throw new FileNotFoundException("Source prefab missing", SourcePrefab);
        Shader importedShader = AssetDatabase.LoadAssetAtPath<Shader>(BuilderShader);
        if (importedShader == null || !importedShader.isSupported)
            throw new InvalidOperationException("Compile-ready VillaUltraOpaque shader missing/unsupported: " + BuilderShader);

        Metrics sourceMetrics = CaptureMetrics(sourceAsset, SourceRoot);
        Dictionary<string, NodeFingerprint> sourceNodes = CaptureSourceNodeFingerprints(sourceAsset);
        if (sourceMetrics.rendererCount != 461 || sourceMetrics.materialSlotCount != 498 ||
            sourceMetrics.instanceTriangleCount != 16520 || sourceMetrics.colliderCount != 461)
            throw new InvalidOperationException("Unexpected source baseline: " + JsonUtility.ToJson(sourceMetrics));

        if (AssetDatabase.IsValidFolder(targetRoot) && !AssetDatabase.DeleteAsset(targetRoot))
            throw new IOException("Could not delete exact target folder: " + targetRoot);

        EnsureFolder(targetRoot);
        string materialsRoot = targetRoot + "/Materials";
        string meshesRoot = targetRoot + "/Meshes";
        string texturesRoot = targetRoot + "/Textures";
        string shadersRoot = targetRoot + "/Shaders";
        EnsureFolder(materialsRoot);
        EnsureFolder(meshesRoot);
        EnsureFolder(texturesRoot);
        EnsureFolder(shadersRoot);

        string targetShaderPath = shadersRoot + "/VillaUltraOpaque.shader";
        if (!AssetDatabase.CopyAsset(BuilderShader, targetShaderPath))
            throw new IOException("Could not copy the local opaque shader.");
        AssetDatabase.ImportAsset(targetShaderPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        Shader opaqueShader = AssetDatabase.LoadAssetAtPath<Shader>(targetShaderPath);
        if (opaqueShader == null || !opaqueShader.isSupported)
            throw new InvalidOperationException("Target opaque shader failed to import: " + targetShaderPath);

        string atlasPath = texturesRoot + "/Villa_BaseAtlas.png";
        Texture2D atlas = BuildAtlas(atlasPath);
        Material opaqueMaterial = CreateOpaqueMaterial(materialsRoot, opaqueShader, atlas);
        Material zwriteMaterial = CopyMaterial(SourceRoot + "/Materials/zwrite.mat", materialsRoot + "/M_Villa_ZWrite.mat");
        Material glassMaterial = CopyMaterial(SourceRoot + "/Materials/glass.mat", materialsRoot + "/M_Villa_Glass.mat");
        Material waterMaterial = CopyMaterial(SourceRoot + "/Materials/UnityPBR_25.mat", materialsRoot + "/M_Villa_Water.mat");

        string targetPrefab = targetRoot + "/" + TargetPrefabName;
        GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefab);
        sMeshIndex = 0;
        int opaqueSourceMaterialCount = 0;
        int outputGroupCount = 0;
        try
        {
            root.name = Path.GetFileNameWithoutExtension(TargetPrefabName);
            MeshRenderer[] sourceRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (sourceRenderers.Any(renderer => !renderer.gameObject.activeInHierarchy))
                throw new InvalidOperationException("Inactive source renderer found; V2 activation semantics require a dedicated group.");

            HashSet<Material> opaqueSourceMaterials = new HashSet<Material>();
            foreach (MeshRenderer renderer in sourceRenderers)
            {
                RenderClass renderClass = GetRenderClass(renderer);
                if (renderClass == RenderClass.Opaque) opaqueSourceMaterials.Add(renderer.sharedMaterials[0]);
            }
            opaqueSourceMaterialCount = opaqueSourceMaterials.Count;
            if (opaqueSourceMaterialCount != 23)
                throw new InvalidOperationException("Expected 23 used opaque source materials, found " + opaqueSourceMaterialCount);

            CloneColliderMeshes(root, meshesRoot);

            Transform staticVisuals = new GameObject("OPTIMIZED_STATIC_VISUALS").transform;
            staticVisuals.SetParent(root.transform, false);

            Dictionary<string, BatchGroup> groups = new Dictionary<string, BatchGroup>(StringComparer.Ordinal);
            foreach (MeshRenderer renderer in sourceRenderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException("Renderer is missing its source mesh: " + RelativePath(renderer.transform, root.transform));
                if (filter.sharedMesh.subMeshCount != 1)
                    throw new InvalidOperationException("Source mesh is not single-submesh: " + filter.sharedMesh.name);

                RenderClass renderClass = GetRenderClass(renderer);
                Transform motionAnchor = FindDynamicAnchor(renderer.transform, root.transform);
                string groupName;
                Transform groupParent;
                bool moving;
                if (motionAnchor != null)
                {
                    groupName = "DYNAMIC_" + motionAnchor.name;
                    groupParent = motionAnchor;
                    moving = true;
                }
                else
                {
                    groupName = BuildStaticZone(renderer, root.transform);
                    groupParent = staticVisuals;
                    moving = false;
                }

                // Water must stay in its own renderer because Unity's extra-material rule can
                // repeat only the final submesh; glass can safely coexist with opaque geometry.
                if (renderClass == RenderClass.Water) groupName += "_WATER";
                string key = (moving ? "M|" : "S|") + groupName;
                BatchGroup group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new BatchGroup(groupName, groupParent, moving);
                    groups.Add(key, group);
                }
                else if (group.parent != groupParent)
                    throw new InvalidOperationException("A dynamic group resolved to multiple anchors: " + groupName);
                group.sources.Add(new BatchSource(renderer, filter, renderClass));
            }

            foreach (BatchGroup group in groups.Values.OrderBy(value => value.name, StringComparer.Ordinal))
            {
                BuildGroupMesh(group, meshesRoot, opaqueMaterial, zwriteMaterial, glassMaterial, waterMaterial);
                outputGroupCount++;
            }

            // Visual data now lives only in the compact batch children. Keep every original
            // Transform/GameObject/Collider so gameplay paths, pivots, gates and the ramp survive.
            foreach (MeshRenderer renderer in sourceRenderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                UnityEngine.Object.DestroyImmediate(renderer, true);
                if (filter != null) UnityEngine.Object.DestroyImmediate(filter, true);
            }

            PrefabUtility.SaveAsPrefabAsset(root, targetPrefab, out bool saved);
            if (!saved) throw new IOException("Could not save ultra optimized prefab: " + targetPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        GameObject optimizedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefab);
        if (optimizedAsset == null) throw new IOException("Optimized prefab did not reload: " + targetPrefab);
        Metrics optimizedMetrics = CaptureMetrics(optimizedAsset, targetRoot);
        CompareSourceNodeFingerprints(sourceNodes, optimizedAsset);
        ValidateMetrics(sourceMetrics, optimizedMetrics);
        ValidateDependencies(targetRoot, targetPrefab, outputGroupCount);
        ValidateDynamicOutputs(optimizedAsset);

        OptimizationReport report = new OptimizationReport
        {
            passed = true,
            sourcePrefab = SourcePrefab,
            optimizedPrefab = targetPrefab,
            source = sourceMetrics,
            optimized = optimizedMetrics,
            sourceOpaqueMaterialCount = opaqueSourceMaterialCount,
            outputGroupCount = outputGroupCount,
            rendererReductionPercent = PercentReduction(sourceMetrics.rendererCount, optimizedMetrics.rendererCount),
            materialSlotReductionPercent = PercentReduction(sourceMetrics.materialSlotCount, optimizedMetrics.materialSlotCount),
            uniqueMeshReductionPercent = PercentReduction(sourceMetrics.uniqueMeshCount, optimizedMetrics.uniqueMeshCount),
            sourceLocalAssetBytes = DirectoryBytes(AssetPathToFullPath(SourceRoot)),
            optimizedLocalAssetBytes = DirectoryBytes(AssetPathToFullPath(targetRoot)),
            notes = new[]
            {
                "The original prefab/folder was not modified.",
                "All 517 original transforms and all 461 colliders retain their exact paths and local transforms.",
                "The 23 opaque appearances are baked into UV3-UV5 and one shared atlas material.",
                "Glass and water retain the original Piglet zwrite plus transparent material passes.",
                "Only ten true moving anchors receive their own compact visual batch; ramp visuals are region-batched.",
                "No decimation was used: all 16,520 rendered triangles remain.",
                "Atlas uses original 256px pixels with eight-pixel wrap gutters, BC1 desktop and ASTC 4x4 mobile."
            }
        };
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? ".");
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        Debug.Log(
            "VILLA_ULTRA_OPTIMIZATION_SUCCESS " +
            $"renderers={sourceMetrics.rendererCount}->{optimizedMetrics.rendererCount} " +
            $"slots={sourceMetrics.materialSlotCount}->{optimizedMetrics.materialSlotCount} " +
            $"uniqueMeshes={sourceMetrics.uniqueMeshCount}->{optimizedMetrics.uniqueMeshCount} " +
            $"materials={optimizedMetrics.materialCount} textures={optimizedMetrics.referencedTextureCount} " +
            $"tris={optimizedMetrics.instanceTriangleCount} colliders={optimizedMetrics.colliderCount} report={reportPath}");
    }

    private static Texture2D BuildAtlas(string targetPath)
    {
        const int sourceSize = 256;
        const int gutter = 8;
        const int stride = sourceSize + gutter * 2;
        const int columns = 3;
        const int rows = 2;
        int width = stride * columns;
        int height = stride * rows;
        Color32[] atlasPixels = Enumerable.Repeat(new Color32(255, 255, 255, 255), width * height).ToArray();

        for (int tile = 0; tile < 6; tile++)
        {
            Color32[] sourcePixels;
            if (tile == 0)
            {
                sourcePixels = Enumerable.Repeat(new Color32(255, 255, 255, 255), sourceSize * sourceSize).ToArray();
            }
            else
            {
                string path = AssetPathToFullPath(SourceRoot + "/Textures/texture_" + tile + ".png");
                if (!File.Exists(path)) throw new FileNotFoundException("Atlas source missing", path);
                Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                try
                {
                    if (!ImageConversion.LoadImage(decoded, File.ReadAllBytes(path), false) ||
                        decoded.width != sourceSize || decoded.height != sourceSize)
                        throw new InvalidDataException("Atlas source is not 256x256: " + path);
                    sourcePixels = decoded.GetPixels32();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(decoded);
                }
            }

            int cellX = (tile % columns) * stride;
            int cellY = (tile / columns) * stride;
            for (int y = -gutter; y < sourceSize + gutter; y++)
            {
                int sourceY = PositiveMod(y, sourceSize);
                int targetY = cellY + gutter + y;
                for (int x = -gutter; x < sourceSize + gutter; x++)
                {
                    int sourceX = PositiveMod(x, sourceSize);
                    int targetX = cellX + gutter + x;
                    atlasPixels[targetY * width + targetX] = sourcePixels[sourceY * sourceSize + sourceX];
                }
            }
        }

        Texture2D atlas = new Texture2D(width, height, TextureFormat.RGB24, false, false);
        try
        {
            atlas.SetPixels32(atlasPixels);
            atlas.Apply(false, false);
            File.WriteAllBytes(AssetPathToFullPath(targetPath), ImageConversion.EncodeToPNG(atlas));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(atlas);
        }

        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(targetPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("TextureImporter missing: " + targetPath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = false;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 1;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.compressionQuality = 80;
        SetPlatform(importer, "Standalone", TextureImporterFormat.DXT1);
        SetPlatform(importer, "Android", TextureImporterFormat.ASTC_4x4);
        SetPlatform(importer, "iPhone", TextureImporterFormat.ASTC_4x4);
        importer.SaveAndReimport();
        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
        if (imported == null || imported.width != width || imported.height != height)
            throw new InvalidOperationException("Atlas imported at an unexpected size.");
        return imported;
    }

    private static void SetPlatform(TextureImporter importer, string platform, TextureImporterFormat format)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
        settings.name = platform;
        settings.overridden = true;
        settings.maxTextureSize = 1024;
        settings.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
        settings.format = format;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.compressionQuality = 80;
        importer.SetPlatformTextureSettings(settings);
    }

    private static Material CreateOpaqueMaterial(string materialsRoot, Shader shader, Texture2D atlas)
    {
        Material material = new Material(shader) { name = "M_Villa_UltraOpaque" };
        material.SetTexture("_BaseAtlas", atlas);
        material.SetVector("_AtlasInfo", new Vector4(816f, 544f, 272f, 8f));
        material.SetVector("_AtlasContent", new Vector4(256f, 256f, 3f, 2f));
        string path = materialsRoot + "/M_Villa_UltraOpaque.mat";
        AssetDatabase.CreateAsset(material, path);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CopyMaterial(string sourcePath, string targetPath)
    {
        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            throw new IOException("Could not copy material: " + sourcePath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
        if (material == null) throw new IOException("Copied material did not load: " + targetPath);
        return material;
    }

    private static void CloneColliderMeshes(GameObject root, string meshesRoot)
    {
        Dictionary<Mesh, Mesh> clones = new Dictionary<Mesh, Mesh>();
        foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true))
        {
            Mesh source = collider.sharedMesh;
            if (source == null) throw new InvalidOperationException("MeshCollider has no mesh: " + collider.name);
            Mesh clone;
            if (!clones.TryGetValue(source, out clone))
            {
                clone = UnityEngine.Object.Instantiate(source);
                clone.name = "COLLIDER_" + Sanitize(source.name) + "_" + clones.Count.ToString("D2", CultureInfo.InvariantCulture);
                MeshUtility.SetMeshCompression(clone, ModelImporterMeshCompression.Off);
                AssetDatabase.CreateAsset(clone, meshesRoot + "/" + clone.name + ".asset");
                EditorUtility.SetDirty(clone);
                clones.Add(source, clone);
            }
            collider.sharedMesh = clone;
        }
        if (root.GetComponentsInChildren<MeshCollider>(true).Length != 6 || clones.Count != 6)
            throw new InvalidOperationException("Expected six independent ramp MeshCollider meshes.");
    }

    private static void BuildGroupMesh(
        BatchGroup group,
        string meshesRoot,
        Material opaqueMaterial,
        Material zwriteMaterial,
        Material glassMaterial,
        Material waterMaterial)
    {
        GameObject output = new GameObject("BATCH_" + Sanitize(group.name));
        output.transform.SetParent(group.parent, false);
        BatchSource first = group.sources[0];
        output.layer = first.renderer.gameObject.layer;
        output.tag = first.renderer.gameObject.tag;

        List<Mesh> temporary = new List<Mesh>();
        Mesh opaque = BuildCategoryMesh(group, RenderClass.Opaque, output.transform, temporary);
        Mesh glass = BuildCategoryMesh(group, RenderClass.Glass, output.transform, temporary);
        Mesh water = BuildCategoryMesh(group, RenderClass.Water, output.transform, temporary);
        if (water != null && (opaque != null || glass != null))
            throw new InvalidOperationException("Water was not isolated from its batch: " + group.name);

        Mesh finalMesh;
        Material[] materials;
        if (water != null)
        {
            finalMesh = water;
            materials = new[] { zwriteMaterial, waterMaterial };
        }
        else if (opaque != null && glass != null)
        {
            finalMesh = new Mesh
            {
                name = "MESH_" + Sanitize(group.name),
                indexFormat = opaque.vertexCount + glass.vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            finalMesh.CombineMeshes(new[]
            {
                new CombineInstance { mesh = opaque, subMeshIndex = 0, transform = Matrix4x4.identity },
                new CombineInstance { mesh = glass, subMeshIndex = 0, transform = Matrix4x4.identity }
            }, false, true, false);
            temporary.Add(finalMesh);
            materials = new[] { opaqueMaterial, zwriteMaterial, glassMaterial };
        }
        else if (opaque != null)
        {
            finalMesh = opaque;
            materials = new[] { opaqueMaterial };
        }
        else if (glass != null)
        {
            finalMesh = glass;
            materials = new[] { zwriteMaterial, glassMaterial };
        }
        else throw new InvalidOperationException("Empty batch group: " + group.name);

        finalMesh.name = "MESH_" + sMeshIndex.ToString("D3", CultureInfo.InvariantCulture) + "_" + Sanitize(group.name);
        finalMesh.RecalculateBounds();
        finalMesh.OptimizeIndexBuffers();
        finalMesh.OptimizeReorderVertexBuffer();
        MeshUtility.SetMeshCompression(finalMesh, ModelImporterMeshCompression.Off);
        string meshPath = meshesRoot + "/" + finalMesh.name + ".asset";
        AssetDatabase.CreateAsset(finalMesh, meshPath);
        finalMesh.UploadMeshData(true);
        EditorUtility.SetDirty(finalMesh);
        sMeshIndex++;

        MeshFilter outputFilter = output.AddComponent<MeshFilter>();
        outputFilter.sharedMesh = finalMesh;
        MeshRenderer outputRenderer = output.AddComponent<MeshRenderer>();
        outputRenderer.sharedMaterials = materials;
        CopyRendererState(first.renderer, outputRenderer);

        foreach (Mesh mesh in temporary)
            if (mesh != finalMesh) UnityEngine.Object.DestroyImmediate(mesh);
    }

    private static Mesh BuildCategoryMesh(
        BatchGroup group,
        RenderClass renderClass,
        Transform output,
        List<Mesh> temporary)
    {
        List<BatchSource> sources = group.sources.Where(source => source.renderClass == renderClass).ToList();
        if (sources.Count == 0) return null;
        CombineInstance[] combines = new CombineInstance[sources.Count];
        long vertices = 0;
        for (int index = 0; index < sources.Count; index++)
        {
            BatchSource source = sources[index];
            Mesh baked = BakeSourceMesh(source);
            temporary.Add(baked);
            vertices += baked.vertexCount;
            Matrix4x4 transform = output.worldToLocalMatrix * source.filter.transform.localToWorldMatrix;
            if (Determinant3x3(transform) <= 0f)
                throw new InvalidOperationException("Negative/degenerate source transform needs explicit winding handling: " + source.renderer.name);
            combines[index] = new CombineInstance
            {
                mesh = baked,
                subMeshIndex = 0,
                transform = transform,
                lightmapScaleOffset = source.renderer.lightmapScaleOffset,
                realtimeLightmapScaleOffset = source.renderer.realtimeLightmapScaleOffset
            };
        }

        Mesh combined = new Mesh
        {
            name = "TEMP_" + renderClass + "_" + Sanitize(group.name),
            indexFormat = vertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        combined.CombineMeshes(combines, true, true, false);
        combined.RecalculateBounds();
        temporary.Add(combined);
        return combined;
    }

    private static Mesh BakeSourceMesh(BatchSource source)
    {
        Mesh original = source.filter.sharedMesh;
        Mesh mesh = UnityEngine.Object.Instantiate(original);
        mesh.name = "TEMP_BAKED_" + original.name;
        int vertexCount = mesh.vertexCount;
        if (mesh.normals == null || mesh.normals.Length != vertexCount)
            throw new InvalidOperationException("Source mesh has no complete normals: " + original.name);

        Vector2[] uv0 = mesh.uv;
        if (uv0 == null || uv0.Length != vertexCount) uv0 = new Vector2[vertexCount];
        List<Vector4> uv1 = ReadOrFillUv(mesh, 1, vertexCount);
        Color[] sourceColors = mesh.colors;
        bool hasColors = sourceColors != null && sourceColors.Length == vertexCount;

        Material appearance = source.renderer.sharedMaterials[0];
        Color baseFactor = Color.white;
        Color emissive = Color.black;
        float metallic = 0f;
        float roughness = 1f;
        int tile = 0;
        Vector2 textureScale = Vector2.one;
        Vector2 textureOffset = Vector2.zero;
        if (source.renderClass == RenderClass.Opaque)
        {
            RequireProperty(appearance, "_baseColorFactor");
            RequireProperty(appearance, "_emissiveFactor");
            RequireProperty(appearance, "_metallicFactor");
            RequireProperty(appearance, "_roughnessFactor");
            baseFactor = appearance.GetColor("_baseColorFactor");
            emissive = appearance.GetColor("_emissiveFactor");
            metallic = appearance.GetFloat("_metallicFactor");
            roughness = appearance.GetFloat("_roughnessFactor");
            Texture baseTexture = appearance.HasProperty("_baseColorTexture")
                ? appearance.GetTexture("_baseColorTexture")
                : null;
            tile = AtlasTile(baseTexture);
            if (baseTexture != null)
            {
                textureScale = appearance.GetTextureScale("_baseColorTexture");
                textureOffset = appearance.GetTextureOffset("_baseColorTexture");
            }
        }

        List<Vector4> baseData = new List<Vector4>(vertexCount);
        List<Vector4> emissionMetalData = new List<Vector4>(vertexCount);
        List<Vector4> roughTileData = new List<Vector4>(vertexCount);
        Color32[] colors = new Color32[vertexCount];
        for (int index = 0; index < vertexCount; index++)
        {
            Color vertexColor = hasColors ? sourceColors[index] : Color.white;
            Color bakedBase = new Color(
                baseFactor.r * vertexColor.r,
                baseFactor.g * vertexColor.g,
                baseFactor.b * vertexColor.b,
                baseFactor.a * vertexColor.a);
            baseData.Add(new Vector4(bakedBase.r, bakedBase.g, bakedBase.b, bakedBase.a));
            emissionMetalData.Add(new Vector4(emissive.r, emissive.g, emissive.b, metallic));
            roughTileData.Add(new Vector4(roughness, tile, 0f, 0f));
            colors[index] = hasColors ? (Color32)sourceColors[index] : new Color32(255, 255, 255, 255);
            if (source.renderClass == RenderClass.Opaque)
                uv0[index] = Vector2.Scale(uv0[index], textureScale) + textureOffset;
        }
        mesh.uv = uv0;
        mesh.SetUVs(1, uv1);
        mesh.SetUVs(2, baseData);
        mesh.SetUVs(3, emissionMetalData);
        mesh.SetUVs(4, roughTileData);
        mesh.colors32 = colors;
        mesh.tangents = Array.Empty<Vector4>();
        return mesh;
    }

    private static List<Vector4> ReadOrFillUv(Mesh mesh, int channel, int vertexCount)
    {
        List<Vector4> result = new List<Vector4>();
        mesh.GetUVs(channel, result);
        if (result.Count == vertexCount) return result;
        result.Clear();
        for (int index = 0; index < vertexCount; index++) result.Add(Vector4.zero);
        return result;
    }

    private static void RequireProperty(Material material, string property)
    {
        if (material == null || !material.HasProperty(property))
            throw new InvalidOperationException("Opaque source material lacks " + property + ": " + (material == null ? "null" : material.name));
    }

    private static int AtlasTile(Texture texture)
    {
        if (texture == null) return 0;
        string name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(texture));
        if (!name.StartsWith("texture_", StringComparison.Ordinal) ||
            !int.TryParse(name.Substring("texture_".Length), NumberStyles.None, CultureInfo.InvariantCulture, out int tile) ||
            tile < 1 || tile > 5)
            throw new InvalidOperationException("Unexpected used villa texture: " + AssetDatabase.GetAssetPath(texture));
        return tile;
    }

    private static RenderClass GetRenderClass(MeshRenderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0 || materials[0] == null)
            throw new InvalidOperationException("Renderer has no material: " + renderer.name);
        if (materials.Length == 1) return RenderClass.Opaque;
        if (materials.Length != 2 || materials[1] == null || materials[0].name != "zwrite")
            throw new InvalidOperationException("Unexpected source material signature on " + renderer.name);
        if (materials[1].name == "glass") return RenderClass.Glass;
        if (materials[1].name == "UnityPBR_25") return RenderClass.Water;
        throw new InvalidOperationException("Unknown transparent source signature on " + renderer.name + ": " + materials[1].name);
    }

    private static Transform FindDynamicAnchor(Transform transform, Transform root)
    {
        for (Transform cursor = transform; cursor != null && cursor != root; cursor = cursor.parent)
            if (DynamicAnchorNames.Contains(cursor.name)) return cursor;
        return null;
    }

    private static string BuildStaticZone(MeshRenderer renderer, Transform root)
    {
        Transform transform = renderer.transform;
        Vector3 center = root.InverseTransformPoint(renderer.bounds.center);
        if (HasAncestor(transform, RampRootName))
            return center.y < 5.0f ? "STATIC_Ramp_Lower" : "STATIC_Ramp_Upper";
        if (HasAncestor(transform, "FranklinVilla_Hierarchy_Group_007") ||
            HasAncestor(transform, "FranklinVilla_Hierarchy_Group_011"))
            return "STATIC_UpperFrontGlass";
        if (HasAncestorContaining(transform, "EstateBoundary"))
            return center.x < 0f ? "STATIC_Boundary_West" : "STATIC_Boundary_East";
        if (center.y < 0.75f)
            return center.x < 0f ? "STATIC_Site_West" : "STATIC_Site_East";
        if (center.y < 3.8f)
            return "STATIC_Ground_" + HorizontalBand(center.x);
        if (center.y < 7.2f)
            return "STATIC_Upper_" + HorizontalBand(center.x);
        return center.x < 0f ? "STATIC_Roof_West" : "STATIC_Roof_East";
    }

    private static string HorizontalBand(float x)
    {
        return x < -6f ? "West" : (x < 6f ? "Center" : "East");
    }

    private static bool HasAncestor(Transform transform, string exactName)
    {
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
            if (cursor.name == exactName) return true;
        return false;
    }

    private static bool HasAncestorContaining(Transform transform, string token)
    {
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
            if (cursor.name.IndexOf(token, StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    private static void CopyRendererState(MeshRenderer source, MeshRenderer target)
    {
        target.enabled = source.enabled;
        target.shadowCastingMode = source.shadowCastingMode;
        target.receiveShadows = source.receiveShadows;
        target.staticShadowCaster = source.staticShadowCaster;
        target.motionVectorGenerationMode = source.motionVectorGenerationMode;
        target.lightProbeUsage = source.lightProbeUsage;
        target.reflectionProbeUsage = source.reflectionProbeUsage;
        target.rayTracingMode = source.rayTracingMode;
        target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        target.renderingLayerMask = source.renderingLayerMask;
        target.rendererPriority = source.rendererPriority;
        target.receiveGI = source.receiveGI;
        target.lightmapIndex = source.lightmapIndex;
        target.lightmapScaleOffset = source.lightmapScaleOffset;
        target.realtimeLightmapIndex = source.realtimeLightmapIndex;
        target.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
        target.probeAnchor = source.probeAnchor;
        target.lightProbeProxyVolumeOverride = source.lightProbeProxyVolumeOverride;
    }

    private static Dictionary<string, NodeFingerprint> CaptureSourceNodeFingerprints(GameObject root)
    {
        Dictionary<string, NodeFingerprint> result = new Dictionary<string, NodeFingerprint>(StringComparer.Ordinal);
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            string path = RelativePath(transform, root.transform);
            Collider collider = transform.GetComponent<Collider>();
            result[path] = new NodeFingerprint
            {
                path = path,
                siblingIndex = transform.GetSiblingIndex(),
                activeSelf = transform.gameObject.activeSelf,
                layer = transform.gameObject.layer,
                tag = transform.gameObject.tag,
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale,
                colliderType = collider == null ? "" : collider.GetType().Name,
                colliderSignature = ColliderSignature(collider)
            };
        }
        return result;
    }

    private static void CompareSourceNodeFingerprints(Dictionary<string, NodeFingerprint> source, GameObject optimized)
    {
        Dictionary<string, Transform> output = optimized.GetComponentsInChildren<Transform>(true)
            .ToDictionary(transform => RelativePath(transform, optimized.transform), transform => transform, StringComparer.Ordinal);
        foreach (KeyValuePair<string, NodeFingerprint> entry in source)
        {
            Transform transform;
            if (!output.TryGetValue(entry.Key, out transform))
                throw new InvalidOperationException("Original hierarchy path is missing: " + entry.Key);
            NodeFingerprint expected = entry.Value;
            Collider collider = transform.GetComponent<Collider>();
            // The root name intentionally changes, but its path remains empty.
            if (transform.GetSiblingIndex() != expected.siblingIndex ||
                transform.gameObject.activeSelf != expected.activeSelf ||
                transform.gameObject.layer != expected.layer || transform.gameObject.tag != expected.tag ||
                !Nearly(transform.localPosition, expected.localPosition, 0.000001f) ||
                Quaternion.Angle(transform.localRotation, expected.localRotation) > 0.0001f ||
                !Nearly(transform.localScale, expected.localScale, 0.000001f) ||
                (collider == null ? "" : collider.GetType().Name) != expected.colliderType ||
                ColliderSignature(collider) != expected.colliderSignature)
                throw new InvalidOperationException("Hierarchy/collider fingerprint changed: " + entry.Key);
        }
        if (source.Count != 517)
            throw new InvalidOperationException("Unexpected source transform count: " + source.Count);
    }

    private static string ColliderSignature(Collider collider)
    {
        if (collider == null) return "";
        if (collider is BoxCollider box)
            return "box:" + VectorKey(box.center) + ":" + VectorKey(box.size) + ":" + box.isTrigger;
        if (collider is MeshCollider mesh)
            return "mesh:" + mesh.convex + ":" + (int)mesh.cookingOptions + ":" + mesh.isTrigger + ":" +
                   (mesh.sharedMesh == null ? 0 : mesh.sharedMesh.vertexCount) + ":" +
                   (mesh.sharedMesh == null ? 0 : MeshTriangleCount(mesh.sharedMesh)) + ":" +
                   (mesh.sharedMesh == null ? "none" : VectorKey(mesh.sharedMesh.bounds.center)) + ":" +
                   (mesh.sharedMesh == null ? "none" : VectorKey(mesh.sharedMesh.bounds.size));
        return collider.GetType().FullName + ":" + collider.isTrigger;
    }

    private static void ValidateDynamicOutputs(GameObject optimized)
    {
        Dictionary<string, Transform> transforms = optimized.GetComponentsInChildren<Transform>(true)
            .GroupBy(transform => transform.name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (string anchorName in DynamicAnchorNames)
        {
            Transform anchor;
            if (!transforms.TryGetValue(anchorName, out anchor))
                throw new InvalidOperationException("Moving anchor missing: " + anchorName);
            MeshRenderer[] outputs = anchor.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("BATCH_DYNAMIC_", StringComparison.Ordinal))
                .ToArray();
            if (outputs.Length != 1)
                throw new InvalidOperationException("Moving anchor does not own exactly one compact renderer: " + anchorName);
            if (outputs[0].transform.parent != anchor)
                throw new InvalidOperationException("Moving visual is not an identity child of its anchor: " + anchorName);
            if (!Nearly(outputs[0].transform.localPosition, Vector3.zero, 0.000001f) ||
                Quaternion.Angle(outputs[0].transform.localRotation, Quaternion.identity) > 0.0001f ||
                !Nearly(outputs[0].transform.localScale, Vector3.one, 0.000001f))
                throw new InvalidOperationException("Moving visual child is not zeroed: " + anchorName);
        }
    }

    private static Metrics CaptureMetrics(GameObject root, string localAssetRoot)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        HashSet<Mesh> uniqueMeshes = new HashSet<Mesh>();
        HashSet<Material> materials = new HashSet<Material>();
        HashSet<Texture> textures = new HashSet<Texture>();
        int vertices = 0;
        int triangles = 0;
        int slots = 0;
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (MeshRenderer renderer in renderers)
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
            slots += renderer.sharedMaterials.Length;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                materials.Add(material);
                foreach (string property in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(property);
                    if (texture != null) textures.Add(texture);
                }
            }
            Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) continue;
            uniqueMeshes.Add(mesh);
            vertices += mesh.vertexCount;
            triangles += MeshTriangleCount(mesh);
        }
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        return new Metrics
        {
            gameObjectCount = root.GetComponentsInChildren<Transform>(true).Length,
            rendererCount = renderers.Length,
            materialSlotCount = slots,
            uniqueMeshCount = uniqueMeshes.Count,
            materialCount = materials.Count,
            referencedTextureCount = textures.Count(texture =>
                AssetDatabase.GetAssetPath(texture).StartsWith(localAssetRoot + "/Textures/", StringComparison.Ordinal)),
            colliderCount = colliders.Length,
            boxColliderCount = colliders.OfType<BoxCollider>().Count(),
            meshColliderCount = colliders.OfType<MeshCollider>().Count(),
            instanceVertexCount = vertices,
            instanceTriangleCount = triangles,
            boundsCenter = bounds.center,
            boundsSize = bounds.size
        };
    }

    private static void ValidateMetrics(Metrics source, Metrics optimized)
    {
        if (optimized.instanceTriangleCount != source.instanceTriangleCount)
            throw new InvalidOperationException($"Triangle count changed: {source.instanceTriangleCount}->{optimized.instanceTriangleCount}");
        if (optimized.instanceVertexCount != source.instanceVertexCount)
            throw new InvalidOperationException($"Vertex count changed: {source.instanceVertexCount}->{optimized.instanceVertexCount}");
        if (optimized.colliderCount != 461 || optimized.boxColliderCount != 455 || optimized.meshColliderCount != 6)
            throw new InvalidOperationException("Collider budget/fingerprint count changed.");
        if (!Nearly(source.boundsCenter, optimized.boundsCenter, BoundsTolerance) ||
            !Nearly(source.boundsSize, optimized.boundsSize, BoundsTolerance))
            throw new InvalidOperationException("Rendered bounds changed beyond 1 cm.");
        if (optimized.rendererCount > 40 || optimized.materialSlotCount > 45 || optimized.uniqueMeshCount > 40)
            throw new InvalidOperationException(
                $"Ultra budget failed: renderers={optimized.rendererCount}, slots={optimized.materialSlotCount}, meshes={optimized.uniqueMeshCount}");
        if (optimized.materialCount != 4 || optimized.referencedTextureCount != 1)
            throw new InvalidOperationException(
                $"Dependency budget failed: materials={optimized.materialCount}, localTextures={optimized.referencedTextureCount}");
    }

    private static void ValidateDependencies(string targetRoot, string targetPrefab, int outputGroups)
    {
        string[] leaked = AssetDatabase.GetDependencies(targetPrefab, true)
            .Where(path => path.StartsWith(SourceRoot + "/", StringComparison.Ordinal))
            .ToArray();
        if (leaked.Length > 0)
            throw new InvalidOperationException("Optimized prefab leaks source-local dependencies:\n" + string.Join("\n", leaked));
        int materials = AssetDatabase.FindAssets("t:Material", new[] { targetRoot + "/Materials" }).Length;
        int textures = AssetDatabase.FindAssets("t:Texture2D", new[] { targetRoot + "/Textures" }).Length;
        int shaders = AssetDatabase.FindAssets("t:Shader", new[] { targetRoot + "/Shaders" }).Length;
        int meshes = AssetDatabase.FindAssets("t:Mesh", new[] { targetRoot + "/Meshes" }).Length;
        if (materials != 4 || textures != 1 || shaders != 1 || meshes != outputGroups + 6)
            throw new InvalidOperationException(
                $"Unexpected local assets: materials={materials}, textures={textures}, shaders={shaders}, meshes={meshes}, groups={outputGroups}");
    }

    private static int MeshTriangleCount(Mesh mesh)
    {
        int count = 0;
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            count += checked((int)(mesh.GetIndexCount(submesh) / 3));
        return count;
    }

    private static float Determinant3x3(Matrix4x4 matrix)
    {
        return matrix.m00 * (matrix.m11 * matrix.m22 - matrix.m12 * matrix.m21)
             - matrix.m01 * (matrix.m10 * matrix.m22 - matrix.m12 * matrix.m20)
             + matrix.m02 * (matrix.m10 * matrix.m21 - matrix.m11 * matrix.m20);
    }

    private static int PositiveMod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static string RelativePath(Transform transform, Transform root)
    {
        if (transform == root) return "";
        Stack<string> names = new Stack<string>();
        for (Transform cursor = transform; cursor != null && cursor != root; cursor = cursor.parent)
            names.Push(cursor.name);
        return string.Join("/", names);
    }

    private static bool Nearly(Vector3 left, Vector3 right, float epsilon)
    {
        return (left - right).sqrMagnitude <= epsilon * epsilon;
    }

    private static string VectorKey(Vector4 value)
    {
        return string.Join(",", new[]
        {
            value.x.ToString("R", CultureInfo.InvariantCulture),
            value.y.ToString("R", CultureInfo.InvariantCulture),
            value.z.ToString("R", CultureInfo.InvariantCulture),
            value.w.ToString("R", CultureInfo.InvariantCulture)
        });
    }

    private static string Sanitize(string value)
    {
        return new string(value.Select(character =>
            char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_').ToArray());
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
            if (args[index] == name) return args[index + 1];
        return null;
    }

    private static long DirectoryBytes(string path)
    {
        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length)
            : 0L;
    }

    private static float PercentReduction(int before, int after)
    {
        return before == 0 ? 0f : (before - after) * 100f / before;
    }

    private enum RenderClass { Opaque, Glass, Water }

    private sealed class BatchSource
    {
        public readonly MeshRenderer renderer;
        public readonly MeshFilter filter;
        public readonly RenderClass renderClass;
        public BatchSource(MeshRenderer renderer, MeshFilter filter, RenderClass renderClass)
        {
            this.renderer = renderer;
            this.filter = filter;
            this.renderClass = renderClass;
        }
    }

    private sealed class BatchGroup
    {
        public readonly string name;
        public readonly Transform parent;
        public readonly bool moving;
        public readonly List<BatchSource> sources = new List<BatchSource>();
        public BatchGroup(string name, Transform parent, bool moving)
        {
            this.name = name;
            this.parent = parent;
            this.moving = moving;
        }
    }

    [Serializable]
    private sealed class NodeFingerprint
    {
        public string path;
        public int siblingIndex;
        public bool activeSelf;
        public int layer;
        public string tag;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public string colliderType;
        public string colliderSignature;
    }

    [Serializable]
    public sealed class Metrics
    {
        public int gameObjectCount;
        public int rendererCount;
        public int materialSlotCount;
        public int uniqueMeshCount;
        public int materialCount;
        public int referencedTextureCount;
        public int colliderCount;
        public int boxColliderCount;
        public int meshColliderCount;
        public int instanceVertexCount;
        public int instanceTriangleCount;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
    }

    [Serializable]
    private sealed class OptimizationReport
    {
        public bool passed;
        public string sourcePrefab;
        public string optimizedPrefab;
        public Metrics source;
        public Metrics optimized;
        public int sourceOpaqueMaterialCount;
        public int outputGroupCount;
        public float rendererReductionPercent;
        public float materialSlotReductionPercent;
        public float uniqueMeshReductionPercent;
        public long sourceLocalAssetBytes;
        public long optimizedLocalAssetBytes;
        public string[] notes;
    }
}
