using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal readonly struct RvrBikeRiderTriangle
{
    public readonly bool IsValid;
    public readonly float GripReach;
    public readonly float GripDrop;
    public readonly float FootRearset;
    public readonly float SeatToGrip;
    public readonly float SeatToFoot;
    public readonly RiderFitStyle SuggestedStyle;

    public RvrBikeRiderTriangle(
        bool isValid,
        float gripReach,
        float gripDrop,
        float footRearset,
        float seatToGrip,
        float seatToFoot,
        RiderFitStyle suggestedStyle
    )
    {
        IsValid = isValid;
        GripReach = gripReach;
        GripDrop = gripDrop;
        FootRearset = footRearset;
        SeatToGrip = seatToGrip;
        SeatToFoot = seatToFoot;
        SuggestedStyle = suggestedStyle;
    }
}

internal static class RvrBikeRiderFitClipTool
{
    private const string SOURCE_CLIP_PATH =
        "Assets/Ash Assets/Vehicle Integration/Vehicles/Bike/Animations/Character_Idle_Bike.anim";
    private const string BIKE_FOLDER =
        "Assets/Ash Assets/Arcade Bike Physics Pro/Prefabs/Bikes";
    private const string OUTPUT_FOLDER =
        "Assets/Ash Assets/Arcade Bike Physics Pro/Animations/RiderPoses/Generated";
    private static readonly string[] SHARED_SPORT_FIT_TARGETS =
    {
        $"{BIKE_FOLDER}/Bike_08.prefab",
        $"{BIKE_FOLDER}/Bike_09.prefab",
        $"{BIKE_FOLDER}/Bike_10.prefab"
    };
    private static readonly string[] NON_SHARED_GROUND_LEFT_FOOT_TARGETS =
    {
        $"{BIKE_FOLDER}/Bike_02.prefab",
        $"{BIKE_FOLDER}/Bike_03.prefab",
        $"{BIKE_FOLDER}/Bike_04.prefab",
        $"{BIKE_FOLDER}/Bike_05.prefab",
        $"{BIKE_FOLDER}/Bike_06.prefab",
        $"{BIKE_FOLDER}/Bike_07.prefab"
    };
    [MenuItem("Tools/Franklin/RVR Bikes/Apply Bike 01 Defaults + Targets To Bikes 08-10")]
    public static void ApplyBike01RiderFitToSportBikes()
    {
        ApplyBike01RiderFitToSportBikes(null);
    }

    public static void ApplyBike01RiderFitToSportBikes(BikeEntry _)
    {
        string canonicalPath = $"{BIKE_FOLDER}/Bike_01.prefab";
        GameObject canonicalRoot = PrefabUtility.LoadPrefabContents(canonicalPath);
        try
        {
            BikeEntry canonicalTargets = canonicalRoot.GetComponent<BikeEntry>();
            if (canonicalTargets == null)
            {
                Debug.LogError("RVR Bike Rider Fit: canonical Bike 01 prefab has no BikeEntry.");
                return;
            }

            int updatedCount = 0;
            int verifiedTargetCount = 0;
            foreach (string prefabPath in SHARED_SPORT_FIT_TARGETS)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    BikeEntry target = root.GetComponent<BikeEntry>();
                    if (target == null)
                    {
                        Debug.LogWarning($"RVR Bike Rider Fit: BikeEntry missing on {prefabPath}.");
                        continue;
                    }

                    CopySharedRiderFit(canonicalTargets, canonicalTargets, target);
                    if (SharedTargetsMatchInBikeSpace(canonicalTargets, target))
                    {
                        verifiedTargetCount++;
                    }
                    else
                    {
                        Debug.LogError(
                            $"RVR Bike Rider Fit: target pose verification failed for {prefabPath}."
                        );
                    }
                    target.riderPoseClip = canonicalTargets.riderPoseClip;
                    EditorUtility.SetDirty(target);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    updatedCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            int additionalGroundLeftFootCount = CopyGroundLeftFootToPrefabs(
                canonicalTargets,
                NON_SHARED_GROUND_LEFT_FOOT_TARGETS
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"RVR Bike Rider Fit: applied the canonical Bike 01 shared profile, Entry, " +
                $"Seat, Grip, Footpeg and Ground Left Foot targets to " +
                $"{updatedCount} shared Sport bikes (08-10); " +
                $"verified target poses on {verifiedTargetCount}/{updatedCount}; " +
                $"also updated Ground Left Foot on {additionalGroundLeftFootCount} bikes (02-07)."
            );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(canonicalRoot);
        }
    }

    private static int CopyGroundLeftFootToPrefabs(
        BikeEntry canonical,
        string[] prefabPaths
    )
    {
        int updatedCount = 0;
        foreach (string prefabPath in prefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                BikeEntry target = root.GetComponent<BikeEntry>();
                if (target == null || target.groundLeftFootTarget == null)
                {
                    Debug.LogWarning(
                        $"RVR Bike Rider Fit: Ground Left Foot target missing on {prefabPath}."
                    );
                    continue;
                }

                CopyTargetPoseInBikeSpace(
                    canonical.transform,
                    target.transform,
                    canonical.groundLeftFootTarget,
                    target.groundLeftFootTarget,
                    "Ground Left Foot"
                );
                EditorUtility.SetDirty(target);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                updatedCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return updatedCount;
    }

    [MenuItem("Tools/Franklin/RVR Bikes/Build Rider Fit Poses")]
    public static void BuildAllBikePoses()
    {
        EnsureOutputFolder();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { BIKE_FOLDER });
        int builtCount = 0;

        foreach (string guid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(
                Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'),
                BIKE_FOLDER,
                StringComparison.Ordinal
            )) continue;

            string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            if (!prefabName.StartsWith("Bike_", StringComparison.OrdinalIgnoreCase)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                BikeEntry entry = root.GetComponent<BikeEntry>();
                if (entry == null) continue;

                if (entry.riderPoseSourceClip == null)
                {
                    entry.riderPoseSourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        SOURCE_CLIP_PATH
                    );
                }

                if (entry.riderPoseClip == null)
                {
                    RiderFitStyle initialStyle = prefabName.IndexOf(
                        "Sport",
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                        ? RiderFitStyle.Sport
                        : RiderFitStyle.Standard;
                    ApplyPreset(entry, initialStyle);
                }

                GeneratePoseClip(entry, prefabName);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                builtCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"RVR Bike Rider Fit: built {builtCount} isolated per-bike pose clips.");
    }

    public static void ApplyPreset(BikeEntry entry, RiderFitStyle style)
    {
        if (entry == null) return;

        Undo.RecordObject(entry, $"Apply {style} Rider Fit");
        entry.riderFitStyle = style;

        switch (style)
        {
            case RiderFitStyle.Upright:
                SetPose(entry, 0f, 0.08f, 0.04f, 0.02f, 0.10f, 0.12f);
                break;
            case RiderFitStyle.Standard:
                SetPose(entry, 8f, 0.16f, 0.10f, 0.04f, 0.16f, 0.18f);
                break;
            case RiderFitStyle.Sport:
                SetPose(entry, 20f, 0.28f, 0.18f, 0.07f, 0.28f, 0.32f);
                break;
            case RiderFitStyle.Racing:
                SetPose(entry, 32f, 0.40f, 0.28f, 0.12f, 0.45f, 0.52f);
                break;
            case RiderFitStyle.Custom:
                break;
        }

        EditorUtility.SetDirty(entry);
    }

    public static void AutoFitFromTriangle(BikeEntry entry)
    {
        RvrBikeRiderTriangle triangle = Analyze(entry);
        if (!triangle.IsValid) return;
        ApplyPreset(entry, triangle.SuggestedStyle);
    }

    public static RvrBikeRiderTriangle Analyze(BikeEntry entry)
    {
        if (entry == null || entry.entryParent == null ||
            entry.steeringWheelLeftHandTarget == null ||
            entry.steeringWheelRightHandTarget == null ||
            entry.leftFootTarget == null || entry.rightFootTarget == null)
        {
            return default;
        }

        Transform frame = entry.transform;
        Vector3 seat = frame.InverseTransformPoint(entry.entryParent.position);
        Vector3 grips = frame.InverseTransformPoint(
            (entry.steeringWheelLeftHandTarget.position +
             entry.steeringWheelRightHandTarget.position) * 0.5f
        );
        Vector3 feet = frame.InverseTransformPoint(
            (entry.leftFootTarget.position + entry.rightFootTarget.position) * 0.5f
        );

        float gripReach = grips.z - seat.z;
        float gripDrop = seat.y - grips.y;
        float footRearset = seat.z - feet.z;
        float seatToGrip = Vector3.Distance(seat, grips);
        float seatToFoot = Vector3.Distance(seat, feet);

        RiderFitStyle suggestedStyle;
        if (gripDrop > 0.18f || gripReach > 0.82f || footRearset > 0.38f)
            suggestedStyle = RiderFitStyle.Racing;
        else if (gripDrop > 0.03f || gripReach > 0.56f || footRearset > 0.20f)
            suggestedStyle = RiderFitStyle.Sport;
        else if (grips.y > seat.y + 0.16f && footRearset < 0.05f)
            suggestedStyle = RiderFitStyle.Upright;
        else
            suggestedStyle = RiderFitStyle.Standard;

        return new RvrBikeRiderTriangle(
            true,
            gripReach,
            gripDrop,
            footRearset,
            seatToGrip,
            seatToFoot,
            suggestedStyle
        );
    }

    public static AnimationClip GeneratePoseClip(BikeEntry entry)
    {
        return GeneratePoseClip(entry, entry != null ? entry.gameObject.name : "Bike", true);
    }

    public static AnimationClip GeneratePoseClip(BikeEntry entry, string assetBaseName)
    {
        return GeneratePoseClip(entry, assetBaseName, true);
    }

    public static AnimationClip GeneratePoseClip(BikeEntry entry, bool saveAssets)
    {
        return GeneratePoseClip(
            entry,
            entry != null ? entry.gameObject.name : "Bike",
            saveAssets
        );
    }

    public static AnimationClip GeneratePoseClip(
        BikeEntry entry,
        string assetBaseName,
        bool saveAssets
    )
    {
        if (entry == null) return null;
        EnsureOutputFolder();

        AnimationClip source = entry.riderPoseSourceClip != null
            ? entry.riderPoseSourceClip
            : AssetDatabase.LoadAssetAtPath<AnimationClip>(SOURCE_CLIP_PATH);
        if (source == null)
        {
            Debug.LogError("RVR Bike Rider Fit: source bike idle clip is missing.", entry);
            return null;
        }

        string safeName = SanitizeFileName(assetBaseName);
        string clipPath = $"{OUTPUT_FOLDER}/{safeName}_RiderFit.anim";
        AnimationClip generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (generated == null)
        {
            generated = UnityEngine.Object.Instantiate(source);
            generated.name = $"{safeName}_RiderFit";
            AssetDatabase.CreateAsset(generated, clipPath);
        }
        else
        {
            EditorUtility.CopySerialized(source, generated);
            generated.name = $"{safeName}_RiderFit";
        }

        float radians = entry.riderPelvisPitch * Mathf.Deg2Rad * 0.5f;
        SetConstantCurve(generated, "RootQ.x", Mathf.Sin(radians));
        SetConstantCurve(generated, "RootQ.y", 0f);
        SetConstantCurve(generated, "RootQ.z", 0f);
        SetConstantCurve(generated, "RootQ.w", Mathf.Cos(radians));
        SetConstantCurve(generated, "Spine Front-Back", -entry.riderLowerBackCurl);
        SetConstantCurve(generated, "Chest Front-Back", -entry.riderChestCurl);
        SetConstantCurve(generated, "UpperChest Front-Back", -entry.riderUpperChestCurl);
        SetConstantCurve(generated, "Neck Nod Down-Up", entry.riderNeckLift);
        SetConstantCurve(generated, "Head Nod Down-Up", entry.riderHeadLift);

        entry.riderPoseSourceClip = source;
        entry.riderPoseClip = generated;
        EditorUtility.SetDirty(generated);
        EditorUtility.SetDirty(entry);
        if (saveAssets) AssetDatabase.SaveAssets();
        return generated;
    }

    private static void SetPose(
        BikeEntry entry,
        float pelvisPitch,
        float lowerBack,
        float chest,
        float upperChest,
        float neck,
        float head
    )
    {
        entry.riderPelvisPitch = pelvisPitch;
        entry.riderLowerBackCurl = lowerBack;
        entry.riderChestCurl = chest;
        entry.riderUpperChestCurl = upperChest;
        entry.riderNeckLift = neck;
        entry.riderHeadLift = head;
    }

    private static void CopySharedRiderFit(
        BikeEntry settingsSource,
        BikeEntry targetPoseSource,
        BikeEntry target
    )
    {
        target.entryAnimation = settingsSource.entryAnimation;
        target.exitAnimation = settingsSource.exitAnimation;
        target.animationMask = settingsSource.animationMask;
        target.entryAnimationTransitionIn = settingsSource.entryAnimationTransitionIn;
        target.entryAnimationTransitionOut = settingsSource.entryAnimationTransitionOut;
        target.enterSeatPositionBlendStart = settingsSource.enterSeatPositionBlendStart;
        target.exitAnimationTransitionIn = settingsSource.exitAnimationTransitionIn;
        target.exitAnimationTransitionOut = settingsSource.exitAnimationTransitionOut;
        target.useRootMotion = settingsSource.useRootMotion;
        target.stoppedExitSpeedKph = settingsSource.stoppedExitSpeedKph;
        target.exitStopTimeout = settingsSource.exitStopTimeout;
        target.enterAlignmentDuration = settingsSource.enterAlignmentDuration;
        target.drivingState = settingsSource.drivingState;
        target.drivingStateLayer = settingsSource.drivingStateLayer;
        target.drivingStateTransitionIn = settingsSource.drivingStateTransitionIn;
        target.drivingStateTransitionOut = settingsSource.drivingStateTransitionOut;

        target.riderFitStyle = settingsSource.riderFitStyle;
        target.entrySideMode = settingsSource.entrySideMode;
        target.mirroredEntryAnimation = settingsSource.mirroredEntryAnimation;
        target.alignCharacterToStandingPoint = settingsSource.alignCharacterToStandingPoint;
        target.entryApproachStopDistance = settingsSource.entryApproachStopDistance;
        target.entryApproachTimeout = settingsSource.entryApproachTimeout;
        target.entryApproachAlignmentDuration = settingsSource.entryApproachAlignmentDuration;
        target.entryApproachMotionPriority = settingsSource.entryApproachMotionPriority;

        target.leftHandIKWeight = settingsSource.leftHandIKWeight;
        target.rightHandIKWeight = settingsSource.rightHandIKWeight;
        target.handIKRotationWeight = settingsSource.handIKRotationWeight;
        target.footSmoothingTime = settingsSource.footSmoothingTime;
        target.leftFootIKWeight = settingsSource.leftFootIKWeight;
        target.rightFootIKWeight = settingsSource.rightFootIKWeight;

        target.riderPoseSourceClip = settingsSource.riderPoseSourceClip;
        target.riderSeatOffset = settingsSource.riderSeatOffset;
        target.riderSpinePositionOffset = settingsSource.riderSpinePositionOffset;
        target.riderSpineRotationOffset = settingsSource.riderSpineRotationOffset;
        target.riderPelvisPitch = settingsSource.riderPelvisPitch;
        target.riderLowerBackCurl = settingsSource.riderLowerBackCurl;
        target.riderChestCurl = settingsSource.riderChestCurl;
        target.riderUpperChestCurl = settingsSource.riderUpperChestCurl;
        target.riderNeckLift = settingsSource.riderNeckLift;
        target.riderHeadLift = settingsSource.riderHeadLift;
        target.riderAirborneLift = settingsSource.riderAirborneLift;
        target.riderAirborneLiftDelay = settingsSource.riderAirborneLiftDelay;
        target.riderAirborneLiftSmoothTime = settingsSource.riderAirborneLiftSmoothTime;
        target.fallenBikeRecoveryAnimation = settingsSource.fallenBikeRecoveryAnimation;
        target.fallenBikeReachDuration = settingsSource.fallenBikeReachDuration;
        target.fallenBikeRaiseDuration = settingsSource.fallenBikeRaiseDuration;
        target.fallenBikeStandDistance = settingsSource.fallenBikeStandDistance;
        target.fallenBikeGripReach = settingsSource.fallenBikeGripReach;
        target.fallenBikeMaxAssistDistance = settingsSource.fallenBikeMaxAssistDistance;
        target.fallenBikeHandRotationWeight = settingsSource.fallenBikeHandRotationWeight;
        target.fallenBikeSpinePositionOffset = settingsSource.fallenBikeSpinePositionOffset;
        target.fallenBikeSpineRotationOffset = settingsSource.fallenBikeSpineRotationOffset;
        target.riderForwardLean = settingsSource.riderForwardLean;
        target.riderLeanResponse = settingsSource.riderLeanResponse;
        target.riderHipsLeanWeight = settingsSource.riderHipsLeanWeight;
        target.riderSpineLeanWeight = settingsSource.riderSpineLeanWeight;
        target.riderChestLeanWeight = settingsSource.riderChestLeanWeight;
        target.riderUpperChestLeanWeight = settingsSource.riderUpperChestLeanWeight;
        Transform sourceRoot = targetPoseSource.transform;
        Transform targetRoot = target.transform;
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.entryStandingPoint,
            target.entryStandingPoint,
            "Original Entry"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.mirroredEntryStandingPoint,
            target.mirroredEntryStandingPoint,
            "Mirrored Entry"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.fallenBikeBodyGripLeft,
            target.fallenBikeBodyGripLeft,
            "Fallen Body Grip Left"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.fallenBikeBodyGripRight,
            target.fallenBikeBodyGripRight,
            "Fallen Body Grip Right"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.entryParent,
            target.entryParent,
            "Seat"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.steeringWheelLeftHandTarget,
            target.steeringWheelLeftHandTarget,
            "Left Grip"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.steeringWheelRightHandTarget,
            target.steeringWheelRightHandTarget,
            "Right Grip"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.leftFootTarget,
            target.leftFootTarget,
            "Left Footpeg"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.rightFootTarget,
            target.rightFootTarget,
            "Right Footpeg"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            targetPoseSource.groundLeftFootTarget,
            target.groundLeftFootTarget,
            "Ground Left Foot"
        );
        EditorUtility.SetDirty(target);
    }

    private static void CopyTargetPoseInBikeSpace(
        Transform sourceRoot,
        Transform targetRoot,
        Transform sourceTarget,
        Transform targetTarget,
        string targetLabel
    )
    {
        if (sourceTarget == null || targetTarget == null)
        {
            Debug.LogWarning(
                $"RVR Bike Rider Fit: cannot copy {targetLabel}; " +
                $"source or destination target is missing."
            );
            return;
        }

        Vector3 bikeLocalPosition = sourceRoot.InverseTransformPoint(sourceTarget.position);
        Quaternion bikeLocalRotation =
            Quaternion.Inverse(sourceRoot.rotation) * sourceTarget.rotation;

        targetTarget.SetPositionAndRotation(
            targetRoot.TransformPoint(bikeLocalPosition),
            targetRoot.rotation * bikeLocalRotation
        );
        EditorUtility.SetDirty(targetTarget);
    }

    private static bool SharedTargetsMatchInBikeSpace(BikeEntry source, BikeEntry target)
    {
        Transform sourceRoot = source.transform;
        Transform targetRoot = target.transform;
        return TargetPoseMatchesInBikeSpace(
                sourceRoot,
                targetRoot,
                source.entryStandingPoint,
                target.entryStandingPoint
            ) &&
            TargetPoseMatchesInBikeSpace(
                sourceRoot,
                targetRoot,
                source.mirroredEntryStandingPoint,
                target.mirroredEntryStandingPoint
            ) &&
            TargetPoseMatchesInBikeSpace(
                sourceRoot,
                targetRoot,
                source.entryParent,
                target.entryParent
            ) &&
            TargetPoseMatchesInBikeSpace(
                sourceRoot,
                targetRoot,
                source.steeringWheelLeftHandTarget,
                target.steeringWheelLeftHandTarget
            ) &&
            TargetPoseMatchesInBikeSpace(
                sourceRoot,
                targetRoot,
                source.steeringWheelRightHandTarget,
                target.steeringWheelRightHandTarget
            ) &&
            TargetPoseMatchesInBikeSpace(
                sourceRoot,
                targetRoot,
                source.leftFootTarget,
                target.leftFootTarget
            ) &&
            TargetPoseMatchesInBikeSpace(
                sourceRoot,
                targetRoot,
                source.rightFootTarget,
                target.rightFootTarget
            ) &&
            TargetPoseMatchesInBikeSpace(
                sourceRoot,
                targetRoot,
                source.groundLeftFootTarget,
                target.groundLeftFootTarget
            );
    }

    private static bool TargetPoseMatchesInBikeSpace(
        Transform sourceRoot,
        Transform targetRoot,
        Transform sourceTarget,
        Transform targetTarget
    )
    {
        if (sourceTarget == null || targetTarget == null) return false;

        Vector3 sourcePosition = sourceRoot.InverseTransformPoint(sourceTarget.position);
        Vector3 targetPosition = targetRoot.InverseTransformPoint(targetTarget.position);
        Quaternion sourceRotation =
            Quaternion.Inverse(sourceRoot.rotation) * sourceTarget.rotation;
        Quaternion targetRotation =
            Quaternion.Inverse(targetRoot.rotation) * targetTarget.rotation;

        return Vector3.Distance(sourcePosition, targetPosition) <= 0.0001f &&
               Quaternion.Angle(sourceRotation, targetRotation) <= 0.01f;
    }

    private static void SetConstantCurve(AnimationClip clip, string propertyName, float value)
    {
        float duration = Mathf.Max(clip.length, 1f);
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, value, 0f, 0f),
            new Keyframe(duration, value, 0f, 0f)
        );
        curve.preWrapMode = WrapMode.Loop;
        curve.postWrapMode = WrapMode.Loop;
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
            string.Empty,
            typeof(Animator),
            propertyName
        );
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void EnsureOutputFolder()
    {
        EnsureFolder(
            "Assets/Ash Assets/Arcade Bike Physics Pro/Animations/RiderPoses",
            "Generated"
        );
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Bike";
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }
        return value.Replace("(Clone)", string.Empty).Trim();
    }
}
