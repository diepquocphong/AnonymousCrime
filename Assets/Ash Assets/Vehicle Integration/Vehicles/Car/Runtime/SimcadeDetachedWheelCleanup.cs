using System.Collections;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Short-lived physics lifecycle for one detached wheel. It watches only
    /// while the wheel is airborne, then disables physics completely, leaves the
    /// tire flat on the contacted surface, waits, sinks and removes the debris.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimcadeDetachedWheelCleanup : MonoBehaviour
    {
        private Rigidbody m_Body;
        private Collider m_Collider;
        private Renderer[] m_Renderers;
        private float m_WheelRadius;
        private float m_MinimumFlightTime;
        private float m_MaximumFlightTime;
        private float m_RestDuration;
        private float m_SinkDuration;
        private float m_SinkDistance;
        private float m_ReleasedAt;
        private float m_NextGroundProbeAt;
        private float m_LastGroundContactAt = float.NegativeInfinity;
        private Vector3 m_GroundPoint;
        private Vector3 m_GroundNormal = Vector3.up;
        private bool m_IsConfigured;
        private bool m_IsSettled;

        public void Configure(
            Rigidbody body,
            Collider wheelCollider,
            float wheelRadius,
            float minimumFlightTime,
            float maximumFlightTime,
            float restDuration,
            float sinkDuration,
            float sinkDistance)
        {
            m_Body = body;
            m_Collider = wheelCollider;
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_WheelRadius = Mathf.Max(0.1f, wheelRadius);
            m_MinimumFlightTime = Mathf.Max(0.1f, minimumFlightTime);
            m_MaximumFlightTime = Mathf.Max(
                m_MinimumFlightTime + 0.25f,
                maximumFlightTime
            );
            m_RestDuration = Mathf.Max(0f, restDuration);
            m_SinkDuration = Mathf.Max(0.1f, sinkDuration);
            m_SinkDistance = Mathf.Max(0.05f, sinkDistance);
            m_ReleasedAt = Time.unscaledTime;
            m_NextGroundProbeAt = 0f;
            m_IsConfigured = m_Body != null && m_Collider != null;
        }

        private void OnCollisionEnter(Collision collision)
        {
            RecordGroundContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            RecordGroundContact(collision);
        }

        private void FixedUpdate()
        {
            if (!m_IsConfigured || m_IsSettled || m_Body == null) return;

            float now = Time.unscaledTime;
            float elapsed = now - m_ReleasedAt;
            if (elapsed < m_MinimumFlightTime) return;

            bool hasRecentGroundContact =
                now - m_LastGroundContactAt <= 0.16f;
            bool hasSettledVelocity = m_Body.IsSleeping() ||
                (m_Body.linearVelocity.sqrMagnitude <= 2.25f &&
                 m_Body.angularVelocity.sqrMagnitude <= 81f);

            if (hasRecentGroundContact &&
                (hasSettledVelocity || elapsed >= m_MaximumFlightTime))
            {
                SettleOnSurface(m_GroundPoint, m_GroundNormal);
                return;
            }

            if (elapsed < m_MaximumFlightTime) return;

            // A lost wheel used to raycast at the full physics rate for up to
            // ten seconds. A 6.7 Hz ground probe is visually equivalent here
            // and bounds the mobile physics-query cost.
            if (elapsed >= m_MaximumFlightTime + 10f)
            {
                Destroy(gameObject);
                return;
            }
            if (now < m_NextGroundProbeAt) return;
            m_NextGroundProbeAt = now + 0.15f;

            Vector3 origin = transform.position + Vector3.up * 0.25f;
            float rayDistance = Mathf.Max(1.5f, m_WheelRadius * 4f);
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    rayDistance,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore))
            {
                SettleOnSurface(hit.point, hit.normal);
                return;
            }
        }

        private void RecordGroundContact(Collision collision)
        {
            if (!m_IsConfigured || m_IsSettled || collision == null) return;

            float bestUp = 0.35f;
            for (int i = 0; i < collision.contactCount; ++i)
            {
                ContactPoint contact = collision.GetContact(i);
                float up = Vector3.Dot(contact.normal, Vector3.up);
                if (up <= bestUp) continue;
                bestUp = up;
                m_GroundPoint = contact.point;
                m_GroundNormal = contact.normal;
                m_LastGroundContactAt = Time.unscaledTime;
            }
        }

        private void SettleOnSurface(Vector3 point, Vector3 normal)
        {
            if (m_IsSettled) return;
            m_IsSettled = true;

            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            normal.Normalize();
            if (Vector3.Dot(normal, Vector3.up) < 0f) normal = -normal;

            // Sim-Cade spins its wheel mesh around the target's local X axis.
            // Aligning that axle with the surface normal makes the tire lie flat.
            transform.rotation = Quaternion.FromToRotation(transform.right, normal) *
                transform.rotation;
            transform.position = point + normal * (m_WheelRadius * 0.3f);

            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
            m_Body.useGravity = false;
            m_Body.detectCollisions = false;
            m_Body.interpolation = RigidbodyInterpolation.None;
            m_Body.isKinematic = true;
            if (m_Collider != null) m_Collider.enabled = false;

            StartCoroutine(HideAfterRest(normal));
        }

        private IEnumerator HideAfterRest(Vector3 surfaceNormal)
        {
            yield return new WaitForSecondsRealtime(m_RestDuration);

            Vector3 start = transform.position;
            float elapsed = 0f;
            while (elapsed < m_SinkDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / m_SinkDuration);
                t = t * t * (3f - 2f * t);
                transform.position = start - surfaceNormal * (m_SinkDistance * t);
                yield return null;
            }

            for (int i = 0; i < m_Renderers.Length; ++i)
            {
                if (m_Renderers[i] != null) m_Renderers[i].enabled = false;
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
