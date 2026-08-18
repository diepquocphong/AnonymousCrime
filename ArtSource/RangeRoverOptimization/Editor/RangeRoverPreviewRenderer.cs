using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders deterministic QA previews of the already-built optimized Range Rover prefab.
///
/// This script is intended to be copied into Assets/Editor in a disposable Unity staging
/// project. It does not save a scene or modify the source prefab. Invoke it with:
///
///   -executeMethod RangeRoverPreviewRenderer.Render
///   -rangeRoverPreviewDir /absolute/output/directory
/// </summary>
public static class RangeRoverPreviewRenderer
{
    private const string PrefabPath =
        "Assets/Ash Assets/VehicleCollection/Range_Rover_Material_Fixed/Range_Rover_Centered_Optimized.prefab";
    private const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";
    private const int PreviewWidth = 1600;
    private const int PreviewHeight = 900;

    [Serializable]
    private sealed class CaptureReport
    {
        public string label;
        public string path;
        public int width;
        public int height;
        public long byteCount;
        public float meanLuminance;
        public float luminanceStandardDeviation;
        public float nearBlackPixelRatio;
        public float likelyErrorMagentaPixelRatio;
    }

    [Serializable]
    private sealed class PreviewReport
    {
        public bool passed;
        public string unityVersion;
        public string prefabPath;
        public string renderPipeline;
        public int rendererCount;
        public int uniqueMaterialCount;
        public int missingMaterialCount;
        public int missingShaderCount;
        public Bounds groundedBounds;
        public MaterialReport[] materials;
        public CaptureReport[] captures;
    }

    [Serializable]
    private sealed class MaterialReport
    {
        public string name;
        public string shader;
        public int renderQueue;
        public bool transparent;
        public bool emissionEnabled;
    }

    public static void Render()
    {
        string outputDirectory = GetCommandLineArgument("-rangeRoverPreviewDir");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../RangeRoverPreview")
            );
        }
        Directory.CreateDirectory(outputDirectory);

        RenderPipelineAsset pipeline =
            AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
            throw new InvalidOperationException($"URP asset is missing: {PipelinePath}");

#pragma warning disable 0618
        GraphicsSettings.defaultRenderPipeline = pipeline;
#pragma warning restore 0618
        QualitySettings.renderPipeline = pipeline;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.shadowDistance = 40f;
        QualitySettings.antiAliasing = 4;
        QualitySettings.vSyncCount = 0;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Optimized prefab is missing: {PrefabPath}");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        car.name = "Range_Rover_Preview_Instance";

        Renderer[] carRenderers = car.GetComponentsInChildren<Renderer>(true);
        if (carRenderers.Length == 0)
            throw new InvalidOperationException("Optimized prefab has no renderers");

        Bounds initialBounds = CalculateBounds(carRenderers);
        car.transform.position += Vector3.up * -initialBounds.min.y;
        Bounds groundedBounds = CalculateBounds(carRenderers);

        int missingMaterials = 0;
        int missingShaders = 0;
        HashSet<Material> uniqueMaterials = new HashSet<Material>();
        foreach (Renderer renderer in carRenderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    missingMaterials++;
                    continue;
                }
                uniqueMaterials.Add(material);
                if (material.shader == null || !material.shader.isSupported) missingShaders++;
            }
        }
        if (missingMaterials != 0 || missingShaders != 0)
        {
            throw new InvalidOperationException(
                $"Material QA failed: missingMaterials={missingMaterials}, "
                    + $"missingOrUnsupportedShaders={missingShaders}"
            );
        }

        Light keyLight = ConfigureEnvironment();
        CreateGround();
        Camera camera = CreateCamera();

        Vector3 target = groundedBounds.center
            + Vector3.up * (groundedBounds.size.y * 0.06f);
        Vector3 heroPosition = target
            + new Vector3(
                groundedBounds.size.x * 2.2f,
                groundedBounds.size.y * 0.9f,
                groundedBounds.size.z * 1.3f
            );
        Vector3 frontPosition = target
            + new Vector3(
                0f,
                groundedBounds.size.y * 0.72f,
                groundedBounds.size.z * 1.35f
            );
        Vector3 rearPosition = target
            + new Vector3(
                -groundedBounds.size.x * 2.2f,
                groundedBounds.size.y * 0.9f,
                -groundedBounds.size.z * 1.3f
            );

        RenderSettings.sun = keyLight;
        DynamicGI.UpdateEnvironment();

        CaptureReport hero = Capture(
            camera,
            "hero-three-quarter",
            heroPosition,
            target,
            Path.Combine(outputDirectory, "Range_Rover_Optimized_Preview_Hero.png")
        );
        CaptureReport front = Capture(
            camera,
            "front",
            frontPosition,
            target,
            Path.Combine(outputDirectory, "Range_Rover_Optimized_Preview_Front.png")
        );
        CaptureReport rear = Capture(
            camera,
            "rear-three-quarter",
            rearPosition,
            target,
            Path.Combine(outputDirectory, "Range_Rover_Optimized_Preview_Rear.png")
        );

        MaterialReport[] materialReports = uniqueMaterials
            .OrderBy(material => material.name, StringComparer.Ordinal)
            .Select(material => new MaterialReport
            {
                name = material.name,
                shader = material.shader != null ? material.shader.name : "<missing>",
                renderQueue = material.renderQueue,
                transparent = material.renderQueue >= (int)RenderQueue.Transparent
                    || (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f),
                emissionEnabled = material.IsKeywordEnabled("_EMISSION")
            })
            .ToArray();

        PreviewReport report = new PreviewReport
        {
            passed = true,
            unityVersion = Application.unityVersion,
            prefabPath = PrefabPath,
            renderPipeline = pipeline.name,
            rendererCount = carRenderers.Length,
            uniqueMaterialCount = uniqueMaterials.Count,
            missingMaterialCount = missingMaterials,
            missingShaderCount = missingShaders,
            groundedBounds = groundedBounds,
            materials = materialReports,
            captures = new[] { hero, front, rear }
        };
        string reportPath = Path.Combine(outputDirectory, "Range_Rover_Preview_QA.json");
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));

        Debug.Log(
            $"RANGE_ROVER_PREVIEW_SUCCESS hero={hero.path} front={front.path} rear={rear.path} "
                + $"renderers={report.rendererCount} materials={report.uniqueMaterialCount} "
                + $"meanLuma={hero.meanLuminance:F4} magenta={hero.likelyErrorMagentaPixelRatio:F6}"
        );
    }

    private static Light ConfigureEnvironment()
    {
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.56f, 0.63f, 0.74f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.25f, 0.29f, 0.36f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        RenderSettings.ambientIntensity = 1.05f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.reflectionBounces = 1;

        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader == null)
            throw new InvalidOperationException("Skybox/Procedural shader is unavailable");
        Material skybox = new Material(skyShader) { name = "Range Rover Preview Sky" };
        skybox.SetColor("_SkyTint", new Color(0.50f, 0.57f, 0.68f, 1f));
        skybox.SetColor("_GroundColor", new Color(0.09f, 0.10f, 0.13f, 1f));
        skybox.SetFloat("_AtmosphereThickness", 0.72f);
        skybox.SetFloat("_Exposure", 0.82f);
        skybox.SetFloat("_SunSize", 0.035f);
        skybox.SetFloat("_SunSizeConvergence", 7f);
        RenderSettings.skybox = skybox;

        Light key = CreateDirectionalLight(
            "Preview Key",
            new Color(1f, 0.91f, 0.82f, 1f),
            1.55f,
            new Vector3(46f, -38f, 0f),
            true
        );
        CreateDirectionalLight(
            "Preview Fill",
            new Color(0.58f, 0.72f, 1f, 1f),
            0.62f,
            new Vector3(28f, 142f, 0f),
            false
        );
        CreateDirectionalLight(
            "Preview Rim",
            new Color(0.78f, 0.88f, 1f, 1f),
            0.36f,
            new Vector3(18f, 218f, 0f),
            false
        );
        return key;
    }

    private static Light CreateDirectionalLight(
        string name,
        Color color,
        float intensity,
        Vector3 rotation,
        bool shadows
    )
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.rotation = Quaternion.Euler(rotation);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = 0.74f;
        light.shadowBias = 0.04f;
        light.shadowNormalBias = 0.35f;
        return light;
    }

    private static void CreateGround()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("Universal Render Pipeline/Lit shader is unavailable");

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Preview Ground";
        ground.transform.position = new Vector3(0f, -0.04f, 0f);
        ground.transform.localScale = new Vector3(13f, 0.08f, 13f);
        Material material = new Material(shader) { name = "Preview Ground Material" };
        Color groundColor = new Color(0.105f, 0.115f, 0.135f, 1f);
        material.SetColor("_BaseColor", groundColor);
        material.SetColor("_Color", groundColor);
        material.SetFloat("_Metallic", 0.03f);
        material.SetFloat("_Smoothness", 0.24f);
        ground.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("Range Rover Preview Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 31f;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 80f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.aspect = (float)PreviewWidth / PreviewHeight;
        return camera;
    }

    private static CaptureReport Capture(
        Camera camera,
        string label,
        Vector3 position,
        Vector3 target,
        string outputPath
    )
    {
        camera.transform.position = position;
        camera.transform.LookAt(target, Vector3.up);

        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
            PreviewWidth,
            PreviewHeight,
            RenderTextureFormat.ARGB32,
            24
        )
        {
            msaaSamples = 4,
            sRGB = true,
            useMipMap = false,
            autoGenerateMips = false
        };
        RenderTexture targetTexture = new RenderTexture(descriptor)
        {
            name = $"Range Rover Preview {label}"
        };
        targetTexture.Create();

        RenderTexture previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            camera.targetTexture = targetTexture;
            camera.Render();
            camera.Render();

            RenderTexture.active = targetTexture;
            image = new Texture2D(
                PreviewWidth,
                PreviewHeight,
                TextureFormat.RGB24,
                false
            );
            image.ReadPixels(new Rect(0f, 0f, PreviewWidth, PreviewHeight), 0, 0, false);
            image.Apply(false, false);

            Color32[] pixels = image.GetPixels32();
            double luminanceSum = 0d;
            double luminanceSquaredSum = 0d;
            int nearBlackPixels = 0;
            int likelyErrorMagentaPixels = 0;
            foreach (Color32 pixel in pixels)
            {
                float luminance =
                    (0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b) / 255f;
                luminanceSum += luminance;
                luminanceSquaredSum += luminance * luminance;
                if (pixel.r < 8 && pixel.g < 8 && pixel.b < 8) nearBlackPixels++;
                if (pixel.r > 210 && pixel.b > 210 && pixel.g < 90)
                    likelyErrorMagentaPixels++;
            }

            byte[] png = image.EncodeToPNG();
            if (png == null || png.Length < 32768)
                throw new InvalidOperationException(
                    $"Preview {label} appears invalid; encoded PNG is only {png?.Length ?? 0} bytes"
                );
            File.WriteAllBytes(outputPath, png);

            double count = pixels.Length;
            double mean = luminanceSum / count;
            double variance = Math.Max(0d, luminanceSquaredSum / count - mean * mean);
            return new CaptureReport
            {
                label = label,
                path = Path.GetFullPath(outputPath),
                width = PreviewWidth,
                height = PreviewHeight,
                byteCount = png.LongLength,
                meanLuminance = (float)mean,
                luminanceStandardDeviation = (float)Math.Sqrt(variance),
                nearBlackPixelRatio = nearBlackPixels / (float)pixels.Length,
                likelyErrorMagentaPixelRatio = likelyErrorMagentaPixels / (float)pixels.Length
            };
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            targetTexture.Release();
            UnityEngine.Object.DestroyImmediate(targetTexture);
        }
    }

    private static Bounds CalculateBounds(IEnumerable<Renderer> renderers)
    {
        Renderer[] array = renderers.Where(renderer => renderer != null).ToArray();
        if (array.Length == 0) throw new InvalidOperationException("No renderer bounds found");
        Bounds bounds = array[0].bounds;
        for (int index = 1; index < array.Length; index++) bounds.Encapsulate(array[index].bounds);
        return bounds;
    }

    private static string GetCommandLineArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                return arguments[index + 1];
        }
        return null;
    }
}
