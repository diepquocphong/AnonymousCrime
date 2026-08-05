
using UnityEngine.EventSystems;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Cameras;
using System.Linq;

[Icon(RuntimePaths.GIZMOS + "GizmoTouchstick.png")]
public class MobileControllerCameraOrbitHandler : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public TouchStick stick;
    public RectTransform Surface;
    public RectTransform Handle;
    public bool invertXAxis = false;
    public bool invertYAxis = false;
    [Range(0, 2)] public float HandleLimit = 1f;

    private float zoomType = 1f;

    [SerializeField]
    public GameCreator.Runtime.VisualScripting.Event e;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isAllowedOrbit())
        {
            Surface.gameObject.SetActive(true);
            OnDrag(eventData);
            Surface.position = eventData.position;
            Handle.anchoredPosition = Vector2.zero;
            stick.OnPointerDown(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isAllowedOrbit())
        {
            Handle.anchoredPosition = new Vector2(0f, zoomType);
            eventData.position =  isInvert(eventData);
            stick.OnDrag(eventData);
        }
    }


    public void OnPointerUp(PointerEventData eventData)
    {
        Surface.gameObject.SetActive(false);
        Handle.anchoredPosition = new Vector2(0f, zoomType);
        stick.OnPointerUp(eventData);
    }

    bool isAllowedOrbit()
    {
        if (ShortcutPlayer.Instance != null)
        {
            Character character = ShortcutPlayer.Instance.Get<Character>();

            if (character != null && character.Player.InputDirection == Vector3.zero && !(Input.touchCount >= 2 && Input.touchCount <= 3))
            {
                return true;
            }
            else if (character != null && character.Player.InputDirection != Vector3.zero && Input.touchCount == 2)
            {
                return true;
            }
            else if (Input.touchCount == 0 && Application.isEditor)
            {
                return true;
            }
        }
        return false;
    }

    Vector2 isInvert(PointerEventData eventData)
    {
        if (invertXAxis && invertYAxis)
        {
            return new Vector2(-eventData.position.x, -eventData.position.y);
        }else if (invertXAxis)
        {
            return new Vector2(-eventData.position.x, eventData.position.y);
        }
        else if (invertYAxis)
        {
            return new Vector2(eventData.position.x, -eventData.position.y);
        }else
        {
            return eventData.position;
        }
    }

}
