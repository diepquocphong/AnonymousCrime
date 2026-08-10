using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class InitialSetupMotorbike : MonoBehaviour
{
    [Header("Drag & Drop References")]
    public Transform bikeBody;
    public Transform frontWheel;
    public Transform rearWheel;
    public Transform steeringWheel;

    [Header("Lights")]
    public MeshRenderer frontLeftLight;
    public MeshRenderer frontRightLight;
    public MeshRenderer rearLeftLight;
    public MeshRenderer rearRightLight;
    public bool assignLightMaterials = false;

    public void Setup()
    {
        var bikeController = GetComponent<PhysicsBikeController>();
        var bikeEntry = GetComponent<BikeEntry>();
        var vehicleLights = GetComponent<VehicleLights>();

        if (bikeBody != null && bikeController != null)
            bikeController.bikeBody = bikeBody;

        SetupWheel(frontWheel, "FWTarget", t => bikeController.frontWheelTransform = t);
        SetupWheel(rearWheel, "RWTarget", t => bikeController.rearWheelTransform = t);

        if (steeringWheel != null && bikeController != null && bikeEntry != null)
        {
            bikeController.steeringWheelMesh = steeringWheel;

            Transform leftHand = CreateChildIfNotExists(steeringWheel, "LeftHand");
            Transform rightHand = CreateChildIfNotExists(steeringWheel, "RightHand");
            bikeEntry.steeringWheelLeftHandTarget = leftHand;
            bikeEntry.steeringWheelRightHandTarget = rightHand;
        }

        if (bikeBody != null && bikeEntry != null)
        {
            Transform leftFoot = CreateChildIfNotExists(bikeBody, "LeftFoot");
            Transform rightFoot = CreateChildIfNotExists(bikeBody, "RightFoot");
            Transform groundFoot = CreateChildIfNotExists(bikeBody, "Ground");

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
                const string basePath = "Assets/Ash Assets/Vehicle Integration/Models/Materials/";
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

        if (bikeController != null)
            bikeController.AutoSetupWheelColliders();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(gameObject);
#endif
    }

    private void SetupWheel(Transform wheel, string targetName, System.Action<Transform> assignAction)
    {
        if (wheel == null) return;
        GameObject targetGO = new GameObject(targetName);
        targetGO.transform.SetParent(transform, false);
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
[CustomEditor(typeof(InitialSetupMotorbike))]
public class InitialSetupMotorbikeEditor : Editor
{
    SerializedProperty bikeBody;
    SerializedProperty frontWheel;
    SerializedProperty rearWheel;
    SerializedProperty steeringWheel;
    SerializedProperty frontLeftLight;
    SerializedProperty frontRightLight;
    SerializedProperty rearLeftLight;
    SerializedProperty rearRightLight;
    SerializedProperty assignLightMaterials;

    void OnEnable()
    {
        bikeBody = serializedObject.FindProperty("bikeBody");
        frontWheel = serializedObject.FindProperty("frontWheel");
        rearWheel = serializedObject.FindProperty("rearWheel");
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

        EditorGUILayout.PropertyField(bikeBody);
        EditorGUILayout.PropertyField(frontWheel);
        EditorGUILayout.PropertyField(rearWheel);
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
            var setup = (InitialSetupMotorbike)target;
            Undo.RecordObject(setup, "Initial Setup Motorbike");
            setup.Setup();
        }
    }
}
#endif
