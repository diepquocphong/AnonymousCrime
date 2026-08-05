using UnityEngine;
using System.Collections.Generic;

namespace Veridian.BuildingSystem.Runtime
{
    /// <summary>
    /// Explicitly tracks and cleans up unmanaged procedural meshes and fallback material instances 
    /// from GPU memory when the parent building GameObject is dynamically destroyed during runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class RuntimeBuildingTracker : MonoBehaviour
    {
        [HideInInspector] public List<Mesh> trackedMeshes = new List<Mesh>();
        [HideInInspector] public List<Material> trackedMaterials = new List<Material>();

        public void TrackMesh(Mesh mesh)
        {
            if (mesh != null && !trackedMeshes.Contains(mesh)) trackedMeshes.Add(mesh);
        }

        public void TrackMaterial(Material mat)
        {
            if (mat != null && !trackedMaterials.Contains(mat)) trackedMaterials.Add(mat);
        }

        private void OnDestroy()
        {
            if (trackedMeshes != null)
            {
                foreach (var mesh in trackedMeshes)
                {
                    if (mesh != null) SafeDestroy(mesh);
                }
            }
            trackedMeshes.Clear();

            if (trackedMaterials != null)
            {
                foreach (var mat in trackedMaterials)
                {
                    if (mat != null) SafeDestroy(mat);
                }
            }
            trackedMaterials.Clear();
        }

        private void SafeDestroy(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}