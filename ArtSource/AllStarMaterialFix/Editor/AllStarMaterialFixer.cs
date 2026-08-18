using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

public static class AllStarMaterialFixer
{
    private const string Root = "Assets/AllStarCharacterLibrary";
    private const string LitShaderName = "Universal Render Pipeline/Lit";
    private const string UnlitShaderName = "Universal Render Pipeline/Unlit";

    [Serializable]
    private sealed class MaterialResult
    {
        public string path;
        public string name;
        public string shader;
        public bool transparent;
        public bool baseTexturePreserved;
        public string baseTexturePath;
        public Color baseColor;
        public float metallic;
        public float smoothness;
    }

    [Serializable]
    private sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string root;
        public int materialCount;
        public int opaqueLitCount;
        public int transparentLitCount;
        public int transparentUnlitCount;
        public int materialsWithBaseTexture;
        public int preservedBaseTextures;
        public int gameObjectAssetCount;
        public int reimportedModelAssetCount;
        public int rendererCount;
        public int rendererMaterialSlots;
        public int repairedLegacyPrefabMaterialSlots;
        public int removedMissingScriptComponents;
        public int missingRendererMaterialSlots;
        public int unsupportedRendererMaterialSlots;
        public string[] missingRendererMaterialDetails;
        public MaterialResult[] materials;
    }

    private sealed class Snapshot
    {
        public string Path;
        public string Name;
        public string BaseTextureGuid;
        public string NormalTextureGuid;
        public string EmissionTextureGuid;
        public Color BaseColor;
        public Color EmissionColor;
        public float Metallic;
        public float Smoothness;
        public float BumpScale;
        public bool Transparent;
        public bool Skidmarks;
    }

    public static void ConvertAndValidate()
    {
        Shader lit = Shader.Find(LitShaderName);
        Shader unlit = Shader.Find(UnlitShaderName);
        if (lit == null || unlit == null)
            throw new InvalidOperationException("URP Lit/Unlit shaders are unavailable");

        string[] paths = AssetDatabase.FindAssets("t:Material", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (paths.Length == 0) throw new InvalidOperationException("No AllStar materials found");

        List<Snapshot> snapshots = paths.Select(ReadSnapshot).ToList();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (Snapshot snapshot in snapshots)
                Convert(snapshot, lit, unlit);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        int reimportedModelAssetCount = ReimportModelAssets();
        int removedMissingScriptComponents;
        int repairedLegacyPrefabMaterialSlots = RepairLegacyPrefabMaterials(out removedMissingScriptComponents);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Report report = Validate(
            snapshots,
            repairedLegacyPrefabMaterialSlots,
            removedMissingScriptComponents,
            reimportedModelAssetCount
        );
        WriteReport(report);
        Debug.Log(
            $"ALLSTAR_MATERIAL_FIX_SUCCESS materials={report.materialCount} "
                + $"opaque={report.opaqueLitCount} transparent={report.transparentLitCount} "
                + $"skid={report.transparentUnlitCount} renderers={report.rendererCount} "
                + $"slots={report.rendererMaterialSlots} repaired={report.repairedLegacyPrefabMaterialSlots} "
                + $"removedMissingScripts={report.removedMissingScriptComponents}"
        );
    }

    private static int ReimportModelAssets()
    {
        string[] modelPaths = AssetDatabase.FindAssets("t:Model", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        foreach (string path in modelPaths)
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport
            );
        return modelPaths.Length;
    }

    private static int RepairLegacyPrefabMaterials(out int removedMissingScriptComponents)
    {
        Dictionary<string, string> prefabModels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { Root + "/Prefabs/Bus.prefab", Root + "/Models/Vehicles/Bus/Bus.FBX" },
            { Root + "/Prefabs/DZ.prefab", Root + "/Models/Vehicles/DZ/DZ.FBX" },
            { Root + "/Prefabs/DZClassic.prefab", Root + "/Models/Vehicles/DZClassic/DZClassic.FBX" },
            { Root + "/Prefabs/Goat.prefab", Root + "/Models/Vehicles/Goat/Goat.FBX" },
            { Root + "/Prefabs/Gyrocopter.prefab", Root + "/Models/Vehicles/Gyrocopter/Gyrocopter.FBX" },
            { Root + "/Prefabs/Paramotor.prefab", Root + "/Models/Vehicles/Paraglider/Paramotor.FBX" },
            { Root + "/Prefabs/PoliceCar.prefab", Root + "/Models/Vehicles/PoliceCar/PoliceCar.FBX" },
            { Root + "/Prefabs/SUV.prefab", Root + "/Models/Vehicles/SUV/SUV.FBX" }
        };

        int repairedSlots = 0;
        removedMissingScriptComponents = 0;
        foreach (KeyValuePair<string, string> pair in prefabModels)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);
            if (model == null)
                throw new InvalidOperationException($"Legacy prefab source model is missing: {pair.Value}");

            List<GameObject> sourceModels = new List<GameObject> { model };
            if (pair.Key.EndsWith("/Paramotor.prefab", StringComparison.Ordinal))
            {
                const string canopyPath = Root + "/Models/Vehicles/Paraglider/Paraglider.FBX";
                GameObject canopy = AssetDatabase.LoadAssetAtPath<GameObject>(canopyPath);
                if (canopy == null)
                    throw new InvalidOperationException($"Paramotor canopy source model is missing: {canopyPath}");
                sourceModels.Add(canopy);
            }

            Dictionary<string, List<Renderer>> sourceByName = sourceModels
                .SelectMany(sourceModel => sourceModel.GetComponentsInChildren<Renderer>(true))
                .GroupBy(renderer => renderer.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(pair.Key);
            bool changed = false;
            try
            {
                foreach (Transform transform in prefabRoot.GetComponentsInChildren<Transform>(true))
                    removedMissingScriptComponents += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                        transform.gameObject
                    );

                foreach (Renderer renderer in prefabRoot.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] current = renderer.sharedMaterials;
                    int nullCount = current.Count(material => material == null);
                    if (nullCount == 0) continue;

                    if (!sourceByName.TryGetValue(renderer.name, out List<Renderer> candidates)
                        || candidates.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"No source renderer matches {pair.Key} :: {GetHierarchyPath(renderer.transform)}"
                        );
                    }

                    Renderer source = candidates
                        .OrderByDescending(candidate => HierarchySuffixScore(renderer.transform, candidate.transform))
                        .First();
                    Material[] replacements = source.sharedMaterials;
                    if (replacements.Length == 0 || replacements.Any(material => material == null))
                    {
                        throw new InvalidOperationException(
                            $"Source renderer has missing materials: {AssetDatabase.GetAssetPath(source)} "
                                + $":: {GetHierarchyPath(source.transform)}"
                        );
                    }

                    renderer.sharedMaterials = replacements;
                    repairedSlots += nullCount;
                    changed = true;
                }

                if (changed && PrefabUtility.SaveAsPrefabAsset(prefabRoot, pair.Key) == null)
                    throw new InvalidOperationException($"Failed to save repaired prefab: {pair.Key}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        return repairedSlots;
    }

    private static int HierarchySuffixScore(Transform left, Transform right)
    {
        string[] leftParts = GetHierarchyPath(left).Split('/');
        string[] rightParts = GetHierarchyPath(right).Split('/');
        int score = 0;
        while (score < leftParts.Length
            && score < rightParts.Length
            && string.Equals(
                leftParts[leftParts.Length - 1 - score],
                rightParts[rightParts.Length - 1 - score],
                StringComparison.Ordinal
            ))
        {
            score++;
        }
        return score;
    }

    private static Snapshot ReadSnapshot(string assetPath)
    {
        string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        string yaml = File.ReadAllText(absolute);
        string name = Path.GetFileNameWithoutExtension(assetPath);
        Color color = ExtractColor(yaml, "_BaseColor", ExtractColor(yaml, "_Color", Color.white));
        Color emission = ExtractColor(yaml, "_EmissionColor", Color.black);
        float metallic = Mathf.Clamp01(ExtractFloat(yaml, "_Metallic", 0f));
        float smoothness = Mathf.Clamp01(
            ExtractFloat(yaml, "_Smoothness", ExtractFloat(yaml, "_Glossiness", 0.5f))
        );
        float bumpScale = Mathf.Max(0f, ExtractFloat(yaml, "_BumpScale", 1f));
        int renderQueue = ExtractInt(yaml, @"^\s*m_CustomRenderQueue:\s*(-?\d+)\s*$", -1);
        bool skidmarks = assetPath.EndsWith("/Effects/Skids/Skidmarks.mat", StringComparison.Ordinal);
        bool transparent = skidmarks
            || renderQueue >= (int)RenderQueue.Transparent
            || color.a < 0.98f
            || assetPath.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0
            || assetPath.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0;

        return new Snapshot
        {
            Path = assetPath,
            Name = name,
            BaseTextureGuid = ExtractTextureGuid(yaml, "_BaseMap", "_MainTex"),
            NormalTextureGuid = ExtractTextureGuid(yaml, "_BumpMap", "_NormalMap"),
            EmissionTextureGuid = ExtractTextureGuid(yaml, "_EmissionMap", "_Illum"),
            BaseColor = color,
            EmissionColor = emission,
            Metallic = metallic,
            Smoothness = smoothness,
            BumpScale = bumpScale,
            Transparent = transparent,
            Skidmarks = skidmarks
        };
    }

    private static void Convert(Snapshot snapshot, Shader lit, Shader unlit)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(snapshot.Path);
        if (material == null) throw new InvalidOperationException($"Material missing: {snapshot.Path}");

        Shader targetShader = snapshot.Skidmarks ? unlit : lit;
        Material defaults = new Material(targetShader);
        EditorUtility.CopySerialized(defaults, material);
        UnityEngine.Object.DestroyImmediate(defaults);
        material.shaderKeywords = Array.Empty<string>();
        material.name = snapshot.Name;
        material.enableInstancing = true;
        material.doubleSidedGI = snapshot.Transparent;
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        Texture baseTexture = LoadTexture(snapshot.BaseTextureGuid);
        Texture normalTexture = LoadTexture(snapshot.NormalTextureGuid);
        Texture emissionTexture = LoadTexture(snapshot.EmissionTextureGuid);

        SetTextureIfPresent(material, "_BaseMap", baseTexture);
        SetTextureIfPresent(material, "_MainTex", baseTexture);
        SetColorIfPresent(material, "_BaseColor", snapshot.BaseColor);
        SetColorIfPresent(material, "_Color", snapshot.BaseColor);
        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_QueueOffset", 0f);
        SetFloatIfPresent(material, "_ReceiveShadows", 1f);
        SetFloatIfPresent(material, "_Surface", snapshot.Transparent ? 1f : 0f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_BlendModePreserveSpecular", snapshot.Skidmarks ? 0f : 1f);
        SetFloatIfPresent(material, "_Cull", snapshot.Transparent ? (float)CullMode.Off : (float)CullMode.Back);

        if (!snapshot.Skidmarks)
        {
            SetFloatIfPresent(material, "_WorkflowMode", 1f);
            SetFloatIfPresent(material, "_Metallic", snapshot.Transparent ? 0f : snapshot.Metallic);
            SetFloatIfPresent(
                material,
                "_Smoothness",
                snapshot.Transparent ? Mathf.Max(0.82f, snapshot.Smoothness) : snapshot.Smoothness
            );
            SetTextureIfPresent(material, "_BumpMap", normalTexture);
            SetFloatIfPresent(material, "_BumpScale", snapshot.BumpScale);
            SetTextureIfPresent(material, "_EmissionMap", emissionTexture);
            bool hasEmission = emissionTexture != null || snapshot.EmissionColor.maxColorComponent > 0.001f;
            SetColorIfPresent(material, "_EmissionColor", snapshot.EmissionColor);
            if (hasEmission) material.EnableKeyword("_EMISSION");

            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
            if (hasEmission) material.EnableKeyword("_EMISSION");
        }
        else
        {
            BaseShaderGUI.SetMaterialKeywords(material);
        }

        material.SetShaderPassEnabled("ShadowCaster", !snapshot.Transparent);
        material.SetShaderPassEnabled("DepthOnly", !snapshot.Transparent);
        EditorUtility.SetDirty(material);
    }

    private static Report Validate(
        IReadOnlyList<Snapshot> snapshots,
        int repairedLegacyPrefabMaterialSlots,
        int removedMissingScriptComponents,
        int reimportedModelAssetCount
    )
    {
        int opaque = 0;
        int transparent = 0;
        int skid = 0;
        int withTexture = 0;
        int preservedTexture = 0;
        List<MaterialResult> results = new List<MaterialResult>(snapshots.Count);

        foreach (Snapshot snapshot in snapshots)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(snapshot.Path);
            if (material == null || material.shader == null || !material.shader.isSupported)
                throw new InvalidOperationException($"Unsupported material after conversion: {snapshot.Path}");
            string expectedShader = snapshot.Skidmarks ? UnlitShaderName : LitShaderName;
            if (!string.Equals(material.shader.name, expectedShader, StringComparison.Ordinal))
                throw new InvalidOperationException($"Wrong shader on {snapshot.Path}: {material.shader.name}");

            bool texturePreserved = true;
            string expectedTexturePath = GuidToExistingAssetPath(snapshot.BaseTextureGuid);
            if (!string.IsNullOrEmpty(expectedTexturePath))
            {
                withTexture++;
                Texture actual = material.GetTexture("_BaseMap");
                string actualPath = actual != null ? AssetDatabase.GetAssetPath(actual) : string.Empty;
                texturePreserved = string.Equals(expectedTexturePath, actualPath, StringComparison.Ordinal);
                if (!texturePreserved)
                    throw new InvalidOperationException($"Base texture was not preserved: {snapshot.Path}");
                preservedTexture++;
            }

            if (snapshot.Transparent)
            {
                if (!material.HasProperty("_Surface")
                    || material.GetFloat("_Surface") < 0.5f
                    || material.renderQueue < (int)RenderQueue.Transparent)
                {
                    throw new InvalidOperationException($"Transparency setup failed: {snapshot.Path}");
                }
                if (snapshot.Skidmarks) skid++;
                else transparent++;
            }
            else opaque++;

            results.Add(new MaterialResult
            {
                path = snapshot.Path,
                name = material.name,
                shader = material.shader.name,
                transparent = snapshot.Transparent,
                baseTexturePreserved = texturePreserved,
                baseTexturePath = expectedTexturePath,
                baseColor = material.GetColor("_BaseColor"),
                metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f,
                smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0f
            });
        }

        int gameObjectAssets = 0;
        int rendererCount = 0;
        int slots = 0;
        int missingSlots = 0;
        int unsupportedSlots = 0;
        List<string> missingDetails = new List<string>();
        string[] objectPaths = AssetDatabase.FindAssets("t:GameObject", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        foreach (string path in objectPaths)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) continue;
            gameObjectAssets++;
            foreach (Renderer renderer in asset.GetComponentsInChildren<Renderer>(true))
            {
                rendererCount++;
                foreach (Material material in renderer.sharedMaterials)
                {
                    slots++;
                    if (material == null)
                    {
                        missingSlots++;
                        missingDetails.Add(path + " :: " + GetHierarchyPath(renderer.transform));
                        continue;
                    }
                    if (material.shader == null
                        || !material.shader.isSupported
                        || !material.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                    {
                        unsupportedSlots++;
                    }
                }
            }
        }
        if (missingSlots != 0 || unsupportedSlots != 0)
        {
            throw new InvalidOperationException(
                $"Renderer material validation failed: missing={missingSlots}, unsupported={unsupportedSlots}"
            );
        }

        return new Report
        {
            passed = true,
            unityVersion = Application.unityVersion,
            root = Root,
            materialCount = snapshots.Count,
            opaqueLitCount = opaque,
            transparentLitCount = transparent,
            transparentUnlitCount = skid,
            materialsWithBaseTexture = withTexture,
            preservedBaseTextures = preservedTexture,
            gameObjectAssetCount = gameObjectAssets,
            reimportedModelAssetCount = reimportedModelAssetCount,
            rendererCount = rendererCount,
            rendererMaterialSlots = slots,
            repairedLegacyPrefabMaterialSlots = repairedLegacyPrefabMaterialSlots,
            removedMissingScriptComponents = removedMissingScriptComponents,
            missingRendererMaterialSlots = missingSlots,
            unsupportedRendererMaterialSlots = unsupportedSlots,
            missingRendererMaterialDetails = missingDetails.ToArray(),
            materials = results.ToArray()
        };
    }

    private static string GetHierarchyPath(Transform transform)
    {
        List<string> names = new List<string>();
        for (Transform current = transform; current != null; current = current.parent)
            names.Add(current.name);
        names.Reverse();
        return string.Join("/", names);
    }

    private static Texture LoadTexture(string guid)
    {
        string path = GuidToExistingAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture>(path);
    }

    private static string GuidToExistingAssetPath(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return string.Empty;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? string.Empty : path;
    }

    private static void SetTextureIfPresent(Material material, string property, Texture texture)
    {
        if (material.HasProperty(property)) material.SetTexture(property, texture);
    }

    private static void SetColorIfPresent(Material material, string property, Color color)
    {
        if (material.HasProperty(property)) material.SetColor(property, color);
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static string ExtractTextureGuid(string yaml, params string[] properties)
    {
        foreach (string property in properties)
        {
            Match match = Regex.Match(
                yaml,
                @"-\s+" + Regex.Escape(property)
                    + @":\s*\r?\n\s*m_Texture:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*\d+\}",
                RegexOptions.CultureInvariant
            );
            if (match.Success) return match.Groups[1].Value;
        }
        return string.Empty;
    }

    private static Color ExtractColor(string yaml, string property, Color fallback)
    {
        const string number = @"[-+0-9.eE]+";
        Match match = Regex.Match(
            yaml,
            @"-\s+" + Regex.Escape(property)
                + @":\s*\{r:\s*(" + number + @"),\s*g:\s*(" + number
                + @"),\s*b:\s*(" + number + @"),\s*a:\s*(" + number + @")\}",
            RegexOptions.CultureInvariant
        );
        if (!match.Success) return fallback;
        return new Color(
            ParseFloat(match.Groups[1].Value, fallback.r),
            ParseFloat(match.Groups[2].Value, fallback.g),
            ParseFloat(match.Groups[3].Value, fallback.b),
            ParseFloat(match.Groups[4].Value, fallback.a)
        );
    }

    private static float ExtractFloat(string yaml, string property, float fallback)
    {
        Match match = Regex.Match(
            yaml,
            @"^\s*-\s+" + Regex.Escape(property) + @":\s*([-+0-9.eE]+)\s*$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant
        );
        return match.Success ? ParseFloat(match.Groups[1].Value, fallback) : fallback;
    }

    private static int ExtractInt(string yaml, string pattern, int fallback)
    {
        Match match = Regex.Match(yaml, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
    }

    private static string ExtractString(string yaml, string pattern, string fallback)
    {
        Match match = Regex.Match(yaml, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim() : fallback;
    }

    private static float ParseFloat(string value, float fallback)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : fallback;
    }

    private static void WriteReport(Report report)
    {
        string output = GetCommandLineArgument("-allStarReport");
        if (string.IsNullOrWhiteSpace(output))
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../AllStar_Material_Fix_QA.json"));
        string directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
    }

    private static string GetCommandLineArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
            if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                return arguments[index + 1];
        return null;
    }
}
