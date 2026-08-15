using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Collapses the local Player head bone into the neck while the car FPS view
    /// is active. It runs after the seated pose guard so Animator evaluation cannot
    /// expose the head for a frame, and restores the exact original transform on
    /// exit. The component remains disabled outside FPS and has no steady-state cost.
    /// </summary>
    [DefaultExecutionOrder(3000)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonHeadOcclusion : MonoBehaviour
    {
        private Transform m_Head;
        private Vector3 m_OriginalLocalPosition;
        private Vector3 m_OriginalLocalScale;
        private Vector3 m_HiddenLocalPosition;
        private bool m_IsActive;

        public void Begin(Animator animator)
        {
            this.End();
            if (animator == null || !animator.isHuman) return;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;

            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            this.m_Head = head;
            this.m_OriginalLocalPosition = head.localPosition;
            this.m_OriginalLocalScale = head.localScale;
            this.m_HiddenLocalPosition = head.parent != null && neck != null
                ? head.parent.InverseTransformPoint(neck.position)
                : Vector3.zero;
            this.m_IsActive = true;
            this.enabled = true;
            this.ApplyHiddenPose();
        }

        public void End()
        {
            if (this.m_IsActive && this.m_Head != null)
            {
                this.m_Head.localPosition = this.m_OriginalLocalPosition;
                this.m_Head.localScale = this.m_OriginalLocalScale;
            }

            this.m_IsActive = false;
            this.m_Head = null;
            this.enabled = false;
        }

        private void LateUpdate()
        {
            if (!this.m_IsActive || this.m_Head == null)
            {
                this.End();
                return;
            }

            this.ApplyHiddenPose();
        }

        private void OnDisable()
        {
            if (!this.m_IsActive) return;

            if (this.m_Head != null)
            {
                this.m_Head.localPosition = this.m_OriginalLocalPosition;
                this.m_Head.localScale = this.m_OriginalLocalScale;
            }
            this.m_IsActive = false;
            this.m_Head = null;
        }

        private void ApplyHiddenPose()
        {
            this.m_Head.localPosition = this.m_HiddenLocalPosition;
            this.m_Head.localScale = Vector3.zero;
        }
    }
}
