using UnityEngine;
using UnityEngine.EventSystems;

namespace Ashsvp
{
    public class UiButton_SVP : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool isPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        private void OnDisable()
        {
            // PointerUp is not guaranteed when the complete vehicle-control
            // canvas is hidden during exit, destruction or a scene transition.
            isPressed = false;
        }
    }

}
