
using UnityEngine.EventSystems;
using UnityEngine;
using GameCreator.Runtime.Common;

[Icon(RuntimePaths.GIZMOS + "GizmoTouchstick.png")]
public class MobileControllerCharacterMovementHandler : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public TouchStick stick;
    public RectTransform Surface;
    public RectTransform Handle;
    [Range(0, 2)] public float HandleLimit = 1f;

    private float zoomType = 1f;

    public void OnPointerDown(PointerEventData eventData)
    {
            Surface.gameObject.SetActive(true);
            OnDrag(eventData);
            Surface.position = eventData.position;
            Handle.anchoredPosition = Vector2.zero;
            stick.OnPointerDown(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
            Handle.anchoredPosition = new Vector2(0f, zoomType);
            stick.OnDrag(eventData);
    }


    public void OnPointerUp(PointerEventData eventData)
    {
            Surface.gameObject.SetActive(false);
            Handle.anchoredPosition = new Vector2(0f, zoomType);
            stick.OnPointerUp(eventData);
    }
}
