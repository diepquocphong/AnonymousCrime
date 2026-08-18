using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class VillaOriginalOptimizer
{
    private const string SourceRoot = "Assets/Model/franklin-villa-estate-unity";
    private const string SourcePrefab = SourceRoot + "/franklin-villa-estate-unity.prefab";
    private const string DefaultTargetRoot = "Assets/Model/franklin-villa-estate-mobile-optimized";
    private const string RampRootName = "FranklinVilla_Hierarchy_ExternalVehicleRamp_001";
    private const string InteractivePrefix = "FranklinVilla_Interactive_";
    private const float BoundsTolerance = 0.01f;

    private static int sMeshIndex;

    public static void BuildAndAudit()
    {
        string targetRoot = GetArgument("-villaOptimizedRoot");
        if (string.IsNullOrWhiteSpace(targetRoot)) targetRoot = DefaultTargetRoot;
        string reportPath = GetArgument("-villaOptimizationReport");
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            reportPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../VillaOriginalOptimizationReport.json"));
        }

        if (!targetRoot.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException("Target root must be below Assets: " + targetRoot);
        if (targetRoot == SourceRoot || targetRoot.StartsWith(SourceRoot + "/", StringComparison.Ordinal))
            throw new ArgumentException("Refusing to overwrite or nest inside the original villa folder.");

        GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (sourceAsset == null) throw new FileNotFoundException("Source prefab missing", SourcePrefab);

        Metrics sourceMetrics = CaptureMetrics(sourceAsset, SourceRoot);
        List<NodeFingerprint> sourceProtected = CaptureProtectedNodes(sourceAsset);
        if (sourceMetrics.rendererCount != 461 || sourceMetrics.colliderCount != 461)
            throw new InvalidOperationException(
                $"Unexpected source baseline: renderers={sourceMetrics.rendererCount}, colliders={sourceMetrics.colliderCount}");
        if (sourceMetrics.instanceTriangleCount != 16520)
            throw new InvalidOperationException(
                $"Unexpected source triangle baseline: {sourceMetrics.instanceTriangleCount}");

        if (AssetDatabase.IsValidFolder(targetRoot) && !AssetDatabase.DeleteAsset(targetRoot))
            throw new IOException("Could not delete previous exact target folder: " + targetRoot);

        EnsureFolder(targetRoot);
        string materialsRoot = targetRoot + "/Materials";
        string meshesRoot = targetRoot + "/Meshes";
        string texturesRoot = targetRoot + "/Textures";
        EnsureFolder(materialsRoot);
        EnsureFolder(meshesRoot);
        EnsureFolder(texturesRoot);

        Dictionary<Texture, Texture> textureMap = CopyAndOptimizeUsedTextures(sourceAsset, texturesRoot);
        Dictionary<Material, Material> materialMap = CopyUsedMaterials(sourceAsset, materialsRoot, textureMap);

        string targetPrefab = targetRoot + "/franklin-villa-estate-mobile-optimized.prefab";
        sMeshIndex = 0;
        GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefab);
        try
        {
            root.name = "franklin-villa-estate-mobile-optimized";
            MeshRenderer[] allRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            HashSet<Mesh> colliderSourceMeshes = new HashSet<Mesh>(
                root.GetComponentsInChildren<MeshCollider>(true)
                    .Where(collider => collider.sharedMesh != null)
                    .Select(collider => collider.sharedMesh));
            Dictionary<Mesh, Mesh> protectedMeshMap = new Dictionary<Mesh, Mesh>();

            foreach (MeshRenderer renderer in allRenderers)
                renderer.sharedMaterials = RemapMaterials(renderer.sharedMaterials, materialMap);

            // Protect every interactive subtree and the complete driveable ramp subtree.
            // Inactive renderers are also kept one-for-one so batching never changes activation semantics.
            foreach (MeshRenderer renderer in allRenderers)
            {
                if (!IsProtected(renderer.transform) && renderer.gameObject.activeInHierarchy) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                filter.sharedMesh = CloneProtectedMesh(
                    filter.sharedMesh,
                    meshesRoot,
                    colliderSourceMeshes.Contains(filter.sharedMesh),
                    protectedMeshMap);
            }

            foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.sharedMesh == null) continue;
                collider.sharedMesh = CloneProtectedMesh(
                    collider.sharedMesh,
                    meshesRoot,
                    true,
                    protectedMeshMap);
            }

            List<BatchSource> sources = new List<BatchSource>();
            foreach (MeshRenderer renderer in allRenderers)
            {
                if (IsProtected(renderer.transform) || !renderer.gameObject.activeInHierarchy) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                sources.Add(new BatchSource(renderer, filter));
            }

            GameObject visuals = new GameObject("OPTIMIZED_STATIC_VISUALS");
            visuals.transform.SetParent(root.transform, false);

            List<IGrouping<string, BatchSource>> groups = sources
                .GroupBy(source => BuildBatchKey(root.transform, source))
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToList();

            int batchNumber = 0;
            HashSet<BatchSource> combinedSources = new HashSet<BatchSource>();
            foreach (IGrouping<string, BatchSource> group in groups)
            {
                List<BatchSource> groupSources = group.ToList();
                if (groupSources.Count == 1)
                {
                    // A one-renderer "batch" cannot reduce a draw call. Preserve its exact transform and
                    // renderer bounds because transform baking can subtly change shadow bias/probe sampling.
                    BatchSource single = groupSources[0];
                    single.filter.sharedMesh = CloneProtectedMesh(
                        single.filter.sharedMesh,
                        meshesRoot,
                        false,
                        protectedMeshMap);
                    continue;
                }
                BuildStaticBatch(root.transform, visuals.transform, groupSources, meshesRoot, batchNumber++);
                foreach (BatchSource source in groupSources) combinedSources.Add(source);
            }

            foreach (BatchSource source in combinedSources)
            {
                UnityEngine.Object.DestroyImmediate(source.renderer, true);
                UnityEngine.Object.DestroyImmediate(source.filter, true);
            }

            PrefabUtility.SaveAsPrefabAsset(root, targetPrefab, out bool saved);
            if (!saved) throw new IOException("Failed to save optimized villa prefab: " + targetPrefab);
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
        List<NodeFingerprint> optimizedProtected = CaptureProtectedNodes(optimizedAsset);
        CompareProtectedNodes(sourceProtected, optimizedProtected);
        ValidateMetrics(sourceMetrics, optimizedMetrics);
        ValidateDependencies(targetRoot, targetPrefab);

        OptimizationReport report = new OptimizationReport
        {
            passed = true,
            sourcePrefab = SourcePrefab,
            optimizedPrefab = targetPrefab,
            source = sourceMetrics,
            optimized = optimizedMetrics,
            protectedNodeCount = optimizedProtected.Count,
            rendererReductionPercent = PercentReduction(sourceMetrics.rendererCount, optimizedMetrics.rendererCount),
            materialSlotReductionPercent = PercentReduction(sourceMetrics.materialSlotCount, optimizedMetrics.materialSlotCount),
            sourceLocalAssetBytes = DirectoryBytes(AssetPathToFullPath(SourceRoot)),
            optimizedLocalAssetBytes = DirectoryBytes(AssetPathToFullPath(targetRoot)),
            notes = new[]
            {
                "Original source prefab and folder were not modified.",
                "All interactive and vehicle-ramp subtrees retain their hierarchy, transforms, renderers and colliders.",
                "Only static visual MeshRenderer/MeshFilter components were replaced with regional material-signature batches.",
                "All original triangles and all 461 colliders are retained; no polygon decimation was used.",
                "Only the 26 referenced materials and 5 referenced textures are copied.",
                "Textures are RGB, Max Size 256, mipmapped, non-readable, BC1 on Standalone and ASTC 4x4 on mobile.",
                "Glass and pool water retain the original zwrite + transparent two-material pass order."
            }
        };

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? ".");
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        Debug.Log(
            $"VILLA_ORIGINAL_OPTIMIZATION_SUCCESS prefab={targetPrefab} " +
            $"renderers={sourceMetrics.rendererCount}->{optimizedMetrics.rendererCount} " +
            $"slots={sourceMetrics.materialSlotCount}->{optimizedMetrics.materialSlotCount} " +
            $"tris={optimizedMetrics.instanceTriangleCount} colliders={optimizedMetrics.colliderCount} " +
            $"report={reportPath}");
    }

    private static Dictionary<Texture, Texture> CopyAndOptimizeUsedTextures(GameObject sourceAsset, string targetRoot)
    {
        HashSet<Texture> textures = new HashSet<Texture>();
        foreach (Material material in CollectUsedMaterials(sourceAsset))
        {
            foreach (string property in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(property);
                string path = texture == null ? null : AssetDatabase.GetAssetPath(texture);
                if (!string.IsNullOrEmpty(path) && path.StartsWith(SourceRoot + "/Textures/", StringComparison.Ordinal))
                    textures.Add(texture);
            }
        }

        Dictionary<Texture, Texture> result = new Dictionary<Texture, Texture>();
        foreach (Texture source in textures.OrderBy(texture => AssetDatabase.GetAssetPath(texture), StringComparer.Ordinal))
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string targetPath = targetRoot + "/" + Path.GetFileName(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                throw new IOException($"Failed to copy texture {sourcePath} -> {targetPath}");
            RewritePngAsRgb(targetPath);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ConfigureTextureImporter(targetPath);
            Texture target = AssetDatabase.LoadAssetAtPath<Texture>(targetPath);
            if (target == null) throw new IOException("Copied texture did not load: " + targetPath);
            result[source] = target;
        }

        if (result.Count != 5)
            throw new InvalidOperationException("Expected exactly 5 used textures, found " + result.Count);
        return result;
    }

    private static void RewritePngAsRgb(string assetPath)
    {
        string fullPath = AssetPathToFullPath(assetPath);
        byte[] input = File.ReadAllBytes(fullPath);
        Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        Texture2D rgb = null;
        try
        {
            if (!ImageConversion.LoadImage(decoded, input, false))
                throw new InvalidDataException("Could not decode PNG: " + assetPath);
            Color32[] pixels = decoded.GetPixels32();
            for (int index = 0; index < pixels.Length; index++) pixels[index].a = 255;
            rgb = new Texture2D(decoded.width, decoded.height, TextureFormat.RGB24, false, false);
            rgb.SetPixels32(pixels);
            rgb.Apply(false, false);
            File.WriteAllBytes(fullPath, ImageConversion.EncodeToPNG(rgb));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(decoded);
            if (rgb != null) UnityEngine.Object.DestroyImmediate(rgb);
        }
    }

    private static void ConfigureTextureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("TextureImporter missing: " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = false;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 1;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.compressionQuality = 70;

        SetPlatform(importer, "Standalone", TextureImporterFormat.DXT1);
        SetPlatform(importer, "Android", TextureImporterFormat.ASTC_4x4);
        SetPlatform(importer, "iPhone", TextureImporterFormat.ASTC_4x4);
        importer.SaveAndReimport();
    }

    private static void SetPlatform(TextureImporter importer, string platform, TextureImporterFormat format)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
        settings.name = platform;
        settings.overridden = true;
        settings.maxTextureSize = 256;
        settings.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
        settings.format = format;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.compressionQuality = 70;
        importer.SetPlatformTextureSettings(settings);
    }

    private static Dictionary<Material, Material> CopyUsedMaterials(
        GameObject sourceAsset,
        string targetRoot,
        Dictionary<Texture, Texture> textureMap)
    {
        List<Material> used = CollectUsedMaterials(sourceAsset)
            .OrderBy(material => AssetDatabase.GetAssetPath(material), StringComparer.Ordinal)
            .ToList();
        if (used.Count != 26)
            throw new InvalidOperationException("Expected exactly 26 used materials, found " + used.Count);

        Dictionary<Material, Material> result = new Dictionary<Material, Material>();
        foreach (Material source in used)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string targetPath = targetRoot + "/" + Path.GetFileName(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                throw new IOException($"Failed to copy material {sourcePath} -> {targetPath}");
            Material target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (target == null) throw new IOException("Copied material did not load: " + targetPath);
            foreach (string property in target.GetTexturePropertyNames())
            {
                Texture sourceTexture = source.GetTexture(property);
                if (sourceTexture != null && textureMap.TryGetValue(sourceTexture, out Texture mapped))
                    target.SetTexture(property, mapped);
            }
            EditorUtility.SetDirty(target);
            result[source] = target;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static HashSet<Material> CollectUsedMaterials(GameObject asset)
    {
        HashSet<Material> result = new HashSet<Material>();
        foreach (MeshRenderer renderer in asset.GetComponentsInChildren<MeshRenderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (material != null) result.Add(material);
        return result;
    }

    private static Material[] RemapMaterials(Material[] source, Dictionary<Material, Material> map)
    {
        Material[] result = new Material[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == null || !map.TryGetValue(source[index], out Material target))
                throw new InvalidOperationException("Missing copied material mapping for " + (source[index] == null ? "null" : source[index].name));
            result[index] = target;
        }
        return result;
    }

    private static Mesh CloneProtectedMesh(
        Mesh source,
        string meshesRoot,
        bool colliderMesh,
        Dictionary<Mesh, Mesh> map)
    {
        if (map.TryGetValue(source, out Mesh existing)) return existing;
        Mesh clone = UnityEngine.Object.Instantiate(source);
        clone.name = "PRESERVED_" + Sanitize(source.name) + "_" + sMeshIndex.ToString("D3", CultureInfo.InvariantCulture);
        clone.OptimizeIndexBuffers();
        clone.OptimizeReorderVertexBuffer();
        MeshUtility.SetMeshCompression(
            clone,
            ModelImporterMeshCompression.Off);
        string path = meshesRoot + "/" + clone.name + ".asset";
        AssetDatabase.CreateAsset(clone, path);
        if (!colliderMesh) clone.UploadMeshData(true);
        EditorUtility.SetDirty(clone);
        map[source] = clone;
        sMeshIndex++;
        return clone;
    }

    private static void BuildStaticBatch(
        Transform root,
        Transform parent,
        List<BatchSource> sources,
        string meshesRoot,
        int batchNumber)
    {
        BatchSource first = sources[0];
        string zone = BuildZone(root, first);
        string materialLabel = string.Join("_", first.renderer.sharedMaterials.Select(material => material.name));
        string safeName = $"BATCH_{batchNumber:D3}_{Sanitize(zone)}_{Sanitize(materialLabel)}";

        GameObject go = new GameObject(safeName);
        go.transform.SetParent(parent, false);
        go.layer = first.renderer.gameObject.layer;
        go.tag = first.renderer.gameObject.tag;

        long vertexTotal = sources.Sum(source => (long)source.filter.sharedMesh.vertexCount);
        Mesh mesh = new Mesh
        {
            name = "MESH_" + safeName,
            indexFormat = vertexTotal > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        CombineInstance[] combines = new CombineInstance[sources.Count];
        for (int index = 0; index < sources.Count; index++)
        {
            combines[index] = new CombineInstance
            {
                mesh = sources[index].filter.sharedMesh,
                subMeshIndex = 0,
                transform = go.transform.worldToLocalMatrix * sources[index].filter.transform.localToWorldMatrix,
                lightmapScaleOffset = sources[index].renderer.lightmapScaleOffset,
                realtimeLightmapScaleOffset = sources[index].renderer.realtimeLightmapScaleOffset
            };
        }

        // Every item in a group has the same lightmap state and scale/offset.
        // Preserve UV2 and renderer scale/offset as-is; baking and then reapplying it would transform UV2 twice.
        mesh.CombineMeshes(combines, true, true, false);
        mesh.RecalculateBounds();
        mesh.OptimizeIndexBuffers();
        mesh.OptimizeReorderVertexBuffer();
        // The source is already only 16.5k triangles. Quantizing normals with Unity's mesh
        // compression produces a measurable lighting delta, so keep lossless vertex data and
        // get the memory/draw-call win from merging, deduplication and non-readable upload.
        MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Off);

        string meshPath = meshesRoot + "/" + mesh.name + ".asset";
        AssetDatabase.CreateAsset(mesh, meshPath);
        mesh.UploadMeshData(true);
        EditorUtility.SetDirty(mesh);

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = first.renderer.sharedMaterials;
        CopyRendererState(first.renderer, renderer);
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

    private static string BuildBatchKey(Transform root, BatchSource source)
    {
        MeshRenderer renderer = source.renderer;
        string materials = string.Join(",", renderer.sharedMaterials.Select(StableObjectId));
        string state = string.Join(";", new[]
        {
            renderer.gameObject.layer.ToString(CultureInfo.InvariantCulture),
            renderer.gameObject.tag,
            renderer.enabled ? "1" : "0",
            ((int)renderer.shadowCastingMode).ToString(CultureInfo.InvariantCulture),
            renderer.receiveShadows ? "1" : "0",
            renderer.staticShadowCaster ? "1" : "0",
            ((int)renderer.motionVectorGenerationMode).ToString(CultureInfo.InvariantCulture),
            ((int)renderer.lightProbeUsage).ToString(CultureInfo.InvariantCulture),
            ((int)renderer.reflectionProbeUsage).ToString(CultureInfo.InvariantCulture),
            ((int)renderer.rayTracingMode).ToString(CultureInfo.InvariantCulture),
            renderer.allowOcclusionWhenDynamic ? "1" : "0",
            renderer.renderingLayerMask.ToString(CultureInfo.InvariantCulture),
            renderer.rendererPriority.ToString(CultureInfo.InvariantCulture),
            ((int)renderer.receiveGI).ToString(CultureInfo.InvariantCulture),
            renderer.lightmapIndex.ToString(CultureInfo.InvariantCulture),
            VectorKey(renderer.lightmapScaleOffset),
            renderer.realtimeLightmapIndex.ToString(CultureInfo.InvariantCulture),
            VectorKey(renderer.realtimeLightmapScaleOffset),
            StableObjectId(renderer.probeAnchor),
            StableObjectId(renderer.lightProbeProxyVolumeOverride)
        });
        return BuildZone(root, source) + "|" + materials + "|" + state;
    }

    private static string BuildZone(Transform root, BatchSource source)
    {
        Vector3 center = root.InverseTransformPoint(source.renderer.bounds.center);
        bool transparentSignature = source.renderer.sharedMaterials.Length > source.filter.sharedMesh.subMeshCount;
        if (transparentSignature)
        {
            int cellX = Mathf.FloorToInt((center.x + 36f) / 12f);
            int cellZ = Mathf.FloorToInt((center.z + 32f) / 12f);
            return FloorBand(center.y) + "_Transparent_X" + cellX + "_Z" + cellZ;
        }
        // Keep opaque batches spatially bounded for GTA-style camera culling and per-renderer lighting.
        // The source geometry is tiny, so 24 m cells retain a large draw-call reduction without turning
        // an entire floor/site into one sparse renderer with a far-away bounds center.
        int opaqueCellX = Mathf.FloorToInt((center.x + 36f) / 24f);
        int opaqueCellZ = Mathf.FloorToInt((center.z + 32f) / 24f);
        return FloorBand(center.y) + "_Opaque_X" + opaqueCellX + "_Z" + opaqueCellZ;
    }

    private static string FloorBand(float y)
    {
        if (y >= 7.2f) return "Rooftop";
        if (y >= 3.8f) return "UpperFloor";
        if (y < 0.75f) return "SiteExterior";
        return "GroundFloor";
    }

    private static bool IsProtected(Transform transform)
    {
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
        {
            if (cursor.name == RampRootName || cursor.name.StartsWith(InteractivePrefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static Metrics CaptureMetrics(GameObject root, string localAssetRoot)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        HashSet<Mesh> uniqueMeshes = new HashSet<Mesh>();
        HashSet<Material> materials = new HashSet<Material>();
        HashSet<Texture> textures = new HashSet<Texture>();
        int instanceVertices = 0;
        int instanceTriangles = 0;
        int slots = 0;
        bool hasBounds = false;
        Bounds bounds = default;

        foreach (MeshRenderer renderer in renderers)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
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
            instanceVertices += mesh.vertexCount;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                instanceTriangles += checked((int)(mesh.GetIndexCount(submesh) / 3));
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
            instanceVertexCount = instanceVertices,
            instanceTriangleCount = instanceTriangles,
            boundsCenter = bounds.center,
            boundsSize = bounds.size
        };
    }

    private static List<NodeFingerprint> CaptureProtectedNodes(GameObject root)
    {
        List<NodeFingerprint> result = new List<NodeFingerprint>();
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (!IsProtected(transform)) continue;
            MeshRenderer renderer = transform.GetComponent<MeshRenderer>();
            MeshFilter filter = transform.GetComponent<MeshFilter>();
            Collider collider = transform.GetComponent<Collider>();
            result.Add(new NodeFingerprint
            {
                path = RelativePath(transform, root.transform),
                siblingIndex = transform.GetSiblingIndex(),
                activeSelf = transform.gameObject.activeSelf,
                layer = transform.gameObject.layer,
                tag = transform.gameObject.tag,
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale,
                rendererPresent = renderer != null,
                materialSignature = renderer == null ? "" : string.Join(",", renderer.sharedMaterials.Select(material => material == null ? "null" : material.name)),
                vertexCount = filter == null || filter.sharedMesh == null ? 0 : filter.sharedMesh.vertexCount,
                triangleCount = filter == null || filter.sharedMesh == null ? 0 : MeshTriangleCount(filter.sharedMesh),
                colliderType = collider == null ? "" : collider.GetType().Name,
                colliderSignature = ColliderSignature(collider)
            });
        }
        result.Sort((left, right) => StringComparer.Ordinal.Compare(left.path, right.path));
        return result;
    }

    private static void CompareProtectedNodes(List<NodeFingerprint> source, List<NodeFingerprint> optimized)
    {
        if (source.Count != optimized.Count)
            throw new InvalidOperationException($"Protected node count changed: {source.Count} -> {optimized.Count}");
        for (int index = 0; index < source.Count; index++)
        {
            NodeFingerprint left = source[index];
            NodeFingerprint right = optimized[index];
            if (left.path != right.path || left.siblingIndex != right.siblingIndex || left.activeSelf != right.activeSelf ||
                left.layer != right.layer || left.tag != right.tag || !Nearly(left.localPosition, right.localPosition, 0.000001f) ||
                Quaternion.Angle(left.localRotation, right.localRotation) > 0.0001f || !Nearly(left.localScale, right.localScale, 0.000001f) ||
                left.rendererPresent != right.rendererPresent || left.materialSignature != right.materialSignature ||
                left.vertexCount != right.vertexCount || left.triangleCount != right.triangleCount ||
                left.colliderType != right.colliderType || left.colliderSignature != right.colliderSignature)
            {
                throw new InvalidOperationException("Protected hierarchy changed at " + left.path);
            }
        }
    }

    private static void ValidateMetrics(Metrics source, Metrics optimized)
    {
        if (optimized.instanceTriangleCount != source.instanceTriangleCount)
            throw new InvalidOperationException($"Rendered triangle count changed: {source.instanceTriangleCount} -> {optimized.instanceTriangleCount}");
        if (optimized.colliderCount != 461 || optimized.boxColliderCount != 455 || optimized.meshColliderCount != 6)
            throw new InvalidOperationException(
                $"Collider count changed: total={optimized.colliderCount}, box={optimized.boxColliderCount}, mesh={optimized.meshColliderCount}");
        if (!Nearly(source.boundsCenter, optimized.boundsCenter, BoundsTolerance) ||
            !Nearly(source.boundsSize, optimized.boundsSize, BoundsTolerance))
            throw new InvalidOperationException(
                $"Bounds changed beyond {BoundsTolerance}m: source={source.boundsCenter}/{source.boundsSize}, optimized={optimized.boundsCenter}/{optimized.boundsSize}");
        if (optimized.materialCount != 26)
            throw new InvalidOperationException("Expected 26 exact used materials, found " + optimized.materialCount);
        if (optimized.referencedTextureCount != 5)
            throw new InvalidOperationException("Expected 5 referenced local textures, found " + optimized.referencedTextureCount);
        if (optimized.materialSlotCount > 220)
            throw new InvalidOperationException("Material slot budget exceeded: " + optimized.materialSlotCount);
        if (optimized.rendererCount >= source.rendererCount)
            throw new InvalidOperationException("Renderer count was not reduced.");
    }

    private static void ValidateDependencies(string targetRoot, string targetPrefab)
    {
        string[] dependencies = AssetDatabase.GetDependencies(targetPrefab, true);
        string[] leaked = dependencies
            .Where(path => path.StartsWith(SourceRoot + "/", StringComparison.Ordinal))
            .ToArray();
        if (leaked.Length > 0)
            throw new InvalidOperationException("Optimized prefab still references source-local assets:\n" + string.Join("\n", leaked));

        int localMaterials = AssetDatabase.FindAssets("t:Material", new[] { targetRoot + "/Materials" }).Length;
        int localTextures = AssetDatabase.FindAssets("t:Texture2D", new[] { targetRoot + "/Textures" }).Length;
        if (localMaterials != 26 || localTextures != 5)
            throw new InvalidOperationException($"Unexpected local dependency counts: materials={localMaterials}, textures={localTextures}");
    }

    private static string ColliderSignature(Collider collider)
    {
        if (collider == null) return "";
        if (collider is BoxCollider box)
            return $"box:{VectorKey(box.center)}:{VectorKey(box.size)}:{box.isTrigger}:{StableObjectId(box.sharedMaterial)}";
        if (collider is MeshCollider mesh)
            return $"mesh:{mesh.convex}:{(int)mesh.cookingOptions}:{mesh.isTrigger}:{(mesh.sharedMesh == null ? 0 : mesh.sharedMesh.vertexCount)}:{(mesh.sharedMesh == null ? 0 : MeshTriangleCount(mesh.sharedMesh))}:{StableObjectId(mesh.sharedMaterial)}";
        return collider.GetType().FullName + ":" + collider.isTrigger;
    }

    private static int MeshTriangleCount(Mesh mesh)
    {
        int result = 0;
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            result += checked((int)(mesh.GetIndexCount(submesh) / 3));
        return result;
    }

    private static string RelativePath(Transform transform, Transform root)
    {
        if (transform == root) return "";
        Stack<string> names = new Stack<string>();
        for (Transform cursor = transform; cursor != null && cursor != root; cursor = cursor.parent)
            names.Push(cursor.name);
        return string.Join("/", names);
    }

    private static string StableObjectId(UnityEngine.Object value)
    {
        if (value == null) return "null";
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId))
            return guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
        if (value is Component component) return RelativeScenePath(component.transform);
        return value.name;
    }

    private static string RelativeScenePath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent) names.Push(cursor.name);
        return string.Join("/", names);
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
        StringBuilder result = new StringBuilder(value.Length);
        foreach (char character in value)
            result.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
        return result.ToString();
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

    private static long DirectoryBytes(string root)
    {
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length)
            : 0L;
    }

    private static float PercentReduction(int before, int after)
    {
        return before <= 0 ? 0f : (before - after) * 100f / before;
    }

    private static bool Nearly(Vector3 left, Vector3 right, float tolerance)
    {
        return (left - right).sqrMagnitude <= tolerance * tolerance;
    }

    private static string GetArgument(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
            if (args[index] == key) return args[index + 1];
        return null;
    }

    private sealed class BatchSource
    {
        public readonly MeshRenderer renderer;
        public readonly MeshFilter filter;

        public BatchSource(MeshRenderer renderer, MeshFilter filter)
        {
            this.renderer = renderer;
            this.filter = filter;
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
        public bool rendererPresent;
        public string materialSignature;
        public int vertexCount;
        public int triangleCount;
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
    public sealed class OptimizationReport
    {
        public bool passed;
        public string sourcePrefab;
        public string optimizedPrefab;
        public Metrics source;
        public Metrics optimized;
        public int protectedNodeCount;
        public float rendererReductionPercent;
        public float materialSlotReductionPercent;
        public long sourceLocalAssetBytes;
        public long optimizedLocalAssetBytes;
        public string[] notes;
    }
}
