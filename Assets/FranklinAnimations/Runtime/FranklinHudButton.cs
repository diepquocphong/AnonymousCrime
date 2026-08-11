using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FranklinGame.UI
{
    /// <summary>
    /// Serialized touch-button behaviour used by the controls embedded in CanvasPlayerControl.
    /// It lives in its own script file so Unity can persist it on the prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinHudButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        private static readonly Color NORMAL_COLOR = new(1f, 1f, 1f, 0.78f);
        private static readonly Color PRESSED_COLOR = new(0.72f, 0.9f, 1f, 0.98f);

        private FranklinMobileHud m_Hud;
        [SerializeField] private FranklinHudAction m_Action;
        [SerializeField] private Image m_Image;
        [SerializeField] private bool m_IsToggle;
        [SerializeField] private bool m_UseOpaqueVisual;
        private bool m_IsPressed;
        private bool m_IsToggled;
        private int m_ActivePointerId = int.MinValue;

        internal void Initialize(
            FranklinMobileHud hud,
            FranklinHudAction action,
            Image image,
            bool isToggle = false,
            bool useOpaqueVisual = false)
        {
            this.m_Action = action;
            this.m_Image = image;
            this.m_IsToggle = isToggle;
            this.m_UseOpaqueVisual = useOpaqueVisual;
            this.Bind(hud);
        }

        internal void Bind(FranklinMobileHud hud)
        {
            this.m_Hud = hud;
            if (this.m_Image == null) this.m_Image = this.GetComponent<Image>();
            this.SetVisual(this.m_IsToggle && this.m_IsToggled);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (this.m_IsToggle)
            {
                this.m_IsToggled = !this.m_IsToggled;
                this.SetVisual(this.m_IsToggled);
                this.m_Hud?.SetAction(this.m_Action, this.m_IsToggled);
                return;
            }

            if (this.m_IsPressed) return;

            this.m_ActivePointerId = eventData != null
                ? eventData.pointerId
                : int.MinValue;
            this.m_IsPressed = true;
            this.SetVisual(true);
            this.m_Hud?.SetAction(this.m_Action, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (this.m_IsToggle) return;
            if (!this.IsActivePointer(eventData)) return;
            this.Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (this.m_IsToggle) return;

            // Jog and Sprint are hold controls. Keep their original pointer captured until
            // PointerUp so a small thumb drift or a second orbit finger cannot interrupt run.
            if (this.m_Action is FranklinHudAction.Jog or FranklinHudAction.Sprint) return;
            if (!this.IsActivePointer(eventData)) return;
            this.Release();
        }

        private void OnDisable()
        {
            if (this.m_IsToggle)
            {
                if (this.m_IsToggled)
                {
                    this.m_Hud?.SetAction(this.m_Action, false);
                }

                this.m_IsToggled = false;
                this.m_IsPressed = false;
                this.m_ActivePointerId = int.MinValue;
                this.SetVisual(false);
                return;
            }

            this.Release();
            this.SetVisual(false);
        }

        private void Release()
        {
            if (!this.m_IsPressed) return;
            this.m_IsPressed = false;
            this.m_ActivePointerId = int.MinValue;
            this.SetVisual(false);
            this.m_Hud?.SetAction(this.m_Action, false);
        }

        private bool IsActivePointer(PointerEventData eventData)
        {
            return this.m_IsPressed &&
                   (eventData == null || eventData.pointerId == this.m_ActivePointerId);
        }

        private void SetVisual(bool isPressed)
        {
            if (this.m_Image != null)
            {
                this.m_Image.color = this.m_UseOpaqueVisual
                    ? isPressed
                        ? new Color(0.88f, 0.98f, 1f, 1f)
                        : Color.white
                    : isPressed
                        ? PRESSED_COLOR
                        : NORMAL_COLOR;
            }
            this.transform.localScale = isPressed ? Vector3.one * 0.93f : Vector3.one;
        }
    }
}
