using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(CarEntry))]
public class CarEntryEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();
        root.AddToClassList("narrow-inspector");

        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
            "Assets/Ash Assets/Vehicle Integration/Scripts/Editor/RapidEditorStyle.uss"
        );
        if (styleSheet != null) root.styleSheets.Add(styleSheet);

        // Spacer helper
        VisualElement Spacer()
        {
            var s = new VisualElement();
            s.AddToClassList("unity-spacer");
            return s;
        }

        root.Add(Spacer());
        root.Add(Spacer());

        Foldout MakeFoldout(string text, bool openByDefault, string[] props)
        {
            var fold = new Foldout { text = text, value = openByDefault };
            fold.AddToClassList("foldout");

            if (openByDefault) fold.AddToClassList("foldout-expanded");

            fold.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) fold.AddToClassList("foldout-expanded");
                else fold.RemoveFromClassList("foldout-expanded");
            });

            foreach (string prop in props)
            {
                SerializedProperty sp = serializedObject.FindProperty(prop);
                if (sp == null) continue;

                var field = new PropertyField(sp);
                field.AddToClassList("property-field");
                fold.Add(field);
            }

            return fold;
        }

        root.Add(Spacer());
        root.Add(MakeFoldout("Animation Settings", true, new[]
        {
            "entryAnimation",
            "useMirroredEntryAnimation",
            "mirroredEntryAnimation",
            "mirrorDoorHandleHandWithEntry",
            "exitAnimation",
            "animationMask"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("Animation Transitions", false, new[]
        {
            "entryAnimationTransitionIn",
            "entryAnimationTransitionOut",
            "exitAnimationTransitionIn",
            "exitAnimationTransitionOut",
            "entryAnimationSpeed",
            "exitAnimationSpeed",
            "useRootMotion"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("Speed Aware Exit", true, new[]
        {
            "movingExitAnimation",
            "movingExitLandingAnimation",
            "fastExitSpeedKph",
            "stoppedExitSpeedKph",
            "exitStopTimeout",
            "movingExitAnimationSpeed",
            "movingExitLandingSpeed",
            "movingExitDoorLeadTime",
            "movingExitLandingClipDuration",
            "movingExitInheritedVelocity",
            "movingExitLateralSpeed",
            "movingExitUpwardSpeed",
            "movingExitTransientDuration",
            "movingExitTransientFade",
            "movingExitClearance"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("Door Settings", false, new[]
        {
            "doorTransform",
            "doorOpenRotation",
            "doorRotationDuration",
            "doorRotationStartDelay",
            "doorResetDelay"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("Door Handle IK", true, new[]
        {
            "doorHandleTarget",
            "doorHandleHand",
            "doorHandleIKWeight",
            "entryDoorHandleIKCurve",
            "exitDoorHandleIKCurve"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("Driving State", false, new[]
        {
            "drivingState",
            "drivingStateLayer",
            "drivingStateTransitionIn",
            "drivingStateTransitionOut"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("Entry Alignment", true, new[]
        {
            "entryStandingPoint",
            "entryStepPoint",
            "entryCabinPoint",
            "alignCharacterToStandingPoint",
            "useAuthoredEntryPath",
            "entryStepNormalizedTime",
            "entryApproachStopDistance",
            "entryApproachTimeout",
            "entryApproachAlignmentDuration",
            "entryApproachMotionPriority",
            "entryParent",
            "entrySeatAlignmentStart",
            "entrySeatAlignmentSharpness"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("Cabin To Driver Seat", true, new[]
        {
            "entrySeatSettleDuration",
            "entrySeatSettleCurve",
            "entryLeftFootTarget",
            "entryRightFootTarget",
            "entryFootIKWeight",
            "entryFootIKCurve"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("IK Settings", false, new[]
        {
            "steeringWheelLeftHandTarget",
            "steeringWheelRightHandTarget",
            "leftHandIKWeight",
            "rightHandIKWeight"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("On Enter Instructions", false, new[]
        {
            "onEnter"
        }));

        root.Add(Spacer());
        root.Add(MakeFoldout("On Exit Instructions", false, new[]
        {
            "onExit"
        }));

        root.Add(Spacer());

        var helpBox = new HelpBox(
            "Use Tools > Franklin Game > Car Entry Live Setup to move the standing, seat " +
            "and door-handle targets directly in Scene View with a live door preview.",
            HelpBoxMessageType.Info
        );
        helpBox.AddToClassList("property-field");
        root.Add(helpBox);

        root.Bind(serializedObject);
        return root;
    }
}
