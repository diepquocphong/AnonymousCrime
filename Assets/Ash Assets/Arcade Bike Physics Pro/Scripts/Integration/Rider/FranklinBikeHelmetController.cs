using System;
using System.Collections;
using GameCreator.Runtime.Characters;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Owns the Player helmet and overlays a procedural two-hand reach after the
    /// Animator/handlebar IK. The helmet is transferred between RightHand and Head
    /// at the peak of the motion, so no extra Animator state is required.
    /// </summary>
    [DefaultExecutionOrder(950)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
    public sealed class FranklinBikeHelmetController : MonoBehaviour
    {
        private const float MOBILE_IDLE_POSE_CHECK_SECONDS = 0.1f;
        private const float DESKTOP_IDLE_POSE_CHECK_SECONDS = 1f / 30f;

        [Header("Helmet Asset")]
        [SerializeField] private GameObject m_HelmetPrefab;

        [Header("Helmet On Head - Live Editable")]
        [SerializeField, InspectorName("Position")]
        [Tooltip("Local position của Helmet so với bone Head.")]
        private Vector3 m_HeadLocalPosition = new(0f, 0.075f, 0.015f);
        [SerializeField, InspectorName("Rotation")]
        [Tooltip("Local Euler rotation của Helmet trên bone Head.")]
        private Vector3 m_HeadLocalEuler = new(0f, 180f, 0f);
        [SerializeField, InspectorName("Scale")]
        private Vector3 m_HeadLocalScale = Vector3.one;

        [Header("Helmet In Right Hand - Live Editable")]
        [SerializeField, InspectorName("Position")]
        [Tooltip("Local position của Helmet khi Player đang cầm bằng tay phải.")]
        private Vector3 m_HandLocalPosition = new(0.025f, 0.015f, 0.085f);
        [SerializeField, InspectorName("Rotation")]
        [Tooltip("Local Euler rotation của Helmet khi nằm trong tay phải.")]
        private Vector3 m_HandLocalEuler = new(0f, 90f, 90f);
        [SerializeField, InspectorName("Scale")]
        private Vector3 m_HandLocalScale = Vector3.one;

        [Header("Procedural Equip / Remove")]
        [SerializeField, Range(0.45f, 2f)] private float m_TransitionDuration = 1.05f;
        [SerializeField, Range(0.2f, 0.8f)] private float m_EquipTransferTime = 0.58f;
        [SerializeField, Range(0.2f, 0.8f)] private float m_RemoveTransferTime = 0.44f;
        [SerializeField] private Vector3 m_RightHandHeadOffset =
            new(0.15f, -0.015f, 0.105f);
        [SerializeField] private Vector3 m_LeftHandHeadOffset =
            new(-0.15f, -0.015f, 0.105f);
        [SerializeField] private Vector3 m_RightHandHeadEuler =
            new(5f, -15f, -80f);
        [SerializeField] private Vector3 m_LeftHandHeadEuler =
            new(5f, 15f, 80f);
        [SerializeField, Range(0.1f, 1f)] private float m_UpperArmReach = 0.82f;
        [SerializeField, Range(0.1f, 1f)] private float m_LowerArmReach = 0.95f;

        private Animator m_Animator;
        private Character m_Character;
        private Transform m_Head;
        private Transform m_RightUpperArm;
        private Transform m_RightLowerArm;
        private Transform m_RightHand;
        private Transform m_LeftUpperArm;
        private Transform m_LeftLowerArm;
        private Transform m_LeftHand;
        private GameObject m_HelmetInstance;
        private Coroutine m_Transition;
        private float m_ReachWeight;
        private bool m_IsEquipped;
        private bool m_HasWarnedMissingSetup;
        private Transform m_LastHelmetPoseParent;
        private Vector3 m_LastHelmetPosePosition;
        private Vector3 m_LastHelmetPoseEuler;
        private Vector3 m_LastHelmetPoseScale;
        private bool m_HasAppliedHelmetPose;
        private float m_NextIdlePoseCheck;

        public bool IsEquipped => this.m_IsEquipped;
        public bool IsTransitioning => this.m_Transition != null;
        public GameObject HelmetInstance => this.m_HelmetInstance;
        public bool IsProtectingHead =>
            this.m_IsEquipped &&
            this.m_HelmetInstance != null &&
            this.m_HelmetInstance.activeInHierarchy &&
            this.m_Head != null &&
            this.m_HelmetInstance.transform.parent == this.m_Head;
        public Vector3 HeadPosition
        {
            get => this.m_HeadLocalPosition;
            set
            {
                this.m_HeadLocalPosition = value;
                this.ApplyCurrentHelmetPose();
            }
        }
        public Vector3 HeadRotation
        {
            get => this.m_HeadLocalEuler;
            set
            {
                this.m_HeadLocalEuler = value;
                this.ApplyCurrentHelmetPose();
            }
        }
        public Vector3 RightHandPosition
        {
            get => this.m_HandLocalPosition;
            set
            {
                this.m_HandLocalPosition = value;
                this.ApplyCurrentHelmetPose();
            }
        }
        public Vector3 RightHandRotation
        {
            get => this.m_HandLocalEuler;
            set
            {
                this.m_HandLocalEuler = value;
                this.ApplyCurrentHelmetPose();
            }
        }
        public event Action<bool> EventHelmetChanged;

        private void Awake()
        {
            this.m_Character = this.GetComponent<Character>();
            this.ResolveBones();
        }

        private void OnDisable()
        {
            if (this.m_Transition != null)
            {
                this.StopCoroutine(this.m_Transition);
                this.m_Transition = null;
            }

            this.m_ReachWeight = 0f;
            if (this.m_HelmetInstance == null) return;
            if (this.m_IsEquipped && this.ResolveBones()) this.AttachHelmetToHead();
            else this.m_HelmetInstance.SetActive(false);
        }

        private void OnDestroy()
        {
            if (this.m_HelmetInstance != null) Destroy(this.m_HelmetInstance);
        }

        private void LateUpdate()
        {
            bool helmetVisible = this.m_HelmetInstance != null &&
                                 this.m_HelmetInstance.activeSelf;
            if (!helmetVisible && this.m_ReachWeight <= 0.0001f) return;

            bool transitionActive = this.m_ReachWeight > 0.0001f;
            if (!transitionActive)
            {
                float now = Time.unscaledTime;
                if (now < this.m_NextIdlePoseCheck) return;
                this.m_NextIdlePoseCheck = now +
                    (Application.isMobilePlatform
                        ? MOBILE_IDLE_POSE_CHECK_SECONDS
                        : DESKTOP_IDLE_POSE_CHECK_SECONDS);
            }
            if (!this.ResolveBones()) return;
            if (helmetVisible && this.HasHelmetPoseChanged())
                this.ApplyCurrentHelmetPose();
            if (this.m_ReachWeight <= 0.0001f) return;

            Vector3 rightTarget = this.m_Head.TransformPoint(this.m_RightHandHeadOffset);
            Vector3 leftTarget = this.m_Head.TransformPoint(this.m_LeftHandHeadOffset);
            Quaternion rightRotation = this.m_Head.rotation *
                                       Quaternion.Euler(this.m_RightHandHeadEuler);
            Quaternion leftRotation = this.m_Head.rotation *
                                      Quaternion.Euler(this.m_LeftHandHeadEuler);

            ApplyArmReach(
                this.m_RightUpperArm,
                this.m_RightLowerArm,
                this.m_RightHand,
                rightTarget,
                rightRotation,
                this.m_ReachWeight,
                this.m_UpperArmReach,
                this.m_LowerArmReach
            );
            ApplyArmReach(
                this.m_LeftUpperArm,
                this.m_LeftLowerArm,
                this.m_LeftHand,
                leftTarget,
                leftRotation,
                this.m_ReachWeight,
                this.m_UpperArmReach,
                this.m_LowerArmReach
            );
        }

        public bool ToggleHelmet()
        {
            if (this.m_Transition != null || !this.EnsureReady()) return false;
            this.m_Transition = this.StartCoroutine(
                this.PlayHelmetTransition(!this.m_IsEquipped)
            );
            return true;
        }

        public bool SetHelmetEquipped(bool equipped, bool immediate = false)
        {
            if (this.m_Transition != null || !this.EnsureReady()) return false;
            if (this.m_IsEquipped == equipped) return true;

            if (!immediate)
            {
                this.m_Transition = this.StartCoroutine(
                    this.PlayHelmetTransition(equipped)
                );
                return true;
            }

            if (equipped)
            {
                this.EnsureHelmetInstance();
                this.AttachHelmetToHead();
                this.m_HelmetInstance.SetActive(true);
            }
            else if (this.m_HelmetInstance != null)
            {
                this.m_HelmetInstance.SetActive(false);
            }

            this.m_IsEquipped = equipped;
            this.EventHelmetChanged?.Invoke(equipped);
            return true;
        }

        /// <summary>
        /// Detaches the equipped Helmet and converts it into a short-lived physics prop.
        /// Returns false when the Helmet is not currently protecting the Head bone.
        /// </summary>
        public bool KnockOffHelmet(Vector3 hitPoint, Vector3 impulse)
        {
            if (!this.ResolveBones() || !this.IsProtectingHead) return false;

            if (this.m_Transition != null)
            {
                this.StopCoroutine(this.m_Transition);
                this.m_Transition = null;
            }

            this.m_ReachWeight = 0f;
            GameObject knockedHelmet = this.m_HelmetInstance;
            this.m_HelmetInstance = null;
            this.m_IsEquipped = false;

            knockedHelmet.transform.SetParent(null, true);
            knockedHelmet.SetActive(true);

            Collider[] helmetColliders =
                knockedHelmet.GetComponentsInChildren<Collider>(true);
            if (helmetColliders.Length == 0)
            {
                BoxCollider box = knockedHelmet.AddComponent<BoxCollider>();
                FitColliderToRenderers(knockedHelmet.transform, box);
                helmetColliders = new Collider[] { box };
            }

            Collider[] characterColliders = this.GetComponentsInChildren<Collider>(true);
            foreach (Collider helmetCollider in helmetColliders)
            {
                helmetCollider.enabled = true;
                helmetCollider.isTrigger = false;
                foreach (Collider characterCollider in characterColliders)
                {
                    if (characterCollider != null)
                        Physics.IgnoreCollision(helmetCollider, characterCollider, true);
                }
            }

            Rigidbody body = knockedHelmet.GetComponent<Rigidbody>();
            if (body == null) body = knockedHelmet.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.detectCollisions = true;
            body.mass = 1.15f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.AddForceAtPosition(impulse, hitPoint, ForceMode.Impulse);
            body.AddTorque(
                UnityEngine.Random.insideUnitSphere * Mathf.Max(1f, impulse.magnitude * 0.3f),
                ForceMode.Impulse
            );

            this.EventHelmetChanged?.Invoke(false);
            Destroy(knockedHelmet, 8f);
            return true;
        }

        private IEnumerator PlayHelmetTransition(bool equip)
        {
            this.EnsureHelmetInstance();
            this.m_HelmetInstance.SetActive(true);

            if (equip) this.AttachHelmetToRightHand();
            else this.AttachHelmetToHead();

            float duration = Mathf.Max(0.45f, this.m_TransitionDuration);
            float transferAt = Mathf.Clamp01(
                equip ? this.m_EquipTransferTime : this.m_RemoveTransferTime
            );
            float elapsed = 0f;
            bool transferred = false;

            while (elapsed < duration && this.isActiveAndEnabled)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                this.m_ReachWeight = Mathf.Sin(progress * Mathf.PI);

                if (!transferred && progress >= transferAt)
                {
                    transferred = true;
                    if (equip) this.AttachHelmetToHead();
                    else this.AttachHelmetToRightHand();
                }

                yield return null;
            }

            this.m_ReachWeight = 0f;
            if (equip)
            {
                this.AttachHelmetToHead();
                this.m_IsEquipped = true;
            }
            else
            {
                this.m_HelmetInstance.SetActive(false);
                this.m_IsEquipped = false;
            }

            this.m_Transition = null;
            this.EventHelmetChanged?.Invoke(this.m_IsEquipped);
        }

        private bool EnsureReady()
        {
            bool ready = this.m_HelmetPrefab != null && this.ResolveBones();
            if (!ready && !this.m_HasWarnedMissingSetup)
            {
                this.m_HasWarnedMissingSetup = true;
                Debug.LogWarning(
                    "Bike helmet requires a Helmet prefab and a Humanoid Animator.",
                    this
                );
            }

            return ready;
        }

        private bool ResolveBones()
        {
            if (this.m_Character == null)
                this.m_Character = this.GetComponent<Character>();
            Animator animator = this.m_Character?.Animim?.Animator;
            if (animator == null) animator = this.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman) return false;

            if (this.m_Animator == animator && this.m_Head != null) return true;
            this.m_Animator = animator;
            this.m_Head = animator.GetBoneTransform(HumanBodyBones.Head);
            this.m_RightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            this.m_RightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            this.m_RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            this.m_LeftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            this.m_LeftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            this.m_LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            bool valid = this.m_Head != null &&
                         this.m_RightUpperArm != null &&
                         this.m_RightLowerArm != null &&
                         this.m_RightHand != null &&
                         this.m_LeftUpperArm != null &&
                         this.m_LeftLowerArm != null &&
                         this.m_LeftHand != null;
            if (valid && this.m_IsEquipped && this.m_HelmetInstance != null &&
                this.m_HelmetInstance.transform.parent != this.m_Head)
            {
                this.AttachHelmetToHead();
            }

            return valid;
        }

        private void EnsureHelmetInstance()
        {
            if (this.m_HelmetInstance != null) return;
            this.m_HelmetInstance = Instantiate(this.m_HelmetPrefab);
            this.m_HelmetInstance.name = "Franklin Equipped Helmet";
            this.m_HelmetInstance.SetActive(false);

            foreach (Collider helmetCollider in
                     this.m_HelmetInstance.GetComponentsInChildren<Collider>(true))
            {
                helmetCollider.enabled = false;
            }
            foreach (Rigidbody helmetBody in
                     this.m_HelmetInstance.GetComponentsInChildren<Rigidbody>(true))
            {
                helmetBody.isKinematic = true;
                helmetBody.detectCollisions = false;
            }
        }

        private void AttachHelmetToHead()
        {
            if (this.m_HelmetInstance == null || this.m_Head == null) return;
            SetLocalPose(
                this.m_HelmetInstance.transform,
                this.m_Head,
                this.m_HeadLocalPosition,
                this.m_HeadLocalEuler,
                this.m_HeadLocalScale
            );
        }

        private void AttachHelmetToRightHand()
        {
            if (this.m_HelmetInstance == null || this.m_RightHand == null) return;
            SetLocalPose(
                this.m_HelmetInstance.transform,
                this.m_RightHand,
                this.m_HandLocalPosition,
                this.m_HandLocalEuler,
                this.m_HandLocalScale
            );
        }

        /// <summary>Reapplies the editable Inspector offsets to the visible Helmet.</summary>
        public void ApplyCurrentHelmetPose()
        {
            if (this.m_HelmetInstance == null || !this.m_HelmetInstance.activeSelf) return;

            Transform helmet = this.m_HelmetInstance.transform;
            if (helmet.parent == this.m_Head)
            {
                ApplyLocalPose(
                    helmet,
                    this.m_HeadLocalPosition,
                    this.m_HeadLocalEuler,
                    this.m_HeadLocalScale
                );
                this.RememberHelmetPose(
                    helmet.parent,
                    this.m_HeadLocalPosition,
                    this.m_HeadLocalEuler,
                    this.m_HeadLocalScale
                );
            }
            else if (helmet.parent == this.m_RightHand)
            {
                ApplyLocalPose(
                    helmet,
                    this.m_HandLocalPosition,
                    this.m_HandLocalEuler,
                    this.m_HandLocalScale
                );
                this.RememberHelmetPose(
                    helmet.parent,
                    this.m_HandLocalPosition,
                    this.m_HandLocalEuler,
                    this.m_HandLocalScale
                );
            }
        }

        private bool HasHelmetPoseChanged()
        {
            if (this.m_HelmetInstance == null) return false;
            Transform parent = this.m_HelmetInstance.transform.parent;
            Vector3 position;
            Vector3 euler;
            Vector3 scale;
            if (parent == this.m_Head)
            {
                position = this.m_HeadLocalPosition;
                euler = this.m_HeadLocalEuler;
                scale = this.m_HeadLocalScale;
            }
            else if (parent == this.m_RightHand)
            {
                position = this.m_HandLocalPosition;
                euler = this.m_HandLocalEuler;
                scale = this.m_HandLocalScale;
            }
            else
            {
                return false;
            }

            return !this.m_HasAppliedHelmetPose ||
                   this.m_LastHelmetPoseParent != parent ||
                   this.m_LastHelmetPosePosition != position ||
                   this.m_LastHelmetPoseEuler != euler ||
                   this.m_LastHelmetPoseScale != scale;
        }

        private void RememberHelmetPose(
            Transform parent,
            Vector3 position,
            Vector3 euler,
            Vector3 scale)
        {
            this.m_LastHelmetPoseParent = parent;
            this.m_LastHelmetPosePosition = position;
            this.m_LastHelmetPoseEuler = euler;
            this.m_LastHelmetPoseScale = scale;
            this.m_HasAppliedHelmetPose = true;
        }

        private static void SetLocalPose(
            Transform target,
            Transform parent,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 localScale)
        {
            target.SetParent(parent, false);
            ApplyLocalPose(target, localPosition, localEuler, localScale);
        }

        private static void ApplyLocalPose(
            Transform target,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 localScale)
        {
            target.localPosition = localPosition;
            target.localRotation = Quaternion.Euler(localEuler);
            target.localScale = localScale;
        }

        private static void FitColliderToRenderers(Transform root, BoxCollider collider)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                collider.center = Vector3.zero;
                collider.size = Vector3.one * 0.25f;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; ++i)
                bounds.Encapsulate(renderers[i].bounds);

            collider.center = root.InverseTransformPoint(bounds.center);
            Vector3 scale = root.lossyScale;
            collider.size = new Vector3(
                bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z))
            );
        }

        private static void ApplyArmReach(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float weight,
            float upperWeight,
            float lowerWeight)
        {
            if (upperArm == null || lowerArm == null || hand == null) return;

            Vector3 upperCurrent = hand.position - upperArm.position;
            Vector3 upperTarget = targetPosition - upperArm.position;
            if (upperCurrent.sqrMagnitude > 0.000001f &&
                upperTarget.sqrMagnitude > 0.000001f)
            {
                Quaternion goal = Quaternion.FromToRotation(upperCurrent, upperTarget) *
                                  upperArm.rotation;
                upperArm.rotation = Quaternion.Slerp(
                    upperArm.rotation,
                    goal,
                    Mathf.Clamp01(weight * upperWeight)
                );
            }

            Vector3 lowerCurrent = hand.position - lowerArm.position;
            Vector3 lowerTarget = targetPosition - lowerArm.position;
            if (lowerCurrent.sqrMagnitude > 0.000001f &&
                lowerTarget.sqrMagnitude > 0.000001f)
            {
                Quaternion goal = Quaternion.FromToRotation(lowerCurrent, lowerTarget) *
                                  lowerArm.rotation;
                lowerArm.rotation = Quaternion.Slerp(
                    lowerArm.rotation,
                    goal,
                    Mathf.Clamp01(weight * lowerWeight)
                );
            }

            hand.rotation = Quaternion.Slerp(
                hand.rotation,
                targetRotation,
                Mathf.Clamp01(weight * 0.9f)
            );
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_TransitionDuration = Mathf.Clamp(this.m_TransitionDuration, 0.45f, 2f);
            this.m_EquipTransferTime = Mathf.Clamp(this.m_EquipTransferTime, 0.2f, 0.8f);
            this.m_RemoveTransferTime = Mathf.Clamp(this.m_RemoveTransferTime, 0.2f, 0.8f);
            this.m_UpperArmReach = Mathf.Clamp01(this.m_UpperArmReach);
            this.m_LowerArmReach = Mathf.Clamp01(this.m_LowerArmReach);
        }
#endif
    }
}
