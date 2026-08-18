using GameCreator.Runtime.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// Keeps one lightweight controller model and blends both humanoid hands onto
    /// its grips while the Player owns a drone-control session.
    /// </summary>
    [DefaultExecutionOrder(460)]
    [DisallowMultipleComponent]
    public sealed class DroneControllerHandPresentation : MonoBehaviour
    {
        private const float VISIBLE_WEIGHT = 0.01f;

        [SerializeField] private Character m_Player;
        [SerializeField] private GameObject m_ControllerPrefab;

        [Header("Controller Pose")]
        [SerializeField] private Vector3 m_ControllerChestOffset =
            new Vector3(0f, -0.24f, 0.29f);
        [SerializeField] private Vector3 m_ControllerScale =
            new Vector3(0.075f, 0.075f, 0.075f);
        [SerializeField, Range(0f, 45f)] private float m_ScreenTiltTowardPlayer = 18f;

        [Header("Hand IK")]
        [SerializeField] private Vector3 m_LeftGripOffset =
            new Vector3(-0.135f, -0.035f, 0f);
        [SerializeField] private Vector3 m_RightGripOffset =
            new Vector3(0.135f, -0.035f, 0f);
        [SerializeField] private Vector3 m_LeftHandEuler =
            new Vector3(8f, 18f, 80f);
        [SerializeField] private Vector3 m_RightHandEuler =
            new Vector3(8f, -18f, -80f);
        [SerializeField] private Vector3 m_LeftElbowChestOffset =
            new Vector3(-0.38f, -0.23f, 0.12f);
        [SerializeField] private Vector3 m_RightElbowChestOffset =
            new Vector3(0.38f, -0.23f, 0.12f);
        [SerializeField, Range(0f, 1f)] private float m_HandIKWeight = 0.95f;
        [SerializeField, Range(0f, 1f)] private float m_HandRotationWeight = 0.7f;
        [SerializeField, Min(0f)] private float m_BlendInTime = 0.22f;
        [SerializeField, Min(0f)] private float m_BlendOutTime = 0.18f;

        private GameObject m_ControllerInstance;
        private Animator m_Animator;
        private Transform m_Chest;
        private float m_PresentationWeight;
        private bool m_TargetPresented;
        private bool m_IKRegistered;
        private bool m_WarnedMissingSetup;

        public bool IsPresented => this.m_TargetPresented;

        private void Awake()
        {
            this.ResolvePlayer();
        }

        private void OnValidate()
        {
            this.m_ControllerScale.x = Mathf.Max(0.001f, this.m_ControllerScale.x);
            this.m_ControllerScale.y = Mathf.Max(0.001f, this.m_ControllerScale.y);
            this.m_ControllerScale.z = Mathf.Max(0.001f, this.m_ControllerScale.z);
            this.m_BlendInTime = Mathf.Max(0f, this.m_BlendInTime);
            this.m_BlendOutTime = Mathf.Max(0f, this.m_BlendOutTime);
            this.ResolvePlayer();
        }

        private void OnDisable()
        {
            this.HideImmediate();
        }

        private void OnDestroy()
        {
            this.UnregisterAnimatorIK();
            if (this.m_ControllerInstance != null)
            {
                Destroy(this.m_ControllerInstance);
                this.m_ControllerInstance = null;
            }
        }

        private void Update()
        {
            if (!this.m_TargetPresented && this.m_PresentationWeight <= 0f) return;

            float target = this.m_TargetPresented ? 1f : 0f;
            float duration = this.m_TargetPresented
                ? this.m_BlendInTime
                : this.m_BlendOutTime;
            this.m_PresentationWeight = duration <= 0f
                ? target
                : Mathf.MoveTowards(
                    this.m_PresentationWeight,
                    target,
                    Time.unscaledDeltaTime / duration
                );

            if (!this.m_TargetPresented && this.m_PresentationWeight <= 0f)
            {
                if (this.m_ControllerInstance != null)
                    this.m_ControllerInstance.SetActive(false);
                this.UnregisterAnimatorIK();
            }
        }

        private void LateUpdate()
        {
            if (this.m_PresentationWeight <= VISIBLE_WEIGHT ||
                !this.EnsureReady())
            {
                return;
            }

            this.ApplyControllerPose();
            if (!this.m_ControllerInstance.activeSelf)
                this.m_ControllerInstance.SetActive(true);
        }

        public void SetPresented(bool presented)
        {
            this.m_TargetPresented = presented;
            if (!presented)
            {
                if (this.m_PresentationWeight <= 0f)
                {
                    if (this.m_ControllerInstance != null)
                        this.m_ControllerInstance.SetActive(false);
                    this.UnregisterAnimatorIK();
                }
                return;
            }

            if (!this.EnsureReady()) return;
            this.RegisterAnimatorIK();
            this.ApplyControllerPose();
            this.m_ControllerInstance.SetActive(true);
        }

        private bool EnsureReady()
        {
            if (!this.ResolvePlayer() || this.m_ControllerPrefab == null)
            {
                this.WarnMissingSetup();
                return false;
            }

            Animator animator = this.m_Player.Animim?.Animator;
            if (animator == null)
                animator = this.m_Player.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman || animator.avatar == null ||
                !animator.avatar.isHuman)
            {
                this.WarnMissingSetup();
                return false;
            }

            if (this.m_Animator != animator || this.m_Chest == null)
            {
                this.UnregisterAnimatorIK();
                this.m_Animator = animator;
                this.m_Chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                               animator.GetBoneTransform(HumanBodyBones.Chest) ??
                               animator.GetBoneTransform(HumanBodyBones.Spine);
            }
            if (this.m_Chest == null)
            {
                this.WarnMissingSetup();
                return false;
            }

            if (this.m_ControllerInstance == null)
            {
                this.m_ControllerInstance = Instantiate(
                    this.m_ControllerPrefab,
                    this.m_Player.transform,
                    false
                );
                this.m_ControllerInstance.name = "Franklin Drone Controller";
                this.m_ControllerInstance.transform.localScale = this.m_ControllerScale;
                this.ConfigureControllerInstance();
                this.m_ControllerInstance.SetActive(false);
            }
            return true;
        }

        private void ConfigureControllerInstance()
        {
            foreach (Animator animator in
                     this.m_ControllerInstance.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }
            foreach (Collider controllerCollider in
                     this.m_ControllerInstance.GetComponentsInChildren<Collider>(true))
            {
                controllerCollider.enabled = false;
            }
            foreach (Renderer controllerRenderer in
                     this.m_ControllerInstance.GetComponentsInChildren<Renderer>(true))
            {
                controllerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                controllerRenderer.receiveShadows = false;
                controllerRenderer.motionVectorGenerationMode =
                    MotionVectorGenerationMode.ForceNoMotion;
            }
        }

        private void ApplyControllerPose()
        {
            if (this.m_ControllerInstance == null || this.m_Chest == null) return;

            Transform character = this.m_Player.transform;
            Vector3 position = this.GetChestPoint(this.m_ControllerChestOffset);
            Quaternion tilt = Quaternion.AngleAxis(
                -this.m_ScreenTiltTowardPlayer,
                character.right
            );
            Vector3 screenNormal = tilt * character.up;
            Vector3 controllerTop = tilt * character.forward;
            Quaternion rotation = Quaternion.LookRotation(screenNormal, controllerTop);
            this.m_ControllerInstance.transform.SetPositionAndRotation(position, rotation);
            this.m_ControllerInstance.transform.localScale = this.m_ControllerScale;
        }

        private void RegisterAnimatorIK()
        {
            if (this.m_IKRegistered || this.m_Player?.Animim == null) return;
            this.m_Player.Animim.EventOnAnimatorIK -= this.OnAnimatorIK;
            this.m_Player.Animim.EventOnAnimatorIK += this.OnAnimatorIK;
            this.m_IKRegistered = true;
        }

        private void UnregisterAnimatorIK()
        {
            if (this.m_Player?.Animim != null)
                this.m_Player.Animim.EventOnAnimatorIK -= this.OnAnimatorIK;
            this.m_IKRegistered = false;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (this.m_Animator == null || this.m_Chest == null ||
                this.m_PresentationWeight <= 0f)
            {
                return;
            }

            Transform character = this.m_Player.transform;
            Vector3 position = this.GetChestPoint(this.m_ControllerChestOffset);
            Quaternion tilt = Quaternion.AngleAxis(
                -this.m_ScreenTiltTowardPlayer,
                character.right
            );
            Quaternion controllerRotation = Quaternion.LookRotation(
                tilt * character.up,
                tilt * character.forward
            );
            float weight = Mathf.SmoothStep(0f, 1f, this.m_PresentationWeight) *
                           this.m_HandIKWeight;
            float rotationWeight = weight * this.m_HandRotationWeight;

            this.ApplyHandIK(
                AvatarIKGoal.LeftHand,
                AvatarIKHint.LeftElbow,
                this.GetGripPoint(
                    position,
                    controllerRotation,
                    this.m_LeftGripOffset
                ),
                character.rotation * Quaternion.Euler(this.m_LeftHandEuler),
                this.GetChestPoint(this.m_LeftElbowChestOffset),
                weight,
                rotationWeight
            );
            this.ApplyHandIK(
                AvatarIKGoal.RightHand,
                AvatarIKHint.RightElbow,
                this.GetGripPoint(
                    position,
                    controllerRotation,
                    this.m_RightGripOffset
                ),
                character.rotation * Quaternion.Euler(this.m_RightHandEuler),
                this.GetChestPoint(this.m_RightElbowChestOffset),
                weight,
                rotationWeight
            );
        }

        private void ApplyHandIK(
            AvatarIKGoal hand,
            AvatarIKHint elbow,
            Vector3 handPosition,
            Quaternion handRotation,
            Vector3 elbowPosition,
            float positionWeight,
            float rotationWeight)
        {
            this.m_Animator.SetIKPositionWeight(hand, positionWeight);
            this.m_Animator.SetIKRotationWeight(hand, rotationWeight);
            this.m_Animator.SetIKHintPositionWeight(elbow, positionWeight);
            this.m_Animator.SetIKPosition(hand, handPosition);
            this.m_Animator.SetIKRotation(hand, handRotation);
            this.m_Animator.SetIKHintPosition(elbow, elbowPosition);
        }

        private Vector3 GetGripPoint(
            Vector3 controllerPosition,
            Quaternion controllerRotation,
            Vector3 gripOffset)
        {
            // The imported controller's local X axis is mirrored relative to the
            // character. Keep the visual orientation, but resolve grip width in
            // character space so LeftHand always remains on the character's left.
            Vector3 depthAndHeight = controllerRotation * new Vector3(
                0f,
                gripOffset.y,
                gripOffset.z
            );
            return controllerPosition +
                   this.m_Player.transform.right * gripOffset.x +
                   depthAndHeight;
        }

        private Vector3 GetChestPoint(Vector3 offset)
        {
            Transform character = this.m_Player.transform;
            return this.m_Chest.position +
                   character.right * offset.x +
                   character.up * offset.y +
                   character.forward * offset.z;
        }

        private bool ResolvePlayer()
        {
            if (this.m_Player == null)
                this.m_Player = this.GetComponentInParent<Character>();
            return this.m_Player != null;
        }

        private void HideImmediate()
        {
            this.m_TargetPresented = false;
            this.m_PresentationWeight = 0f;
            if (this.m_ControllerInstance != null)
                this.m_ControllerInstance.SetActive(false);
            this.UnregisterAnimatorIK();
        }

        private void WarnMissingSetup()
        {
            if (this.m_WarnedMissingSetup) return;
            this.m_WarnedMissingSetup = true;
            Debug.LogWarning(
                "Drone controller presentation requires a humanoid Player and " +
                "the Drone Controller Handheld prefab.",
                this
            );
        }
    }
}
