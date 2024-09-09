using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class JoystickControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private bool isTouching = false;
    private bool isDragging = false; // Track if the user is currently dragging

    public UnityEvent PointerDown;
    public UnityEvent PointerUp;

    // Public TMP Variables
    public TextMeshProUGUI sensitivityTMP;
    public TextMeshProUGUI smoothTimeTMP;

    // Public Unity Events for Sensitivity and SmoothTime changes
    public UnityEvent OnSensitivityChange;
    public UnityEvent OnSmoothTimeChange;

    // Public max values
    public float MaxSensitivity = 5f;
    public float MaxSmoothTime = 0.1f;

    // Public TMP Settings
    public TextMeshProUGUI SettingSensitivity;
    public TextMeshProUGUI SettingSmoothTime;

    // Values to be updated smoothly
    private float sensitivity = 0f;
    private float smoothTime = 0f;

    // Speed of smooth value changes
    public float smoothingSpeed = 5f;

    // Initial position of the Handle
    private Vector2 initialHandlePosition;
    private Vector2 lastHandlePosition;

    public float noChangeTime = 0.05f;
    private float timer = 0f;

    // Previous values for comparison
    private float previousSensitivity = 0f;
    private float previousSmoothTime = 0f;

    void Start()
    {
        if (Handle != null)
        {
            initialHandlePosition = Handle.anchoredPosition;
            lastHandlePosition = initialHandlePosition;
        }

        // Initialize MaxSensitivity and MaxSmoothTime from TMP
        UpdateMaxValues();
    }

    void Update()
    {
        // Update MaxSensitivity and MaxSmoothTime if TMP values change
        UpdateMaxValues();

        if (Surface == null)
        {
            Surface = GameObject.FindGameObjectWithTag("JoystickRotate").transform.GetChild(1).GetComponent<RectTransform>();
        }
        if (Handle == null && Surface != null)
        {
            Handle = Surface.transform.GetChild(0).GetComponent<RectTransform>();
            initialHandlePosition = Handle.anchoredPosition;  // Store the initial position when the Handle is found
            lastHandlePosition = initialHandlePosition;
        }
        if (stick == null && Surface != null)
        {
            stick = Surface.transform.GetComponent<GameCreator.Runtime.Common.TouchStick>();
        }

        // Check if Handle position has changed
        if (isTouching && Handle.anchoredPosition == lastHandlePosition)
        {
            timer += Time.deltaTime;
            if (timer >= noChangeTime)
            {
                float targetSensitivity = 0f;
                float targetSmoothTime = 0f;

                sensitivity = Mathf.Lerp(sensitivity, targetSensitivity, Time.deltaTime * smoothingSpeed);
                smoothTime = Mathf.Lerp(smoothTime, targetSmoothTime, Time.deltaTime * smoothingSpeed);
            }
        }
        else
        {
            timer = 0f;
            lastHandlePosition = Handle.anchoredPosition;

            float targetSensitivity = isTouching ? CalculateSensitivity() : 0f;
            float targetSmoothTime = isTouching ? CalculateSmoothTime() : 0f;

            sensitivity = Mathf.Lerp(sensitivity, targetSensitivity, Time.deltaTime * smoothingSpeed);
            smoothTime = Mathf.Lerp(smoothTime, targetSmoothTime, Time.deltaTime * smoothingSpeed);
        }

        // Invoke events if values change, only if dragging
        if (isDragging)
        {
            if (Mathf.Abs(sensitivity - previousSensitivity) > Mathf.Epsilon)
            {
                OnSensitivityChange.Invoke();
                previousSensitivity = sensitivity;
            }
            if (Mathf.Abs(smoothTime - previousSmoothTime) > Mathf.Epsilon)
            {
                OnSmoothTimeChange.Invoke();
                previousSmoothTime = smoothTime;
            }
        }

        sensitivityTMP.text = sensitivity.ToString("F2");
        smoothTimeTMP.text = smoothTime.ToString("F3");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isTouching = true;
        isDragging = true; // Set dragging to true when the pointer is down
        Surface.gameObject.SetActive(true);
        OnDrag(eventData);
        Surface.gameObject.transform.position = eventData.position;
        Handle.anchoredPosition = Vector2.zero;
        stick.OnPointerDown(eventData);
        PointerDown.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isTouching = false;
        isDragging = false; // Stop dragging when the pointer is up
        Surface.gameObject.SetActive(false);
        Handle.anchoredPosition = initialHandlePosition;

        // Reset Sensitivity and SmoothTime to 0
        sensitivity = 0f;
        smoothTime = 0f;
        sensitivityTMP.text = sensitivity.ToString("F2");
        smoothTimeTMP.text = smoothTime.ToString("F3");

        stick.OnPointerUp(eventData);
        PointerUp.Invoke();
    }

    public GameCreator.Runtime.Common.TouchStick stick;
    public RectTransform Surface;
    public RectTransform Handle;

    public bool invertXAxis = false;
    public bool invertYAxis = false;

    public void OnDrag(PointerEventData eventData)
    {
        Handle.anchoredPosition = eventData.position - (Vector2)Surface.position;
        Handle.anchoredPosition = Vector2.ClampMagnitude(Handle.anchoredPosition, Surface.rect.width / 2); // Limiting Handle movement
        eventData.position = isInvert(eventData);
        stick.OnDrag(eventData);
    }

    Vector2 isInvert(PointerEventData eventData)
    {
        if (invertXAxis && invertYAxis)
        {
            return new Vector2(-eventData.position.x, -eventData.position.y);
        }
        else if (invertXAxis)
        {
            return new Vector2(-eventData.position.x, eventData.position.y);
        }
        else if (invertYAxis)
        {
            return new Vector2(eventData.position.x, -eventData.position.y);
        }
        else
        {
            return eventData.position;
        }
    }

    float CalculateSensitivity()
    {
        // Calculate the distance between the initial position and the current Handle position
        float distance = Vector2.Distance(initialHandlePosition, Handle.anchoredPosition);
        // Normalize the distance to calculate Sensitivity
        return Mathf.Clamp(distance / (Surface.rect.width / 2) * MaxSensitivity, 0f, MaxSensitivity);
    }

    float CalculateSmoothTime()
    {
        // Calculate the distance between the initial position and the current Handle position
        float distance = Vector2.Distance(initialHandlePosition, Handle.anchoredPosition);
        // Normalize the distance to calculate SmoothTime
        return Mathf.Clamp(distance / (Surface.rect.width / 2) * MaxSmoothTime, 0f, MaxSmoothTime);
    }

    void UpdateMaxValues()
    {
        // Update MaxSensitivity and MaxSmoothTime based on TMP values
        float tempMaxSensitivity;
        float tempMaxSmoothTime;

        if (float.TryParse(SettingSensitivity.text, out tempMaxSensitivity))
        {
            MaxSensitivity = Mathf.Clamp(tempMaxSensitivity, 0f, 5f); // Ensure the value is within a valid range
        }

        if (float.TryParse(SettingSmoothTime.text, out tempMaxSmoothTime))
        {
            MaxSmoothTime = Mathf.Clamp(tempMaxSmoothTime, 0f, 0.1f); // Ensure the value is within a valid range
        }
    }
}
