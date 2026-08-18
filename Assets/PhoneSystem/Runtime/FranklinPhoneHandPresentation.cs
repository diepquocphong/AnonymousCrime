using System;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Characters.IK;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.PhoneSystem
{
    /// <summary>
    /// Presents the physical phone in the right hand. Unity's Humanoid IK owns the
    /// arm pose, while Humanoid muscles provide a rig-independent grip and thumb tap.
    /// </summary>
    [DefaultExecutionOrder(980)]
    [DisallowMultipleComponent]
    public sealed class FranklinPhoneHandPresentation : MonoBehaviour
    {
        private static readonly int BaseColorFactor =
            Shader.PropertyToID("_baseColorFactor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EmissiveFactor =
            Shader.PropertyToID("_emissiveFactor");
        private static readonly int EmissionColor =
            Shader.PropertyToID("_EmissionColor");

        [Header("Physical Phone - Right Hand")]
        [SerializeField] private GameObject m_PhonePrefab;
        [Tooltip("Phone rotation in Character space while fully presented.")]
        [SerializeField] private Vector3 m_PhoneWorldEuler = Vector3.zero;
        [Tooltip("Phone pivot offset from the right wrist in Character right/up/forward axes.")]
        [SerializeField] private Vector3 m_PhoneHandOffset =
            new(-0.035f, 0.055f, 0.015f);
        [Tooltip("Fine position adjustment from the right-hand socket in local axes.")]
        [SerializeField] private Vector3 m_PhonePosition = Vector3.zero;
        [Tooltip("Fine rotation adjustment from the right-hand socket in local Euler angles.")]
        [SerializeField] private Vector3 m_PhoneRotation = Vector3.zero;
        [SerializeField] private Vector3 m_PhoneScale =
            new(0.105f, 0.105f, 0.105f);

        [Header("Phone Draw - Back Pocket IK")]
        [InspectorName("Back Pocket Hand Position")]
        [Tooltip(
            "Hidden draw pose offset from Chest in Character right/up/forward " +
            "axes. A negative Z places the hand behind the Player's hip/butt."
        )]
        [SerializeField] private Vector3 m_PocketChestOffset =
            new(0.25f, -0.44f, -0.18f);
        [InspectorName("Back Pocket Hand Rotation")]
        [SerializeField] private Vector3 m_PocketHandEuler =
            new(-25f, 10f, -105f);
        [InspectorName("Back Pocket Elbow Position")]
        [Tooltip(
            "Right-elbow hint while reaching behind the hip. Kept separate " +
            "from the holding elbow so the arm bends naturally around the body."
        )]
        [SerializeField] private Vector3 m_PocketElbowChestOffset =
            new(0.40f, -0.22f, -0.10f);

        [Header("Right Hand Hold IK")]
        [Tooltip("Visible hold pose offset from Chest in Character right/up/forward axes.")]
        [SerializeField] private Vector3 m_RightHandChestOffset =
            new(0.17f, -0.15f, 0.30f);
        [SerializeField] private Vector3 m_RightHandEuler =
            new(-16f, 8f, -76f);
        [Tooltip("Right elbow hint offset from Chest in Character right/up/forward axes.")]
        [SerializeField] private Vector3 m_RightElbowChestOffset =
            new(0.42f, -0.10f, 0.08f);
        [SerializeField, Range(0f, 1f)] private float m_HandIKWeight = 1f;

        [Header("Selfie Pose")]
        [Tooltip("Raised selfie position from Chest in Character right/up/forward axes.")]
        [SerializeField] private Vector3 m_SelfieHandChestOffset =
            new(0.27f, 0.12f, 0.34f);
        [SerializeField] private Vector3 m_SelfieHandEuler =
            new(-20f, 10f, -92f);
        [SerializeField] private Vector3 m_SelfieElbowChestOffset =
            new(0.46f, 0.02f, 0.10f);
        [SerializeField, Range(0.1f, 1f)] private float m_SelfieBlendDuration = 0.32f;

        [Header("Right Hand Humanoid Grip")]
        [Tooltip("Negative Stretched muscle values curl the fingers around the phone.")]
        [SerializeField, Range(-1f, 1f)] private float m_IndexGrip = -0.55f;
        [SerializeField, Range(-1f, 1f)] private float m_MiddleGrip = -0.68f;
        [SerializeField, Range(-1f, 1f)] private float m_RingGrip = -0.74f;
        [SerializeField, Range(-1f, 1f)] private float m_LittleGrip = -0.78f;
        [SerializeField, Range(-1f, 1f)] private float m_ThumbRestCurl = -0.18f;
        [SerializeField, Range(-1f, 1f)] private float m_ThumbRestSpread = 0.12f;
        [SerializeField, Range(-1f, 1f)] private float m_ThumbTapCurl = -0.62f;
        [SerializeField, Range(-1f, 1f)] private float m_ThumbTapSpread = -0.16f;

        [Header("Timing and Blend")]
        [SerializeField, Range(0.35f, 1.5f)] private float m_DrawDuration = 0.72f;
        [SerializeField, Range(0.25f, 1.25f)] private float m_StoreDuration = 0.58f;
        [InspectorName("Back Pocket Reach Threshold")]
        [Tooltip(
            "Normalized point in the draw animation where the hidden hand has " +
            "reached the back pocket, grips the phone and starts bringing it forward."
        )]
        [SerializeField, Range(0.08f, 0.45f)]
        private float m_PhoneRevealThreshold = 0.38f;
        [SerializeField, Range(0.25f, 0.85f)] private float m_ScreenOnThreshold = 0.52f;
        [SerializeField, Range(0.12f, 0.4f)] private float m_TapDuration = 0.22f;
        [SerializeField, Range(0f, 0.02f)] private float m_IdleSwayAmount = 0.004f;
        [SerializeField, Range(0.1f, 3f)] private float m_IdleSwaySpeed = 0.75f;

        [Header("Screen Light")]
        [SerializeField] private Color m_ScreenOnColor =
            new(0.72f, 0.98f, 1f, 1f);
        [SerializeField] private Color m_ScreenEmission =
            new(0.12f, 0.82f, 0.95f, 1f);
        [SerializeField] private Color m_ScreenOffColor =
            new(0.008f, 0.012f, 0.016f, 1f);

        private Character m_Character;
        private Animator m_Animator;
        private Transform m_Chest;
        private Transform m_RightHand;
        private Transform m_PhoneSocket;

        private HumanPoseHandler m_HumanPoseHandler;
        private HumanPose m_HumanPose;
        private int[] m_RightThumbMuscles;
        private int[] m_RightIndexMuscles;
        private int[] m_RightMiddleMuscles;
        private int[] m_RightRingMuscles;
        private int[] m_RightLittleMuscles;

        private GameObject m_PhoneInstance;
        private Renderer m_ScreenRenderer;
        private MaterialPropertyBlock m_ScreenProperties;
        private RigLookTo m_LookRig;
        private ILookTo m_LookTarget;

        private float m_PresentationWeight;
        private float m_SelfieWeight;
        private float m_TapElapsed = -1f;
        private bool m_TargetOpen;
        private bool m_SelfieMode;
        private bool m_PhoneModelHidden;
        private bool m_TapPending;
        private bool m_ScreenIsOn;
        private bool m_IKRegistered;
        private bool m_OwnsRightArmBusyMask;
        private bool m_HasWarnedMissingSetup;
        private bool m_HasNotifiedPhoneStored = true;
        private Transform m_SelfieLookTarget;

        public event Action EventPhoneStored;

        public bool IsPresented => this.m_TargetOpen;
        public bool IsConfigured => this.m_PhonePrefab != null;
        public GameObject PhoneInstance => this.m_PhoneInstance;

        /// <summary>Raises the phone and redirects head-look toward the selfie camera.</summary>
        public void SetSelfieMode(bool active, Transform lookTarget = null)
        {
            if (this.m_SelfieMode == active &&
                this.m_SelfieLookTarget == lookTarget)
            {
                return;
            }

            this.m_SelfieMode = active;
            this.m_SelfieLookTarget = active ? lookTarget : null;
            this.StopCharacterLook();
            if (this.m_TargetOpen &&
                this.m_PresentationWeight >= this.m_ScreenOnThreshold)
            {
                this.StartCharacterLook();
            }
        }

        public void Configure(GameObject phonePrefab)
        {
            if (phonePrefab != null) this.m_PhonePrefab = phonePrefab;
        }

        /// <summary>
        /// Temporarily hides only the physical phone mesh without interrupting
        /// the hand pose, draw/store animation or screen state.
        /// </summary>
        public void SetPhoneModelHidden(bool hidden)
        {
            if (this.m_PhoneModelHidden == hidden) return;
            this.m_PhoneModelHidden = hidden;
            this.RefreshPhoneModelVisibility();
        }

        public void SetPhoneOpen(bool open, bool immediate = false)
        {
            this.m_TargetOpen = open;

            if (open)
            {
                this.m_HasNotifiedPhoneStored = false;
                if (this.EnsureReady())
                {
                    this.AcquireRightArmBusyMask();
                    if (immediate) this.m_PresentationWeight = 1f;
                    this.PlacePhoneAtRightHand();
                    this.RefreshPhoneModelVisibility();
                    this.SetScreenLit(
                        immediate || this.m_PresentationWeight >= this.m_ScreenOnThreshold
                    );
                    if (immediate ||
                        this.m_PresentationWeight >= this.m_ScreenOnThreshold)
                    {
                        this.StartCharacterLook();
                    }
                }
            }
            else
            {
                this.m_TapPending = false;
                this.SetScreenLit(false);
                this.StopCharacterLook();
                if (immediate)
                {
                    this.m_TapElapsed = -1f;
                    this.m_PresentationWeight = 0f;
                    if (this.m_PhoneInstance != null)
                        this.m_PhoneInstance.SetActive(false);
                    this.ReleaseRightArmBusyMask();
                    this.NotifyPhoneStored();
                }
            }
        }

        /// <summary>Plays one right-thumb press for any Phone UI button.</summary>
        public void NotifyTap()
        {
            if (!this.m_TargetOpen) return;
            if (this.m_PresentationWeight < 0.55f)
            {
                this.m_TapPending = true;
                return;
            }

            this.m_TapElapsed = 0f;
        }

        /// <summary>
        /// Refreshes the physical phone immediately after Humanoid IK. The selfie
        /// Camera Shot calls this from GC2 Main Camera's before-update event so the
        /// shot and the RenderTexture lens use the same hand pose in the same frame.
        /// </summary>
        public void SyncPhoneTransform()
        {
            if (this.m_PresentationWeight <= 0.0001f) return;
            if (!this.EnsureReady()) return;
            this.PlacePhoneAtRightHand();
        }

        private void Update()
        {
            if (this.m_TargetOpen && !this.EnsureReady()) return;

            float selfieDuration = Mathf.Max(0.01f, this.m_SelfieBlendDuration);
            this.m_SelfieWeight = Mathf.MoveTowards(
                this.m_SelfieWeight,
                this.m_SelfieMode ? 1f : 0f,
                Time.unscaledDeltaTime / selfieDuration
            );

            float duration = this.m_TargetOpen
                ? Mathf.Max(0.01f, this.m_DrawDuration)
                : Mathf.Max(0.01f, this.m_StoreDuration);
            float target = this.m_TargetOpen ? 1f : 0f;
            this.m_PresentationWeight = Mathf.MoveTowards(
                this.m_PresentationWeight,
                target,
                Time.unscaledDeltaTime / duration
            );

            if (this.m_TargetOpen && this.m_PhoneInstance != null)
            {
                this.RefreshPhoneModelVisibility();
                if (!this.m_ScreenIsOn &&
                    this.m_PresentationWeight >= this.m_ScreenOnThreshold)
                {
                    this.SetScreenLit(true);
                    this.StartCharacterLook();
                }

                if (this.m_TapPending && this.m_PresentationWeight >= 0.55f)
                {
                    this.m_TapPending = false;
                    this.m_TapElapsed = 0f;
                }
            }
            else if (!this.m_TargetOpen && this.m_PhoneInstance != null &&
                     this.m_PresentationWeight <= this.m_PhoneRevealThreshold)
            {
                this.m_PhoneInstance.SetActive(false);
                this.ReleaseRightArmBusyMask();
            }

            if (!this.m_TargetOpen && this.m_PresentationWeight <= 0.0001f)
                this.NotifyPhoneStored();

            if (this.m_TapElapsed >= 0f)
            {
                this.m_TapElapsed += Time.unscaledDeltaTime;
                if (this.m_TapElapsed >= this.m_TapDuration)
                    this.m_TapElapsed = -1f;
            }
        }

        private void RefreshPhoneModelVisibility()
        {
            if (this.m_PhoneInstance == null) return;

            bool visible = !this.m_PhoneModelHidden &&
                           this.m_PresentationWeight >=
                           this.m_PhoneRevealThreshold;
            if (this.m_PhoneInstance.activeSelf != visible)
                this.m_PhoneInstance.SetActive(visible);
        }

        private void LateUpdate()
        {
            this.SyncPhoneTransform();
        }

        private void OnDisable()
        {
            this.SetPhoneOpen(false, true);
            this.UnregisterAnimatorIK();
        }

        private void OnDestroy()
        {
            this.UnregisterAnimatorIK();
            this.StopCharacterLook();
            this.ReleaseRightArmBusyMask();
            this.DisposeHumanPoseHandler();
            this.DestroyPhoneSocket();
            if (this.m_PhoneInstance != null) Destroy(this.m_PhoneInstance);
        }

        private void NotifyPhoneStored()
        {
            if (this.m_TargetOpen || this.m_HasNotifiedPhoneStored) return;

            this.m_HasNotifiedPhoneStored = true;
            this.EventPhoneStored?.Invoke();
        }

        private bool EnsureReady()
        {
            Character character = ShortcutPlayer.Get<Character>();
            Animator animator = character?.Animim?.Animator;
            if (animator == null && character != null)
                animator = character.GetComponentInChildren<Animator>(true);

            if (character == null || animator == null || !animator.isHuman ||
                animator.avatar == null || !animator.avatar.isHuman ||
                this.m_PhonePrefab == null)
            {
                if (!this.m_HasWarnedMissingSetup && this.m_TargetOpen)
                {
                    this.m_HasWarnedMissingSetup = true;
                    Debug.LogWarning(
                        "Phone hand presentation requires the Player humanoid Animator " +
                        "and Phone_A1_LP prefab.",
                        this
                    );
                }
                return false;
            }

            bool animatorChanged = this.m_Character != character ||
                                   this.m_Animator != animator;
            if (animatorChanged || this.m_RightHand == null ||
                this.m_HumanPoseHandler == null)
            {
                this.StopCharacterLook();
                this.UnregisterAnimatorIK();
                this.ReleaseRightArmBusyMask();
                this.DisposeHumanPoseHandler();
                this.DestroyPhoneSocket();
                this.m_Character = character;
                this.m_Animator = animator;
                this.ResolveHumanoid(animator);
            }

            if (this.m_Chest == null || this.m_RightHand == null ||
                this.m_HumanPoseHandler == null)
            {
                return false;
            }

            this.RegisterAnimatorIK();
            this.EnsurePhoneInstance();
            if (this.m_TargetOpen) this.AcquireRightArmBusyMask();
            if (this.m_TargetOpen &&
                this.m_PresentationWeight >= this.m_ScreenOnThreshold)
            {
                this.StartCharacterLook();
            }
            return this.m_PhoneInstance != null;
        }

        private void ResolveHumanoid(Animator animator)
        {
            this.m_Chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                           animator.GetBoneTransform(HumanBodyBones.Chest) ??
                           animator.GetBoneTransform(HumanBodyBones.Spine);
            this.m_RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            this.m_HumanPoseHandler = new HumanPoseHandler(
                animator.avatar,
                animator.transform
            );
            this.m_HumanPose = new HumanPose
            {
                muscles = new float[HumanTrait.MuscleCount]
            };
            this.BuildRightHandMuscleLookup();
            this.EnsurePhoneSocket();
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
                this.m_Character.Animim.EventOnAnimatorIK -= this.OnAnimatorIK;
            this.m_IKRegistered = false;
        }

        private void AcquireRightArmBusyMask()
        {
            if (this.m_OwnsRightArmBusyMask || this.m_Character?.Busy == null)
                return;
            if (this.m_Character.Busy.IsArmRightBusy) return;

            this.m_Character.Busy.MakeArmRightBusy();
            this.m_OwnsRightArmBusyMask = true;
        }

        private void ReleaseRightArmBusyMask()
        {
            if (!this.m_OwnsRightArmBusyMask) return;
            if (this.m_Character?.Busy != null)
                this.m_Character.Busy.RemoveArmRightBusy();
            this.m_OwnsRightArmBusyMask = false;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (this.m_Animator == null || this.m_Character == null ||
                this.m_Chest == null)
            {
                return;
            }

            float pocketWeight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0f,
                    this.m_PhoneRevealThreshold,
                    this.m_PresentationWeight
                )
            );
            float ikWeight = pocketWeight * this.m_HandIKWeight;

            this.m_Animator.SetIKPositionWeight(AvatarIKGoal.RightHand, ikWeight);
            this.m_Animator.SetIKRotationWeight(AvatarIKGoal.RightHand, ikWeight);
            this.m_Animator.SetIKHintPositionWeight(
                AvatarIKHint.RightElbow,
                ikWeight
            );

            if (ikWeight <= 0f) return;

            float raiseWeight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    this.m_PhoneRevealThreshold,
                    1f,
                    this.m_PresentationWeight
                )
            );
            Vector3 pocketTarget = this.GetChestPoint(this.m_PocketChestOffset);
            Vector3 regularHoldTarget = this.GetChestPoint(
                this.m_RightHandChestOffset
            );
            Vector3 selfieHoldTarget = this.GetChestPoint(
                this.m_SelfieHandChestOffset
            );
            Vector3 holdTarget = Vector3.Lerp(
                regularHoldTarget,
                selfieHoldTarget,
                this.m_SelfieWeight
            ) + this.GetIdleSway(raiseWeight);
            Vector3 handTarget = Vector3.Lerp(
                pocketTarget,
                holdTarget,
                raiseWeight
            );

            Transform character = this.m_Character.transform;
            Quaternion pocketRotation = character.rotation *
                                        Quaternion.Euler(this.m_PocketHandEuler);
            Quaternion holdRotation = character.rotation *
                                      Quaternion.Euler(this.m_RightHandEuler);
            Quaternion selfieRotation = character.rotation *
                                        Quaternion.Euler(this.m_SelfieHandEuler);
            holdRotation = Quaternion.Slerp(
                holdRotation,
                selfieRotation,
                this.m_SelfieWeight
            );
            Quaternion handRotation = Quaternion.Slerp(
                pocketRotation,
                holdRotation,
                raiseWeight
            );

            this.ApplyRightHandMusclePose(pocketWeight, this.GetTapWeight());
            this.m_Animator.SetIKPosition(AvatarIKGoal.RightHand, handTarget);
            this.m_Animator.SetIKRotation(AvatarIKGoal.RightHand, handRotation);
            Vector3 holdElbowOffset = Vector3.Lerp(
                this.m_RightElbowChestOffset,
                this.m_SelfieElbowChestOffset,
                this.m_SelfieWeight
            );
            Vector3 elbowOffset = Vector3.Lerp(
                this.m_PocketElbowChestOffset,
                holdElbowOffset,
                raiseWeight
            );
            this.m_Animator.SetIKHintPosition(
                AvatarIKHint.RightElbow,
                this.GetChestPoint(elbowOffset)
            );
        }

        private void EnsurePhoneInstance()
        {
            if (this.m_PhoneInstance != null) return;

            this.m_PhoneInstance = Instantiate(this.m_PhonePrefab);
            this.m_PhoneInstance.name = "Franklin Right-Hand Phone";
            this.m_PhoneInstance.transform.SetParent(null, false);
            this.m_PhoneInstance.transform.localScale = this.m_PhoneScale;
            DontDestroyOnLoad(this.m_PhoneInstance);

            foreach (Animator animator in
                     this.m_PhoneInstance.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }
            foreach (Collider phoneCollider in
                     this.m_PhoneInstance.GetComponentsInChildren<Collider>(true))
            {
                phoneCollider.enabled = false;
            }

            Transform screen = FindChildRecursive(
                this.m_PhoneInstance.transform,
                "ScrOn"
            );
            this.m_ScreenRenderer = screen != null
                ? screen.GetComponent<Renderer>()
                : null;
            this.m_ScreenProperties = new MaterialPropertyBlock();
            this.SetScreenLit(false);
            this.m_PhoneInstance.SetActive(false);
        }

        private void PlacePhoneAtRightHand()
        {
            if (this.m_PhoneInstance == null || this.m_RightHand == null) return;

            this.EnsurePhoneSocket();
            if (this.m_PhoneSocket == null) return;

            this.m_PhoneInstance.transform.SetPositionAndRotation(
                this.m_PhoneSocket.TransformPoint(this.m_PhonePosition),
                this.m_PhoneSocket.rotation *
                Quaternion.Euler(this.m_PhoneRotation)
            );
            this.m_PhoneInstance.transform.localScale = this.m_PhoneScale;
        }

        private void EnsurePhoneSocket()
        {
            if (this.m_PhoneSocket != null || this.m_RightHand == null ||
                this.m_Character == null)
            {
                return;
            }

            GameObject socket = new GameObject("Franklin Right-Hand Phone Socket");
            socket.hideFlags = HideFlags.HideInHierarchy;
            this.m_PhoneSocket = socket.transform;
            this.m_PhoneSocket.SetParent(this.m_RightHand, false);

            Transform character = this.m_Character.transform;
            Quaternion referenceHandRotation = character.rotation *
                Quaternion.Euler(this.m_RightHandEuler);
            Quaternion referencePhoneRotation = character.rotation *
                Quaternion.Euler(this.m_PhoneWorldEuler);
            Vector3 worldOffset =
                character.right * this.m_PhoneHandOffset.x +
                character.up * this.m_PhoneHandOffset.y +
                character.forward * this.m_PhoneHandOffset.z;

            Vector3 localOffset = Quaternion.Inverse(referenceHandRotation) *
                                  worldOffset;
            Vector3 handScale = this.m_RightHand.lossyScale;
            localOffset.x /= SafeScale(handScale.x);
            localOffset.y /= SafeScale(handScale.y);
            localOffset.z /= SafeScale(handScale.z);

            this.m_PhoneSocket.localPosition = localOffset;
            this.m_PhoneSocket.localRotation =
                Quaternion.Inverse(referenceHandRotation) * referencePhoneRotation;
            this.m_PhoneSocket.localScale = Vector3.one;
        }

        private void DestroyPhoneSocket()
        {
            if (this.m_PhoneSocket == null) return;
            Destroy(this.m_PhoneSocket.gameObject);
            this.m_PhoneSocket = null;
        }

        private void DisposeHumanPoseHandler()
        {
            if (this.m_HumanPoseHandler != null)
                this.m_HumanPoseHandler.Dispose();
            this.m_HumanPoseHandler = null;
            this.m_HumanPose = new HumanPose();
        }

        private Vector3 GetChestPoint(Vector3 offset)
        {
            Transform character = this.m_Character.transform;
            return this.m_Chest.position +
                   character.right * offset.x +
                   character.up * offset.y +
                   character.forward * offset.z;
        }

        private Vector3 GetIdleSway(float weight)
        {
            if (weight <= 0f || this.m_IdleSwayAmount <= 0f) return Vector3.zero;

            float phase = Time.unscaledTime * this.m_IdleSwaySpeed * Mathf.PI * 2f;
            Vector3 localOffset = new Vector3(
                Mathf.Sin(phase * 0.61f) * this.m_IdleSwayAmount * 0.35f,
                Mathf.Sin(phase) * this.m_IdleSwayAmount,
                Mathf.Cos(phase * 0.47f) * this.m_IdleSwayAmount * 0.4f
            ) * weight;
            Transform character = this.m_Character.transform;
            return character.right * localOffset.x +
                   character.up * localOffset.y +
                   character.forward * localOffset.z;
        }

        private float GetTapWeight()
        {
            if (this.m_TapElapsed < 0f || this.m_TapDuration <= 0f) return 0f;
            float progress = Mathf.Clamp01(this.m_TapElapsed / this.m_TapDuration);
            return Mathf.Sin(progress * Mathf.PI);
        }

        private void BuildRightHandMuscleLookup()
        {
            this.m_RightThumbMuscles = GetFingerMuscles("Thumb");
            this.m_RightIndexMuscles = GetFingerMuscles("Index");
            this.m_RightMiddleMuscles = GetFingerMuscles("Middle");
            this.m_RightRingMuscles = GetFingerMuscles("Ring");
            this.m_RightLittleMuscles = GetFingerMuscles("Little");
        }

        private void ApplyRightHandMusclePose(float poseWeight, float tapWeight)
        {
            if (this.m_HumanPoseHandler == null || poseWeight <= 0f) return;

            this.m_HumanPoseHandler.GetHumanPose(ref this.m_HumanPose);
            float[] muscles = this.m_HumanPose.muscles;
            if (muscles == null || muscles.Length == 0) return;

            float weight = Mathf.Clamp01(poseWeight);
            ApplyFingerGrip(
                muscles,
                this.m_RightIndexMuscles,
                this.m_IndexGrip,
                weight
            );
            ApplyFingerGrip(
                muscles,
                this.m_RightMiddleMuscles,
                this.m_MiddleGrip,
                weight
            );
            ApplyFingerGrip(
                muscles,
                this.m_RightRingMuscles,
                this.m_RingGrip,
                weight
            );
            ApplyFingerGrip(
                muscles,
                this.m_RightLittleMuscles,
                this.m_LittleGrip,
                weight
            );

            float thumbCurl = Mathf.Lerp(
                this.m_ThumbRestCurl,
                this.m_ThumbTapCurl,
                tapWeight
            );
            float thumbSpread = Mathf.Lerp(
                this.m_ThumbRestSpread,
                this.m_ThumbTapSpread,
                tapWeight
            );
            ApplyThumbPose(
                muscles,
                this.m_RightThumbMuscles,
                thumbCurl,
                thumbSpread,
                weight
            );

            this.m_HumanPoseHandler.SetHumanPose(ref this.m_HumanPose);
        }

        private static void ApplyFingerGrip(
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
                weight
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

        private void SetScreenLit(bool lit)
        {
            this.m_ScreenIsOn = lit;
            if (this.m_ScreenRenderer == null) return;

            this.m_ScreenRenderer.GetPropertyBlock(this.m_ScreenProperties);
            Color baseColor = lit ? this.m_ScreenOnColor : this.m_ScreenOffColor;
            Color emission = lit ? this.m_ScreenEmission : Color.black;
            this.m_ScreenProperties.SetColor(BaseColorFactor, baseColor);
            this.m_ScreenProperties.SetColor(BaseColor, baseColor);
            this.m_ScreenProperties.SetColor(ColorProperty, baseColor);
            this.m_ScreenProperties.SetColor(EmissiveFactor, emission);
            this.m_ScreenProperties.SetColor(EmissionColor, emission);
            this.m_ScreenRenderer.SetPropertyBlock(this.m_ScreenProperties);
        }

        private void StartCharacterLook()
        {
            if (this.m_Character == null || this.m_PhoneInstance == null ||
                this.m_Character.IK == null || this.m_LookTarget != null)
            {
                return;
            }

            this.m_LookRig = this.m_Character.IK.RequireRig<RigLookTo>();
            if (this.m_LookRig == null) return;

            Transform lookTransform = this.m_SelfieMode &&
                                      this.m_SelfieLookTarget != null
                ? this.m_SelfieLookTarget
                : this.m_PhoneInstance.transform;
            this.m_LookTarget = new LookToTransform(
                -100,
                lookTransform,
                Vector3.zero
            );
            this.m_LookRig.SetTarget(this.m_LookTarget);
        }

        private void StopCharacterLook()
        {
            if (this.m_LookRig != null && this.m_LookTarget != null)
                this.m_LookRig.RemoveTarget(this.m_LookTarget);
            this.m_LookTarget = null;
            this.m_LookRig = null;
        }

        private static float SafeScale(float scale)
        {
            return Mathf.Abs(scale) > 0.0001f ? scale : 1f;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null) return null;
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; ++i)
            {
                Transform result = FindChildRecursive(root.GetChild(i), childName);
                if (result != null) return result;
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_DrawDuration = Mathf.Max(0.35f, this.m_DrawDuration);
            this.m_StoreDuration = Mathf.Max(0.25f, this.m_StoreDuration);
            this.m_TapDuration = Mathf.Max(0.12f, this.m_TapDuration);
            this.m_PhoneRevealThreshold = Mathf.Clamp(
                this.m_PhoneRevealThreshold,
                0.08f,
                0.45f
            );
            this.m_ScreenOnThreshold = Mathf.Clamp(
                this.m_ScreenOnThreshold,
                0.25f,
                0.85f
            );
            this.m_HandIKWeight = Mathf.Clamp01(this.m_HandIKWeight);
            this.m_SelfieBlendDuration = Mathf.Clamp(
                this.m_SelfieBlendDuration,
                0.1f,
                1f
            );
            this.m_IdleSwayAmount = Mathf.Clamp(this.m_IdleSwayAmount, 0f, 0.02f);
            this.m_IdleSwaySpeed = Mathf.Clamp(this.m_IdleSwaySpeed, 0.1f, 3f);
        }
#endif
    }
}
