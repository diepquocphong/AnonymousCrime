using System.Collections.Generic;
using GameCreator.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(PhysicsCarController))]
public class PhysicsCarControllerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();
        root.AddToClassList("narrow-inspector");

        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Ash Assets/Vehicle Integration/Scripts/Editor/RapidEditorStyle.uss");
        if (styleSheet != null) root.styleSheets.Add(styleSheet);

        var unitySpacer = new VisualElement();
        unitySpacer.AddToClassList("unity-spacer");
        root.Add(unitySpacer);

        var header = new Image
        {
            image = EditorGUIUtility.IconContent("d_Rigidbody Icon").image
        };
        header.AddToClassList("header-image");

        header.style.width = 40;
        header.style.height = 40;
        header.style.marginLeft = 5;
        header.style.marginTop = 5;
        header.style.marginBottom = 5;

        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.justifyContent = Justify.SpaceBetween;
        headerRow.style.marginBottom = 5;

        headerRow.Add(header);

        var toggleContainer = new VisualElement();
        toggleContainer.style.flexDirection = FlexDirection.Row;
        toggleContainer.style.alignItems = Align.Center;
        toggleContainer.style.marginRight = 10;

        var toggleLabel = new Label("Vehicle Enabled");
        toggleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toggleLabel.style.marginRight = 4;
        toggleLabel.style.alignSelf = Align.Center;
        toggleContainer.Add(toggleLabel);

        var isVehicleEnabledProp = serializedObject.FindProperty("isVehicleEnabled");
        var isVehicleEnabledToggle = new PropertyField(isVehicleEnabledProp, "");
        isVehicleEnabledToggle.style.alignSelf = Align.Center;
        isVehicleEnabledToggle.style.marginTop = -2;
        isVehicleEnabledToggle.style.marginBottom = 0;
        isVehicleEnabledToggle.style.marginLeft = 5;
        isVehicleEnabledToggle.style.paddingBottom = 0;
        isVehicleEnabledToggle.style.paddingTop = 0;
        isVehicleEnabledToggle.style.height = 12;
        toggleContainer.Add(isVehicleEnabledToggle);

        headerRow.Add(toggleContainer);

        root.Add(headerRow);
        root.Add(unitySpacer);

        System.Func<string, bool, string[], Foldout> makeFoldout = (text, openByDefault, props) =>
        {
            var fold = new Foldout { text = text, value = openByDefault };
            fold.AddToClassList("foldout");

            if (openByDefault)
            {
                fold.AddToClassList("foldout-expanded");
            }

            fold.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    fold.AddToClassList("foldout-expanded");
                }
                else
                {
                    fold.RemoveFromClassList("foldout-expanded");
                }
            });

            foreach (var prop in props)
            {
                var field = new PropertyField(serializedObject.FindProperty(prop));
                field.AddToClassList("property-field");
                fold.Add(field);
            }
            return fold;
        };

        root.Add(unitySpacer);
        root.Add(makeFoldout("Base Stats", true, new string[] { "topSpeed", "currentSpeed", "acceleration", "braking" }));
        root.Add(unitySpacer);
        root.Add(makeFoldout("Advanced Physics", false, new string[] { "reverseSpeed", "reverseAcceleration", "accelerationCurve", "accelerationCurveCoefficient", "coastingDrag", "grip", "steer", "handbrakeForce", "addedGravity" }));
        root.Add(unitySpacer);
        root.Add(makeFoldout("Transmission", false, new string[] { "numberOfGears", "maxRPM", "currentRPM", "gearRatio", "idleRPM" }));
        root.Add(unitySpacer);
        root.Add(makeFoldout("Drifting", false, new string[] { "driftGrip", "driftControl", "driftDampening" }));
        root.Add(unitySpacer);
        root.Add(makeFoldout("Damage", false, new string[] { "maxDamage", "currentHealth", "collisionThreshold", "damageIntensity" }));
        root.Add(unitySpacer);
        root.Add(makeFoldout("Fuel", false, new string[] { "fuelCapacity", "currentFuel", "fuelEfficiency" }));
        root.Add(unitySpacer);
        root.Add(makeFoldout("Wheels & Suspension", false, new string[]{
            "frontLeftWheelTransform","frontRightWheelTransform","rearLeftWheelTransform",
            "rearRightWheelTransform","frontLeftWheelCollider","frontRightWheelCollider",
            "rearLeftWheelCollider","rearRightWheelCollider","suspensionHeight",
            "suspensionSpring","suspensionDamp"
        }));
        root.Add(unitySpacer);
        root.Add(makeFoldout("Center of Mass", false, new string[] { "centerOfMass" }));
        root.Add(unitySpacer);
        root.Add(makeFoldout("External Physics", false, new string[] { "m_UseExternalPhysics" }));
        root.Add(unitySpacer);
        root.Add(makeFoldout("Visual Components", false, new string[] { "carBody", "steeringWheelMesh", "steeringWheelMaxAngle", "steeringWheelRotationAxis" }));
        root.Add(unitySpacer);

        var inputProps = new List<string> { "inputMode" };
#if ENABLE_INPUT_SYSTEM
        inputProps.Add("inputActionAsset");
        inputProps.Add("moveActionName");
#endif
        root.Add(makeFoldout("Input", false, inputProps.ToArray()));
        root.Add(unitySpacer);

        var autoButton = new Button(() => (target as PhysicsCarController).AutoSetupWheelColliders())
        {
            text = "Auto Setup Wheel Colliders"
        };
        autoButton.AddToClassList("property-field");
        root.Add(autoButton);

        root.Bind(serializedObject);
        return root;
    }
}
