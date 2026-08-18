using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class AllStarMaterialPreviewRenderer
{
    private const int Width = 1200;
    private const int Height = 1200;

    public static void Render()
    {
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
            "Assets/Settings/PC_RPAsset.asset"
        );
        if (pipeline == null) throw new InvalidOperationException("Preview URP asset is missing");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;

        string output = GetArgument("-allStarPreviewOutput");
        if (string.IsNullOrWhiteSpace(output)) output = Path.Combine(Application.dataPath, "../Preview");
        Directory.CreateDirectory(output);

        RenderAsset(
            "Assets/AllStarCharacterLibrary/Characters/Tommy/Tommy.FBX",
            Path.Combine(output, "AllStar_Tommy_URP.png"),
            new Vector3(0.9f, 0.45f, 1.15f)
        );
        RenderAsset(
            "Assets/AllStarCharacterLibrary/Prefabs/PoliceCar.prefab",
            Path.Combine(output, "AllStar_PoliceCar_URP.png"),
            new Vector3(1.25f, 0.65f, 1.35f)
        );
        Debug.Log("ALLSTAR_MATERIAL_PREVIEW_SUCCESS " + output);
    }

    private static void RenderAsset(string assetPath, string outputPath, Vector3 viewDirection)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null) throw new InvalidOperationException("Preview asset missing: " + assetPath);
        GameObject root = UnityEngine.Object.Instantiate(source);
        root.name = source.name;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("No renderers: " + assetPath);
        if (renderers.SelectMany(renderer => renderer.sharedMaterials).Any(material => material == null))
            throw new InvalidOperationException("Null material in preview asset: " + assetPath);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        root.transform.position -= bounds.center;
        float floorY = -bounds.extents.y;

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "PreviewGround";
        float span = Mathf.Max(bounds.size.x, bounds.size.z) * 3.25f;
        ground.transform.position = new Vector3(0f, floorY - 0.04f, 0f);
        ground.transform.localScale = new Vector3(span, 0.08f, span);
        Material groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMaterial.SetColor("_BaseColor", new Color(0.11f, 0.13f, 0.16f, 1f));
        groundMaterial.SetFloat("_Smoothness", 0.18f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        GameObject keyObject = new GameObject("KeyLight");
        Light key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.8f;
        key.color = new Color(1f, 0.93f, 0.84f);
        key.transform.rotation = Quaternion.LookRotation(-viewDirection.normalized, Vector3.up);

        GameObject fillObject = new GameObject("FillLight");
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.9f;
        fill.color = new Color(0.5f, 0.7f, 1f);
        fill.transform.rotation = Quaternion.Euler(25f, 145f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.26f, 0.29f, 0.35f);

        GameObject cameraObject = new GameObject("PreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
        camera.fieldOfView = 31f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 500f;
        camera.allowHDR = true;
        camera.allowMSAA = true;

        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        Vector3 direction = viewDirection.normalized;
        camera.transform.position = direction * (radius / Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 1.2f);
        camera.transform.LookAt(Vector3.zero + Vector3.up * bounds.extents.y * 0.06f);

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4
        };
        camera.targetTexture = target;
        camera.Render();
        camera.Render();
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        image.Apply();
        File.WriteAllBytes(outputPath, image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;

        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(fillObject);
        UnityEngine.Object.DestroyImmediate(keyObject);
        UnityEngine.Object.DestroyImmediate(groundMaterial);
        UnityEngine.Object.DestroyImmediate(ground);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static string GetArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
            if (string.Equals(arguments[index], name, StringComparison.Ordinal)) return arguments[index + 1];
        return null;
    }
}
