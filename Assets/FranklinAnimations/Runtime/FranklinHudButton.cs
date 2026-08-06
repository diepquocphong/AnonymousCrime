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
        private bool m_IsPressed;

        internal void Initialize(
            FranklinMobileHud hud,
            FranklinHudAction action,
            Image image)
        {
            this.m_Action = action;
            this.m_Image = image;
            this.Bind(hud);
        }

        internal void Bind(FranklinMobileHud hud)
        {
            this.m_Hud = hud;
            if (this.m_Image == null) this.m_Image = this.GetComponent<Image>();
            this.SetVisual(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (this.m_IsPressed) return;
            this.m_IsPressed = true;
            this.SetVisual(true);
            this.m_Hud?.SetAction(this.m_Action, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            this.Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            this.Release();
        }

        private void OnDisable()
        {
            this.Release();
            this.SetVisual(false);
        }

        private void Release()
        {
            if (!this.m_IsPressed) return;
            this.m_IsPressed = false;
            this.SetVisual(false);
            this.m_Hud?.SetAction(this.m_Action, false);
        }

        private void SetVisual(bool isPressed)
        {
            if (this.m_Image != null)
            {
                this.m_Image.color = isPressed ? PRESSED_COLOR : NORMAL_COLOR;
            }
            this.transform.localScale = isPressed ? Vector3.one * 0.93f : Vector3.one;
        }
    }
}
