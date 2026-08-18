using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FranklinGame.Shooter
{
    [DisallowMultipleComponent]
    public sealed class FranklinShooterTouchButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler
    {
        public enum Action
        {
            Fire,
            Reload,
            Melee,
            CautiousWalk,
            FirstPersonCamera
        }

        [SerializeField] private Action m_Action;
        [SerializeField] private Image m_Image;

        private bool m_Pressed;
        private bool m_Toggled;
        private int m_PointerId = int.MinValue;
        private int m_TouchId = int.MinValue;
        private Vector3 m_RestScale = Vector3.one;
        private Color m_RestColor = Color.white;

        public void Initialize(Action action, Image image)
        {
            this.m_Action = action;
            this.m_Image = image;
            this.m_RestScale = this.transform.localScale;
            this.m_RestColor = image != null ? image.color : Color.white;
            this.SetVisual(false);
        }

        public void SetToggled(bool toggled)
        {
            if (this.m_Toggled == toggled) return;
            this.m_Toggled = toggled;
            this.SetVisual(this.m_Pressed);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (this.m_Pressed) return;
            this.m_Pressed = true;
            this.m_PointerId = eventData?.pointerId ?? int.MinValue;
            this.m_TouchId = eventData is ExtendedPointerEventData extended &&
                             extended.pointerType == UIPointerType.Touch
                ? extended.touchId
                : int.MinValue;
            if (this.m_Action == Action.Fire)
                FranklinFirstPersonCameraInput.SetFireTouchReserved(this.m_TouchId, true);
            this.SetVisual(true);
            if (this.m_Action != Action.Fire)
            {
                FranklinShooterSystem.Instance?.SetBikeFirstPersonOrbitSuppressed(true);
            }
            FranklinShooterSystem.Instance?.SetTouchAction(this.m_Action, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!this.IsActivePointer(eventData)) return;
            this.Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Fire owns the pointer from PointerDown until that same finger sends PointerUp.
            // Leaving the button bounds must not release the trigger: mobile players often
            // drift the firing thumb while a second finger orbits the camera.
            if (this.m_Action == Action.Fire) return;
            if (!this.IsActivePointer(eventData)) return;
            this.Release();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (this.m_Action != Action.Fire || !this.IsActivePointer(eventData)) return;

            int touchId = eventData is ExtendedPointerEventData extended &&
                          extended.pointerType == UIPointerType.Touch
                ? extended.touchId
                : int.MinValue;
            if (touchId != this.m_TouchId) return;

            // EventSystem only invokes this after its configured pixel drag threshold.
            // At that point the same Fire finger intentionally becomes the orbit owner.
            FranklinFirstPersonCameraInput.SetFireTouchOrbitEnabled(touchId);
        }

        public void OnDrag(PointerEventData eventData)
        { }

        private void OnDisable() => this.Release();

        private bool IsActivePointer(PointerEventData eventData)
        {
            return this.m_Pressed && (eventData == null || eventData.pointerId == this.m_PointerId);
        }

        private void Release()
        {
            if (!this.m_Pressed) return;
            if (this.m_Action == Action.Fire)
                FranklinFirstPersonCameraInput.SetFireTouchReserved(this.m_TouchId, false);
            this.m_Pressed = false;
            this.m_PointerId = int.MinValue;
            this.m_TouchId = int.MinValue;
            this.SetVisual(false);
            FranklinShooterSystem.Instance?.SetTouchAction(this.m_Action, false);
            if (this.m_Action != Action.Fire)
            {
                FranklinShooterSystem.Instance?.SetBikeFirstPersonOrbitSuppressed(false);
            }
        }

        private void SetVisual(bool pressed)
        {
            float scale = pressed ? 0.9f : this.m_Toggled ? 1.04f : 1f;
            this.transform.localScale = this.m_RestScale * scale;
            if (this.m_Image != null)
            {
                Color color = this.m_Toggled
                    ? Color.Lerp(this.m_RestColor, new Color(0.35f, 0.94f, 1f, 1f), 0.24f)
                    : this.m_RestColor;
                color.a = pressed || this.m_Toggled ? 1f : this.m_RestColor.a;
                this.m_Image.color = color;
            }
        }
    }
}
