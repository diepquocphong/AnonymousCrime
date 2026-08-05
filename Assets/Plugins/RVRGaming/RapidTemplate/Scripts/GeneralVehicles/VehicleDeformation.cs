using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DeformableMesh
{
    [Tooltip("Assign the MeshFilter that you want to deform.")]
    public MeshFilter meshFilter;

    [HideInInspector] public Mesh originalMesh;
    [HideInInspector] public Mesh deformedMesh;
    [HideInInspector] public Vector3[] originalVertices;
    [HideInInspector] public Vector3[] displacedVertices;
}

public class VehicleDeformation : MonoBehaviour
{
    [Header("Deformation Settings")]
    [Tooltip("Radius around the collision point within which vertices are affected")]
    public float deformationRadius = 0.5f;
    [Tooltip("Multiplier for how much the damage affects the deformation")]
    public float deformationIntensity = 0.1f;
    [Tooltip("Maximum allowed deformation offset per vertex (relative to original mesh)")]
    public float maxDeformation = 0.5f;
    [Tooltip("Optional: Speed at which the mesh recovers over time (if recovery is desired)")]
    public float recoverySpeed = 0.1f;
    [Tooltip("Cooldown (in seconds) between deformation calculations to avoid performance spikes.")]
    public float deformationCooldown = 0.5f;

    private float lastDeformationTime = -100f;

    [Header("Meshes to Deform")]
    [Tooltip("List of MeshFilters to be deformed when damage is applied")]
    public List<DeformableMesh> deformableMeshes = new List<DeformableMesh>();

    void Start()
    {
        foreach (var deformable in deformableMeshes)
        {
            if (deformable.meshFilter != null)
            {
                deformable.originalMesh = deformable.meshFilter.mesh;
                deformable.deformedMesh = Instantiate(deformable.originalMesh);
                deformable.meshFilter.mesh = deformable.deformedMesh;
                deformable.deformedMesh.MarkDynamic();

                deformable.originalVertices = deformable.originalMesh.vertices;
                deformable.displacedVertices = new Vector3[deformable.originalVertices.Length];
                System.Array.Copy(deformable.originalVertices, deformable.displacedVertices, deformable.originalVertices.Length);
            }
        }
    }

    /// <param name="collisionPoint">World-space collision point</param>
    /// <param name="damage">Damage value (typically based on collision velocity)</param>
    /// <param name="forceDirection">Collision force direction</param>
    public void ApplyDeformation(Vector3 collisionPoint, float damage, Vector3 forceDirection)
    {
        if (Time.time - lastDeformationTime < deformationCooldown)
        {
            return;
        }
        lastDeformationTime = Time.time;

        foreach (var deformable in deformableMeshes)
        {
            if (deformable.deformedMesh == null || deformable.meshFilter == null)
                continue;

            Vector3 localCollisionPoint = deformable.meshFilter.transform.InverseTransformPoint(collisionPoint);

            for (int i = 0; i < deformable.displacedVertices.Length; i++)
            {
                float distance = Vector3.Distance(deformable.originalVertices[i], localCollisionPoint);
                if (distance < deformationRadius)
                {
                    float falloff = 1f - (distance / deformationRadius);
                    float deformationAmount = damage * deformationIntensity * falloff;
                    deformationAmount = Mathf.Clamp(deformationAmount, -maxDeformation, maxDeformation);

                    Vector3 localDeformation = deformable.meshFilter.transform.InverseTransformDirection(forceDirection.normalized) * deformationAmount;

                    deformable.displacedVertices[i] += localDeformation;

                    Vector3 totalDeformation = deformable.displacedVertices[i] - deformable.originalVertices[i];
                    if (totalDeformation.magnitude > maxDeformation)
                    {
                        deformable.displacedVertices[i] = deformable.originalVertices[i] + totalDeformation.normalized * maxDeformation;
                    }
                }
            }

            deformable.deformedMesh.vertices = deformable.displacedVertices;
            deformable.deformedMesh.RecalculateNormals();
        }
    }

    public void ResetDeformation()
    {
        foreach (var deformable in deformableMeshes)
        {
            if (deformable.deformedMesh == null)
                continue;

            System.Array.Copy(deformable.originalVertices, deformable.displacedVertices, deformable.originalVertices.Length);
            deformable.deformedMesh.vertices = deformable.displacedVertices;
            deformable.deformedMesh.RecalculateNormals();
        }
    }
}
