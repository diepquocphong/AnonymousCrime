using System.Collections.Generic;
using GameCreator.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(PhysicsBikeController))]
public class PhysicsBikeControllerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();
        root.AddToClassList("narrow-inspector");

        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Ash Assets/Vehicle Integration/Scripts/Editor/RapidEditorStyle.uss");
        root.styleSheets.Add(styleSheet);

        var unitySpacer = new VisualElement();
        unitySpacer.AddToClassList("unity-spacer");
        root.Add(unitySpacer);

        var header = new Image
        {
            image = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Ash Assets/Vehicle Integration/UI/Bike.png")
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

        System.Func<string, bool, string[], Foldout> Make = (title, open, names) => {
            var f = new Foldout { text = title, value = open };
            f.AddToClassList("foldout");
            if (open) f.AddToClassList("foldout-expanded");
            f.RegisterValueChangedCallback(e => {
                if (e.newValue) f.AddToClassList("foldout-expanded");
                else f.RemoveFromClassList("foldout-expanded");
            });
            foreach (var n in names)
            {
                var field = new PropertyField(serializedObject.FindProperty(n));
                field.AddToClassList("property-field");
                f.Add(field);
            }
            return f;
        };

        root.Add(unitySpacer);
        root.Add(Make("Base Stats", true, new[] { "topSpeed", "currentSpeed", "acceleration", "braking" }));
        root.Add(unitySpacer);
        root.Add(Make("Advanced Physics", false, new[] { "reverseSpeed", "reverseAcceleration", "accelerationCurve", "accelerationCurveCoefficient", "coastingDrag", "grip", "steer", "handbrakeForce", "addedGravity" }));
        root.Add(unitySpacer);
        root.Add(Make("Transmission", false, new[] { "numberOfGears", "maxRPM", "currentRPM", "gearRatio", "idleRPM" }));
        root.Add(unitySpacer);
        root.Add(Make("Drifting", false, new[] { "driftGrip", "driftControl", "driftDampening" }));
        root.Add(unitySpacer);
        root.Add(Make("Lean Tweaks", false, new[] { "driftLeanMultiplier" }));
        root.Add(unitySpacer);
        root.Add(Make("Leaning", false, new[] {
            "leanTorque",
            "maxLeanAngle",
            "leanSpeedThreshold",
            "leanSmooth",
            "visualTireGroundClearance"
        }));
        root.Add(unitySpacer);
        root.Add(Make("Stability Tuning", false, new[] { "airAngularDamping", "driftAngularDamping" }));
        root.Add(unitySpacer);
        root.Add(Make("Damage", false, new[] { "maxDamage", "currentHealth", "collisionThreshold", "damageIntensity" }));
        root.Add(unitySpacer);
        root.Add(Make("Fuel", false, new[] { "fuelCapacity", "currentFuel", "fuelEfficiency" }));
        root.Add(unitySpacer);
        root.Add(Make("Wheels & Suspension", false, new[] { "frontWheelTransform", "rearWheelTransform", "frontWheelCollider", "rearWheelCollider", "suspensionHeight", "suspensionSpring", "suspensionDamp" }));
        root.Add(unitySpacer);
        root.Add(Make("Center of Mass", false, new[] { "centerOfMass" }));
        root.Add(unitySpacer);
        root.Add(Make("Visual Components", false, new[] { "bikeBody", "steeringWheelMesh", "steeringWheelMaxAngle", "frontWheelMaxAngle", "steeringWheelRotationAxis" }));
        root.Add(unitySpacer);

        var inputProps = new List<string> { "inputMode" };
#if ENABLE_INPUT_SYSTEM
        inputProps.Add("inputActionAsset");
        inputProps.Add("moveActionName");
#endif
        root.Add(Make("Input", false, inputProps.ToArray()));
        root.Add(unitySpacer);

        var autoBtn = new Button(() => (target as PhysicsBikeController).AutoSetupWheelColliders())
        {
            text = "Auto Setup Wheel Colliders"
        };
        autoBtn.AddToClassList("property-field");
        root.Add(autoBtn);

        root.Bind(serializedObject);
        return root;
    }
}
