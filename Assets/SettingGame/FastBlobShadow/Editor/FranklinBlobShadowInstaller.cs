using System;
using FranklinGame.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Rendering.Editor
{
    /// <summary>
    /// Idempotent prefab installer for Franklin's gameplay characters and vehicles.
    /// It only owns the MobileBlobShadow child and FranklinBlobShadow component.
    /// </summary>
    public static class FranklinBlobShadowInstaller
    {
        private const string MeshPath =
            "Assets/SettingGame/FastBlobShadow/Meshes/ShadowSphere_Mesh.fbx";
        private const string MaterialPath =
            "Assets/SettingGame/FastBlobShadow/Materials/PlayerBlobShadow.mat";

        private readonly struct ShadowProfile
        {
            public readonly FranklinBlobShadow.FootprintShape Shape;
            public readonly float Width;
            public readonly float Length;
            public readonly float VolumeHeight;
            public readonly float GroundedPivotHeight;
            public readonly float FadeStartHeight;
            public readonly float HideHeight;
            public readonly float MinimumScale;
            public readonly float Intensity;
            public readonly float ProbeInterval;
            public readonly float MaximumGroundDistance;
            public readonly float MaximumVisibleDistance;

            public ShadowProfile(
                FranklinBlobShadow.FootprintShape shape,
                float width,
                float length,
                float volumeHeight,
                float groundedPivotHeight,
                float fadeStartHeight,
                float hideHeight,
                float minimumScale,
                float intensity,
                float probeInterval,
                float maximumGroundDistance,
                float maximumVisibleDistance
            )
            {
                this.Shape = shape;
                this.Width = width;
                this.Length = length;
                this.VolumeHeight = volumeHeight;
                this.GroundedPivotHeight = groundedPivotHeight;
                this.FadeStartHeight = fadeStartHeight;
                this.HideHeight = hideHeight;
                this.MinimumScale = minimumScale;
                this.Intensity = intensity;
                this.ProbeInterval = probeInterval;
                this.MaximumGroundDistance = maximumGroundDistance;
                this.MaximumVisibleDistance = maximumVisibleDistance;
            }
        }

        private readonly struct FootprintSize
        {
            public readonly float Width;
            public readonly float Length;

            public FootprintSize(float width, float length)
            {
                this.Width = width;
                this.Length = length;
            }
        }

        private static readonly string[] IgnoredRendererNameTokens =
        {
            "MobileBlobShadow",
            "Smoke",
            "Fire",
            "Flare",
            "Nitro",
            "Particle",
            "Skidmark",
            "Trail",
            "Canvas",
            "Gizmo",
            "Marker"
        };

        public static void InstallAll()
        {
            Mesh mesh = LoadVolumeMesh();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mesh == null || material == null)
            {
                throw new InvalidOperationException(
                    "Franklin Blob Shadow installer could not load its core mesh or material."
                );
            }

            ShadowProfile npcProfile = new ShadowProfile(
                FranklinBlobShadow.FootprintShape.Circle,
                1f, 1f, 0.65f, 1f, 0.05f, 2.5f, 0.25f, 0.52f, 0.10f, 5f, 28f
            );
            ShadowProfile carProfile = new ShadowProfile(
                FranklinBlobShadow.FootprintShape.Rectangle,
                2f, 4.2f, 0.85f, 0.70f, 0.15f, 4.5f, 0.20f, 0.68f, 0.10f, 6f, 45f
            );
            ShadowProfile bikeProfile = new ShadowProfile(
                FranklinBlobShadow.FootprintShape.Ellipse,
                0.9f, 2.15f, 0.75f, 0.65f, 0.10f, 3.5f, 0.20f, 0.64f, 0.10f, 5f, 40f
            );

            int changedCount = 0;
            changedCount += InstallPrefab(
                "Assets/Prefab/NPC.prefab",
                mesh,
                material,
                npcProfile
            );
            changedCount += InstallPrefab(
                "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Prefabs/Car.prefab",
                mesh,
                material,
                carProfile
            );

            for (int index = 1; index <= 10; index++)
            {
                string path = string.Format(
                    "Assets/Ash Assets/Arcade Bike Physics Pro/Prefabs/Bikes/Bike_{0:00}.prefab",
                    index
                );
                changedCount += InstallPrefab(path, mesh, material, bikeProfile);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[FranklinBlobShadowInstaller] Installed/updated {changedCount} NPC, Car and Bike prefabs."
            );
        }

        private static int InstallPrefab(
            string prefabPath,
            Mesh mesh,
            Material material,
            ShadowProfile profile
        )
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"[FranklinBlobShadowInstaller] Prefab not found: {prefabPath}");
                return 0;
            }

            try
            {
                Transform shadowTransform = FindDirectChild(root.transform, "MobileBlobShadow");
                if (shadowTransform == null)
                {
                    GameObject shadowObject = new GameObject("MobileBlobShadow");
                    shadowObject.layer = root.layer;
                    shadowTransform = shadowObject.transform;
                    shadowTransform.SetParent(root.transform, false);
                }

                FootprintSize footprint = ResolveFootprintSize(
                    root.transform,
                    shadowTransform,
                    profile
                );

                shadowTransform.localPosition = new Vector3(
                    0f,
                    -profile.GroundedPivotHeight + 0.025f,
                    0f
                );
                shadowTransform.localRotation = Quaternion.identity;
                shadowTransform.localScale = new Vector3(
                    footprint.Width,
                    profile.VolumeHeight,
                    footprint.Length
                );

                MeshFilter filter = GetOrAddComponent<MeshFilter>(shadowTransform.gameObject);
                MeshRenderer renderer = GetOrAddComponent<MeshRenderer>(shadowTransform.gameObject);
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.allowOcclusionWhenDynamic = true;

                FranklinBlobShadow controller = GetOrAddComponent<FranklinBlobShadow>(root);
                SerializedObject serialized = new SerializedObject(controller);
                SetObject(serialized, "m_ShadowTransform", shadowTransform);
                SetObject(serialized, "m_ShadowRenderer", renderer);
                SetObject(serialized, "m_RenderCamera", null);
                SetColor(serialized, "m_ShadowColor", Color.black);
                SetFloat(serialized, "m_Intensity", profile.Intensity);
                SetFloat(serialized, "m_Power", 1.7f);
                SetEnum(serialized, "m_FootprintShape", (int)profile.Shape);
                SetVector(serialized, "m_VolumeSize", new Vector3(
                    footprint.Width,
                    profile.VolumeHeight,
                    footprint.Length
                ));
                SetFloat(serialized, "m_GroundedPivotHeight", profile.GroundedPivotHeight);
                SetFloat(serialized, "m_AirborneFadeStart", profile.FadeStartHeight);
                SetFloat(serialized, "m_AirborneHideHeight", profile.HideHeight);
                SetFloat(serialized, "m_MinAirborneScale", profile.MinimumScale);
                SetBool(serialized, "m_FollowGround", true);
                SetLayerMask(serialized, "m_GroundLayers", ~0);
                SetFloat(serialized, "m_ProbeStartHeight", 0.5f);
                SetFloat(serialized, "m_MaxGroundDistance", profile.MaximumGroundDistance);
                SetFloat(serialized, "m_ProbeInterval", profile.ProbeInterval);
                SetFloat(serialized, "m_GroundOffset", 0.025f);
                SetFloat(serialized, "m_MinGroundNormalY", 0.35f);
                SetBool(serialized, "m_HideWhenGroundMissing", true);
                SetBool(serialized, "m_ConfigureCameraDepth", true);
                SetFloat(serialized, "m_MaxVisibleDistance", profile.MaximumVisibleDistance);
                SetFloat(serialized, "m_DistanceCheckInterval", 0.25f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(shadowTransform.gameObject);
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log(
                    $"[FranklinBlobShadowInstaller] {root.name}: {profile.Shape} " +
                    $"{footprint.Width:F2}m x {footprint.Length:F2}m, " +
                    $"opacity {profile.Intensity:F2}."
                );
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static FootprintSize ResolveFootprintSize(
            Transform root,
            Transform shadowTransform,
            ShadowProfile profile
        )
        {
            if (profile.Shape == FranklinBlobShadow.FootprintShape.Circle)
            {
                float diameter = Mathf.Max(profile.Width, profile.Length);
                return new FootprintSize(diameter, diameter);
            }

            if (!TryMeasureVisualFootprint(root, shadowTransform, out FootprintSize measured))
            {
                return new FootprintSize(profile.Width, profile.Length);
            }

            if (profile.Shape == FranklinBlobShadow.FootprintShape.Rectangle)
            {
                return new FootprintSize(
                    Mathf.Clamp(measured.Width * 0.96f, 1.75f, 2.30f),
                    Mathf.Clamp(measured.Length * 0.96f, 3.60f, 5.00f)
                );
            }

            return new FootprintSize(
                Mathf.Clamp(measured.Width * 0.95f, 0.72f, 1.40f),
                Mathf.Clamp(measured.Length * 0.95f, 1.65f, 2.80f)
            );
        }

        private static bool TryMeasureVisualFootprint(
            Transform root,
            Transform shadowTransform,
            out FootprintSize footprint
        )
        {
            Vector3 minimum = new Vector3(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity
            );
            Vector3 maximum = new Vector3(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity
            );
            bool foundBounds = false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer.transform.IsChildOf(shadowTransform)) continue;
                if (ShouldIgnoreRenderer(renderer.transform, root)) continue;
                if (!TryGetRendererLocalBounds(renderer, out Bounds localBounds)) continue;

                Vector3 center = localBounds.center;
                Vector3 extents = localBounds.extents;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localCorner = center + new Vector3(
                        (corner & 1) == 0 ? -extents.x : extents.x,
                        (corner & 2) == 0 ? -extents.y : extents.y,
                        (corner & 4) == 0 ? -extents.z : extents.z
                    );
                    Vector3 rootPoint = root.InverseTransformPoint(
                        renderer.transform.TransformPoint(localCorner)
                    );
                    minimum = Vector3.Min(minimum, rootPoint);
                    maximum = Vector3.Max(maximum, rootPoint);
                    foundBounds = true;
                }
            }

            footprint = foundBounds
                ? new FootprintSize(maximum.x - minimum.x, maximum.z - minimum.z)
                : default;
            return foundBounds;
        }

        private static bool TryGetRendererLocalBounds(Renderer renderer, out Bounds bounds)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                if (skinnedRenderer.sharedMesh == null)
                {
                    bounds = default;
                    return false;
                }

                bounds = skinnedRenderer.localBounds;
                return bounds.size.sqrMagnitude > 0f;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                bounds = default;
                return false;
            }

            bounds = filter.sharedMesh.bounds;
            return bounds.size.sqrMagnitude > 0f;
        }

        private static bool ShouldIgnoreRenderer(Transform current, Transform root)
        {
            while (current != null)
            {
                foreach (string token in IgnoredRendererNameTokens)
                {
                    if (current.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                if (current == root) break;
                current = current.parent;
            }

            return false;
        }

        private static Mesh LoadVolumeMesh()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(MeshPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Mesh mesh) return mesh;
            }

            return null;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == childName) return child;
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }

        private static void SetObject(
            SerializedObject serialized,
            string name,
            UnityEngine.Object value
        )
        {
            serialized.FindProperty(name).objectReferenceValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string name, float value)
        {
            serialized.FindProperty(name).floatValue = value;
        }

        private static void SetBool(SerializedObject serialized, string name, bool value)
        {
            serialized.FindProperty(name).boolValue = value;
        }

        private static void SetLayerMask(SerializedObject serialized, string name, int value)
        {
            serialized.FindProperty(name).intValue = value;
        }

        private static void SetEnum(SerializedObject serialized, string name, int value)
        {
            serialized.FindProperty(name).enumValueIndex = value;
        }

        private static void SetVector(SerializedObject serialized, string name, Vector3 value)
        {
            serialized.FindProperty(name).vector3Value = value;
        }

        private static void SetColor(SerializedObject serialized, string name, Color value)
        {
            serialized.FindProperty(name).colorValue = value;
        }
    }
}
