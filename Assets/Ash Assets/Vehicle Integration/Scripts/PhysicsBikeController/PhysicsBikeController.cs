using UnityEngine;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;

public enum SteeringWheelRotationAxis
{
    X,
    Y,
    Z
}

[RequireComponent(typeof(Rigidbody))]
public class PhysicsBikeController : MonoBehaviour, IRvrVehicleInputController
{
    public bool isVehicleEnabled = false;

    [Space]
    [SerializeField] public PropertyGetDecimal topSpeed = new PropertyGetDecimal(220); // km/h
    [SerializeField] public PropertySetNumber currentSpeed = new PropertySetNumber();   // km/h
    [SerializeField] public PropertyGetDecimal acceleration = new PropertyGetDecimal(5f);
    [SerializeField] public PropertyGetDecimal braking = new PropertyGetDecimal(8f);

    [Space]
    [SerializeField] public PropertyGetDecimal reverseSpeed = new PropertyGetDecimal(80);
    [SerializeField] public PropertyGetDecimal reverseAcceleration = new PropertyGetDecimal(2f);
    [SerializeField] public PropertyGetDecimal accelerationCurve = new PropertyGetDecimal(1.5f);
    [SerializeField] public PropertyGetDecimal accelerationCurveCoefficient = new PropertyGetDecimal(1.5f);
    [SerializeField] public PropertyGetDecimal coastingDrag = new PropertyGetDecimal(4f);
    [SerializeField] public PropertyGetDecimal grip = new PropertyGetDecimal(0.95f);
    [SerializeField] public PropertyGetDecimal steer = new PropertyGetDecimal(2.2f);
    [SerializeField] public PropertyGetDecimal handbrakeForce = new PropertyGetDecimal(20f);
    [SerializeField] public PropertyGetDecimal addedGravity = new PropertyGetDecimal(1f);

    [Space]
    [SerializeField] public int numberOfGears = 6;
    [SerializeField] public PropertyGetDecimal maxRPM = new PropertyGetDecimal(12000f);
    [SerializeField] public PropertySetNumber currentRPM = new PropertySetNumber();
    [SerializeField] public PropertyGetDecimal gearRatio = new PropertyGetDecimal(10f);
    [SerializeField] public PropertyGetDecimal idleRPM = new PropertyGetDecimal(1000f);
    private int currentGear = 1;

    [Space]
    [SerializeField] public PropertyGetDecimal driftGrip = new PropertyGetDecimal(0.1f);
    [SerializeField] public PropertyGetDecimal driftControl = new PropertyGetDecimal(10f);
    [SerializeField] public PropertyGetDecimal driftDampening = new PropertyGetDecimal(10f);

    [Space]
    [SerializeField] private float driftLeanMultiplier = 0.5f;

    [Space]
    [SerializeField] public PropertyGetDecimal leanTorque = new PropertyGetDecimal(15f);
    [SerializeField] public PropertyGetDecimal maxLeanAngle = new PropertyGetDecimal(45f);

    [Space]
    [SerializeField] private float airStabilityCoefficient = 10f;

    [Space]
    [SerializeField] private float airAngularDamping = 2f;
    [SerializeField] private float driftAngularDamping = 2f;

    [Space]
    [SerializeField] public PropertyGetDecimal maxDamage = new PropertyGetDecimal(100f);
    [SerializeField] public PropertySetNumber currentHealth = new PropertySetNumber();
    [SerializeField] public PropertyGetDecimal collisionThreshold = new PropertyGetDecimal(3f);
    [SerializeField] public PropertyGetDecimal damageIntensity = new PropertyGetDecimal(15f);

    [Space]
    [SerializeField] public PropertyGetDecimal fuelCapacity = new PropertyGetDecimal(20f);
    [SerializeField] public PropertySetNumber currentFuel = new PropertySetNumber();
    [SerializeField] public PropertyGetDecimal fuelEfficiency = new PropertyGetDecimal(0.8f);

    [Space]
    public Transform frontWheelTransform;
    public Transform rearWheelTransform;
    [Tooltip("Front Wheel Collider")]
    public WheelCollider frontWheelCollider;
    [Tooltip("Rear Wheel Collider")]
    public WheelCollider rearWheelCollider;
    [SerializeField] public PropertyGetDecimal suspensionHeight = new PropertyGetDecimal(0.15f);
    [SerializeField] public PropertyGetDecimal suspensionSpring = new PropertyGetDecimal(15000f);
    [SerializeField] public PropertyGetDecimal suspensionDamp = new PropertyGetDecimal(600f);

    [Space]
    public Transform centerOfMass;

    [Space]
    [Tooltip("The bike body mesh that will lean when steering")]
    public Transform bikeBody;
    [Tooltip("Minimum km/h before the bike body actually leans")]
    [SerializeField] private float leanSpeedThreshold = 10f;
    [Tooltip("The steering wheel or handlebar mesh that will turn when steering")]
    public Transform steeringWheelMesh;
    [Tooltip("Maximum visual steering angle for the steering wheel (in degrees)")]
    public PropertyGetDecimal steeringWheelMaxAngle = new PropertyGetDecimal(30f);
    [Tooltip("Maximum visual steering angle for the front wheel (in degrees)")]
    public PropertyGetDecimal frontWheelMaxAngle = new PropertyGetDecimal(30f);
    [Tooltip("Select the axis on which the steering wheel rotates")]
    public SteeringWheelRotationAxis steeringWheelRotationAxis = SteeringWheelRotationAxis.Y;

    [SerializeField] private float leanSmooth = 5f;
    [Tooltip("Extra visual clearance above the WheelCollider contact patch. " +
             "This also compensates when the rendered tyre is larger than its collider.")]
    [SerializeField, Range(0f, 0.08f)] private float visualTireGroundClearance = 0.015f;


    public enum InputMode { Legacy, Both, New }
    [Tooltip("Which input system to read driving input from at runtime. " +
             "New/Both require the Input System package installed.")]
    [SerializeField] public InputMode inputMode = InputMode.Legacy;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Input Action Asset used when Input Mode is New or Both.")]
    [SerializeField] public UnityEngine.InputSystem.InputActionAsset inputActionAsset;
    [Tooltip("The Vector2 \"Move\" action inside the asset (x = steer, y = throttle/brake).")]
    [RVRInputActionPicker("inputActionAsset")]
    [SerializeField] public string moveActionName = "Move";
    private UnityEngine.InputSystem.InputAction m_MoveAction;
#endif

    private Quaternion _initialSteeringWheelLocalRotation;
    private Vector3 bikeBodyInitialLocalPos;
    private Quaternion bikeBodyInitialLocalRotation;
    private float m_CurrentVisualLean;
    private Vector3 m_CurrentVisualGroundOffset;
    private Quaternion m_VisualLeanWorldDelta = Quaternion.identity;
    private Vector3 m_VisualLeanPivotWorld;

    private bool m_FrontGrounded;
    private bool m_RearGrounded;
    private int m_GroundedWheelCount;
    private WheelHit m_FrontGroundHit;
    private WheelHit m_RearGroundHit;
    private bool m_HasContactPivot;
    private Vector3 m_ContactPivotWorld;
    private Vector3 m_ContactAxisWorld;
    private Vector3 m_AverageGroundNormal;

    private Vector3 m_FrontWheelPosePosition;
    private Vector3 m_RearWheelPosePosition;
    private Quaternion m_FrontWheelPoseRotation;
    private Quaternion m_RearWheelPoseRotation;
    private bool m_HasFrontWheelPose;
    private bool m_HasRearWheelPose;
    private float m_FrontVisualWheelRadius;
    private float m_RearVisualWheelRadius;

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;
    private bool m_VirtualAccelerate;
    private bool m_VirtualSlowAccelerate;
    private bool m_VirtualBrakeReverse;
    private bool m_VirtualSteerLeft;
    private bool m_VirtualSteerRight;
    private bool m_VirtualHandbrake;
    private bool m_ExternalHandbrake;
    private bool m_IsStoppingForExit;
    private BikeEntry m_BikeEntry;
    private float m_CurrentGrip;
    private float m_DriftTurningPower;
    [HideInInspector] public bool isDrifting;
    [HideInInspector] public bool handbrakeInput = false;

    public bool IsVehicleEnabled => isVehicleEnabled;
    public bool UseSeatEntryAlignment => false;
    public Transform VehicleBody => bikeBody != null ? bikeBody : transform;
    public float SpeedMetersPerSecond => rb != null
        ? Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).magnitude
        : 0f;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        m_BikeEntry = GetComponent<BikeEntry>();
        if (centerOfMass != null)
            rb.centerOfMass = centerOfMass.localPosition;
        SetupSuspension();

        m_CurrentGrip = (float)grip.Get(this.gameObject);
        m_DriftTurningPower = 0f;
        isDrifting = false;

        if (steeringWheelMesh != null)
        {
            _initialSteeringWheelLocalRotation = steeringWheelMesh.localRotation;
        }

        if (bikeBody != null)
        {
            bikeBodyInitialLocalPos = bikeBody.localPosition;
            bikeBodyInitialLocalRotation = bikeBody.localRotation;
            if (frontWheelTransform != null)
            {
                frontWheelTransform.SetParent(bikeBody, true);
            }
            if (rearWheelTransform != null)
            {
                rearWheelTransform.SetParent(bikeBody, true);
            }
        }

        m_FrontVisualWheelRadius = ResolveVisualWheelRadius(
            frontWheelTransform,
            frontWheelCollider != null ? frontWheelCollider.radius : 0f
        );
        m_RearVisualWheelRadius = ResolveVisualWheelRadius(
            rearWheelTransform,
            rearWheelCollider != null ? rearWheelCollider.radius : 0f
        );
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        SetupInputActions();
#endif
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (m_MoveAction != null) m_MoveAction.Disable();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void SetupInputActions()
    {
        m_MoveAction = null;
        if (inputActionAsset == null) return;

        m_MoveAction = inputActionAsset.FindAction(moveActionName, false);
        if (m_MoveAction != null) m_MoveAction.Enable();
        else Debug.LogWarning($"[{nameof(PhysicsBikeController)}] Move action '{moveActionName}' " +
                              "was not found in the assigned Input Action Asset.", this);
    }
#endif

    // Returns combined movement input. x = steering (Horizontal), y = throttle/brake (Vertical).
    private Vector2 ReadMovementInput()
    {
        Vector2 result = Vector2.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (inputMode == InputMode.Legacy || inputMode == InputMode.Both)
        {
            result.x += Input.GetAxis("Horizontal");
            result.y += Input.GetAxis("Vertical");
        }
#endif

#if ENABLE_INPUT_SYSTEM
        if ((inputMode == InputMode.New || inputMode == InputMode.Both) && m_MoveAction != null)
        {
            Vector2 v = m_MoveAction.ReadValue<Vector2>();
            result.x += v.x;
            result.y += v.y;
        }
#endif

        if (m_VirtualSteerLeft) result.x -= 1f;
        if (m_VirtualSteerRight) result.x += 1f;
        if (m_VirtualAccelerate) result.y += 1f;
        if (m_VirtualSlowAccelerate) result.y += 0.35f;
        if (m_VirtualBrakeReverse) result.y -= 1f;

        result.x = Mathf.Clamp(result.x, -1f, 1f);
        result.y = Mathf.Clamp(result.y, -1f, 1f);
        return result;
    }

    void Update()
    {
        if (!isVehicleEnabled)
        {
            verticalInput = 0f;
            horizontalInput = 0f;
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current?.eKey.wasPressedThisFrame == true)
        {
            RequestExit();
            return;
        }
#endif

        Vector2 move = ReadMovementInput();
        horizontalInput = move.x;
        verticalInput = move.y;
        handbrakeInput = m_ExternalHandbrake || m_VirtualHandbrake || m_IsStoppingForExit;

        if (Mathf.Abs(verticalInput) > 0.01f && (float)currentFuel.Get(this.gameObject) > 0)
        {
            float newFuel = (float)currentFuel.Get(this.gameObject) -
                            ((float)fuelEfficiency.Get(this.gameObject) * Mathf.Abs(verticalInput) * Time.deltaTime);
            currentFuel.Set(Mathf.Clamp(newFuel, 0, (float)fuelCapacity.Get(this.gameObject)), this.gameObject);
        }
    }

    void FixedUpdate()
    {
        if (rb.isKinematic)
        {
            return;
        }

        if (!isVehicleEnabled)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        SetupSuspension();
        CacheGroundContacts();

        bool isAirborne = m_GroundedWheelCount == 0;

        if (isAirborne)
        {
            verticalInput = 0f;
            horizontalInput = 0f;
        }

        MoveBike(verticalInput, horizontalInput);

        float rawSpeed = rb.linearVelocity.magnitude * 3.6f;
        currentSpeed.Set(Mathf.RoundToInt(rawSpeed), this.gameObject);

        HandleTransmission();

        BalanceBike();

        if (!isAirborne)
        {
            float effectiveLeanTorque = isDrifting ? (float)leanTorque.Get(this.gameObject) * driftLeanMultiplier :
                                                      (float)leanTorque.Get(this.gameObject);
            rb.AddTorque(transform.forward * horizontalInput * effectiveLeanTorque, ForceMode.Acceleration);

            if (isDrifting)
            {
                Vector3 dampedAngular = rb.angularVelocity;
                dampedAngular.x = Mathf.Lerp(rb.angularVelocity.x, 0, Time.fixedDeltaTime * driftAngularDamping);
                dampedAngular.z = Mathf.Lerp(rb.angularVelocity.z, 0, Time.fixedDeltaTime * driftAngularDamping);
                rb.angularVelocity = dampedAngular;
            }
        }
        else
        {
            StabilizeAirborne();
            Vector3 dampedAngular = new Vector3(0, rb.angularVelocity.y, 0);
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, dampedAngular, Time.fixedDeltaTime * airAngularDamping);
        }
    }

    public void SetBikeEnabled(bool state)
    {
        isVehicleEnabled = state;

        if (!state)
        {
            ResetVirtualInputs();
            m_IsStoppingForExit = false;
            m_ExternalHandbrake = false;
            handbrakeInput = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (rb == null) return;
        if (!state)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ;
        }
        else
        {
            rb.constraints = RigidbodyConstraints.None;
        }
    }


    void LateUpdate()
    {
        UpdateVisuals();
    }

    public void SetHandbrakeInput(bool active)
    {
        m_ExternalHandbrake = active;
        handbrakeInput = m_ExternalHandbrake || m_VirtualHandbrake || m_IsStoppingForExit;
    }

    public void SetVehicleEnabled(bool state)
    {
        SetBikeEnabled(state);
    }

    public void SetVehicleEnabled(bool state, bool preserveMomentum)
    {
        if (!preserveMomentum)
        {
            SetBikeEnabled(state);
            return;
        }

        isVehicleEnabled = state;
        if (!state)
        {
            ResetVirtualInputs();
            m_IsStoppingForExit = false;
            m_ExternalHandbrake = false;
            handbrakeInput = false;
        }
    }

    public void BeginExitStop()
    {
        m_IsStoppingForExit = true;
        ResetVirtualInputs();
        handbrakeInput = true;
    }

    public void CancelExitStop()
    {
        m_IsStoppingForExit = false;
        handbrakeInput = m_ExternalHandbrake || m_VirtualHandbrake;
    }

    public void SetVirtualAccelerateInput(bool active)
    {
        m_VirtualAccelerate = active;
    }

    public void SetVirtualSlowAccelerateInput(bool active)
    {
        m_VirtualSlowAccelerate = active;
    }

    public void SetVirtualBrakeReverseInput(bool active)
    {
        m_VirtualBrakeReverse = active;
    }

    public void SetVirtualSteerLeftInput(bool active)
    {
        m_VirtualSteerLeft = active;
    }

    public void SetVirtualSteerRightInput(bool active)
    {
        m_VirtualSteerRight = active;
    }

    public void SetVirtualHandbrakeInput(bool active)
    {
        m_VirtualHandbrake = active;
    }

    public void RequestExit()
    {
        if (!isVehicleEnabled || m_BikeEntry == null || m_BikeEntry.IsTransitioning) return;

        Character character = m_BikeEntry.SeatedCharacter;
        if (character != null) m_BikeEntry.RequestExit(character);
    }

    private void ResetVirtualInputs()
    {
        m_VirtualAccelerate = false;
        m_VirtualSlowAccelerate = false;
        m_VirtualBrakeReverse = false;
        m_VirtualSteerLeft = false;
        m_VirtualSteerRight = false;
        m_VirtualHandbrake = false;
    }

    void MoveBike(float motorInput, float steerInput)
    {
        const float k_NullInput = 0.01f;

        float effectiveMotorInput = ((float)currentFuel.Get(this.gameObject) <= 0 || (float)currentHealth.Get(this.gameObject) <= 0)
            ? 0f : motorInput;
        float effectiveTurnInput = ((float)currentFuel.Get(this.gameObject) <= 0 || (float)currentHealth.Get(this.gameObject) <= 0)
            ? 0f : steerInput;

        bool driftMode = handbrakeInput && Mathf.Abs(steerInput) >= 0.1f;

        if (handbrakeInput && !driftMode)
        {
            Vector3 velocity = rb.linearVelocity;
            Vector3 horizontalVel = new Vector3(velocity.x, 0, velocity.z);
            horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, (float)handbrakeForce.Get(this.gameObject) * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(horizontalVel.x, velocity.y, horizontalVel.z);
            effectiveTurnInput = 0f;
            return;
        }
        else if (handbrakeInput && driftMode)
        {
            Vector3 velocity = rb.linearVelocity;
            Vector3 horizontalVel = new Vector3(velocity.x, 0, velocity.z);
            horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, 0.5f * (float)handbrakeForce.Get(this.gameObject) * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(horizontalVel.x, velocity.y, horizontalVel.z);
        }

        float topSpeedMPS = (float)topSpeed.Get(this.gameObject) / 3.6f;
        float reverseSpeedMPS = (float)reverseSpeed.Get(this.gameObject) / 3.6f;

        bool accelerate = effectiveMotorInput > k_NullInput;
        bool brake = effectiveMotorInput < -k_NullInput;
        float accelInput = (accelerate ? 1f : 0f) - (brake ? 1f : 0f);

        Vector3 localVel = transform.InverseTransformVector(rb.linearVelocity);
        bool accelDirectionIsFwd = accelInput >= 0;
        bool localVelDirectionIsFwd = localVel.z >= 0;
        float maxSpeed = localVelDirectionIsFwd ? topSpeedMPS : reverseSpeedMPS;
        float accelPower = accelDirectionIsFwd ? (float)acceleration.Get(this.gameObject) : (float)reverseAcceleration.Get(this.gameObject);
        float currentSpeed = rb.linearVelocity.magnitude;
        float accelRampT = currentSpeed / maxSpeed;
        float multipliedAccelerationCurve = (float)accelerationCurve.Get(this.gameObject) * (float)accelerationCurveCoefficient.Get(this.gameObject);
        float accelRamp = Mathf.Lerp(multipliedAccelerationCurve, 1, accelRampT * accelRampT);
        bool isBraking = (localVelDirectionIsFwd && brake) || (!localVelDirectionIsFwd && accelerate);
        float finalAccelPower = isBraking ? (float)braking.Get(this.gameObject) : accelPower;
        float finalAcceleration = finalAccelPower * accelRamp;

        float rawSpeed = rb.linearVelocity.magnitude * 3.6f;

        if (driftMode && rawSpeed > 1f)
        {
            isDrifting = true;
            m_CurrentGrip = Mathf.Lerp(m_CurrentGrip, (float)driftGrip.Get(this.gameObject),
                                       (float)driftDampening.Get(this.gameObject) * Time.fixedDeltaTime);
            m_DriftTurningPower = Mathf.Lerp(m_DriftTurningPower, steerInput * (float)driftControl.Get(this.gameObject),
                                             (float)driftDampening.Get(this.gameObject) * Time.fixedDeltaTime);
        }
        else
        {
            isDrifting = false;
            m_CurrentGrip = Mathf.Lerp(m_CurrentGrip, (float)grip.Get(this.gameObject), 2f * Time.fixedDeltaTime);
            m_DriftTurningPower = 0f;
        }

        float turningPower = isDrifting ? m_DriftTurningPower : effectiveTurnInput * (float)steer.Get(this.gameObject);

        float groundPercent = m_GroundedWheelCount / 2f;
        float airPercent = 1f - groundPercent;
        if (airPercent >= 1f)
        {
            rb.AddForce(Physics.gravity * ((float)addedGravity.Get(this.gameObject) - 1f) * rb.mass);
        }

        Quaternion turnAngle = Quaternion.AngleAxis(turningPower, transform.up);
        Vector3 fwd = turnAngle * transform.forward;
        Vector3 movement = fwd * accelInput * finalAcceleration * Time.fixedDeltaTime;

        if (groundPercent > 0f)
        {
            Vector3 newVelocity = rb.linearVelocity + movement;
            newVelocity.y = rb.linearVelocity.y;
            newVelocity = Vector3.ClampMagnitude(newVelocity, maxSpeed);
            if (Mathf.Abs(accelInput) < k_NullInput)
            {
                newVelocity = Vector3.MoveTowards(newVelocity, new Vector3(0, rb.linearVelocity.y, 0),
                                                  Time.fixedDeltaTime * (float)coastingDrag.Get(this.gameObject));
            }
            rb.linearVelocity = newVelocity;
        }

        if (groundPercent > 0f)
        {
            float angularVelocitySteering = 0.4f;
            float angularVelocitySmoothSpeed = 20f;
            if (!localVelDirectionIsFwd && !accelDirectionIsFwd)
                angularVelocitySteering *= -1f;

            Vector3 angularVel = rb.angularVelocity;
            angularVel.y = Mathf.MoveTowards(angularVel.y, turningPower * angularVelocitySteering,
                                             Time.fixedDeltaTime * angularVelocitySmoothSpeed);
            rb.angularVelocity = angularVel;

            float velocitySteering = 25f;
            float speed = rb.linearVelocity.magnitude;
            float lowerBound = 2.78f;
            float upperBound = 5f;
            float steeringMultiplier = Mathf.Clamp01((speed - lowerBound) / (upperBound - lowerBound));

            float appliedGrip = isDrifting ? m_CurrentGrip : (float)grip.Get(this.gameObject);
            rb.linearVelocity = Quaternion.AngleAxis(
                turningPower * Mathf.Sign(localVel.z) * velocitySteering * appliedGrip * Time.fixedDeltaTime * steeringMultiplier,
                transform.up) * rb.linearVelocity;
        }
    }

    void HandleTransmission()
    {
        if (frontWheelCollider == null) return;

        float wheelRadius = frontWheelCollider.radius;
        float wheelCircumference = 2 * Mathf.PI * wheelRadius;
        float wheelRPM = rb.linearVelocity.magnitude / wheelCircumference * 60f;
        float baseGearRatio = (float)gearRatio.Get(this.gameObject);
        float effectiveGearRatio = baseGearRatio / currentGear;
        float engineRPM = wheelRPM * effectiveGearRatio;

        float idleRPMValue = (float)idleRPM.Get(this.gameObject);
        if (engineRPM < idleRPMValue)
            engineRPM = idleRPMValue;

        float maxRPMValue = (float)maxRPM.Get(this.gameObject);
        bool shiftUp = engineRPM > 0.95f * maxRPMValue && currentGear < numberOfGears;
        bool shiftDown = engineRPM < idleRPMValue + 0.3f * (maxRPMValue - idleRPMValue) && currentGear > 1;

        if (shiftUp)
        {
            currentGear++;
            engineRPM = idleRPMValue;
            rb.linearVelocity *= 0.95f;
        }
        else if (shiftDown)
        {
            currentGear--;
            engineRPM = idleRPMValue;
            rb.linearVelocity *= 0.95f;
        }
        currentRPM.Set(engineRPM, this.gameObject);
    }

    void SetupSuspension()
    {
        SetupWheelSuspension(frontWheelCollider);
        SetupWheelSuspension(rearWheelCollider);
    }

    void SetupWheelSuspension(WheelCollider wheel)
    {
        if (wheel == null) return;
        wheel.suspensionDistance = (float)suspensionHeight.Get(this.gameObject);
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = (float)suspensionSpring.Get(this.gameObject);
        spring.damper = (float)suspensionDamp.Get(this.gameObject);
        wheel.suspensionSpring = spring;
    }

    private void CacheGroundContacts()
    {
        m_FrontGrounded = frontWheelCollider != null &&
            frontWheelCollider.GetGroundHit(out m_FrontGroundHit);
        m_RearGrounded = rearWheelCollider != null &&
            rearWheelCollider.GetGroundHit(out m_RearGroundHit);

        m_GroundedWheelCount = (m_FrontGrounded ? 1 : 0) + (m_RearGrounded ? 1 : 0);
        if (m_GroundedWheelCount == 0)
        {
            m_HasContactPivot = false;
            m_AverageGroundNormal = transform.up;
            return;
        }

        Vector3 normalSum = Vector3.zero;
        if (m_FrontGrounded) normalSum += m_FrontGroundHit.normal;
        if (m_RearGrounded) normalSum += m_RearGroundHit.normal;
        m_AverageGroundNormal = normalSum.sqrMagnitude > 0.000001f
            ? normalSum.normalized
            : transform.up;

        if (m_FrontGrounded && m_RearGrounded)
        {
            m_ContactPivotWorld = (m_FrontGroundHit.point + m_RearGroundHit.point) * 0.5f;
            m_ContactAxisWorld = m_FrontGroundHit.point - m_RearGroundHit.point;
        }
        else
        {
            m_ContactPivotWorld = m_FrontGrounded
                ? m_FrontGroundHit.point
                : m_RearGroundHit.point;
            m_ContactAxisWorld = Vector3.ProjectOnPlane(
                transform.forward,
                m_AverageGroundNormal
            );
        }

        if (m_ContactAxisWorld.sqrMagnitude < 0.000001f)
        {
            m_ContactAxisWorld = transform.forward;
        }
        else
        {
            m_ContactAxisWorld.Normalize();
        }

        if (Vector3.Dot(m_ContactAxisWorld, transform.forward) < 0f)
        {
            m_ContactAxisWorld = -m_ContactAxisWorld;
        }

        m_HasContactPivot = true;
    }

    private void RefreshWheelVisualPoses()
    {
        m_HasFrontWheelPose = frontWheelCollider != null;
        if (m_HasFrontWheelPose)
        {
            frontWheelCollider.GetWorldPose(
                out m_FrontWheelPosePosition,
                out m_FrontWheelPoseRotation
            );
        }

        m_HasRearWheelPose = rearWheelCollider != null;
        if (m_HasRearWheelPose)
        {
            rearWheelCollider.GetWorldPose(
                out m_RearWheelPosePosition,
                out m_RearWheelPoseRotation
            );
        }
    }

    private Vector3 CalculateVisualGroundOffset()
    {
        Vector3 correction = Vector3.zero;
        int correctionCount = 0;

        if (m_FrontGrounded && m_HasFrontWheelPose)
        {
            correction += CalculateWheelGroundCorrection(
                m_FrontWheelPosePosition,
                m_FrontGroundHit,
                m_FrontVisualWheelRadius
            );
            correctionCount++;
        }

        if (m_RearGrounded && m_HasRearWheelPose)
        {
            correction += CalculateWheelGroundCorrection(
                m_RearWheelPosePosition,
                m_RearGroundHit,
                m_RearVisualWheelRadius
            );
            correctionCount++;
        }

        return correctionCount > 0 ? correction / correctionCount : Vector3.zero;
    }

    private Vector3 CalculateWheelGroundCorrection(
        Vector3 wheelCenter,
        WheelHit hit,
        float visualRadius
    )
    {
        Vector3 normal = hit.normal.sqrMagnitude > 0.000001f
            ? hit.normal.normalized
            : transform.up;
        float currentClearance = Vector3.Dot(wheelCenter - hit.point, normal);
        float desiredClearance = visualRadius + visualTireGroundClearance;
        float lift = Mathf.Clamp(desiredClearance - currentClearance, 0f, 0.12f);
        return normal * lift;
    }

    private void ApplyContactPatchLean()
    {
        Quaternion baseWorldRotation = transform.rotation * bikeBodyInitialLocalRotation;
        Vector3 baseWorldPosition = transform.TransformPoint(bikeBodyInitialLocalPos) +
            m_CurrentVisualGroundOffset;
        Vector3 leanAxis = m_HasContactPivot
            ? m_ContactAxisWorld
            : transform.forward;

        if (leanAxis.sqrMagnitude < 0.000001f)
        {
            leanAxis = transform.forward;
        }
        leanAxis.Normalize();

        m_VisualLeanPivotWorld = m_HasContactPivot
            ? m_ContactPivotWorld
            : baseWorldPosition;
        m_VisualLeanWorldDelta = Quaternion.AngleAxis(m_CurrentVisualLean, leanAxis);

        Vector3 targetWorldPosition = m_VisualLeanPivotWorld +
            m_VisualLeanWorldDelta * (baseWorldPosition - m_VisualLeanPivotWorld);
        Quaternion targetWorldRotation = m_VisualLeanWorldDelta * baseWorldRotation;
        bikeBody.SetPositionAndRotation(targetWorldPosition, targetWorldRotation);
    }

    private static float ResolveVisualWheelRadius(
        Transform wheelTransform,
        float fallbackRadius
    )
    {
        float radius = Mathf.Max(0f, fallbackRadius);
        if (wheelTransform == null) return radius;

        // This allocation happens once in Awake, never in Update/FixedUpdate.
        Renderer[] renderers = wheelTransform.GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer wheelRenderer = renderers[index];
            if (wheelRenderer == null) continue;
            radius = Mathf.Max(radius, wheelRenderer.bounds.extents.y);
        }

        return radius;
    }

    [ContextMenu("Auto Setup Wheel Colliders")]
    public void AutoSetupWheelColliders()
    {
        if (frontWheelTransform != null)
            frontWheelCollider = CreateWheelColliderForWheel(frontWheelTransform, "FrontLeftWheelCollider");
        if (rearWheelTransform != null)
            rearWheelCollider = CreateWheelColliderForWheel(rearWheelTransform, "RearLeftWheelCollider");
        SetupSuspension();
    }

    WheelCollider CreateWheelColliderForWheel(Transform wheelTransform, string colliderName)
    {
        GameObject colliderObject = new GameObject(colliderName);
        colliderObject.transform.parent = transform;
        colliderObject.transform.position = wheelTransform.position;
        colliderObject.transform.rotation = wheelTransform.rotation;
        WheelCollider wheelCollider = colliderObject.AddComponent<WheelCollider>();
        wheelCollider.radius = 0.35f;
        wheelCollider.suspensionDistance = (float)suspensionHeight.Get(this.gameObject);
        JointSpring spring = wheelCollider.suspensionSpring;
        spring.spring = (float)suspensionSpring.Get(this.gameObject);
        spring.damper = (float)suspensionDamp.Get(this.gameObject);
        wheelCollider.suspensionSpring = spring;
        colliderObject.layer = LayerMask.NameToLayer("Default");
        return wheelCollider;
    }

    void UpdateWheelVisuals()
    {
        if (m_HasFrontWheelPose && frontWheelTransform != null)
        {
            UpdateWheelVisual(
                frontWheelTransform,
                m_FrontWheelPosePosition,
                m_FrontWheelPoseRotation,
                true
            );
        }
        if (m_HasRearWheelPose && rearWheelTransform != null)
        {
            UpdateWheelVisual(
                rearWheelTransform,
                m_RearWheelPosePosition,
                m_RearWheelPoseRotation,
                false
            );
        }
    }

    void UpdateWheelVisual(
        Transform wheelTransform,
        Vector3 baseWorldPosition,
        Quaternion baseWorldRotation,
        bool isFrontWheel
    )
    {
        if (wheelTransform == null) return;

        Vector3 correctedBasePosition = baseWorldPosition + m_CurrentVisualGroundOffset;
        Vector3 visualPosition = m_VisualLeanPivotWorld +
            m_VisualLeanWorldDelta * (correctedBasePosition - m_VisualLeanPivotWorld);
        Quaternion visualRotation = m_VisualLeanWorldDelta * baseWorldRotation;

        if (isFrontWheel)
        {
            float frontSteerAngle = horizontalInput * (float)frontWheelMaxAngle.Get(this.gameObject);
            Vector3 visualUp = m_VisualLeanWorldDelta * transform.up;
            visualRotation = Quaternion.AngleAxis(frontSteerAngle, visualUp) * visualRotation;
        }

        wheelTransform.SetPositionAndRotation(visualPosition, visualRotation);
    }

    void UpdateVisuals()
    {
        RefreshWheelVisualPoses();

        float rawSpeed = rb.linearVelocity.magnitude * 3.6f;
        bool canLeanBody = rawSpeed > leanSpeedThreshold;

        float targetLean = canLeanBody
            ? -horizontalInput * (float)maxLeanAngle.Get(this.gameObject)
            : 0f;
        float response = 1f - Mathf.Exp(-Mathf.Max(0.01f, leanSmooth) * Time.deltaTime);
        m_CurrentVisualLean = Mathf.Lerp(m_CurrentVisualLean, targetLean, response);

        Vector3 targetGroundOffset = CalculateVisualGroundOffset();
        m_CurrentVisualGroundOffset = Vector3.Lerp(
            m_CurrentVisualGroundOffset,
            targetGroundOffset,
            response
        );

        if (bikeBody != null)
        {
            ApplyContactPatchLean();
        }

        UpdateWheelVisuals();

        if (steeringWheelMesh != null)
        {
            float targetSteer = -horizontalInput * (float)steeringWheelMaxAngle.Get(this.gameObject);
            Quaternion offset = Quaternion.identity;

            switch (steeringWheelRotationAxis)
            {
                case SteeringWheelRotationAxis.X:
                    offset = Quaternion.Euler(targetSteer, 0f, 0f);
                    break;
                case SteeringWheelRotationAxis.Y:
                    offset = Quaternion.Euler(0f, targetSteer, 0f);
                    break;
                case SteeringWheelRotationAxis.Z:
                    offset = Quaternion.Euler(0f, 0f, targetSteer);
                    break;
            }

            Quaternion finalRotation = _initialSteeringWheelLocalRotation * offset;
            steeringWheelMesh.localRotation = Quaternion.Lerp(
                steeringWheelMesh.localRotation,
                finalRotation,
                Time.deltaTime * leanSmooth
            );
        }
    }

    void StabilizeAirborne()
    {
        float rollAngle = transform.localEulerAngles.z;
        if (rollAngle > 180f) rollAngle -= 360f;
        float stabilizationTorque = -rollAngle * airStabilityCoefficient;
        rb.AddTorque(transform.forward * stabilizationTorque, ForceMode.Acceleration);
    }

    void BalanceBike()
    {
        if (m_GroundedWheelCount == 0)
            return;

        float tiltAngle = Vector3.Angle(transform.up, m_AverageGroundNormal);
        if (tiltAngle > 1f)
        {
            Vector3 tiltAxis = Vector3.Cross(transform.up, m_AverageGroundNormal);
            rb.AddTorque(tiltAxis.normalized * tiltAngle * 500f * Time.fixedDeltaTime, ForceMode.Acceleration);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude > (float)collisionThreshold.Get(this.gameObject))
        {
            float damage = collision.relativeVelocity.magnitude * (float)damageIntensity.Get(this.gameObject);
            float newHealth = (float)currentHealth.Get(this.gameObject) - damage;
            currentHealth.Set(Mathf.Clamp(newHealth, 0, (float)maxDamage.Get(this.gameObject)), this.gameObject);

            VehicleDeformation deformation = GetComponent<VehicleDeformation>();
            if (deformation != null && collision.contacts.Length > 0)
            {
                deformation.ApplyDeformation(collision.contacts[0].point, damage, collision.contacts[0].normal);
            }
            else
            {
                Debug.LogWarning("VehicleDeformation component not found or no collision contacts available.");
            }
        }
    }

    public void ResetVehicle()
    {
        if (Vector3.Dot(transform.up, Vector3.up) < 0.5f)
        {
            Vector3 euler = transform.rotation.eulerAngles;
            euler.x = 0f;
            euler.z = 0f;
            transform.rotation = Quaternion.Euler(euler);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
