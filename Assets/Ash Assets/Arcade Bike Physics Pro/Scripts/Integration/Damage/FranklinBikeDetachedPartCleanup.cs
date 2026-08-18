using System.Collections;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Short mobile-safe physics lifetime for the three parts released by a
    /// terminal Bike explosion. Physics exists only while the debris is moving;
    /// after settling it is frozen, sunk and destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinBikeDetachedPartCleanup : MonoBehaviour
    {
        private const float GROUND_PROBE_INTERVAL = 0.2f;

        private readonly RaycastHit[] m_GroundHits = new RaycastHit[8];

        private Rigidbody m_Body;
        private Collider[] m_Colliders;
        private Renderer[] m_Renderers;
        private bool m_AlignWheelToGround;
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
            bool alignWheelToGround,
            float wheelRadius,
            float minimumFlightTime,
            float maximumFlightTime,
            float restDuration,
            float sinkDuration,
            float sinkDistance)
        {
            m_Body = body;
            m_Colliders = GetComponentsInChildren<Collider>(true);
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_AlignWheelToGround = alignWheelToGround;
            m_WheelRadius = Mathf.Max(0.08f, wheelRadius);
            m_MinimumFlightTime = Mathf.Max(0.1f, minimumFlightTime);
            m_MaximumFlightTime = Mathf.Max(
                m_MinimumFlightTime + 0.25f,
                maximumFlightTime
            );
            m_RestDuration = Mathf.Max(0f, restDuration);
            m_SinkDuration = Mathf.Max(0.1f, sinkDuration);
            m_SinkDistance = Mathf.Max(0.05f, sinkDistance);
            m_ReleasedAt = Time.unscaledTime;
            m_NextGroundProbeAt = m_ReleasedAt + m_MaximumFlightTime;
            // A future mesh variant may have no collider. It still needs the
            // raycast/hard-timeout lifecycle instead of becoming an immortal
            // world-root Rigidbody, so collider presence is not a prerequisite.
            m_IsConfigured = m_Body != null;
            if (m_Body == null)
                Destroy(gameObject, m_MaximumFlightTime + 8f);
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

            float elapsed = Time.unscaledTime - m_ReleasedAt;
            if (elapsed < m_MinimumFlightTime) return;

            bool recentGroundContact =
                Time.unscaledTime - m_LastGroundContactAt <= 0.16f;
            bool slowEnough = m_Body.IsSleeping() ||
                (m_Body.linearVelocity.sqrMagnitude <= 2.25f &&
                 m_Body.angularVelocity.sqrMagnitude <= 81f);
            if (recentGroundContact &&
                (slowEnough || elapsed >= m_MaximumFlightTime))
            {
                SettleOnSurface(m_GroundPoint, m_GroundNormal);
                return;
            }

            if (elapsed < m_MaximumFlightTime) return;
            float now = Time.unscaledTime;
            if (now >= m_NextGroundProbeAt)
            {
                m_NextGroundProbeAt = now + GROUND_PROBE_INTERVAL;
                if (TryFindGroundBelow(out RaycastHit hit))
                {
                    SettleOnSurface(hit.point, hit.normal);
                    return;
                }
            }

            if (elapsed >= m_MaximumFlightTime + 8f)
                Destroy(gameObject);
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

        private bool TryFindGroundBelow(out RaycastHit result)
        {
            result = default;
            Vector3 origin = transform.position + Vector3.up * 0.35f;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                m_GroundHits,
                Mathf.Max(1.5f, m_WheelRadius * 4f),
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore
            );

            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; ++i)
            {
                RaycastHit hit = m_GroundHits[i];
                if (hit.collider == null || hit.collider.attachedRigidbody == m_Body ||
                    Vector3.Dot(hit.normal, Vector3.up) < 0.35f ||
                    hit.distance >= bestDistance)
                {
                    continue;
                }
                bestDistance = hit.distance;
                result = hit;
            }
            return bestDistance < float.PositiveInfinity;
        }

        private void SettleOnSurface(Vector3 point, Vector3 normal)
        {
            if (m_IsSettled) return;
            m_IsSettled = true;

            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            normal.Normalize();
            if (Vector3.Dot(normal, Vector3.up) < 0f) normal = -normal;

            if (m_AlignWheelToGround)
            {
                transform.rotation = Quaternion.FromToRotation(
                    transform.right,
                    normal
                ) * transform.rotation;
                transform.position = point + normal * (m_WheelRadius * 0.3f);
            }

            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
            m_Body.useGravity = false;
            m_Body.detectCollisions = false;
            m_Body.interpolation = RigidbodyInterpolation.None;
            m_Body.isKinematic = true;
            for (int i = 0; i < m_Colliders.Length; ++i)
            {
                if (m_Colliders[i] != null) m_Colliders[i].enabled = false;
            }

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
