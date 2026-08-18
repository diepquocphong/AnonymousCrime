using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class VillaColliderAudit
{
    private const string PrefabPath =
        "Assets/Model/franklin-villa-estate-ultra-optimized/franklin-villa-estate-ultra-optimized.prefab";

    public static void Run()
    {
        string output = GetArgument("-villaColliderAudit");
        if (string.IsNullOrWhiteSpace(output))
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../VillaColliderAudit.json"));
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new FileNotFoundException("Ultra villa prefab missing", PrefabPath);

        Report report = new Report();
        report.prefab = PrefabPath;
        foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
        {
            Transform transform = collider.transform;
            Entry entry = new Entry
            {
                path = RelativePath(transform, prefab.transform),
                name = transform.name,
                type = collider.GetType().Name,
                active = collider.gameObject.activeInHierarchy && collider.enabled,
                isTrigger = collider.isTrigger,
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale,
                worldCenter = collider.bounds.center,
                worldSize = collider.bounds.size,
                movingAnchor = FindMovingAnchor(transform)
            };
            if (collider is BoxCollider box)
            {
                entry.boxCenter = box.center;
                entry.boxSize = box.size;
                report.boxCount++;
            }
            else if (collider is MeshCollider mesh)
            {
                entry.meshVertices = mesh.sharedMesh == null ? 0 : mesh.sharedMesh.vertexCount;
                entry.meshTriangles = mesh.sharedMesh == null ? 0 : TriangleCount(mesh.sharedMesh);
                entry.convex = mesh.convex;
                report.meshCount++;
            }
            report.entries.Add(entry);
        }
        report.total = report.entries.Count;
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Debug.Log($"VILLA_COLLIDER_AUDIT_SUCCESS total={report.total} box={report.boxCount} mesh={report.meshCount} output={output}");
    }

    private static string FindMovingAnchor(Transform transform)
    {
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
        {
            string name = cursor.name;
            if (name == "FranklinVilla_Interactive_GateLeaf_Left_001" ||
                name == "FranklinVilla_Interactive_GateLeaf_Right_001" ||
                name.StartsWith("FranklinVilla_Interactive_GaragePanel_", StringComparison.Ordinal) ||
                name == "FranklinVilla_Interactive_cua_chinh_Pivot_001" ||
                name == "FranklinVilla_Interactive_cua_ban_cong_Pivot_001" ||
                name == "FranklinVilla_Hierarchy_Group_003" ||
                name == "FranklinVilla_Hierarchy_Group_005")
                return name;
        }
        return "";
    }

    private static int TriangleCount(Mesh mesh)
    {
        int result = 0;
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            result += checked((int)(mesh.GetIndexCount(submesh) / 3));
        return result;
    }

    private static string RelativePath(Transform transform, Transform root)
    {
        if (transform == root) return "";
        Stack<string> parts = new Stack<string>();
        for (Transform cursor = transform; cursor != null && cursor != root; cursor = cursor.parent)
            parts.Push(cursor.name);
        return string.Join("/", parts);
    }

    private static string GetArgument(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
            if (args[index] == key) return args[index + 1];
        return null;
    }

    [Serializable]
    private sealed class Report
    {
        public string prefab;
        public int total;
        public int boxCount;
        public int meshCount;
        public List<Entry> entries = new List<Entry>();
    }

    [Serializable]
    private sealed class Entry
    {
        public string path;
        public string name;
        public string type;
        public bool active;
        public bool isTrigger;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Vector3 boxCenter;
        public Vector3 boxSize;
        public Vector3 worldCenter;
        public Vector3 worldSize;
        public int meshVertices;
        public int meshTriangles;
        public bool convex;
        public string movingAnchor;
    }
}
