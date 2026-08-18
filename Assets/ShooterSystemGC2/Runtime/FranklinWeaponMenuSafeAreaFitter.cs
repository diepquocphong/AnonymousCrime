using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Uniformly fits the fixed weapon-wheel composition inside the current safe area.
    /// It reacts only to rect changes, so the open menu adds no per-frame layout work.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FranklinWeaponMenuSafeAreaFitter : MonoBehaviour
    {
        private RectTransform m_SafeArea;
        private RectTransform m_Layout;
        private Vector2 m_ReferenceSize = new(960f, 1080f);

        public void Initialize(RectTransform layout, Vector2 referenceSize)
        {
            this.m_SafeArea = this.transform as RectTransform;
            this.m_Layout = layout;
            this.m_ReferenceSize = new Vector2(
                Mathf.Max(1f, referenceSize.x),
                Mathf.Max(1f, referenceSize.y)
            );
            this.Apply();
        }

        private void OnEnable()
        {
            this.Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            this.Apply();
        }

        private void Apply()
        {
            this.m_SafeArea ??= this.transform as RectTransform;
            if (this.m_SafeArea == null || this.m_Layout == null) return;

            Vector2 available = this.m_SafeArea.rect.size;
            float scale = Mathf.Min(
                1f,
                Mathf.Min(
                    available.x / this.m_ReferenceSize.x,
                    available.y / this.m_ReferenceSize.y
                )
            );
            scale = Mathf.Max(0.01f, scale);

            Vector3 targetScale = new(scale, scale, 1f);
            if (this.m_Layout.localScale != targetScale)
            {
                this.m_Layout.localScale = targetScale;
            }
            if (this.m_Layout.anchoredPosition != Vector2.zero)
            {
                this.m_Layout.anchoredPosition = Vector2.zero;
            }
        }
    }
}
