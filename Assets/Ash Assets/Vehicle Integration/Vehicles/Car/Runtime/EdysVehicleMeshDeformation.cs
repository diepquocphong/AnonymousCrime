using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Render-mesh-only port of VehicleDamage.DeformMesh from Edy's Vehicle
    /// Physics 5.5.3. The project owner supplied the licensed unitypackage.
    /// EVP controller, collider recooking, node/wheel damage, input and repair
    /// hotkey are deliberately not included.
    /// </summary>
    internal static class EdysVehicleMeshDeformation
    {
        // VehicleDamage.ProcessImpact multiplies contact velocity by 0.02.
        public const float ImpactScale = 0.02f;

        public static int DeformVertices(
            Vector3[] vertices,
            Vector3[] originalVertices,
            Vector3 localContactPoint,
            Vector3 localImpactVelocity,
            float damageRadius,
            float maximumDisplacement,
            float maximumVertexFracture)
        {
            if (vertices == null || originalVertices == null ||
                vertices.Length != originalVertices.Length ||
                vertices.Length == 0 || damageRadius <= 0f)
            {
                return 0;
            }

            float radiusSq = damageRadius * damageRadius;
            float maximumDisplacementSq = maximumDisplacement * maximumDisplacement;
            int damagedVertices = 0;

            for (int i = 0; i < vertices.Length; ++i)
            {
                float distanceSq = (localContactPoint - vertices[i]).sqrMagnitude;
                if (distanceSq >= radiusSq) continue;

                float falloff = (damageRadius - Mathf.Sqrt(distanceSq)) / damageRadius;
                Vector3 damage = localImpactVelocity * falloff;
                if (maximumVertexFracture > 0f)
                    damage += Random.onUnitSphere * maximumVertexFracture;

                vertices[i] += damage;
                Vector3 totalDeformation = vertices[i] - originalVertices[i];
                if (totalDeformation.sqrMagnitude > maximumDisplacementSq)
                {
                    vertices[i] = originalVertices[i] +
                        totalDeformation.normalized * maximumDisplacement;
                }

                ++damagedVertices;
            }

            return damagedVertices;
        }
    }
}
