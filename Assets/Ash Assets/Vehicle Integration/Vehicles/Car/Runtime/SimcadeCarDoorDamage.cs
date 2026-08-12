using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Converts each authored Car door into a lightweight physical hinge after a
    /// local heavy impact. A second, stronger impact can break that hinge while
    /// the visual door remains as physical debris in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SimcadeCarDoorDamage : MonoBehaviour
    {
        private enum DoorDamageState : byte
        {
            Intact,
            Loose,
            Detached
        }

        private sealed class DoorSlot
        {
            public CarEntrySideMode Side;
            public Transform Pivot;
            public DoorDamageState State;
            public Rigidbody Body;
            public HingeJoint Hinge;
            public SimcadeLooseCarDoor LoosePart;
            public Renderer[] Renderers;
        }

        [Header("Impact Thresholds")]
        [Tooltip("Accepted impact severity needed to unlatch the nearest door.")]
        [SerializeField, Min(0.1f)] private float m_LooseImpactSeverity = 6.5f;
        [Tooltip("Accepted impact severity needed to tear the nearest door off immediately.")]
        [SerializeField, Min(0.2f)] private float m_DetachImpactSeverity = 12.5f;
        [Tooltip("Maximum distance from the collision point to the visible door surface.")]
        [SerializeField, Min(0.25f)] private float m_DoorImpactRadius = 1.15f;

        [Header("Physical Door")]
        [SerializeField, Min(1f)] private float m_DoorMass = 34f;
        [SerializeField, Min(0f)] private float m_OpenAngularVelocity = 3.8f;
        [SerializeField, Min(0f)] private float m_DetachVelocity = 2.2f;
        [SerializeField, Min(0f)] private float m_DetachAngularVelocity = 4.5f;
        [SerializeField, Min(100f)] private float m_HingeBreakForce = 6800f;
        [SerializeField, Min(100f)] private float m_HingeBreakTorque = 5200f;
        [SerializeField, Min(1f)] private float m_DetachedSettleDelay = 8f;

        private readonly DoorSlot[] m_Doors = new DoorSlot[4];
        private SimcadeCarImpactAudio m_Impact;
        private CarEntry m_Entry;
        private Rigidbody m_CarBody;
        private Collider[] m_CarColliders;

        public bool IsDoorMissing(CarEntrySideMode side)
        {
            DoorSlot slot = this.GetDoor(side);
            return slot != null && slot.State == DoorDamageState.Detached;
        }

        public bool CanAnimateDoor(CarEntrySideMode side)
        {
            DoorSlot slot = this.GetDoor(side);
            return slot == null || slot.State == DoorDamageState.Intact;
        }

        public bool CanAnimateDoor(Transform door)
        {
            if (door == null) return false;

            for (int i = 0; i < this.m_Doors.Length; ++i)
            {
                DoorSlot slot = this.m_Doors[i];
                if (slot?.Pivot != door) continue;
                return slot.State == DoorDamageState.Intact;
            }

            return true;
        }

        private void Awake()
        {
            this.m_CarBody = this.GetComponent<Rigidbody>();
            this.m_Entry = this.GetComponent<CarEntry>();
            this.m_Impact = this.GetComponent<SimcadeCarImpactAudio>();
            this.CacheDoors();
            this.m_CarColliders = this.GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            if (this.m_Impact == null)
                this.m_Impact = this.GetComponent<SimcadeCarImpactAudio>();

            if (this.m_Impact != null)
                this.m_Impact.EventImpactContactAccepted += this.OnImpactAccepted;
        }

        private void OnDisable()
        {
            if (this.m_Impact != null)
                this.m_Impact.EventImpactContactAccepted -= this.OnImpactAccepted;
        }

        private void CacheDoors()
        {
            if (this.m_Entry == null) return;

            this.SetDoor(0, CarEntrySideMode.DriverDoor, this.m_Entry.doorTransform);
            this.SetDoor(1, CarEntrySideMode.PassengerDoor, this.m_Entry.passengerDoorTransform);
            this.SetDoor(2, CarEntrySideMode.RearLeftDoor, this.m_Entry.rearLeftDoorTransform);
            this.SetDoor(3, CarEntrySideMode.RearRightDoor, this.m_Entry.rearRightDoorTransform);
        }

        private void SetDoor(int index, CarEntrySideMode side, Transform pivot)
        {
            this.m_Doors[index] = new DoorSlot
            {
                Side = side,
                Pivot = pivot,
                State = DoorDamageState.Intact,
                Renderers = pivot != null
                    ? pivot.GetComponentsInChildren<Renderer>(true)
                    : null
            };
        }

        private DoorSlot GetDoor(CarEntrySideMode side)
        {
            for (int i = 0; i < this.m_Doors.Length; ++i)
            {
                DoorSlot slot = this.m_Doors[i];
                if (slot != null && slot.Side == side) return slot;
            }

            return null;
        }

        private void OnImpactAccepted(Collision collision, bool isHeavy, float severity)
        {
            if (!isHeavy || collision == null || collision.contactCount <= 0 ||
                severity < this.m_LooseImpactSeverity)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            DoorSlot slot = this.FindNearestDoor(contact.point);
            if (slot == null || slot.State == DoorDamageState.Detached) return;

            Vector3 impulseDirection = collision.relativeVelocity.sqrMagnitude > 0.001f
                ? -collision.relativeVelocity.normalized
                : contact.normal;

            if (slot.State == DoorDamageState.Intact)
                this.LoosenDoor(slot, impulseDirection, severity);

            if (severity >= this.m_DetachImpactSeverity)
                this.DetachDoor(slot, impulseDirection, severity);
        }

        private DoorSlot FindNearestDoor(Vector3 contactPoint)
        {
            DoorSlot closest = null;
            float closestDistance = this.m_DoorImpactRadius * this.m_DoorImpactRadius;

            for (int i = 0; i < this.m_Doors.Length; ++i)
            {
                DoorSlot slot = this.m_Doors[i];
                if (slot?.Pivot == null || slot.State == DoorDamageState.Detached) continue;

                Renderer[] renderers = slot.Renderers;
                if (renderers == null) continue;
                if (renderers.Length == 0) continue;

                float doorDistance = float.PositiveInfinity;
                for (int rendererIndex = 0; rendererIndex < renderers.Length; ++rendererIndex)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null || !renderer.enabled) continue;
                    Vector3 nearest = renderer.bounds.ClosestPoint(contactPoint);
                    doorDistance = Mathf.Min(
                        doorDistance,
                        (contactPoint - nearest).sqrMagnitude
                    );
                }

                if (doorDistance >= closestDistance) continue;
                closestDistance = doorDistance;
                closest = slot;
            }

            return closest;
        }

        private void LoosenDoor(DoorSlot slot, Vector3 impactDirection, float severity)
        {
            Transform pivot = slot.Pivot;
            if (pivot == null || this.m_CarBody == null) return;

            Vector3 hingePosition = pivot.position;
            pivot.SetParent(null, true);

            BoxCollider doorCollider = pivot.GetComponent<BoxCollider>();
            if (doorCollider == null) doorCollider = pivot.gameObject.AddComponent<BoxCollider>();
            FitColliderToRenderers(pivot, doorCollider, slot.Renderers);

            Rigidbody body = pivot.GetComponent<Rigidbody>();
            if (body == null) body = pivot.gameObject.AddComponent<Rigidbody>();
            body.mass = this.m_DoorMass;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.42f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 14f;
            body.linearVelocity = this.m_CarBody.GetPointVelocity(hingePosition);

            HingeJoint hinge = pivot.GetComponent<HingeJoint>();
            if (hinge == null) hinge = pivot.gameObject.AddComponent<HingeJoint>();
            hinge.connectedBody = this.m_CarBody;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector3.zero;
            hinge.connectedAnchor = this.m_CarBody.transform.InverseTransformPoint(hingePosition);
            hinge.axis = Vector3.up;
            hinge.enableCollision = false;
            hinge.breakForce = this.m_HingeBreakForce;
            hinge.breakTorque = this.m_HingeBreakTorque;
            hinge.useLimits = true;

            bool rightSide = IsRightSide(slot.Side);
            JointLimits limits = hinge.limits;
            limits.min = rightSide ? -82f : -4f;
            limits.max = rightSide ? 4f : 82f;
            limits.bounciness = 0.08f;
            limits.contactDistance = 2f;
            hinge.limits = limits;

            if (this.m_CarColliders == null)
                this.m_CarColliders = this.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < this.m_CarColliders.Length; ++i)
            {
                Collider carCollider = this.m_CarColliders[i];
                if (carCollider == null || carCollider == doorCollider) continue;
                Physics.IgnoreCollision(doorCollider, carCollider, true);
            }

            SimcadeLooseCarDoor loosePart = pivot.GetComponent<SimcadeLooseCarDoor>();
            if (loosePart == null)
                loosePart = pivot.gameObject.AddComponent<SimcadeLooseCarDoor>();
            loosePart.Initialize(this, slot.Side, body, this.m_DetachedSettleDelay);

            slot.Body = body;
            slot.Hinge = hinge;
            slot.LoosePart = loosePart;
            slot.State = DoorDamageState.Loose;

            float openSign = rightSide ? -1f : 1f;
            Vector3 hingeAxis = pivot.TransformDirection(Vector3.up);
            body.AddTorque(
                hingeAxis * openSign * this.m_OpenAngularVelocity,
                ForceMode.VelocityChange
            );
            body.AddForce(
                impactDirection * Mathf.Clamp(severity * 0.08f, 0.25f, 1.4f),
                ForceMode.VelocityChange
            );
        }

        internal void RequestDetach(
            CarEntrySideMode side,
            Vector3 impactDirection,
            float severity)
        {
            DoorSlot slot = this.GetDoor(side);
            if (slot == null || slot.State != DoorDamageState.Loose) return;
            this.DetachDoor(slot, impactDirection, severity);
        }

        internal void NotifyJointBroken(CarEntrySideMode side)
        {
            DoorSlot slot = this.GetDoor(side);
            if (slot == null || slot.State != DoorDamageState.Loose) return;
            this.DetachDoor(slot, Vector3.zero, this.m_DetachImpactSeverity);
        }

        private void DetachDoor(DoorSlot slot, Vector3 impactDirection, float severity)
        {
            if (slot == null || slot.State == DoorDamageState.Detached) return;

            if (slot.Hinge != null)
            {
                slot.Hinge.connectedBody = null;
                Destroy(slot.Hinge);
            }

            slot.Hinge = null;
            slot.State = DoorDamageState.Detached;
            slot.LoosePart?.MarkDetached();

            if (slot.Body == null) return;

            float amount = Mathf.InverseLerp(
                this.m_DetachImpactSeverity,
                this.m_DetachImpactSeverity * 1.8f,
                severity
            );
            Vector3 direction = impactDirection.sqrMagnitude > 0.001f
                ? impactDirection.normalized
                : (slot.Pivot.position - this.transform.position).normalized;
            slot.Body.AddForce(
                direction * Mathf.Lerp(this.m_DetachVelocity, this.m_DetachVelocity * 1.7f, amount) +
                Vector3.up * 0.45f,
                ForceMode.VelocityChange
            );
            slot.Body.AddTorque(
                Random.onUnitSphere * this.m_DetachAngularVelocity,
                ForceMode.VelocityChange
            );
        }

        private static void FitColliderToRenderers(
            Transform pivot,
            BoxCollider collider,
            Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
            {
                collider.center = Vector3.zero;
                collider.size = new Vector3(0.12f, 1.05f, 1.15f);
                return;
            }

            bool hasBounds = false;
            Vector3 localMin = Vector3.zero;
            Vector3 localMax = Vector3.zero;

            for (int i = 0; i < renderers.Length; ++i)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;

                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 worldCorner = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z
                    );
                    Vector3 localCorner = pivot.InverseTransformPoint(worldCorner);

                    if (!hasBounds)
                    {
                        localMin = localCorner;
                        localMax = localCorner;
                        hasBounds = true;
                    }
                    else
                    {
                        localMin = Vector3.Min(localMin, localCorner);
                        localMax = Vector3.Max(localMax, localCorner);
                    }
                }
            }

            if (!hasBounds) return;
            collider.center = (localMin + localMax) * 0.5f;
            Vector3 size = localMax - localMin;
            collider.size = new Vector3(
                Mathf.Max(0.06f, size.x),
                Mathf.Max(0.06f, size.y),
                Mathf.Max(0.06f, size.z)
            );
        }

        private static bool IsRightSide(CarEntrySideMode side)
        {
            return side == CarEntrySideMode.PassengerDoor ||
                   side == CarEntrySideMode.RearRightDoor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_LooseImpactSeverity = Mathf.Max(0.1f, this.m_LooseImpactSeverity);
            this.m_DetachImpactSeverity = Mathf.Max(
                this.m_LooseImpactSeverity + 0.1f,
                this.m_DetachImpactSeverity
            );
            this.m_DoorImpactRadius = Mathf.Max(0.25f, this.m_DoorImpactRadius);
            this.m_DoorMass = Mathf.Max(1f, this.m_DoorMass);
            this.m_DetachedSettleDelay = Mathf.Max(1f, this.m_DetachedSettleDelay);
        }
#endif
    }

    /// <summary>
    /// Runtime-only helper placed on a door after its latch fails. It handles a
    /// direct second impact and parks detached debris after it becomes still.
    /// </summary>
    internal sealed class SimcadeLooseCarDoor : MonoBehaviour
    {
        private SimcadeCarDoorDamage m_Owner;
        private CarEntrySideMode m_Side;
        private Rigidbody m_Body;
        private float m_SettleDelay;
        private float m_DetachedAt;
        private float m_StillSince;
        private bool m_Detached;

        internal void Initialize(
            SimcadeCarDoorDamage owner,
            CarEntrySideMode side,
            Rigidbody body,
            float settleDelay)
        {
            this.m_Owner = owner;
            this.m_Side = side;
            this.m_Body = body;
            this.m_SettleDelay = settleDelay;
            this.m_Detached = false;
            this.m_StillSince = 0f;
        }

        internal void MarkDetached()
        {
            this.m_Detached = true;
            this.m_DetachedAt = Time.unscaledTime;
            this.m_StillSince = 0f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (this.m_Detached || this.m_Owner == null || collision == null) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < 8f) return;

            Vector3 direction = collision.relativeVelocity.sqrMagnitude > 0.001f
                ? -collision.relativeVelocity.normalized
                : Vector3.zero;
            this.m_Owner.RequestDetach(this.m_Side, direction, speed);
        }

        private void OnJointBreak(float breakForce)
        {
            this.m_Owner?.NotifyJointBroken(this.m_Side);
        }

        private void FixedUpdate()
        {
            if (!this.m_Detached || this.m_Body == null || this.m_Body.isKinematic)
                return;
            if (Time.unscaledTime - this.m_DetachedAt < this.m_SettleDelay) return;

            bool still = this.m_Body.linearVelocity.sqrMagnitude < 0.04f &&
                         this.m_Body.angularVelocity.sqrMagnitude < 0.12f;
            if (!still)
            {
                this.m_StillSince = 0f;
                return;
            }

            if (this.m_StillSince <= 0f)
            {
                this.m_StillSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - this.m_StillSince < 1.25f) return;
            this.m_Body.linearVelocity = Vector3.zero;
            this.m_Body.angularVelocity = Vector3.zero;
            this.m_Body.isKinematic = true;
            this.enabled = false;
        }
    }
}
