using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces the ultra villa's per-detail collision with gameplay-sized primitive proxies.
/// The 26-renderer visual asset is shared; only a second prefab is created.
/// </summary>
public static class VillaColliderOptimizer
{
    private const string UltraRoot = "Assets/Model/franklin-villa-estate-ultra-optimized";
    private const string SourcePrefab = UltraRoot + "/franklin-villa-estate-ultra-optimized.prefab";
    private const string TargetPrefab = UltraRoot + "/franklin-villa-estate-ultra-collider-optimized.prefab";
    private const string RampRootName = "FranklinVilla_Hierarchy_ExternalVehicleRamp_001";

    private static readonly HashSet<string> DynamicAnchorNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "FranklinVilla_Interactive_GateLeaf_Left_001",
        "FranklinVilla_Interactive_GateLeaf_Right_001",
        "FranklinVilla_Interactive_GaragePanel_1_001",
        "FranklinVilla_Interactive_GaragePanel_2_001",
        "FranklinVilla_Interactive_GaragePanel_3_001",
        "FranklinVilla_Interactive_GaragePanel_4_001",
        "FranklinVilla_Interactive_cua_chinh_Pivot_001",
        "FranklinVilla_Interactive_cua_ban_cong_Pivot_001",
        "FranklinVilla_Hierarchy_Group_003",
        "FranklinVilla_Hierarchy_Group_005"
    };

    private static readonly HashSet<string> StaticGlassAnchorNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "FranklinVilla_Hierarchy_Group_001",
        "FranklinVilla_Hierarchy_Group_006",
        "FranklinVilla_Hierarchy_Group_007",
        "FranklinVilla_Hierarchy_Group_008",
        "FranklinVilla_Hierarchy_Group_009",
        "FranklinVilla_Hierarchy_Group_009 (1)",
        "FranklinVilla_Hierarchy_Group_011"
    };

    private static readonly string[] FurniturePrefixes =
    {
        "FranklinVilla_Props_LivingRoomSofa_",
        "FranklinVilla_Props_InteriorTable_",
        "FranklinVilla_Props_BedroomBed_"
    };

    private static readonly HashSet<string> CoreDecorativeRemovals = new HashSet<string>(StringComparer.Ordinal)
    {
        "FranklinVilla_Geometry_blackMetal_Cylinder_005",
        "FranklinVilla_Geometry_blackMetal_Cylinder_006",
        "FranklinVilla_Geometry_blackMetal_Cylinder_007",
        "FranklinVilla_Geometry_blackMetal_Cylinder_008",
        "FranklinVilla_Geometry_blackMetal_Cylinder_015",
        "FranklinVilla_Geometry_concreteDark_Cylinder_001",
        "FranklinVilla_Geometry_emissiveWarm_Box_001",
        "FranklinVilla_Geometry_fabricDark_Cylinder_001"
    };

    public static void BuildAndAudit()
    {
        string reportPath = GetArgument("-villaColliderReport");
        if (string.IsNullOrWhiteSpace(reportPath))
            reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../VillaColliderOptimizationReport.json"));

        GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (sourceAsset == null) throw new FileNotFoundException("Ultra villa prefab missing", SourcePrefab);
        ColliderMetrics source = CaptureColliderMetrics(sourceAsset);
        VisualMetrics sourceVisual = CaptureVisualMetrics(sourceAsset);
        if (source.total != 461 || source.box != 455 || source.mesh != 6)
            throw new InvalidOperationException("Unexpected collider baseline: " + JsonUtility.ToJson(source));
        if (sourceVisual.renderers != 26 || sourceVisual.slots != 41 || sourceVisual.triangles != 16520)
            throw new InvalidOperationException("Unexpected ultra visual baseline: " + JsonUtility.ToJson(sourceVisual));

        if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab) != null && !AssetDatabase.DeleteAsset(TargetPrefab))
            throw new IOException("Could not replace exact derived prefab: " + TargetPrefab);

        GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefab);
        int aggregateProxyCount = 0;
        int removedColliderCount = 0;
        int rampMeshConversions = 0;
        try
        {
            root.name = Path.GetFileNameWithoutExtension(TargetPrefab);
            Collider[] originals = root.GetComponentsInChildren<Collider>(true);
            HashSet<Collider> aggregated = new HashSet<Collider>();
            Dictionary<string, AggregateGroup> groups = new Dictionary<string, AggregateGroup>(StringComparer.Ordinal);

            Transform staticProxyRoot = new GameObject("OPTIMIZED_COLLIDERS").transform;
            staticProxyRoot.SetParent(root.transform, false);

            // Collect every safe union first, while the original BoxCollider geometry is intact.
            foreach (BoxCollider box in originals.OfType<BoxCollider>())
            {
                Transform dynamicAnchor = FindAncestor(box.transform, root.transform, DynamicAnchorNames);
                if (dynamicAnchor != null)
                {
                    AddAggregate(groups, "DYNAMIC|" + RelativePath(dynamicAnchor, root.transform), dynamicAnchor, box, "Dynamic_" + dynamicAnchor.name);
                    aggregated.Add(box);
                    continue;
                }

                Transform glassAnchor = FindAncestor(box.transform, root.transform, StaticGlassAnchorNames);
                if (glassAnchor != null)
                {
                    string glassSegment = "";
                    // Group_007 contains the upstairs facade on both sides of the balcony
                    // door. One union across the full bank would silently close that doorway.
                    if (glassAnchor.name == "FranklinVilla_Hierarchy_Group_007")
                    {
                        Vector3 center = glassAnchor.InverseTransformPoint(box.transform.TransformPoint(box.center));
                        glassSegment = center.x < -1.96f ? "|LeftOfBalconyDoor" : "|RightOfBalconyDoor";
                    }
                    AddAggregate(
                        groups,
                        "GLASS|" + RelativePath(glassAnchor, root.transform) + glassSegment,
                        glassAnchor,
                        box,
                        "GlassBank_" + glassAnchor.name + glassSegment.Replace('|', '_'));
                    aggregated.Add(box);
                    continue;
                }

                Transform furnitureAnchor = FindAncestorByPrefix(box.transform, root.transform, FurniturePrefixes);
                if (furnitureAnchor != null)
                {
                    AddAggregate(groups, "FURNITURE|" + RelativePath(furnitureAnchor, root.transform), furnitureAnchor, box, "Furniture_" + furnitureAnchor.name);
                    aggregated.Add(box);
                    continue;
                }

                if (HasAncestorContaining(box.transform, "FranklinVilla_Props_KitchenSet_001"))
                {
                    if (box.name.IndexOf("KitchenIsland", StringComparison.Ordinal) >= 0)
                    {
                        AddAggregate(groups, "KITCHEN|Island", staticProxyRoot, box, "KitchenIsland");
                        aggregated.Add(box);
                    }
                    else if (box.name.IndexOf("KitchenRear", StringComparison.Ordinal) >= 0)
                    {
                        AddAggregate(groups, "KITCHEN|Rear", staticProxyRoot, box, "KitchenRearRun");
                        aggregated.Add(box);
                    }
                    continue;
                }

                if (box.name.IndexOf("GatePillar_Left", StringComparison.Ordinal) >= 0)
                {
                    AddAggregate(groups, "PILLAR|Left", staticProxyRoot, box, "GatePillar_Left");
                    aggregated.Add(box);
                    continue;
                }
                if (box.name.IndexOf("GatePillar_Right", StringComparison.Ordinal) >= 0)
                {
                    AddAggregate(groups, "PILLAR|Right", staticProxyRoot, box, "GatePillar_Right");
                    aggregated.Add(box);
                }
            }

            foreach (AggregateGroup group in groups.Values.OrderBy(value => value.key, StringComparer.Ordinal))
            {
                CreateUnionProxy(group);
                aggregateProxyCount++;
            }

            // The 16 detailed steps become one smooth walkable ramp. This also prevents a
            // CharacterController from snagging on 15 individual riser seams.
            List<BoxCollider> stairs = originals.OfType<BoxCollider>().Where(box => IsStair(box.name)).ToList();
            if (stairs.Count != 16) throw new InvalidOperationException("Expected 16 stair step colliders, found " + stairs.Count);
            CreateStairRampProxy(staticProxyRoot, root.transform, stairs);

            foreach (Collider collider in originals)
            {
                if (aggregated.Contains(collider) || (collider is BoxCollider && stairs.Contains((BoxCollider)collider)))
                {
                    UnityEngine.Object.DestroyImmediate(collider, true);
                    removedColliderCount++;
                    continue;
                }

                if (collider is MeshCollider rampMesh)
                {
                    if (!HasAncestor(rampMesh.transform, RampRootName))
                        throw new InvalidOperationException("Unexpected non-ramp MeshCollider: " + rampMesh.name);
                    ConvertCuboidMeshColliderToBox(rampMesh);
                    UnityEngine.Object.DestroyImmediate(rampMesh, true);
                    rampMeshConversions++;
                    continue;
                }

                BoxCollider box = collider as BoxCollider;
                if (box != null && ShouldRemove(box))
                {
                    UnityEngine.Object.DestroyImmediate(box, true);
                    removedColliderCount++;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, TargetPrefab, out bool saved);
            if (!saved) throw new IOException("Could not save collider-optimized prefab: " + TargetPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        GameObject targetAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab);
        if (targetAsset == null) throw new IOException("Derived collider prefab did not reload.");

        ColliderMetrics optimized = CaptureColliderMetrics(targetAsset);
        VisualMetrics optimizedVisual = CaptureVisualMetrics(targetAsset);
        ValidateResult(sourceVisual, optimizedVisual, optimized);
        ValidateDynamicAnchors(targetAsset);
        ValidateBalconyOpening(targetAsset);
        ValidateRamp(targetAsset);
        ValidateDependencies();
        RunPhysicsSmokeTest(targetAsset);

        Report report = new Report
        {
            passed = true,
            sourcePrefab = SourcePrefab,
            optimizedPrefab = TargetPrefab,
            source = source,
            optimized = optimized,
            sourceVisual = sourceVisual,
            optimizedVisual = optimizedVisual,
            aggregateProxyCount = aggregateProxyCount,
            removedSourceColliders = removedColliderCount,
            convertedRampMeshColliders = rampMeshConversions,
            reductionPercent = (source.total - optimized.total) * 100f / source.total,
            notes = new[]
            {
                "The existing 26-renderer ultra prefab is unchanged; this is a second prefab sharing its visual assets.",
                "Ten moving anchors each own one local BoxCollider proxy and a gravity-free kinematic Rigidbody.",
                "Eight static glass/window proxies replace 104 per-pane/frame colliders; the upstairs front bank is split around the balcony doorway.",
                "Furniture uses one coarse proxy per sofa, table and bed; kitchen retains island, rear run and refrigerator.",
                "Parking/helipad markings, glow meshes, foliage leaves, garage stripes/tracks and redundant boundary caps do not collide.",
                "Six 12-triangle cuboid ramp MeshColliders are represented by exact local-bounds BoxColliders.",
                "The pool water plane's invalid zero-thickness solid collider is removed; pool shell colliders remain.",
                "All visual metrics remain 26 renderers, 41 slots and 16,520 triangles."
            }
        };
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? ".");
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        Debug.Log(
            $"VILLA_COLLIDER_OPTIMIZATION_SUCCESS colliders={source.total}->{optimized.total} " +
            $"box={optimized.box} mesh={optimized.mesh} renderers={optimizedVisual.renderers} " +
            $"slots={optimizedVisual.slots} tris={optimizedVisual.triangles} report={reportPath}");
    }

    private static void AddAggregate(
        Dictionary<string, AggregateGroup> groups,
        string key,
        Transform parent,
        BoxCollider source,
        string label)
    {
        AggregateGroup group;
        if (!groups.TryGetValue(key, out group))
        {
            group = new AggregateGroup(key, parent, label);
            groups.Add(key, group);
        }
        if (group.parent != parent) throw new InvalidOperationException("Aggregate parent mismatch: " + key);
        group.sources.Add(source);
    }

    private static void CreateUnionProxy(AggregateGroup group)
    {
        Bounds bounds = BoundsInTarget(group.parent, group.sources);
        GameObject proxy = new GameObject("COL_" + Sanitize(group.label));
        proxy.transform.SetParent(group.parent, false);
        BoxCollider box = proxy.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = NonZeroSize(bounds.size);

        // Runtime-moving gates and doors should not force PhysX to rebuild static
        // broadphase entries. Keep each moving proxy on a lightweight kinematic body.
        if (group.key.StartsWith("DYNAMIC|", StringComparison.Ordinal))
        {
            Rigidbody body = group.parent.GetComponent<Rigidbody>();
            if (body == null) body = group.parent.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }

    private static Bounds BoundsInTarget(Transform target, IEnumerable<BoxCollider> sources)
    {
        bool initialized = false;
        Bounds result = default;
        foreach (BoxCollider source in sources)
        {
            Matrix4x4 matrix = target.worldToLocalMatrix * source.transform.localToWorldMatrix;
            Vector3 half = source.size * 0.5f;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 point = matrix.MultiplyPoint3x4(source.center + Vector3.Scale(half, new Vector3(x, y, z)));
                if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                else result.Encapsulate(point);
            }
        }
        if (!initialized) throw new InvalidOperationException("Cannot build an empty collider union.");
        return result;
    }

    private static void CreateStairRampProxy(Transform parent, Transform root, List<BoxCollider> steps)
    {
        List<Vector3> centers = steps
            .Select(step => root.InverseTransformPoint(step.transform.TransformPoint(step.center)))
            .OrderBy(center => center.z)
            .ToList();
        Vector3 delta = centers[centers.Count - 1] - centers[0];
        float angleX = -Mathf.Atan2(delta.y, delta.z) * Mathf.Rad2Deg;

        GameObject proxy = new GameObject("COL_InteriorStair_WalkRamp");
        proxy.transform.SetParent(parent, false);
        proxy.transform.localRotation = Quaternion.Euler(angleX, 0f, 0f);
        Bounds localBounds = BoundsInTarget(proxy.transform, steps);
        BoxCollider box = proxy.AddComponent<BoxCollider>();
        box.center = localBounds.center;
        box.size = NonZeroSize(localBounds.size);

        if (box.size.x < 2f || box.size.z < 6f || Mathf.Abs(angleX) < 20f || Mathf.Abs(angleX) > 40f)
            throw new InvalidOperationException($"Unexpected stair proxy: angle={angleX}, size={box.size}");
    }

    private static void ConvertCuboidMeshColliderToBox(MeshCollider source)
    {
        Mesh mesh = source.sharedMesh;
        if (mesh == null || MeshTriangleCount(mesh) != 12 || mesh.vertexCount != 24)
            throw new InvalidOperationException("Ramp mesh is no longer the audited 24-vertex/12-triangle cuboid: " + source.name);
        Bounds bounds = mesh.bounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        foreach (Vector3 vertex in mesh.vertices)
        {
            if ((!Nearly(vertex.x, min.x, 0.0001f) && !Nearly(vertex.x, max.x, 0.0001f)) ||
                (!Nearly(vertex.y, min.y, 0.0001f) && !Nearly(vertex.y, max.y, 0.0001f)) ||
                (!Nearly(vertex.z, min.z, 0.0001f) && !Nearly(vertex.z, max.z, 0.0001f)))
                throw new InvalidOperationException("Ramp mesh is not an axis-aligned cuboid in local space: " + source.name);
        }
        BoxCollider box = source.gameObject.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = NonZeroSize(bounds.size);
        box.isTrigger = source.isTrigger;
        box.sharedMaterial = source.sharedMaterial;
    }

    private static bool ShouldRemove(BoxCollider box)
    {
        string name = box.name;
        string path = FullHierarchyPath(box.transform);
        string lower = path.ToLowerInvariant();

        if (CoreDecorativeRemovals.Contains(name)) return true;
        if (lower.Contains("parkingline") || lower.Contains("helipadh_") || lower.Contains("helipadbeacon")) return true;
        if (lower.Contains("pool_water")) return true;

        if (lower.Contains("palmtree"))
            return name.IndexOf("woodDark_Cylinder", StringComparison.Ordinal) < 0;
        if (lower.Contains("decorativeplanter"))
            return name.IndexOf("concreteDark_Cylinder", StringComparison.Ordinal) < 0;

        if (lower.Contains("franklinvilla_lighting_") || lower.Contains("/franklinvilla_lighting_lighting_001/"))
        {
            return name != "FranklinVilla_Geometry_blackMetal_Cylinder_003" &&
                   name != "FranklinVilla_Geometry_blackMetal_Cylinder_004";
        }

        if (lower.Contains("garageSectionalDoor".ToLowerInvariant()) &&
            name.IndexOf("GarageTrack", StringComparison.Ordinal) >= 0) return true;

        // Fixed pool-facade trim remains visual. Only the two moving door groups retain collision.
        if (lower.Contains("poolfacadeslidingglassdoor") && FindDynamicAnchor(box.transform) == null) return true;

        if (lower.Contains("estateboundary") && name.IndexOf("_Cap_", StringComparison.Ordinal) >= 0) return true;

        if (lower.Contains("kitchenset") &&
            name.IndexOf("KitchenRefrigerator", StringComparison.Ordinal) < 0 &&
            name.IndexOf("KitchenIsland", StringComparison.Ordinal) < 0 &&
            name.IndexOf("KitchenRear", StringComparison.Ordinal) < 0) return true;

        if (name.IndexOf("_plant_", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("_plantLight_", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("_soil_", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("_Glow_", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        return false;
    }

    private static void ValidateResult(VisualMetrics sourceVisual, VisualMetrics optimizedVisual, ColliderMetrics optimized)
    {
        if (optimized.total > 115 || optimized.total < 95 || optimized.mesh != 0 || optimized.box != optimized.total)
            throw new InvalidOperationException("Collider budget failed: " + JsonUtility.ToJson(optimized));
        if (optimized.zeroSized != 0 || optimized.trigger != 0 || optimized.disabled != 0)
            throw new InvalidOperationException("Invalid output collider flags/dimensions: " + JsonUtility.ToJson(optimized));
        if (sourceVisual.renderers != optimizedVisual.renderers || sourceVisual.slots != optimizedVisual.slots ||
            sourceVisual.uniqueMeshes != optimizedVisual.uniqueMeshes || sourceVisual.vertices != optimizedVisual.vertices ||
            sourceVisual.triangles != optimizedVisual.triangles)
            throw new InvalidOperationException("Visual metrics changed while optimizing colliders.");
    }

    private static void ValidateDynamicAnchors(GameObject root)
    {
        foreach (string name in DynamicAnchorNames)
        {
            Transform anchor = FindByName(root.transform, name);
            if (anchor == null) throw new InvalidOperationException("Dynamic anchor missing: " + name);
            Collider[] colliders = anchor.GetComponentsInChildren<Collider>(true);
            if (colliders.Length != 1 || !(colliders[0] is BoxCollider) || colliders[0].transform.parent != anchor)
                throw new InvalidOperationException("Dynamic anchor must own exactly one direct proxy collider: " + name);
            Rigidbody body = anchor.GetComponent<Rigidbody>();
            if (body == null || !body.isKinematic || body.useGravity)
                throw new InvalidOperationException("Dynamic anchor must own a gravity-free kinematic Rigidbody: " + name);
            Transform proxy = colliders[0].transform;
            if (!Nearly(proxy.localPosition, Vector3.zero, 0.000001f) ||
                Quaternion.Angle(proxy.localRotation, Quaternion.identity) > 0.0001f ||
                !Nearly(proxy.localScale, Vector3.one, 0.000001f))
                throw new InvalidOperationException("Dynamic collider proxy transform is not zeroed: " + name);
        }
    }

    private static void ValidateRamp(GameObject root)
    {
        Transform ramp = FindByName(root.transform, RampRootName);
        if (ramp == null) throw new InvalidOperationException("Ramp hierarchy missing.");
        Collider[] colliders = ramp.GetComponentsInChildren<Collider>(true);
        if (colliders.Length != 15 || colliders.Any(collider => !(collider is BoxCollider)))
            throw new InvalidOperationException("Ramp must contain exactly 15 BoxColliders after conversion.");
    }

    private static void ValidateBalconyOpening(GameObject root)
    {
        Transform bank = FindByName(root.transform, "FranklinVilla_Hierarchy_Group_007");
        if (bank == null) throw new InvalidOperationException("Upper front glass bank is missing.");
        List<Bounds> sections = bank.GetComponentsInChildren<BoxCollider>(true)
            .Select(box => BoundsInTarget(bank, new[] { box }))
            .OrderBy(bounds => bounds.min.x)
            .ToList();
        if (sections.Count != 2)
            throw new InvalidOperationException("Upper front glass bank must be split into two collider sections.");
        float clearWidth = sections[1].min.x - sections[0].max.x;
        if (clearWidth < 1.35f || sections[0].max.x > -2.65f || sections[1].min.x < -1.25f)
            throw new InvalidOperationException(
                $"Balcony doorway was blocked by a glass proxy: gap={clearWidth}, " +
                $"leftMax={sections[0].max.x}, rightMin={sections[1].min.x}");
    }

    private static void ValidateDependencies()
    {
        string[] dependencies = AssetDatabase.GetDependencies(TargetPrefab, true);
        if (dependencies.Any(path => path.IndexOf("franklin-villa-estate-unity/", StringComparison.Ordinal) >= 0))
            throw new InvalidOperationException("Collider prefab leaked a dependency back to the original villa folder.");
    }

    private static void RunPhysicsSmokeTest(GameObject prefab)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Physics.SyncTransforms();
        Vector3[] surfacePoints =
        {
            new Vector3(-7.0f, 12f, -20f),    // drive court
            new Vector3(-11.2f, 12f, -3.1f),  // garage floor
            new Vector3(7.0f, 12f, 0f),       // ground interior
            new Vector3(-9.0f, 12f, 3.1f),    // upper bedroom floor
            new Vector3(0.7f, 15f, 0.5f),     // rooftop deck
            new Vector3(-27.1f, 12f, -12.9f), // ramp run 1
            new Vector3(-26.9f, 12f, -5.2f),  // ramp landing 1
            new Vector3(-21.8f, 12f, -5.3f),  // ramp run 2
            new Vector3(-16.9f, 14f, -5.2f),  // ramp landing 2
            new Vector3(-17.0f, 14f, -0.7f),  // ramp run 3
            new Vector3(-16.9f, 15f, 4.4f)    // roof landing
        };
        foreach (Vector3 point in surfacePoints)
        {
            RaycastHit hit;
            if (!Physics.Raycast(point, Vector3.down, out hit, 25f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Physics smoke test missed a required surface below " + point);
        }

        // Broad interior lines must remain open; this catches an accidental room-sized union box.
        Vector3[,] clearSegments =
        {
            { new Vector3(5.5f, 2.0f, -1.0f), new Vector3(9.0f, 2.0f, -1.0f) },
            { new Vector3(-4.0f, 2.1f, 1.0f), new Vector3(1.0f, 2.1f, 1.0f) },
            { new Vector3(-15.0f, 2.0f, -3.0f), new Vector3(-7.0f, 2.0f, -3.0f) }
        };
        for (int index = 0; index < clearSegments.GetLength(0); index++)
        {
            if (Physics.Linecast(clearSegments[index, 0], clearSegments[index, 1], ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("A coarse collider blocked an audited interior route " + index);
        }


        // Open the balcony door and verify a player-sized capsule can pass through the
        // intentional gap in the upper front glass bank.
        Transform balconyDoor = FindByName(instance.transform, "FranklinVilla_Interactive_cua_ban_cong_Pivot_001");
        if (balconyDoor == null) throw new InvalidOperationException("Balcony door pivot is missing from physics QA.");
        Quaternion closedRotation = balconyDoor.localRotation;
        balconyDoor.localRotation = closedRotation * Quaternion.Euler(0f, 100f, 0f);
        Physics.SyncTransforms();
        if (Physics.CheckCapsule(
                new Vector3(-1.96f, 4.90f, -4.32f),
                new Vector3(-1.96f, 6.20f, -4.32f),
                0.28f,
                ~0,
                QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("Player capsule cannot pass through the opened balcony door.");
        balconyDoor.localRotation = closedRotation;
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private static ColliderMetrics CaptureColliderMetrics(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        ColliderMetrics metrics = new ColliderMetrics
        {
            total = colliders.Length,
            box = colliders.OfType<BoxCollider>().Count(),
            mesh = colliders.OfType<MeshCollider>().Count(),
            trigger = colliders.Count(collider => collider.isTrigger),
            disabled = colliders.Count(collider => !collider.enabled)
        };
        foreach (BoxCollider box in colliders.OfType<BoxCollider>())
            if (box.size.x <= 0.0001f || box.size.y <= 0.0001f || box.size.z <= 0.0001f) metrics.zeroSized++;
        return metrics;
    }

    private static VisualMetrics CaptureVisualMetrics(GameObject root)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        HashSet<Mesh> meshes = new HashSet<Mesh>();
        int vertices = 0;
        int triangles = 0;
        foreach (MeshRenderer renderer in renderers)
        {
            Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || !meshes.Add(mesh)) continue;
            vertices += mesh.vertexCount;
            triangles += MeshTriangleCount(mesh);
        }
        return new VisualMetrics
        {
            renderers = renderers.Length,
            slots = renderers.Sum(renderer => renderer.sharedMaterials.Length),
            uniqueMeshes = meshes.Count,
            vertices = vertices,
            triangles = triangles
        };
    }

    private static int MeshTriangleCount(Mesh mesh)
    {
        int result = 0;
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            result += checked((int)(mesh.GetIndexCount(submesh) / 3));
        return result;
    }

    private static Transform FindDynamicAnchor(Transform transform)
    {
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
            if (DynamicAnchorNames.Contains(cursor.name)) return cursor;
        return null;
    }

    private static Transform FindAncestor(Transform transform, Transform root, HashSet<string> names)
    {
        for (Transform cursor = transform; cursor != null && cursor != root; cursor = cursor.parent)
            if (names.Contains(cursor.name)) return cursor;
        return null;
    }

    private static Transform FindAncestorByPrefix(Transform transform, Transform root, string[] prefixes)
    {
        for (Transform cursor = transform; cursor != null && cursor != root; cursor = cursor.parent)
            if (prefixes.Any(prefix => cursor.name.StartsWith(prefix, StringComparison.Ordinal))) return cursor;
        return null;
    }

    private static bool HasAncestor(Transform transform, string exactName)
    {
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
            if (cursor.name == exactName) return true;
        return false;
    }

    private static bool HasAncestorContaining(Transform transform, string token)
    {
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
            if (cursor.name.IndexOf(token, StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    private static Transform FindByName(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == name);
    }

    private static bool IsStair(string name)
    {
        const string prefix = "FranklinVilla_Geometry_concreteDark_Box_";
        if (!name.StartsWith(prefix, StringComparison.Ordinal)) return false;
        int value;
        return int.TryParse(name.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
               value >= 9 && value <= 24;
    }

    private static string RelativePath(Transform transform, Transform root)
    {
        if (transform == root) return "";
        Stack<string> names = new Stack<string>();
        for (Transform cursor = transform; cursor != null && cursor != root; cursor = cursor.parent)
            names.Push(cursor.name);
        return string.Join("/", names);
    }

    private static string FullHierarchyPath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent) names.Push(cursor.name);
        return "/" + string.Join("/", names) + "/";
    }

    private static Vector3 NonZeroSize(Vector3 size)
    {
        return new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));
    }

    private static bool Nearly(float left, float right, float epsilon)
    {
        return Mathf.Abs(left - right) <= epsilon;
    }

    private static bool Nearly(Vector3 left, Vector3 right, float epsilon)
    {
        return (left - right).sqrMagnitude <= epsilon * epsilon;
    }

    private static string Sanitize(string value)
    {
        return new string(value.Select(character =>
            char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_').ToArray());
    }

    private static string GetArgument(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
            if (args[index] == key) return args[index + 1];
        return null;
    }

    private sealed class AggregateGroup
    {
        public readonly string key;
        public readonly Transform parent;
        public readonly string label;
        public readonly List<BoxCollider> sources = new List<BoxCollider>();
        public AggregateGroup(string key, Transform parent, string label)
        {
            this.key = key;
            this.parent = parent;
            this.label = label;
        }
    }

    [Serializable]
    public sealed class ColliderMetrics
    {
        public int total;
        public int box;
        public int mesh;
        public int trigger;
        public int disabled;
        public int zeroSized;
    }

    [Serializable]
    public sealed class VisualMetrics
    {
        public int renderers;
        public int slots;
        public int uniqueMeshes;
        public int vertices;
        public int triangles;
    }

    [Serializable]
    private sealed class Report
    {
        public bool passed;
        public string sourcePrefab;
        public string optimizedPrefab;
        public ColliderMetrics source;
        public ColliderMetrics optimized;
        public VisualMetrics sourceVisual;
        public VisualMetrics optimizedVisual;
        public int aggregateProxyCount;
        public int removedSourceColliders;
        public int convertedRampMeshColliders;
        public float reductionPercent;
        public string[] notes;
    }
}
