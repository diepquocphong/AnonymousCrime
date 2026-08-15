using UnityEngine;
using UnityEngine.Rendering;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Keeps the GC2 combat prop root independent from the rendered Weapons Low model. GC2 uses
    /// the root for muzzle, optics and IK calculations; pose editing is applied only to Model.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinWeaponModelPose : MonoBehaviour
    {
        private const float STATE_TRANSITION = 0.02f;

        [SerializeField] private GameObject m_Model;
        [SerializeField] private bool m_OptimizeRenderersForMobile = true;

        private Animator m_GameplayAnimator;
        private Animator m_ModelAnimator;
        private RuntimeAnimatorController m_CachedController;
        private AnimatorControllerParameter[] m_CachedParameters =
            System.Array.Empty<AnimatorControllerParameter>();
        private int m_CachedLayerCount;
        private bool m_RenderersOptimized;

        public GameObject Model => this.m_Model;
        public Transform ModelTransform => this.m_Model != null ? this.m_Model.transform : null;

        public void Initialize(
            GameObject modelPrefab,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            if (modelPrefab == null) return;
            if (this.m_Model != null) Destroy(this.m_Model);

            this.m_Model = Instantiate(modelPrefab, this.transform, false);
            this.m_Model.name = $"{modelPrefab.name} Visual Model";
            this.m_ModelAnimator = null;
            this.m_RenderersOptimized = false;
            this.m_CachedController = null;
            this.m_CachedParameters = System.Array.Empty<AnimatorControllerParameter>();
            this.m_CachedLayerCount = 0;
            this.Apply(localPosition, localRotation, localScale);
            this.OptimizeRenderers();
        }

        public void Apply(
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            if (this.m_Model == null) return;
            Transform modelTransform = this.m_Model.transform;
            modelTransform.localPosition = localPosition;
            modelTransform.localRotation = localRotation;
            modelTransform.localScale = ClampScale(localScale);
        }

        /// <summary>
        /// GC2 installs its weapon controller on the gameplay root during Equip. The original
        /// model hierarchy stays intact below its own Animator so slide/magazine animations can
        /// still use their original transform paths.
        /// </summary>
        public void BindAnimator()
        {
            if (this.m_Model == null) return;
            this.OptimizeRenderers();
            this.m_GameplayAnimator = this.GetComponent<Animator>();
            if (this.m_GameplayAnimator == null) return;

            this.m_ModelAnimator = this.m_Model.GetComponent<Animator>();
            if (this.m_ModelAnimator == null)
                this.m_ModelAnimator = this.m_Model.AddComponent<Animator>();

            this.CopyAnimatorConfiguration();
        }

        private void LateUpdate()
        {
            if (this.m_Model == null) return;
            if (this.m_GameplayAnimator == null)
                this.m_GameplayAnimator = this.GetComponent<Animator>();
            if (this.m_GameplayAnimator == null) return;

            if (this.m_ModelAnimator == null)
                this.m_ModelAnimator = this.m_Model.GetComponent<Animator>();
            if (this.m_ModelAnimator == null)
                this.m_ModelAnimator = this.m_Model.AddComponent<Animator>();

            RuntimeAnimatorController gameplayController =
                this.m_GameplayAnimator.runtimeAnimatorController;
            if (this.m_CachedController != gameplayController ||
                this.m_ModelAnimator.runtimeAnimatorController != gameplayController)
            {
                this.CopyAnimatorConfiguration();
            }

            this.SyncAnimatorParameters();
            this.SyncAnimatorStates();
        }

        private void CopyAnimatorConfiguration()
        {
            if (this.m_GameplayAnimator == null || this.m_ModelAnimator == null) return;
            this.m_ModelAnimator.runtimeAnimatorController =
                this.m_GameplayAnimator.runtimeAnimatorController;
            this.m_ModelAnimator.avatar = this.m_GameplayAnimator.avatar;
            this.m_ModelAnimator.applyRootMotion = false;
            this.m_ModelAnimator.updateMode = this.m_GameplayAnimator.updateMode;
            // The rendered Weapons Low model never owns muzzle/IK gameplay transforms.
            // Let Unity skip its transform evaluation while it is outside the camera,
            // which is important for mobile when several pooled weapon props exist.
            this.m_ModelAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            this.m_ModelAnimator.speed = this.m_GameplayAnimator.speed;

            this.m_CachedController = this.m_GameplayAnimator.runtimeAnimatorController;
            this.m_CachedParameters = this.m_CachedController != null
                ? this.m_GameplayAnimator.parameters
                : System.Array.Empty<AnimatorControllerParameter>();
            this.m_CachedLayerCount = Mathf.Min(
                this.m_GameplayAnimator.layerCount,
                this.m_ModelAnimator.layerCount
            );
        }

        private void OptimizeRenderers()
        {
            if (this.m_RenderersOptimized || !this.m_OptimizeRenderersForMobile ||
                this.m_Model == null)
            {
                return;
            }

            Renderer[] renderers = this.m_Model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; ++i)
            {
                Renderer item = renderers[i];
                if (item == null) continue;

                // Weapon gameplay uses the GC2 prop root, not renderer transforms.
                // These low-poly visuals do not need extra shadow, probe or motion-vector
                // passes on mobile; the normal URP light still shades their material.
                item.shadowCastingMode = ShadowCastingMode.Off;
                item.receiveShadows = false;
                item.lightProbeUsage = LightProbeUsage.Off;
                item.reflectionProbeUsage = ReflectionProbeUsage.Off;
                item.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            this.m_RenderersOptimized = true;
        }

        private void SyncAnimatorParameters()
        {
            if (this.m_GameplayAnimator.runtimeAnimatorController == null ||
                this.m_ModelAnimator.runtimeAnimatorController == null) return;

            // Animator.parameters returns a new array. Cache it when the controller changes
            // instead of allocating once per rendered frame while a weapon is equipped.
            for (int i = 0; i < this.m_CachedParameters.Length; ++i)
            {
                AnimatorControllerParameter parameter = this.m_CachedParameters[i];
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        this.m_ModelAnimator.SetBool(
                            parameter.nameHash,
                            this.m_GameplayAnimator.GetBool(parameter.nameHash)
                        );
                        break;
                    case AnimatorControllerParameterType.Float:
                        this.m_ModelAnimator.SetFloat(
                            parameter.nameHash,
                            this.m_GameplayAnimator.GetFloat(parameter.nameHash)
                        );
                        break;
                    case AnimatorControllerParameterType.Int:
                        this.m_ModelAnimator.SetInteger(
                            parameter.nameHash,
                            this.m_GameplayAnimator.GetInteger(parameter.nameHash)
                        );
                        break;
                }
            }
        }

        private void SyncAnimatorStates()
        {
            if (!this.m_GameplayAnimator.isActiveAndEnabled ||
                !this.m_ModelAnimator.isActiveAndEnabled ||
                this.m_GameplayAnimator.runtimeAnimatorController == null ||
                this.m_ModelAnimator.runtimeAnimatorController == null) return;

            for (int layer = 0; layer < this.m_CachedLayerCount; ++layer)
            {
                AnimatorStateInfo source = this.m_GameplayAnimator.IsInTransition(layer)
                    ? this.m_GameplayAnimator.GetNextAnimatorStateInfo(layer)
                    : this.m_GameplayAnimator.GetCurrentAnimatorStateInfo(layer);
                if (source.fullPathHash == 0) continue;

                AnimatorStateInfo target = this.m_ModelAnimator.GetCurrentAnimatorStateInfo(layer);
                AnimatorStateInfo targetNext = this.m_ModelAnimator.IsInTransition(layer)
                    ? this.m_ModelAnimator.GetNextAnimatorStateInfo(layer)
                    : default;
                if (target.fullPathHash == source.fullPathHash ||
                    targetNext.fullPathHash == source.fullPathHash) continue;

                this.m_ModelAnimator.CrossFade(
                    source.fullPathHash,
                    STATE_TRANSITION,
                    layer,
                    source.normalizedTime
                );
            }
        }

        private static Vector3 ClampScale(Vector3 value)
        {
            value.x = Mathf.Max(0.001f, value.x);
            value.y = Mathf.Max(0.001f, value.y);
            value.z = Mathf.Max(0.001f, value.z);
            return value;
        }
    }
}
