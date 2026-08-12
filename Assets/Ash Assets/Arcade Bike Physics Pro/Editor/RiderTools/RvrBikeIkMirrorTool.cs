#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mirrors the selected RVR bike entry/hand/foot target across the bike's local
/// X center plane. This works in Prefab Mode and on scene instances without
/// adding an authoring component to runtime prefabs.
/// </summary>
[InitializeOnLoad]
internal static class RvrBikeIkMirrorTool
{
    private const string EnabledPreference =
        "FranklinGame.RvrBikeIkMirrorTool.Enabled";
    private const string ToggleMenu =
        "Tools/Franklin/RVR/Bike IK Mirror/Enabled";

    private static Transform s_TrackedTarget;
    private static TargetPose s_TrackedPose;
    private static bool s_HasTrackedPose;

    private readonly struct TargetPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 LocalScale;

        public TargetPose(Transform target, Transform frame)
        {
            this.Position = frame.InverseTransformPoint(target.position);
            this.Rotation = Quaternion.Inverse(frame.rotation) * target.rotation;
            this.LocalScale = target.localScale;
        }

        public bool ApproximatelyEquals(TargetPose other)
        {
            return (this.Position - other.Position).sqrMagnitude < 0.00000001f &&
                   Quaternion.Angle(this.Rotation, other.Rotation) < 0.001f &&
                   (this.LocalScale - other.LocalScale).sqrMagnitude < 0.00000001f;
        }
    }

    static RvrBikeIkMirrorTool()
    {
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.delayCall += RefreshMenuCheck;
    }

    internal static bool Enabled
    {
        get => EditorPrefs.GetBool(EnabledPreference, true);
        set
        {
            EditorPrefs.SetBool(EnabledPreference, value);
            Menu.SetChecked(ToggleMenu, value);
            ResetTracking();
        }
    }

    [MenuItem(ToggleMenu, false, 120)]
    private static void ToggleEnabled()
    {
        Enabled = !Enabled;
    }

    [MenuItem(ToggleMenu, true)]
    private static bool ValidateToggleEnabled()
    {
        Menu.SetChecked(ToggleMenu, Enabled);
        return true;
    }

    [MenuItem("Tools/Franklin/RVR/Bike IK Mirror/Mirror Selected Target Now", false, 121)]
    private static void MirrorSelectedTargetNow()
    {
        Transform source = Selection.activeTransform;
        if (!TryGetEntryAndCounterpart(source, out BikeEntry entry, out _))
        {
            Debug.LogWarning(
                "Select an Entry, Left/Right Hand or Left/Right Foot target assigned to BikeEntry."
            );
            return;
        }

        MirrorFrom(entry, source, true);
        s_TrackedTarget = source;
        s_TrackedPose = new TargetPose(source, entry.transform);
        s_HasTrackedPose = true;
    }

    [MenuItem("Tools/Franklin/RVR/Bike IK Mirror/Validate Tool", false, 122)]
    private static void ValidateTool()
    {
        GameObject root = new GameObject("Bike IK Mirror Validation");
        try
        {
            BikeEntry entry = root.AddComponent<BikeEntry>();
            Transform leftHand = CreateTarget(root.transform, "LeftHand");
            Transform rightHand = CreateTarget(root.transform, "RightHand");
            Transform leftFoot = CreateTarget(root.transform, "LeftFoot");
            Transform rightFoot = CreateTarget(root.transform, "RightFoot");
            Transform originalEntry = CreateTarget(root.transform, "Entry Original");
            Transform mirroredEntry = CreateTarget(root.transform, "Entry Mirrored");
            entry.steeringWheelLeftHandTarget = leftHand;
            entry.steeringWheelRightHandTarget = rightHand;
            entry.leftFootTarget = leftFoot;
            entry.rightFootTarget = rightFoot;
            entry.entryStandingPoint = originalEntry;
            entry.mirroredEntryStandingPoint = mirroredEntry;

            originalEntry.localPosition = new Vector3(-0.8f, 0.43f, 0.05f);
            originalEntry.localRotation = Quaternion.Euler(0f, 4f, 0f);
            MirrorFrom(entry, originalEntry, false);
            AssertMirrored(root.transform, originalEntry, mirroredEntry, "entry");

            leftHand.localPosition = new Vector3(-0.42f, 0.73f, 0.18f);
            leftHand.localRotation = Quaternion.Euler(12f, 25f, -30f);
            MirrorFrom(entry, leftHand, false);
            AssertMirrored(root.transform, leftHand, rightHand, "hand");

            rightFoot.localPosition = new Vector3(0.31f, 0.16f, -0.27f);
            rightFoot.localRotation = Quaternion.Euler(-8f, 14f, 21f);
            MirrorFrom(entry, rightFoot, false);
            AssertMirrored(root.transform, rightFoot, leftFoot, "foot");

            Debug.Log("[RVR Bikes] Entry/IK mirror validation: PASS.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    internal static bool MirrorFrom(BikeEntry entry, Transform source, bool recordUndo)
    {
        if (entry == null || source == null ||
            !TryGetCounterpart(entry, source, out Transform counterpart))
        {
            return false;
        }

        Transform mirrorFrame = entry.transform;
        Vector3 localPosition = mirrorFrame.InverseTransformPoint(source.position);
        localPosition.x = -localPosition.x;

        Quaternion localRotation = Quaternion.Inverse(mirrorFrame.rotation) * source.rotation;
        Quaternion mirroredLocalRotation = new Quaternion(
            localRotation.x,
            -localRotation.y,
            -localRotation.z,
            localRotation.w
        );

        if (recordUndo) Undo.RecordObject(counterpart, "Mirror Bike IK Target");
        counterpart.position = mirrorFrame.TransformPoint(localPosition);
        counterpart.rotation = mirrorFrame.rotation * mirroredLocalRotation;
        if (counterpart.parent == source.parent)
        {
            counterpart.localScale = source.localScale;
        }

        EditorUtility.SetDirty(counterpart);
        if (PrefabUtility.IsPartOfPrefabInstance(counterpart))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(counterpart);
        }

        SceneView.RepaintAll();
        return true;
    }

    private static void OnEditorUpdate()
    {
        if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ResetTracking();
            return;
        }

        Transform source = Selection.activeTransform;
        if (!TryGetEntryAndCounterpart(source, out BikeEntry entry, out _))
        {
            ResetTracking();
            return;
        }

        TargetPose currentPose = new TargetPose(source, entry.transform);
        if (source != s_TrackedTarget || !s_HasTrackedPose)
        {
            s_TrackedTarget = source;
            s_TrackedPose = currentPose;
            s_HasTrackedPose = true;
            return;
        }

        if (currentPose.ApproximatelyEquals(s_TrackedPose)) return;

        MirrorFrom(entry, source, true);
        s_TrackedPose = currentPose;
    }

    private static bool TryGetEntryAndCounterpart(
        Transform source,
        out BikeEntry entry,
        out Transform counterpart)
    {
        counterpart = null;
        entry = source != null ? source.GetComponentInParent<BikeEntry>(true) : null;
        return entry != null && TryGetCounterpart(entry, source, out counterpart);
    }

    private static bool TryGetCounterpart(
        BikeEntry entry,
        Transform source,
        out Transform counterpart)
    {
        counterpart = null;
        if (source == entry.steeringWheelLeftHandTarget)
            counterpart = entry.steeringWheelRightHandTarget;
        else if (source == entry.steeringWheelRightHandTarget)
            counterpart = entry.steeringWheelLeftHandTarget;
        else if (source == entry.leftFootTarget)
            counterpart = entry.rightFootTarget;
        else if (source == entry.rightFootTarget)
            counterpart = entry.leftFootTarget;
        else if (source == entry.entryStandingPoint)
            counterpart = entry.mirroredEntryStandingPoint;
        else if (source == entry.mirroredEntryStandingPoint)
            counterpart = entry.entryStandingPoint;

        return counterpart != null && counterpart != source;
    }

    private static void RefreshMenuCheck()
    {
        Menu.SetChecked(ToggleMenu, Enabled);
    }

    private static void ResetTracking()
    {
        s_TrackedTarget = null;
        s_HasTrackedPose = false;
    }

    private static Transform CreateTarget(Transform parent, string name)
    {
        GameObject target = new GameObject(name);
        target.transform.SetParent(parent, false);
        return target.transform;
    }

    private static void AssertMirrored(
        Transform frame,
        Transform source,
        Transform counterpart,
        string label)
    {
        Vector3 expectedPosition = frame.InverseTransformPoint(source.position);
        expectedPosition.x = -expectedPosition.x;
        Vector3 actualPosition = frame.InverseTransformPoint(counterpart.position);
        if ((expectedPosition - actualPosition).sqrMagnitude > 0.00000001f)
        {
            throw new InvalidOperationException($"Bike IK {label} position mirror failed.");
        }

        Quaternion sourceLocal = Quaternion.Inverse(frame.rotation) * source.rotation;
        Quaternion expectedRotation = new Quaternion(
            sourceLocal.x,
            -sourceLocal.y,
            -sourceLocal.z,
            sourceLocal.w
        );
        Quaternion actualRotation = Quaternion.Inverse(frame.rotation) * counterpart.rotation;
        if (Quaternion.Angle(expectedRotation, actualRotation) > 0.001f)
        {
            throw new InvalidOperationException($"Bike IK {label} rotation mirror failed.");
        }
    }
}

[CustomEditor(typeof(BikeEntry))]
internal sealed class RvrBikeEntryMirrorEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        this.serializedObject.Update();

        BikeEntry entry = (BikeEntry)this.target;
        SerializedProperty fitStyleProperty =
            this.serializedObject.FindProperty("riderFitStyle");
        SerializedProperty sourceClipProperty =
            this.serializedObject.FindProperty("riderPoseSourceClip");
        SerializedProperty generatedClipProperty =
            this.serializedObject.FindProperty("riderPoseClip");
        SerializedProperty seatOffsetProperty =
            this.serializedObject.FindProperty("riderSeatOffset");
        SerializedProperty pelvisPitchProperty =
            this.serializedObject.FindProperty("riderPelvisPitch");
        SerializedProperty lowerBackCurlProperty =
            this.serializedObject.FindProperty("riderLowerBackCurl");
        SerializedProperty chestCurlProperty =
            this.serializedObject.FindProperty("riderChestCurl");
        SerializedProperty upperChestCurlProperty =
            this.serializedObject.FindProperty("riderUpperChestCurl");
        SerializedProperty neckLiftProperty =
            this.serializedObject.FindProperty("riderNeckLift");
        SerializedProperty headLiftProperty =
            this.serializedObject.FindProperty("riderHeadLift");
        SerializedProperty spinePositionOffsetProperty =
            this.serializedObject.FindProperty("riderSpinePositionOffset");
        SerializedProperty spineRotationOffsetProperty =
            this.serializedObject.FindProperty("riderSpineRotationOffset");
        SerializedProperty airborneLiftProperty =
            this.serializedObject.FindProperty("riderAirborneLift");
        SerializedProperty airborneLiftDelayProperty =
            this.serializedObject.FindProperty("riderAirborneLiftDelay");
        SerializedProperty airborneLiftSmoothTimeProperty =
            this.serializedObject.FindProperty("riderAirborneLiftSmoothTime");
        SerializedProperty leftHandPositionWeightProperty =
            this.serializedObject.FindProperty("leftHandIKWeight");
        SerializedProperty rightHandPositionWeightProperty =
            this.serializedObject.FindProperty("rightHandIKWeight");
        SerializedProperty handRotationWeightProperty =
            this.serializedObject.FindProperty("handIKRotationWeight");
        EditorGUILayout.LabelField("Rider Fit Tool", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(fitStyleProperty, new GUIContent("Ergonomic Style"));
        EditorGUILayout.PropertyField(sourceClipProperty, new GUIContent("Source Bike Idle"));
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(
                generatedClipProperty,
                new GUIContent("Generated Per-Bike Pose")
            );
        }
        EditorGUILayout.PropertyField(seatOffsetProperty, new GUIContent("Seat Offset"));

        RvrBikeRiderTriangle triangle = RvrBikeRiderFitClipTool.Analyze(entry);
        if (triangle.IsValid)
        {
            EditorGUILayout.LabelField("Rider Triangle", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                "Seat → Grips / Feet",
                $"{triangle.SeatToGrip:0.00} m / {triangle.SeatToFoot:0.00} m"
            );
            EditorGUILayout.LabelField(
                "Grip Reach / Drop / Rearset",
                $"{triangle.GripReach:0.00} / {triangle.GripDrop:0.00} / " +
                $"{triangle.FootRearset:0.00} m"
            );
            EditorGUILayout.LabelField("Suggested Style", triangle.SuggestedStyle.ToString());
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Assign Seat, both Hand targets and both Foot targets to analyze the rider triangle.",
                MessageType.Warning
            );
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Upright")) ApplyFitPreset(entry, RiderFitStyle.Upright);
        if (GUILayout.Button("Standard")) ApplyFitPreset(entry, RiderFitStyle.Standard);
        if (GUILayout.Button("Sport")) ApplyFitPreset(entry, RiderFitStyle.Sport);
        if (GUILayout.Button("Racing")) ApplyFitPreset(entry, RiderFitStyle.Racing);
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(!triangle.IsValid))
        {
            if (GUILayout.Button("Auto Fit From Seat / Grips / Footpegs"))
            {
                this.serializedObject.ApplyModifiedProperties();
                RvrBikeRiderFitClipTool.AutoFitFromTriangle(entry);
                UpdateLivePose(entry);
                this.serializedObject.Update();
            }
        }

        EditorGUILayout.Space(3f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Generated Humanoid Pose", EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(
            Application.isPlaying ? "LIVE SAFE" : "EDIT",
            EditorStyles.miniBoldLabel,
            GUILayout.Width(Application.isPlaying ? 60f : 30f)
        );
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(pelvisPitchProperty, new GUIContent("Pelvis Pitch"));
        EditorGUILayout.PropertyField(lowerBackCurlProperty, new GUIContent("Lower Back Curl"));
        EditorGUILayout.PropertyField(chestCurlProperty, new GUIContent("Chest Curl"));
        EditorGUILayout.PropertyField(upperChestCurlProperty, new GUIContent("Upper Chest Curl"));
        EditorGUILayout.PropertyField(neckLiftProperty, new GUIContent("Neck Road Lift"));
        EditorGUILayout.PropertyField(headLiftProperty, new GUIContent("Head Road Lift"));
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Manual Spine Bone", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            spinePositionOffsetProperty,
            new GUIContent("Spine Local Position")
        );
        EditorGUILayout.PropertyField(
            spineRotationOffsetProperty,
            new GUIContent("Spine Local Rotation")
        );
        bool livePoseChanged = EditorGUI.EndChangeCheck();

        if (livePoseChanged)
        {
            fitStyleProperty.enumValueIndex = (int)RiderFitStyle.Custom;
            this.serializedObject.ApplyModifiedProperties();
            UpdateLivePose(entry);
            this.serializedObject.Update();
        }

        if (GUILayout.Button("Reset Manual Spine Position / Rotation"))
        {
            spinePositionOffsetProperty.vector3Value = Vector3.zero;
            spineRotationOffsetProperty.vector3Value = Vector3.zero;
            fitStyleProperty.enumValueIndex = (int)RiderFitStyle.Custom;
            this.serializedObject.ApplyModifiedProperties();
            UpdateLivePose(entry);
            this.serializedObject.Update();
        }

        if (entry.gameObject.name.StartsWith("Bike_01", StringComparison.OrdinalIgnoreCase))
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button(
                    "Apply Bike 01 Defaults + Targets To Bikes 08 / 09 / 10"
                ))
                {
                    this.serializedObject.ApplyModifiedProperties();
                    RvrBikeRiderFitClipTool.ApplyBike01RiderFitToSportBikes(entry);
                    this.serializedObject.Update();
                }
            }
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Rebuild / Save Per-Bike Rider Pose"))
            {
                this.serializedObject.ApplyModifiedProperties();
                RvrBikeRiderFitClipTool.GeneratePoseClip(entry);
                if (entry.gameObject.name.StartsWith(
                    "Bike_01",
                    StringComparison.OrdinalIgnoreCase
                ))
                {
                    RvrBikeRiderFitClipTool.ApplyBike01RiderFitToSportBikes(entry);
                }
                this.serializedObject.Update();
            }
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Safe live preview is memory-only. Stop Play Mode before rebuilding the " +
                "AnimationClip asset.",
                MessageType.Info
            );
        }

        EditorGUILayout.HelpBox(
            "The generated clip builds the Humanoid pose. Manual Spine Position/Rotation is " +
            "applied to the Humanoid Spine bone afterwards, then Hand/Foot IK is solved. " +
            "The rider parent remains fixed at the seat during normal riding.",
            MessageType.Info
        );

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Airborne Rider Lift", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            airborneLiftProperty,
            new GUIContent("Hip / Parent Lift")
        );
        EditorGUILayout.PropertyField(
            airborneLiftDelayProperty,
            new GUIContent("Airborne Delay")
        );
        EditorGUILayout.PropertyField(
            airborneLiftSmoothTimeProperty,
            new GUIContent("Lift Smooth Time")
        );
        EditorGUILayout.HelpBox(
            "When both wheels leave the ground, the seated rider root moves upward in " +
            "bike-local space. Grip and foot IK remain attached to the bike.",
            MessageType.None
        );

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Handlebar Reach & Hand IK", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            leftHandPositionWeightProperty,
            new GUIContent("Left Hand Position Weight")
        );
        EditorGUILayout.PropertyField(
            rightHandPositionWeightProperty,
            new GUIContent("Right Hand Position Weight")
        );
        EditorGUILayout.PropertyField(
            handRotationWeightProperty,
            new GUIContent("Hand Rotation Weight")
        );
        EditorGUILayout.HelpBox(
            "Keep both Position Weights at 1 to pin the hands to the grip targets. " +
            "Use Rotation Weight to relax or tighten wrist orientation.",
            MessageType.None
        );

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Bike Entry & IK", EditorStyles.boldLabel);
        DrawPropertiesExcluding(
            this.serializedObject,
            "riderFitStyle",
            "riderPoseSourceClip",
            "riderPoseClip",
            "riderSeatOffset",
            "riderPelvisPitch",
            "riderLowerBackCurl",
            "riderChestCurl",
            "riderUpperChestCurl",
            "riderNeckLift",
            "riderHeadLift",
            "riderSpinePositionOffset",
            "riderSpineRotationOffset",
            "riderAirborneLift",
            "riderAirborneLiftDelay",
            "riderAirborneLiftSmoothTime",
            "riderForwardLean",
            "riderLeanResponse",
            "riderHipsLeanWeight",
            "riderSpineLeanWeight",
            "riderChestLeanWeight",
            "riderUpperChestLeanWeight",
            "leftHandIKWeight",
            "rightHandIKWeight",
            "handIKRotationWeight"
        );
        this.serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Franklin IK Mirror", EditorStyles.boldLabel);
        bool enabled = EditorGUILayout.ToggleLeft(
            "Auto mirror selected Left/Right target",
            RvrBikeIkMirrorTool.Enabled
        );
        if (enabled != RvrBikeIkMirrorTool.Enabled)
        {
            RvrBikeIkMirrorTool.Enabled = enabled;
        }

        EditorGUILayout.HelpBox(
            "Select and move or rotate an assigned entry/hand/foot target. Its opposite side " +
            "is mirrored across the bike's local X center plane.",
            MessageType.Info
        );

        DrawMirrorRow(
            "Entry",
            entry,
            entry.entryStandingPoint,
            entry.mirroredEntryStandingPoint
        );
        DrawMirrorRow(
            "Hands",
            entry,
            entry.steeringWheelLeftHandTarget,
            entry.steeringWheelRightHandTarget
        );
        DrawMirrorRow("Feet", entry, entry.leftFootTarget, entry.rightFootTarget);
    }

    private void ApplyFitPreset(BikeEntry entry, RiderFitStyle style)
    {
        this.serializedObject.ApplyModifiedProperties();
        RvrBikeRiderFitClipTool.ApplyPreset(entry, style);
        UpdateLivePose(entry);
        this.serializedObject.Update();
    }

    private void UpdateLivePose(BikeEntry entry)
    {
        EditorUtility.SetDirty(entry);
        if (Application.isPlaying)
        {
            entry.ApplyRiderHandIK();
            entry.SetLiveRiderPosePreview(true);
            EditorApplication.QueuePlayerLoopUpdate();
        }
        SceneView.RepaintAll();
        this.Repaint();
    }

    private static void DrawMirrorRow(
        string label,
        BikeEntry entry,
        Transform left,
        Transform right)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        using (new EditorGUI.DisabledScope(left == null || right == null))
        {
            if (GUILayout.Button("Left → Right"))
                RvrBikeIkMirrorTool.MirrorFrom(entry, left, true);
            if (GUILayout.Button("Right → Left"))
                RvrBikeIkMirrorTool.MirrorFrom(entry, right, true);
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif
