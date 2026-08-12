using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Mobile-safe hold button. A single captured pointer owns each refueling hold.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinFuelStationHoldButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler,
        ICancelHandler
    {
        private FranklinFuelStation m_Station;
        private Image m_Background;
        private Color m_NormalColor;
        private Color m_PressedColor;
        private bool m_IsHolding;
        private int m_PointerId = int.MinValue;

        internal void Initialize(
            FranklinFuelStation station,
            Image background,
            Color normalColor,
            Color pressedColor)
        {
            this.m_Station = station;
            this.m_Background = background;
            this.m_NormalColor = normalColor;
            this.m_PressedColor = pressedColor;
            this.SetPressed(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (this.m_IsHolding || this.m_Station == null ||
                !this.m_Station.CanBeginRefueling)
            {
                return;
            }

            this.m_IsHolding = true;
            this.m_PointerId = eventData != null ? eventData.pointerId : int.MinValue;
            this.SetPressed(true);
            this.m_Station.SetRefuelHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!this.IsCapturedPointer(eventData)) return;
            this.Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!this.IsCapturedPointer(eventData)) return;
            this.Release();
        }

        public void OnCancel(BaseEventData eventData)
        {
            this.Release();
        }

        private void OnDisable()
        {
            this.Release();
        }

        internal void ForceRelease()
        {
            this.Release();
        }

        private bool IsCapturedPointer(PointerEventData eventData)
        {
            return this.m_IsHolding &&
                   (eventData == null || eventData.pointerId == this.m_PointerId);
        }

        private void Release()
        {
            if (!this.m_IsHolding) return;
            this.m_IsHolding = false;
            this.m_PointerId = int.MinValue;
            this.SetPressed(false);
            this.m_Station?.SetRefuelHeld(false);
        }

        private void SetPressed(bool pressed)
        {
            if (this.m_Background != null)
                this.m_Background.color = pressed
                    ? this.m_PressedColor
                    : this.m_NormalColor;

            this.transform.localScale = pressed ? Vector3.one * 0.98f : Vector3.one;
        }
    }
}
