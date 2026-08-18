using UnityEngine;
using UnityEngine.EventSystems;

namespace FranklinGame.AirSystem
{
    public enum DroneHudAction
    {
        Connect,
        Exit
    }

    /// <summary>
    /// Small runtime-only contract shared by drone and rotorcraft sessions. The
    /// Canvas Air Control prefab can therefore stay pooled and asset-driven
    /// without knowing which aircraft currently owns it.
    /// </summary>
    public interface IAirHudActionHandler
    {
        void HandleHudAction(DroneHudAction action);
    }

    [DisallowMultipleComponent]
    public sealed class DroneHudActionButton : MonoBehaviour,
        IPointerClickHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        private IAirHudActionHandler m_Owner;
        private DroneHudAction m_Action;
        private RectTransform m_Rect;

        public void Initialize(IAirHudActionHandler owner, DroneHudAction action)
        {
            this.m_Owner = owner;
            this.m_Action = action;
            this.m_Rect = this.transform as RectTransform;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (this.m_Owner != null) this.m_Owner.HandleHudAction(this.m_Action);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (this.m_Rect != null) this.m_Rect.localScale = Vector3.one * 0.94f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            this.ResetVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            this.ResetVisual();
        }

        public void ResetVisual()
        {
            if (this.m_Rect != null) this.m_Rect.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            this.ResetVisual();
        }
    }
}
