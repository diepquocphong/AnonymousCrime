using UnityEngine;
using System.Collections.Generic;
using GameCreator.Runtime.Common;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsCarController : MonoBehaviour
{
    [Space]
    public bool isVehicleEnabled = false;

    [Header("External Physics")]
    [Tooltip("Lets a vehicle-specific physics component drive this car while this component continues to provide RVR input, telemetry, fuel, health, vehicle entry and UI compatibility. Leave disabled for the original RVR physics.")]
    [SerializeField] private bool m_UseExternalPhysics;

    [Space]
    [SerializeField] public PropertyGetDecimal topSpeed = new PropertyGetDecimal(160); // km/h
    [SerializeField] public PropertySetNumber currentSpeed = new PropertySetNumber(); // km/h
    [SerializeField] public PropertyGetDecimal acceleration = new PropertyGetDecimal(3f);
    [SerializeField] public PropertyGetDecimal braking = new PropertyGetDecimal(5f);

    [SerializeField] public PropertyGetDecimal reverseSpeed = new PropertyGetDecimal(80); // km/h
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
    [SerializeField] public PropertyGetDecimal maxRPM = new PropertyGetDecimal(8000f);
    [SerializeField] public PropertySetNumber currentRPM = new PropertySetNumber();
    [SerializeField] public PropertyGetDecimal gearRatio = new PropertyGetDecimal(12f);
    [SerializeField] public PropertyGetDecimal idleRPM = new PropertyGetDecimal(800f);
    private int currentGear = 1;

    [Space]
    [SerializeField] public PropertyGetDecimal driftGrip = new PropertyGetDecimal(0.1f);
    [SerializeField] public PropertyGetDecimal driftControl = new PropertyGetDecimal(10f);
    [SerializeField] public PropertyGetDecimal driftDampening = new PropertyGetDecimal(10f);

    [Space]
    [SerializeField] public PropertyGetDecimal maxDamage = new PropertyGetDecimal(100f);
    [SerializeField] public PropertySetNumber currentHealth = new PropertySetNumber();
    [SerializeField] public PropertyGetDecimal collisionThreshold = new PropertyGetDecimal(5f);
    [SerializeField] public PropertyGetDecimal damageIntensity = new PropertyGetDecimal(10f);


    [Space]
    [SerializeField] public PropertyGetDecimal fuelCapacity = new PropertyGetDecimal(100f);
    [SerializeField] public PropertySetNumber currentFuel = new PropertySetNumber();
    [SerializeField] public PropertyGetDecimal fuelEfficiency = new PropertyGetDecimal(1f);

    [Space]
    public Transform frontLeftWheelTransform;
    public Transform frontRightWheelTransform;
    public Transform rearLeftWheelTransform;
    public Transform rearRightWheelTransform;
    [Tooltip("Front Left Wheel Collider")]
    public WheelCollider frontLeftWheelCollider;
    [Tooltip("Front Right Wheel Collider")]
    public WheelCollider frontRightWheelCollider;
    [Tooltip("Rear Left Wheel Collider")]
    public WheelCollider rearLeftWheelCollider;
    [Tooltip("Rear Right Wheel Collider")]
    public WheelCollider rearRightWheelCollider;
    [SerializeField] public PropertyGetDecimal suspensionHeight = new PropertyGetDecimal(0.2f);
    [SerializeField] public PropertyGetDecimal suspensionSpring = new PropertyGetDecimal(20000f);
    [SerializeField] public PropertyGetDecimal suspensionDamp = new PropertyGetDecimal(500f);

    [Space]
    public Transform centerOfMass;

    [Space]
    [Tooltip("The bike body mesh that will lean when steering")]
    public Transform carBody;
    [Tooltip("Steering wheel mesh transform.")]
    [SerializeField] public Transform steeringWheelMesh;
    [Tooltip("Maximum rotation angle for the steering wheel (in degrees).")]
    [SerializeField] private float steeringWheelMaxAngle = 360f;
    public enum Axis { X, Y, Z }
    [Tooltip("Axis around which the steering wheel rotates.")]
    [SerializeField] private Axis steeringWheelRotationAxis = Axis.Z;


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

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;
    [HideInInspector] public bool driftInput;
    [HideInInspector] public bool handbrakeInput;
    [HideInInspector] public bool isDrifting = false;
    private float m_DriftTurningPower = 0f;
    private float m_CurrentGrip;
    private bool inAir = false;
    private float groundPercent;
    private float airPercent;
    private Vector3 verticalReference = Vector3.up;
    private bool hasCollision = false;
    private Vector3 lastCollisionNormal;
    const float k_NullInput = 0.01f;
    const float k_NullSpeed = 0.01f;

    private float frontLeftRoll = 0f;
    private float frontRightRoll = 0f;
    private float rearLeftRoll = 0f;
    private float rearRightRoll = 0f;
    private Quaternion steeringWheelInitialRotation;

    /// <summary>
    /// The current RVR movement input. x is steering and y is throttle/brake.
    /// It is intended for an optional vehicle-specific external physics component.
    /// </summary>
    public Vector2 MovementInput => new Vector2(horizontalInput, verticalInput);

    public bool UsesExternalPhysics => this.m_UseExternalPhysics;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (centerOfMass != null)
            rb.centerOfMass = centerOfMass.localPosition;

        if (steeringWheelMesh != null)
            steeringWheelInitialRotation = steeringWheelMesh.localRotation;

        m_CurrentGrip = (float)grip.Get(this.gameObject);
        SetupSuspension();
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
        else Debug.LogWarning($"[{nameof(PhysicsCarController)}] Move action '{moveActionName}' " +
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

        if (Mathf.Abs(verticalInput) > k_NullInput && (float)currentFuel.Get(this.gameObject) > 0)
        {
            float newFuel = (float)currentFuel.Get(this.gameObject) - ((float)fuelEfficiency.Get(this.gameObject) * Mathf.Abs(verticalInput) * Time.deltaTime);
            currentFuel.Set(Mathf.Clamp(newFuel, 0, (float)fuelCapacity.Get(this.gameObject)), this.gameObject);
        }
    }

    void FixedUpdate()
    {
        if (!isVehicleEnabled)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        // A per-prefab external controller can opt in without changing the default
        // RVR behaviour used by any other vehicle prefab.
        if (this.m_UseExternalPhysics) return;

        SetupSuspension();

        int groundedCount = 0;
        if (frontLeftWheelCollider != null && frontLeftWheelCollider.isGrounded) groundedCount++;
        if (frontRightWheelCollider != null && frontRightWheelCollider.isGrounded) groundedCount++;
        if (rearLeftWheelCollider != null && rearLeftWheelCollider.isGrounded) groundedCount++;
        if (rearRightWheelCollider != null && rearRightWheelCollider.isGrounded) groundedCount++;
        groundPercent = groundedCount / 4f;
        airPercent = 1f - groundPercent;

        if (airPercent >= 1f)
            rb.AddForce(Physics.gravity * ((float)addedGravity.Get(this.gameObject) - 1f) * rb.mass);

        MoveVehicle(verticalInput, horizontalInput);
        FixedUpdateRolls();

        float rawSpeed = rb.linearVelocity.magnitude * 3.6f;
        if (rawSpeed < 0.1f) rawSpeed = 0f;
        currentSpeed.Set(Mathf.RoundToInt(rawSpeed), this.gameObject);

        float wheelRadius = frontLeftWheelCollider.radius;
        float wheelCircumference = 2 * Mathf.PI * wheelRadius;
        float wheelRPM = rb.linearVelocity.magnitude / wheelCircumference * 60f;
        float baseGearRatio = (float)gearRatio.Get(this.gameObject);
        float effectiveGearRatio = baseGearRatio / currentGear;
        float engineRPM = wheelRPM * effectiveGearRatio;
        if (engineRPM < (float)idleRPM.Get(this.gameObject))
            engineRPM = (float)idleRPM.Get(this.gameObject);

        float maxRPMValue = (float)maxRPM.Get(this.gameObject);
        float idleRPMValue = (float)idleRPM.Get(this.gameObject);
        bool shiftUp = engineRPM > 0.95f * maxRPMValue && currentGear < numberOfGears;
        bool shiftDown = engineRPM < idleRPMValue + 0.3f * (maxRPMValue - idleRPMValue) && currentGear > 1;
        if (shiftUp)
        {
            currentGear++;
            engineRPM = idleRPMValue;
            rb.linearVelocity *= 0.9f;
        }
        else if (shiftDown)
        {
            currentGear--;
            engineRPM = idleRPMValue;
            rb.linearVelocity *= 0.9f;
        }
        currentRPM.Set(engineRPM, this.gameObject);

        // Check if the car is fully repaired (health restored to max)
        if ((float)currentHealth.Get(this.gameObject) >= (float)maxDamage.Get(this.gameObject))
        {
            VehicleDeformation deformation = GetComponent<VehicleDeformation>();
            if (deformation != null)
            {
                deformation.ResetDeformation();
            }
        }

        UpdateVisuals();
    }

    public void SetCarEnabled(bool state)
    {
        isVehicleEnabled = state;

        if (!state)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void SetDriftInput(bool active)
    {
        driftInput = active;
    }

    public void SetHandbrakeInput(bool active)
    {
        handbrakeInput = active;
    }

    void MoveVehicle(float motorInput, float turnInput)
    {
        float steeringAngle = turnInput * (float)steer.Get(this.gameObject);

        frontLeftWheelCollider.steerAngle = steeringAngle;
        frontRightWheelCollider.steerAngle = steeringAngle;

        float effectiveMotorInput = ((float)currentFuel.Get(this.gameObject) <= 0 || (float)currentHealth.Get(this.gameObject) <= 0) ? 0f : motorInput;
        float effectiveTurnInput = ((float)currentFuel.Get(this.gameObject) <= 0 || (float)currentHealth.Get(this.gameObject) <= 0) ? 0f : turnInput;

        if (rb.linearVelocity.magnitude < 0.5f)
        {
            effectiveTurnInput = 0f;
        }

        bool driftMode = handbrakeInput && Mathf.Abs(effectiveTurnInput) >= 0.1f;

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

        float turningPower = driftMode ? m_DriftTurningPower : effectiveTurnInput * (float)steer.Get(this.gameObject);

        Quaternion turnAngle = Quaternion.AngleAxis(turningPower, transform.up);
        Vector3 fwd = turnAngle * transform.forward;
        Vector3 movement = fwd * accelInput * finalAcceleration * ((hasCollision || groundPercent > 0f) ? 1f : 0f);
        bool wasOverMaxSpeed = currentSpeed >= maxSpeed;
        if (wasOverMaxSpeed && !isBraking)
            movement = Vector3.zero;
        Vector3 newVelocity = rb.linearVelocity + movement * Time.fixedDeltaTime;
        newVelocity.y = rb.linearVelocity.y;
        newVelocity = Vector3.ClampMagnitude(newVelocity, maxSpeed);
        if (Mathf.Abs(accelInput) < k_NullInput && groundPercent > 0f)
            newVelocity = Vector3.MoveTowards(newVelocity, new Vector3(0, rb.linearVelocity.y, 0), Time.fixedDeltaTime * (float)coastingDrag.Get(this.gameObject));
        rb.linearVelocity = newVelocity;

        if (groundPercent > 0f)
        {
            if (inAir)
                inAir = false;
            float angularVelocitySteering = 0.4f;
            float angularVelocitySmoothSpeed = 20f;
            if (!localVelDirectionIsFwd && !accelDirectionIsFwd)
                angularVelocitySteering *= -1f;
            Vector3 angularVel = rb.angularVelocity;
            angularVel.y = Mathf.MoveTowards(angularVel.y, turningPower * angularVelocitySteering, Time.fixedDeltaTime * angularVelocitySmoothSpeed);
            rb.angularVelocity = angularVel;

            float velocitySteering = 25f;
            float speed = rb.linearVelocity.magnitude;
            float lowerBound = 2.78f;
            float upperBound = 5f;
            float steeringMultiplier = Mathf.Clamp01((speed - lowerBound) / (upperBound - lowerBound));
            rb.linearVelocity = Quaternion.AngleAxis(
                turningPower * Mathf.Sign(localVel.z) * velocitySteering * m_CurrentGrip * Time.fixedDeltaTime * steeringMultiplier,
                transform.up) * rb.linearVelocity;

            float currentSpeedKPH = rb.linearVelocity.magnitude * 3.6f;
            if (driftMode && currentSpeedKPH > 1f)
            {
                isDrifting = true;
                m_CurrentGrip = Mathf.Lerp(m_CurrentGrip, 0.8f, (float)driftDampening.Get(this.gameObject) * Time.fixedDeltaTime);
                m_DriftTurningPower = Mathf.Lerp(m_DriftTurningPower, effectiveTurnInput * 5f, (float)driftDampening.Get(this.gameObject) * Time.fixedDeltaTime);
            }
            else
            {
                isDrifting = false;
                m_CurrentGrip = Mathf.Lerp(m_CurrentGrip, (float)grip.Get(this.gameObject), 2f * Time.fixedDeltaTime);
                m_DriftTurningPower = 0f;
            }
            turningPower = isDrifting ? m_DriftTurningPower : effectiveTurnInput * (float)steer.Get(this.gameObject);
        }
        else
        {
            inAir = true;
            isDrifting = false;
            m_DriftTurningPower = 0f;
        }
    }

    void SetupSuspension()
    {
        SetupWheelSuspension(frontLeftWheelCollider);
        SetupWheelSuspension(frontRightWheelCollider);
        SetupWheelSuspension(rearLeftWheelCollider);
        SetupWheelSuspension(rearRightWheelCollider);
    }

    void SetupWheelSuspension(WheelCollider wheel)
    {
        if (wheel == null)
            return;
        wheel.suspensionDistance = (float)suspensionHeight.Get(this.gameObject);
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = (float)suspensionSpring.Get(this.gameObject);
        spring.damper = (float)suspensionDamp.Get(this.gameObject);
        wheel.suspensionSpring = spring;
    }

    [ContextMenu("Auto Setup Wheel Colliders")]
    public void AutoSetupWheelColliders()
    {
        if (frontLeftWheelTransform != null)
            frontLeftWheelCollider = CreateWheelColliderForWheel(frontLeftWheelTransform, "FrontLeftWheelCollider");
        if (frontRightWheelTransform != null)
            frontRightWheelCollider = CreateWheelColliderForWheel(frontRightWheelTransform, "FrontRightWheelCollider");
        if (rearLeftWheelTransform != null)
            rearLeftWheelCollider = CreateWheelColliderForWheel(rearLeftWheelTransform, "RearLeftWheelCollider");
        if (rearRightWheelTransform != null)
            rearRightWheelCollider = CreateWheelColliderForWheel(rearRightWheelTransform, "RearRightWheelCollider");
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

    void OnCollisionEnter(Collision collision)
    {
        hasCollision = true;
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

    void OnCollisionExit(Collision collision)
    {
        hasCollision = false;
    }

    void OnCollisionStay(Collision collision)
    {
        hasCollision = true;
        lastCollisionNormal = Vector3.zero;
        float dot = -1f;
        foreach (var contact in collision.contacts)
        {
            if (Vector3.Dot(contact.normal, Vector3.up) > dot)
            {
                lastCollisionNormal = contact.normal;
                dot = Vector3.Dot(contact.normal, Vector3.up);
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

    void UpdateVisuals()
    {
        UpdateFrontWheelVisual(frontLeftWheelCollider, frontLeftWheelTransform, ref frontLeftRoll);
        UpdateFrontWheelVisual(frontRightWheelCollider, frontRightWheelTransform, ref frontRightRoll);
        UpdateRearWheelVisual(rearLeftWheelCollider, rearLeftWheelTransform, ref rearLeftRoll);
        UpdateRearWheelVisual(rearRightWheelCollider, rearRightWheelTransform, ref rearRightRoll);

        if (steeringWheelMesh != null)
        {
            float angle = horizontalInput * steeringWheelMaxAngle;

            Vector3 axis;
            switch (steeringWheelRotationAxis)
            {
                default:
                case Axis.X: axis = Vector3.right; break;
                case Axis.Y: axis = Vector3.up; break;
                case Axis.Z: axis = Vector3.forward; break;
            }

            steeringWheelMesh.localRotation =
                steeringWheelInitialRotation
                * Quaternion.AngleAxis(-angle, axis);
        }
    }

    void UpdateFrontWheelVisual(WheelCollider collider, Transform pivot, ref float roll)
    {
        if (collider == null || pivot == null) return;

        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);

        pivot.position = pos;

        float visualSteerFactor = 15f;
        float visualSteerAngle = collider.steerAngle * visualSteerFactor;
        visualSteerAngle = Mathf.Clamp(visualSteerAngle, -45f, 45f);

        pivot.localRotation = Quaternion.Euler(0f, visualSteerAngle, 0f);

        if (pivot.childCount > 0)
        {
            Transform mesh = pivot.GetChild(0);
            mesh.localRotation = Quaternion.Euler(roll, 0f, 0f);
        }
    }

    void UpdateRearWheelVisual(WheelCollider collider, Transform pivot, ref float roll)
    {
        if (collider == null || pivot == null) return;
        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);

        pivot.position = pos;
        if (pivot.childCount > 0)
        {
            Transform mesh = pivot.GetChild(0);
            mesh.localRotation = Quaternion.Euler(roll, 0f, 0f);
        }
    }

    void LateUpdate()
    {
        // Keep the stock parked-wheel pose while a vehicle-specific external
        // controller is waiting for CarEntry to finish. Once driven, that
        // controller owns the visuals and RVR stops updating them.
        if (this.m_UseExternalPhysics && isVehicleEnabled) return;
        UpdateVisuals();
    }

    /// <summary>
    /// Allows an external physics implementation to keep the existing RVR audio,
    /// UI and visual-scripting properties in sync.
    /// </summary>
    public void SetExternalTelemetry(float speedKph, float rpm)
    {
        this.currentSpeed.Set(Mathf.RoundToInt(Mathf.Max(0f, speedKph)), this.gameObject);
        this.currentRPM.Set(Mathf.Max(0f, rpm), this.gameObject);
    }

    void FixedUpdateRolls()
    {
        float deltaRoll = (rb.linearVelocity.magnitude / frontLeftWheelCollider.radius) * Time.fixedDeltaTime * Mathf.Rad2Deg;
        frontLeftRoll += deltaRoll;
        frontRightRoll += deltaRoll;
        rearLeftRoll += deltaRoll;
        rearRightRoll += deltaRoll;
    }

    void FixedUpdateWrapper()
    {
        FixedUpdate();
        FixedUpdateRolls();
    }
}
