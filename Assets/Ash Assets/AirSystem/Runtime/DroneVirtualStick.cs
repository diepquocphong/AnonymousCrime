using UnityEngine;
using UnityEngine.EventSystems;

namespace FranklinGame.AirSystem
{
    [DisallowMultipleComponent]
    public sealed class DroneVirtualStick : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        private RectTransform m_Area;
        private RectTransform m_Knob;
        private float m_TravelRatio;
        private float m_DeadZone;
        private int m_PointerId = int.MinValue;

        public Vector2 Value { get; private set; }

        public void Initialize(RectTransform knob, float travelRatio, float deadZone)
        {
            this.m_Area = this.transform as RectTransform;
            this.m_Knob = knob;
            this.m_TravelRatio = Mathf.Clamp(travelRatio, 0.1f, 0.5f);
            this.m_DeadZone = Mathf.Clamp01(deadZone);
            this.ResetInput();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (this.m_PointerId != int.MinValue) return;
            this.m_PointerId = eventData.pointerId;
            this.UpdateInput(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != this.m_PointerId) return;
            this.UpdateInput(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
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
            this.Value = Vector2.zero;
            if (this.m_Knob != null) this.m_Knob.anchoredPosition = Vector2.zero;
        }

        private void UpdateInput(PointerEventData eventData)
        {
            if (this.m_Area == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    this.m_Area,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint
                ))
            {
                return;
            }

            float radius = Mathf.Min(this.m_Area.rect.width, this.m_Area.rect.height) *
                           this.m_TravelRatio;
            if (radius <= 0.001f) return;

            Vector2 raw = Vector2.ClampMagnitude(localPoint / radius, 1f);
            float magnitude = raw.magnitude;
            this.Value = magnitude <= this.m_DeadZone
                ? Vector2.zero
                : raw.normalized * Mathf.InverseLerp(this.m_DeadZone, 1f, magnitude);

            if (this.m_Knob != null)
            {
                this.m_Knob.anchoredPosition = raw * radius;
            }
        }
    }
}
