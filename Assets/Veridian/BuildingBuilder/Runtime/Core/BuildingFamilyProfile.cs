using UnityEngine;
using System.Collections.Generic;

namespace Veridian.BuildingSystem.Runtime
{
    // =========================================================================
    // PHASE 1: PROBABILITY DATA STRUCTURES
    // =========================================================================

    [System.Serializable]
    public struct RandomFloatRange
    {
        public float min;
        public float max;
        public RandomFloatRange(float min, float max) { this.min = min; this.max = max; }
        public float Roll() => Random.Range(min, max);
    }

    [System.Serializable]
    public struct WeightedIntOption
    {
        public int value;
        public float weight;
        public WeightedIntOption(int value, float weight) { this.value = value; this.weight = weight; }
    }

    [System.Serializable]
    public class WeightedInt
    {
        public List<WeightedIntOption> options = new List<WeightedIntOption>();

        public WeightedInt(params WeightedIntOption[] defaultOptions)
        {
            options.AddRange(defaultOptions);
        }

        public int Roll()
        {
            if (options == null || options.Count == 0) return 0;

            float total = 0f;
            foreach (var opt in options) total += opt.weight;
            if (total <= 0f) return options[0].value;

            float roll = Random.Range(0f, total);
            float cumulative = 0f;
            foreach (var opt in options)
            {
                cumulative += opt.weight;
                if (roll <= cumulative) return opt.value;
            }
            return options[options.Count - 1].value;
        }
    }

    // =========================================================================
    // PHASE 2: CURATED MATERIAL PALETTE SYSTEM
    // =========================================================================

    [System.Serializable]
    public class MaterialPalette
    {
        public string paletteName = "New Architectural Style";
        [Range(0.1f, 10f)] public float selectionWeight = 1f;

        [Header("Materials")]
        public List<Material> wallMaterials = new List<Material>();
        public List<Material> roofMaterials = new List<Material>();
        public List<Material> glassMaterials = new List<Material>();
        public List<Material> masonryMaterials = new List<Material>();
        public List<Material> accentMaterials = new List<Material>();
        public List<Material> frameMaterials = new List<Material>();
        public List<Material> doorMaterials = new List<Material>();

        [Header("Fallback Colors (If materials missing)")]
        public Color wallFallbackColor = new Color(0.5f, 0.55f, 0.45f);
        public Color roofFallbackColor = new Color(0.2f, 0.2f, 0.22f);
        public Color glassFallbackColor = new Color(0.1f, 0.15f, 0.2f);
        public Color masonryFallbackColor = new Color(0.55f, 0.3f, 0.25f);
        public Color accentFallbackColor = new Color(0.35f, 0.35f, 0.38f);
        public Color frameFallbackColor = new Color(0.4f, 0.45f, 0.55f);
        public Color doorFallbackColor = new Color(0.35f, 0.2f, 0.1f);

        public void ApplyTo(BuildingProfile profile)
        {
            profile.wallMaterial = GetRandom(wallMaterials) ?? profile.wallMaterial;
            profile.roofMaterial = GetRandom(roofMaterials) ?? profile.roofMaterial;
            profile.glassMaterial = GetRandom(glassMaterials) ?? profile.glassMaterial;
            profile.masonryMaterial = GetRandom(masonryMaterials) ?? profile.masonryMaterial;
            profile.accentMaterial = GetRandom(accentMaterials) ?? profile.accentMaterial;
            profile.frameMaterial = GetRandom(frameMaterials) ?? profile.frameMaterial;
            profile.doorMaterial = GetRandom(doorMaterials) ?? profile.doorMaterial;

            profile.wallFallbackColor = wallFallbackColor;
            profile.roofFallbackColor = roofFallbackColor;
            profile.glassFallbackColor = glassFallbackColor;
            profile.masonryFallbackColor = masonryFallbackColor;
            profile.accentFallbackColor = accentFallbackColor;
            profile.frameFallbackColor = frameFallbackColor;
            profile.doorFallbackColor = doorFallbackColor;
        }

        private Material GetRandom(List<Material> list)
        {
            if (list == null || list.Count == 0) return null;
            return list[Random.Range(0, list.Count)];
        }
    }

    // =========================================================================
    // PHASE 3: THE GENERATION API
    // =========================================================================

    [CreateAssetMenu(fileName = "NewBuildingFamily", menuName = "Procedural/Building Family Profile")]
    public class BuildingFamilyProfile : BuildingProfileBase
    {
        [Header("Curated Material Palettes")]
        [Tooltip("The engine rolls to select ONE palette, preventing visual clashing.")]
        public List<MaterialPalette> palettes = new List<MaterialPalette> { new MaterialPalette() };

        [Header("Dimensions (Min/Max)")]
        public RandomFloatRange widthRange = new RandomFloatRange(8f, 15f);
        public RandomFloatRange lengthRange = new RandomFloatRange(10f, 20f);
        public RandomFloatRange wallHeightRange = new RandomFloatRange(2.8f, 3.5f);

        [Header("Dynamic Wing (Probabilities & Ranges)")]
        [Range(0f, 1f)] public float addWingProbability = 0.35f;
        public RandomFloatRange wingWidthRange = new RandomFloatRange(6f, 10f);
        public RandomFloatRange wingLengthRange = new RandomFloatRange(6f, 12f);
        public RandomFloatRange wingOffsetRange = new RandomFloatRange(-1f, 1f);

        [Header("Mirrored Secondary Building")]
        [Range(0f, 1f)] public float addMirroredBuildingProbability = 0.2f;
        public RandomFloatRange mirroredWidthRange = new RandomFloatRange(8f, 15f);
        public RandomFloatRange mirroredLengthRange = new RandomFloatRange(10f, 20f);
        public RandomFloatRange mirroredOffsetRange = new RandomFloatRange(-1f, 1f);

        [Header("Structure (Weighted Bell Curve)")]
        public WeightedInt floorCountDistribution = new WeightedInt(
            new WeightedIntOption(1, 8f), new WeightedIntOption(2, 5f), new WeightedIntOption(3, 2f), new WeightedIntOption(4, 1f)
        );
        public bool generateColliders = true;
        public bool generateLODGroup = true;
        public RandomFloatRange lod1ScreenSizeRange = new RandomFloatRange(0.5f, 0.7f);
        public RandomFloatRange lod2ScreenSizeRange = new RandomFloatRange(0.1f, 0.3f);

        [Header("Foundation & Masonry")]
        [Range(0f, 1f)] public float addFoundationProbability = 0.9f;
        public RandomFloatRange foundationHeightRange = new RandomFloatRange(0.3f, 0.8f);
        public RandomFloatRange foundationExtensionRange = new RandomFloatRange(0.1f, 0.3f);
        [Range(0f, 1f)] public float addChimneyProbability = 0.5f;

        [Header("Roof")]
        public WeightedInt roofTypeDistribution = new WeightedInt(
            new WeightedIntOption((int)BuildingProfile.RoofType.Gabled, 10f), new WeightedIntOption((int)BuildingProfile.RoofType.Flat, 3f)
        );
        public RandomFloatRange roofHeightRange = new RandomFloatRange(2f, 5f);
        public RandomFloatRange roofOverhangRange = new RandomFloatRange(0.2f, 0.6f);

        [Header("Windows")]
        public RandomFloatRange windowWidthRange = new RandomFloatRange(0.8f, 1.2f);
        public RandomFloatRange windowHeightRange = new RandomFloatRange(1.2f, 1.6f);
        public RandomFloatRange windowSillHeightRange = new RandomFloatRange(0.8f, 1.0f);
        [Range(0f, 1f)] public float addWindowFramesProbability = 0.8f;
        public RandomFloatRange windowFrameWidthRange = new RandomFloatRange(0.1f, 0.2f);

        public WeightedInt windowsFacadeDistribution = new WeightedInt(
            new WeightedIntOption(1, 2f), new WeightedIntOption(2, 6f), new WeightedIntOption(3, 4f), new WeightedIntOption(4, 1f)
        );

        [Header("Doors")]
        [Range(0f, 1f)] public float addDoorProbability = 1.0f;
        [Range(0f, 1f)] public float addBackDoorProbability = 0.2f;
        [Range(0f, 1f)] public float addWingDoorProbability = 0.1f;
        public RandomFloatRange doorWidthRange = new RandomFloatRange(1.0f, 1.4f);
        public RandomFloatRange doorHeightRange = new RandomFloatRange(2.0f, 2.4f);
        public RandomFloatRange doorOffsetRange = new RandomFloatRange(-1.5f, 1.5f);
        [Range(0f, 1f)] public float addDoorFrameProbability = 0.9f;
        public RandomFloatRange doorFrameWidthRange = new RandomFloatRange(0.1f, 0.2f);

        [Header("Porches")]
        [Range(0f, 1f)] public float addPorchProbability = 0.4f;
        [Range(0f, 1f)] public float addBackPorchProbability = 0.2f;
        [Range(0f, 1f)] public float addWingPorchProbability = 0.1f;
        public RandomFloatRange porchDepthRange = new RandomFloatRange(1.5f, 3.0f);
        public RandomFloatRange porchWidthRange = new RandomFloatRange(3.0f, 6.0f);
        public RandomFloatRange porchHeightRange = new RandomFloatRange(2.5f, 3.0f);
        public RandomFloatRange porchColumnSizeRange = new RandomFloatRange(0.15f, 0.25f);

        [Header("Accents & Trims")]
        public RandomFloatRange trimWidthRange = new RandomFloatRange(0.2f, 0.4f);
        [Range(0f, 1f)] public float addCornerTrimProbability = 0.5f;
        [Range(0f, 1f)] public float addHorizontalTrimProbability = 0.5f;
        [Range(0f, 1f)] public float addVerticalTrimProbability = 0.3f;
        [Range(0f, 1f)] public float addFasciaTrimProbability = 0.7f;
        public RandomFloatRange fasciaHeightRange = new RandomFloatRange(0.15f, 0.3f);

        [Header("UV Settings")]
        public RandomFloatRange textureScaleRange = new RandomFloatRange(0.8f, 1.2f);
        public RandomFloatRange detailOffsetBiasRange = new RandomFloatRange(0.01f, 0.02f);

        /// <summary>
        /// Instantiates a fresh, deterministic profile configured by rolling weighted probabilities.
        /// Phase 5 Warning: Ensure the Editor deletes these ghost profiles to prevent memory leakage.
        /// </summary>
        public BuildingProfile GenerateRandomProfile()
        {
            BuildingProfile profile = ScriptableObject.CreateInstance<BuildingProfile>();

            // Critical Anti-Leak Measure: Tagged so Editor can sweep memory
            profile.name = "Dynamic_Temp_BuildingProfile_" + Random.Range(10000, 99999);
            profile.hideFlags = HideFlags.HideAndDontSave;

            if (palettes != null && palettes.Count > 0)
            {
                float totalWeight = 0f;
                foreach (var p in palettes) totalWeight += p.selectionWeight;

                float roll = Random.Range(0, totalWeight);
                float cumulative = 0f;
                foreach (var p in palettes)
                {
                    cumulative += p.selectionWeight;
                    if (roll <= cumulative) { p.ApplyTo(profile); break; }
                }
            }

            profile.width = widthRange.Roll();
            profile.length = lengthRange.Roll();
            profile.wallHeight = wallHeightRange.Roll();

            profile.addWing = Random.value <= addWingProbability;
            profile.wingWidth = Mathf.Clamp(wingWidthRange.Roll(), 2f, profile.length);
            profile.wingLength = wingLengthRange.Roll();
            profile.wingOffset = wingOffsetRange.Roll();

            profile.addMirroredBuilding = profile.addWing && (Random.value <= addMirroredBuildingProbability);
            profile.mirroredWidth = mirroredWidthRange.Roll();
            profile.mirroredLength = Mathf.Max(mirroredLengthRange.Roll(), profile.wingWidth);
            profile.mirroredOffset = mirroredOffsetRange.Roll();

            profile.floorCount = floorCountDistribution.Roll();
            profile.generateColliders = generateColliders;
            profile.generateLODGroup = generateLODGroup;
            profile.lod1ScreenSize = lod1ScreenSizeRange.Roll();
            profile.lod2ScreenSize = lod2ScreenSizeRange.Roll();

            profile.addFoundation = Random.value <= addFoundationProbability;
            profile.foundationHeight = foundationHeightRange.Roll();
            profile.foundationExtension = foundationExtensionRange.Roll();

            profile.addChimney = Random.value <= addChimneyProbability;
            profile.chimneySize = new Vector3(Random.Range(0.6f, 1f), Random.Range(1.5f, 2.5f), Random.Range(0.6f, 1f));

            profile.roofType = (BuildingProfile.RoofType)roofTypeDistribution.Roll();
            profile.roofHeight = roofHeightRange.Roll();
            profile.roofOverhang = roofOverhangRange.Roll();

            profile.windowWidth = windowWidthRange.Roll();
            profile.windowHeight = windowHeightRange.Roll();
            profile.windowSillHeight = windowSillHeightRange.Roll();
            profile.addWindowFrames = Random.value <= addWindowFramesProbability;
            profile.windowFrameWidth = windowFrameWidthRange.Roll();

            profile.windowsFront = windowsFacadeDistribution.Roll();
            profile.windowsBack = windowsFacadeDistribution.Roll();
            profile.windowsLeft = windowsFacadeDistribution.Roll();
            profile.windowsRight = windowsFacadeDistribution.Roll();

            profile.addDoor = Random.value <= addDoorProbability;
            profile.addBackDoor = Random.value <= addBackDoorProbability;
            profile.addWingDoor = Random.value <= addWingDoorProbability;
            profile.doorWidth = doorWidthRange.Roll();
            profile.doorHeight = doorHeightRange.Roll();
            profile.doorOffsetFromCenter = doorOffsetRange.Roll();
            profile.addDoorFrame = Random.value <= addDoorFrameProbability;
            profile.doorFrameWidth = doorFrameWidthRange.Roll();

            profile.addPorch = Random.value <= addPorchProbability;
            profile.addBackPorch = Random.value <= addBackPorchProbability;
            profile.addWingPorch = Random.value <= addWingPorchProbability;
            profile.porchDepth = porchDepthRange.Roll();
            profile.porchWidth = Mathf.Clamp(porchWidthRange.Roll(), 0f, profile.width);
            profile.porchHeight = porchHeightRange.Roll();
            profile.porchColumnSize = porchColumnSizeRange.Roll();

            profile.trimWidth = trimWidthRange.Roll();
            profile.addCornerTrim = Random.value <= addCornerTrimProbability;
            profile.addHorizontalTrim = Random.value <= addHorizontalTrimProbability;
            profile.addVerticalTrim = Random.value <= addVerticalTrimProbability;
            profile.addFasciaTrim = Random.value <= addFasciaTrimProbability;
            profile.fasciaHeight = fasciaHeightRange.Roll();

            profile.textureScale = textureScaleRange.Roll();
            profile.detailOffsetBias = detailOffsetBiasRange.Roll();

            return profile;
        }
    }
}

// =========================================================================
// PHASE 4: INSPECTOR UX (Streamlined Single-Line Rendering)
// =========================================================================
#if UNITY_EDITOR
namespace Veridian.BuildingSystem.Editor
{
    using UnityEditor;
    using Veridian.BuildingSystem.Runtime;

    [CustomPropertyDrawer(typeof(RandomFloatRange))]
    public class RandomFloatRangeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");

            float w = position.width / 2f;
            Rect minRect = new Rect(position.x, position.y, w - 5, position.height);
            Rect maxRect = new Rect(position.x + w + 5, position.y, w - 5, position.height);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float oldLabelWidth = EditorGUIUtility.labelWidth;

            EditorGUIUtility.labelWidth = 30f;
            EditorGUI.PropertyField(minRect, minProp, new GUIContent("Min"));
            EditorGUI.PropertyField(maxRect, maxProp, new GUIContent("Max"));

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(WeightedIntOption))]
    public class WeightedIntOptionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, GUIContent.none, property);

            var valProp = property.FindPropertyRelative("value");
            var wProp = property.FindPropertyRelative("weight");

            float w = position.width / 2f;
            Rect valRect = new Rect(position.x, position.y, w - 5, position.height);
            Rect wRect = new Rect(position.x + w + 5, position.y, w - 5, position.height);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float oldLabelWidth = EditorGUIUtility.labelWidth;

            EditorGUIUtility.labelWidth = 40f;
            EditorGUI.PropertyField(valRect, valProp, new GUIContent("Value"));
            EditorGUIUtility.labelWidth = 50f;
            EditorGUI.PropertyField(wRect, wProp, new GUIContent("Weight"));

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
}
#endif