using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Vehicles.Editor
{
    public static class SimcadeCarDeformationInstaller
    {
        private const string CarPrefabPath =
            "Assets/Ash Assets/Vehicle Integration/Car/Prefabs/Car.prefab";
        private const string CarModelPath =
            "Assets/Ash Assets/Vehicle Integration/Car/Visuals/Models/Car.FBX";

        // Edy's algorithm processes every visible mesh within the impact radius,
        // so overlapping pieces no longer compete to become one selected panel.
        private static readonly string[] PanelNames =
        {
            "FrontBumper",
            "RearBumper",
            "FrontFender",
            "FrontFenderR",
            // The source DoorFL is intentionally inactive. DoorFL (1) is the
            // visible copy parented under DoorHook for entry/exit animation.
            "DoorFL (1)",
            "DoorFR",
            "DoorRL",
            "DoorRR",
            "RearSanoughDoor",
            "MainBody"
        };

        public static void Install()
        {
            EnsureReadableCarModel();
            GameObject carRoot = PrefabUtility.LoadPrefabContents(CarPrefabPath);
            try
            {
                RequireComponent<Rigidbody>(carRoot);
                BoxCollider bodyCollider = RequireComponent<BoxCollider>(carRoot);
                SimcadeCarImpactAudio impact =
                    RequireComponent<SimcadeCarImpactAudio>(carRoot);
                SimcadeCarDriver driver = RequireComponent<SimcadeCarDriver>(carRoot);
                MeshFilter[] panels = ResolvePanels(carRoot);

                SimcadeCarDeformation deformation =
                    GetOrAdd<SimcadeCarDeformation>(carRoot);
                deformation.Configure(impact, driver, bodyCollider, panels);
                driver.ResetDamageSteering();

                bodyCollider.enabled = true;
                bodyCollider.isTrigger = false;

                EditorUtility.SetDirty(driver);
                EditorUtility.SetDirty(bodyCollider);
                EditorUtility.SetDirty(deformation);
                PrefabUtility.SaveAsPrefabAsset(carRoot, CarPrefabPath, out bool saved);
                if (!saved)
                    throw new InvalidOperationException("Could not save Car deformation setup");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(carRoot);
            }

            AssetDatabase.SaveAssets();
            ValidateInstallation();
        }

        public static void ValidateInstallation()
        {
            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (car == null) throw new InvalidOperationException("Car.prefab is missing");

            SimcadeCarDeformation deformation = car.GetComponent<SimcadeCarDeformation>();
            SimcadeCarDriver driver = car.GetComponent<SimcadeCarDriver>();
            BoxCollider bodyCollider = car.GetComponent<BoxCollider>();
            if (deformation == null || !deformation.IsConfigured)
                throw new InvalidOperationException("Car visual deformation is incomplete");
            if (driver == null || driver.MaximumDamageSteeringBias > 0.161f)
                throw new InvalidOperationException("Crash steering bias exceeds the mobile profile");
            if (bodyCollider == null || !bodyCollider.enabled || bodyCollider.isTrigger)
                throw new InvalidOperationException("Car requires its stable primitive body collider");
            if (car.GetComponentsInChildren<MeshCollider>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "Dynamic Car must not use MeshCollider; keep the primitive body collider"
                );
            }
            if (!deformation.UsesEdysMeshDeformation ||
                deformation.DeformablePanelCount != PanelNames.Length ||
                deformation.MaximumDentCount > 12 ||
                deformation.MaximumVerticesPerPanel > 24000 ||
                Mathf.Abs(deformation.MinimumImpactVelocity - 2.5f) > 0.01f ||
                Mathf.Abs(deformation.DamageRadius - 0.5f) > 0.01f ||
                Mathf.Abs(deformation.MaximumVertexDisplacement - 0.2f) > 0.01f ||
                Mathf.Abs(deformation.MaximumVertexFracture - 0.03f) > 0.001f ||
                deformation.MaximumSteeringBiasPerImpact > 0.056f)
            {
                throw new InvalidOperationException(
                    "Car deformation exceeds the event/vertex/dent mobile budget"
                );
            }

            ModelImporter importer = AssetImporter.GetAtPath(CarModelPath) as ModelImporter;
            if (importer == null || !importer.isReadable)
                throw new InvalidOperationException("Car model must be readable for visual dents");

            MeshFilter[] panels = ResolvePanels(car);
            int totalVertices = 0;
            int largestPanel = 0;
            for (int i = 0; i < panels.Length; ++i)
            {
                Mesh mesh = panels[i].sharedMesh;
                if (mesh == null || !mesh.isReadable ||
                    mesh.vertexCount > deformation.MaximumVerticesPerPanel)
                {
                    throw new InvalidOperationException(
                        $"Panel '{panels[i].name}' is missing, unreadable or above the vertex cap"
                    );
                }
                totalVertices += mesh.vertexCount;
                largestPanel = Mathf.Max(largestPanel, mesh.vertexCount);
            }

            Debug.Log(
                $"Car deformation validation passed: {panels.Length} exterior panels, " +
                $"largest panel {largestPanel} vertices ({totalVertices} total source vertices), " +
                "Edy 5.5.3 render-mesh deformation across every panel inside the radius, " +
                "normals/bounds refresh after each dent, <=12 impacts, persistent <=0.16 " +
                "steering bias and no EVP controller or deformable MeshCollider."
            );
        }

        private static void EnsureReadableCarModel()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CarModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Car model importer is missing");
            if (importer.isReadable) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static MeshFilter[] ResolvePanels(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            List<MeshFilter> panels = new List<MeshFilter>(PanelNames.Length);
            for (int nameIndex = 0; nameIndex < PanelNames.Length; ++nameIndex)
            {
                MeshFilter match = null;
                for (int transformIndex = 0; transformIndex < transforms.Length; ++transformIndex)
                {
                    Transform candidate = transforms[transformIndex];
                    if (candidate.name != PanelNames[nameIndex]) continue;
                    match = candidate.GetComponent<MeshFilter>();
                    if (match != null) break;
                }

                if (match == null || match.sharedMesh == null)
                {
                    throw new InvalidOperationException(
                        $"Deformable exterior panel is missing: {PanelNames[nameIndex]}"
                    );
                }
                panels.Add(match);
            }
            return panels.ToArray();
        }

        private static T RequireComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component == null)
                throw new InvalidOperationException(
                    $"Car.prefab requires {typeof(T).Name} before deformation setup"
                );
            return component;
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }
    }
}
