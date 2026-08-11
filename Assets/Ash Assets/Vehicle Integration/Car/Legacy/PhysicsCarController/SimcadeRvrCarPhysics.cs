using UnityEngine;

/// <summary>
/// A mobile-friendly integration of the Sim-Cade Vehicle Physics suspension,
/// tire friction and Ackermann steering model with the RVR vehicle framework.
///
/// It deliberately consumes the RVR controller's input and reports telemetry
/// back to it. This keeps the existing CarEntry, vehicle UI, audio, damage and
/// Game Creator instructions working, while only cars opting into External
/// Physics use this component.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(PhysicsCarController))]
public sealed class SimcadeRvrCarPhysics : MonoBehaviour
{
    private const int WHEEL_COUNT = 4;
    private const int FRONT_LEFT = 0;
    private const int FRONT_RIGHT = 1;
    private const int REAR_LEFT = 2;
    private const int REAR_RIGHT = 3;

    [Header("RVR Bridge")]
    [SerializeField] private PhysicsCarController m_RvrController;
    [SerializeField] private Transform m_CenterOfMass;

    [Header("Sim-Cade Suspension")]
    [SerializeField, Min(0.05f)] private float m_WheelRadius = 0.30f;
    [SerializeField, Min(0.05f)] private float m_SuspensionDistance = 1.25f;
    [SerializeField, Min(0f)] private float m_SpringForce = 30000f;
    [SerializeField, Min(0f)] private float m_SpringDamper = 2200f;
    [SerializeField] private LayerMask m_DrivableLayers = ~0;

    [Header("Sim-Cade Driving")]
    [SerializeField, Min(1f)] private float m_MaxSpeedKph = 160f;
    [SerializeField, Min(0f)] private float m_Acceleration = 28f;
    [SerializeField, Min(0f)] private float m_BrakeAcceleration = 52f;
    [SerializeField, Min(0f)] private float m_RollingResistance = 1.8f;
    [SerializeField, Range(1f, 60f)] private float m_MaxTurnAngle = 30f;
    [SerializeField, Range(0.05f, 2f)] private float m_Grip = 1f;
    [SerializeField, Range(0.01f, 1f)] private float m_RearDriftGrip = 0.25f;
    [SerializeField, Min(0f)] private float m_DownForce = 5f;
    [SerializeField] private AnimationCurve m_AccelerationCurve =
        new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
    [SerializeField] private AnimationCurve m_SteeringCurve =
        new AnimationCurve(new Keyframe(-1f, 1f), new Keyframe(0f, 1f), new Keyframe(1f, 0.45f));

    [Header("Wheel Visuals (FL, FR, RL, RR)")]
    [SerializeField] private Transform[] m_Wheels = new Transform[WHEEL_COUNT];

    private readonly WheelState[] m_WheelStates = new WheelState[WHEEL_COUNT];
    private readonly Vector3[] m_HardPointLocal = new Vector3[WHEEL_COUNT];
    private readonly Vector3[] m_WheelInitialLocalPosition = new Vector3[WHEEL_COUNT];
    private readonly Quaternion[] m_WheelInitialLocalRotation = new Quaternion[WHEEL_COUNT];
    private readonly Quaternion[] m_WheelMeshInitialRotation = new Quaternion[WHEEL_COUNT];
    private readonly float[] m_WheelSpin = new float[WHEEL_COUNT];

    private Rigidbody m_Rigidbody;
    private WheelCollider[] m_RvrWheelColliders;
    private float m_WheelBase;
    private float m_RearTrack;
    private bool m_Initialized;
    private bool m_WheelVisualsModified;
    private bool m_RvrWheelCollidersSuppressed;

    private struct WheelState
    {
        public bool IsGrounded;
        public RaycastHit Hit;
        public Vector3 HardPoint;
        public float SuspensionForce;
    }

    private void Awake()
    {
        this.m_Rigidbody = this.GetComponent<Rigidbody>();
        if (this.m_RvrController == null)
        {
            this.m_RvrController = this.GetComponent<PhysicsCarController>();
        }

        this.ResolveWheelReferences();
        this.CacheWheelGeometry();

        if (this.m_CenterOfMass == null && this.m_RvrController != null)
        {
            this.m_CenterOfMass = this.m_RvrController.centerOfMass;
        }

        if (this.m_CenterOfMass != null)
        {
            this.m_Rigidbody.centerOfMass = this.m_CenterOfMass.localPosition;
        }

        this.m_Initialized = this.HasCompleteWheelSetup();
        if (!this.m_Initialized)
        {
            Debug.LogError(
                "Sim-Cade RVR physics needs four wheel target transforms (FL, FR, RL, RR).",
                this
            );
        }
    }

    private void OnEnable()
    {
        // Keep the original RVR colliders and wheel poses untouched while parked.
        // CarEntry relies on that original idle state before the driver reaches the seat.
    }

    private void OnDisable()
    {
        this.SetRvrWheelCollidersEnabled(true);
        this.RestoreWheelVisuals();
    }

    private void FixedUpdate()
    {
        if (!this.m_Initialized || this.m_RvrController == null) return;
        if (!this.m_RvrController.UsesExternalPhysics) return;
        if (!this.IsDriving())
        {
            this.RestoreRvrParkedState();
            return;
        }

        this.SetRvrWheelCollidersEnabled(false);

        Vector2 input = this.m_RvrController.MovementInput;
        bool handbrake = this.m_RvrController.handbrakeInput;
        this.m_RvrController.isDrifting = handbrake && Mathf.Abs(input.x) > 0.1f;

        this.UpdateSteering(input.x);

        int groundedWheels = 0;
        for (int i = 0; i < WHEEL_COUNT; i++)
        {
            this.m_WheelStates[i] = this.SimulateSuspension(i);
            if (!this.m_WheelStates[i].IsGrounded) continue;

            groundedWheels++;
            this.ApplyTireFriction(i, handbrake);
        }

        if (groundedWheels >= 2)
        {
            this.ApplyDrive(input.y, handbrake);
            this.ApplyDownForce();
        }
        else
        {
            this.m_Rigidbody.AddForce(Physics.gravity * 0.35f, ForceMode.Acceleration);
        }

        this.ReportTelemetry();
    }

    private void LateUpdate()
    {
        if (!this.m_Initialized || this.m_RvrController == null) return;
        if (!this.m_RvrController.UsesExternalPhysics) return;
        if (!this.IsDriving())
        {
            this.RestoreRvrParkedState();
            return;
        }

        this.SetRvrWheelCollidersEnabled(false);

        for (int i = 0; i < WHEEL_COUNT; i++)
        {
            this.UpdateWheelVisual(i);
        }
    }

    private void ResolveWheelReferences()
    {
        if (this.m_RvrController == null) return;

        if (this.m_Wheels[FRONT_LEFT] == null)
            this.m_Wheels[FRONT_LEFT] = this.m_RvrController.frontLeftWheelTransform;
        if (this.m_Wheels[FRONT_RIGHT] == null)
            this.m_Wheels[FRONT_RIGHT] = this.m_RvrController.frontRightWheelTransform;
        if (this.m_Wheels[REAR_LEFT] == null)
            this.m_Wheels[REAR_LEFT] = this.m_RvrController.rearLeftWheelTransform;
        if (this.m_Wheels[REAR_RIGHT] == null)
            this.m_Wheels[REAR_RIGHT] = this.m_RvrController.rearRightWheelTransform;

        this.m_RvrWheelColliders = new[]
        {
            this.m_RvrController.frontLeftWheelCollider,
            this.m_RvrController.frontRightWheelCollider,
            this.m_RvrController.rearLeftWheelCollider,
            this.m_RvrController.rearRightWheelCollider
        };
    }

    private void CacheWheelGeometry()
    {
        for (int i = 0; i < WHEEL_COUNT; i++)
        {
            Transform wheel = this.m_Wheels[i];
            if (wheel == null) continue;

            Vector3 localWheelPosition = this.transform.InverseTransformPoint(wheel.position);
            this.m_WheelInitialLocalPosition[i] = wheel.localPosition;
            this.m_WheelInitialLocalRotation[i] = wheel.localRotation;
            this.m_HardPointLocal[i] = new Vector3(
                localWheelPosition.x,
                0f,
                localWheelPosition.z
            );

            if (wheel.childCount > 0)
            {
                this.m_WheelMeshInitialRotation[i] = wheel.GetChild(0).localRotation;
            }
        }

        if (!this.HasCompleteWheelSetup()) return;

        this.m_WheelBase = Vector3.Distance(
            this.m_HardPointLocal[FRONT_LEFT],
            this.m_HardPointLocal[REAR_LEFT]
        );
        this.m_RearTrack = Vector3.Distance(
            this.m_HardPointLocal[REAR_LEFT],
            this.m_HardPointLocal[REAR_RIGHT]
        );
    }

    private bool HasCompleteWheelSetup()
    {
        for (int i = 0; i < WHEEL_COUNT; i++)
        {
            if (this.m_Wheels[i] == null) return false;
        }

        return true;
    }

    private WheelState SimulateSuspension(int index)
    {
        Vector3 hardPoint = this.transform.TransformPoint(this.m_HardPointLocal[index]);
        Vector3 direction = -this.transform.up;
        Vector3 castOrigin = hardPoint + this.transform.up * this.m_WheelRadius;

        WheelState state = new WheelState { HardPoint = hardPoint };
        if (!Physics.SphereCast(
                castOrigin,
                this.m_WheelRadius,
                direction,
                out RaycastHit hit,
                this.m_SuspensionDistance,
                this.m_DrivableLayers,
                QueryTriggerInteraction.Ignore
            ))
        {
            return state;
        }

        state.IsGrounded = true;
        state.Hit = hit;

        float compression = Mathf.Clamp01(
            (this.m_SuspensionDistance - hit.distance) / this.m_SuspensionDistance
        );
        float pointVelocity = Vector3.Dot(
            this.m_Rigidbody.GetPointVelocity(hit.point),
            hit.normal
        );
        float spring = compression * compression * this.m_SpringForce;
        float damper = -pointVelocity * this.m_SpringDamper;
        state.SuspensionForce = Mathf.Max(0f, spring + damper);

        Vector3 forceDirection = Vector3.Project(hit.normal, this.transform.up).normalized;
        if (forceDirection == Vector3.zero) forceDirection = this.transform.up;

        this.m_Rigidbody.AddForceAtPosition(
            forceDirection * state.SuspensionForce,
            hardPoint,
            ForceMode.Force
        );

        return state;
    }

    private void ApplyTireFriction(int index, bool handbrake)
    {
        WheelState state = this.m_WheelStates[index];
        Transform wheel = this.m_Wheels[index];
        Vector3 pointVelocity = this.m_Rigidbody.GetPointVelocity(state.HardPoint);
        Vector3 sideVelocity = Vector3.ProjectOnPlane(
            Vector3.Project(pointVelocity, wheel.right),
            state.Hit.normal
        );

        float grip = this.m_Grip;
        if (handbrake && index >= REAR_LEFT) grip *= this.m_RearDriftGrip;

        float maxFriction = state.SuspensionForce * grip;
        Vector3 desiredForce = -sideVelocity * this.m_Rigidbody.mass / Time.fixedDeltaTime;
        Vector3 frictionForce = Vector3.ClampMagnitude(desiredForce, maxFriction);

        this.m_Rigidbody.AddForceAtPosition(
            frictionForce,
            state.HardPoint,
            ForceMode.Force
        );
    }

    private void ApplyDrive(float throttleInput, bool handbrake)
    {
        float forwardSpeed = Vector3.Dot(
            this.m_Rigidbody.linearVelocity,
            this.transform.forward
        );
        float speedRatio = Mathf.Clamp01(
            Mathf.Abs(forwardSpeed) / this.MaxSpeedMetersPerSecond
        );
        bool brakingAgainstVelocity = throttleInput * forwardSpeed < -0.05f;

        if (handbrake)
        {
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                this.m_Rigidbody.linearVelocity,
                this.transform.up
            );
            this.m_Rigidbody.AddForce(
                -horizontalVelocity * this.m_BrakeAcceleration,
                ForceMode.Acceleration
            );
            return;
        }

        if (brakingAgainstVelocity)
        {
            this.m_Rigidbody.AddForce(
                -Mathf.Sign(forwardSpeed) * this.transform.forward *
                this.m_BrakeAcceleration,
                ForceMode.Acceleration
            );
        }
        else if (Mathf.Abs(forwardSpeed) < this.MaxSpeedMetersPerSecond)
        {
            float accelerationCurve = Mathf.Max(
                0f,
                this.m_AccelerationCurve.Evaluate(speedRatio)
            );
            this.m_Rigidbody.AddForce(
                this.transform.forward * throttleInput * this.m_Acceleration * accelerationCurve,
                ForceMode.Acceleration
            );
        }

        if (Mathf.Abs(throttleInput) < 0.01f)
        {
            this.m_Rigidbody.AddForce(
                -Vector3.ProjectOnPlane(this.m_Rigidbody.linearVelocity, this.transform.up) *
                this.m_RollingResistance,
                ForceMode.Acceleration
            );
        }
    }

    private void ApplyDownForce()
    {
        float speedRatio = Mathf.Clamp01(
            this.m_Rigidbody.linearVelocity.magnitude / this.MaxSpeedMetersPerSecond
        );
        this.m_Rigidbody.AddForce(
            -this.transform.up * this.m_DownForce * speedRatio,
            ForceMode.Acceleration
        );
    }

    private void UpdateSteering(float steerInput)
    {
        float speedRatio = Mathf.Clamp01(
            Mathf.Abs(Vector3.Dot(this.m_Rigidbody.linearVelocity, this.transform.forward)) /
            this.MaxSpeedMetersPerSecond
        );
        float turn = steerInput * this.m_MaxTurnAngle * this.m_SteeringCurve.Evaluate(speedRatio);
        float turnRadius = this.m_WheelBase / Mathf.Max(
            0.01f,
            Mathf.Tan(this.m_MaxTurnAngle * Mathf.Deg2Rad)
        );
        float leftAngle = 0f;
        float rightAngle = 0f;

        if (turn > 0f)
        {
            leftAngle = Mathf.Atan(this.m_WheelBase / (turnRadius + this.m_RearTrack * 0.5f)) * Mathf.Rad2Deg * steerInput;
            rightAngle = Mathf.Atan(this.m_WheelBase / (turnRadius - this.m_RearTrack * 0.5f)) * Mathf.Rad2Deg * steerInput;
        }
        else if (turn < 0f)
        {
            leftAngle = Mathf.Atan(this.m_WheelBase / (turnRadius - this.m_RearTrack * 0.5f)) * Mathf.Rad2Deg * steerInput;
            rightAngle = Mathf.Atan(this.m_WheelBase / (turnRadius + this.m_RearTrack * 0.5f)) * Mathf.Rad2Deg * steerInput;
        }

        this.SetWheelSteer(FRONT_LEFT, leftAngle);
        this.SetWheelSteer(FRONT_RIGHT, rightAngle);
    }

    private void SetWheelSteer(int index, float angle)
    {
        Transform wheel = this.m_Wheels[index];
        if (wheel == null) return;

        wheel.localRotation = Quaternion.Euler(0f, angle, 0f);
        this.m_WheelVisualsModified = true;
    }

    private void UpdateWheelVisual(int index)
    {
        Transform wheel = this.m_Wheels[index];
        if (wheel == null) return;

        WheelState state = this.m_WheelStates[index];
        Vector3 wheelPosition = state.IsGrounded
            ? state.Hit.point + this.transform.up * this.m_WheelRadius
            : this.transform.TransformPoint(
                this.m_HardPointLocal[index] - Vector3.up *
                (this.m_SuspensionDistance - this.m_WheelRadius)
            );
        wheel.position = wheelPosition;
        this.m_WheelVisualsModified = true;

        if (wheel.childCount == 0) return;

        float forwardSpeed = Vector3.Dot(
            this.m_Rigidbody.GetPointVelocity(state.HardPoint),
            wheel.forward
        );
        this.m_WheelSpin[index] += forwardSpeed / this.m_WheelRadius * Mathf.Rad2Deg *
            Time.deltaTime;
        wheel.GetChild(0).localRotation = this.m_WheelMeshInitialRotation[index] *
            Quaternion.Euler(this.m_WheelSpin[index], 0f, 0f);
    }

    private void ReportTelemetry()
    {
        float speedKph = this.m_Rigidbody.linearVelocity.magnitude * 3.6f;
        float idleRpm = (float)this.m_RvrController.idleRPM.Get(this.gameObject);
        float maxRpm = (float)this.m_RvrController.maxRPM.Get(this.gameObject);
        float rpm = Mathf.Lerp(
            idleRpm,
            maxRpm,
            Mathf.Clamp01(speedKph / this.m_MaxSpeedKph)
        );

        this.m_RvrController.SetExternalTelemetry(speedKph, rpm);
    }

    private float MaxSpeedMetersPerSecond => this.m_MaxSpeedKph / 3.6f;

    private bool IsDriving()
    {
        return this.m_RvrController != null &&
               this.m_RvrController.enabled &&
               this.m_RvrController.isVehicleEnabled;
    }

    private void RestoreRvrParkedState()
    {
        this.SetRvrWheelCollidersEnabled(true);
        this.RestoreWheelVisuals();
    }

    private void RestoreWheelVisuals()
    {
        if (!this.m_WheelVisualsModified) return;

        for (int i = 0; i < WHEEL_COUNT; i++)
        {
            Transform wheel = this.m_Wheels[i];
            if (wheel == null) continue;

            wheel.localPosition = this.m_WheelInitialLocalPosition[i];
            wheel.localRotation = this.m_WheelInitialLocalRotation[i];
            this.m_WheelSpin[i] = 0f;

            if (wheel.childCount > 0)
            {
                wheel.GetChild(0).localRotation = this.m_WheelMeshInitialRotation[i];
            }
        }

        this.m_WheelVisualsModified = false;
    }

    private void SetRvrWheelCollidersEnabled(bool enabled)
    {
        if (this.m_RvrWheelColliders == null) return;
        if (this.m_RvrWheelCollidersSuppressed == !enabled) return;

        for (int i = 0; i < this.m_RvrWheelColliders.Length; i++)
        {
            if (this.m_RvrWheelColliders[i] != null)
            {
                this.m_RvrWheelColliders[i].enabled = enabled;
            }
        }

        this.m_RvrWheelCollidersSuppressed = !enabled;
    }
}
