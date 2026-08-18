using System;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VillaMeshDebug
{
    public static void ReportAsphalt()
    {
        Report("SOURCE", "Assets/Model/franklin-villa-estate-unity/franklin-villa-estate-unity.prefab");
        Report("OPTIMIZED", "Assets/Model/franklin-villa-estate-mobile-optimized/franklin-villa-estate-mobile-optimized.prefab");
    }

    public static void ReportAsphaltScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject source = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/franklin-villa-estate-unity/franklin-villa-estate-unity.prefab"));
        ReportObject("SOURCE_SCENE", source);
        UnityEngine.Object.DestroyImmediate(source);
        GameObject optimized = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/franklin-villa-estate-mobile-optimized/franklin-villa-estate-mobile-optimized.prefab"));
        ReportObject("OPTIMIZED_SCENE", optimized);
    }

    public static void DumpTextures()
    {
        Dump("SOURCE", "Assets/Model/franklin-villa-estate-unity/Textures/texture_1.png");
        Dump("OPTIMIZED", "Assets/Model/franklin-villa-estate-mobile-optimized/Textures/texture_1.png");
    }

    private static void Dump(string label, string path)
    {
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Debug.Log($"VILLA_TEXTURE_DEBUG {label} format={source.format} graphics={source.graphicsFormat} size={source.width}x{source.height}");
        RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, target);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();
        File.WriteAllBytes("/tmp/villa-texture-" + label.ToLowerInvariant() + ".png", copy.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(copy);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
    }

    private static void Report(string label, string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        ReportObject(label, prefab);
    }

    private static void ReportObject(string label, GameObject prefab)
    {
        foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true)
                     .Where(value => value.sharedMaterials.Any(material => material != null && material.name == "asphalt")))
        {
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            using Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData data = dataArray[0];
            NativeArray<Vector3> normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            NativeArray<Vector3> positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            NativeArray<Vector2> uv = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp);
            data.GetNormals(normals);
            data.GetVertices(positions);
            if (data.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0)) data.GetUVs(0, uv);
            Vector3 sum = Vector3.zero;
            Vector2 uvMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 uvMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            Matrix4x4 normalMatrix = renderer.localToWorldMatrix.inverse.transpose;
            string[] geometryValues = GeometryValues(data, renderer.localToWorldMatrix, normalMatrix, positions, normals, uv);
            string geometryHash = GeometryHash(geometryValues);
            File.WriteAllLines("/tmp/villa-geometry-" + label.ToLowerInvariant() + "-" + renderer.name + ".txt", geometryValues);
            for (int index = 0; index < data.vertexCount; index++)
            {
                sum += normalMatrix.MultiplyVector(normals[index]).normalized;
                uvMin = Vector2.Min(uvMin, uv[index]);
                uvMax = Vector2.Max(uvMax, uv[index]);
            }
            Debug.Log($"VILLA_MESH_DEBUG {label} renderer={renderer.name} mesh={mesh.name} vertices={mesh.vertexCount} " +
                      $"bounds={renderer.bounds.center}/{renderer.bounds.size} avgWorldNormal={(sum / data.vertexCount)} uv={uvMin}..{uvMax} hash={geometryHash}");
            normals.Dispose();
            positions.Dispose();
            uv.Dispose();
        }
    }

    private static string[] GeometryValues(
        Mesh.MeshData data,
        Matrix4x4 objectToWorld,
        Matrix4x4 normalMatrix,
        NativeArray<Vector3> positions,
        NativeArray<Vector3> normals,
        NativeArray<Vector2> uv)
    {
        string[] values = new string[data.vertexCount];
        for (int index = 0; index < data.vertexCount; index++)
        {
            Vector3 p = objectToWorld.MultiplyPoint3x4(positions[index]);
            Vector3 n = normalMatrix.MultiplyVector(normals[index]).normalized;
            values[index] = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:F6},{1:F6},{2:F6}|{3:F6},{4:F6},{5:F6}|{6:F6},{7:F6}",
                p.x, p.y, p.z, n.x, n.y, n.z, uv[index].x, uv[index].y);
        }
        Array.Sort(values, StringComparer.Ordinal);
        return values;
    }

    private static string GeometryHash(string[] values)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", values)));
        return BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
    }
}
