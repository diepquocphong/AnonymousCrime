using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class InitialSetupCar : MonoBehaviour
{
    [Header("Drag & Drop References")]
    public Transform carBody;
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;
    public Transform steeringWheel;
    public Transform frontLeftDoor;

    [Header("Lights")]
    public MeshRenderer frontLeftLight;
    public MeshRenderer frontRightLight;
    public MeshRenderer rearLeftLight;
    public MeshRenderer rearRightLight;
    public bool assignLightMaterials = false;

    public void Setup()
    {
        var carController = GetComponent<PhysicsCarController>();
        var carEntry = GetComponent<CarEntry>();
        var vehicleLights = GetComponent<VehicleLights>();

        if (carBody != null && carController != null)
            carController.carBody = carBody;

        SetupWheel(frontLeftWheel, "FLTarget", t => carController.frontLeftWheelTransform = t);
        SetupWheel(frontRightWheel, "FRTarget", t => carController.frontRightWheelTransform = t);
        SetupWheel(rearLeftWheel, "RLTarget", t => carController.rearLeftWheelTransform = t);
        SetupWheel(rearRightWheel, "RRTarget", t => carController.rearRightWheelTransform = t);

        if (steeringWheel != null && carController != null && carEntry != null)
        {
            carController.steeringWheelMesh = steeringWheel;
            Transform left = CreateChildIfNotExists(steeringWheel, "Left");
            Transform right = CreateChildIfNotExists(steeringWheel, "Right");
            carEntry.steeringWheelLeftHandTarget = left;
            carEntry.steeringWheelRightHandTarget = right;
        }

        if (frontLeftDoor != null && carEntry != null)
            carEntry.doorTransform = frontLeftDoor;

        if (vehicleLights != null)
        {
            if (frontLeftLight != null) vehicleLights.frontLight1 = frontLeftLight;
            if (frontRightLight != null) vehicleLights.frontLight2 = frontRightLight;
            if (rearLeftLight != null) vehicleLights.backLight1 = rearLeftLight;
            if (rearRightLight != null) vehicleLights.backLight2 = rearRightLight;

#if UNITY_EDITOR
            if (assignLightMaterials)
            {
                const string basePath = "Assets/Plugins/RVRGaming/RapidTemplate/Models/Materials/";
                string frontPath = basePath + "FrontLight.mat";
                string rearPath = basePath + "GlassRed.mat";
                Material frontMat = AssetDatabase.LoadAssetAtPath<Material>(frontPath);
                if (frontMat == null)
                {
                    Debug.LogError($"[InitialSetupCar] Failed to load front material at {frontPath}");
                }
                else
                {
                    Debug.Log($"[InitialSetupCar] Loaded front material: {frontMat.name}");
                    if (vehicleLights.frontLight1 != null)
                    {
                        Undo.RecordObject(vehicleLights.frontLight1, "Assign Front Light Material");
                        vehicleLights.frontLight1.sharedMaterial = frontMat;
                        EditorUtility.SetDirty(vehicleLights.frontLight1);
                        Debug.Log($"[InitialSetupCar] Assigned front material to frontLight1");
                    }
                    if (vehicleLights.frontLight2 != null)
                    {
                        Undo.RecordObject(vehicleLights.frontLight2, "Assign Front Light Material");
                        vehicleLights.frontLight2.sharedMaterial = frontMat;
                        EditorUtility.SetDirty(vehicleLights.frontLight2);
                        Debug.Log($"[InitialSetupCar] Assigned front material to frontLight2");
                    }
                }

                Material rearMat = AssetDatabase.LoadAssetAtPath<Material>(rearPath);
                if (rearMat == null)
                {
                    Debug.LogError($"[InitialSetupCar] Failed to load rear material at {rearPath}");
                }
                else
                {
                    Debug.Log($"[InitialSetupCar] Loaded rear material: {rearMat.name}");
                    if (vehicleLights.backLight1 != null)
                    {
                        Undo.RecordObject(vehicleLights.backLight1, "Assign Rear Light Material");
                        vehicleLights.backLight1.sharedMaterial = rearMat;
                        EditorUtility.SetDirty(vehicleLights.backLight1);
                        Debug.Log($"[InitialSetupCar] Assigned rear material to backLight1");
                    }
                    if (vehicleLights.backLight2 != null)
                    {
                        Undo.RecordObject(vehicleLights.backLight2, "Assign Rear Light Material");
                        vehicleLights.backLight2.sharedMaterial = rearMat;
                        EditorUtility.SetDirty(vehicleLights.backLight2);
                        Debug.Log($"[InitialSetupCar] Assigned rear material to backLight2");
                    }
                }
            }
#endif
        }

        if (carController != null)
            carController.AutoSetupWheelColliders();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(gameObject);
#endif
    }

    private void SetupWheel(Transform wheel, string targetName, System.Action<Transform> assignAction)
    {
        if (wheel == null) return;
        GameObject targetGO = new GameObject(targetName);
        targetGO.transform.SetParent(transform);
        targetGO.transform.position = wheel.position;
        targetGO.transform.rotation = wheel.rotation;
        wheel.SetParent(targetGO.transform, true);
        assignAction(targetGO.transform);
    }

    private Transform CreateChildIfNotExists(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null) return existing;
        GameObject go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go.transform;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(InitialSetupCar))]
public class InitialSetupCarEditor : Editor
{
    SerializedProperty carBody;
    SerializedProperty frontLeftWheel;
    SerializedProperty frontRightWheel;
    SerializedProperty rearLeftWheel;
    SerializedProperty rearRightWheel;
    SerializedProperty steeringWheel;
    SerializedProperty frontLeftDoor;
    SerializedProperty frontLeftLight;
    SerializedProperty frontRightLight;
    SerializedProperty rearLeftLight;
    SerializedProperty rearRightLight;
    SerializedProperty assignLightMaterials;

    void OnEnable()
    {
        carBody = serializedObject.FindProperty("carBody");
        frontLeftWheel = serializedObject.FindProperty("frontLeftWheel");
        frontRightWheel = serializedObject.FindProperty("frontRightWheel");
        rearLeftWheel = serializedObject.FindProperty("rearLeftWheel");
        rearRightWheel = serializedObject.FindProperty("rearRightWheel");
        steeringWheel = serializedObject.FindProperty("steeringWheel");
        frontLeftDoor = serializedObject.FindProperty("frontLeftDoor");
        frontLeftLight = serializedObject.FindProperty("frontLeftLight");
        frontRightLight = serializedObject.FindProperty("frontRightLight");
        rearLeftLight = serializedObject.FindProperty("rearLeftLight");
        rearRightLight = serializedObject.FindProperty("rearRightLight");
        assignLightMaterials = serializedObject.FindProperty("assignLightMaterials");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(carBody);
        EditorGUILayout.PropertyField(frontLeftWheel);
        EditorGUILayout.PropertyField(frontRightWheel);
        EditorGUILayout.PropertyField(rearLeftWheel);
        EditorGUILayout.PropertyField(rearRightWheel);
        EditorGUILayout.PropertyField(steeringWheel);
        EditorGUILayout.PropertyField(frontLeftDoor);

        GUILayout.Space(6);
        EditorGUILayout.PropertyField(frontLeftLight);
        EditorGUILayout.PropertyField(frontRightLight);
        EditorGUILayout.PropertyField(rearLeftLight);
        EditorGUILayout.PropertyField(rearRightLight);
        EditorGUILayout.PropertyField(assignLightMaterials, new GUIContent("Assign Light Materials"));

        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(8);
        if (GUILayout.Button("Auto-Setup"))
        {
            InitialSetupCar setup = (InitialSetupCar)target;
            Undo.RecordObject(setup, "Initial Setup Car");
            setup.Setup();
        }
    }
}
#endif