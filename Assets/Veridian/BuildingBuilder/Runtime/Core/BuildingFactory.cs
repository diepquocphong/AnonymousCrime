using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace Veridian.BuildingSystem.Runtime
{
    public static class BuildingFactory
    {
        /// <summary>
        /// Explicit Runtime Generation API. Use this within standalone games to securely generate a 
        /// fully encapsulated building memory structure natively plugged against GC memory leaks.
        /// </summary>
        public static GameObject GenerateAtRuntime(BuildingProfile profile, List<Mesh> outMeshes = null, List<Material> outMaterials = null)
        {
            BuildingBuilder builder = new BuildingBuilder();
            var meshDataArray = builder.Generate(profile);

            GameObject root = new GameObject(string.IsNullOrEmpty(profile.name) ? "Procedural Building" : profile.name);

            // Notice we no longer add the tracker here. We just pass the lists down.
            PopulateBuilding(root, profile, meshDataArray, outMeshes, outMaterials);

            return root;
        }

        /// <summary>
        /// Native Editor Baker integration endpoint. Does not trigger the Runtime Tracker dependencies. 
        /// </summary>
        public static GameObject CreateBuilding(BuildingProfile profile, BuildingBuilder.MeshData[] meshDataArray, string name)
        {
            GameObject root = new GameObject(name);
            PopulateBuilding(root, profile, meshDataArray, null, null);
            return root;
        }

        private static void PopulateBuilding(GameObject root, BuildingProfile profile, BuildingBuilder.MeshData[] meshDataArray, List<Mesh> outMeshes, List<Material> outMaterials)
        {
            Material[] materials = new Material[]
            {
                GetMaterial(profile.wallMaterial, profile.wallFallbackColor, outMaterials),
                GetMaterial(profile.roofMaterial, profile.roofFallbackColor, outMaterials),
                GetMaterial(profile.glassMaterial, profile.glassFallbackColor, outMaterials),
                GetMaterial(profile.masonryMaterial, profile.masonryFallbackColor, outMaterials),
                GetMaterial(profile.accentMaterial, profile.accentFallbackColor, outMaterials),
                GetMaterial(profile.frameMaterial, profile.frameFallbackColor, outMaterials),
                GetMaterial(profile.doorMaterial, profile.doorFallbackColor, outMaterials)
            };

            if (profile.generateLODGroup)
            {
                SetupLODGroup(root, meshDataArray, materials, profile, outMeshes);
            }
            else
            {
                MeshFilter mf = root.AddComponent<MeshFilter>();
                MeshRenderer mr = root.AddComponent<MeshRenderer>();
                Mesh mesh = CreateUnityMesh(meshDataArray[0], root.name + "_Mesh", materials, out Material[] usedMats, outMeshes);
                if (mesh != null)
                {
                    mf.sharedMesh = mesh;
                    mr.sharedMaterials = usedMats;
                }
            }

            if (profile.generateColliders) GenerateColliders(root, profile);
        }

        private static void SetupLODGroup(GameObject root, BuildingBuilder.MeshData[] lodMeshData, Material[] globalMats, BuildingProfile profile, List<Mesh> outMeshes)
        {
            LODGroup lodGroup = root.AddComponent<LODGroup>();
            List<LOD> lods = new List<LOD>();

            for (int i = 0; i < 3; i++)
            {
                if (lodMeshData[i].vertices.Count == 0) continue;

                GameObject lodGO = new GameObject($"{root.name}_LOD{i}");
                lodGO.transform.SetParent(root.transform, false);

                MeshFilter lMf = lodGO.AddComponent<MeshFilter>();
                MeshRenderer lMr = lodGO.AddComponent<MeshRenderer>();

                Mesh mesh = CreateUnityMesh(lodMeshData[i], $"{root.name}_LOD{i}_Mesh", globalMats, out Material[] usedMats, outMeshes);
                if (mesh != null)
                {
                    lMf.sharedMesh = mesh;
                    lMr.sharedMaterials = usedMats;
                    float transition = i == 0 ? profile.lod1ScreenSize : (i == 1 ? profile.lod2ScreenSize : 0.01f);
                    lods.Add(new LOD(transition, new Renderer[] { lMr }));
                }
            }

            if (lods.Count > 0)
            {
                lodGroup.SetLODs(lods.ToArray());
                lodGroup.RecalculateBounds();
            }
            else
            {
                if (Application.isPlaying) Object.Destroy(lodGroup);
                else Object.DestroyImmediate(lodGroup);
            }
        }

        private static Mesh CreateUnityMesh(BuildingBuilder.MeshData data, string meshName, Material[] allMats, out Material[] usedMats, List<Mesh> outMeshes)
        {
            List<Material> activeMats = new List<Material>();
            List<List<int>> activeTriangles = new List<List<int>>();

            for (int i = 0; i < 7; i++)
            {
                if (data.triangles[i].Count > 0)
                {
                    activeMats.Add(allMats[i]);
                    activeTriangles.Add(data.triangles[i]);
                }
            }

            usedMats = activeMats.ToArray();
            if (data.vertices.Count == 0) return null;

            Mesh mesh = new Mesh { name = meshName };
            if (data.vertices.Count > 65534) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(data.vertices);
            mesh.SetUVs(0, data.uvs);
            mesh.SetNormals(data.normals);

            mesh.subMeshCount = activeTriangles.Count;
            for (int i = 0; i < activeTriangles.Count; i++) mesh.SetTriangles(activeTriangles[i], i);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            // Intercept mesh and push to the list instead of the tracker
            if (outMeshes != null && !outMeshes.Contains(mesh)) outMeshes.Add(mesh);

            return mesh;
        }

        private static Material GetMaterial(Material sourceMat, Color fallbackColor, List<Material> outMaterials)
        {
            if (sourceMat != null) return sourceMat;

            Shader shader = Shader.Find("Standard");

            if (GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultMaterial != null)
                shader = GraphicsSettings.currentRenderPipeline.defaultMaterial.shader;

            Material fallbackMat = new Material(shader) { name = "ProceduralFallback" };

            if (fallbackMat.HasProperty("_BaseColor")) fallbackMat.SetColor("_BaseColor", fallbackColor);
            else if (fallbackMat.HasProperty("_Color")) fallbackMat.color = fallbackColor;

            // Intercept procedurally spawned material and push to the list
            if (outMaterials != null && !outMaterials.Contains(fallbackMat)) outMaterials.Add(fallbackMat);

            return fallbackMat;
        }

        private static void GenerateColliders(GameObject root, BuildingProfile profile)
        {
            float baseY = profile.BuildingBaseY;
            BoxCollider mainCol = root.AddComponent<BoxCollider>();
            mainCol.center = new Vector3(0, baseY + profile.TotalWallHeight / 2, 0);
            mainCol.size = new Vector3(profile.width, profile.TotalWallHeight, profile.length);

            if (profile.addFoundation)
            {
                BoxCollider fCol = root.AddComponent<BoxCollider>();
                fCol.center = new Vector3(0, profile.foundationHeight / 2, 0);
                fCol.size = new Vector3(profile.width, profile.foundationHeight, profile.length);
            }

            if (profile.roofType != BuildingProfile.RoofType.Flat && profile.roofHeight > 0)
            {
                BoxCollider rCol = root.AddComponent<BoxCollider>();
                rCol.center = new Vector3(0, baseY + profile.TotalWallHeight + profile.roofHeight / 2, 0);
                rCol.size = new Vector3(profile.width, profile.roofHeight, profile.length);
            }
        }
    }
}