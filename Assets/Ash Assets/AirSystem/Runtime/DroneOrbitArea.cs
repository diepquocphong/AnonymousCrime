using UnityEngine;
using UnityEngine.EventSystems;

namespace FranklinGame.AirSystem
{
    [DisallowMultipleComponent]
    public sealed class DroneOrbitArea : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        private int m_PointerId = int.MinValue;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (this.m_PointerId != int.MinValue ||
                eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            this.m_PointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != this.m_PointerId) return;
            DroneCameraOrbitInput.SubmitTouchDelta(eventData.delta);
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
            DroneCameraOrbitInput.ResetTouchInput();
        }
    }
}
