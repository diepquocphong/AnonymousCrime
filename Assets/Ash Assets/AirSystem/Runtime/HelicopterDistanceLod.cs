using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.AirSystem
{
    /// <summary>
    /// Lightweight dynamic LOD for a moving aircraft. The authored low-resolution
    /// body replaces the eleven-renderer cabin at distance; no runtime mesh bake,
    /// Read/Write mesh copy or static batching is required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelicopterDistanceLod : MonoBehaviour
    {
        [SerializeField] private Renderer[] m_NearRenderers;
        [SerializeField] private Renderer m_FarBodyRenderer;
        [SerializeField] private Renderer[] m_RotorRenderers;
        [SerializeField, Min(5f)] private float m_FarDistance = 32f;
        [SerializeField, Min(10f)] private float m_CullDistance = 140f;
        [SerializeField, Range(0.1f, 1f)] private float m_UpdateInterval = 0.25f;

        private float m_NextUpdate;
        private int m_CurrentLevel = -1;

        private void OnEnable()
        {
            this.m_NextUpdate = 0f;
            this.m_CurrentLevel = -1;
            this.UpdateLod(true);
        }

        private void Update()
        {
            this.UpdateLod(false);
        }

        private void OnValidate()
        {
            this.m_FarDistance = Mathf.Max(5f, this.m_FarDistance);
            this.m_CullDistance = Mathf.Max(
                this.m_FarDistance + 1f,
                this.m_CullDistance
            );
        }

        private void UpdateLod(bool force)
        {
            if (!force && Time.unscaledTime < this.m_NextUpdate) return;
            this.m_NextUpdate = Time.unscaledTime + this.m_UpdateInterval;

            Transform cameraTransform = ShortcutMainCamera.Transform;
            if (cameraTransform == null) return;

            float distanceSquared =
                (cameraTransform.position - this.transform.position).sqrMagnitude;
            int level = distanceSquared >= this.m_CullDistance * this.m_CullDistance
                ? 2
                : distanceSquared >= this.m_FarDistance * this.m_FarDistance
                    ? 1
                    : 0;
            if (!force && level == this.m_CurrentLevel) return;
            this.m_CurrentLevel = level;

            SetRenderers(this.m_NearRenderers, level == 0);
            if (this.m_FarBodyRenderer != null)
                this.m_FarBodyRenderer.enabled = level == 1;
            SetRenderers(this.m_RotorRenderers, level < 2);
        }

        private static void SetRenderers(Renderer[] renderers, bool state)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; ++i)
            {
                if (renderers[i] != null) renderers[i].enabled = state;
            }
        }
    }
}
