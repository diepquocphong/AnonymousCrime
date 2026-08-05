using UnityEngine;
using UnityEngine.UI;
using GameCreator.Runtime.Common;

public class RPMDisplayUI : MonoBehaviour
{
    [Tooltip("Reference to the PhysicsCarController on the car.")]
    public PhysicsCarController carController;

    [Tooltip("UI Text component used to display the RPM (optional).")]
    public Text rpmText;

    [Tooltip("UI RectTransform for the RPM needle (optional).")]
    public RectTransform rpmNeedle;

    [Tooltip("Minimum and maximum rotation angles of the needle.")]
    public float minNeedleRotation = -90f;
    public float maxNeedleRotation = 90f;

    [Tooltip("Maximum RPM value (redline).")]
    public float maxRPM = 8000f;

    void Update()
    {
        if (carController == null) return;

        float rpm = (float)carController.currentRPM.Get(carController.gameObject);

        if (rpmText != null)
        {
            rpmText.text = $"RPM: {rpm:F0}";
        }

        // If there's a needle, rotate it
        if (rpmNeedle != null)
        {
            float normalizedRPM = Mathf.Clamp01(rpm / maxRPM);
            float needleRotation = Mathf.Lerp(minNeedleRotation, maxNeedleRotation, normalizedRPM);
            rpmNeedle.localRotation = Quaternion.Euler(0f, 0f, needleRotation);
        }
    }
}
