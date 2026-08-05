using UnityEngine;

namespace Veridian.BuildingSystem.Runtime
{
    // ARCHITECTURE: Allows the EditorWindow to polymorphicly ingest either static or dynamic generation sources.
    public abstract class BuildingProfileBase : ScriptableObject { }

    [CreateAssetMenu(fileName = "NewBuildingProfile", menuName = "Procedural/Building Profile")]
    public class BuildingProfile : BuildingProfileBase
    {
        [Header("Dimensions")]
        [Range(3f, 50f)] public float width = 10f;
        [Range(3f, 50f)] public float length = 15f;
        [Range(2f, 10f)] public float wallHeight = 3f;

        [Header("Dynamic Wing Extension (L/T Shapes)")]
        public bool addWing = false;
        [Range(2f, 25f)] public float wingWidth = 8f;
        [Range(2f, 25f)] public float wingLength = 10f;
        [Tooltip("-1.0 = Flush Back, 0.0 = Centered (T-Shape), 1.0 = Flush Front. Determines where the wing connects along the main building's length.")]
        [Range(-1f, 1f)] public float wingOffset = 0f;

        [Header("Mirrored Secondary Building (H/U Shapes)")]
        public bool addMirroredBuilding = false;
        [Range(3f, 50f)] public float mirroredWidth = 10f;
        [Range(3f, 50f)] public float mirroredLength = 15f;
        [Tooltip("-1.0 = Flush Back, 0.0 = Centered (H-Shape), 1.0 = Flush Front. Determines the Z-axis offset of the mirrored secondary building relative to the connecting wing.")]
        [Range(-1f, 1f)] public float mirroredOffset = 0f;

        [Header("Structure")]
        [Range(1, 10)] public int floorCount = 1;
        public bool generateColliders = true;

        [Header("Aggressive LOD Decay Pipeline")]
        public bool generateLODGroup = true;
        [Range(0.1f, 0.9f)] public float lod1ScreenSize = 0.6f;
        [Range(0.01f, 0.5f)] public float lod2ScreenSize = 0.2f;

        [Header("Foundation & Masonry (3D Cubes)")]
        public bool addFoundation = true;
        [Range(0.1f, 4.0f)] public float foundationHeight = 0.5f;
        [Range(0f, 2f)] public float foundationExtension = 0.2f;
        public bool addChimney = true;
        public Vector3 chimneySize = new Vector3(0.8f, 2.0f, 0.8f);

        public enum RoofType { Gabled, Flat }
        [Header("Roof (Structural)")]
        public RoofType roofType = RoofType.Gabled;
        [Range(0.1f, 8f)] public float roofHeight = 3f;
        [Range(0f, 2f)] public float roofOverhang = 0.3f;

        [Header("Windows (Floating Decals)")]
        [Range(0.5f, 5f)] public float windowWidth = 1f;
        [Range(0.5f, 5f)] public float windowHeight = 1.4f;
        public float windowSillHeight = 0.9f;
        public bool addWindowFrames = true;
        [Range(0.05f, 0.5f)] public float windowFrameWidth = 0.15f;

        [Header("Window Counts (Per Facade Cardinal)")]
        [Range(0, 10)] public int windowsFront = 2;
        [Range(0, 10)] public int windowsBack = 2;
        [Range(0, 10)] public int windowsLeft = 3;
        [Range(0, 10)] public int windowsRight = 3;

        [Header("Doors (Assemblies)")]
        public bool addDoor = true;
        public bool addBackDoor = false;
        public bool addWingDoor = false;
        [Range(0.5f, 5f)] public float doorWidth = 1.2f;
        [Range(1f, 5f)] public float doorHeight = 2.2f;
        [Range(-20f, 20f)] public float doorOffsetFromCenter = 0f;
        public bool addDoorFrame = true;
        [Range(0.05f, 0.5f)] public float doorFrameWidth = 0.15f;

        [Header("Porch (3D Pillars & Extensions)")]
        public bool addPorch = false;
        public bool addBackPorch = false;
        public bool addWingPorch = false;
        [Range(1f, 5f)] public float porchDepth = 2f;
        [Range(0f, 40f)] public float porchWidth = 4f;
        public float porchHeight = 2.7f;
        public float porchColumnSize = 0.2f;

        [Header("Accents (Floating Decals)")]
        [Range(0.05f, 2f)] public float trimWidth = 0.3f;
        public bool addCornerTrim = false;
        public bool addHorizontalTrim = false;
        public bool addVerticalTrim = false;
        public bool addFasciaTrim = false;
        public float fasciaHeight = 0.2f;

        [Header("UV Settings")]
        [Range(0.1f, 5f)] public float textureScale = 1f;
        [Tooltip("Applies a minute outward translation to floating decals (like windows and trims) along their normal vector. Essential for preventing Z-fighting render artifacts when intersecting planar surfaces.")]
        public float detailOffsetBias = 0.01f;

        [Header("Dynamic Consolidation Materials")]
        public Material wallMaterial;
        public Color wallFallbackColor = new Color(0.5f, 0.55f, 0.45f);

        public Material roofMaterial;
        public Color roofFallbackColor = new Color(0.2f, 0.2f, 0.22f);

        public Material glassMaterial;
        public Color glassFallbackColor = new Color(0.1f, 0.15f, 0.2f);

        public Material masonryMaterial;
        public Color masonryFallbackColor = new Color(0.55f, 0.3f, 0.25f);

        public Material accentMaterial;
        public Color accentFallbackColor = new Color(0.35f, 0.35f, 0.38f);

        public Material frameMaterial;
        public Color frameFallbackColor = new Color(0.4f, 0.45f, 0.55f);

        public Material doorMaterial;
        public Color doorFallbackColor = new Color(0.35f, 0.2f, 0.1f);

        public float BuildingBaseY => addFoundation ? foundationHeight : 0f;
        public float TotalWallHeight => floorCount * wallHeight;

        private void OnValidate()
        {
            porchWidth = Mathf.Clamp(porchWidth, 0f, width);
            wingWidth = Mathf.Clamp(wingWidth, 2f, length);

            if (addMirroredBuilding)
            {
                // Enforce geometric integrity: The mirrored building must be at least as long as the wing connecting to it
                mirroredLength = Mathf.Max(mirroredLength, wingWidth);
            }
        }
    }

}