using UnityEngine;

/// <summary>
/// Shared head/tail-light presentation for Car, Bike and the remaining vehicle
/// types. Renderer emission is written through MaterialPropertyBlock so a
/// driven vehicle never clones its shared materials at runtime.
/// </summary>
[DisallowMultipleComponent]
public class VehicleLights : MonoBehaviour
{
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

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

    private bool lightsOn;
    private bool rearOverrideActive;
    private Color rearOverrideColor = Color.black;
    private MaterialPropertyBlock propertyBlock;

    public bool AreLightsOn => this.lightsOn;
    public bool HasRearLights => this.backLight1 != null || this.backLight2 != null;

    private MaterialPropertyBlock PropertyBlock =>
        this.propertyBlock ??= new MaterialPropertyBlock();

    public void FrontLightsOn()
    {
        this.lightsOn = true;
        this.ApplyFrontLights(true);
    }

    public void FrontLightsOff()
    {
        this.lightsOn = false;
        this.ApplyFrontLights(false);
    }

    public void LightsOn()
    {
        this.lightsOn = true;
        this.ApplyFrontLights(true);
        this.ApplyRearLights();
    }

    public void LightsOff()
    {
        this.lightsOn = false;
        this.ApplyFrontLights(false);
        this.ApplyRearLights();
    }

    /// <summary>
    /// Lets a vehicle-specific controller temporarily own the rear emission.
    /// Headlight state is still retained, so releasing the override restores
    /// the normal running tail light immediately.
    /// </summary>
    public void SetRearLightOverride(bool active, Color color)
    {
        this.rearOverrideActive = active;
        this.rearOverrideColor = color;
        this.ApplyRearLights();
    }

    public void ClearRearLightOverride()
    {
        this.rearOverrideActive = false;
        this.ApplyRearLights();
    }

    private void ApplyFrontLights(bool active)
    {
        Color color = active ? this.frontLightOnColor : Color.black;
        this.SetGlow(this.frontLight1, color);
        this.SetGlow(this.frontLight2, color);

        this.ApplySpotLight(this.spotLight1, active);
        this.ApplySpotLight(this.spotLight2, active);
    }

    private void ApplyRearLights()
    {
        Color color = this.rearOverrideActive
            ? this.rearOverrideColor
            : this.lightsOn
                ? this.backLightOnColor
                : Color.black;
        this.SetGlow(this.backLight1, color);
        this.SetGlow(this.backLight2, color);
    }

    private void SetGlow(Renderer target, Color color)
    {
        if (target == null) return;

        MaterialPropertyBlock block = this.PropertyBlock;
        target.GetPropertyBlock(block);
        block.SetColor(GlowColorId, color);
        target.SetPropertyBlock(block);
        block.Clear();
    }

    private void ApplySpotLight(Light target, bool active)
    {
        if (target == null) return;

        float intensity = active ? this.spotLightIntensity : 0f;
        if (!Mathf.Approximately(target.intensity, intensity))
            target.intensity = intensity;

        // An enabled zero-intensity Light is still registered with the render
        // pipeline. Fully disabling parked/off headlights prevents every Bike
        // and Car from contributing an otherwise invisible per-camera light.
        if (target.enabled != active) target.enabled = active;
    }
}
