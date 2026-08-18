using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Keeps the weapon wheel interactive inside its circular silhouette while allowing
    /// taps in the transparent square corners to reach the full-screen close target.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FranklinCircularRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private RectTransform m_RectTransform;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            this.m_RectTransform ??= this.transform as RectTransform;
            if (this.m_RectTransform == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    this.m_RectTransform,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint
                ))
            {
                return false;
            }

            Rect rect = this.m_RectTransform.rect;
            float radiusX = rect.width * 0.5f;
            float radiusY = rect.height * 0.5f;
            if (radiusX <= Mathf.Epsilon || radiusY <= Mathf.Epsilon) return false;

            Vector2 center = rect.center;
            float normalizedX = (localPoint.x - center.x) / radiusX;
            float normalizedY = (localPoint.y - center.y) / radiusY;
            return normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
        }
    }
}
