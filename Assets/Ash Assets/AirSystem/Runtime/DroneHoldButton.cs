using UnityEngine;
using UnityEngine.EventSystems;

namespace FranklinGame.AirSystem
{
    [DisallowMultipleComponent]
    public sealed class DroneHoldButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        private RectTransform m_Rect;
        private int m_PointerId = int.MinValue;

        public bool IsHeld => this.m_PointerId != int.MinValue;

        private void Awake()
        {
            this.m_Rect = this.transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (this.m_PointerId != int.MinValue ||
                eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            this.m_PointerId = eventData.pointerId;
            if (this.m_Rect != null) this.m_Rect.localScale = Vector3.one * 0.94f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != this.m_PointerId) return;
            this.ResetInput();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId != this.m_PointerId) return;
            this.ResetInput();
        }

        private void OnDisable()
        {
            this.ResetInput();
        }

        public void ResetInput()
        {
            this.m_PointerId = int.MinValue;
            if (this.m_Rect != null) this.m_Rect.localScale = Vector3.one;
        }
    }
}
