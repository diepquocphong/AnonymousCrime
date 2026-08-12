using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Vehicles.Editor
{
    /// <summary>
    /// One-time, GUID-preserving migration of the active Car integration into
    /// the documented Vehicles/Car domain. Shared data deliberately stays in Core.
    /// </summary>
    public static class VehicleCarAssetConsolidator
    {
        public const string IntegrationRoot =
            "Assets/Ash Assets/Vehicle Integration";
        public const string CarRoot = IntegrationRoot + "/Vehicles/Car";
        public const string CoreDataRoot = IntegrationRoot + "/Core/Data";
        public const string CarPrefabPath = CarRoot + "/Prefabs/Car.prefab";
        public const string SimcadeRoot =
            IntegrationRoot + "/ThirdParty/Sim-Cade Vehicle Physics";

        private readonly struct MoveDefinition
        {
            public MoveDefinition(string source, string destination)
            {
                Source = source;
                Destination = destination;
            }

            public string Source { get; }
            public string Destination { get; }
        }

        private static readonly MoveDefinition[] Moves =
        {
            new MoveDefinition(
                "Assets/Ash Assets/Sim-Cade Vehicle Physics",
                SimcadeRoot
            ),
            new MoveDefinition(
                IntegrationRoot + "/Prefabs/Vehicles/Car.prefab",
                CarPrefabPath
            ),
            new MoveDefinition(
                IntegrationRoot + "/Models",
                CarRoot + "/Models"
            ),
            new MoveDefinition(
                IntegrationRoot + "/Stats/Car",
                CoreDataRoot + "/Stats"
            ),
            new MoveDefinition(
                IntegrationRoot + "/VFX/Vehicles",
                CarRoot + "/VFX/Legacy"
            ),
            new MoveDefinition(
                IntegrationRoot + "/Scripts/PhysicsCarController",
                CarRoot + "/Legacy/PhysicsCarController"
            ),
            new MoveDefinition(
                IntegrationRoot + "/Scripts/GeneralVehicles/CarEntry.cs",
                CarRoot + "/Runtime/CarEntry.cs"
            ),
            new MoveDefinition(
                IntegrationRoot + "/Scripts/GeneralVehicles/SeatedSkeletonPoseGuard.cs",
                CarRoot + "/Runtime/SeatedSkeletonPoseGuard.cs"
            ),
            new MoveDefinition(
                IntegrationRoot + "/Scripts/GeneralVehicles/ConditionCarEnabled.cs",
                CarRoot + "/Runtime/ConditionCarEnabled.cs"
            ),
            new MoveDefinition(
                IntegrationRoot + "/Scripts/GeneralVehicles/Editor/CarEntryEditor.cs",
                CarRoot + "/Editor/CarEntryEditor.cs"
            ),
            new MoveDefinition(
                IntegrationRoot + "/Animations/Vehicles/Character_Enter_Car.anim",
                CarRoot + "/Animations/EntryExit/Character_Enter_Car.anim"
            ),
            new MoveDefinition(
                IntegrationRoot + "/Animations/Vehicles/Character_Exit_Car.anim",
                CarRoot + "/Animations/EntryExit/Character_Exit_Car.anim"
            ),
            new MoveDefinition(
                IntegrationRoot + "/Animations/Vehicles/Character_Idle_Car.anim",
                CarRoot + "/Animations/EntryExit/Character_Idle_Car.anim"
            ),
            new MoveDefinition(
                IntegrationRoot + "/UI/FuelMeter.psd",
                CarRoot + "/Textures/UI/Source/FuelMeter.psd"
            ),
            new MoveDefinition(
                IntegrationRoot + "/UI/Needle.psd",
                CarRoot + "/Textures/UI/Source/Needle.psd"
            ),
            new MoveDefinition(
                IntegrationRoot + "/UI/RevCounter.psd",
                CarRoot + "/Textures/UI/Source/RevCounter.psd"
            ),
            new MoveDefinition(
                IntegrationRoot + "/UI/Square.png",
                CarRoot + "/Textures/UI/Source/Square.png"
            ),
            new MoveDefinition(
                IntegrationRoot + "/UI/Prefabs/Canvas-Interact.prefab",
                CarRoot + "/Prefabs/UI/Canvas-Interact.prefab"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Animations/Vehicles/CarExitLanding_L.anim",
                CarRoot + "/Animations/EntryExit/CarExitLanding_L.anim"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Animations/Vehicles/CarExitMoving_L.anim",
                CarRoot + "/Animations/EntryExit/CarExitMoving_L.anim"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Animations/Vehicles/Carjacking",
                CarRoot + "/Animations/Carjacking"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Generated/CarEntry",
                CarRoot + "/Animations/Generated"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Audio/Vehicles",
                CarRoot + "/Audio/SFX"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Runtime/SimcadeCarDriver.cs",
                CarRoot + "/Runtime/SimcadeCarDriver.cs"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Runtime/SimcadeCarImpactAudio.cs",
                CarRoot + "/Runtime/SimcadeCarImpactAudio.cs"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Runtime/SimcadeCarjacking.cs",
                CarRoot + "/Runtime/SimcadeCarjacking.cs"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Editor/SimcadeCarEntrySceneTool.cs",
                CarRoot + "/Editor/SimcadeCarEntrySceneTool.cs"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Editor/SimcadeCarInstaller.cs",
                CarRoot + "/Editor/SimcadeCarInstaller.cs"
            ),
            new MoveDefinition(
                "Assets/FranklinAnimations/Editor/SimcadeCarjackingInstaller.cs",
                CarRoot + "/Editor/SimcadeCarjackingInstaller.cs"
            )
        };

        public static void Consolidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "Stop Play Mode before consolidating Car assets."
                );

            ValidateMovePlan();
            EnsureDestinationParents();

            List<string> moved = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (MoveDefinition definition in Moves)
                {
                    if (!SourceExists(definition.Source)) continue;

                    string error = AssetDatabase.MoveAsset(
                        definition.Source,
                        definition.Destination
                    );
                    if (!string.IsNullOrEmpty(error))
                    {
                        throw new InvalidOperationException(
                            $"Could not move '{definition.Source}' to " +
                            $"'{definition.Destination}': {error}"
                        );
                    }

                    moved.Add($"{definition.Source} -> {definition.Destination}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateConsolidatedAssets(logSuccess: false);

            Debug.Log(
                $"Car asset consolidation complete. Moved {moved.Count} assets/folders " +
                $"with their GUIDs preserved. Root: {CarRoot}\n" +
                string.Join("\n", moved)
            );
        }

        [MenuItem("Tools/Franklin Game/Validate Consolidated Car Assets", priority = 131)]
        public static void ValidateConsolidatedAssets()
        {
            ValidateConsolidatedAssets(logSuccess: true);
        }

        private static void ValidateConsolidatedAssets(bool logSuccess)
        {
            string[] requiredPaths =
            {
                CarPrefabPath,
                CarRoot + "/Runtime/CarEntry.cs",
                CarRoot + "/Runtime/SimcadeCarDriver.cs",
                CarRoot + "/Runtime/SimcadeCarDashboard.cs",
                CarRoot + "/Runtime/SimcadeCarHealth.cs",
                CarRoot + "/Runtime/SimcadeCarDeformation.cs",
                CarRoot + "/Runtime/EdysVehicleMeshDeformation.cs",
                CarRoot + "/Runtime/SimcadeCarDamageEffects.cs",
                CarRoot + "/Runtime/SimcadeCarDestruction.cs",
                CarRoot + "/Runtime/SimcadeCarParticleWind.cs",
                CarRoot + "/Runtime/SimcadeDetachedWheelCleanup.cs",
                CarRoot + "/Runtime/SimcadeCarjacking.cs",
                CarRoot + "/Animations/EntryExit/Character_Enter_Car.anim",
                CarRoot + "/Animations/Carjacking/CarKickOutL.anim",
                CarRoot + "/Animations/Generated/Character_Enter_Car_Mirrored.anim",
                CarRoot + "/Audio/SFX/door_opening.wav",
                CarRoot + "/Audio/SFX/car_explosion_test_vehicle_cc0.wav",
                CarRoot + "/Audio/SFX/car_smoke_hiss_loop_cc0.wav",
                CarRoot + "/Audio/SFX/car_fire_crackle_loop_cc0.wav",
                CarRoot + "/VFX/ThirdParty/Hovl Studio/3D Fire and Explosions/Prefabs/Smoke1.prefab",
                CarRoot + "/VFX/ThirdParty/Hovl Studio/3D Fire and Explosions/Prefabs/Fire3.prefab",
                CarRoot + "/VFX/ThirdParty/Hovl Studio/3D Fire and Explosions/Prefabs/Explosion11.prefab",
                CarRoot + "/Audio/Radio/Stations/City_Loop_CC0.mp3",
                CarRoot + "/Audio/Radio/Stations/Vision_CC0.mp3",
                CarRoot + "/Audio/Radio/Stations/Iso1nhab1tans_CC0.mp3",
                CarRoot + "/Audio/Radio/SFX/Radio_Tune_Static_CC0.mp3",
                CarRoot + "/Textures/UI/Generated/RadioDisc.png",
                CarRoot + "/Textures/UI/Generated/RadioPower.png",
                CarRoot + "/Textures/UI/Generated/RadioPrevious.png",
                CarRoot + "/Textures/UI/Generated/RadioPlay.png",
                CarRoot + "/Textures/UI/Generated/RadioNext.png",
                CarRoot + "/Textures/UI/Generated/VehicleHealthFrame.png",
                CarRoot + "/Models/Car.FBX",
                SimcadeRoot + "/Prefabs/Car Presets/Ash_Sedan Prefab.prefab",
                SimcadeRoot + "/Prefabs/Camera Rigs/CinemachineCamera_Chase.prefab"
            };

            foreach (string path in requiredPaths)
            {
                if (!SourceExists(path))
                    throw new InvalidOperationException(
                        $"Consolidated Car asset is missing: {path}"
                    );
            }

            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (car == null || car.GetComponent<CarEntry>() == null ||
                car.GetComponent<SimcadeCarDriver>() == null)
            {
                throw new InvalidOperationException(
                    "Consolidated Car prefab is missing CarEntry or SimcadeCarDriver."
                );
            }

            SimcadeCarInstaller.ValidateInstallation();
            SimcadeCarjackingInstaller.ValidateInstallation();
            SimcadeCarDashboardInstaller.ValidateInstallation();
            SimcadeCarDamageEffectsInstaller.ValidateInstallation();
            SimcadeCarDeformationInstaller.ValidateInstallation();

            if (logSuccess)
                Debug.Log("Consolidated Car asset validation passed: " + CarPrefabPath);
        }

        private static void ValidateMovePlan()
        {
            foreach (MoveDefinition definition in Moves)
            {
                bool sourceExists = SourceExists(definition.Source);
                bool destinationExists = SourceExists(definition.Destination);
                if (sourceExists && destinationExists)
                {
                    throw new InvalidOperationException(
                        "Consolidation would overwrite an existing asset: " +
                        definition.Destination
                    );
                }

                if (!sourceExists && !destinationExists)
                {
                    throw new InvalidOperationException(
                        "Both source and destination are missing for: " +
                        definition.Source
                    );
                }
            }
        }

        private static void EnsureDestinationParents()
        {
            EnsureFolder(CarRoot);
            EnsureFolder(IntegrationRoot + "/ThirdParty");
            foreach (MoveDefinition definition in Moves)
            {
                if (!SourceExists(definition.Source)) continue;
                string parent = Path.GetDirectoryName(definition.Destination)
                    ?.Replace('\\', '/');
                EnsureFolder(parent);
            }
        }

        private static bool SourceExists(string path)
        {
            return AssetDatabase.IsValidFolder(path) ||
                AssetDatabase.LoadMainAssetAtPath(path) != null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) ||
                AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException(
                    "Cannot create AssetDatabase folder without a parent: " + folderPath
                );

            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Could not create folder: " + folderPath);
        }
    }
}
