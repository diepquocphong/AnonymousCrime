using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Veridian.BuildingSystem.Runtime
{
    /// <summary>
    /// Demonstrates the operational safety and dynamic generation capabilities of the Procedural Building System.
    /// Endlessly cycles through random building variations while strictly managing memory by cleaning up
    /// explicitly generated GameObjects, Tracker-managed unmanaged assets, and temporary ScriptableObjects.
    /// </summary>
    public class RuntimeBuildingDemo : MonoBehaviour
    {
        [Tooltip("The dynamic building family used to roll randomized profiles.")]
        public BuildingFamilyProfile familyProfile;

        [Tooltip("Time in seconds between each new building generation cycle.")]
        [Min(0.5f)]
        public float cycleInterval = 2.0f;

        private GameObject currentBuilding;
        private BuildingProfile currentDynamicProfile;
        private Coroutine demoCoroutine;

        private void OnEnable()
        {
            if (familyProfile == null)
            {
                Debug.LogWarning("RuntimeBuildingDemo requires a BuildingFamilyProfile to run. Please assign one in the Inspector.");
                return;
            }
            demoCoroutine = StartCoroutine(GenerateLoop());
        }

        private void OnDisable()
        {
            if (demoCoroutine != null)
            {
                StopCoroutine(demoCoroutine);
                demoCoroutine = null;
            }
            CleanupCurrentBuilding();
        }

        private IEnumerator GenerateLoop()
        {
            // Endless generation loop
            while (true)
            {
                // 1. Roll a fresh, randomized profile from the family probabilities.
                currentDynamicProfile = familyProfile.GenerateRandomProfile();

                // 2. Prepare lists to catch the unmanaged assets
                List<Mesh> generatedMeshes = new List<Mesh>();
                List<Material> generatedMaterials = new List<Material>();

                // 3. Generate the physical building GameObject and extract the assets.
                currentBuilding = BuildingFactory.GenerateAtRuntime(currentDynamicProfile, generatedMeshes, generatedMaterials);

                // 4. Shift Responsibility: The caller attaches the Tracker and feeds it the data
                RuntimeBuildingTracker tracker = currentBuilding.AddComponent<RuntimeBuildingTracker>();
                foreach (Mesh mesh in generatedMeshes) tracker.TrackMesh(mesh);
                foreach (Material mat in generatedMaterials) tracker.TrackMaterial(mat);

                // Parent it to this demo object for clean hierarchy organization.
                currentBuilding.transform.SetParent(transform, false);
                currentBuilding.transform.localPosition = Vector3.zero;
                currentBuilding.transform.localRotation = Quaternion.identity;

                // 5. Wait for the specified interval so the user can inspect the generated structure.
                yield return new WaitForSeconds(cycleInterval);

                // 6. Memory Hygiene: Clean up explicitly to prevent memory leaks before the next cycle.
                CleanupCurrentBuilding();
            }
        }

        private void CleanupCurrentBuilding()
        {
            // Destroy the physical building. The implicitly attached RuntimeBuildingTracker 
            // will automatically intercept this event and natively flush all unmanaged meshes and fallback materials from GPU memory.
            if (currentBuilding != null)
            {
                Destroy(currentBuilding);
                currentBuilding = null;
            }

            // CRITICAL ANTI-LEAK: We must manually destroy the dynamically instantiated ScriptableObject profile.
            // ScriptableObjects generated at runtime via ScriptableObject.CreateInstance bypass standard Unity Garbage Collection
            // and will persist unmanaged in RAM indefinitely, creating a leak if ignored.
            if (currentDynamicProfile != null)
            {
                Destroy(currentDynamicProfile);
                currentDynamicProfile = null;
            }
        }
    }
}