using System.Collections.Generic;
using GameCreator.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(HoverVehicleController))]
public class HoverVehicleControllerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();
        root.AddToClassList("narrow-inspector");

        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Ash Assets/Vehicle Integration/Core/Editor/Styles/RapidEditorStyle.uss");
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

        System.Func<string, bool, string[], Foldout> MakeFoldout = (title, open, props) => {
            var f = new Foldout { text = title, value = open };
            f.AddToClassList("foldout");
            if (open) f.AddToClassList("foldout-expanded");
            f.RegisterValueChangedCallback(evt => {
                if (evt.newValue) f.AddToClassList("foldout-expanded");
                else f.RemoveFromClassList("foldout-expanded");
            });
            foreach (var p in props)
            {
                var field = new PropertyField(serializedObject.FindProperty(p));
                field.AddToClassList("property-field");
                f.Add(field);
            }
            return f;
        };

        root.Add(unitySpacer);
        root.Add(MakeFoldout("Base Stats", true, new[] { "currentSpeed", "MoveForce", "ReversingForce", "TurnForce", "BoostForce", "HoverMultiplerInMotion", "HoverMultiplierAtRest", "HoverExponent" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Boost Resource", false, new[] { "maxBoost", "currentBoost", "boostEfficiency", "BoostCooldown", "AirControlMultiplier" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Slope Forces", false, new[] { "MaxDownhillForce", "MaxDownhillAngle", "UpthrustAngleLimit", "SlopeAngleLimit" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Landing Forces", false, new[] { "GroundedThreshold", "LandingDownforceDuration", "DescentSlowMultiplier", "DescentSlowExponent", "DescentSlowSpeedMultiplier" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Drag Values", false, new[] { "DragWhenGrounded", "DragInAir" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Rotational Forces", false, new[] { "ZRebalanceThreshold", "ZRebalanceForce", "XRebalanceThreshold", "XRebalanceForce", "MaxBankAngle", "BankSpeed" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Throttle", false, new[] { "ThrottleAcceleration", "ThrottleMaxSpeed", "BoostThrottleAcceleration", "BoostThrottleMaxSpeed" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Damage", false, new[] { "maxDamage", "currentHealth", "collisionThreshold", "damageIntensity" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Fuel", false, new[] { "fuelCapacity", "currentFuel", "fuelEfficiency" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Component References", false, new[] { "hoverPoints" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Terrain", false, new[] { "TerrainMask" }));
        root.Add(unitySpacer);
        root.Add(MakeFoldout("Visual Components", false, new[] { "modelTransform" }));
        root.Add(unitySpacer);

        var inputProps = new List<string> { "inputMode" };
#if ENABLE_INPUT_SYSTEM
        inputProps.Add("inputActionAsset");
        inputProps.Add("moveActionName");
#endif
        root.Add(MakeFoldout("Input", false, inputProps.ToArray()));
        root.Add(unitySpacer);

        root.Bind(serializedObject);
        return root;
    }
}
