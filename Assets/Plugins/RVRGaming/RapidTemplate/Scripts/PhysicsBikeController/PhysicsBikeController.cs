using UnityEngine;
using GameCreator.Runtime.Common;

public enum SteeringWheelRotationAxis
{
    X,
    Y,
    Z
}

[RequireComponent(typeof(Rigidbody))]
public class PhysicsBikeController : MonoBehaviour
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
    private Quaternion _currentLeanOffset = Quaternion.identity;
    private Vector3 frontWheelInitialLocalPos;
    private Vector3 rearWheelInitialLocalPos;
    private Vector3 bikeBodyInitialLocalPos;

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;

    private float m_CurrentGrip;
    private float m_DriftTurningPower;
    [HideInInspector] public bool isDrifting;
    [HideInInspector] public bool handbrakeInput = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
            if (frontWheelTransform != null)
            {
                frontWheelTransform.SetParent(bikeBody, true);
                frontWheelInitialLocalPos = frontWheelTransform.localPosition;
            }
            if (rearWheelTransform != null)
            {
                rearWheelTransform.SetParent(bikeBody, true);
                rearWheelInitialLocalPos = rearWheelTransform.localPosition;
            }
        }
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

        Vector2 move = ReadMovementInput();
        horizontalInput = move.x;
        verticalInput = move.y;

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

        bool isAirborne = (frontWheelCollider != null && !frontWheelCollider.isGrounded) &&
                          (rearWheelCollider != null && !rearWheelCollider.isGrounded);

        if (isAirborne)
        {
            verticalInput = 0f;
            horizontalInput = 0f;
        }

        MoveBike(verticalInput, horizontalInput);

        float rawSpeed = rb.linearVelocity.magnitude * 3.6f;
        currentSpeed.Set(Mathf.RoundToInt(rawSpeed), this.gameObject);

        HandleTransmission();

        Quaternion targetLean = Quaternion.Euler(0f, 0f, -horizontalInput * (float)maxLeanAngle.Get(this.gameObject));
        _currentLeanOffset = Quaternion.Lerp(_currentLeanOffset, targetLean, Time.fixedDeltaTime * leanSmooth);

        UpdateWheelVisuals();
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

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

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
        handbrakeInput = active;
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

        int groundedCount = 0;
        if (frontWheelCollider != null && frontWheelCollider.isGrounded) groundedCount++;
        if (rearWheelCollider != null && rearWheelCollider.isGrounded) groundedCount++;
        float groundPercent = groundedCount / 2f;
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
        if (frontWheelCollider != null && frontWheelTransform != null)
        {
            UpdateWheelVisual(frontWheelCollider, frontWheelTransform);
        }
        if (rearWheelCollider != null && rearWheelTransform != null)
        {
            UpdateWheelVisual(rearWheelCollider, rearWheelTransform);
        }
    }

    void UpdateWheelVisual(WheelCollider collider, Transform wheelTransform)
    {
        if (collider == null || wheelTransform == null) return;

        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);

        Vector3 computedLocalPos = bikeBody.InverseTransformPoint(pos);

        float drop = bikeBodyInitialLocalPos.y - bikeBody.localPosition.y;

        Vector3 newLocalPos;
        if (wheelTransform == frontWheelTransform)
        {
            newLocalPos = new Vector3(frontWheelInitialLocalPos.x, computedLocalPos.y - drop, frontWheelInitialLocalPos.z);
        }
        else if (wheelTransform == rearWheelTransform)
        {
            newLocalPos = new Vector3(rearWheelInitialLocalPos.x, computedLocalPos.y - drop, rearWheelInitialLocalPos.z);
        }
        else
        {
            newLocalPos = computedLocalPos;
        }
        wheelTransform.localPosition = newLocalPos;

        Quaternion localRot = Quaternion.Inverse(transform.rotation) * rot;

        if (wheelTransform == frontWheelTransform)
        {
            float frontSteerAngle = horizontalInput * (float)frontWheelMaxAngle.Get(this.gameObject);
            Quaternion steerRotation = Quaternion.Euler(0, frontSteerAngle, 0);
            localRot = steerRotation * localRot;
        }
        wheelTransform.localRotation = localRot;
    }

    void UpdateVisuals()
    {
        float rawSpeed = rb.linearVelocity.magnitude * 3.6f;
        bool canLeanBody = rawSpeed > leanSpeedThreshold;

        float targetLean = canLeanBody
            ? -horizontalInput * (float)maxLeanAngle.Get(this.gameObject)
            : 0f;
        _currentLeanOffset = Quaternion.Euler(0f, 0f, targetLean);

        if (bikeBody != null)
        {
            bikeBody.localRotation = Quaternion.Lerp(
                bikeBody.localRotation,
                _currentLeanOffset,
                Time.deltaTime * leanSmooth
            );

            Vector3 targetLocalPos = bikeBodyInitialLocalPos;
            if (canLeanBody)
            {
                float leanDropAmount = 0.1f;
                float drop = Mathf.Abs(horizontalInput) * leanDropAmount;
                targetLocalPos.y -= drop;
            }

            bikeBody.localPosition = Vector3.Lerp(
                bikeBody.localPosition,
                targetLocalPos,
                Time.deltaTime * leanSmooth
            );
        }

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
        bool grounded = false;
        Vector3 sumNormals = Vector3.zero;
        int normalCount = 0;
        WheelHit hit;

        if (frontWheelCollider != null && frontWheelCollider.GetGroundHit(out hit))
        {
            sumNormals += hit.normal;
            normalCount++;
            grounded = true;
        }
        if (rearWheelCollider != null && rearWheelCollider.GetGroundHit(out hit))
        {
            sumNormals += hit.normal;
            normalCount++;
            grounded = true;
        }

        if (!grounded || normalCount == 0)
            return;

        Vector3 groundNormal = sumNormals / normalCount;
        float tiltAngle = Vector3.Angle(transform.up, groundNormal);
        if (tiltAngle > 1f)
        {
            Vector3 tiltAxis = Vector3.Cross(transform.up, groundNormal);
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