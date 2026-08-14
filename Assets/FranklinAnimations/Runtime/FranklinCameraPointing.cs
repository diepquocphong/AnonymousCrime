using System.Reflection;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Turns the on-foot Player towards the active camera and uses Humanoid IK to point the
    /// right index finger along the camera centre ray. GC2 keeps ownership of locomotion.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(980)]
    public sealed class FranklinCameraPointing : MonoBehaviour
    {
        private const float CAMERA_LOOKUP_RETRY_SECONDS = 0.5f;
        private const float ACTIVE_WEIGHT_EPSILON = 0.0001f;
        private const BindingFlags PRIVATE_INSTANCE =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo CAMERA_SHOULDER_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Shoulder", PRIVATE_INSTANCE);
        private static readonly FieldInfo CAMERA_LIFT_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Lift", PRIVATE_INSTANCE);
        private static readonly FieldInfo CAMERA_RADIUS_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_Radius", PRIVATE_INSTANCE);
        private static readonly FieldInfo CAMERA_SENSITIVITY_X_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_SensitivityX", PRIVATE_INSTANCE);
        private static readonly FieldInfo CAMERA_SENSITIVITY_Y_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_SensitivityY", PRIVATE_INSTANCE);
        private static readonly FieldInfo CAMERA_MAX_YAW_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_MaxYaw", PRIVATE_INSTANCE);
        private static readonly FieldInfo CAMERA_SMOOTH_TIME_FIELD =
            typeof(ShotSystemThirdPerson).GetField("m_SmoothTime", PRIVATE_INSTANCE);

        [Header("Character and camera")]
        [SerializeField]
        [Tooltip("GC2 Character to pose. Resolved from a parent when omitted.")]
        private Character m_Player;
        [SerializeField]
        [Tooltip("Optional camera override. The active GC2 Main Camera is used when empty.")]
        private Transform m_CameraOverride;

        [Header("Camera Shot while pointing")]
        [SerializeField]
        [Tooltip("Temporarily applies these values to the current GC2 Third Person Camera Shot while pointing, then restores its previous values.")]
        private bool m_OverrideCameraShotWhilePointing = true;
        [SerializeField]
        [Tooltip("Optional Shot Camera to adjust. The active GC2 Main Camera Shot is used when empty.")]
        private ShotCamera m_PointingShot;
        [SerializeField, Min(0f)]
        [Tooltip("Seconds used to blend into the Pointing Camera Shot values with Quad In/Out easing.")]
        private float m_PointingCameraBlendInTime = 0.35f;
        [SerializeField, Min(0f)]
        [Tooltip("Seconds used to blend back to the original Camera Shot values with Quad In/Out easing.")]
        private float m_PointingCameraBlendOutTime = 0.30f;
        [SerializeField] private float m_PointingShoulder = 0.5f;
        [SerializeField] private float m_PointingLift = 0.5f;
        [SerializeField, Min(0.01f)] private float m_PointingRadius = 3f;
        [SerializeField] private bool m_OverridePointingSensitivity = true;
        [SerializeField, Min(0f)] private float m_PointingSensitivityX = 0.2f;
        [SerializeField, Min(0f)] private float m_PointingSensitivityY = 0.2f;
        [SerializeField, Range(1f, 179f)] private float m_PointingMaxPitch = 150f;
        [SerializeField] private bool m_EnablePointingMaxYaw;
        [SerializeField, Range(0f, 179f)] private float m_PointingMaxYaw = 120f;
        [SerializeField, Min(0f)] private float m_PointingSmoothTime = 0.15f;
        [SerializeField] private bool m_PointingAutoAlign;
        [SerializeField, Min(0f)] private float m_PointingAlignDelay = 3f;
        [SerializeField, Min(0f)] private float m_PointingAlignSmoothTime = 3f;

        [Header("Pointing blend")]
        [SerializeField, Range(0.05f, 0.6f)] private float m_BlendInTime = 0.22f;
        [SerializeField, Range(0.05f, 0.6f)] private float m_BlendOutTime = 0.18f;
        [SerializeField, Range(0.5f, 1f)] private float m_ArmIKWeight = 0.96f;
        [SerializeField, Range(0.7f, 0.98f)] private float m_ArmExtension = 0.92f;
        [SerializeField, Range(20f, 100f)] private float m_ArmRaiseStartAngle = 65f;
        [SerializeField, Range(0f, 30f)] private float m_ArmFullyRaisedAngle = 10f;
        [SerializeField, Range(0f, 60f)] private float m_MaxUpPitch = 38f;
        [SerializeField, Range(0f, 60f)] private float m_MaxDownPitch = 32f;
        [SerializeField, Range(-180f, 180f)] private float m_HandRollDegrees;
        [SerializeField]
        [Tooltip("Right elbow hint in Character right/up/point-forward axes, relative to the shoulder.")]
        private Vector3 m_ElbowHintOffset = new(0.38f, -0.14f, 0.30f);

        [Header("Right index finger")]
        [SerializeField, Range(-1f, 1f)] private float m_IndexStraight = 0.08f;
        [SerializeField, Range(-1f, 1f)] private float m_OtherFingerCurl = -0.82f;
        [SerializeField, Range(-1f, 1f)] private float m_ThumbCurl = -0.30f;
        [SerializeField, Range(-1f, 1f)] private float m_ThumbSpread = 0.08f;

        private Character m_Character;
        private FranklinAnimationBridge m_MovementBridge;
        private MainCamera m_MainCamera;
        private ShotCamera m_ActivePointingShot;
        private ShotSystemThirdPerson m_PointingThirdPerson;
        private EnablerAngle180 m_PointingMaxYawSetting;
        private Animator m_Animator;
        private Transform m_UpperArm;
        private Transform m_LowerArm;
        private Transform m_RightHand;
        private Transform m_IndexProximal;
        private Transform m_ActiveCamera;
        private HumanPoseHandler m_HumanPoseHandler;
        private HumanPose m_HumanPose;
        private int[] m_ThumbMuscles;
        private int[] m_IndexMuscles;
        private int[] m_MiddleMuscles;
        private int[] m_RingMuscles;
        private int[] m_LittleMuscles;
        private Vector3 m_HandLocalPointAxis = Vector3.forward;
        private Vector3 m_PointDirection = Vector3.forward;
        private float m_ArmLength = 0.65f;
        private float m_PointWeight;
        private float m_NextCameraLookupTime;
        private int m_FacingLayerKey = -1;
        private bool m_VirtualPointHeld;
        private bool m_HasPointDirection;
        private bool m_IKRegistered;
        private bool m_OwnsRightArmBusyMask;
        private bool m_OwnsIdleSuppression;
        private bool m_IsPointingCameraActive;
        private bool m_HasCameraSnapshot;
        private bool m_HasWarnedMissingSetup;
        private bool m_HasWarnedCameraSetup;

        private CameraSnapshot m_CameraSnapshot;
        private CameraValues m_CurrentCameraValues;
        private CameraValues m_CameraBlendFrom;
        private CameraValues m_CameraBlendTo;
        private CameraBlendPhase m_CameraBlendPhase;
        private float m_CameraBlendElapsed;
        private float m_CameraBlendDuration;
        private MutableCameraProperties m_MutableCameraProperties;

        private enum CameraBlendPhase
        {
            Inactive,
            BlendIn,
            Active,
            BlendOut
        }

        private struct CameraValues
        {
            public float Shoulder;
            public float Lift;
            public float Radius;
            public float SensitivityX;
            public float SensitivityY;
            public float SmoothTime;
            public float MaxPitch;
            public float MaxYaw;
            public float AlignDelay;
            public float AlignSmoothTime;
        }

        private struct CameraSnapshot
        {
            public object Shoulder;
            public object Lift;
            public object Radius;
            public object SensitivityX;
            public object SensitivityY;
            public object SmoothTime;
            public CameraValues Values;
            public bool MaxYawEnabled;
            public bool AutoAlign;
        }

        /// <summary>
        /// Mutable constant GC2 properties reused throughout a camera blend. This avoids creating
        /// PropertyGetDecimal instances every frame on mobile.
        /// </summary>
        private sealed class MutableCameraProperties
        {
            private readonly MutableDecimal m_Shoulder;
            private readonly MutableDecimal m_Lift;
            private readonly MutableDecimal m_Radius;
            private readonly MutableDecimal m_SensitivityX;
            private readonly MutableDecimal m_SensitivityY;
            private readonly MutableDecimal m_SmoothTime;

            private readonly PropertyGetDecimal m_ShoulderProperty;
            private readonly PropertyGetDecimal m_LiftProperty;
            private readonly PropertyGetDecimal m_RadiusProperty;
            private readonly PropertyGetDecimal m_SensitivityXProperty;
            private readonly PropertyGetDecimal m_SensitivityYProperty;
            private readonly PropertyGetDecimal m_SmoothTimeProperty;

            public MutableCameraProperties(CameraValues values)
            {
                this.m_Shoulder = new MutableDecimal(values.Shoulder);
                this.m_Lift = new MutableDecimal(values.Lift);
                this.m_Radius = new MutableDecimal(values.Radius);
                this.m_SensitivityX = new MutableDecimal(values.SensitivityX);
                this.m_SensitivityY = new MutableDecimal(values.SensitivityY);
                this.m_SmoothTime = new MutableDecimal(values.SmoothTime);

                this.m_ShoulderProperty = new PropertyGetDecimal(this.m_Shoulder);
                this.m_LiftProperty = new PropertyGetDecimal(this.m_Lift);
                this.m_RadiusProperty = new PropertyGetDecimal(this.m_Radius);
                this.m_SensitivityXProperty = new PropertyGetDecimal(
                    this.m_SensitivityX
                );
                this.m_SensitivityYProperty = new PropertyGetDecimal(
                    this.m_SensitivityY
                );
                this.m_SmoothTimeProperty = new PropertyGetDecimal(this.m_SmoothTime);
            }

            public void Bind(ShotSystemThirdPerson thirdPerson)
            {
                CAMERA_SHOULDER_FIELD?.SetValue(thirdPerson, this.m_ShoulderProperty);
                CAMERA_LIFT_FIELD?.SetValue(thirdPerson, this.m_LiftProperty);
                CAMERA_RADIUS_FIELD?.SetValue(thirdPerson, this.m_RadiusProperty);
                CAMERA_SENSITIVITY_X_FIELD?.SetValue(
                    thirdPerson,
                    this.m_SensitivityXProperty
                );
                CAMERA_SENSITIVITY_Y_FIELD?.SetValue(
                    thirdPerson,
                    this.m_SensitivityYProperty
                );
                CAMERA_SMOOTH_TIME_FIELD?.SetValue(
                    thirdPerson,
                    this.m_SmoothTimeProperty
                );
            }

            public void Set(CameraValues values)
            {
                this.m_Shoulder.Value = values.Shoulder;
                this.m_Lift.Value = values.Lift;
                this.m_Radius.Value = values.Radius;
                this.m_SensitivityX.Value = values.SensitivityX;
                this.m_SensitivityY.Value = values.SensitivityY;
                this.m_SmoothTime.Value = values.SmoothTime;
            }
        }

        private sealed class MutableDecimal : GetDecimalDecimal
        {
            public MutableDecimal(float value) : base(value) { }

            public float Value
            {
                set => this.m_Value = value;
            }
        }

        public bool IsPointing => this.m_PointWeight > ACTIVE_WEIGHT_EPSILON;

        /// <summary>Called by the mobile HUD while its Point Direction button is held.</summary>
        public void SetVirtualPointInput(bool isHeld)
        {
            this.m_VirtualPointHeld = isHeld;
        }

        private void Awake()
        {
            this.ResolveCharacter();
        }

        private void OnValidate()
        {
            this.m_BlendInTime = Mathf.Max(0.05f, this.m_BlendInTime);
            this.m_BlendOutTime = Mathf.Max(0.05f, this.m_BlendOutTime);
            this.m_ArmIKWeight = Mathf.Clamp01(this.m_ArmIKWeight);
            this.m_ArmExtension = Mathf.Clamp(this.m_ArmExtension, 0.7f, 0.98f);
            this.m_ArmRaiseStartAngle = Mathf.Clamp(this.m_ArmRaiseStartAngle, 20f, 100f);
            this.m_ArmFullyRaisedAngle = Mathf.Clamp(
                this.m_ArmFullyRaisedAngle,
                0f,
                Mathf.Min(30f, this.m_ArmRaiseStartAngle - 1f)
            );
            this.m_MaxUpPitch = Mathf.Clamp(this.m_MaxUpPitch, 0f, 60f);
            this.m_MaxDownPitch = Mathf.Clamp(this.m_MaxDownPitch, 0f, 60f);
            this.m_PointingCameraBlendInTime = Mathf.Max(
                0f,
                this.m_PointingCameraBlendInTime
            );
            this.m_PointingCameraBlendOutTime = Mathf.Max(
                0f,
                this.m_PointingCameraBlendOutTime
            );
            this.m_PointingRadius = Mathf.Max(0.01f, this.m_PointingRadius);
            this.m_PointingSensitivityX = Mathf.Max(0f, this.m_PointingSensitivityX);
            this.m_PointingSensitivityY = Mathf.Max(0f, this.m_PointingSensitivityY);
            this.m_PointingMaxPitch = Mathf.Clamp(this.m_PointingMaxPitch, 1f, 179f);
            this.m_PointingMaxYaw = Mathf.Clamp(this.m_PointingMaxYaw, 0f, 179f);
            this.m_PointingSmoothTime = Mathf.Max(0f, this.m_PointingSmoothTime);
            this.m_PointingAlignDelay = Mathf.Max(0f, this.m_PointingAlignDelay);
            this.m_PointingAlignSmoothTime = Mathf.Max(
                0f,
                this.m_PointingAlignSmoothTime
            );

            if (Application.isPlaying && this.m_IsPointingCameraActive)
            {
                if (this.m_OverrideCameraShotWhilePointing)
                {
                    this.ApplyPointingCameraBooleans();
                    this.BeginCameraBlend(
                        this.CreatePointingCameraValues(),
                        this.m_PointingCameraBlendInTime,
                        CameraBlendPhase.BlendIn
                    );
                }
                else
                {
                    this.BeginPointingCameraBlendOut();
                }
            }
        }

        private void Update()
        {
            bool pointRequested = this.m_VirtualPointHeld ||
                                  Keyboard.current?.gKey.isPressed == true;
            Transform cameraTransform = null;
            bool canPoint = pointRequested && this.CanPoint() && this.EnsureReady() &&
                            this.TryAcquireRightArm() &&
                            this.TryResolveCamera(out cameraTransform);

            if (canPoint)
            {
                this.m_PointDirection = this.CalculatePointDirection(cameraTransform.forward);
                this.m_HasPointDirection = true;
                this.UpdateFacingDirection(this.m_PointDirection);
            }
            else
            {
                this.ReleaseFacingDirection();
            }

            // Turn the body first, then raise the arm as it approaches the camera heading. This
            // prevents a full IK arm from twisting through the torso after a 180-degree orbit.
            float targetWeight = canPoint ? this.GetFacingAlignmentWeight() : 0f;
            float blendDuration = canPoint ? this.m_BlendInTime : this.m_BlendOutTime;
            this.m_PointWeight = Mathf.MoveTowards(
                this.m_PointWeight,
                targetWeight,
                Time.deltaTime / Mathf.Max(0.01f, blendDuration)
            );

            this.SetIdleSuppressed(canPoint ||
                                   this.m_PointWeight > ACTIVE_WEIGHT_EPSILON);
            this.UpdatePointingCamera(canPoint);

            if (!canPoint && this.m_PointWeight <= ACTIVE_WEIGHT_EPSILON)
            {
                this.m_PointWeight = 0f;
                this.m_HasPointDirection = false;
                this.ReleaseRightArm();
                this.UnregisterAnimatorIK();
            }
        }

        private void OnDisable()
        {
            this.m_VirtualPointHeld = false;
            this.m_PointWeight = 0f;
            this.m_HasPointDirection = false;
            this.ReleaseFacingDirection();
            this.UnregisterAnimatorIK();
            this.ReleaseRightArm();
            this.SetIdleSuppressed(false);
            this.RestorePointingCamera();
        }

        private void OnDestroy()
        {
            this.UnregisterAnimatorIK();
            this.ReleaseFacingDirection();
            this.ReleaseRightArm();
            this.SetIdleSuppressed(false);
            this.RestorePointingCamera();
            this.DisposeHumanPoseHandler();
        }

        private bool ResolveCharacter()
        {
            if (this.m_Player == null)
            {
                this.m_Player = this.GetComponentInParent<Character>();
            }

            this.m_Character = this.m_Player;
            if (this.m_MovementBridge == null)
            {
                this.m_MovementBridge = this.GetComponent<FranklinAnimationBridge>() ??
                                        this.GetComponentInParent<FranklinAnimationBridge>() ??
                                        this.m_Player?.GetComponentInChildren<
                                            FranklinAnimationBridge>(true);
            }
            return this.m_Character != null;
        }

        private void SetIdleSuppressed(bool suppressed)
        {
            if (suppressed == this.m_OwnsIdleSuppression) return;
            if (!this.ResolveCharacter()) return;

            if (suppressed)
            {
                if (this.m_MovementBridge == null) return;
                this.m_MovementBridge.AcquireIdleVariationsSuppression(this);
                this.m_OwnsIdleSuppression = true;
            }
            else
            {
                this.m_MovementBridge?.ReleaseIdleVariationsSuppression(this);
                this.m_OwnsIdleSuppression = false;
            }
        }

        private void UpdatePointingCamera(bool shouldBeActive)
        {
            shouldBeActive &= this.m_OverrideCameraShotWhilePointing;
            if (shouldBeActive)
            {
                ShotCamera shot = this.ResolvePointingShot();
                if (!this.m_IsPointingCameraActive ||
                    shot != this.m_ActivePointingShot)
                {
                    this.RestorePointingCamera();
                    if (!this.ActivatePointingCamera(shot)) return;
                }
                else if (this.m_CameraBlendPhase == CameraBlendPhase.BlendOut)
                {
                    // A quick repress reverses from the exact current framing instead of
                    // snapping back to either end of the transition.
                    this.ApplyPointingCameraBooleans();
                    this.BeginCameraBlend(
                        this.CreatePointingCameraValues(),
                        this.m_PointingCameraBlendInTime,
                        CameraBlendPhase.BlendIn
                    );
                }
            }
            else if (this.m_IsPointingCameraActive &&
                     this.m_CameraBlendPhase != CameraBlendPhase.BlendOut)
            {
                this.BeginPointingCameraBlendOut();
            }

            this.UpdateCameraBlend();
        }

        private ShotCamera ResolvePointingShot()
        {
            if (this.m_PointingShot != null) return this.m_PointingShot;

            if (this.m_MainCamera == null)
                this.m_MainCamera = ShortcutMainCamera.Get<MainCamera>();
            return this.m_MainCamera?.Transition.CurrentShotCamera;
        }

        private bool ActivatePointingCamera(ShotCamera shot)
        {
            if (!TryGetThirdPerson(shot, out ShotSystemThirdPerson thirdPerson))
            {
                this.WarnCameraSetup();
                return false;
            }

            this.m_ActivePointingShot = shot;
            this.m_PointingThirdPerson = thirdPerson;
            this.CapturePointingCamera();
            this.m_IsPointingCameraActive = true;

            this.m_MutableCameraProperties ??= new MutableCameraProperties(
                this.m_CurrentCameraValues
            );
            this.m_MutableCameraProperties.Set(this.m_CurrentCameraValues);
            this.m_MutableCameraProperties.Bind(this.m_PointingThirdPerson);
            this.ApplyPointingCameraBooleans();
            this.BeginCameraBlend(
                this.CreatePointingCameraValues(),
                this.m_PointingCameraBlendInTime,
                CameraBlendPhase.BlendIn
            );
            return true;
        }

        private void CapturePointingCamera()
        {
            this.m_PointingMaxYawSetting = CAMERA_MAX_YAW_FIELD?.GetValue(
                this.m_PointingThirdPerson
            ) as EnablerAngle180;

            this.m_CurrentCameraValues = this.ReadCameraValues(
                this.m_PointingMaxYawSetting
            );

            this.m_CameraSnapshot = new CameraSnapshot
            {
                Shoulder = CAMERA_SHOULDER_FIELD?.GetValue(this.m_PointingThirdPerson),
                Lift = CAMERA_LIFT_FIELD?.GetValue(this.m_PointingThirdPerson),
                Radius = CAMERA_RADIUS_FIELD?.GetValue(this.m_PointingThirdPerson),
                SensitivityX = CAMERA_SENSITIVITY_X_FIELD?.GetValue(
                    this.m_PointingThirdPerson
                ),
                SensitivityY = CAMERA_SENSITIVITY_Y_FIELD?.GetValue(
                    this.m_PointingThirdPerson
                ),
                SmoothTime = CAMERA_SMOOTH_TIME_FIELD?.GetValue(
                    this.m_PointingThirdPerson
                ),
                Values = this.m_CurrentCameraValues,
                MaxYawEnabled = this.m_PointingMaxYawSetting?.IsEnabled ?? false,
                AutoAlign = this.m_PointingThirdPerson.Alignment.AutoAlign
            };
            this.m_HasCameraSnapshot = true;
        }

        private CameraValues ReadCameraValues(EnablerAngle180 maxYaw)
        {
            return new CameraValues
            {
                Shoulder = this.ReadCameraDecimal(CAMERA_SHOULDER_FIELD, 0f),
                Lift = this.ReadCameraDecimal(CAMERA_LIFT_FIELD, 0f),
                Radius = this.ReadCameraDecimal(CAMERA_RADIUS_FIELD, 0.01f),
                SensitivityX = this.ReadCameraDecimal(
                    CAMERA_SENSITIVITY_X_FIELD,
                    1f
                ),
                SensitivityY = this.ReadCameraDecimal(
                    CAMERA_SENSITIVITY_Y_FIELD,
                    1f
                ),
                SmoothTime = this.ReadCameraDecimal(CAMERA_SMOOTH_TIME_FIELD, 0.15f),
                MaxPitch = this.m_PointingThirdPerson.MaxPitch,
                MaxYaw = maxYaw?.Value ?? 0f,
                AlignDelay = this.m_PointingThirdPerson.Alignment.Delay,
                AlignSmoothTime = this.m_PointingThirdPerson.Alignment.SmoothTime
            };
        }

        private float ReadCameraDecimal(FieldInfo field, float fallback)
        {
            if (field?.GetValue(this.m_PointingThirdPerson) is not
                PropertyGetDecimal property)
            {
                return fallback;
            }

            if (this.m_ActivePointingShot?.ShotType is ShotTypeThirdPerson shotType)
            {
                return (float) property.Get(shotType.Args);
            }

            return (float) property.Get(this.m_ActivePointingShot);
        }

        private CameraValues CreatePointingCameraValues()
        {
            CameraValues values = this.m_CameraSnapshot.Values;
            values.Shoulder = this.m_PointingShoulder;
            values.Lift = this.m_PointingLift;
            values.Radius = this.m_PointingRadius;
            values.SmoothTime = this.m_PointingSmoothTime;
            values.MaxPitch = this.m_PointingMaxPitch;
            values.MaxYaw = this.m_PointingMaxYaw;
            values.AlignDelay = this.m_PointingAlignDelay;
            values.AlignSmoothTime = this.m_PointingAlignSmoothTime;

            if (this.m_OverridePointingSensitivity)
            {
                values.SensitivityX = this.m_PointingSensitivityX;
                values.SensitivityY = this.m_PointingSensitivityY;
            }

            return values;
        }

        private void ApplyPointingCameraBooleans()
        {
            if (this.m_PointingThirdPerson == null) return;

            this.m_PointingThirdPerson.Alignment.AutoAlign = this.m_PointingAutoAlign;
            if (this.m_PointingMaxYawSetting != null)
            {
                this.m_PointingMaxYawSetting.IsEnabled = this.m_EnablePointingMaxYaw;
            }
        }

        private void BeginPointingCameraBlendOut()
        {
            if (!this.m_IsPointingCameraActive || !this.m_HasCameraSnapshot) return;
            this.BeginCameraBlend(
                this.m_CameraSnapshot.Values,
                this.m_PointingCameraBlendOutTime,
                CameraBlendPhase.BlendOut
            );
        }

        private void BeginCameraBlend(
            CameraValues target,
            float duration,
            CameraBlendPhase phase)
        {
            this.m_CameraBlendFrom = this.m_CurrentCameraValues;
            this.m_CameraBlendTo = target;
            this.m_CameraBlendElapsed = 0f;
            this.m_CameraBlendDuration = Mathf.Max(0f, duration);
            this.m_CameraBlendPhase = phase;
        }

        private void UpdateCameraBlend()
        {
            if (!this.m_IsPointingCameraActive ||
                this.m_CameraBlendPhase is CameraBlendPhase.Inactive or
                    CameraBlendPhase.Active)
            {
                return;
            }

            this.m_CameraBlendElapsed += Time.deltaTime;
            float progress = this.m_CameraBlendDuration > ACTIVE_WEIGHT_EPSILON
                ? Mathf.Clamp01(this.m_CameraBlendElapsed / this.m_CameraBlendDuration)
                : 1f;
            float eased = Easing.QuadInOut(0f, 1f, progress);
            this.m_CurrentCameraValues = LerpCameraValues(
                this.m_CameraBlendFrom,
                this.m_CameraBlendTo,
                eased
            );
            this.ApplyCurrentCameraValues();

            if (progress < 1f) return;

            if (this.m_CameraBlendPhase == CameraBlendPhase.BlendOut)
            {
                this.RestorePointingCamera();
            }
            else
            {
                this.m_CameraBlendPhase = CameraBlendPhase.Active;
            }
        }

        private void ApplyCurrentCameraValues()
        {
            if (this.m_PointingThirdPerson == null ||
                this.m_MutableCameraProperties == null)
            {
                return;
            }

            this.m_MutableCameraProperties.Set(this.m_CurrentCameraValues);
            this.m_PointingThirdPerson.MaxPitch = this.m_CurrentCameraValues.MaxPitch;
            this.m_PointingThirdPerson.Alignment.Delay =
                this.m_CurrentCameraValues.AlignDelay;
            this.m_PointingThirdPerson.Alignment.SmoothTime =
                this.m_CurrentCameraValues.AlignSmoothTime;

            if (this.m_PointingMaxYawSetting != null)
            {
                this.m_PointingMaxYawSetting.Value = this.m_CurrentCameraValues.MaxYaw;
            }
        }

        private static CameraValues LerpCameraValues(
            CameraValues from,
            CameraValues to,
            float t)
        {
            return new CameraValues
            {
                Shoulder = Mathf.LerpUnclamped(from.Shoulder, to.Shoulder, t),
                Lift = Mathf.LerpUnclamped(from.Lift, to.Lift, t),
                Radius = Mathf.LerpUnclamped(from.Radius, to.Radius, t),
                SensitivityX = Mathf.LerpUnclamped(
                    from.SensitivityX,
                    to.SensitivityX,
                    t
                ),
                SensitivityY = Mathf.LerpUnclamped(
                    from.SensitivityY,
                    to.SensitivityY,
                    t
                ),
                SmoothTime = Mathf.LerpUnclamped(from.SmoothTime, to.SmoothTime, t),
                MaxPitch = Mathf.LerpUnclamped(from.MaxPitch, to.MaxPitch, t),
                MaxYaw = Mathf.LerpUnclamped(from.MaxYaw, to.MaxYaw, t),
                AlignDelay = Mathf.LerpUnclamped(from.AlignDelay, to.AlignDelay, t),
                AlignSmoothTime = Mathf.LerpUnclamped(
                    from.AlignSmoothTime,
                    to.AlignSmoothTime,
                    t
                )
            };
        }

        private void RestorePointingCamera()
        {
            if (this.m_HasCameraSnapshot && this.m_PointingThirdPerson != null)
            {
                CAMERA_SHOULDER_FIELD?.SetValue(
                    this.m_PointingThirdPerson,
                    this.m_CameraSnapshot.Shoulder
                );
                CAMERA_LIFT_FIELD?.SetValue(
                    this.m_PointingThirdPerson,
                    this.m_CameraSnapshot.Lift
                );
                CAMERA_RADIUS_FIELD?.SetValue(
                    this.m_PointingThirdPerson,
                    this.m_CameraSnapshot.Radius
                );
                CAMERA_SENSITIVITY_X_FIELD?.SetValue(
                    this.m_PointingThirdPerson,
                    this.m_CameraSnapshot.SensitivityX
                );
                CAMERA_SENSITIVITY_Y_FIELD?.SetValue(
                    this.m_PointingThirdPerson,
                    this.m_CameraSnapshot.SensitivityY
                );
                CAMERA_SMOOTH_TIME_FIELD?.SetValue(
                    this.m_PointingThirdPerson,
                    this.m_CameraSnapshot.SmoothTime
                );

                this.m_PointingThirdPerson.MaxPitch =
                    this.m_CameraSnapshot.Values.MaxPitch;
                this.m_PointingThirdPerson.Alignment.AutoAlign =
                    this.m_CameraSnapshot.AutoAlign;
                this.m_PointingThirdPerson.Alignment.Delay =
                    this.m_CameraSnapshot.Values.AlignDelay;
                this.m_PointingThirdPerson.Alignment.SmoothTime =
                    this.m_CameraSnapshot.Values.AlignSmoothTime;

                if (this.m_PointingMaxYawSetting != null)
                {
                    this.m_PointingMaxYawSetting.IsEnabled =
                        this.m_CameraSnapshot.MaxYawEnabled;
                    this.m_PointingMaxYawSetting.Value =
                        this.m_CameraSnapshot.Values.MaxYaw;
                }
            }

            this.m_IsPointingCameraActive = false;
            this.m_HasCameraSnapshot = false;
            this.m_CameraBlendPhase = CameraBlendPhase.Inactive;
            this.m_CameraBlendElapsed = 0f;
            this.m_CameraBlendDuration = 0f;
            this.m_ActivePointingShot = null;
            this.m_PointingThirdPerson = null;
            this.m_PointingMaxYawSetting = null;
        }

        private static bool TryGetThirdPerson(
            ShotCamera shot,
            out ShotSystemThirdPerson thirdPerson)
        {
            thirdPerson = null;
            if (shot?.ShotType is not ShotTypeThirdPerson shotType) return false;

            thirdPerson = shotType.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            return thirdPerson != null;
        }

        private bool CanPoint()
        {
            if (!this.ResolveCharacter() || !this.m_Character.isActiveAndEnabled) return false;
            if (this.m_MovementBridge != null &&
                this.m_MovementBridge.IsExternalAnimationLocked) return false;
            if (this.m_Character.IsDead || this.m_Character.Player?.IsControllable != true)
            {
                return false;
            }

            return this.m_Character.Driver?.IsGrounded == true &&
                   this.m_Character.Motion?.IsJumping != true;
        }

        private bool EnsureReady()
        {
            if (!this.ResolveCharacter()) return false;

            Animator animator = this.m_Character.Animim?.Animator;
            if (animator == null)
            {
                animator = this.m_Character.GetComponentInChildren<Animator>(true);
            }

            if (animator == null || !animator.isHuman || animator.avatar == null ||
                !animator.avatar.isHuman)
            {
                this.WarnMissingSetup();
                return false;
            }

            if (this.m_Animator != animator || this.m_RightHand == null ||
                this.m_HumanPoseHandler == null)
            {
                this.UnregisterAnimatorIK();
                this.DisposeHumanPoseHandler();
                this.m_Animator = animator;
                this.ResolveHumanoidBones();
            }

            if (this.m_UpperArm == null || this.m_LowerArm == null ||
                this.m_RightHand == null || this.m_HumanPoseHandler == null)
            {
                this.WarnMissingSetup();
                return false;
            }

            this.RegisterAnimatorIK();
            return true;
        }

        private void ResolveHumanoidBones()
        {
            this.m_UpperArm = this.m_Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            this.m_LowerArm = this.m_Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            this.m_RightHand = this.m_Animator.GetBoneTransform(HumanBodyBones.RightHand);
            this.m_IndexProximal = this.m_Animator.GetBoneTransform(
                HumanBodyBones.RightIndexProximal
            );

            if (this.m_UpperArm == null || this.m_LowerArm == null ||
                this.m_RightHand == null)
            {
                return;
            }

            this.m_ArmLength = Vector3.Distance(
                this.m_UpperArm.position,
                this.m_LowerArm.position
            ) + Vector3.Distance(this.m_LowerArm.position, this.m_RightHand.position);

            if (this.m_IndexProximal != null)
            {
                Vector3 fingerAxis = this.m_IndexProximal.position - this.m_RightHand.position;
                if (fingerAxis.sqrMagnitude > ACTIVE_WEIGHT_EPSILON)
                {
                    this.m_HandLocalPointAxis = this.m_RightHand.InverseTransformDirection(
                        fingerAxis.normalized
                    );
                }
            }

            this.m_HumanPoseHandler = new HumanPoseHandler(
                this.m_Animator.avatar,
                this.m_Animator.transform
            );
            this.m_HumanPose = new HumanPose
            {
                muscles = new float[HumanTrait.MuscleCount]
            };
            this.BuildFingerMuscleLookup();
        }

        private void RegisterAnimatorIK()
        {
            if (this.m_IKRegistered || this.m_Character?.Animim == null) return;
            this.m_Character.Animim.EventOnAnimatorIK -= this.OnAnimatorIK;
            this.m_Character.Animim.EventOnAnimatorIK += this.OnAnimatorIK;
            this.m_IKRegistered = true;
        }

        private void UnregisterAnimatorIK()
        {
            if (this.m_Character?.Animim != null)
            {
                this.m_Character.Animim.EventOnAnimatorIK -= this.OnAnimatorIK;
            }
            this.m_IKRegistered = false;
        }

        private bool TryAcquireRightArm()
        {
            if (this.m_OwnsRightArmBusyMask) return true;
            if (this.m_Character?.Busy == null) return true;
            if (this.m_Character.Busy.IsArmRightBusy) return false;

            this.m_Character.Busy.MakeArmRightBusy();
            this.m_OwnsRightArmBusyMask = true;
            return true;
        }

        private void ReleaseRightArm()
        {
            if (!this.m_OwnsRightArmBusyMask) return;
            if (this.m_Character?.Busy != null)
            {
                this.m_Character.Busy.RemoveArmRightBusy();
            }
            this.m_OwnsRightArmBusyMask = false;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (this.m_Animator == null || this.m_UpperArm == null ||
                this.m_RightHand == null)
            {
                return;
            }

            float ikWeight = Mathf.SmoothStep(0f, 1f, this.m_PointWeight) *
                             this.m_ArmIKWeight;
            this.m_Animator.SetIKPositionWeight(AvatarIKGoal.RightHand, ikWeight);
            this.m_Animator.SetIKRotationWeight(AvatarIKGoal.RightHand, ikWeight);
            this.m_Animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, ikWeight);
            if (ikWeight <= ACTIVE_WEIGHT_EPSILON || !this.m_HasPointDirection) return;

            Vector3 shoulder = this.m_UpperArm.position;
            Vector3 handTarget = shoulder +
                                 this.m_PointDirection *
                                 (this.m_ArmLength * this.m_ArmExtension);
            Transform characterTransform = this.m_Character.transform;
            Vector3 elbowHint = shoulder +
                                characterTransform.right * this.m_ElbowHintOffset.x +
                                characterTransform.up * this.m_ElbowHintOffset.y +
                                this.m_PointDirection * this.m_ElbowHintOffset.z;

            Vector3 currentFingerAxis = this.m_RightHand.TransformDirection(
                this.m_HandLocalPointAxis
            );
            Quaternion handRotation = Quaternion.FromToRotation(
                currentFingerAxis,
                this.m_PointDirection
            ) * this.m_RightHand.rotation;
            handRotation = Quaternion.AngleAxis(
                this.m_HandRollDegrees,
                this.m_PointDirection
            ) * handRotation;

            this.ApplyPointingFingerPose(ikWeight);
            this.m_Animator.SetIKPosition(AvatarIKGoal.RightHand, handTarget);
            this.m_Animator.SetIKRotation(AvatarIKGoal.RightHand, handRotation);
            this.m_Animator.SetIKHintPosition(AvatarIKHint.RightElbow, elbowHint);
        }

        private Vector3 CalculatePointDirection(Vector3 cameraForward)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (flatForward.sqrMagnitude <= ACTIVE_WEIGHT_EPSILON)
            {
                flatForward = this.m_Character.transform.forward;
            }
            flatForward.Normalize();

            float horizontal = Mathf.Sqrt(
                cameraForward.x * cameraForward.x + cameraForward.z * cameraForward.z
            );
            float pitch = Mathf.Atan2(cameraForward.y, horizontal) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -this.m_MaxDownPitch, this.m_MaxUpPitch);
            float pitchRadians = pitch * Mathf.Deg2Rad;

            return (
                flatForward * Mathf.Cos(pitchRadians) +
                Vector3.up * Mathf.Sin(pitchRadians)
            ).normalized;
        }

        private void UpdateFacingDirection(Vector3 pointDirection)
        {
            if (this.m_Character?.Facing == null) return;
            Vector3 flatDirection = Vector3.ProjectOnPlane(pointDirection, Vector3.up);
            if (flatDirection.sqrMagnitude <= ACTIVE_WEIGHT_EPSILON) return;

            this.m_FacingLayerKey = this.m_Character.Facing.SetLayerDirection(
                this.m_FacingLayerKey,
                flatDirection.normalized,
                false
            );
        }

        private float GetFacingAlignmentWeight()
        {
            Vector3 flatPoint = Vector3.ProjectOnPlane(this.m_PointDirection, Vector3.up);
            Vector3 flatFacing = Vector3.ProjectOnPlane(
                this.m_Character.transform.forward,
                Vector3.up
            );
            if (flatPoint.sqrMagnitude <= ACTIVE_WEIGHT_EPSILON ||
                flatFacing.sqrMagnitude <= ACTIVE_WEIGHT_EPSILON)
            {
                return 0f;
            }

            float angle = Vector3.Angle(flatFacing, flatPoint);
            return Mathf.InverseLerp(
                this.m_ArmRaiseStartAngle,
                this.m_ArmFullyRaisedAngle,
                angle
            );
        }

        private void ReleaseFacingDirection()
        {
            if (this.m_FacingLayerKey < 0) return;
            this.m_Character?.Facing?.DeleteLayer(this.m_FacingLayerKey);
            this.m_FacingLayerKey = -1;
        }

        private bool TryResolveCamera(out Transform cameraTransform)
        {
            if (this.m_CameraOverride != null)
            {
                cameraTransform = this.m_CameraOverride;
                return true;
            }

            if (this.m_ActiveCamera == null &&
                Time.unscaledTime >= this.m_NextCameraLookupTime)
            {
                this.m_ActiveCamera = ShortcutMainCamera.Transform;
                if (this.m_ActiveCamera == null)
                {
                    this.m_ActiveCamera = ShortcutMainShot.Transform;
                }
                if (this.m_ActiveCamera == null && Camera.main != null)
                {
                    this.m_ActiveCamera = Camera.main.transform;
                }
                if (this.m_ActiveCamera == null)
                {
                    this.m_NextCameraLookupTime =
                        Time.unscaledTime + CAMERA_LOOKUP_RETRY_SECONDS;
                }
            }

            cameraTransform = this.m_ActiveCamera;
            return cameraTransform != null;
        }

        private void BuildFingerMuscleLookup()
        {
            this.m_ThumbMuscles = GetFingerMuscles("Thumb");
            this.m_IndexMuscles = GetFingerMuscles("Index");
            this.m_MiddleMuscles = GetFingerMuscles("Middle");
            this.m_RingMuscles = GetFingerMuscles("Ring");
            this.m_LittleMuscles = GetFingerMuscles("Little");
        }

        private void ApplyPointingFingerPose(float weight)
        {
            if (this.m_HumanPoseHandler == null) return;

            this.m_HumanPoseHandler.GetHumanPose(ref this.m_HumanPose);
            float[] muscles = this.m_HumanPose.muscles;
            if (muscles == null || muscles.Length == 0) return;

            ApplyFingerPose(muscles, this.m_IndexMuscles, this.m_IndexStraight, weight);
            ApplyFingerPose(muscles, this.m_MiddleMuscles, this.m_OtherFingerCurl, weight);
            ApplyFingerPose(muscles, this.m_RingMuscles, this.m_OtherFingerCurl, weight);
            ApplyFingerPose(muscles, this.m_LittleMuscles, this.m_OtherFingerCurl, weight);
            ApplyThumbPose(
                muscles,
                this.m_ThumbMuscles,
                this.m_ThumbCurl,
                this.m_ThumbSpread,
                weight
            );
            this.m_HumanPoseHandler.SetHumanPose(ref this.m_HumanPose);
        }

        private static void ApplyFingerPose(
            float[] muscles,
            int[] indices,
            float curl,
            float weight)
        {
            if (indices == null || indices.Length < 4) return;
            BlendMuscle(muscles, indices[0], curl * 0.82f, weight);
            BlendMuscle(muscles, indices[1], 0f, weight);
            BlendMuscle(muscles, indices[2], curl, weight);
            BlendMuscle(muscles, indices[3], curl * 0.88f, weight);
        }

        private static void ApplyThumbPose(
            float[] muscles,
            int[] indices,
            float curl,
            float spread,
            float weight)
        {
            if (indices == null || indices.Length < 4) return;
            BlendMuscle(muscles, indices[0], curl * 0.72f, weight);
            BlendMuscle(muscles, indices[1], spread, weight);
            BlendMuscle(muscles, indices[2], curl, weight);
            BlendMuscle(muscles, indices[3], curl * 0.80f, weight);
        }

        private static void BlendMuscle(
            float[] muscles,
            int index,
            float target,
            float weight)
        {
            if (index < 0 || index >= muscles.Length) return;
            muscles[index] = Mathf.Lerp(
                muscles[index],
                Mathf.Clamp(target, -1f, 1f),
                Mathf.Clamp01(weight)
            );
        }

        private static int[] GetFingerMuscles(string finger)
        {
            return new[]
            {
                FindMuscleIndex($"RightHand.{finger}.1 Stretched"),
                FindMuscleIndex($"RightHand.{finger}.Spread"),
                FindMuscleIndex($"RightHand.{finger}.2 Stretched"),
                FindMuscleIndex($"RightHand.{finger}.3 Stretched")
            };
        }

        private static int FindMuscleIndex(string muscleName)
        {
            string[] names = HumanTrait.MuscleName;
            for (int i = 0; i < names.Length; ++i)
            {
                if (names[i] == muscleName) return i;
            }
            return -1;
        }

        private void DisposeHumanPoseHandler()
        {
            this.m_HumanPoseHandler?.Dispose();
            this.m_HumanPoseHandler = null;
            this.m_HumanPose = new HumanPose();
        }

        private void WarnMissingSetup()
        {
            if (this.m_HasWarnedMissingSetup) return;
            this.m_HasWarnedMissingSetup = true;
            Debug.LogWarning(
                "Camera pointing requires the Player Humanoid Animator and right arm bones.",
                this
            );
        }

        private void WarnCameraSetup()
        {
            if (this.m_HasWarnedCameraSetup) return;
            this.m_HasWarnedCameraSetup = true;
            Debug.LogWarning(
                "Pointing camera overrides require the active GC2 Camera Shot to use " +
                "Third Person. Character pointing remains available without the override.",
                this
            );
        }
    }
}
