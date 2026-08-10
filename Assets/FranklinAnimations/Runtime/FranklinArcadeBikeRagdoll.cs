using ArcadeBP_Pro;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// In-place adaptation of Arcade Bike Physics Pro's RagdollActivator.
    /// The package normally spawns a separate dummy-bike prefab and disables the
    /// original. Franklin keeps the real DQP bike, makes its Rigidbody dynamic,
    /// and enables convex colliders generated from the rendered body meshes plus
    /// short-lived wheel clearance colliders instead.
    /// Character ragdoll and camera remain owned by GC2 and the Main Camera Shot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArcadeBikeControllerPro))]
    [RequireComponent(typeof(FranklinArcadeBikeDriver))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FranklinArcadeBikeRagdoll : MonoBehaviour
    {
        [SerializeField, HideInInspector] private ArcadeBikeControllerPro m_Controller;
        [SerializeField, HideInInspector] private FranklinArcadeBikeDriver m_Driver;
        [SerializeField, HideInInspector] private Rigidbody m_Body;
        [SerializeField, HideInInspector] private Collider m_DrivingCollider;
        [SerializeField, HideInInspector] private MeshCollider[] m_BodyMeshColliders;
        [SerializeField, HideInInspector] private PhysicsMaterial m_DrivingMeshMaterial;
        [SerializeField, HideInInspector] private PhysicsMaterial m_RagdollMeshMaterial;
        [SerializeField, HideInInspector] private SphereCollider m_FrontWheelCollider;
        [SerializeField, HideInInspector] private SphereCollider m_RearWheelCollider;

        [Header("Left / Right Surface Grounding")]
        [SerializeField, HideInInspector] private Vector3 m_LocalLeftSurface;
        [SerializeField, HideInInspector] private Vector3 m_LocalRightSurface;
        [SerializeField, HideInInspector] private float m_SurfaceProbeHalfLength = 0.5f;
        [Tooltip("Minimum bike tilt before a left/right surface can count as grounded.")]
        [Range(20f, 89f)] [SerializeField] private float m_MinimumSideTilt = 48f;
        [Tooltip("Maximum angle between the bike side plane and the supporting ground.")]
        [Range(2f, 35f)] [SerializeField] private float m_MaximumSurfaceParallelError = 18f;
        [Tooltip("Maximum distance from a configured side surface to static ground.")]
        [Min(0.02f)] [SerializeField] private float m_SurfaceGroundDistance = 0.16f;
        [Tooltip("Contact must remain valid for this long before kinematic is restored.")]
        [Min(0.02f)] [SerializeField] private float m_StableGroundTime = 0.3f;
        [Tooltip("Maximum linear speed allowed when parking on a grounded side.")]
        [Min(0f)] [SerializeField] private float m_ParkLinearSpeed = 0.65f;
        [Tooltip("Maximum angular speed allowed when parking on a grounded side.")]
        [Min(0f)] [SerializeField] private float m_ParkAngularSpeed = 0.5f;
        [SerializeField] private LayerMask m_GroundLayers = ~0;
        [Header("Wheel Collider Release")]
        [Tooltip("Extra wheel colliders are released after this tilt so they cannot wedge the fallen bike against ground or walls.")]
        [Range(10f, 70f)] [SerializeField] private float m_ReleaseWheelCollidersAtTilt = 32f;
        [Tooltip("Mesh collider scale while ABP suspension is driving. Ragdoll restores the full rendered size.")]
        [Range(0.7f, 0.98f)] [SerializeField] private float m_DrivingMeshScale = 0.86f;

        private const string BODY_COLLIDER_PROXY_NAME =
            "Franklin Body Mesh Collider";
        // ABP suspension casts against bikeSettings.drivableLayerMask. The pack
        // integration already removes Unity's Ignore Raycast layer from that
        // mask, so body colliders on this layer still make physical contacts but
        // can never be mistaken for road by the bike's own wheel raycasts.
        private const int BODY_COLLIDER_QUERY_LAYER = 2;

        private readonly RaycastHit[] m_GroundHits = new RaycastHit[16];
        private readonly Collider[] m_WheelOverlapHits = new Collider[24];
        private float m_StableGroundTimer;
        private int m_StableGroundSide;
        private int m_ParkedGroundSide;

        public bool IsRagdoll { get; private set; }
        public bool RequiresManualRecovery { get; private set; }
        public int ParkedGroundSide => this.m_ParkedGroundSide;
        /// <summary>
        /// True when a nearby Player may be offered the lift/enter action. A live
        /// tumbling bike is deliberately excluded; once both side probes are on
        /// ground and the body is slow enough, the button may appear even before
        /// the next FixedUpdate parks the bike for manual recovery.
        /// </summary>
        public bool CanInteractWhileFallen
        {
            get
            {
                if (this.RequiresManualRecovery) return true;
                if (!this.IsRagdoll || this.m_Body == null) return false;

                bool slowEnough = this.m_Body.linearVelocity.magnitude <=
                                      this.m_ParkLinearSpeed &&
                                  this.m_Body.angularVelocity.magnitude <=
                                      this.m_ParkAngularSpeed;
                if (!slowEnough) return false;

                float tilt = Vector3.Angle(transform.up, Vector3.up);
                return tilt >= this.m_MinimumSideTilt &&
                       this.GetGroundedSide() != 0;
            }
        }
        public bool IsConfigured =>
            this.m_Controller != null &&
            this.m_Driver != null &&
            this.m_Body != null &&
            this.m_DrivingCollider != null &&
            this.m_BodyMeshColliders != null &&
            this.m_BodyMeshColliders.Length > 0 &&
            this.m_DrivingMeshMaterial != null &&
            this.m_RagdollMeshMaterial != null &&
            this.BodyCollidersUseNonDrivableLayer() &&
            this.m_FrontWheelCollider != null &&
            this.m_RearWheelCollider != null;

        public void Configure(
            ArcadeBikeControllerPro controller,
            FranklinArcadeBikeDriver driver,
            Rigidbody body,
            Collider drivingCollider,
            MeshCollider[] bodyMeshColliders,
            PhysicsMaterial drivingMeshMaterial,
            PhysicsMaterial ragdollMeshMaterial,
            SphereCollider frontWheelCollider,
            SphereCollider rearWheelCollider,
            Vector3 localLeftSurface,
            Vector3 localRightSurface,
            float surfaceProbeHalfLength)
        {
            this.m_Controller = controller;
            this.m_Driver = driver;
            this.m_Body = body;
            this.m_DrivingCollider = drivingCollider;
            this.m_BodyMeshColliders = bodyMeshColliders;
            this.m_DrivingMeshMaterial = drivingMeshMaterial;
            this.m_RagdollMeshMaterial = ragdollMeshMaterial;
            this.m_FrontWheelCollider = frontWheelCollider;
            this.m_RearWheelCollider = rearWheelCollider;
            this.m_LocalLeftSurface = localLeftSurface;
            this.m_LocalRightSurface = localRightSurface;
            this.m_SurfaceProbeHalfLength = Mathf.Max(0.1f, surfaceProbeHalfLength);
            this.ConfigureSuspensionCollisionFiltering();
            this.RefreshRenderedBodySurfaceProbes();
            this.SetWheelCollidersEnabled(false);
            this.SetBodyColliderMode(false);
        }

        /// <summary>
        /// Activates bike ragdoll in the required order: the original Rigidbody is
        /// made non-kinematic first, then ragdoll colliders/center of mass and free
        /// rotation are enabled.
        /// </summary>
        public bool ActivateRagdoll(float fallSign, float toppleAngularVelocity)
        {
            this.ResolveReferences();
            if (!this.IsConfigured || this.IsRagdoll) return false;

            // Must happen before any ragdoll state or collider is activated.
            this.m_Body.isKinematic = false;
            this.BakeRenderedBikePoseIntoBody();

            this.RequiresManualRecovery = false;
            this.m_ParkedGroundSide = 0;
            this.m_StableGroundSide = 0;
            this.SetBodyColliderMode(true);
            this.RefreshRenderedBodySurfaceProbes();
            this.EnableClearWheelColliders();
            this.m_Body.ResetCenterOfMass();
            this.m_StableGroundTimer = 0f;
            this.IsRagdoll = true;
            this.m_Driver.CrashDismount(fallSign, toppleAngularVelocity);
            return true;
        }

        /// <summary>
        /// ABP steers and leans child transforms while the Rigidbody root normally
        /// stays rotation-frozen. A physical ragdoll must first bake that rendered
        /// pose into the root; otherwise the mesh collider and the body's angular
        /// axes disagree and the fallen bike can spin around an unrelated axis.
        /// </summary>
        private void BakeRenderedBikePoseIntoBody()
        {
            ArcadeBikeControllerPro.BikeReferences references =
                this.m_Controller != null ? this.m_Controller.bikeReferences : null;
            if (references == null || this.m_Body == null) return;

            Transform pose = references.LeanTransform != null
                ? references.LeanTransform
                : references.BikeModel != null
                    ? references.BikeModel
                    : references.Rotator;
            if (pose == null) return;

            Vector3 renderedPosition = pose.position;
            Quaternion renderedRotation = pose.rotation;
            if (references.Rotator != null)
                references.Rotator.localRotation = Quaternion.identity;
            if (references.WheelieTransform != null)
                references.WheelieTransform.localRotation = Quaternion.identity;
            if (references.LeanTransform != null)
                references.LeanTransform.localRotation = Quaternion.identity;

            this.m_Body.rotation = renderedRotation;
            this.m_Body.position += renderedPosition - pose.position;
            Physics.SyncTransforms();
        }

        public void SettleRagdoll()
        {
            if (!this.IsRagdoll) return;
            this.EnsureDynamicRagdollState();
        }

        /// <summary>
        /// Interaction does not need to wait for another full stable-time window
        /// after the rendered left/right surface is already grounded and still.
        /// It may park that confirmed physical pose immediately for the authored
        /// lift sequence. A moving or only partially fallen bike remains dynamic.
        /// </summary>
        public bool TryPrepareManualRecoveryForInteraction()
        {
            if (this.RequiresManualRecovery) return true;
            if (!this.CanInteractWhileFallen) return false;

            int groundedSide = this.GetGroundedSide();
            if (groundedSide == 0) return false;

            this.ParkForManualRecovery(groundedSide);
            return true;
        }

        public void DeactivateRagdollForDriving()
        {
            this.IsRagdoll = false;
            this.RequiresManualRecovery = false;
            this.m_ParkedGroundSide = 0;
            this.m_StableGroundSide = 0;
            this.m_StableGroundTimer = 0f;
            this.SetWheelCollidersEnabled(false);
            this.SetBodyColliderMode(false);
        }

        private void Awake()
        {
            this.ResolveReferences();
            this.ConfigureSuspensionCollisionFiltering();
            this.SetWheelCollidersEnabled(false);
            this.SetBodyColliderMode(false);
        }

        private void LateUpdate()
        {
            if (this.IsRagdoll && this.m_Driver != null &&
                this.m_Driver.IsVehicleEnabled)
            {
                this.DeactivateRagdollForDriving();
            }
        }

        private void FixedUpdate()
        {
            if (!this.IsRagdoll) return;
            this.EnsureDynamicRagdollState();
            if (Vector3.Angle(transform.up, Vector3.up) >=
                this.m_ReleaseWheelCollidersAtTilt)
            {
                this.SetWheelCollidersEnabled(false);
            }
            this.UpdateGroundedSideParking();
        }

        private void ResolveReferences()
        {
            if (this.m_Controller == null)
                this.m_Controller = this.GetComponent<ArcadeBikeControllerPro>();
            if (this.m_Driver == null)
                this.m_Driver = this.GetComponent<FranklinArcadeBikeDriver>();
            if (this.m_Body == null) this.m_Body = this.GetComponent<Rigidbody>();
        }

        private void SetWheelCollidersEnabled(bool enabled)
        {
            if (this.m_FrontWheelCollider != null)
                this.m_FrontWheelCollider.enabled = enabled;
            if (this.m_RearWheelCollider != null)
                this.m_RearWheelCollider.enabled = enabled;
        }

        private void SetBodyColliderMode(bool ragdollMode)
        {
            bool hasMeshColliders = this.m_BodyMeshColliders != null &&
                                    this.m_BodyMeshColliders.Length > 0;
            if (this.m_DrivingCollider != null)
            {
                // The legacy ABP capsule remains serialized only because the
                // package reference is typed as CapsuleCollider. Franklin never
                // uses it for physics once rendered-body colliders are available.
                this.m_DrivingCollider.enabled = !hasMeshColliders;
            }

            if (!hasMeshColliders) return;
            foreach (MeshCollider meshCollider in this.m_BodyMeshColliders)
            {
                if (meshCollider == null) continue;
                meshCollider.gameObject.layer = BODY_COLLIDER_QUERY_LAYER;
                meshCollider.enabled = true;
                meshCollider.sharedMaterial = ragdollMode
                    ? this.m_RagdollMeshMaterial
                    : this.m_DrivingMeshMaterial;
                if (meshCollider.transform.name == BODY_COLLIDER_PROXY_NAME)
                {
                    meshCollider.transform.localScale = ragdollMode
                        ? Vector3.one
                        : Vector3.one * this.m_DrivingMeshScale;
                }
            }
        }

        private void ConfigureSuspensionCollisionFiltering()
        {
            if (this.m_Controller != null)
            {
                this.m_Controller.bikeSettings.drivableLayerMask &=
                    ~(1 << BODY_COLLIDER_QUERY_LAYER);
            }

            if (this.m_BodyMeshColliders == null) return;
            foreach (MeshCollider meshCollider in this.m_BodyMeshColliders)
            {
                if (meshCollider != null)
                    meshCollider.gameObject.layer = BODY_COLLIDER_QUERY_LAYER;
            }
        }

        private bool BodyCollidersUseNonDrivableLayer()
        {
            if (this.m_BodyMeshColliders == null ||
                this.m_BodyMeshColliders.Length == 0)
            {
                return false;
            }

            foreach (MeshCollider meshCollider in this.m_BodyMeshColliders)
            {
                if (meshCollider == null ||
                    meshCollider.gameObject.layer != BODY_COLLIDER_QUERY_LAYER)
                {
                    return false;
                }
            }

            return this.m_Controller == null ||
                   (this.m_Controller.bikeSettings.drivableLayerMask.value &
                    (1 << BODY_COLLIDER_QUERY_LAYER)) == 0;
        }

        public Vector3 GetCollisionSafeLeftExitPosition(
            Vector3 authoredWorldPosition,
            float characterClearance)
        {
            Transform bodyFrame = this.m_Controller?.bikeReferences?.BodyMesh != null
                ? this.m_Controller.bikeReferences.BodyMesh
                : transform;
            if (!this.TryGetRenderedBodyBoundsInFrame(
                    bodyFrame,
                    out Bounds bodyBounds
                ))
            {
                return authoredWorldPosition;
            }

            Vector3 localPosition = bodyFrame.InverseTransformPoint(
                authoredWorldPosition
            );
            float safeLocalX = bodyBounds.min.x -
                               Mathf.Max(0.05f, characterClearance);
            localPosition.x = Mathf.Min(localPosition.x, safeLocalX);
            return bodyFrame.TransformPoint(localPosition);
        }

        private void RefreshRenderedBodySurfaceProbes()
        {
            if (!this.TryGetRenderedBodyBoundsInFrame(transform, out Bounds bounds))
                return;
            Vector3 bodyCenter = bounds.center;
            Vector3 bodyExtents = bounds.extents;
            this.m_LocalLeftSurface = new Vector3(
                bounds.min.x,
                bodyCenter.y,
                bodyCenter.z
            );
            this.m_LocalRightSurface = new Vector3(
                bounds.max.x,
                bodyCenter.y,
                bodyCenter.z
            );
            this.m_SurfaceProbeHalfLength = Mathf.Clamp(
                bodyExtents.z * 0.35f,
                0.2f,
                0.75f
            );
        }

        private bool TryGetRenderedBodyBoundsInFrame(
            Transform frame,
            out Bounds result)
        {
            result = default;
            if (frame == null || this.m_BodyMeshColliders == null ||
                this.m_BodyMeshColliders.Length == 0)
            {
                return false;
            }

            bool initialized = false;
            foreach (MeshCollider meshCollider in this.m_BodyMeshColliders)
            {
                Mesh mesh = meshCollider != null ? meshCollider.sharedMesh : null;
                if (mesh == null) continue;

                Bounds meshBounds = mesh.bounds;
                Vector3 center = meshBounds.center;
                Vector3 extents = meshBounds.extents;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 meshPoint = center + Vector3.Scale(
                        extents,
                        new Vector3(x, y, z)
                    );
                    Vector3 framePoint = frame.InverseTransformPoint(
                        meshCollider.transform.TransformPoint(meshPoint)
                    );
                    if (!initialized)
                    {
                        result = new Bounds(framePoint, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        result.Encapsulate(framePoint);
                    }
                }
            }

            return initialized;
        }

        private void EnableClearWheelColliders()
        {
            Physics.SyncTransforms();
            this.EnableWheelColliderIfClear(this.m_FrontWheelCollider);
            this.EnableWheelColliderIfClear(this.m_RearWheelCollider);
        }

        private void EnableWheelColliderIfClear(SphereCollider wheelCollider)
        {
            if (wheelCollider == null) return;
            wheelCollider.enabled = false;

            Vector3 center = wheelCollider.transform.TransformPoint(wheelCollider.center);
            Vector3 scale = wheelCollider.transform.lossyScale;
            float radiusScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z)
            );
            float radius = wheelCollider.radius * radiusScale * 0.98f;
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                this.m_WheelOverlapHits,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            for (int index = 0; index < hitCount; index++)
            {
                Collider hit = this.m_WheelOverlapHits[index];
                if (hit == null || hit == wheelCollider ||
                    hit.attachedRigidbody == this.m_Body ||
                    hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                // Do not introduce a new collider inside a wall, the road or the
                // rider at the exact frame driving physics is released.
                return;
            }

            wheelCollider.enabled = true;
        }

        private void EnsureDynamicRagdollState()
        {
            if (this.m_Body == null) return;
            bool requiresRepair = this.m_Body.isKinematic ||
                                  this.m_Body.constraints != RigidbodyConstraints.None ||
                                  !this.m_Body.useGravity;
            if (!requiresRepair) return;

            this.m_Driver?.KeepCrashRagdollDynamic();
            if (this.m_Body.isKinematic) this.m_Body.isKinematic = false;
            if (this.m_Body.constraints != RigidbodyConstraints.None)
                this.m_Body.constraints = RigidbodyConstraints.None;
            if (!this.m_Body.useGravity) this.m_Body.useGravity = true;
        }

        /// <summary>
        /// Computes a stable upright pose on the same supporting ground. The bike
        /// remains parked and kinematic while BikeEntry animates the manual lift.
        /// </summary>
        public bool TryGetManualRecoveryTarget(
            out Vector3 targetPosition,
            out Quaternion targetRotation,
            out Vector3 groundNormal)
        {
            targetPosition = transform.position;
            targetRotation = transform.rotation;
            groundNormal = Vector3.up;
            if (!this.RequiresManualRecovery || this.m_Body == null) return false;

            if (!this.TryGetSupportingGround(transform.position, out RaycastHit groundHit))
                return false;

            groundNormal = groundHit.normal.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(transform.up, groundNormal);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, groundNormal);
            targetRotation = Quaternion.LookRotation(forward.normalized, groundNormal);

            float minimumWheelBottom = float.PositiveInfinity;
            this.AccumulateWheelBottom(
                this.m_FrontWheelCollider,
                targetRotation,
                groundNormal,
                ref minimumWheelBottom
            );
            this.AccumulateWheelBottom(
                this.m_RearWheelCollider,
                targetRotation,
                groundNormal,
                ref minimumWheelBottom
            );
            if (float.IsPositiveInfinity(minimumWheelBottom))
                minimumWheelBottom = -0.35f;

            float desiredRootDistance = -minimumWheelBottom + 0.015f;
            float currentRootDistance = Vector3.Dot(
                targetPosition - groundHit.point,
                groundNormal
            );
            targetPosition += groundNormal *
                              (desiredRootDistance - currentRootDistance);
            return true;
        }

        public void SetManualRecoveryPose(Vector3 position, Quaternion rotation)
        {
            if (!this.RequiresManualRecovery || this.m_Body == null) return;
            this.m_Body.position = position;
            this.m_Body.rotation = rotation;
            this.m_Body.linearVelocity = Vector3.zero;
            this.m_Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        public void CompleteManualRecovery()
        {
            if (!this.RequiresManualRecovery) return;
            this.RequiresManualRecovery = false;
            this.m_ParkedGroundSide = 0;
            this.m_StableGroundSide = 0;
            this.m_StableGroundTimer = 0f;
            this.SetWheelCollidersEnabled(false);
            this.SetBodyColliderMode(false);
            this.m_Driver?.ParkGroundedRagdoll();
        }

        private void AccumulateWheelBottom(
            SphereCollider wheelCollider,
            Quaternion targetRotation,
            Vector3 groundNormal,
            ref float minimumWheelBottom)
        {
            if (wheelCollider == null) return;
            Vector3 localCenter = transform.InverseTransformPoint(
                wheelCollider.transform.TransformPoint(wheelCollider.center)
            );
            float visualRadius = wheelCollider.radius / 0.86f;
            float centerDistance = Vector3.Dot(
                targetRotation * localCenter,
                groundNormal
            );
            minimumWheelBottom = Mathf.Min(
                minimumWheelBottom,
                centerDistance - visualRadius
            );
        }

        private bool TryGetSupportingGround(Vector3 origin, out RaycastHit groundHit)
        {
            groundHit = default;
            Ray ray = new Ray(origin + Vector3.up * 2f, Vector3.down);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                this.m_GroundHits,
                6f,
                this.m_GroundLayers,
                QueryTriggerInteraction.Ignore
            );
            float closestDistance = float.PositiveInfinity;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = this.m_GroundHits[index];
                Collider collider = candidate.collider;
                if (collider == null || collider.attachedRigidbody == this.m_Body ||
                    collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                Rigidbody supportBody = collider.attachedRigidbody;
                if (supportBody != null && !supportBody.isKinematic) continue;
                if (Vector3.Dot(candidate.normal, Vector3.up) < 0.35f) continue;
                if (candidate.distance >= closestDistance) continue;
                closestDistance = candidate.distance;
                groundHit = candidate;
                found = true;
            }

            return found;
        }

        private void UpdateGroundedSideParking()
        {
            if (this.m_Body == null) return;

            float tilt = Vector3.Angle(transform.up, Vector3.up);
            bool slowEnough = this.m_Body.linearVelocity.magnitude <=
                                  this.m_ParkLinearSpeed &&
                              this.m_Body.angularVelocity.magnitude <=
                                  this.m_ParkAngularSpeed;
            int groundedSide = tilt >= this.m_MinimumSideTilt
                ? this.GetGroundedSide()
                : 0;
            bool sideGrounded = groundedSide != 0;
            if (!slowEnough || !sideGrounded)
            {
                this.m_StableGroundTimer = 0f;
                this.m_StableGroundSide = 0;
                return;
            }

            if (this.m_StableGroundSide != groundedSide)
            {
                this.m_StableGroundSide = groundedSide;
                this.m_StableGroundTimer = 0f;
            }

            this.m_StableGroundTimer += Time.fixedDeltaTime;
            if (this.m_StableGroundTimer < this.m_StableGroundTime) return;

            this.ParkForManualRecovery(groundedSide);
        }

        private void ParkForManualRecovery(int groundedSide)
        {
            // End ragdoll before parking, preserving the invariant that a live
            // ragdoll Rigidbody is never kinematic.
            this.IsRagdoll = false;
            this.RequiresManualRecovery = true;
            this.m_ParkedGroundSide = groundedSide < 0 ? -1 : 1;
            this.m_StableGroundSide = this.m_ParkedGroundSide;
            this.m_StableGroundTimer = 0f;
            this.SetWheelCollidersEnabled(false);
            this.m_Driver?.ParkGroundedRagdoll();
        }

        private int GetGroundedSide()
        {
            bool leftGrounded = this.IsSurfaceGrounded(this.m_LocalLeftSurface);
            bool rightGrounded = this.IsSurfaceGrounded(this.m_LocalRightSurface);
            if (leftGrounded && rightGrounded)
            {
                // Positive right.y means local-left is the lower face.
                return Vector3.Dot(transform.right, Vector3.up) >= 0f ? -1 : 1;
            }
            if (leftGrounded) return -1;
            return rightGrounded ? 1 : 0;
        }

        private bool IsSurfaceGrounded(Vector3 localSurface)
        {
            float halfLength = Mathf.Max(0.1f, this.m_SurfaceProbeHalfLength);
            bool frontGrounded = this.TryGetGroundBelowSurface(
                localSurface + Vector3.forward * halfLength,
                out Vector3 frontNormal
            );
            bool rearGrounded = this.TryGetGroundBelowSurface(
                localSurface - Vector3.forward * halfLength,
                out Vector3 rearNormal
            );

            // A single touching corner is not enough. Requiring both longitudinal
            // ends prevents a half-fallen bike from being frozen in mid-air.
            if (!frontGrounded || !rearGrounded) return false;

            Vector3 supportingNormal = (frontNormal + rearNormal).normalized;
            if (supportingNormal.sqrMagnitude < 0.001f) return false;

            // When a bike side plane is parallel to the ground, its local right
            // axis is nearly perpendicular to that plane and therefore nearly
            // parallel (in either direction) to the supporting ground normal.
            float requiredAlignment = Mathf.Cos(
                this.m_MaximumSurfaceParallelError * Mathf.Deg2Rad
            );
            float alignment = Mathf.Abs(Vector3.Dot(transform.right, supportingNormal));
            return alignment >= requiredAlignment;
        }

        private bool TryGetGroundBelowSurface(
            Vector3 localSurfacePoint,
            out Vector3 groundNormal)
        {
            groundNormal = Vector3.zero;
            Vector3 surfacePoint = transform.TransformPoint(localSurfacePoint);
            float lift = Mathf.Max(0.1f, this.m_SurfaceGroundDistance + 0.08f);
            Ray ray = new Ray(surfacePoint + Vector3.up * lift, Vector3.down);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                this.m_GroundHits,
                lift + this.m_SurfaceGroundDistance,
                this.m_GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = this.m_GroundHits[index];
                Collider collider = hit.collider;
                if (collider == null || collider.attachedRigidbody == this.m_Body ||
                    collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                // GC2 ragdoll bones and other moving props must never be mistaken
                // for the road/terrain supporting the bike.
                Rigidbody supportBody = collider.attachedRigidbody;
                if (supportBody != null && !supportBody.isKinematic) continue;
                if (Vector3.Dot(hit.normal, Vector3.up) < 0.35f) continue;
                groundNormal = hit.normal;
                return true;
            }

            return false;
        }

        private void OnValidate()
        {
            this.m_MinimumSideTilt = Mathf.Clamp(this.m_MinimumSideTilt, 20f, 89f);
            this.m_MaximumSurfaceParallelError = Mathf.Clamp(
                this.m_MaximumSurfaceParallelError,
                2f,
                35f
            );
            this.m_SurfaceGroundDistance = Mathf.Max(0.02f, this.m_SurfaceGroundDistance);
            this.m_StableGroundTime = Mathf.Max(0.02f, this.m_StableGroundTime);
            this.m_ParkLinearSpeed = Mathf.Max(0f, this.m_ParkLinearSpeed);
            this.m_ParkAngularSpeed = Mathf.Max(0f, this.m_ParkAngularSpeed);
            this.m_SurfaceProbeHalfLength = Mathf.Max(0.1f, this.m_SurfaceProbeHalfLength);
            this.m_ReleaseWheelCollidersAtTilt = Mathf.Clamp(
                this.m_ReleaseWheelCollidersAtTilt,
                10f,
                70f
            );
            this.m_DrivingMeshScale = Mathf.Clamp(
                this.m_DrivingMeshScale,
                0.7f,
                0.98f
            );
            this.ResolveReferences();
        }
    }
}
