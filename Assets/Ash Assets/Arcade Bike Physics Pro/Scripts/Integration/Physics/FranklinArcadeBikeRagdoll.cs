using System;
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
        [Header("Ground Tunnelling Safety")]
        [SerializeField, Min(0.01f)] private float m_MaximumGroundPenetration = 0.12f;
        [SerializeField, Min(0f)] private float m_GroundSafetyClearance = 0.025f;
        [SerializeField, Min(1)] private int m_RagdollSolverIterations = 12;
        [SerializeField, Min(1)] private int m_RagdollSolverVelocityIterations = 4;

        private const string BODY_COLLIDER_PROXY_NAME =
            "Franklin Body Mesh Collider";
        // ABP suspension casts against bikeSettings.drivableLayerMask. The pack
        // integration already removes Unity's Ignore Raycast layer from that
        // mask, so body colliders on this layer still make physical contacts but
        // can never be mistaken for road by the bike's own wheel raycasts.
        private const int BODY_COLLIDER_QUERY_LAYER = 2;

        private readonly RaycastHit[] m_GroundHits = new RaycastHit[16];
        private readonly Collider[] m_WheelOverlapHits = new Collider[24];
        private Collider[] m_BodyAttachedColliders = Array.Empty<Collider>();
        private float m_StableGroundTimer;
        private int m_StableGroundSide;
        private int m_ParkedGroundSide;
        private Collider m_CachedSafetyGroundCollider;
        private Vector3 m_CachedSafetyGroundPoint;
        private Vector3 m_CachedSafetyGroundNormal = Vector3.up;

        public bool IsRagdoll { get; private set; }
        public bool RequiresManualRecovery { get; private set; }
        public int ParkedGroundSide => this.m_ParkedGroundSide;
        /// <summary>
        /// True when a nearby Player may be offered the lift/enter action. The
        /// interaction click itself parks the physical body, so compound ground
        /// and wall contacts cannot permanently hide the mobile button.
        /// </summary>
        public bool CanInteractWhileFallen
        {
            get
            {
                if (this.RequiresManualRecovery) return true;
                // Interaction itself takes ownership of the fallen Rigidbody
                // and parks it before recovery. Do not hide the mobile button
                // just because a wall, curb or MeshCollider prevents both strict
                // side probes from reporting ground.
                return this.IsRagdoll && this.m_Body != null;
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
            this.CacheBodyAttachedColliders();
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
            this.HardenDynamicBody();
            this.UpdateGroundSafetyCache();
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
        /// Transfers a live bike ragdoll into the kinematic manual-lift phase.
        /// Strict surface probes are preferred, with a rendered-side fallback for
        /// bikes wedged on curbs, walls or compound MeshColliders.
        /// </summary>
        public bool TryPrepareManualRecoveryForInteraction()
        {
            if (this.RequiresManualRecovery) return true;
            this.ResolveReferences();
            if (!this.IsRagdoll || this.m_Body == null) return false;

            int groundedSide = this.GetGroundedSide();
            if (groundedSide == 0)
            {
                // A bike wedged against a wall may never satisfy both surface
                // probes. Infer the lower rendered side and let the authored
                // mirrored recovery finish the job.
                groundedSide = Vector3.Dot(transform.right, Vector3.up) >= 0f
                    ? -1
                    : 1;
            }

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
            this.CacheBodyAttachedColliders();
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
            this.PreventGroundTunnelling();
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
            if (this.m_FrontWheelCollider != null &&
                this.m_FrontWheelCollider.enabled != enabled)
            {
                this.m_FrontWheelCollider.enabled = enabled;
            }
            if (this.m_RearWheelCollider != null &&
                this.m_RearWheelCollider.enabled != enabled)
            {
                this.m_RearWheelCollider.enabled = enabled;
            }
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

        public Vector3 GetCollisionSafeExitPosition(
            Vector3 authoredWorldPosition,
            Vector3 outwardDirection,
            float characterClearance)
        {
            outwardDirection = Vector3.ProjectOnPlane(
                outwardDirection,
                Vector3.up
            );
            if (outwardDirection.sqrMagnitude < 0.0001f)
            {
                return authoredWorldPosition;
            }
            outwardDirection.Normalize();

            bool foundBody = false;
            float furthestBodyProjection = float.NegativeInfinity;
            if (this.m_BodyMeshColliders != null)
            {
                foreach (MeshCollider meshCollider in this.m_BodyMeshColliders)
                {
                    if (meshCollider == null || !meshCollider.enabled) continue;
                    Bounds bounds = meshCollider.bounds;
                    float projection = Vector3.Dot(
                        bounds.center,
                        outwardDirection
                    ) + Vector3.Dot(
                        bounds.extents,
                        new Vector3(
                            Mathf.Abs(outwardDirection.x),
                            Mathf.Abs(outwardDirection.y),
                            Mathf.Abs(outwardDirection.z)
                        )
                    );
                    furthestBodyProjection = Mathf.Max(
                        furthestBodyProjection,
                        projection
                    );
                    foundBody = true;
                }
            }
            if (!foundBody) return authoredWorldPosition;

            float authoredProjection = Vector3.Dot(
                authoredWorldPosition,
                outwardDirection
            );
            float requiredProjection = furthestBodyProjection +
                                       Mathf.Max(0.05f, characterClearance);
            if (authoredProjection >= requiredProjection)
                return authoredWorldPosition;

            // Only move farther toward the authored left side. Never let a
            // collider-clearance correction cross the bike toward the right.
            return authoredWorldPosition + outwardDirection *
                   (requiredProjection - authoredProjection);
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
            this.HardenDynamicBody();
        }

        private void HardenDynamicBody()
        {
            if (this.m_Body == null || this.m_Body.isKinematic) return;
            this.m_Body.detectCollisions = true;
            this.m_Body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;
            this.m_Body.interpolation = RigidbodyInterpolation.Interpolate;
            this.m_Body.maxDepenetrationVelocity = Mathf.Max(
                this.m_Body.maxDepenetrationVelocity,
                12f
            );
            this.m_Body.solverIterations = Mathf.Max(
                this.m_Body.solverIterations,
                this.m_RagdollSolverIterations
            );
            this.m_Body.solverVelocityIterations = Mathf.Max(
                this.m_Body.solverVelocityIterations,
                this.m_RagdollSolverVelocityIterations
            );
        }

        private void UpdateGroundSafetyCache()
        {
            if (!this.TryGetSupportingGround(transform.position, out RaycastHit hit))
                return;
            this.m_CachedSafetyGroundCollider = hit.collider;
            this.m_CachedSafetyGroundPoint = hit.point;
            this.m_CachedSafetyGroundNormal = hit.normal.normalized;
        }

        private void PreventGroundTunnelling()
        {
            if (this.m_Body == null || this.m_Body.isKinematic) return;
            this.UpdateGroundSafetyCache();
            if (!this.IsSafetyGroundValidBelow()) return;

            Vector3 normal = this.m_CachedSafetyGroundNormal.sqrMagnitude > 0.001f
                ? this.m_CachedSafetyGroundNormal.normalized
                : Vector3.up;
            bool foundCollider = false;
            float lowestSurface = float.PositiveInfinity;
            if (this.m_BodyAttachedColliders.Length == 0)
                this.CacheBodyAttachedColliders();

            for (int index = 0; index < this.m_BodyAttachedColliders.Length; ++index)
            {
                Collider collider = this.m_BodyAttachedColliders[index];
                if (collider == null || !collider.enabled || collider.isTrigger ||
                    collider.attachedRigidbody != this.m_Body)
                {
                    continue;
                }
                Bounds bounds = collider.bounds;
                float surface = Vector3.Dot(bounds.center, normal) - Vector3.Dot(
                    bounds.extents,
                    new Vector3(
                        Mathf.Abs(normal.x),
                        Mathf.Abs(normal.y),
                        Mathf.Abs(normal.z)
                    )
                );
                lowestSurface = Mathf.Min(lowestSurface, surface);
                foundCollider = true;
            }
            if (!foundCollider) return;

            float groundPlane = Vector3.Dot(this.m_CachedSafetyGroundPoint, normal);
            float penetration = groundPlane + this.m_GroundSafetyClearance -
                                lowestSurface;
            if (penetration <= this.m_MaximumGroundPenetration) return;

            this.m_Body.position += normal * penetration;
            float inwardSpeed = Vector3.Dot(this.m_Body.linearVelocity, normal);
            if (inwardSpeed < 0f)
                this.m_Body.linearVelocity -= normal * inwardSpeed;
        }

        private void CacheBodyAttachedColliders()
        {
            if (this.m_Body == null)
            {
                this.m_BodyAttachedColliders = Array.Empty<Collider>();
                return;
            }

            Collider[] candidates = this.GetComponentsInChildren<Collider>(true);
            int count = 0;
            for (int index = 0; index < candidates.Length; ++index)
            {
                Collider collider = candidates[index];
                if (collider != null && !collider.isTrigger &&
                    collider.attachedRigidbody == this.m_Body)
                {
                    candidates[count++] = collider;
                }
            }

            if (count == candidates.Length)
            {
                this.m_BodyAttachedColliders = candidates;
                return;
            }

            this.m_BodyAttachedColliders = new Collider[count];
            Array.Copy(candidates, this.m_BodyAttachedColliders, count);
        }

        private bool IsSafetyGroundValidBelow()
        {
            Collider ground = this.m_CachedSafetyGroundCollider;
            if (ground == null || !ground.enabled) return false;
            Vector3 normal = this.m_CachedSafetyGroundNormal.sqrMagnitude > 0.001f
                ? this.m_CachedSafetyGroundNormal.normalized
                : Vector3.up;
            float signedDistance = Vector3.Dot(
                transform.position - this.m_CachedSafetyGroundPoint,
                normal
            );
            Vector3 projected = transform.position - normal * signedDistance;
            return Vector3.Distance(ground.ClosestPoint(projected), projected) <= 1f;
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

            Vector3 groundPoint;
            if (this.TryGetSupportingGround(transform.position, out RaycastHit groundHit))
            {
                groundNormal = groundHit.normal.normalized;
                groundPoint = groundHit.point;
            }
            else
            {
                // Forced recovery fallback for a bike resting on compound meshes,
                // curbs or beside a wall where the central ground ray is hidden.
                Vector3 localSurface = this.m_ParkedGroundSide < 0
                    ? this.m_LocalLeftSurface
                    : this.m_LocalRightSurface;
                groundPoint = transform.TransformPoint(localSurface);
                groundNormal = Vector3.up;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(transform.up, groundNormal);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, groundNormal);
            targetRotation = Quaternion.LookRotation(forward.normalized, groundNormal);

            targetPosition = this.AlignRecoveryHeight(
                targetPosition,
                targetRotation,
                groundPoint,
                groundNormal
            );
            return true;
        }

        public Vector3 AlignManualRecoveryTargetToGround(
            Vector3 desiredPosition,
            Quaternion targetRotation,
            Vector3 fallbackNormal)
        {
            Vector3 groundNormal = fallbackNormal.sqrMagnitude > 0.001f
                ? fallbackNormal.normalized
                : Vector3.up;
            if (!this.TryGetSupportingGround(desiredPosition, out RaycastHit groundHit))
                return desiredPosition;

            return this.AlignRecoveryHeight(
                desiredPosition,
                targetRotation,
                groundHit.point,
                groundHit.normal.sqrMagnitude > 0.001f
                    ? groundHit.normal.normalized
                    : groundNormal
            );
        }

        private Vector3 AlignRecoveryHeight(
            Vector3 targetPosition,
            Quaternion targetRotation,
            Vector3 groundPoint,
            Vector3 groundNormal)
        {

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
                targetPosition - groundPoint,
                groundNormal
            );
            targetPosition += groundNormal *
                              (desiredRootDistance - currentRootDistance);
            return targetPosition;
        }

        public void SetManualRecoveryPose(Vector3 position, Quaternion rotation)
        {
            if (!this.RequiresManualRecovery || this.m_Body == null) return;
            this.m_Body.position = position;
            this.m_Body.rotation = rotation;
            this.m_Body.linearVelocity = Vector3.zero;
            this.m_Body.angularVelocity = Vector3.zero;
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
            Physics.SyncTransforms();
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
            bool slowEnough = this.m_Body.linearVelocity.sqrMagnitude <=
                                  this.m_ParkLinearSpeed * this.m_ParkLinearSpeed &&
                              this.m_Body.angularVelocity.sqrMagnitude <=
                                  this.m_ParkAngularSpeed * this.m_ParkAngularSpeed;
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
            if (this.m_Body != null && !this.m_Body.isKinematic)
            {
                this.m_Body.linearVelocity = Vector3.zero;
                this.m_Body.angularVelocity = Vector3.zero;
                this.m_Body.isKinematic = true;
            }
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
            this.m_MaximumGroundPenetration = Mathf.Max(
                0.01f,
                this.m_MaximumGroundPenetration
            );
            this.m_GroundSafetyClearance = Mathf.Max(0f, this.m_GroundSafetyClearance);
            this.m_RagdollSolverIterations = Mathf.Max(
                1,
                this.m_RagdollSolverIterations
            );
            this.m_RagdollSolverVelocityIterations = Mathf.Max(
                1,
                this.m_RagdollSolverVelocityIterations
            );
            this.ResolveReferences();
        }
    }
}
