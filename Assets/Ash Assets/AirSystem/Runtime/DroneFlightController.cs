using System;
using System.Collections.Generic;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// Fixed-step flight motor shared by desktop, gamepad and touch input.
    /// Return breadcrumbs are pooled, and parked drones leave the physics loop.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class DroneFlightController : MonoBehaviour
    {
        private static readonly List<DroneFlightController> INSTANCES = new(4);

        [SerializeField] private Rigidbody m_Body;
        [SerializeField] private ShotCamera m_CameraShot;
        [SerializeField] private Transform m_CameraPivot;
        [SerializeField] private DroneRotorVisuals m_RotorVisuals;
        [SerializeField] private AudioSource m_MotorAudio;

        [Header("Flight")]
        [SerializeField, Min(1f)] private float m_MaxHorizontalSpeed = 18f;
        [SerializeField, Min(1f)] private float m_MaxVerticalSpeed = 8f;
        [SerializeField, Min(0.1f)] private float m_Acceleration = 16f;
        [SerializeField, Min(0.1f)] private float m_Braking = 12f;
        [SerializeField, Min(1f)] private float m_YawSpeed = 105f;
        [SerializeField, Range(0f, 45f)] private float m_MaxPitch = 12f;
        [SerializeField, Range(0f, 45f)] private float m_MaxRoll = 15f;
        [SerializeField, Min(1f)] private float m_RotationSpeed = 180f;
        [SerializeField, Min(0.1f)] private float m_TiltEaseSpeed = 7f;
        [SerializeField] private Easing.Type m_TiltEasing = Easing.Type.SineOut;

        [Header("Camera Stability")]
        [SerializeField, Min(0f)] private float m_CameraPivotHeight = 0.18f;

        [Header("Motor Audio")]
        [SerializeField, Range(0f, 1f)] private float m_IdleMotorVolume = 0.24f;
        [SerializeField, Range(0f, 1f)] private float m_FlightMotorVolume = 0.58f;
        [SerializeField, Range(0.1f, 3f)] private float m_IdleMotorPitch = 0.85f;
        [SerializeField, Range(0.1f, 3f)] private float m_FlightMotorPitch = 1.22f;

        [Header("Return Home")]
        [SerializeField, Min(0.5f)] private float m_MarkerInterval = 3f;
        [SerializeField, Range(8, 96)] private int m_MaxReturnMarkers = 48;
        [SerializeField, Min(0f)] private float m_MinMarkerDistance = 0.75f;
        [SerializeField, Min(1f)] private float m_ReturnSpeed = 24f;
        [SerializeField, Min(0.1f)] private float m_ReturnAcceleration = 22f;
        [SerializeField, Min(0.1f)] private float m_ReturnArrivalDistance = 0.75f;
        [SerializeField, Min(1f)] private float m_ReturnSlowdownDistance = 25f;
        [SerializeField, Min(0f)] private float m_ReturnMinimumSpeed = 1.5f;
        [SerializeField] private Easing.Type m_ReturnSlowdownEasing = Easing.Type.QuadInOut;

        [Header("Return Obstacle Avoidance")]
        [SerializeField, Min(0.05f)] private float m_AvoidanceRadius = 0.32f;
        [SerializeField, Min(0.5f)] private float m_AvoidanceLookAhead = 5.5f;
        [SerializeField, Range(0.05f, 1f)] private float m_AvoidanceHoldTime = 0.3f;
        [SerializeField] private LayerMask m_ReturnObstacleMask =
            ~((1 << 2) | (1 << 3) | (1 << 11));

        private const int AVOIDANCE_HIT_CAPACITY = 8;

        private DronePlayerController m_Owner;
        private Vector2 m_MoveInput;
        private float m_YawInput;
        private float m_LiftInput;
        private float m_TargetYaw;
        private Vector2 m_CurrentTilt;
        private Vector3 m_CameraForward;
        private bool m_HasCameraForward;
        private Marker[] m_ReturnMarkers;
        private Transform m_MarkerPoolRoot;
        private readonly RaycastHit[] m_AvoidanceHits = new RaycastHit[AVOIDANCE_HIT_CAPACITY];
        private int m_ReturnMarkerCount;
        private int m_ReturnMarkerIndex;
        private float m_MarkerTimer;
        private float m_AvoidanceHoldRemaining;
        private Vector3 m_AvoidanceDirection;
        private bool m_IsReturning;

        public static IReadOnlyList<DroneFlightController> Instances => INSTANCES;

        public ShotCamera CameraShot => this.m_CameraShot;
        public bool IsPiloted => this.m_Owner != null;
        public bool IsReturningHome => this.m_IsReturning;
        public bool IsAvailable => this.isActiveAndEnabled && this.m_Owner == null &&
                                   !this.m_IsReturning &&
                                   this.m_Body != null && this.m_CameraShot != null;

        public event Action<DroneFlightController> EventControlLost;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            INSTANCES.Clear();
        }

        private void Awake()
        {
            if (this.m_Body == null) this.m_Body = this.GetComponent<Rigidbody>();
            if (this.m_RotorVisuals == null)
            {
                this.m_RotorVisuals = this.GetComponent<DroneRotorVisuals>();
            }
            this.m_ReturnMarkers = new Marker[Mathf.Clamp(this.m_MaxReturnMarkers, 8, 96)];
            this.ConfigureParkedState();
        }

        private void OnEnable()
        {
            if (!INSTANCES.Contains(this)) INSTANCES.Add(this);
            if (!this.IsPiloted) this.ConfigureParkedState();
        }

        private void OnDisable()
        {
            INSTANCES.Remove(this);
            bool hadOwner = this.m_Owner != null;
            this.m_Owner = null;
            this.ClearInput();
            this.m_IsReturning = false;
            this.ClearReturnPath();
            this.ConfigureParkedState();
            if (hadOwner) this.EventControlLost?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (this.m_MarkerPoolRoot != null)
            {
                Destroy(this.m_MarkerPoolRoot.gameObject);
                this.m_MarkerPoolRoot = null;
            }
        }

        private void OnValidate()
        {
            if (this.m_Body == null) this.m_Body = this.GetComponent<Rigidbody>();
            if (this.m_RotorVisuals == null)
            {
                this.m_RotorVisuals = this.GetComponent<DroneRotorVisuals>();
            }
            this.m_MaxHorizontalSpeed = Mathf.Max(1f, this.m_MaxHorizontalSpeed);
            this.m_MaxVerticalSpeed = Mathf.Max(1f, this.m_MaxVerticalSpeed);
            this.m_Acceleration = Mathf.Max(0.1f, this.m_Acceleration);
            this.m_Braking = Mathf.Max(0.1f, this.m_Braking);
            this.m_YawSpeed = Mathf.Max(1f, this.m_YawSpeed);
            this.m_RotationSpeed = Mathf.Max(1f, this.m_RotationSpeed);
            this.m_TiltEaseSpeed = Mathf.Max(0.1f, this.m_TiltEaseSpeed);
            this.m_CameraPivotHeight = Mathf.Max(0f, this.m_CameraPivotHeight);
            this.m_IdleMotorVolume = Mathf.Clamp01(this.m_IdleMotorVolume);
            this.m_FlightMotorVolume = Mathf.Clamp01(this.m_FlightMotorVolume);
            this.m_IdleMotorPitch = Mathf.Clamp(this.m_IdleMotorPitch, 0.1f, 3f);
            this.m_FlightMotorPitch = Mathf.Clamp(this.m_FlightMotorPitch, 0.1f, 3f);
            this.m_MarkerInterval = Mathf.Max(0.5f, this.m_MarkerInterval);
            this.m_MaxReturnMarkers = Mathf.Clamp(this.m_MaxReturnMarkers, 8, 96);
            this.m_MinMarkerDistance = Mathf.Max(0f, this.m_MinMarkerDistance);
            this.m_ReturnSpeed = Mathf.Max(1f, this.m_ReturnSpeed);
            this.m_ReturnAcceleration = Mathf.Max(0.1f, this.m_ReturnAcceleration);
            this.m_ReturnArrivalDistance = Mathf.Max(0.1f, this.m_ReturnArrivalDistance);
            this.m_ReturnSlowdownDistance = Mathf.Max(1f, this.m_ReturnSlowdownDistance);
            this.m_ReturnMinimumSpeed = Mathf.Clamp(
                this.m_ReturnMinimumSpeed,
                0f,
                this.m_ReturnSpeed
            );
            this.m_AvoidanceRadius = Mathf.Max(0.05f, this.m_AvoidanceRadius);
            this.m_AvoidanceLookAhead = Mathf.Max(0.5f, this.m_AvoidanceLookAhead);
            this.m_AvoidanceHoldTime = Mathf.Clamp(this.m_AvoidanceHoldTime, 0.05f, 1f);
        }

        private void FixedUpdate()
        {
            if (this.m_Body == null || this.m_Body.isKinematic) return;

            float deltaTime = Time.fixedDeltaTime;
            if (this.IsPiloted)
            {
                this.UpdatePilotedFlight(deltaTime);
                this.SampleReturnMarker(deltaTime);
                return;
            }

            if (this.m_IsReturning) this.UpdateReturnFlight(deltaTime);
        }

        private void UpdatePilotedFlight(float deltaTime)
        {
            Vector3 cameraForward = this.m_HasCameraForward
                ? this.m_CameraForward
                : this.transform.forward;
            if (cameraForward.sqrMagnitude < 0.0001f) cameraForward = Vector3.forward;
            else cameraForward.Normalize();

            Vector3 planarForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (planarForward.sqrMagnitude < 0.0001f)
            {
                planarForward = Vector3.ProjectOnPlane(this.transform.forward, Vector3.up);
            }
            if (planarForward.sqrMagnitude < 0.0001f) planarForward = Vector3.forward;
            else planarForward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, planarForward).normalized;
            Vector3 desiredVelocity =
                cameraForward * (this.m_MoveInput.y * this.m_MaxHorizontalSpeed) +
                right * (this.m_MoveInput.x * this.m_MaxHorizontalSpeed) +
                Vector3.up * (this.m_LiftInput * this.m_MaxVerticalSpeed);

            Vector3 currentVelocity = this.m_Body.linearVelocity;
            bool hasMotionInput = this.m_MoveInput.sqrMagnitude > 0.0001f ||
                                  Mathf.Abs(this.m_LiftInput) > 0.01f;
            float response = hasMotionInput ? this.m_Acceleration : this.m_Braking;
            Vector3 nextVelocity = Vector3.MoveTowards(
                currentVelocity,
                desiredVelocity,
                response * deltaTime
            );

            Vector2 horizontal = new(nextVelocity.x, nextVelocity.z);
            if (horizontal.sqrMagnitude > this.m_MaxHorizontalSpeed * this.m_MaxHorizontalSpeed)
            {
                horizontal = horizontal.normalized * this.m_MaxHorizontalSpeed;
                nextVelocity.x = horizontal.x;
                nextVelocity.z = horizontal.y;
            }
            nextVelocity.y = Mathf.Clamp(
                nextVelocity.y,
                -this.m_MaxVerticalSpeed,
                this.m_MaxVerticalSpeed
            );
            if (!hasMotionInput && nextVelocity.sqrMagnitude < 0.0004f)
            {
                nextVelocity = Vector3.zero;
            }
            this.m_Body.linearVelocity = nextVelocity;
            this.m_Body.angularVelocity = Vector3.zero;

            if (this.m_MoveInput.y > 0.05f && this.m_HasCameraForward)
            {
                this.m_TargetYaw = Mathf.Atan2(planarForward.x, planarForward.z) *
                                   Mathf.Rad2Deg;
            }
            this.m_TargetYaw = Mathf.Repeat(
                this.m_TargetYaw + this.m_YawInput * this.m_YawSpeed * deltaTime,
                360f
            );

            Vector2 targetTilt = new(
                this.m_MoveInput.y * this.m_MaxPitch,
                -this.m_MoveInput.x * this.m_MaxRoll
            );
            float tiltProgress = Mathf.Clamp01(this.m_TiltEaseSpeed * deltaTime);
            float tiltEase = Easing.GetEase(
                this.m_TiltEasing,
                0f,
                1f,
                tiltProgress
            );
            this.m_CurrentTilt = Vector2.LerpUnclamped(
                this.m_CurrentTilt,
                targetTilt,
                tiltEase
            );
            Quaternion targetRotation = Quaternion.Euler(
                this.m_CurrentTilt.x,
                this.m_TargetYaw,
                this.m_CurrentTilt.y
            );
            this.m_Body.MoveRotation(Quaternion.RotateTowards(
                this.m_Body.rotation,
                targetRotation,
                this.m_RotationSpeed * deltaTime
            ));

            this.UpdateMotorAudio(nextVelocity, deltaTime);
        }

        private void LateUpdate()
        {
            if (!this.IsPiloted || this.m_CameraPivot == null) return;

            Vector3 yawForward = Vector3.ProjectOnPlane(this.transform.forward, Vector3.up);
            if (yawForward.sqrMagnitude < 0.0001f) yawForward = Vector3.forward;
            else yawForward.Normalize();

            this.m_CameraPivot.SetPositionAndRotation(
                this.transform.position + Vector3.up * this.m_CameraPivotHeight,
                Quaternion.LookRotation(yawForward, Vector3.up)
            );
        }

        public bool TryAcquire(DronePlayerController owner)
        {
            if (owner == null || !this.IsAvailable || this.m_Body == null) return false;

            this.m_Owner = owner;
            this.m_IsReturning = false;
            this.ClearInput();
            this.m_TargetYaw = this.transform.eulerAngles.y;
            this.m_CurrentTilt = Vector2.zero;
            this.m_CameraShot.enabled = true;
            if (this.m_RotorVisuals != null) this.m_RotorVisuals.enabled = true;
            this.StartMotorAudio();

            this.m_Body.isKinematic = false;
            this.m_Body.useGravity = false;
            this.m_Body.interpolation = RigidbodyInterpolation.Interpolate;
            this.m_Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            this.m_Body.constraints = RigidbodyConstraints.None;
            this.m_Body.maxAngularVelocity = 7f;
            this.m_Body.WakeUp();
            this.BeginReturnPath();
            return true;
        }

        public bool Release(DronePlayerController owner)
        {
            if (owner == null || this.m_Owner != owner) return false;

            this.m_Owner = null;
            this.ClearInput();
            this.RecordReturnMarker(true);
            this.BeginReturnHome();
            return true;
        }

        public void SetInput(Vector2 move, float yaw, float lift, Vector3 cameraForward)
        {
            if (!this.IsPiloted) return;
            this.m_MoveInput = Vector2.ClampMagnitude(move, 1f);
            this.m_YawInput = Mathf.Clamp(yaw, -1f, 1f);
            this.m_LiftInput = Mathf.Clamp(lift, -1f, 1f);
            this.m_CameraForward = cameraForward;
            this.m_HasCameraForward = cameraForward.sqrMagnitude > 0.0001f;
        }

        public void ClearInput()
        {
            this.m_MoveInput = Vector2.zero;
            this.m_YawInput = 0f;
            this.m_LiftInput = 0f;
            this.m_HasCameraForward = false;
        }

        private void BeginReturnPath()
        {
            int capacity = Mathf.Clamp(this.m_MaxReturnMarkers, 8, 96);
            if (this.m_ReturnMarkers == null || this.m_ReturnMarkers.Length != capacity)
            {
                this.m_ReturnMarkers = new Marker[capacity];
            }

            this.ClearReturnPath();
            this.RecordReturnMarker(true);
        }

        private void SampleReturnMarker(float deltaTime)
        {
            float interval = Mathf.Max(0.5f, this.m_MarkerInterval);
            this.m_MarkerTimer += deltaTime;
            if (this.m_MarkerTimer < interval) return;

            this.m_MarkerTimer %= interval;
            this.RecordReturnMarker(false);
        }

        private void RecordReturnMarker(bool force)
        {
            if (this.m_ReturnMarkers == null || this.m_ReturnMarkers.Length == 0) return;

            if (!force && this.m_ReturnMarkerCount > 0)
            {
                Marker previous = this.m_ReturnMarkers[this.m_ReturnMarkerCount - 1];
                if (previous != null)
                {
                    float minimumDistanceSquared = this.m_MinMarkerDistance *
                                                   this.m_MinMarkerDistance;
                    if ((previous.transform.position - this.transform.position).sqrMagnitude <
                        minimumDistanceSquared)
                    {
                        return;
                    }
                }
            }

            if (this.m_ReturnMarkerCount >= this.m_ReturnMarkers.Length)
            {
                this.CompactReturnMarkers();
            }

            int index = this.m_ReturnMarkerCount;
            Marker marker = this.GetOrCreateReturnMarker(index);
            if (marker == null) return;

            marker.transform.SetPositionAndRotation(
                this.transform.position,
                Quaternion.Euler(0f, this.transform.eulerAngles.y, 0f)
            );
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            this.m_ReturnMarkerCount = index + 1;
        }

        private Marker GetOrCreateReturnMarker(int index)
        {
            Marker marker = this.m_ReturnMarkers[index];
            if (marker != null) return marker;

            if (this.m_MarkerPoolRoot == null)
            {
                GameObject root = new($"{this.name} Return Markers");
                root.layer = this.gameObject.layer;
                root.hideFlags = HideFlags.HideInHierarchy;
                this.m_MarkerPoolRoot = root.transform;
            }

            GameObject markerObject = new($"Return Marker {index:00} (GC2)");
            markerObject.layer = this.gameObject.layer;
            markerObject.transform.SetParent(this.m_MarkerPoolRoot, false);
            marker = markerObject.AddComponent<Marker>();

            // These are private breadcrumbs, not world-query targets. Keeping
            // them out of GC2's global spatial hash avoids increasing the cost
            // of unrelated marker searches as a flight becomes longer.
            SpatialHashMarkers.Remove(marker);
            markerObject.SetActive(false);
            this.m_ReturnMarkers[index] = marker;
            return marker;
        }

        private void CompactReturnMarkers()
        {
            int oldCount = this.m_ReturnMarkerCount;
            if (oldCount < 3) return;

            int write = 1;
            for (int read = 2; read < oldCount - 1; read += 2)
            {
                this.CopyReturnMarker(read, write++);
            }

            this.CopyReturnMarker(oldCount - 1, write++);
            for (int i = write; i < oldCount; ++i)
            {
                Marker unused = this.m_ReturnMarkers[i];
                if (unused != null && unused.gameObject.activeSelf)
                {
                    unused.gameObject.SetActive(false);
                }
            }
            this.m_ReturnMarkerCount = write;
        }

        private void CopyReturnMarker(int sourceIndex, int destinationIndex)
        {
            Marker source = this.m_ReturnMarkers[sourceIndex];
            Marker destination = this.GetOrCreateReturnMarker(destinationIndex);
            if (source == null || destination == null) return;

            destination.transform.SetPositionAndRotation(
                source.transform.position,
                source.transform.rotation
            );
            if (!destination.gameObject.activeSelf) destination.gameObject.SetActive(true);
        }

        private void BeginReturnHome()
        {
            if (this.m_ReturnMarkerCount < 2 || this.m_Body == null)
            {
                this.ClearReturnPath();
                this.ConfigureParkedState();
                return;
            }

            if (this.m_CameraShot != null) this.m_CameraShot.enabled = false;
            if (this.m_RotorVisuals != null) this.m_RotorVisuals.enabled = true;
            this.StartMotorAudio();

            this.m_IsReturning = true;
            this.m_ReturnMarkerIndex = this.m_ReturnMarkerCount - 2;
            this.m_AvoidanceDirection = Vector3.zero;
            this.m_AvoidanceHoldRemaining = 0f;
            this.m_TargetYaw = this.transform.eulerAngles.y;
            this.m_CurrentTilt = Vector2.zero;

            this.m_Body.isKinematic = false;
            this.m_Body.useGravity = false;
            this.m_Body.interpolation = RigidbodyInterpolation.Interpolate;
            this.m_Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            this.m_Body.constraints = RigidbodyConstraints.None;
            this.m_Body.WakeUp();
        }

        private void UpdateReturnFlight(float deltaTime)
        {
            Vector3 target = Vector3.zero;
            float distance = 0f;
            while (this.m_ReturnMarkerIndex >= 0)
            {
                Marker marker = this.m_ReturnMarkers[this.m_ReturnMarkerIndex];
                if (marker == null)
                {
                    --this.m_ReturnMarkerIndex;
                    continue;
                }

                target = marker.GetPosition(this.gameObject);
                distance = Vector3.Distance(this.m_Body.position, target);
                if (distance > this.m_ReturnArrivalDistance) break;

                if (this.m_ReturnMarkerIndex == 0)
                {
                    this.CompleteReturnHome(marker);
                    return;
                }
                --this.m_ReturnMarkerIndex;
            }

            if (this.m_ReturnMarkerIndex < 0)
            {
                this.ConfigureParkedState();
                return;
            }

            Vector3 toTarget = target - this.m_Body.position;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            Vector3 directDirection = toTarget.normalized;
            Vector3 travelDirection = this.ResolveReturnDirection(
                directDirection,
                this.m_Body.linearVelocity.magnitude,
                deltaTime
            );

            float desiredSpeed = this.m_ReturnSpeed;
            float safeAcceleration = Mathf.Max(0.1f, this.m_ReturnAcceleration);
            if (this.m_ReturnMarkerIndex == 0)
            {
                float slowdownProgress = Mathf.Clamp01(
                    distance / Mathf.Max(1f, this.m_ReturnSlowdownDistance)
                );
                float easedProgress = Easing.GetEase(
                    this.m_ReturnSlowdownEasing,
                    0f,
                    1f,
                    slowdownProgress
                );
                float approachSpeed = Mathf.Lerp(
                    this.m_ReturnMinimumSpeed,
                    this.m_ReturnSpeed,
                    easedProgress
                );
                float stoppingSpeed = Mathf.Sqrt(
                    2f * safeAcceleration * Mathf.Max(0f, distance)
                );
                desiredSpeed = Mathf.Min(desiredSpeed, approachSpeed, stoppingSpeed);
            }
            else
            {
                Marker nextMarker = this.m_ReturnMarkers[this.m_ReturnMarkerIndex - 1];
                if (nextMarker != null)
                {
                    Vector3 afterCorner = nextMarker.GetPosition(this.gameObject) - target;
                    if (afterCorner.sqrMagnitude > 0.0001f)
                    {
                        afterCorner.Normalize();
                        float cornerAlignment = Vector3.Dot(directDirection, afterCorner);
                        float cornerBlend = Mathf.InverseLerp(-0.2f, 0.85f, cornerAlignment);
                        float cornerSpeed = Mathf.Lerp(5f, this.m_ReturnSpeed, cornerBlend);
                        float allowedSpeed = Mathf.Sqrt(
                            cornerSpeed * cornerSpeed +
                            2f * safeAcceleration * Mathf.Max(0f, distance)
                        );
                        desiredSpeed = Mathf.Min(desiredSpeed, allowedSpeed);
                    }
                }
            }

            Vector3 desiredVelocity = travelDirection * desiredSpeed;
            Vector3 nextVelocity = Vector3.MoveTowards(
                this.m_Body.linearVelocity,
                desiredVelocity,
                this.m_ReturnAcceleration * deltaTime
            );
            if (nextVelocity.sqrMagnitude > this.m_ReturnSpeed * this.m_ReturnSpeed)
            {
                nextVelocity = nextVelocity.normalized * this.m_ReturnSpeed;
            }

            this.m_Body.linearVelocity = nextVelocity;
            this.m_Body.angularVelocity = Vector3.zero;
            this.UpdateReturnRotation(travelDirection, nextVelocity, deltaTime);
            this.UpdateMotorAudio(nextVelocity, deltaTime);
        }

        private Vector3 ResolveReturnDirection(
            Vector3 directDirection,
            float currentSpeed,
            float deltaTime
        )
        {
            float safeAcceleration = Mathf.Max(0.1f, this.m_ReturnAcceleration);
            float stoppingDistance = currentSpeed * currentSpeed /
                                     (2f * safeAcceleration);
            float castDistance = Mathf.Max(
                this.m_AvoidanceLookAhead,
                stoppingDistance + this.m_AvoidanceRadius * 2f
            );
            float directClearance = this.GetReturnClearance(directDirection, castDistance);
            if (currentSpeed > 0.5f)
            {
                Vector3 velocityDirection = this.m_Body.linearVelocity / currentSpeed;
                if (Vector3.Dot(velocityDirection, directDirection) < 0.95f)
                {
                    directClearance = Mathf.Min(
                        directClearance,
                        this.GetReturnClearance(velocityDirection, castDistance)
                    );
                }
            }

            if (directClearance >= castDistance)
            {
                if (this.m_AvoidanceHoldRemaining > 0f &&
                    this.m_AvoidanceDirection.sqrMagnitude > 0.5f)
                {
                    this.m_AvoidanceHoldRemaining = Mathf.Max(
                        0f,
                        this.m_AvoidanceHoldRemaining - deltaTime
                    );
                    float heldClearance = this.GetReturnClearance(
                        this.m_AvoidanceDirection,
                        castDistance * 0.7f
                    );
                    if (heldClearance >= castDistance * 0.7f)
                    {
                        return this.m_AvoidanceDirection;
                    }
                }

                this.m_AvoidanceHoldRemaining = 0f;
                return directDirection;
            }

            if (this.m_AvoidanceHoldRemaining > 0f &&
                this.m_AvoidanceDirection.sqrMagnitude > 0.5f)
            {
                this.m_AvoidanceHoldRemaining = Mathf.Max(
                    0f,
                    this.m_AvoidanceHoldRemaining - deltaTime
                );
                float heldClearance = this.GetReturnClearance(
                    this.m_AvoidanceDirection,
                    castDistance
                );
                if (heldClearance > this.m_AvoidanceRadius * 2f)
                {
                    return this.m_AvoidanceDirection;
                }
            }

            Vector3 right = Vector3.Cross(Vector3.up, directDirection);
            if (right.sqrMagnitude < 0.01f) right = this.transform.right;
            else right.Normalize();
            Vector3 up = Vector3.Cross(directDirection, right).normalized;

            Vector3 bestDirection = directDirection;
            float bestScore = directClearance;
            this.ScoreReturnDirection(
                (directDirection + right * 0.9f).normalized,
                directDirection,
                castDistance,
                ref bestDirection,
                ref bestScore
            );
            this.ScoreReturnDirection(
                (directDirection - right * 0.9f).normalized,
                directDirection,
                castDistance,
                ref bestDirection,
                ref bestScore
            );
            this.ScoreReturnDirection(
                (directDirection + up * 0.9f).normalized,
                directDirection,
                castDistance,
                ref bestDirection,
                ref bestScore
            );
            this.ScoreReturnDirection(
                (directDirection - up * 0.65f).normalized,
                directDirection,
                castDistance,
                ref bestDirection,
                ref bestScore
            );

            this.m_AvoidanceDirection = bestDirection;
            this.m_AvoidanceHoldRemaining = this.m_AvoidanceHoldTime;
            return bestDirection;
        }

        private void ScoreReturnDirection(
            Vector3 candidate,
            Vector3 directDirection,
            float castDistance,
            ref Vector3 bestDirection,
            ref float bestScore
        )
        {
            float clearance = this.GetReturnClearance(candidate, castDistance);
            float alignment = Mathf.Max(0f, Vector3.Dot(candidate, directDirection));
            float score = clearance + alignment * castDistance * 0.15f;
            if (score <= bestScore) return;

            bestScore = score;
            bestDirection = candidate;
        }

        private float GetReturnClearance(Vector3 direction, float castDistance)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                this.m_Body.worldCenterOfMass,
                this.m_AvoidanceRadius,
                direction,
                this.m_AvoidanceHits,
                castDistance,
                this.m_ReturnObstacleMask,
                QueryTriggerInteraction.Ignore
            );

            float nearestDistance = castDistance;
            for (int i = 0; i < hitCount; ++i)
            {
                Collider hitCollider = this.m_AvoidanceHits[i].collider;
                if (hitCollider == null || hitCollider.attachedRigidbody == this.m_Body ||
                    hitCollider.transform.IsChildOf(this.transform))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(
                    nearestDistance,
                    this.m_AvoidanceHits[i].distance
                );
            }
            return nearestDistance;
        }

        private void UpdateReturnRotation(
            Vector3 travelDirection,
            Vector3 velocity,
            float deltaTime
        )
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(travelDirection, Vector3.up);
            if (planarDirection.sqrMagnitude > 0.0001f)
            {
                planarDirection.Normalize();
                this.m_TargetYaw = Mathf.Atan2(planarDirection.x, planarDirection.z) *
                                   Mathf.Rad2Deg;
            }

            float speedRatio = Mathf.Clamp01(velocity.magnitude / this.m_ReturnSpeed);
            float lateral = Vector3.Dot(travelDirection, this.transform.right);
            Vector2 targetTilt = new(
                speedRatio * this.m_MaxPitch,
                -lateral * this.m_MaxRoll * 0.6f
            );
            float tiltProgress = Mathf.Clamp01(this.m_TiltEaseSpeed * deltaTime);
            float tiltEase = Easing.GetEase(
                this.m_TiltEasing,
                0f,
                1f,
                tiltProgress
            );
            this.m_CurrentTilt = Vector2.LerpUnclamped(
                this.m_CurrentTilt,
                targetTilt,
                tiltEase
            );

            Quaternion targetRotation = Quaternion.Euler(
                this.m_CurrentTilt.x,
                this.m_TargetYaw,
                this.m_CurrentTilt.y
            );
            this.m_Body.MoveRotation(Quaternion.RotateTowards(
                this.m_Body.rotation,
                targetRotation,
                this.m_RotationSpeed * deltaTime
            ));
        }

        private void CompleteReturnHome(Marker home)
        {
            Vector3 homePosition = home.GetPosition(this.gameObject);
            float homeYaw = home.GetRotation(this.gameObject).eulerAngles.y;
            this.m_Body.position = homePosition;
            this.m_Body.rotation = Quaternion.Euler(0f, homeYaw, 0f);
            this.m_Body.linearVelocity = Vector3.zero;
            this.m_Body.angularVelocity = Vector3.zero;
            this.ClearReturnPath();
            this.ConfigureParkedState();
        }

        private void ClearReturnPath()
        {
            if (this.m_ReturnMarkers != null)
            {
                for (int i = 0; i < this.m_ReturnMarkers.Length; ++i)
                {
                    Marker marker = this.m_ReturnMarkers[i];
                    if (marker != null && marker.gameObject.activeSelf)
                    {
                        marker.gameObject.SetActive(false);
                    }
                }
            }

            this.m_ReturnMarkerCount = 0;
            this.m_ReturnMarkerIndex = -1;
            this.m_MarkerTimer = 0f;
            this.m_AvoidanceHoldRemaining = 0f;
            this.m_AvoidanceDirection = Vector3.zero;
        }

        private void ConfigureParkedState()
        {
            this.m_IsReturning = false;
            if (this.m_CameraShot != null) this.m_CameraShot.enabled = false;
            if (this.m_RotorVisuals != null) this.m_RotorVisuals.enabled = false;
            this.StopMotorAudio();
            if (this.m_Body == null) return;

            if (!this.m_Body.isKinematic)
            {
                this.m_Body.linearVelocity = Vector3.zero;
                this.m_Body.angularVelocity = Vector3.zero;
            }
            Vector3 uprightEuler = this.m_Body.rotation.eulerAngles;
            this.m_Body.rotation = Quaternion.Euler(0f, uprightEuler.y, 0f);
            this.m_CurrentTilt = Vector2.zero;
            this.m_Body.useGravity = false;
            this.m_Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            this.m_Body.interpolation = RigidbodyInterpolation.None;
            this.m_Body.isKinematic = true;

            if (this.m_CameraPivot != null)
            {
                this.m_CameraPivot.localPosition = Vector3.up * this.m_CameraPivotHeight;
                this.m_CameraPivot.localRotation = Quaternion.identity;
            }
        }

        private void StartMotorAudio()
        {
            if (this.m_MotorAudio == null || this.m_MotorAudio.clip == null) return;

            this.m_MotorAudio.loop = true;
            this.m_MotorAudio.volume = this.m_IdleMotorVolume;
            this.m_MotorAudio.pitch = this.m_IdleMotorPitch;
            if (!this.m_MotorAudio.isPlaying) this.m_MotorAudio.Play();
        }

        private void StopMotorAudio()
        {
            if (this.m_MotorAudio != null && this.m_MotorAudio.isPlaying)
            {
                this.m_MotorAudio.Stop();
            }
        }

        private void UpdateMotorAudio(Vector3 velocity, float deltaTime)
        {
            if (this.m_MotorAudio == null || !this.m_MotorAudio.isPlaying) return;

            float inputLoad = Mathf.Max(
                Mathf.Max(this.m_MoveInput.magnitude, Mathf.Abs(this.m_LiftInput)),
                Mathf.Abs(this.m_YawInput) * 0.65f
            );
            float maximumSpeed = Mathf.Sqrt(
                this.m_MaxHorizontalSpeed * this.m_MaxHorizontalSpeed +
                this.m_MaxVerticalSpeed * this.m_MaxVerticalSpeed
            );
            float speedLoad = maximumSpeed > 0.001f
                ? velocity.magnitude / maximumSpeed
                : 0f;
            float motorLoad = Mathf.Clamp01(Mathf.Max(inputLoad, speedLoad));
            float blend = 1f - Mathf.Exp(-8f * deltaTime);

            float targetVolume = Mathf.Lerp(
                this.m_IdleMotorVolume,
                this.m_FlightMotorVolume,
                motorLoad
            );
            float targetPitch = Mathf.Lerp(
                this.m_IdleMotorPitch,
                this.m_FlightMotorPitch,
                motorLoad
            );
            this.m_MotorAudio.volume = Mathf.Lerp(
                this.m_MotorAudio.volume,
                targetVolume,
                blend
            );
            this.m_MotorAudio.pitch = Mathf.Lerp(
                this.m_MotorAudio.pitch,
                targetPitch,
                blend
            );
        }
    }
}
