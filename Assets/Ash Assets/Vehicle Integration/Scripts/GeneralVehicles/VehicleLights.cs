using UnityEngine;

public class VehicleLights : MonoBehaviour
{
    [Header("Mesh Lights")]
    [Tooltip("Front light mesh 1")]
    public MeshRenderer frontLight1;
    [Tooltip("Front light mesh 2")]
    public MeshRenderer frontLight2;
    [Tooltip("Back light mesh 1")]
    public MeshRenderer backLight1;
    [Tooltip("Back light mesh 2")]
    public MeshRenderer backLight2;

    [Header("Light Colors")]
    [Tooltip("HDR Color for the front lights when turned on")]
    [ColorUsage(true, true)]
    public Color frontLightOnColor = Color.white * 6f;
    [Tooltip("HDR Color for the back lights when turned on")]
    [ColorUsage(true, true)]
    public Color backLightOnColor = Color.red * 6f;

    [Header("Spot Lights")]
    [Tooltip("Spot light 1")]
    public Light spotLight1;
    [Tooltip("Spot light 2")]
    public Light spotLight2;
    [Tooltip("Spot light intensity when on")]
    public float spotLightIntensity = 20f;

    private bool lightsOn = false;
    public bool AreLightsOn => lightsOn;

    public void FrontLightsOn()
    {
        lightsOn = true;

        if (frontLight1 != null)
            frontLight1.material.SetColor("_GlowColor", frontLightOnColor);
        if (frontLight2 != null)
            frontLight2.material.SetColor("_GlowColor", frontLightOnColor);
        if (spotLight1 != null)
            spotLight1.intensity = spotLightIntensity;
        if (spotLight2 != null)
            spotLight2.intensity = spotLightIntensity;
    }

    public void FrontLightsOff()
    {
        lightsOn = false;

        if (frontLight1 != null)
            frontLight1.material.SetColor("_GlowColor", Color.black);
        if (frontLight2 != null)
            frontLight2.material.SetColor("_GlowColor", Color.black);
        if (spotLight1 != null)
            spotLight1.intensity = 0f;
        if (spotLight2 != null)
            spotLight2.intensity = 0f;
    }

    public void LightsOn()
    {
        lightsOn = true;

        if (frontLight1 != null)
            frontLight1.material.SetColor("_GlowColor", frontLightOnColor);
        if (frontLight2 != null)
            frontLight2.material.SetColor("_GlowColor", frontLightOnColor);

        if (backLight1 != null)
            backLight1.material.SetColor("_GlowColor", backLightOnColor);
        if (backLight2 != null)
            backLight2.material.SetColor("_GlowColor", backLightOnColor);

        if (spotLight1 != null)
            spotLight1.intensity = spotLightIntensity;
        if (spotLight2 != null)
            spotLight2.intensity = spotLightIntensity;
    }

    public void LightsOff()
    {
        lightsOn = false;

        if (frontLight1 != null)
            frontLight1.material.SetColor("_GlowColor", Color.black);
        if (frontLight2 != null)
            frontLight2.material.SetColor("_GlowColor", Color.black);
        if (backLight1 != null)
            backLight1.material.SetColor("_GlowColor", Color.black);
        if (backLight2 != null)
            backLight2.material.SetColor("_GlowColor", Color.black);

        if (spotLight1 != null)
            spotLight1.intensity = 0f;
        if (spotLight2 != null)
            spotLight2.intensity = 0f;
    }
}
