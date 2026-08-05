using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class InitialSetupHoverbike : MonoBehaviour
{
    [Header("Drag & Drop References")]
    [Tooltip("Transform to tilt when turning (the visible model)")]
    public Transform modelTransform;
    [Tooltip("Handlebar or steering mesh")]
    public Transform steeringWheel;

    [Header("Lights")]
    public MeshRenderer frontLeftLight;
    public MeshRenderer frontRightLight;
    public MeshRenderer rearLeftLight;
    public MeshRenderer rearRightLight;
    public bool assignLightMaterials = false;

    public void Setup()
    {
        var hoverCtrl = GetComponent<HoverVehicleController>();
        var bikeEntry = GetComponent<BikeEntry>();
        var vehicleLights = GetComponent<VehicleLights>();

        if (modelTransform != null && hoverCtrl != null)
            hoverCtrl.modelTransform = modelTransform;

        if (steeringWheel != null && bikeEntry != null)
        {
            Transform leftHand = CreateChildIfNotExists(steeringWheel, "LeftHand");
            Transform rightHand = CreateChildIfNotExists(steeringWheel, "RightHand");
            bikeEntry.steeringWheelLeftHandTarget = leftHand;
            bikeEntry.steeringWheelRightHandTarget = rightHand;
        }

        if (modelTransform != null && bikeEntry != null)
        {
            Transform leftFoot = CreateChildIfNotExists(modelTransform, "LeftFoot");
            Transform rightFoot = CreateChildIfNotExists(modelTransform, "RightFoot");
            Transform groundFoot = CreateChildIfNotExists(modelTransform, "Ground");
            bikeEntry.leftFootTarget = leftFoot;
            bikeEntry.rightFootTarget = rightFoot;
            bikeEntry.groundLeftFootTarget = groundFoot;
        }

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
                Material frontMat = AssetDatabase.LoadAssetAtPath<Material>(basePath + "FrontLight.mat");
                Material rearMat = AssetDatabase.LoadAssetAtPath<Material>(basePath + "GlassRed.mat");

                if (frontMat != null)
                {
                    if (vehicleLights.frontLight1 != null)
                    {
                        Undo.RecordObject(vehicleLights.frontLight1, "Assign Front Light Material");
                        vehicleLights.frontLight1.sharedMaterial = frontMat;
                        EditorUtility.SetDirty(vehicleLights.frontLight1);
                    }
                    if (vehicleLights.frontLight2 != null)
                    {
                        Undo.RecordObject(vehicleLights.frontLight2, "Assign Front Light Material");
                        vehicleLights.frontLight2.sharedMaterial = frontMat;
                        EditorUtility.SetDirty(vehicleLights.frontLight2);
                    }
                }
                if (rearMat != null)
                {
                    if (vehicleLights.backLight1 != null)
                    {
                        Undo.RecordObject(vehicleLights.backLight1, "Assign Rear Light Material");
                        vehicleLights.backLight1.sharedMaterial = rearMat;
                        EditorUtility.SetDirty(vehicleLights.backLight1);
                    }
                    if (vehicleLights.backLight2 != null)
                    {
                        Undo.RecordObject(vehicleLights.backLight2, "Assign Rear Light Material");
                        vehicleLights.backLight2.sharedMaterial = rearMat;
                        EditorUtility.SetDirty(vehicleLights.backLight2);
                    }
                }
            }
#endif
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(gameObject);
#endif
    }

    private Transform CreateChildIfNotExists(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go.transform;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(InitialSetupHoverbike))]
public class InitialSetupHoverbikeEditor : Editor
{
    SerializedProperty modelTransform;
    SerializedProperty steeringWheel;
    SerializedProperty frontLeftLight;
    SerializedProperty frontRightLight;
    SerializedProperty rearLeftLight;
    SerializedProperty rearRightLight;
    SerializedProperty assignLightMaterials;

    void OnEnable()
    {
        modelTransform = serializedObject.FindProperty("modelTransform");
        steeringWheel = serializedObject.FindProperty("steeringWheel");
        frontLeftLight = serializedObject.FindProperty("frontLeftLight");
        frontRightLight = serializedObject.FindProperty("frontRightLight");
        rearLeftLight = serializedObject.FindProperty("rearLeftLight");
        rearRightLight = serializedObject.FindProperty("rearRightLight");
        assignLightMaterials = serializedObject.FindProperty("assignLightMaterials");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(modelTransform);
        EditorGUILayout.PropertyField(steeringWheel);

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
            var setup = (InitialSetupHoverbike)target;
            Undo.RecordObject(setup, "Initial Setup Hoverbike");
            setup.Setup();
        }
    }
}
#endif
