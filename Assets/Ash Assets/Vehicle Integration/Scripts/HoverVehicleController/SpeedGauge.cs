using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class SegmentedSpeedGauge : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Paints the first 2/3 of the circle")]
    [SerializeField] private Image primaryImage;
    [Tooltip("Paints the final   1/3 of the circle")]
    [SerializeField] private Image boostImage;

    [Header("Vehicle Reference")]
    [Tooltip("Any Rigidbody whose speed you want to read (your HoverVehicle�s)")]
    [SerializeField] private Rigidbody vehicleRb;

    [Header("Speed Settings (km/h)")]
    [Tooltip("Max speed WITHOUT boost")]
    [SerializeField] private float maxUnboostedSpeed = 120f;
    [Tooltip("Max speed WITH    boost")]
    [SerializeField] private float maxBoostedSpeed = 180f;

    private const float PRIMARY_END = 2f / 3f;

    void Start()
    {
        // sanity
        if (primaryImage == null || boostImage == null || vehicleRb == null)
            Debug.LogError("SegmentedSpeedGauge: drag in your Images and Rigidbody!");

        float angle = PRIMARY_END * 360f;
        boostImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
    }

    void Update()
    {
        // 1) read speed in km/h
        float speed = vehicleRb.linearVelocity.magnitude * 3.6f;

        float unboostNorm = Mathf.Clamp01(speed / maxUnboostedSpeed);
        float primaryFill = unboostNorm * PRIMARY_END;

        float boostNorm = Mathf.InverseLerp(maxUnboostedSpeed, maxBoostedSpeed, speed);
        float boostFill = Mathf.Clamp01(boostNorm) * (1f - PRIMARY_END);

        primaryImage.fillAmount = primaryFill;
        boostImage.fillAmount = boostFill;
    }
}
