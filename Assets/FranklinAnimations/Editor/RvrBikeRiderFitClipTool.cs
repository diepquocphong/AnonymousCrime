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
        "Assets/Ash Assets/Vehicle Integration/Animations/Vehicles/Character_Idle_Bike.anim";
    private const string BIKE_FOLDER =
        "Assets/Model/DQP_MotorBikePack_URP14/Generated/Prefabs/Bikes";
    private const string OUTPUT_FOLDER =
        "Assets/FranklinAnimations/Generated/BikeRiderPoses";
    private static readonly string[] SHARED_SPORT_FIT_TARGETS =
    {
        $"{BIKE_FOLDER}/Bike_08_Sport.prefab",
        $"{BIKE_FOLDER}/Bike_09_Sport.prefab",
        $"{BIKE_FOLDER}/Bike_10_Sport.prefab"
    };

    [MenuItem("Tools/Franklin/RVR Bikes/Apply Bike 01 Rider Fit + Targets To Bikes 08-10")]
    public static void ApplyBike01RiderFitToSportBikes()
    {
        BikeEntry source = FindSceneBike01();
        GameObject loadedSourceRoot = null;

        if (source == null)
        {
            string sourcePath = $"{BIKE_FOLDER}/Bike_01_Sport.prefab";
            loadedSourceRoot = PrefabUtility.LoadPrefabContents(sourcePath);
            source = loadedSourceRoot.GetComponent<BikeEntry>();
        }

        try
        {
            ApplyBike01RiderFitToSportBikes(source);
        }
        finally
        {
            if (loadedSourceRoot != null)
                PrefabUtility.UnloadPrefabContents(loadedSourceRoot);
        }
    }

    public static void ApplyBike01RiderFitToSportBikes(BikeEntry source)
    {
        if (source == null)
        {
            Debug.LogError("RVR Bike Rider Fit: Bike 01 source was not found.");
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

                CopySharedRiderFit(source, target);
                if (SharedTargetsMatchInBikeSpace(source, target))
                {
                    verifiedTargetCount++;
                }
                else
                {
                    Debug.LogError(
                        $"RVR Bike Rider Fit: target pose verification failed for {prefabPath}."
                    );
                }
                GeneratePoseClip(target, Path.GetFileNameWithoutExtension(prefabPath), false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                updatedCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"RVR Bike Rider Fit: applied Bike 01 pose, Spine, Entry, Seat, Grip and Footpeg " +
            $"targets to " +
            $"{updatedCount} shared Sport bikes (08-10); " +
            $"verified target poses on {verifiedTargetCount}/{updatedCount}."
        );
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

    private static BikeEntry FindSceneBike01()
    {
        if (Selection.activeGameObject != null)
        {
            BikeEntry selected = Selection.activeGameObject.GetComponentInParent<BikeEntry>();
            if (IsBike01(selected)) return selected;
        }

        BikeEntry[] entries = UnityEngine.Object.FindObjectsByType<BikeEntry>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        foreach (BikeEntry entry in entries)
        {
            if (IsBike01(entry)) return entry;
        }

        return null;
    }

    private static bool IsBike01(BikeEntry entry)
    {
        return entry != null && entry.gameObject.name.StartsWith(
            "Bike_01",
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static void CopySharedRiderFit(BikeEntry source, BikeEntry target)
    {
        target.riderFitStyle = source.riderFitStyle;
        target.entrySideMode = source.entrySideMode;
        target.mirroredEntryAnimation = source.mirroredEntryAnimation;
        target.enterSeatPositionBlendStart = source.enterSeatPositionBlendStart;
        target.riderPoseSourceClip = source.riderPoseSourceClip;
        target.riderSeatOffset = source.riderSeatOffset;
        target.riderSpinePositionOffset = source.riderSpinePositionOffset;
        target.riderSpineRotationOffset = source.riderSpineRotationOffset;
        target.riderPelvisPitch = source.riderPelvisPitch;
        target.riderLowerBackCurl = source.riderLowerBackCurl;
        target.riderChestCurl = source.riderChestCurl;
        target.riderUpperChestCurl = source.riderUpperChestCurl;
        target.riderNeckLift = source.riderNeckLift;
        target.riderHeadLift = source.riderHeadLift;
        target.riderAirborneLift = source.riderAirborneLift;
        target.riderAirborneLiftDelay = source.riderAirborneLiftDelay;
        target.riderAirborneLiftSmoothTime = source.riderAirborneLiftSmoothTime;
        target.fallenBikeRecoveryAnimation = source.fallenBikeRecoveryAnimation;
        target.fallenBikeReachDuration = source.fallenBikeReachDuration;
        target.fallenBikeRaiseDuration = source.fallenBikeRaiseDuration;
        target.fallenBikeStandDistance = source.fallenBikeStandDistance;
        target.fallenBikeHandRotationWeight = source.fallenBikeHandRotationWeight;
        target.fallenBikeSpinePositionOffset = source.fallenBikeSpinePositionOffset;
        target.fallenBikeSpineRotationOffset = source.fallenBikeSpineRotationOffset;
        target.leftHandIKWeight = source.leftHandIKWeight;
        target.rightHandIKWeight = source.rightHandIKWeight;
        target.handIKRotationWeight = source.handIKRotationWeight;
        target.leftFootIKWeight = source.leftFootIKWeight;
        target.rightFootIKWeight = source.rightFootIKWeight;

        Transform sourceRoot = source.transform;
        Transform targetRoot = target.transform;
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            source.entryStandingPoint,
            target.entryStandingPoint,
            "Original Entry"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            source.mirroredEntryStandingPoint,
            target.mirroredEntryStandingPoint,
            "Mirrored Entry"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            source.fallenBikeBodyGripLeft,
            target.fallenBikeBodyGripLeft,
            "Fallen Body Grip Left"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            source.fallenBikeBodyGripRight,
            target.fallenBikeBodyGripRight,
            "Fallen Body Grip Right"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            source.entryParent,
            target.entryParent,
            "Seat"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            source.steeringWheelLeftHandTarget,
            target.steeringWheelLeftHandTarget,
            "Left Grip"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            source.steeringWheelRightHandTarget,
            target.steeringWheelRightHandTarget,
            "Right Grip"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            source.leftFootTarget,
            target.leftFootTarget,
            "Left Footpeg"
        );
        CopyTargetPoseInBikeSpace(
            sourceRoot,
            targetRoot,
            source.rightFootTarget,
            target.rightFootTarget,
            "Right Footpeg"
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
        EnsureFolder("Assets/FranklinAnimations", "Generated");
        EnsureFolder("Assets/FranklinAnimations/Generated", "BikeRiderPoses");
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
