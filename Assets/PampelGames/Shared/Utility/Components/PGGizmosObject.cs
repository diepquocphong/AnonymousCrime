// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.Shared.Utility
{
    public enum PGGizmoObjectType
    {
        Box,
        Sphere,
        Capsule,
        Cylinder,
        Arrow
    }

    public class PGGizmosObject : MonoBehaviour
    {
        public PGGizmoObjectType gizmoType = PGGizmoObjectType.Sphere;
        public bool wireframe;
        public Color color = Color.blue;
        public Vector3 size = Vector3.one;
        public Vector3 arrowDirection = Vector3.forward;

        private void OnDrawGizmos()
        {
            Gizmos.color = color;

#if UNITY_EDITOR
            switch (gizmoType)
            {
                case PGGizmoObjectType.Box:
                    if (wireframe)
                        Gizmos.DrawWireCube(transform.position, size);
                    else
                        Gizmos.DrawCube(transform.position, size);
                    break;
                case PGGizmoObjectType.Sphere:
                    if (wireframe)
                        Gizmos.DrawWireSphere(transform.position, size.x);
                    else
                        Gizmos.DrawSphere(transform.position, size.x);
                    break;
                case PGGizmoObjectType.Capsule:
                    DrawCapsule(transform.position, transform.rotation, size.x, size.y);
                    break;
                case PGGizmoObjectType.Cylinder:
                    DrawCylinder(transform.position, transform.rotation, size.x, size.y);
                    break;
                case PGGizmoObjectType.Arrow:
                    PGInformationUtility.DrawArrow(transform.position, arrowDirection.normalized * size.x, color);
                    break;
            }
#endif
        }

        private void DrawCapsule(Vector3 position, Quaternion rotation, float radius, float height)
        {
            var pointOffset = (height - radius * 2) / 2;

            var upperSphere = position + rotation * Vector3.up * pointOffset;
            var lowerSphere = position + rotation * Vector3.down * pointOffset;

            Gizmos.DrawWireSphere(upperSphere, radius);
            Gizmos.DrawWireSphere(lowerSphere, radius);

            Gizmos.DrawLine(upperSphere + rotation * Vector3.left * radius, lowerSphere + rotation * Vector3.left * radius);
            Gizmos.DrawLine(upperSphere + rotation * Vector3.right * radius, lowerSphere + rotation * Vector3.right * radius);
            Gizmos.DrawLine(upperSphere + rotation * Vector3.forward * radius, lowerSphere + rotation * Vector3.forward * radius);
            Gizmos.DrawLine(upperSphere + rotation * Vector3.back * radius, lowerSphere + rotation * Vector3.back * radius);
        }

        private void DrawCylinder(Vector3 position, Quaternion rotation, float radius, float height)
        {
            var halfHeight = height / 2;
            var upperCircle = position + rotation * Vector3.up * halfHeight;
            var lowerCircle = position + rotation * Vector3.down * halfHeight;

            DrawWireCircle(upperCircle, rotation, radius);
            DrawWireCircle(lowerCircle, rotation, radius);

            Gizmos.DrawLine(upperCircle + rotation * Vector3.left * radius, lowerCircle + rotation * Vector3.left * radius);
            Gizmos.DrawLine(upperCircle + rotation * Vector3.right * radius, lowerCircle + rotation * Vector3.right * radius);
            Gizmos.DrawLine(upperCircle + rotation * Vector3.forward * radius, lowerCircle + rotation * Vector3.forward * radius);
            Gizmos.DrawLine(upperCircle + rotation * Vector3.back * radius, lowerCircle + rotation * Vector3.back * radius);
        }

        private void DrawWireCircle(Vector3 position, Quaternion rotation, float radius)
        {
            var segments = 32;
            var lastPoint = Vector3.zero;

            for (var i = 0; i <= segments; i++)
            {
                var angle = (float) i / segments * 360 * Mathf.Deg2Rad;
                var newPoint = new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
                if (i > 0)
                    Gizmos.DrawLine(position + rotation * lastPoint, position + rotation * newPoint);
                lastPoint = newPoint;
            }
        }
    }
}