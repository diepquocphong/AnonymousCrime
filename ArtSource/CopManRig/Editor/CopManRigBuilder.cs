using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CopManRigBuilder
{
    private const string DonorFbxPath = "Assets/Donor/franklin_1_fixed.fbx";
    private const string RiggedFbxPath = "Assets/Character/CopMan/Rigged/CopMan_Rigged.fbx";
    private const string MaterialPath =
        "Assets/Character/CopMan/Materials/tripo_mat_a12a1546-291e-4a69-9c07-4a8453a56544.mat";
    private const string PrefabPath = "Assets/Character/CopMan/CopMan.prefab";
    private const string StaticPrefabPath = "Assets/Character/CopMan/CopMan_Static.prefab";

    [Serializable]
    private sealed class RigReport
    {
        public bool passed;
        public string unityVersion;
        public string prefabPath;
        public string riggedFbxPath;
        public string avatarName;
        public bool avatarIsValid;
        public bool avatarIsHuman;
        public float rootRotationY;
        public int skinnedRendererCount;
        public int boneCount;
        public int bindPoseCount;
        public int vertexCount;
        public int triangleCount;
        public int unweightedVertexCount;
        public int maxInfluencesPerVertex;
        public float minimumWeightSum;
        public float maximumWeightSum;
        public float poseTestMaxVertexDelta;
        public string materialPath;
    }

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
        RequireAsset(DonorFbxPath);
        RequireAsset(RiggedFbxPath);
        RequireAsset(MaterialPath);
        RequireAsset(PrefabPath);

        AssetDatabase.ImportAsset(
            DonorFbxPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate
        );

        ConfigureRiggedModelImporter();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(StaticPrefabPath) == null)
        {
            if (!AssetDatabase.CopyAsset(PrefabPath, StaticPrefabPath))
            {
                throw new InvalidOperationException($"Could not create static backup at {StaticPrefabPath}");
            }
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedFbxPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (modelAsset == null) throw new InvalidOperationException("Rigged FBX has no GameObject asset");
        if (material == null) throw new InvalidOperationException("CopMan material could not be loaded");

        GameObject wrapper = new GameObject("CopMan");
        wrapper.transform.localPosition = Vector3.zero;
        wrapper.transform.localRotation = new Quaternion(0f, 1f, 0f, 0f);
        wrapper.transform.localScale = Vector3.one;

        GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        modelInstance.name = "CopMan_Rigged";
        modelInstance.transform.SetParent(wrapper.transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        SkinnedMeshRenderer[] renderers = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one SkinnedMeshRenderer in rigged FBX, found {renderers.Length}"
            );
        }

        renderers[0].sharedMaterial = material;
        renderers[0].updateWhenOffscreen = false;
        renderers[0].skinnedMotionVectors = true;

        Animator animator = modelInstance.GetComponent<Animator>();
        if (animator == null) animator = modelInstance.AddComponent<Animator>();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(RiggedFbxPath)
            .OfType<Avatar>()
            .FirstOrDefault(candidate => candidate != null && candidate.isValid && candidate.isHuman);
        if (avatar == null)
        {
            throw new InvalidOperationException("Unity did not create a valid Humanoid Avatar for CopMan");
        }

        animator.avatar = avatar;
        animator.applyRootMotion = true;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath, out bool saved);
        UnityEngine.Object.DestroyImmediate(wrapper);
        if (!saved || savedPrefab == null)
        {
            throw new InvalidOperationException($"Could not save rigged prefab at {PrefabPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        RigReport report = ValidatePrefab();
        WriteReport(report);
        Debug.Log(
            $"COPMAN_RIG_QA_SUCCESS avatar={report.avatarName} bones={report.boneCount} "
                + $"vertices={report.vertexCount} tris={report.triangleCount} "
                + $"poseDelta={report.poseTestMaxVertexDelta:F6} y={report.rootRotationY:F1}"
        );
    }

    private static void ConfigureRiggedModelImporter()
    {
        ModelImporter donorImporter = AssetImporter.GetAtPath(DonorFbxPath) as ModelImporter;
        ModelImporter rigImporter = AssetImporter.GetAtPath(RiggedFbxPath) as ModelImporter;
        if (donorImporter == null) throw new InvalidOperationException("Donor ModelImporter is missing");
        if (rigImporter == null) throw new InvalidOperationException("Rigged ModelImporter is missing");

        rigImporter.importAnimation = false;
        rigImporter.animationType = ModelImporterAnimationType.Human;
        rigImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        rigImporter.humanDescription = donorImporter.humanDescription;
        rigImporter.materialImportMode = ModelImporterMaterialImportMode.None;
        rigImporter.importCameras = false;
        rigImporter.importLights = false;
        rigImporter.importBlendShapes = false;
        rigImporter.isReadable = true;
        rigImporter.optimizeGameObjects = false;
        rigImporter.globalScale = 1.85f;
        rigImporter.maxBonesPerVertex = 4;
        rigImporter.minBoneWeight = 0.001f;
        rigImporter.SaveAndReimport();
    }

    private static RigReport ValidatePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Rigged CopMan prefab could not be loaded");

        float rootY = NormalizeAngle(prefab.transform.localEulerAngles.y);
        if (Mathf.Abs(Mathf.DeltaAngle(rootY, 180f)) > 0.01f)
        {
            throw new InvalidOperationException($"CopMan root Y rotation is {rootY}, expected 180");
        }

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null) throw new InvalidOperationException("Rigged CopMan prefab has no Animator");
        if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            throw new InvalidOperationException("Rigged CopMan prefab Avatar is not a valid Humanoid");
        }

        SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length != 1)
        {
            throw new InvalidOperationException($"Expected one SkinnedMeshRenderer, found {renderers.Length}");
        }

        SkinnedMeshRenderer renderer = renderers[0];
        Mesh mesh = renderer.sharedMesh;
        if (mesh == null) throw new InvalidOperationException("SkinnedMeshRenderer has no mesh");
        if (renderer.rootBone == null) throw new InvalidOperationException("SkinnedMeshRenderer has no root bone");
        if (renderer.bones == null || renderer.bones.Length < 20)
        {
            throw new InvalidOperationException("SkinnedMeshRenderer bone array is incomplete");
        }
        if (mesh.bindposes == null || mesh.bindposes.Length == 0)
        {
            throw new InvalidOperationException("Rigged mesh has no bind poses");
        }
        if (renderer.sharedMaterial == null || AssetDatabase.GetAssetPath(renderer.sharedMaterial) != MaterialPath)
        {
            throw new InvalidOperationException("CopMan material was not preserved on the rigged prefab");
        }

        BoneWeight[] weights = mesh.boneWeights;
        if (weights.Length != mesh.vertexCount)
        {
            throw new InvalidOperationException(
                $"Bone weight count {weights.Length} does not match vertex count {mesh.vertexCount}"
            );
        }

        int unweighted = 0;
        int maxInfluences = 0;
        float minimumWeightSum = float.PositiveInfinity;
        float maximumWeightSum = float.NegativeInfinity;
        foreach (BoneWeight weight in weights)
        {
            float[] values = { weight.weight0, weight.weight1, weight.weight2, weight.weight3 };
            float sum = values.Sum();
            int influences = values.Count(value => value > 0.00001f);
            if (sum < 0.999f) unweighted++;
            maxInfluences = Math.Max(maxInfluences, influences);
            minimumWeightSum = Mathf.Min(minimumWeightSum, sum);
            maximumWeightSum = Mathf.Max(maximumWeightSum, sum);
        }
        if (unweighted != 0)
        {
            throw new InvalidOperationException($"Rigged mesh contains {unweighted} unweighted vertices");
        }
        if (maxInfluences > 4)
        {
            throw new InvalidOperationException($"Rigged mesh uses {maxInfluences} influences per vertex");
        }

        float poseDelta = RunPoseDeformationTest(prefab);
        if (poseDelta < 0.001f)
        {
            throw new InvalidOperationException(
                $"Pose deformation test produced too little movement: {poseDelta:F6}"
            );
        }

        return new RigReport
        {
            passed = true,
            unityVersion = Application.unityVersion,
            prefabPath = PrefabPath,
            riggedFbxPath = RiggedFbxPath,
            avatarName = animator.avatar.name,
            avatarIsValid = animator.avatar.isValid,
            avatarIsHuman = animator.avatar.isHuman,
            rootRotationY = rootY,
            skinnedRendererCount = renderers.Length,
            boneCount = renderer.bones.Length,
            bindPoseCount = mesh.bindposes.Length,
            vertexCount = mesh.vertexCount,
            triangleCount = mesh.triangles.Length / 3,
            unweightedVertexCount = unweighted,
            maxInfluencesPerVertex = maxInfluences,
            minimumWeightSum = minimumWeightSum,
            maximumWeightSum = maximumWeightSum,
            poseTestMaxVertexDelta = poseDelta,
            materialPath = AssetDatabase.GetAssetPath(renderer.sharedMaterial)
        };
    }

    private static float RunPoseDeformationTest(GameObject prefab)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            SkinnedMeshRenderer renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Transform testBone = renderer.bones.FirstOrDefault(bone => bone != null && bone.name == "forearm_stretch.l");
            if (testBone == null)
            {
                throw new InvalidOperationException("Could not find forearm_stretch.l for pose test");
            }

            Mesh bindMesh = new Mesh();
            Mesh posedMesh = new Mesh();
            try
            {
                renderer.BakeMesh(bindMesh, true);
                Vector3[] bindVertices = bindMesh.vertices;

                Quaternion originalRotation = testBone.localRotation;
                testBone.localRotation = originalRotation * Quaternion.AngleAxis(35f, Vector3.up);
                renderer.BakeMesh(posedMesh, true);
                testBone.localRotation = originalRotation;

                Vector3[] posedVertices = posedMesh.vertices;
                if (bindVertices.Length != posedVertices.Length)
                {
                    throw new InvalidOperationException("Pose test vertex counts do not match");
                }

                float maxDelta = 0f;
                for (int index = 0; index < bindVertices.Length; index++)
                {
                    maxDelta = Mathf.Max(maxDelta, Vector3.Distance(bindVertices[index], posedVertices[index]));
                }
                return maxDelta;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bindMesh);
                UnityEngine.Object.DestroyImmediate(posedMesh);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void WriteReport(RigReport report)
    {
        string output = GetCommandLineArgument("-copManRigReport");
        if (string.IsNullOrWhiteSpace(output))
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../CopMan_Rig_QA.json"));
        }

        string directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
    }

    private static string GetCommandLineArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (arguments[index] == name) return arguments[index + 1];
        }
        return null;
    }

    private static void RequireAsset(string assetPath)
    {
        string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
        {
            throw new FileNotFoundException($"Required asset is missing: {assetPath}", absolutePath);
        }
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle < 0f ? angle + 360f : angle;
    }
}
