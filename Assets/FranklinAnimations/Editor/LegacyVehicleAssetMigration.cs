using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Vehicles.Editor
{
    /// <summary>
    /// Keeps only RVR assets that are referenced by assets outside the package,
    /// plus the scripts and authoring templates still used by Franklin Game.
    /// Required files retain their GUIDs while moving under Ash Assets.
    /// </summary>
    public static class LegacyVehicleAssetMigration
    {
        public const string SourceRoot = "Assets/Plugins/RVRGaming";
        public const string SourceContentRoot = SourceRoot + "/RapidTemplate";
        public const string DestinationRoot = "Assets/Ash Assets/Vehicle Integration";
        public const string MigratedCarPath =
            DestinationRoot + "/Vehicles/Car/Prefabs/Car.prefab";

        private static readonly string[] ExplicitAuthoringRoots =
        {
            SourceContentRoot + "/Prefabs/Vehicles/Car.prefab",
            SourceContentRoot + "/Prefabs/Vehicles/Empty-Motorbike.prefab",
            SourceContentRoot + "/Animations/Vehicles/Character_Idle_Bike.anim",
            SourceContentRoot + "/InputSystem_RT.inputactions"
        };

        [MenuItem("Tools/Franklin Game/Audit RVR Asset Migration")]
        public static void Audit()
        {
            HashSet<string> preserved = BuildPreserveSet();
            string reportPath = WriteReport(preserved, null);
            Debug.Log(
                $"RVR migration audit: {preserved.Count} required assets will retain their GUIDs. " +
                $"Report: {reportPath}"
            );
        }

        [MenuItem("Tools/Franklin Game/Migrate RVR Dependencies to Ash Assets")]
        public static void Migrate()
        {
            if (!AssetDatabase.IsValidFolder(SourceRoot))
            {
                Debug.Log("RVR asset migration skipped: the source folder is already absent.");
                return;
            }

            if (AssetDatabase.IsValidFolder(DestinationRoot))
            {
                throw new InvalidOperationException(
                    $"Migration destination already exists: {DestinationRoot}"
                );
            }

            HashSet<string> preserved = BuildPreserveSet();
            List<string> sourceAssets = preserved
                .Where(path => path.StartsWith(SourceContentRoot + "/", StringComparison.Ordinal))
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            if (!sourceAssets.Contains(ExplicitAuthoringRoots[0]))
                throw new InvalidOperationException("Car.prefab is missing from the migration set");

            EnsureFolder(DestinationRoot);
            foreach (string sourcePath in sourceAssets)
            {
                string destinationPath = GetDestinationPath(sourcePath);
                EnsureFolder(Path.GetDirectoryName(destinationPath)?.Replace('\\', '/'));
                if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
                {
                    throw new InvalidOperationException(
                        $"Migration would overwrite an existing asset: {destinationPath}"
                    );
                }
            }

            List<string> movedMappings = new List<string>(sourceAssets.Count);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string sourcePath in sourceAssets)
                {
                    string destinationPath = GetDestinationPath(sourcePath);
                    string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
                    if (!string.IsNullOrEmpty(error))
                    {
                        throw new InvalidOperationException(
                            $"Could not move {sourcePath} to {destinationPath}: {error}"
                        );
                    }

                    movedMappings.Add($"{sourcePath}\t{destinationPath}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (!AssetDatabase.DeleteAsset(SourceRoot))
                throw new InvalidOperationException($"Could not delete obsolete folder: {SourceRoot}");

            AssetDatabase.SaveAssets();
            string reportPath = WriteReport(preserved, movedMappings);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"RVR migration complete: moved {movedMappings.Count} required assets to " +
                $"{DestinationRoot}, deleted the obsolete RVRGaming folder. Report: {reportPath}"
            );
        }

        private static HashSet<string> BuildPreserveSet()
        {
            if (!AssetDatabase.IsValidFolder(SourceRoot))
                throw new InvalidOperationException($"RVR source folder is missing: {SourceRoot}");

            string[] externalAssets = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Where(path => !path.StartsWith(SourceRoot + "/", StringComparison.Ordinal))
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .ToArray();

            HashSet<string> preserved = new HashSet<string>(StringComparer.Ordinal);
            AddDependencies(preserved, externalAssets);
            AddDependencies(
                preserved,
                ExplicitAuthoringRoots.Where(path => AssetDatabase.LoadMainAssetAtPath(path) != null)
                    .ToArray()
            );

            string scriptsRoot = SourceContentRoot + "/Scripts";
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { scriptsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                    preserved.Add(path);
            }

            preserved.RemoveWhere(
                path => !path.StartsWith(SourceContentRoot + "/", StringComparison.Ordinal)
            );
            return preserved;
        }

        private static void AddDependencies(HashSet<string> destination, string[] roots)
        {
            if (roots == null || roots.Length == 0) return;
            string[] dependencies = AssetDatabase.GetDependencies(roots, true);
            foreach (string dependency in dependencies)
            {
                if (dependency.StartsWith(SourceContentRoot + "/", StringComparison.Ordinal))
                    destination.Add(dependency);
            }
        }

        private static string GetDestinationPath(string sourcePath)
        {
            string prefix = SourceContentRoot + "/";
            if (!sourcePath.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException($"Path is outside RapidTemplate: {sourcePath}");

            return DestinationRoot + "/" + sourcePath.Substring(prefix.Length);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string name = Path.GetFileName(folderPath);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string WriteReport(
            HashSet<string> preserved,
            List<string> movedMappings)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            string reportPath = Path.Combine(projectRoot, "Temp", "RvrAssetMigrationReport.txt");
            List<string> lines = new List<string>
            {
                $"Generated: {DateTime.Now:O}",
                $"Source: {SourceRoot}",
                $"Destination: {DestinationRoot}",
                $"Required asset count: {preserved.Count}",
                string.Empty,
                "Required assets:"
            };
            lines.AddRange(preserved.OrderBy(path => path, StringComparer.Ordinal));

            if (movedMappings != null)
            {
                lines.Add(string.Empty);
                lines.Add("Moved assets:");
                lines.AddRange(movedMappings);
            }

            File.WriteAllLines(reportPath, lines);
            return reportPath;
        }
    }
}
