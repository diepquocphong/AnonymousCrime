using UnityEditor;
using UnityEngine;

public class RTVehiclesHierarchyMenu : MonoBehaviour
{
    private static readonly string playerPrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Player.prefab";
    private static readonly string carPrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Car.prefab";
    private static readonly string motorBikePrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Motorbike.prefab";
    private static readonly string hoverVehiclePrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Hoverbike.prefab";
    private static readonly string hoverBoardPrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Hoverboard.prefab";

    private static readonly string shotPrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Camera Shot Player.prefab";
    private static readonly string shot2PrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Camera Shot Vehicle.prefab";
    private static readonly string shot3PrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Main Camera.prefab";

    private static readonly string serviceStationPrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/ServiceStation.prefab";

    private static readonly string demoUIPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Canvas-Demo-UI.prefab";
    private static readonly string fadePath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Canvas-FadeIn.prefab";

    private static readonly string emptyCarPrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Empty-Car.prefab";
    private static readonly string emptyMotorbikePrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Empty-Motorbike.prefab";
    private static readonly string emptyHcarPrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Empty-Hovercar.prefab";
    private static readonly string emptyHbikePrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Empty-Hoverbike.prefab";
    private static readonly string emptyHboardPrefabPath = "Assets/Plugins/RVRGaming/RapidTemplate/Prefabs/Vehicles/Empty-Hoverboard.prefab";


    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Player", false, 10)]
    private static void AddVehiclePlayerPrefab() => AddPrefabToScene(playerPrefabPath, "Player");

    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Car", false, 11)]
    private static void AddVehicleCarPrefab() => AddPrefabToScene(carPrefabPath, "Car");

    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Motorbike", false, 12)]
    private static void AddVehicleMotorBikePrefab() => AddPrefabToScene(motorBikePrefabPath, "Motorbike");

    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Hoverbike", false, 13)]
    private static void AddVehicleHoverPrefab() => AddPrefabToScene(hoverVehiclePrefabPath, "Hoverbike");

    [MenuItem("GameObject/Rapid Template/Vehicles/Camera/Shot Player", false, 14)]
    private static void AddVehicleShotPrefab() => AddPrefabToScene(shotPrefabPath, "Camera Shot Player");

    [MenuItem("GameObject/Rapid Template/Vehicles/Camera/Shot Vehicle", false, 15)]
    private static void AddVehicleShot2Prefab() => AddPrefabToScene(shot2PrefabPath, "Camera Shot Vehicle");

    [MenuItem("GameObject/Rapid Template/Vehicles/Camera/Main Camera", false, 16)]
    private static void AddVehicleShot3Prefab() => AddPrefabToScene(shot3PrefabPath, "Main Camera");

    [MenuItem("GameObject/Rapid Template/Vehicles/Props/Service Station", false, 17)]
    private static void AddServiceStationPrefab() => AddPrefabToScene(serviceStationPrefabPath, "Service Station");

    [MenuItem("GameObject/Rapid Template/Vehicles/UI/Canvas Demo UI", false, 18)]
    private static void AddDemoUIPrefab() => AddPrefabToScene(demoUIPath, "Canvas-Demo-UI");

    [MenuItem("GameObject/Rapid Template/Vehicles/UI/Canvas Fade In", false, 19)]
    private static void AddFadePrefab() => AddPrefabToScene(fadePath, "Canvas-FadeIn");

    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Car", false, 24)]
    private static void AddPhysicsCarPrefab() => AddPrefabToScene(emptyCarPrefabPath, "Empty-Car");

    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Motorbike", false, 25)]
    private static void AddPhysicsMotorPrefab() => AddPrefabToScene(emptyMotorbikePrefabPath, "Empty-Motorbike");

    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Hovercar", false, 26)]
    private static void AddHoverCarPrefab() => AddPrefabToScene(emptyHcarPrefabPath, "Empty-Hovercar");

    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Hoverbike", false, 27)]
    private static void AddHoverBikePrefab() => AddPrefabToScene(emptyHbikePrefabPath, "Empty-Hoverbike");

    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Hoverboard", false, 28)]
    private static void AddHoverBoardPrefab() => AddPrefabToScene(hoverBoardPrefabPath, "Hoverboard");

    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Hoverboard", false, 29)]
    private static void AddHoverBoardEmptyPrefab() => AddPrefabToScene(emptyHboardPrefabPath, "Empty-Hoverboard");


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

    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Player", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Car", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Motorbike", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Hoverbike", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Vehicles/Hoverboard", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Camera/Shot Player", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Camera/Shot Vehicle", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Camera/Main Camera", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Props/Service Station", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/UI/Canvas Demo UI", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/UI/Canvas Fade In", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Car", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Motorbike", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Hovercar", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Hoverbike", true)]
    [MenuItem("GameObject/Rapid Template/Vehicles/Empty/Empty-Hoverboard", true)]
    private static bool ValidateAddPrefab()
    {
        return true;
    }
}