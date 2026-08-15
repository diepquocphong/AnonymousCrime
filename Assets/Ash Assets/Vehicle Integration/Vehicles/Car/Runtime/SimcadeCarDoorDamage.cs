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
            public Transform OriginalParent;
            public Vector3 OriginalLocalPosition;
            public Quaternion OriginalLocalRotation;
            public Vector3 OriginalLocalScale;
            public DoorDamageState State;
            public BoxCollider Collider;
            public Rigidbody Body;
            public HingeJoint Hinge;
            public SimcadeLooseCarDoor LoosePart;
            public Renderer[] Renderers;
            public bool CreatedCollider;
            public bool CreatedBody;
            public bool CreatedHinge;
            public bool CreatedLoosePart;
        }

        [Header("Impact Thresholds")]
        [Tooltip("Accepted impact severity needed to unlatch the nearest door.")]
        [SerializeField, Min(0.1f)] private float m_LooseImpactSeverity = 8f;
        [Tooltip("Accepted severity needed for a later impact to tear an already-loose door off.")]
        [SerializeField, Min(0.2f)] private float m_DetachImpactSeverity = 16.5f;
        [Tooltip("Only a catastrophic first impact may unlatch and detach a door in the same contact.")]
        [SerializeField, Min(0.3f)] private float m_CatastrophicDetachImpactSeverity = 22f;
        [Tooltip("Maximum distance from the collision point to the visible door surface.")]
        [SerializeField, Min(0.25f)] private float m_DoorImpactRadius = 0.8f;

        [Header("Door Windows")]
        [Tooltip("Separate glass meshes are bound to their physical door at startup.")]
        [SerializeField] private Transform m_DriverWindow;
        [SerializeField] private Transform m_PassengerWindow;
        [SerializeField] private Transform m_RearLeftWindow;
        [SerializeField] private Transform m_RearRightWindow;

        [Header("Physical Door")]
        [SerializeField, Min(1f)] private float m_DoorMass = 34f;
        [SerializeField, Min(0f)] private float m_OpenAngularVelocity = 3f;
        [SerializeField, Min(0f)] private float m_DetachVelocity = 2.2f;
        [SerializeField, Min(0f)] private float m_DetachAngularVelocity = 4.5f;
        [SerializeField, Min(100f)] private float m_HingeBreakForce = 14500f;
        [SerializeField, Min(100f)] private float m_HingeBreakTorque = 12000f;
        [Tooltip("Prevents the contact that unlatches the door from also removing it one physics step later.")]
        [SerializeField, Min(0f)] private float m_LooseDoorDetachDelay = 0.45f;
        [Tooltip("Minimum normal speed of a direct later hit on a loose door.")]
        [SerializeField, Min(0f)] private float m_LooseDoorDetachSpeed = 13f;
        [Tooltip("Minimum collision impulse divided by door mass for a direct later hit.")]
        [SerializeField, Min(0f)] private float m_LooseDoorDetachImpulseSpeed = 8.5f;
        [SerializeField, Min(1f)] private float m_DetachedSettleDelay = 8f;

        private readonly DoorSlot[] m_Doors = new DoorSlot[4];
        private SimcadeCarImpactAudio m_Impact;
        private CarEntry m_Entry;
        private Rigidbody m_CarBody;
        private Collider[] m_CarColliders;

        public bool HasCompleteWindowConfiguration =>
            this.m_DriverWindow != null &&
            this.m_PassengerWindow != null &&
            this.m_RearLeftWindow != null &&
            this.m_RearRightWindow != null;

        public int DamagedDoorCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < this.m_Doors.Length; ++i)
                {
                    DoorSlot slot = this.m_Doors[i];
                    if (slot != null && slot.State != DoorDamageState.Intact) count++;
                }

                return count;
            }
        }

        public bool HasDamagedDoors => this.DamagedDoorCount > 0;

        /// <summary>
        /// Authoring API used by the existing Car installer. Reparenting keeps
        /// world-space placement, so the glass immediately becomes part of the
        /// matching door without duplicating a mesh or a Rigidbody.
        /// </summary>
        public void ConfigureWindows(
            Transform driverWindow,
            Transform passengerWindow,
            Transform rearLeftWindow,
            Transform rearRightWindow)
        {
            this.m_DriverWindow = driverWindow;
            this.m_PassengerWindow = passengerWindow;
            this.m_RearLeftWindow = rearLeftWindow;
            this.m_RearRightWindow = rearRightWindow;
            if (this.m_Entry == null) this.m_Entry = this.GetComponent<CarEntry>();
            this.BindWindowsToDoors();
        }

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

        /// <summary>
        /// Restores every loose or detached door to its authored closed pose.
        /// The matching window remains below the door pivot, while temporary
        /// physics components are disabled immediately and removed safely.
        /// </summary>
        public int RepairAllDoors()
        {
            int repaired = 0;
            for (int i = 0; i < this.m_Doors.Length; ++i)
            {
                DoorSlot slot = this.m_Doors[i];
                if (slot == null || slot.State == DoorDamageState.Intact) continue;
                if (this.RepairDoor(slot)) repaired++;
            }

            return repaired;
        }

        public bool RepairDoor(CarEntrySideMode side)
        {
            DoorSlot slot = this.GetDoor(side);
            return slot != null && slot.State != DoorDamageState.Intact &&
                   this.RepairDoor(slot);
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

            this.ResolveWindowReferences();
            this.BindWindowsToDoors();

            this.SetDoor(
                0, CarEntrySideMode.DriverDoor,
                this.m_Entry.doorTransform
            );
            this.SetDoor(
                1, CarEntrySideMode.PassengerDoor,
                this.m_Entry.passengerDoorTransform
            );
            this.SetDoor(
                2, CarEntrySideMode.RearLeftDoor,
                this.m_Entry.rearLeftDoorTransform
            );
            this.SetDoor(
                3, CarEntrySideMode.RearRightDoor,
                this.m_Entry.rearRightDoorTransform
            );
        }

        private void SetDoor(
            int index,
            CarEntrySideMode side,
            Transform pivot)
        {
            this.m_Doors[index] = new DoorSlot
            {
                Side = side,
                Pivot = pivot,
                OriginalParent = pivot != null ? pivot.parent : null,
                OriginalLocalPosition = pivot != null
                    ? pivot.localPosition
                    : Vector3.zero,
                OriginalLocalRotation = pivot != null
                    ? pivot.localRotation
                    : Quaternion.identity,
                OriginalLocalScale = pivot != null
                    ? pivot.localScale
                    : Vector3.one,
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

            DoorDamageState stateAtImpact = slot.State;
            if (slot.State == DoorDamageState.Intact)
                this.LoosenDoor(slot, impulseDirection, severity);

            bool laterLooseDoorHit = stateAtImpact == DoorDamageState.Loose &&
                severity >= this.m_DetachImpactSeverity;
            bool catastrophicFirstHit = stateAtImpact == DoorDamageState.Intact &&
                severity >= this.m_CatastrophicDetachImpactSeverity;
            if (laterLooseDoorHit || catastrophicFirstHit)
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
            slot.CreatedCollider = doorCollider == null;
            if (doorCollider == null) doorCollider = pivot.gameObject.AddComponent<BoxCollider>();
            FitColliderToRenderers(pivot, doorCollider, slot.Renderers);

            Rigidbody body = pivot.GetComponent<Rigidbody>();
            slot.CreatedBody = body == null;
            if (body == null) body = pivot.gameObject.AddComponent<Rigidbody>();
            body.mass = this.m_DoorMass;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.42f;
            // Door motion is large but non-critical presentation. Speculative
            // CCD avoids ContinuousDynamic's expensive sweep while keeping the
            // panel attached to the Car; interpolation is unnecessary at the
            // mobile target frame rate.
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 3;
            body.solverVelocityIterations = 1;
            body.maxAngularVelocity = 14f;
            body.linearVelocity = this.m_CarBody.GetPointVelocity(hingePosition);

            HingeJoint hinge = pivot.GetComponent<HingeJoint>();
            slot.CreatedHinge = hinge == null;
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
            slot.CreatedLoosePart = loosePart == null;
            if (loosePart == null)
                loosePart = pivot.gameObject.AddComponent<SimcadeLooseCarDoor>();
            loosePart.Initialize(
                this,
                slot.Side,
                body,
                this.m_LooseDoorDetachDelay,
                this.m_LooseDoorDetachSpeed,
                this.m_LooseDoorDetachImpulseSpeed,
                this.m_DetachedSettleDelay
            );

            slot.Collider = doorCollider;
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

        private bool RepairDoor(DoorSlot slot)
        {
            Transform pivot = slot.Pivot;
            if (pivot == null) return false;

            if (slot.LoosePart != null) slot.LoosePart.enabled = false;
            if (slot.Hinge != null)
            {
                slot.Hinge.connectedBody = null;
            }
            if (slot.Collider != null) slot.Collider.enabled = false;
            if (slot.Body != null)
            {
                slot.Body.linearVelocity = Vector3.zero;
                slot.Body.angularVelocity = Vector3.zero;
                slot.Body.isKinematic = true;
                slot.Body.detectCollisions = false;
            }

            pivot.SetParent(slot.OriginalParent, false);
            pivot.localPosition = slot.OriginalLocalPosition;
            pivot.localRotation = slot.OriginalLocalRotation;
            pivot.localScale = slot.OriginalLocalScale;

            if (slot.CreatedHinge && slot.Hinge != null) Destroy(slot.Hinge);
            if (slot.CreatedBody && slot.Body != null) Destroy(slot.Body);
            if (slot.CreatedCollider && slot.Collider != null) Destroy(slot.Collider);
            if (slot.CreatedLoosePart && slot.LoosePart != null) Destroy(slot.LoosePart);

            slot.Collider = null;
            slot.Body = null;
            slot.Hinge = null;
            slot.LoosePart = null;
            slot.CreatedCollider = false;
            slot.CreatedBody = false;
            slot.CreatedHinge = false;
            slot.CreatedLoosePart = false;
            slot.State = DoorDamageState.Intact;
            slot.Renderers = pivot.GetComponentsInChildren<Renderer>(true);
            return true;
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

        private void ResolveWindowReferences()
        {
            if (this.HasCompleteWindowConfiguration) return;

            Transform[] transforms = this.GetComponentsInChildren<Transform>(true);
            if (this.m_DriverWindow == null)
                this.m_DriverWindow = FindNamedTransform(transforms, "FLWin");
            if (this.m_PassengerWindow == null)
                this.m_PassengerWindow = FindNamedTransform(transforms, "FRWin");
            if (this.m_RearLeftWindow == null)
                this.m_RearLeftWindow = FindNamedTransform(transforms, "RLWin");
            if (this.m_RearRightWindow == null)
                this.m_RearRightWindow = FindNamedTransform(transforms, "RRWin");
        }

        private void BindWindowsToDoors()
        {
            if (this.m_Entry == null) return;

            BindWindow(this.m_DriverWindow, this.m_Entry.doorTransform);
            BindWindow(this.m_PassengerWindow, this.m_Entry.passengerDoorTransform);
            BindWindow(this.m_RearLeftWindow, this.m_Entry.rearLeftDoorTransform);
            BindWindow(this.m_RearRightWindow, this.m_Entry.rearRightDoorTransform);
        }

        private static void BindWindow(Transform window, Transform door)
        {
            if (window == null || door == null || window == door || window.IsChildOf(door))
                return;

            window.SetParent(door, true);
        }

        private static Transform FindNamedTransform(Transform[] transforms, string targetName)
        {
            if (transforms == null) return null;

            for (int i = 0; i < transforms.Length; ++i)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == targetName) return candidate;
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_LooseImpactSeverity = Mathf.Max(0.1f, this.m_LooseImpactSeverity);
            this.m_DetachImpactSeverity = Mathf.Max(
                this.m_LooseImpactSeverity + 0.1f,
                this.m_DetachImpactSeverity
            );
            this.m_CatastrophicDetachImpactSeverity = Mathf.Max(
                this.m_DetachImpactSeverity + 0.1f,
                this.m_CatastrophicDetachImpactSeverity
            );
            this.m_DoorImpactRadius = Mathf.Max(0.25f, this.m_DoorImpactRadius);
            this.m_DoorMass = Mathf.Max(1f, this.m_DoorMass);
            this.m_LooseDoorDetachDelay = Mathf.Max(0f, this.m_LooseDoorDetachDelay);
            this.m_LooseDoorDetachSpeed = Mathf.Max(0f, this.m_LooseDoorDetachSpeed);
            this.m_LooseDoorDetachImpulseSpeed = Mathf.Max(
                0f,
                this.m_LooseDoorDetachImpulseSpeed
            );
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
        private const float MAXIMUM_DETACHED_PHYSICS_SECONDS = 15f;

        private SimcadeCarDoorDamage m_Owner;
        private CarEntrySideMode m_Side;
        private Rigidbody m_Body;
        private float m_SettleDelay;
        private float m_DetachDelay;
        private float m_DetachSpeed;
        private float m_DetachImpulseSpeed;
        private float m_LoosenedAt;
        private float m_DetachedAt;
        private float m_StillSince;
        private bool m_Detached;

        internal void Initialize(
            SimcadeCarDoorDamage owner,
            CarEntrySideMode side,
            Rigidbody body,
            float detachDelay,
            float detachSpeed,
            float detachImpulseSpeed,
            float settleDelay)
        {
            this.m_Owner = owner;
            this.m_Side = side;
            this.m_Body = body;
            this.m_DetachDelay = detachDelay;
            this.m_DetachSpeed = detachSpeed;
            this.m_DetachImpulseSpeed = detachImpulseSpeed;
            this.m_SettleDelay = settleDelay;
            this.m_LoosenedAt = Time.unscaledTime;
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
            if (Time.unscaledTime - this.m_LoosenedAt < this.m_DetachDelay) return;

            float normalSpeed = GetNormalContactSpeed(collision);
            float impulseSpeed = collision.impulse.magnitude /
                Mathf.Max(1f, this.m_Body != null ? this.m_Body.mass : 1f);
            if (normalSpeed < this.m_DetachSpeed ||
                impulseSpeed < this.m_DetachImpulseSpeed)
            {
                return;
            }

            Vector3 direction = collision.relativeVelocity.sqrMagnitude > 0.001f
                ? -collision.relativeVelocity.normalized
                : Vector3.zero;
            this.m_Owner.RequestDetach(
                this.m_Side,
                direction,
                Mathf.Max(normalSpeed, impulseSpeed)
            );
        }

        private static float GetNormalContactSpeed(Collision collision)
        {
            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (collision.contactCount <= 0) return relativeSpeed;

            Vector3 normal = collision.GetContact(0).normal;
            return Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
        }

        private void OnJointBreak(float breakForce)
        {
            this.m_Owner?.NotifyJointBroken(this.m_Side);
        }

        private void FixedUpdate()
        {
            if (!this.m_Detached || this.m_Body == null || this.m_Body.isKinematic)
                return;
            float now = Time.unscaledTime;
            float detachedDuration = now - this.m_DetachedAt;
            if (detachedDuration < this.m_SettleDelay) return;

            bool still = this.m_Body.linearVelocity.sqrMagnitude < 0.04f &&
                         this.m_Body.angularVelocity.sqrMagnitude < 0.12f;
            bool exceededPhysicsBudget =
                detachedDuration >= MAXIMUM_DETACHED_PHYSICS_SECONDS;
            if (!still && !exceededPhysicsBudget)
            {
                this.m_StillSince = 0f;
                return;
            }

            if (!exceededPhysicsBudget && this.m_StillSince <= 0f)
            {
                this.m_StillSince = now;
                return;
            }

            if (!exceededPhysicsBudget && now - this.m_StillSince < 1.25f) return;
            this.m_Body.linearVelocity = Vector3.zero;
            this.m_Body.angularVelocity = Vector3.zero;
            this.m_Body.isKinematic = true;
            this.enabled = false;
        }
    }
}
