using System.Collections;
using GameCreator.Runtime.Common;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HoverVehicleController : MonoBehaviour
{
    [Space]
    [Tooltip("Master enable/disable for this vehicle")]
    public bool isVehicleEnabled = true;

    [HideInInspector] public Vector2 MovementInput;
    [HideInInspector][SerializeField] public PropertyGetDecimal LetOffBuildTime = new PropertyGetDecimal(0f);

    [Space]
    [SerializeField] public PropertySetNumber currentSpeed = new PropertySetNumber();
    [SerializeField] public PropertyGetDecimal MoveForce = new PropertyGetDecimal(560f);
    [SerializeField] public PropertyGetDecimal ReversingForce = new PropertyGetDecimal(350f);
    [SerializeField] public PropertyGetDecimal TurnForce = new PropertyGetDecimal(150f);
    [SerializeField] public PropertyGetDecimal BoostForce = new PropertyGetDecimal(115f);
    [SerializeField] public PropertyGetDecimal HoverMultiplerInMotion = new PropertyGetDecimal(3f);
    [SerializeField] public PropertyGetDecimal HoverMultiplierAtRest = new PropertyGetDecimal(0.4f);
    [SerializeField] public PropertyGetDecimal HoverExponent = new PropertyGetDecimal(2f);

    [Space]
    [SerializeField] public PropertyGetDecimal maxBoost = new PropertyGetDecimal(5f);
    [SerializeField] public PropertySetNumber currentBoost = new PropertySetNumber();
    [SerializeField] public PropertyGetDecimal boostEfficiency = new PropertyGetDecimal(1f);
    [SerializeField] public PropertyGetDecimal BoostCooldown = new PropertyGetDecimal(0.2f);
    [SerializeField] public PropertyGetDecimal AirControlMultiplier = new PropertyGetDecimal(0.5f);

    [Space]
    [SerializeField] public PropertyGetDecimal MaxDownhillForce = new PropertyGetDecimal(400f);
    [SerializeField] public PropertyGetDecimal MaxDownhillAngle = new PropertyGetDecimal(45f);
    [SerializeField] public PropertyGetDecimal UpthrustAngleLimit = new PropertyGetDecimal(90f);
    [SerializeField] public PropertyGetDecimal SlopeAngleLimit = new PropertyGetDecimal(90f);

    [Space]
    [SerializeField] public PropertyGetDecimal GroundedThreshold = new PropertyGetDecimal(5f);
    [SerializeField] public PropertyGetDecimal LandingDownforceDuration = new PropertyGetDecimal(1.5f);
    [SerializeField] public PropertyGetDecimal DescentSlowMultiplier = new PropertyGetDecimal(1f);
    [SerializeField] public PropertyGetDecimal DescentSlowExponent = new PropertyGetDecimal(1.5f);
    [SerializeField] public PropertyGetDecimal DescentSlowSpeedMultiplier = new PropertyGetDecimal(0.1f);

    [Space]
    [SerializeField] public PropertyGetDecimal DragWhenGrounded = new PropertyGetDecimal(1f);
    [SerializeField] public PropertyGetDecimal DragInAir = new PropertyGetDecimal(0.75f);

    [Space]
    [SerializeField] public PropertyGetDecimal ZRebalanceThreshold = new PropertyGetDecimal(20f);
    [SerializeField] public PropertyGetDecimal ZRebalanceForce = new PropertyGetDecimal(20f);
    [SerializeField] public PropertyGetDecimal XRebalanceThreshold = new PropertyGetDecimal(30f);
    [SerializeField] public PropertyGetDecimal XRebalanceForce = new PropertyGetDecimal(40f);
    [SerializeField] public PropertyGetDecimal MaxBankAngle = new PropertyGetDecimal(45f);
    [SerializeField] public PropertyGetDecimal BankSpeed = new PropertyGetDecimal(0.03f);

    [Space]
    [SerializeField] public PropertyGetDecimal ThrottleAcceleration = new PropertyGetDecimal(0.8f);
    [SerializeField] public PropertyGetDecimal ThrottleMaxSpeed = new PropertyGetDecimal(0.05f);
    [SerializeField] public PropertyGetDecimal BoostThrottleAcceleration = new PropertyGetDecimal(0.5f);
    [SerializeField] public PropertyGetDecimal BoostThrottleMaxSpeed = new PropertyGetDecimal(0.5f);

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
    [Tooltip("Points of the vehicle where the hover force is applied")]
    public Transform[] hoverPoints;

    [Space]
    [Tooltip("Optional: the child transform to tilt when turning")]
    public Transform modelTransform;

    [Space]
    [Tooltip("Layers that the hover vehicle considers terrain for physics purposes")]
    public LayerMask TerrainMask;


    public enum InputMode { Legacy, Both, New }
    [Tooltip("Which input system to read driving input from at runtime. " +
             "New/Both require the Input System package installed.")]
    [SerializeField] public InputMode inputMode = InputMode.Legacy;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Input Action Asset used when Input Mode is New or Both.")]
    [SerializeField] public UnityEngine.InputSystem.InputActionAsset inputActionAsset;
    [Tooltip("The Vector2 \"Move\" action inside the asset (x = strafe/turn, y = forward/back).")]
    [RVRInputActionPicker("inputActionAsset")]
    [SerializeField] public string moveActionName = "Move";
    private UnityEngine.InputSystem.InputAction m_MoveAction;
#endif

    private float Throttle, ThrottleSpeed;
    private float BoostThrottle, BoostThrottleSpeed;
    private bool IsGrounded, PreviousGrounded;
    private float LandingDownforceCounter = -1f;
    private bool DescentSlowAvailable = true;
    private float CurrentBoostForce, BoostCooldownCounter;
    private Vector3 MomentumVector;
    private float zBankAngle;
    private bool invertHorizontalWhenReversing = true;
    private Rigidbody rb;
    [HideInInspector] public bool FXActive = false;
    [HideInInspector] public bool IsBoosted;


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.ResetInertiaTensor();

        currentHealth.Set((int)maxDamage.Get(gameObject), this.gameObject);
        currentFuel.Set((int)fuelCapacity.Get(gameObject), this.gameObject);
        currentBoost.Set((int)maxBoost.Get(gameObject), this.gameObject);

        BoostCooldownCounter = (float)BoostCooldown.Get(gameObject) + 1f;
        MomentumVector = transform.forward;
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
        else Debug.LogWarning($"[{nameof(HoverVehicleController)}] Move action '{moveActionName}' " +
                              "was not found in the assigned Input Action Asset.", this);
    }
#endif

    // Returns combined movement input. x = strafe/turn, y = forward/back.
    private Vector2 ReadMovementInput()
    {
        Vector2 result = Vector2.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (inputMode == InputMode.Legacy || inputMode == InputMode.Both)
        {
            float left = Input.GetKey(KeyCode.A) ? -1f : 0f;
            float right = Input.GetKey(KeyCode.D) ? 1f : 0f;
            float forward = Input.GetKey(KeyCode.W) ? 1f : 0f;
            float back = Input.GetKey(KeyCode.S) ? -1f : 0f;
            result.x += left + right;
            result.y += forward + back;
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

    public void SetBoostInput(bool active)
    {
        if (active && !IsBoosted && (float)currentBoost.Get(gameObject) > 0f)
        {
            IsBoosted = true;
            CurrentBoostForce = (float)BoostForce.Get(gameObject);
            BoostCooldownCounter = 0f;
            StartCoroutine(BoostCoroutine());
        }
        else if (!active && IsBoosted)
        {
            IsBoosted = false;
            CurrentBoostForce = 0f;
            StartCoroutine(BoostCooldownCoroutine());
        }
    }

    private IEnumerator DescentSlowCooldown()
    {
        yield return new WaitForSeconds(0.5f);
        DescentSlowAvailable = true;
    }

    void Update()
    {
        if (!isVehicleEnabled) { MovementInput = Vector2.zero; return; }

        MovementInput = ReadMovementInput();
        float forward = Mathf.Max(0f, MovementInput.y);

        float fuel = (float)currentFuel.Get(gameObject);
        if (forward > 0f && fuel > 0f)
        {
            float newFuel = fuel - ((float)fuelEfficiency.Get(gameObject) * forward * Time.deltaTime);
            currentFuel.Set(Mathf.Clamp(newFuel, 0f, (float)fuelCapacity.Get(gameObject)), this.gameObject);
        }

        if (IsBoosted)
        {
            float boostLeft = (float)currentBoost.Get(gameObject);
            if (boostLeft > 0f)
            {
                float use = (float)boostEfficiency.Get(gameObject) * Time.deltaTime;
                currentBoost.Set(Mathf.Clamp(boostLeft - use, 0f, (float)maxBoost.Get(gameObject)), this.gameObject);
            }
            else
            {
                SetBoostInput(false);
            }
        }

        float throttleAcc = (float)ThrottleAcceleration.Get(gameObject);
        float throttleMax = (float)ThrottleMaxSpeed.Get(gameObject);
        float boostAcc = (float)BoostThrottleAcceleration.Get(gameObject);
        float boostMax = (float)BoostThrottleMaxSpeed.Get(gameObject);

        bool verticalInput = Mathf.Abs(MovementInput.y) > 0.01f;
        if (!verticalInput)
        {
            Throttle = 0f;
            ThrottleSpeed = 0f;
        }
        else if (MovementInput.y > 0f)
        {
            if (ThrottleSpeed < 0f) ThrottleSpeed = 0f;
            ThrottleSpeed = Mathf.Clamp(
                ThrottleSpeed + throttleAcc * Time.deltaTime,
                -throttleMax,
                throttleMax
            );
        }
        else
        {
            if (ThrottleSpeed > 0f) ThrottleSpeed = 0f;
            ThrottleSpeed = Mathf.Clamp(
                ThrottleSpeed - throttleAcc * Time.deltaTime,
                -throttleMax,
                throttleMax
            );
        }

        Throttle = Mathf.Clamp01(Throttle + ThrottleSpeed);

        if (IsBoosted)
        {
            if (BoostThrottleSpeed < 0f) BoostThrottleSpeed = 0f;
            BoostThrottleSpeed = Mathf.Clamp(
                BoostThrottleSpeed + boostAcc * Time.deltaTime,
                -boostMax,
                boostMax
            );
        }
        else
        {
            if (BoostThrottle > 0.99f) BoostThrottleSpeed = 0f;
            BoostThrottleSpeed = Mathf.Clamp(
                BoostThrottleSpeed - boostAcc * Time.deltaTime,
                -boostMax,
                boostMax
            );
        }

        BoostThrottle = Mathf.Clamp01(BoostThrottle + BoostThrottleSpeed);

        Debug.DrawLine(
            transform.position,
            transform.position + MomentumVector * 3f,
            Color.red,
            Time.deltaTime
        );
    }

    void FixedUpdate()
    {
        if (!isVehicleEnabled)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        bool hitCenter = Physics.Raycast(
            transform.position, -transform.up,
            out RaycastHit centerHit, 3000f, TerrainMask
        );
        float height = hitCenter ? centerHit.distance : 100f;
        IsGrounded = hitCenter && height <= (float)GroundedThreshold.Get(gameObject);

        rb.linearDamping = IsGrounded ? (float)DragWhenGrounded.Get(gameObject) : (float)DragInAir.Get(gameObject);

        float rawSpeedKmh = rb.linearVelocity.magnitude * 3.6f;
        currentSpeed.Set(Mathf.RoundToInt(rawSpeedKmh), this.gameObject);

        if ((float)currentFuel.Get(gameObject) <= 0f || (float)currentHealth.Get(gameObject) <= 0f)
        {
            IsBoosted = false;
            return;
        }

        float fuelRemaining = (float)currentFuel.Get(gameObject);
        float healthRemaining = (float)currentHealth.Get(gameObject);
        if (fuelRemaining <= 0f || healthRemaining <= 0f)
        {
            IsBoosted = false;
            return;
        }

        rb.linearDamping = IsGrounded
            ? (float)DragWhenGrounded.Get(gameObject)
            : (float)DragInAir.Get(gameObject);

        Vector3 fwdXZ = transform.forward; fwdXZ.y = 0; fwdXZ.Normalize();
        Vector3 rightXZ = transform.right; rightXZ.y = 0; rightXZ.Normalize();
        Vector3 finalMove = (rightXZ * MovementInput.x + fwdXZ * MovementInput.y).normalized;

        float baseMove = (float)MoveForce.Get(gameObject);
        float boostAdd = IsBoosted ? (float)BoostForce.Get(gameObject) : 0f;
        float revForceMax = (float)ReversingForce.Get(gameObject);

        float forwardPercent = Throttle + BoostThrottle;
        float reversePercent = MovementInput.y < 0f
            ? Mathf.Clamp01(-MovementInput.y)
            : 0f;
        float forwardForce = Mathf.Lerp(0f, baseMove + boostAdd, forwardPercent);
        float reverseForce = reversePercent * revForceMax;

        if (IsGrounded)
        {
            if (reversePercent > 0f)
            {
                rb.AddForce(transform.forward * -reverseForce);
            }
            else if (forwardPercent > 0f)
            {
                MomentumVector = Vector3.Slerp(
                    MomentumVector,
                    transform.forward,
                    Time.deltaTime * 5f
                ).normalized;
                rb.AddForce(MomentumVector * forwardForce);
            }

            for (int i = 0; i < hoverPoints.Length; i++)
                ApplyHoverPointForce(hoverPoints[i]);

            if (height <= 0.5f && rb.linearVelocity.y < 0f)
            {
                rb.linearVelocity = new Vector3(
                    rb.linearVelocity.x,
                    rb.linearVelocity.y * 0.5f,
                    rb.linearVelocity.z
                );
            }

            if (LandingDownforceCounter >= 0f &&
                LandingDownforceCounter < (float)LandingDownforceDuration.Get(gameObject))
            {
                LandingDownforceCounter += Time.deltaTime;
                for (int i = 0; i < hoverPoints.Length; i++)
                {
                    Transform hp = hoverPoints[i];
                    if (Physics.Raycast(
                        hp.position, -hp.up,
                        out RaycastHit hit, 2f * (float)GroundedThreshold.Get(gameObject),
                        TerrainMask
                    ))
                    {
                        float downForce = Mathf.Clamp(0.5f * hit.distance, 0f, 3f);
                        rb.AddForceAtPosition(-hp.up * downForce, hp.position, ForceMode.Acceleration);
                    }
                }
            }
            else if (LandingDownforceCounter >= (float)LandingDownforceDuration.Get(gameObject))
            {
                LandingDownforceCounter = -1f;
            }
        }
        else
        {
            Vector3 airCtrl = finalMove
                * (forwardForce * (float)AirControlMultiplier.Get(gameObject));
            rb.AddForce(airCtrl);
        }

        float signed = Vector3.SignedAngle(fwdXZ, finalMove, Vector3.up);
        float absA = Mathf.Abs(signed);
        float turnMult = (invertHorizontalWhenReversing && reversePercent > 0f)
            ? -1f : 1f;
        float torqueVal = (float)TurnForce.Get(gameObject) * (absA / 60f);

        if (MovementInput.x > 0.05f && signed > 1f)
        {
            rb.AddTorque(turnMult * torqueVal * transform.up);
            zBankAngle = Mathf.Lerp(
                zBankAngle,
                -(float)MaxBankAngle.Get(gameObject) * (absA / 60f),
                (float)BankSpeed.Get(gameObject)
            );
        }
        else if (MovementInput.x < -0.05f && signed < -1f)
        {
            rb.AddTorque(turnMult * -torqueVal * transform.up);
            zBankAngle = Mathf.Lerp(
                zBankAngle,
                (float)MaxBankAngle.Get(gameObject) * (absA / 60f),
                (float)BankSpeed.Get(gameObject)
            );
        }
        else
        {
            rb.angularVelocity = new Vector3(
                rb.angularVelocity.x,
                rb.angularVelocity.y * 0.8f,
                rb.angularVelocity.z
            );
            zBankAngle = Mathf.Lerp(zBankAngle, 0f, (float)BankSpeed.Get(gameObject));
        }

        RebalanceAxis(
            ref zBankAngle,
            transform.localEulerAngles.z,
            ZRebalanceThreshold,
            ZRebalanceForce,
            Vector3.forward
        );
        RebalanceAxis(
            ref zBankAngle,
            transform.localEulerAngles.x,
            XRebalanceThreshold,
            XRebalanceForce,
            Vector3.right
        );

        float vertSpeed = Mathf.Abs(rb.linearVelocity.y);
        if (!PreviousGrounded && IsGrounded)
        {
            if (DescentSlowAvailable)
            {
                DescentSlowAvailable = false;
                StartCoroutine(DescentSlowCooldown());

                if (vertSpeed > 1f)
                {
                    float slowForce =
                        Mathf.Pow(
                            vertSpeed * (float)DescentSlowSpeedMultiplier.Get(gameObject),
                            (float)DescentSlowExponent.Get(gameObject)
                        )
                        * (float)DescentSlowMultiplier.Get(gameObject);

                    rb.linearVelocity = new Vector3(
                        rb.linearVelocity.x,
                        rb.linearVelocity.y + slowForce,
                        rb.linearVelocity.z
                    );
                }

                if (vertSpeed > 12f)
                {
                    LandingDownforceCounter = 0f;
                }
            }
        }

        PreviousGrounded = IsGrounded;
    }

    void LateUpdate()
    {
        if (modelTransform != null)
        {
            Vector3 e = modelTransform.localEulerAngles;
            e.z = zBankAngle;
            modelTransform.localEulerAngles = e;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        foreach (ContactPoint cp in collision.contacts)
        {
            if (Vector3.Dot(cp.normal, Vector3.up) > 0.7f) return;
        }

        float impact = collision.relativeVelocity.magnitude;
        if (impact <= (float)collisionThreshold.Get(gameObject)) return;

        float damage = impact * (float)damageIntensity.Get(gameObject);
        float newHealth = (float)currentHealth.Get(gameObject) - damage;
        currentHealth.Set(Mathf.Clamp(newHealth, 0f, (float)maxDamage.Get(gameObject)), this.gameObject);

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

    private void StartBoost()
    {
        float cd = (float)BoostCooldown.Get(gameObject);
        if (!IsBoosted && BoostCooldownCounter > cd)
        {
            IsBoosted = true;
            CurrentBoostForce = (float)BoostForce.Get(gameObject);
            BoostCooldownCounter = 0f;
            StartCoroutine(BoostCoroutine());
        }
    }

    private void EndBoost()
    {
        if (IsBoosted)
        {
            IsBoosted = false;
            CurrentBoostForce = 0f;
            StartCoroutine(BoostCooldownCoroutine());
        }
    }

    private IEnumerator BoostCoroutine()
    {
        float timer = 0f;
        while (IsBoosted && timer < 1f)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator BoostCooldownCoroutine()
    {
        float cd = (float)BoostCooldown.Get(gameObject);
        while (BoostCooldownCounter < cd)
        {
            BoostCooldownCounter += Time.deltaTime;
            yield return null;
        }
        BoostCooldownCounter = cd + 1f;
    }

    private void ApplyHoverPointForce(Transform point)
    {
        var height = (float)GroundedThreshold.Get(gameObject);
        var exponent = (float)HoverExponent.Get(gameObject);
        var multi = MovementInput.magnitude > 0
                        ? (float)HoverMultiplerInMotion.Get(gameObject)
                        : (float)HoverMultiplierAtRest.Get(gameObject);

        if (Physics.Raycast(point.position, -point.up, out RaycastHit hit, height * 2f, TerrainMask))
        {
            if (Vector3.Angle(Vector3.up, hit.normal) < (float)UpthrustAngleLimit.Get(gameObject))
            {
                float dist = Vector3.Distance(point.position, hit.point);
                float force = Mathf.Clamp(1f / Mathf.Pow(dist, exponent) * multi, 0f, 20f);
                rb.AddForceAtPosition(point.up * force, point.position, ForceMode.Acceleration);
            }
        }
    }

    private void RebalanceAxis(ref float bankAngle, float angleDeg, PropertyGetDecimal thresh, PropertyGetDecimal forceProp, Vector3 axis)
    {
        float angle = angleDeg > 180f ? angleDeg - 360f : angleDeg;
        float amt = Mathf.Abs(angle) / 45f;
        if (Mathf.Abs(angle) > (float)thresh.Get(gameObject))
        {
            float f = (float)forceProp.Get(gameObject) * (amt * amt);
            rb.AddRelativeTorque(-Mathf.Sign(angle) * f * axis);
        }
    }
}