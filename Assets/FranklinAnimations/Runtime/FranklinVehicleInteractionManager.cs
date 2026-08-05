using System.Reflection;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Bridges the touch HUD to RapidTemplate vehicles. It deliberately forwards
    /// through the selected RVR/GC2 interaction so the vehicle's existing conditions,
    /// navigation and enter sequence remain the single source of truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinVehicleInteractionManager : MonoBehaviour
    {
        private const float ENTRY_BEGIN_TIMEOUT = 5f;

        private static readonly FieldInfo THIRD_PERSON_SHOULDER_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Shoulder",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_LIFT_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Lift",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_RADIUS_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Radius",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_PIVOT_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Pivot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_MAX_YAW_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_MaxYaw",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_SMOOTH_TIME_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_SmoothTime",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        [SerializeField]
        [Tooltip("The owning GC2 Player Character. It is resolved from the parent when omitted.")]
        private Character m_Player;
        [SerializeField]
        [Tooltip("The Player locomotion bridge to suspend while a vehicle owns the body animation.")]
        private FranklinAnimationBridge m_MovementBridge;

        [Header("Vehicle entry animation")]
        [SerializeField]
        [Tooltip("Prevents the vehicle's seated Driving State from appearing before its door/entry animation finishes.")]
        private bool m_DelayDrivingIdleUntilEntryFinishes = true;

        [Header("Vehicle camera")]
        [SerializeField]
        [Tooltip("Switch the Main Camera to the original RapidTemplate vehicle shot while driving.")]
        private bool m_UseVehicleCamera = true;
        [SerializeField]
        [Tooltip("The original Camera Shot Vehicle prefab. A temporary instance is created at runtime.")]
        private ShotCamera m_VehicleShotPrefab;
        [SerializeField, Min(0f)]
        [Tooltip("Seconds used to blend from the player shot into the vehicle shot.")]
        private float m_VehicleCameraEnterBlend = 2.5f;
        [SerializeField, Min(0f)]
        [Tooltip("Seconds used to blend back to the player shot after exiting the vehicle.")]
        private float m_VehicleCameraExitBlend = 2.5f;
        [SerializeField] private Easing.Type m_VehicleCameraEasing = Easing.Type.Linear;

        [Header("Vehicle camera - Pivot")]
        [SerializeField]
        [Tooltip("When enabled, the vehicle camera orbits around the CarEntry/BikeEntry object currently being driven.")]
        private bool m_PivotCameraToActiveVehicle = true;

        [Header("Vehicle camera - Third Person")]
        [SerializeField] private float m_VehicleShoulder = 0f;
        [SerializeField] private float m_VehicleLift = 1f;
        [SerializeField, Min(0.01f)] private float m_VehicleRadius = 5f;
        [SerializeField]
        [Tooltip("Keeps RapidTemplate's original Global Variable - Vehicles/Camera-Sensitivity value.")]
        private bool m_UseGlobalVehicleSensitivity = true;
        [SerializeField, Min(0f)] private float m_VehicleSensitivityX = 0.5f;
        [SerializeField, Min(0f)] private float m_VehicleSensitivityY = 0.5f;
        [SerializeField, Range(1f, 179f)] private float m_VehicleMaxPitch = 100f;
        [SerializeField] private bool m_EnableVehicleMaxYaw;
        [SerializeField, Range(0f, 179f)] private float m_VehicleMaxYaw = 100f;
        [SerializeField, Min(0f)] private float m_VehicleSmoothTime = 0.15f;
        [SerializeField] private bool m_VehicleAutoAlign;
        [SerializeField, Min(0f)] private float m_VehicleAlignDelay = 3f;
        [SerializeField, Min(0f)] private float m_VehicleAlignSmoothTime = 3f;

        private bool m_IsVehicleAnimationLocked;
        private bool m_HasEnteredVehicle;
        private float m_EntryRequestedAt;
        private bool m_IsDrivingStateSuppressed;
        private StateData m_DelayedDrivingState;
        private int m_DelayedDrivingStateLayer;
        private float m_DelayedDrivingStateTransitionIn;
        private float m_DelayedDrivingStateTransitionOut;
        private MainCamera m_MainCamera;
        private ShotCamera m_RuntimeVehicleShot;
        private ShotCamera m_PreVehicleShot;
        private PropertyGetGameObject m_DefaultVehiclePivot;
        private GameObject m_ActiveVehiclePivot;
        private bool m_IsVehicleCameraActive;

        private void Awake()
        {
            this.ResolvePlayer();
        }

        private void OnDisable()
        {
            if (this.m_IsVehicleAnimationLocked)
            {
                this.m_IsVehicleAnimationLocked = false;
                this.m_MovementBridge?.SetExternalAnimationLock(false);
            }

            this.RestorePlayerCamera();
        }

        private void OnDestroy()
        {
            if (this.m_RuntimeVehicleShot != null)
            {
                Destroy(this.m_RuntimeVehicleShot.gameObject);
            }
        }

        private void Update()
        {
            if (!this.ResolvePlayer()) return;
            this.UpdateVehicleAnimationLock();

            // Keep the physical desktop shortcut for Editor testing. The old on-screen "E"
            // prompt is disabled in the RVR car prefab and replaced by FranklinMobileHud.
            if (this.m_IsVehicleAnimationLocked) return;
            if (Keyboard.current?.eKey.wasPressedThisFrame != true) return;
            if (this.m_Player.Player?.IsControllable != true) return;

            this.RequestVehicleInteraction();
        }

        /// <summary>
        /// True only while RVR has selected the driver's-door interaction spot on an available car.
        /// The mobile HUD uses this to hide its Enter button everywhere else.
        /// </summary>
        public bool CanRequestVehicleInteraction
        {
            get
            {
                if (!this.ResolvePlayer() || this.m_IsVehicleAnimationLocked ||
                    this.m_Player.Player?.IsControllable != true)
                {
                    return false;
                }

                return this.TryGetSelectedDriverDoor(out _, out _);
            }
        }

        /// <summary>
        /// Starts the currently selected RVR driver's-door interaction. Mobile UI calls this
        /// entry point so RVR conditions, navigation and the door animation remain authoritative.
        /// </summary>
        public bool RequestVehicleInteraction()
        {
            if (!this.ResolvePlayer() || this.m_IsVehicleAnimationLocked ||
                this.m_Player.Player?.IsControllable != true)
            {
                return false;
            }

            return this.TryStartVehicleInteraction();
        }

        private bool TryStartVehicleInteraction()
        {
            if (!this.TryGetSelectedDriverDoor(
                    out _,
                    out Component vehicleEntry))
            {
                return false;
            }

            this.LockMovementAnimation();
            this.m_ActiveVehiclePivot = vehicleEntry.gameObject;
            this.DelayVehicleDrivingIdle(vehicleEntry);

            // Do not call CarEntry directly: the RVR/GC2 trigger first
            // validates its conditions and moves the character to the entry point.
            if (!this.m_Player.Interaction.Interact())
            {
                this.CancelVehicleInteractionRequest();
                return false;
            }

            return true;
        }

        private bool TryGetSelectedDriverDoor(
            out IInteractive target,
            out Component vehicleEntry)
        {
            target = this.m_Player.Interaction.Target;
            vehicleEntry = target?.Instance != null
                ? FindVehicleEntry(target.Instance)
                : null;
            Hotspot hotspot = target?.Instance != null
                ? target.Instance.GetComponent<Hotspot>()
                : null;
            if (hotspot == null || !hotspot.IsActive ||
                vehicleEntry is not CarEntry carEntry ||
                carEntry.IsTransitioning ||
                carEntry.SeatedCharacter != null)
            {
                return false;
            }

            SimcadeCarDriver driver = carEntry.GetComponent<SimcadeCarDriver>();
            if (driver != null && driver.IsVehicleEnabled) return false;

            for (Transform current = target.Instance.transform;
                 current != null && current != vehicleEntry.transform;
                 current = current.parent)
            {
                if (current.gameObject.name == "Triggers_Enter/Exit") return true;
            }

            return false;
        }

        private void CancelVehicleInteractionRequest()
        {
            this.m_IsVehicleAnimationLocked = false;
            this.m_MovementBridge?.SetExternalAnimationLock(false);
            this.ClearDelayedDrivingState();
            this.m_ActiveVehiclePivot = null;
        }

        private void LateUpdate()
        {
            if (!this.m_IsDrivingStateSuppressed || this.m_HasEnteredVehicle) return;
            if (this.m_Player?.Player?.IsControllable != true) return;

            // CarEntry/BikeEntry sets its driving state before playing the entry gesture.
            // LateUpdate removes that state before rendering, leaving the door/entry gesture in
            // charge until the character has actually taken the seat.
            this.m_Player.States?.Stop(this.m_DelayedDrivingStateLayer, 0f, 0f);
        }

        private bool ResolvePlayer()
        {
            if (this.m_Player == null)
            {
                this.m_Player = this.GetComponentInParent<Character>();
            }

            if (this.m_MovementBridge == null && this.m_Player != null)
            {
                this.m_MovementBridge = this.m_Player.GetComponentInChildren<
                    FranklinAnimationBridge
                >(true);
            }

            return this.m_Player != null;
        }

        private void LockMovementAnimation()
        {
            this.m_IsVehicleAnimationLocked = true;
            this.m_HasEnteredVehicle = false;
            this.m_EntryRequestedAt = UnityEngine.Time.unscaledTime;
            this.m_MovementBridge?.SetExternalAnimationLock(true);
        }

        private void UpdateVehicleAnimationLock()
        {
            if (!this.m_IsVehicleAnimationLocked) return;

            bool isControllable = this.m_Player.Player?.IsControllable == true;
            if (!this.m_HasEnteredVehicle)
            {
                if (!isControllable)
                {
                    this.m_HasEnteredVehicle = true;
                    this.RestoreVehicleDrivingIdle();
                    this.ActivateVehicleCamera();
                    return;
                }

                // The nearby vehicle Trigger normally makes the Player uncontrollable once
                // its enter animation begins. Avoid leaving controls locked if its conditions
                // reject the interaction or another trigger consumes it.
                if (UnityEngine.Time.unscaledTime - this.m_EntryRequestedAt <
                    ENTRY_BEGIN_TIMEOUT)
                {
                    return;
                }
            }
            else if (!isControllable)
            {
                return;
            }

            this.m_IsVehicleAnimationLocked = false;
            this.m_MovementBridge?.SetExternalAnimationLock(false);
            this.ClearDelayedDrivingState();
            this.RestorePlayerCamera();
            this.m_ActiveVehiclePivot = null;
        }

        private void DelayVehicleDrivingIdle(Component vehicleEntry)
        {
            this.ClearDelayedDrivingState();
            if (!this.m_DelayDrivingIdleUntilEntryFinishes) return;

            System.Type type = vehicleEntry.GetType();
            FieldInfo stateField = type.GetField("drivingState");
            if (stateField?.GetValue(vehicleEntry) is not StateData state) return;

            this.m_DelayedDrivingState = state;
            this.m_DelayedDrivingStateLayer = ReadIntField(
                type,
                vehicleEntry,
                "drivingStateLayer"
            );
            this.m_DelayedDrivingStateTransitionIn = ReadFloatField(
                type,
                vehicleEntry,
                "drivingStateTransitionIn"
            );
            this.m_DelayedDrivingStateTransitionOut = ReadFloatField(
                type,
                vehicleEntry,
                "drivingStateTransitionOut"
            );
            this.m_IsDrivingStateSuppressed = true;
        }

        private void RestoreVehicleDrivingIdle()
        {
            if (!this.m_IsDrivingStateSuppressed) return;

            ConfigState config = new ConfigState(
                0f,
                1f,
                1f,
                this.m_DelayedDrivingStateTransitionIn,
                this.m_DelayedDrivingStateTransitionOut
            );
            _ = this.m_Player.States?.SetState(
                this.m_DelayedDrivingState,
                this.m_DelayedDrivingStateLayer,
                BlendMode.Blend,
                config
            );
            this.ClearDelayedDrivingState();
        }

        private void ClearDelayedDrivingState()
        {
            this.m_IsDrivingStateSuppressed = false;
            this.m_DelayedDrivingState = default;
            this.m_DelayedDrivingStateLayer = 0;
            this.m_DelayedDrivingStateTransitionIn = 0f;
            this.m_DelayedDrivingStateTransitionOut = 0f;
        }

        private void ActivateVehicleCamera()
        {
            if (!this.m_UseVehicleCamera || this.m_IsVehicleCameraActive) return;
            if (this.m_ActiveVehiclePivot != null &&
                this.m_ActiveVehiclePivot.GetComponent<SimcadeCarDriver>() != null)
            {
                return;
            }
            if (!this.EnsureVehicleShot() || !this.ResolveMainCamera()) return;

            this.ApplyVehicleCameraSettings();
            this.m_PreVehicleShot = this.m_MainCamera.Transition.CurrentShotCamera;
            this.m_MainCamera.Transition.ChangeToShot(
                this.m_RuntimeVehicleShot,
                this.m_VehicleCameraEnterBlend,
                this.m_VehicleCameraEasing
            );
            this.m_IsVehicleCameraActive = true;
        }

        private void RestorePlayerCamera()
        {
            if (!this.m_IsVehicleCameraActive) return;

            if (this.ResolveMainCamera() && this.m_PreVehicleShot != null &&
                this.m_MainCamera.Transition.CurrentShotCamera == this.m_RuntimeVehicleShot)
            {
                this.m_MainCamera.Transition.ChangeToShot(
                    this.m_PreVehicleShot,
                    this.m_VehicleCameraExitBlend,
                    this.m_VehicleCameraEasing
                );
            }

            this.m_PreVehicleShot = null;
            this.m_IsVehicleCameraActive = false;
        }

        private bool EnsureVehicleShot()
        {
            if (this.m_RuntimeVehicleShot != null) return true;
            if (this.m_VehicleShotPrefab == null)
            {
                Debug.LogWarning(
                    "Vehicle camera is enabled but no Camera Shot Vehicle prefab is assigned.",
                    this
                );
                return false;
            }

            this.m_RuntimeVehicleShot = Instantiate(this.m_VehicleShotPrefab);
            this.m_RuntimeVehicleShot.name = "Runtime Vehicle Camera Shot";
            this.CacheDefaultVehiclePivot();
            return this.m_RuntimeVehicleShot != null;
        }

        private bool ResolveMainCamera()
        {
            if (this.m_MainCamera == null)
            {
                this.m_MainCamera = ShortcutMainCamera.Get<MainCamera>();
            }

            return this.m_MainCamera != null;
        }

        private void ApplyVehicleCameraSettings()
        {
            if (this.m_RuntimeVehicleShot?.ShotType is not ShotTypeThirdPerson shotType) return;

            ShotSystemThirdPerson thirdPerson = shotType.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            if (thirdPerson == null) return;

            SetDecimalField(thirdPerson, THIRD_PERSON_SHOULDER_FIELD, this.m_VehicleShoulder);
            SetDecimalField(thirdPerson, THIRD_PERSON_LIFT_FIELD, this.m_VehicleLift);
            SetDecimalField(thirdPerson, THIRD_PERSON_RADIUS_FIELD, this.m_VehicleRadius);
            SetDecimalField(thirdPerson, THIRD_PERSON_SMOOTH_TIME_FIELD, this.m_VehicleSmoothTime);
            this.ApplyVehicleCameraPivot(thirdPerson);

            if (!this.m_UseGlobalVehicleSensitivity)
            {
                thirdPerson.Sensitivity = new Vector2(
                    this.m_VehicleSensitivityX,
                    this.m_VehicleSensitivityY
                );
            }

            thirdPerson.MaxPitch = this.m_VehicleMaxPitch;
            thirdPerson.Alignment.AutoAlign = this.m_VehicleAutoAlign;
            thirdPerson.Alignment.Delay = this.m_VehicleAlignDelay;
            thirdPerson.Alignment.SmoothTime = this.m_VehicleAlignSmoothTime;

            if (THIRD_PERSON_MAX_YAW_FIELD?.GetValue(thirdPerson) is EnablerAngle180 maxYaw)
            {
                maxYaw.IsEnabled = this.m_EnableVehicleMaxYaw;
                maxYaw.Value = this.m_VehicleMaxYaw;
            }
        }

        private void CacheDefaultVehiclePivot()
        {
            if (this.m_RuntimeVehicleShot?.ShotType is not ShotTypeThirdPerson shotType) return;

            ShotSystemThirdPerson thirdPerson = shotType.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            if (thirdPerson == null) return;

            this.m_DefaultVehiclePivot = THIRD_PERSON_PIVOT_FIELD?.GetValue(
                thirdPerson
            ) as PropertyGetGameObject;
        }

        private void ApplyVehicleCameraPivot(ShotSystemThirdPerson thirdPerson)
        {
            if (THIRD_PERSON_PIVOT_FIELD == null) return;

            PropertyGetGameObject pivot = this.m_PivotCameraToActiveVehicle &&
                this.m_ActiveVehiclePivot != null
                ? GetGameObjectInstance.Create(this.m_ActiveVehiclePivot)
                : this.m_DefaultVehiclePivot;

            if (pivot != null)
            {
                THIRD_PERSON_PIVOT_FIELD.SetValue(thirdPerson, pivot);
            }
        }

        private static void SetDecimalField(
            ShotSystemThirdPerson thirdPerson,
            FieldInfo field,
            float value)
        {
            field?.SetValue(thirdPerson, new PropertyGetDecimal(value));
        }

        private static Component FindVehicleEntry(GameObject target)
        {
            for (Transform current = target.transform; current != null; current = current.parent)
            {
                Component entry = current.GetComponent("CarEntry") ??
                                  current.GetComponent("BikeEntry");
                if (entry != null) return entry;
            }

            return null;
        }

        private static int ReadIntField(System.Type type, object target, string fieldName)
        {
            return type.GetField(fieldName)?.GetValue(target) is int value ? value : 0;
        }

        private static float ReadFloatField(System.Type type, object target, string fieldName)
        {
            object value = type.GetField(fieldName)?.GetValue(target);
            return value is float result ? result : 0f;
        }

    }
}
