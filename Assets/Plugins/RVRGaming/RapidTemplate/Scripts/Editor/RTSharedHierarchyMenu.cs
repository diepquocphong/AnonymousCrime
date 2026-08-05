using UnityEditor;
using UnityEngine;

public class RTSharedHierarchyMenu : MonoBehaviour
{
    // Save
    private static readonly string checkPointPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Save/Checkpoint.prefab";
    private static readonly string savePointPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Save/Save Point.prefab";

    // Basic
    private static readonly string bedPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Bed.prefab";
    private static readonly string carryPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Carry.prefab";
    private static readonly string chairPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Chair.prefab";
    private static readonly string chestPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Chest.prefab";
    private static readonly string digPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Dig.prefab";
    private static readonly string doorPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Door.prefab";
    private static readonly string fishPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Fish.prefab";
    private static readonly string fountainPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Fountain.prefab";
    private static readonly string gatherPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Gather.prefab";
    private static readonly string pickupPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Pickup.prefab";
    private static readonly string emotePath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Basic/Canvas-Emote.prefab";

    // General
    private static readonly string lightPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/General/Directional Light.prefab";
    private static readonly string volumePath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/General/Global Volume.prefab";
    private static readonly string menuSettingsPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/General/Camera-Preview.prefab";


    [MenuItem("GameObject/Rapid Template/Save/Checkpoint", false, 10)]
    private static void AddCheckPointPrefab() => AddPrefabToScene(checkPointPath, "Checkpoint");

    [MenuItem("GameObject/Rapid Template/Save/Save Point", false, 11)]
    private static void AddSavePointPrefab() => AddPrefabToScene(savePointPath, "Save Point");

    [MenuItem("GameObject/Rapid Template/Basic/Bed", false, 20)]
    private static void AddBedPrefab() => AddPrefabToScene(bedPath, "Bed");

    [MenuItem("GameObject/Rapid Template/Basic/Carry", false, 21)]
    private static void AddCarryPrefab() => AddPrefabToScene(carryPath, "Carry");

    [MenuItem("GameObject/Rapid Template/Basic/Chair", false, 22)]
    private static void AddChairPrefab() => AddPrefabToScene(chairPath, "Chair");

    [MenuItem("GameObject/Rapid Template/Basic/Chest", false, 23)]
    private static void AddChestPrefab() => AddPrefabToScene(chestPath, "Chest");

    [MenuItem("GameObject/Rapid Template/Basic/Dig", false, 24)]
    private static void AddDigPrefab() => AddPrefabToScene(digPath, "Dig");

    [MenuItem("GameObject/Rapid Template/Basic/Door", false, 25)]
    private static void AddDoorPrefab() => AddPrefabToScene(doorPath, "Door");

    [MenuItem("GameObject/Rapid Template/Basic/Emote", false, 26)]
    private static void AddEmotePrefab() => AddPrefabToScene(emotePath, "Canvas-Emote");

    [MenuItem("GameObject/Rapid Template/Basic/Fish", false, 27)]
    private static void AddFishPrefab() => AddPrefabToScene(fishPath, "Fish");

    [MenuItem("GameObject/Rapid Template/Basic/Fountain", false, 28)]
    private static void AddFountainPrefab() => AddPrefabToScene(fountainPath, "Fountain");

    [MenuItem("GameObject/Rapid Template/Basic/Gather", false, 29)]
    private static void AddGatherPrefab() => AddPrefabToScene(gatherPath, "Gather");

    [MenuItem("GameObject/Rapid Template/Basic/Pickup", false, 30)]
    private static void AddPickupPrefab() => AddPrefabToScene(pickupPath, "Pickup");

    [MenuItem("GameObject/Rapid Template/General/Directional Light", false, 40)]
    private static void AddLightPrefab() => AddPrefabToScene(lightPath, "Directional Light");

    [MenuItem("GameObject/Rapid Template/General/Global Volume", false, 41)]
    private static void AddVolumePrefab() => AddPrefabToScene(volumePath, "Global Volume");

    [MenuItem("GameObject/Rapid Template/General/Menu Settings", false, 42)]
    private static void AddMenuSettingsPrefab() => AddPrefabToScene(menuSettingsPath, "Menu Settings");


    private static void AddPrefabToScene(string prefabPath, string prefabName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Prefab '{prefabName}' not found at path: {prefabPath}");
            return;
        }
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, $"Create {prefabName}");
        Selection.activeObject = instance;
    }

    [MenuItem("GameObject/Rapid Template/Save/Checkpoint", true)]
    [MenuItem("GameObject/Rapid Template/Save/Save Point", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Bed", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Carry", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Chair", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Chest", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Dig", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Door", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Emote", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Fish", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Fountain", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Gather", true)]
    [MenuItem("GameObject/Rapid Template/Basic/Pickup", true)]
    [MenuItem("GameObject/Rapid Template/General/Directional Light", true)]
    [MenuItem("GameObject/Rapid Template/General/Global Volume", true)]
    [MenuItem("GameObject/Rapid Template/General/Menu Settings", true)]
    private static bool ValidateAddPrefab()
    {
        return true;
    }
}