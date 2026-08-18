using UnityEngine;

namespace FranklinGame.Menu
{
    /// <summary>
    /// Keeps interactive menu content inside the device safe area while the
    /// full-bleed background remains edge-to-edge.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FranklinSafeArea : MonoBehaviour
    {
        private RectTransform m_RectTransform;
        private Rect m_LastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int m_LastScreenSize = new Vector2Int(-1, -1);

        private void Awake()
        {
            this.m_RectTransform = this.transform as RectTransform;
            this.Apply(true);
        }

        private void OnEnable()
        {
            this.Apply(true);
        }

        private void Update()
        {
            this.Apply(false);
        }

        private void OnRectTransformDimensionsChange()
        {
            this.Apply(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) this.Apply(true);
        }

        private void Apply(bool force)
        {
            if (this.m_RectTransform == null)
            {
                this.m_RectTransform = this.transform as RectTransform;
            }

            if (this.m_RectTransform == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && safeArea == this.m_LastSafeArea && screenSize == this.m_LastScreenSize)
            {
                return;
            }

            this.m_LastSafeArea = safeArea;
            this.m_LastScreenSize = screenSize;
            this.m_RectTransform.anchorMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height
            );
            this.m_RectTransform.anchorMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height
            );
            this.m_RectTransform.offsetMin = Vector2.zero;
            this.m_RectTransform.offsetMax = Vector2.zero;
        }
    }
}
