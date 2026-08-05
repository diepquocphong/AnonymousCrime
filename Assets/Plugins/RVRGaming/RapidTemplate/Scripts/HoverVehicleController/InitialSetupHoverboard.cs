using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class InitialSetupHoverboard : MonoBehaviour
{
    [Header("Drag & Drop References")]
    [Tooltip("Transform to tilt the board model")]
    public Transform modelTransform;

    public void Setup()
    {
        var hoverCtrl = GetComponent<HoverVehicleController>();
        var bikeEntry = GetComponent<BikeEntry>();

        if (modelTransform != null && hoverCtrl != null)
            hoverCtrl.modelTransform = modelTransform;

        if (modelTransform != null && bikeEntry != null)
        {
            Transform leftFoot = CreateChildIfNotExists(modelTransform, "LeftFoot");
            Transform rightFoot = CreateChildIfNotExists(modelTransform, "RightFoot");
            Transform groundFoot = CreateChildIfNotExists(modelTransform, "Ground");

            bikeEntry.leftFootTarget = leftFoot;
            bikeEntry.rightFootTarget = rightFoot;
            bikeEntry.groundLeftFootTarget = groundFoot;
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
[CustomEditor(typeof(InitialSetupHoverboard))]
public class InitialSetupHoverboardEditor : Editor
{
    SerializedProperty modelTransform;

    void OnEnable()
    {
        modelTransform = serializedObject.FindProperty("modelTransform");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(modelTransform);

        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(8);
        if (GUILayout.Button("Auto-Setup"))
        {
            var setup = (InitialSetupHoverboard)target;
            Undo.RecordObject(setup, "Initial Setup Hoverboard");
            setup.Setup();
        }
    }
}
#endif
