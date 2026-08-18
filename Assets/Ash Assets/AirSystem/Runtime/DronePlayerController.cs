using FranklinGame.Animations;
using FranklinGame.UI;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using GCMainCamera = GameCreator.Runtime.Cameras.MainCamera;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// Player-owned remote-control session. It leases GC2 player/camera state through
    /// public APIs, and restores only state still owned by this session.
    /// </summary>
    [DefaultExecutionOrder(450)]
    [DisallowMultipleComponent]
    public sealed class DronePlayerController : MonoBehaviour, IAirHudActionHandler
    {
        private const float NEAREST_REFRESH_SECONDS = 0.15f;

        [SerializeField] private Character m_Player;
        [SerializeField] private FranklinAnimationBridge m_MovementBridge;
        [SerializeField, Min(0.5f)] private float m_InteractionDistance = 3.5f;
        [SerializeField, Min(0f)] private float m_CameraEnterBlend = 0.35f;
        [SerializeField, Min(0f)] private float m_CameraExitBlend = 0.35f;
        [SerializeField] private Easing.Type m_CameraEasing = Easing.Type.QuadInOut;
        [SerializeField] private DroneMobileHud m_HudPrefab;
        [SerializeField] private DroneControllerHandPresentation m_HandPresentation;
        [SerializeField] private bool m_ShowTouchHudInEditor = true;

        private DroneMobileHud m_Hud;
        private DroneFlightController m_NearestDrone;
        private DroneFlightController m_CurrentDrone;
        private GCMainCamera m_MainCamera;
        private ShotCamera m_PreviousShot;
        private PhysicsRaycaster m_PhysicsRaycaster;
        private float m_NextNearestRefresh;
        private bool m_PreviousControllable;
        private bool m_HadPlayerLease;
        private bool m_HudSuppressed;
        private bool m_PhysicsRaycasterWasEnabled;
        private bool m_DisabledPhysicsRaycaster;
        private bool m_WarnedMissingSetup;

        public bool IsControlling => this.m_CurrentDrone != null;

        private void Awake()
        {
            if (!this.ResolvePlayer())
            {
                this.enabled = false;
                return;
            }

            bool showTouchControls = Application.isMobilePlatform;
#if UNITY_EDITOR
            showTouchControls |= this.m_ShowTouchHudInEditor;
#endif
            if (showTouchControls)
            {
                this.m_Hud = DroneMobileHud.Create(
                    this.m_HudPrefab,
                    this,
                    this.transform,
                    true
                );
                if (this.m_Hud == null)
                {
                    this.WarnMissingSetup(
                        "AirSystem requires the separate Canvas Air Control prefab."
                    );
                }
            }
        }

        private void OnValidate()
        {
            this.m_InteractionDistance = Mathf.Max(0.5f, this.m_InteractionDistance);
            this.m_CameraEnterBlend = Mathf.Max(0f, this.m_CameraEnterBlend);
            this.m_CameraExitBlend = Mathf.Max(0f, this.m_CameraExitBlend);
            this.ResolvePlayer();
        }

        private void OnDisable()
        {
            this.EndControl();
            if (this.m_Hud != null) this.m_Hud.SetState(false, false);
        }

        private void Update()
        {
            if (!this.ResolvePlayer()) return;

            if (this.m_CurrentDrone != null)
            {
                if (this.m_Player.IsDead || this.CameraOwnershipWasLost())
                {
                    this.EndControl();
                    return;
                }

                if (WasExitPressed())
                {
                    this.EndControl();
                    return;
                }

                this.UpdateFlightInput();
                return;
            }

            this.RefreshNearestDrone(false);
            bool canConnect = this.CanConnectToNearest();
            if (this.m_Hud != null) this.m_Hud.SetState(canConnect, false);
            if (canConnect && WasConnectPressed())
            {
                this.TryBeginControl(this.m_NearestDrone);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) return;
            this.ResetTransientInput();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (!isPaused) return;
            this.ResetTransientInput();
        }

        public void HandleHudAction(DroneHudAction action)
        {
            switch (action)
            {
                case DroneHudAction.Connect:
                    this.RefreshNearestDrone(true);
                    if (this.CanConnectToNearest())
                    {
                        this.TryBeginControl(this.m_NearestDrone);
                    }
                    break;

                case DroneHudAction.Exit:
                    this.EndControl();
                    break;
            }
        }

        private bool TryBeginControl(DroneFlightController drone)
        {
            if (IsExternalModalActive() || drone == null ||
                !drone.IsAvailable || drone.CameraShot == null ||
                this.m_Player.Player?.IsControllable != true)
            {
                return false;
            }

            // Unity objects use fake-null after destruction, so ??= would keep a
            // dead wrapper across scene/camera recreation.
            if (this.m_MainCamera == null)
            {
                this.m_MainCamera = ShortcutMainCamera.Get<GCMainCamera>();
            }
            if (this.m_MainCamera == null)
            {
                this.WarnMissingSetup("AirSystem requires the existing GC2 Main Camera.");
                return false;
            }

            ShotCamera currentShot = this.m_MainCamera.Transition.CurrentShotCamera;
            if (currentShot == null)
            {
                this.WarnMissingSetup("AirSystem requires an active GC2 Camera Shot.");
                return false;
            }

            if (!drone.TryAcquire(this)) return false;

            this.m_CurrentDrone = drone;
            this.m_CurrentDrone.EventControlLost += this.OnDroneControlLost;
            this.m_PreviousShot = currentShot;

            this.m_PreviousControllable = this.m_Player.Player.IsControllable;
            this.m_HadPlayerLease = true;
            this.m_Player.Player.IsControllable = false;
            if (this.m_MovementBridge != null)
            {
                this.m_MovementBridge.SetExternalAnimationLock(true);
            }
            if (this.m_HandPresentation != null)
            {
                this.m_HandPresentation.SetPresented(true);
            }

            FranklinMobileHud.AcquireControlsSuppression(this);
            this.m_HudSuppressed = true;
            this.DisableWorldUiRaycaster();

            this.m_MainCamera.Transition.ChangeToShot(
                drone.CameraShot,
                this.m_CameraEnterBlend,
                this.m_CameraEasing
            );
            if (this.m_Hud != null) this.m_Hud.SetState(false, true);
            return true;
        }

        private void EndControl()
        {
            DroneFlightController drone = this.m_CurrentDrone;
            if (drone == null)
            {
                this.ReleasePlayerAndHud(this.m_Player != null && !this.m_Player.IsDead);
                return;
            }

            this.m_CurrentDrone = null;
            drone.EventControlLost -= this.OnDroneControlLost;

            bool stillOwnsCamera = this.m_MainCamera != null && drone.CameraShot != null &&
                                   this.m_MainCamera.Transition.CurrentShotCamera ==
                                   drone.CameraShot;
            if (stillOwnsCamera && this.m_PreviousShot != null)
            {
                this.m_MainCamera.Transition.ChangeToShot(
                    this.m_PreviousShot,
                    this.m_CameraExitBlend,
                    this.m_CameraEasing
                );
            }
            this.m_PreviousShot = null;

            drone.Release(this);
            // Camera ownership only decides whether it is safe to restore the
            // previous shot. The player lease is ours and must be returned on
            // every normal exit, including an external camera takeover.
            this.ReleasePlayerAndHud(this.m_Player != null && !this.m_Player.IsDead);
            this.m_NearestDrone = null;
            this.m_NextNearestRefresh = 0f;
        }

        private void ReleasePlayerAndHud(bool allowPlayerControlRestore)
        {
            this.RestoreWorldUiRaycaster();

            if (this.m_HandPresentation != null)
            {
                this.m_HandPresentation.SetPresented(false);
            }

            if (this.m_HudSuppressed)
            {
                FranklinMobileHud.ReleaseControlsSuppression(this);
                this.m_HudSuppressed = false;
            }

            // The animation lock is exclusively owned by this component, so it
            // must always be released even if another system took the camera.
            if (this.m_HadPlayerLease)
            {
                if (this.m_MovementBridge != null)
                {
                    this.m_MovementBridge.SetExternalAnimationLock(false);
                }
            }
            if (this.m_HadPlayerLease && allowPlayerControlRestore &&
                this.m_Player != null && !this.m_Player.IsDead && this.m_Player.Player != null)
            {
                this.m_Player.Player.IsControllable = this.m_PreviousControllable;
            }
            this.m_HadPlayerLease = false;
            if (this.m_Hud != null) this.m_Hud.SetState(false, false);
        }

        private void OnDroneControlLost(DroneFlightController drone)
        {
            if (drone != this.m_CurrentDrone) return;
            this.EndControl();
        }

        private void UpdateFlightInput()
        {
            Vector2 move = Vector2.zero;
            float yaw = 0f;
            float lift = 0f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                move.x += ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
                move.y += ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
                yaw += ReadAxis(keyboard.qKey.isPressed, keyboard.eKey.isPressed);
                bool descend = keyboard.leftCtrlKey.isPressed ||
                               keyboard.rightCtrlKey.isPressed ||
                               keyboard.cKey.isPressed;
                lift += ReadAxis(descend, keyboard.spaceKey.isPressed);
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                move += gamepad.leftStick.ReadValue();
                yaw += ReadAxis(
                    gamepad.leftShoulder.isPressed,
                    gamepad.rightShoulder.isPressed
                );
                lift += gamepad.rightTrigger.ReadValue() -
                        gamepad.leftTrigger.ReadValue();
            }

            if (this.m_Hud != null)
            {
                move += this.m_Hud.MoveInput;
                lift += this.m_Hud.LiftInput;
            }

            if (this.m_CurrentDrone != null)
            {
                Vector3 cameraForward = this.m_MainCamera != null
                    ? this.m_MainCamera.transform.forward
                    : this.m_CurrentDrone.transform.forward;
                this.m_CurrentDrone.SetInput(
                    Vector2.ClampMagnitude(move, 1f),
                    Mathf.Clamp(yaw, -1f, 1f),
                    Mathf.Clamp(lift, -1f, 1f),
                    cameraForward
                );
            }
        }

        private void RefreshNearestDrone(bool force)
        {
            if (!force && Time.unscaledTime < this.m_NextNearestRefresh) return;
            this.m_NextNearestRefresh = Time.unscaledTime + NEAREST_REFRESH_SECONDS;
            this.m_NearestDrone = null;

            if (IsExternalModalActive() || this.m_Player == null ||
                this.m_Player.Player?.IsControllable != true || this.m_Player.IsDead)
            {
                return;
            }

            float maximumDistanceSquared = this.m_InteractionDistance *
                                           this.m_InteractionDistance;
            float nearestDistanceSquared = maximumDistanceSquared;
            Vector3 playerPosition = this.m_Player.transform.position;
            var instances = DroneFlightController.Instances;
            for (int i = 0; i < instances.Count; ++i)
            {
                DroneFlightController candidate = instances[i];
                if (candidate == null || !candidate.IsAvailable) continue;

                float distanceSquared = (candidate.transform.position - playerPosition)
                    .sqrMagnitude;
                if (distanceSquared > nearestDistanceSquared) continue;

                nearestDistanceSquared = distanceSquared;
                this.m_NearestDrone = candidate;
            }
        }

        private bool CanConnectToNearest()
        {
            return this.m_CurrentDrone == null && this.m_NearestDrone != null &&
                   this.m_NearestDrone.IsAvailable && !this.m_Player.IsDead &&
                   !IsExternalModalActive() &&
                   this.m_Player.Player?.IsControllable == true;
        }

        private bool CameraOwnershipWasLost()
        {
            if (this.m_CurrentDrone == null) return false;
            if (this.m_MainCamera == null) return true;
            return this.m_MainCamera.Transition.CurrentShotCamera !=
                   this.m_CurrentDrone.CameraShot;
        }

        private void DisableWorldUiRaycaster()
        {
            if (this.m_MainCamera == null) return;
            this.m_PhysicsRaycaster = this.m_MainCamera.GetComponent<PhysicsRaycaster>();
            if (this.m_PhysicsRaycaster == null) return;

            this.m_PhysicsRaycasterWasEnabled = this.m_PhysicsRaycaster.enabled;
            if (!this.m_PhysicsRaycasterWasEnabled) return;
            this.m_PhysicsRaycaster.enabled = false;
            this.m_DisabledPhysicsRaycaster = true;
        }

        private void RestoreWorldUiRaycaster()
        {
            if (this.m_DisabledPhysicsRaycaster && this.m_PhysicsRaycaster != null)
            {
                this.m_PhysicsRaycaster.enabled = this.m_PhysicsRaycasterWasEnabled;
            }
            this.m_DisabledPhysicsRaycaster = false;
            this.m_PhysicsRaycaster = null;
        }

        private bool ResolvePlayer()
        {
            if (this.m_Player == null)
            {
                this.m_Player = this.GetComponentInParent<Character>();
            }
            if (this.m_Player == null) return false;

            if (this.m_MovementBridge == null)
            {
                this.m_MovementBridge = this.m_Player.GetComponentInChildren<
                    FranklinAnimationBridge
                >(true);
            }
            if (this.m_HandPresentation == null)
            {
                this.m_HandPresentation = this.GetComponent<
                    DroneControllerHandPresentation
                >();
            }
            return true;
        }

        private void WarnMissingSetup(string message)
        {
            if (this.m_WarnedMissingSetup) return;
            this.m_WarnedMissingSetup = true;
            Debug.LogWarning(message, this);
        }

        private void ResetTransientInput()
        {
            if (this.m_Hud != null) this.m_Hud.ResetInput();
            if (this.m_CurrentDrone != null) this.m_CurrentDrone.ClearInput();
        }

        private static bool WasConnectPressed()
        {
            return Keyboard.current?.fKey.wasPressedThisFrame == true ||
                   Gamepad.current?.buttonNorth.wasPressedThisFrame == true;
        }

        private static bool IsExternalModalActive()
        {
            return FranklinMobileHud.ControlsSuppressed ||
                   FranklinMobileHud.FastMovementSuppressed;
        }

        private static bool WasExitPressed()
        {
            return Keyboard.current?.fKey.wasPressedThisFrame == true ||
                   Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                   Gamepad.current?.buttonEast.wasPressedThisFrame == true;
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }
}
