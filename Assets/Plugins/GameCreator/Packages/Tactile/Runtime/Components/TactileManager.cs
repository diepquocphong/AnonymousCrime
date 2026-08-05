using System;
using System.Collections.Generic;

using UnityEngine;
// using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class TactileManager : Singleton<TactileManager>
    {
        private static readonly HashSet<int> s_TouchIdsWithinFrame = new(10);

        // private GameObject m_Canvas;
        // private GameObject m_Template;

        private bool m_IsActive = true;

        // EVENTS: ------------------------------------------------------------------------------------

        private event Action<Touch> EventTouchBegin;
        private event Action<Touch> EventTouchMove;
        private event Action<Touch> EventTouchEnd;

        // LIFE CYCLE: --------------------------------------------------------------------------------

        // private void Awake()
        // {
        //     var canvas = new GameObject("Canvas", typeof(RectTransform));
        //     canvas.hideFlags = HideFlags.HideAndDontSave;
        //     canvas.transform.SetParent(this.transform);

        //     var canva = canvas.AddComponent<Canvas>();
        //     canva.renderMode = RenderMode.ScreenSpaceOverlay;
        //     canva.vertexColorAlwaysGammaSpace = true;
        //     canva.sortingOrder = 30000;

        //     var instance = new GameObject("Touch", typeof(RectTransform));
        //     instance.hideFlags = HideFlags.HideAndDontSave;
        //     instance.SetActive(false);

        //     var transform = (RectTransform)instance.transform;
        //     transform.SetParent(canvas.transform);
        //     transform.sizeDelta = Vector2.one * 15f;
        //     transform.anchoredPosition = Vector3.zero;

        //     var image = instance.AddComponent<Image>();
        //     image.raycastTarget = false;
        //     image.sprite = null;

        //     this.m_Canvas = canvas;
        //     this.m_Template = instance;
        //     PoolManager.Instance.Prewarm(instance, 5);
        // }

        private void Update()
        {
            if (!this.m_IsActive) return;
            if (EnhancedTouchSupport.enabled) this.HandleTouchInput();
        }

        // REGISTER: ----------------------------------------------------------------------------------

        public static void Register(TactileControl control)
        {
            Instance.EventTouchBegin += control.OnInteractBegin;
            Instance.EventTouchMove += control.OnInteractDrag;
            Instance.EventTouchEnd += control.OnInteractEnd;

            EnhancedTouchSupport.Enable();
        }

        public static void Unregister(TactileControl control)
        {
            if (ApplicationManager.IsExiting) return;

            Instance.EventTouchBegin -= control.OnInteractBegin;
            Instance.EventTouchMove -= control.OnInteractDrag;
            Instance.EventTouchEnd -= control.OnInteractEnd;

            EnhancedTouchSupport.Disable();
        }

        // PRIVATE METHODS: ---------------------------------------------------------------------------

        private void HandleTouchInput()
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                if (!s_TouchIdsWithinFrame.Add(touch.touchId))
                {
                    InputSystem.ResetDevice(touch.finger.screen);
                }

                if (float.IsInfinity(touch.screenPosition.x) ||
                    float.IsInfinity(touch.screenPosition.y))
                    continue;

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        EventTouchBegin?.Invoke(touch);
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        EventTouchMove?.Invoke(touch);
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        EventTouchEnd?.Invoke(touch);
                        break;
                }
            }

            s_TouchIdsWithinFrame.Clear();
        }
    }
}