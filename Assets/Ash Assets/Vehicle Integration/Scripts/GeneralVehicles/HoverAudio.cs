using UnityEngine;

public class HoverAudio : MonoBehaviour
{
    [Header("Engine Audio Settings")]
    [Tooltip("AudioSource for the engine sound.")]
    public AudioSource engineAudio;
    [Tooltip("Engine sound clip (recorded to sound natural across the speed range).")]
    public AudioClip engineClip;
    [Tooltip("Vehicle speed (km/h) at idle (e.g. when stationary).")]
    public float idleSpeed = 0f;
    [Tooltip("Vehicle speed (km/h) at max pitch/volume.")]
    public float maxSpeed = 100f;
    [Tooltip("Pitch value at idle speed.")]
    public float idlePitch = 0.8f;
    [Tooltip("Pitch value at max speed.")]
    public float maxPitch = 2.0f;
    [Tooltip("Volume at idle speed.")]
    public float idleVolume = 0.7f;
    [Tooltip("Volume at max speed.")]
    public float maxVolume = 1.0f;
    [Tooltip("Smoothing factor for engine transitions.")]
    public float smoothFactor = 5f;

    [Header("Boost Audio Settings")]
    [Tooltip("AudioSource for the boost sound.")]
    public AudioSource boostAudio;
    [Tooltip("Boost sound clip.")]
    public AudioClip boostClip;
    [Tooltip("Max volume for the boost sound.")]
    public float boostVolume = 1f;
    [Tooltip("Speed at which boost audio fades in/out.")]
    public float boostFadeSpeed = 2f;

    [Header("Collision Sound Settings")]
    [Tooltip("AudioSource for collision sounds.")]
    public AudioSource collisionAudio;
    [Tooltip("Collision sound clip.")]
    public AudioClip collisionClip;
    [Tooltip("Minimum impact magnitude to trigger collision sound.")]
    public float collisionThreshold = 3f;
    [Tooltip("Volume for the collision sound when triggered.")]
    public float collisionVolume = 1f;

    private HoverVehicleController hoverController;

    void Awake()
    {
        hoverController = GetComponentInParent<HoverVehicleController>();
        if (hoverController == null)
        {
            Debug.LogWarning("HoverAudio requires a HoverVehicleController in its parents.");
            enabled = false;
            return;
        }

        if (engineAudio == null)
            Debug.LogWarning("Engine AudioSource not assigned.");
        if (collisionAudio == null)
            Debug.LogWarning("Collision AudioSource not assigned.");

        if (boostAudio != null && boostClip != null)
        {
            boostAudio.clip = boostClip;
            boostAudio.loop = true;
            boostAudio.playOnAwake = false;
            boostAudio.volume = 0f;
        }
    }

    void Start()
    {
        if (engineAudio != null && engineClip != null)
        {
            engineAudio.clip = engineClip;
            engineAudio.loop = true;
            engineAudio.volume = idleVolume;
            engineAudio.pitch = idlePitch;
            engineAudio.Play();
        }
    }

    void Update()
    {
        if (hoverController == null)
            return;

        if (!hoverController.isVehicleEnabled)
        {
            if (engineAudio != null) engineAudio.volume = 0f;
            if (boostAudio != null && boostAudio.isPlaying) boostAudio.Stop();
            return;
        }

        UpdateEngineAudio();
        UpdateBoostAudio();
    }

    private void UpdateEngineAudio()
    {
        if (engineAudio == null) return;

        float currentSpeed = (float)hoverController.currentSpeed.Get(gameObject);
        float t = Mathf.InverseLerp(idleSpeed, maxSpeed, currentSpeed);

        float targetPitch = Mathf.Lerp(idlePitch, maxPitch, t);
        float targetVolume = Mathf.Lerp(idleVolume, maxVolume, t);

        engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, targetPitch, Time.deltaTime * smoothFactor);
        engineAudio.volume = Mathf.Lerp(engineAudio.volume, targetVolume, Time.deltaTime * smoothFactor);
    }

    private void UpdateBoostAudio()
    {
        if (boostAudio == null) return;

        bool boosting = hoverController.IsBoosted;

        float target = boosting ? boostVolume : 0f;

        if (boosting && !boostAudio.isPlaying)
            boostAudio.Play();

        boostAudio.volume = Mathf.MoveTowards(
            boostAudio.volume,
            target,
            boostFadeSpeed * Time.deltaTime
        );

        if (!boosting && boostAudio.isPlaying && boostAudio.volume <= 0.01f)
            boostAudio.Stop();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hoverController == null || collisionAudio == null || collisionClip == null)
            return;

        foreach (ContactPoint cp in collision.contacts)
            if (Vector3.Dot(cp.normal, Vector3.up) > 0.7f)
                return;

        float impact = collision.relativeVelocity.magnitude;
        if (impact > collisionThreshold)
            collisionAudio.PlayOneShot(collisionClip, collisionVolume);
    }
}
