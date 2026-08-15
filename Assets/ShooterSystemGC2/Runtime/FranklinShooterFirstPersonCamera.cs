using FranklinGame.Vehicles;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// On-foot facade for the unified Player-child FPS manager. Shooter uses the
    /// native GC2 camera path; the owning system keeps melee presentation in TPS.
    /// </summary>
    [DefaultExecutionOrder(625)]
    [DisallowMultipleComponent]
    public sealed class FranklinShooterFirstPersonCamera : MonoBehaviour
    {
        private const string RUNTIME_MOUNT_NAME =
            "Franklin On Foot FPS Pivot (Runtime)";
        private const float TRANSITION_DURATION = 0.22f;

        private Character m_Player;
        private Animator m_Animator;
        private MainCamera m_MainCamera;
        private FranklinFirstPersonCameraManager m_Manager;
        private GameObject m_RuntimeMount;
        private FirstPersonHeadOcclusion m_HeadOcclusion;
        private ShotCamera m_ReturnShot;
        private System.Action m_RefreshPreviewPose;
        private bool m_ExternalCameraOwnsShot;
        private bool m_IsPresenting;
        private FranklinFirstPersonCameraManager.Context m_Context =
            FranklinFirstPersonCameraManager.Context.PlayerMovement;

        public bool IsRequested { get; private set; }
        public bool IsActive => this.m_Manager != null &&
                                this.m_Manager.IsOwnedBy(this);

        public void Initialize(Character player)
        {
            this.m_RefreshPreviewPose ??= this.UpdateRuntimeMountPosition;
            if (this.m_Player == player && this.m_Manager != null)
            {
                if (player != null && this.m_RuntimeMount == null)
                {
                    this.m_Animator = player.Animim?.Animator;
                    this.CreateRuntimeMount();
                }
                return;
            }

            this.ReleaseRuntimeObjects(true);
            this.m_Player = player;
            this.m_Animator = player != null ? player.Animim?.Animator : null;
            this.m_Manager = FranklinFirstPersonCameraManager.Resolve(player);
            this.IsRequested = false;
            this.m_ExternalCameraOwnsShot = false;
            if (player != null) this.CreateRuntimeMount();
        }

        public void Toggle(bool canPresent)
        {
            this.SetRequested(!this.IsRequested, canPresent, this.m_Context);
        }

        public void SetRequested(bool requested, bool canPresent)
        {
            this.SetRequested(requested, canPresent, this.m_Context);
        }

        public void SetRequested(
            bool requested,
            bool canPresent,
            FranklinFirstPersonCameraManager.Context context)
        {
            this.IsRequested = requested;
            this.m_Context = context;
            if (!requested) this.m_ExternalCameraOwnsShot = false;
            this.Refresh(canPresent);
        }

        public void SetOrbitSuppressed(bool suppressed)
        {
            this.m_Manager?.SetOrbitSuppressed(this, suppressed);
        }

        public void Refresh(bool canPresent)
        {
            if (!this.ResolveMainCamera() || this.m_Manager == null) return;

            ShotCamera current = this.m_MainCamera.Transition.CurrentShotCamera;
            if (this.IsActive && current != this.m_Manager.ActiveShot)
            {
                ShotCamera interrupted = this.m_Manager.ActiveShot;
                this.Deactivate(false, 0f);
                this.m_ReturnShot = interrupted;
                this.m_ExternalCameraOwnsShot = true;
            }
            else if (this.m_IsPresenting && !this.IsActive)
            {
                // The unified manager yielded to Car/Bike or an external Shot.
                this.EndPresentation();
                this.m_ExternalCameraOwnsShot = current != this.m_ReturnShot;
            }

            if (!this.IsRequested || !canPresent)
            {
                if (this.IsActive || this.m_IsPresenting)
                    this.Deactivate(true, TRANSITION_DURATION);
                if (!canPresent)
                {
                    this.m_ExternalCameraOwnsShot = false;
                    this.m_ReturnShot = null;
                }
                return;
            }

            if (this.m_ExternalCameraOwnsShot)
            {
                if (current != this.m_ReturnShot) return;
                this.m_ExternalCameraOwnsShot = false;
            }

            if (!this.IsActive) this.Activate(current);
            else
            {
                if (this.m_RuntimeMount == null)
                {
                    this.Activate(current);
                    return;
                }

                // Context can change between Player movement and Shooter while
                // the same FPS presentation remains active.
                this.m_Manager.Activate(
                    this,
                    this.m_Context,
                    current,
                    this.m_RuntimeMount.transform,
                    this.m_Player.transform,
                    this.m_RefreshPreviewPose
                );
            }
        }

        public void Shutdown()
        {
            this.IsRequested = false;
            this.ReleaseRuntimeObjects(true);
            this.m_Player = null;
            this.m_Animator = null;
            this.m_MainCamera = null;
            this.m_Manager = null;
        }

        private bool Activate(ShotCamera shot)
        {
            if (this.m_Player == null || this.m_Manager == null || shot == null)
                return false;

            if (this.m_RuntimeMount == null)
            {
                if (this.m_Animator == null)
                    this.m_Animator = this.m_Player.Animim?.Animator;
                this.CreateRuntimeMount();
            }
            if (this.m_RuntimeMount == null) return false;

            this.UpdateRuntimeMountPosition();
            if (!this.m_Manager.Activate(
                    this,
                    this.m_Context,
                    shot,
                    this.m_RuntimeMount.transform,
                    this.m_Player.transform,
                    this.m_RefreshPreviewPose
                ))
            {
                return false;
            }

            this.m_ReturnShot = shot;
            this.BeginPresentation();
            return true;
        }

        private void Deactivate(bool restoreViewport, float duration)
        {
            this.m_Manager?.Deactivate(this, restoreViewport, duration);
            this.EndPresentation();
            if (restoreViewport) this.m_ReturnShot = null;
        }

        private void CreateRuntimeMount()
        {
            if (this.m_Player == null || this.m_RuntimeMount != null)
            {
                return;
            }

            this.m_RuntimeMount = new GameObject(RUNTIME_MOUNT_NAME);
            this.UpdateRuntimeMountPosition();
            Transform parent = this.m_Animator != null
                ? this.m_Animator.transform
                : this.m_Player.transform;
            this.m_RuntimeMount.transform.SetParent(parent, true);
        }

        private void UpdateRuntimeMountPosition()
        {
            if (this.m_RuntimeMount == null || this.m_Manager == null ||
                this.m_Player == null)
            {
                return;
            }

            Vector3 position = this.m_Manager.GetEyeWorldPosition(
                this.m_Context,
                this.m_Animator,
                this.m_Player.transform,
                this.m_Player.transform
            );
            this.m_RuntimeMount.transform.SetPositionAndRotation(
                position,
                GetYawOnlyRotation(this.m_Player.transform)
            );
        }

        private void BeginPresentation()
        {
            if (this.m_Animator == null || this.m_IsPresenting) return;

            this.m_HeadOcclusion =
                this.m_Animator.GetComponent<FirstPersonHeadOcclusion>();
            if (this.m_HeadOcclusion == null)
            {
                this.m_HeadOcclusion =
                    this.m_Animator.gameObject.AddComponent<FirstPersonHeadOcclusion>();
            }
            this.m_HeadOcclusion.Begin(this.m_Animator);
            this.m_IsPresenting = true;
        }

        private void EndPresentation()
        {
            if (!this.m_IsPresenting) return;
            this.m_HeadOcclusion?.End();
            this.m_HeadOcclusion = null;
            this.m_IsPresenting = false;
        }

        private bool ResolveMainCamera()
        {
            if (this.m_MainCamera == null)
                this.m_MainCamera = ShortcutMainCamera.Get<MainCamera>();
            return this.m_MainCamera != null;
        }

        private void ReleaseRuntimeObjects(bool restoreViewport)
        {
            if (this.IsActive || this.m_IsPresenting)
                this.Deactivate(restoreViewport, 0f);
            else this.EndPresentation();

            if (this.m_RuntimeMount != null)
            {
                this.m_RuntimeMount.name = RUNTIME_MOUNT_NAME + " (Released)";
                Destroy(this.m_RuntimeMount);
            }

            this.m_RuntimeMount = null;
            this.m_ReturnShot = null;
            this.m_ExternalCameraOwnsShot = false;
        }

        private static Quaternion GetYawOnlyRotation(Transform source)
        {
            Vector3 forward = source != null
                ? Vector3.ProjectOnPlane(source.forward, Vector3.up)
                : Vector3.forward;
            return forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                : Quaternion.identity;
        }

        private void OnDisable()
        {
            if (this.IsActive || this.m_IsPresenting) this.Deactivate(true, 0f);
        }

        private void OnDestroy()
        {
            this.ReleaseRuntimeObjects(true);
        }
    }
}
