#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using FranklinGame.Animations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class FranklinBikeCameraInstaller
{
    private const string PLAYER_PREFAB_PATH = "Assets/Prefab/Player.prefab";
    private const string BIKE_FOLDER =
        "Assets/Ash Assets/Arcade Bike Physics Pro/Prefabs/Bikes";

    [MenuItem("Tools/Franklin/Arcade Bikes/Use Main Camera Shot for Bikes")]
    public static void UseMainCameraShotForBikes()
    {
        ConfigureMainShotAimOnPlayer();
        int cleanedBikes = RemovePerBikeCameraObjects();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"Bikes now keep the current GC2 Main Camera Shot with a bike TPS Aim " +
            $"component on ManagerVehicle. Cleaned legacy camera objects from " +
            $"{cleanedBikes} bike prefabs."
        );
    }

    public static bool ValidateInstallation(out string message)
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            PLAYER_PREFAB_PATH
        );
        if (playerPrefab == null)
        {
            message = $"Missing Player prefab: {PLAYER_PREFAB_PATH}";
            return false;
        }

        FranklinBikeCameraManager[] managers = playerPrefab.GetComponentsInChildren<
            FranklinBikeCameraManager
        >(true);
        if (managers.Length != 0)
        {
            message = $"Player still contains {managers.Length} bike camera manager(s).";
            return false;
        }

        FranklinVehicleInteractionManager interaction =
            playerPrefab.GetComponentInChildren<FranklinVehicleInteractionManager>(true);
        if (interaction == null)
        {
            message = "Player vehicle interaction manager is missing.";
            return false;
        }
        FranklinBikeMainShotAim aim =
            interaction.GetComponent<FranklinBikeMainShotAim>();
        if (aim == null)
        {
            message = "ManagerVehicle is missing FranklinBikeMainShotAim.";
            return false;
        }

        foreach (string prefabPath in GetBikePrefabPaths())
        {
            GameObject bike = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (bike == null || FindTransform(bike, "Triggers-Camera") != null ||
                bike.GetComponentInChildren<FranklinBikeCameraManager>(true) != null)
            {
                message = $"Per-bike camera data still exists on {prefabPath}.";
                return false;
            }
        }

        message = "Bikes use ManagerVehicle TPS Aim on the current Main Camera Shot.";
        return true;
    }

    private static void ConfigureMainShotAimOnPlayer()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);
        try
        {
            FranklinVehicleInteractionManager interaction =
                root.GetComponentInChildren<FranklinVehicleInteractionManager>(true);
            if (interaction == null)
            {
                throw new InvalidOperationException(
                    "Player vehicle interaction manager is missing."
                );
            }

            Transform managerTransform = FindDirectChild(root.transform, "ManagerBikeCamera");
            if (managerTransform != null)
                Object.DestroyImmediate(managerTransform.gameObject);

            FranklinBikeMainShotAim aim =
                interaction.GetComponent<FranklinBikeMainShotAim>();
            if (aim == null)
            {
                aim = interaction.gameObject.AddComponent<FranklinBikeMainShotAim>();
            }

            SerializedObject serializedInteraction = new SerializedObject(interaction);
            serializedInteraction.FindProperty("m_BikeMainShotAim").objectReferenceValue = aim;
            serializedInteraction.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(aim);
            EditorUtility.SetDirty(interaction);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB_PATH, out bool saved);
            if (!saved)
            {
                throw new InvalidOperationException("Unity could not save Player.prefab.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int RemovePerBikeCameraObjects()
    {
        int savedCount = 0;
        foreach (string prefabPath in GetBikePrefabPaths())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform[] legacyCameras = root.GetComponentsInChildren<Transform>(true)
                    .Where(candidate => candidate.name == "Triggers-Camera")
                    .ToArray();
                foreach (Transform legacyCamera in legacyCameras)
                {
                    Object.DestroyImmediate(legacyCamera.gameObject);
                }

                // Saving also strips the obsolete per-BikeEntry camera fields now
                // owned by FranklinBikeCameraManager on Player.
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                if (saved) savedCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return savedCount;
    }

    private static string[] GetBikePrefabPaths()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { BIKE_FOLDER })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => string.Equals(
                Path.GetDirectoryName(path)?.Replace('\\', '/'),
                BIKE_FOLDER,
                StringComparison.Ordinal
            ))
            .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(
                "Bike_",
                StringComparison.OrdinalIgnoreCase
            ))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == name) return child;
        }

        return null;
    }

    private static Transform FindTransform(GameObject root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.name == name);
    }

}
#endif
