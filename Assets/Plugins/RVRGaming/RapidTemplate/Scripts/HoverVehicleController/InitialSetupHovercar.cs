using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class InitialSetupHovercar : MonoBehaviour
{
    [Header("Drag & Drop References")]
    [Tooltip("Transform to tilt when moving (visual model)")]
    public Transform modelTransform;
    [Tooltip("Steering wheel or handlebar mesh for hand IK")]
    public Transform steeringWheel;
    [Tooltip("Door transform for CarEntry door animations")]
    public Transform doorTransform;

    [Header("Lights")]
    public MeshRenderer frontLeftLight;
    public MeshRenderer frontRightLight;
    public MeshRenderer rearLeftLight;
    public MeshRenderer rearRightLight;
    public bool assignLightMaterials = false;

    public void Setup()
    {
        var hoverCtrl = GetComponent<HoverVehicleController>();
        var carEntry = GetComponent<CarEntry>();
        var vehicleLights = GetComponent<VehicleLights>();

        if (modelTransform != null && hoverCtrl != null)
            hoverCtrl.modelTransform = modelTransform;

        if (doorTransform != null && carEntry != null)
            carEntry.doorTransform = doorTransform;

        if (steeringWheel != null && carEntry != null)
        {
            Transform leftHand = CreateChildIfNotExists(steeringWheel, "LeftHand");
            Transform rightHand = CreateChildIfNotExists(steeringWheel, "RightHand");
            carEntry.steeringWheelLeftHandTarget = leftHand;
            carEntry.steeringWheelRightHandTarget = rightHand;
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
                    AssignMaterial(vehicleLights.frontLight1, frontMat);
                    AssignMaterial(vehicleLights.frontLight2, frontMat);
                }
                if (rearMat != null)
                {
                    AssignMaterial(vehicleLights.backLight1, rearMat);
                    AssignMaterial(vehicleLights.backLight2, rearMat);
                }
            }
#endif
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(gameObject);
#endif
    }

    private static void AssignMaterial(MeshRenderer renderer, Material mat)
    {
        if (renderer == null) return;
#if UNITY_EDITOR
        Undo.RecordObject(renderer, "Assign Light Material");
        renderer.sharedMaterial = mat;
        EditorUtility.SetDirty(renderer);
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
[CustomEditor(typeof(InitialSetupHovercar))]
public class InitialSetupHovercarEditor : Editor
{
    SerializedProperty modelTransform;
    SerializedProperty steeringWheel;
    SerializedProperty doorTransform;
    SerializedProperty frontLeftLight;
    SerializedProperty frontRightLight;
    SerializedProperty rearLeftLight;
    SerializedProperty rearRightLight;
    SerializedProperty assignLightMaterials;

    void OnEnable()
    {
        modelTransform = serializedObject.FindProperty("modelTransform");
        steeringWheel = serializedObject.FindProperty("steeringWheel");
        doorTransform = serializedObject.FindProperty("doorTransform");
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
        EditorGUILayout.PropertyField(doorTransform);

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
            var setup = (InitialSetupHovercar)target;
            Undo.RecordObject(setup, "Initial Setup Hovercar");
            setup.Setup();
        }
    }
}
#endif