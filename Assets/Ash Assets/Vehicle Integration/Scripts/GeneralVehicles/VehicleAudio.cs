using UnityEngine;

public class VehicleAudioPhysics : MonoBehaviour
{
    [Header("Engine Audio Settings")]
    [Tooltip("AudioSource for the engine sound.")]
    public AudioSource engineAudio;
    [Tooltip("Engine sound clip (recorded to sound natural across the RPM range).")]
    public AudioClip engineClip;
    [Tooltip("Engine RPM at idle.")]
    public float idleRPM = 800f;
    [Tooltip("Engine RPM at maximum load.")]
    public float maxRPM = 8000f;
    [Tooltip("Pitch value at idle RPM.")]
    public float idlePitch = 0.8f;
    [Tooltip("Pitch value at max RPM.")]
    public float maxPitch = 2.0f;
    [Tooltip("Volume at idle RPM.")]
    public float idleVolume = 0.7f;
    [Tooltip("Volume at max RPM.")]
    public float maxVolume = 1.0f;
    [Tooltip("Smoothing factor for engine transitions.")]
    public float smoothFactor = 5f;

    [Header("Drift Sound Settings")]
    [Tooltip("AudioSource for the drift sound.")]
    public AudioSource driftAudio;
    [Tooltip("Drift sound clip.")]
    public AudioClip driftClip;
    [Tooltip("Smoothing factor for drift volume transitions.")]
    public float driftVolumeSmooth = 5f;
    [Tooltip("Maximum volume for the drift sound when drifting or handbraking.")]
    public float driftVolume = 1f;

    [Header("Collision Sound Settings")]
    [Tooltip("AudioSource for collision sounds (used to play one-shot sounds).")]
    public AudioSource collisionAudio;
    [Tooltip("Collision sound clip.")]
    public AudioClip collisionClip;
    [Tooltip("Minimum collision impact magnitude to trigger sound.")]
    public float collisionThreshold = 5f;
    [Tooltip("Volume for the collision sound when triggered.")]
    public float collisionVolume = 1f;

    private PhysicsCarController carController;
    private PhysicsBikeController bikeController;
    private bool audioWasEnabled;

    void Awake()
    {
        carController = GetComponentInParent<PhysicsCarController>();
        bikeController = GetComponentInParent<PhysicsBikeController>();

        PrepareSource(engineAudio);
        PrepareSource(driftAudio);
        PrepareSource(collisionAudio);

        if (carController == null && bikeController == null)
        {
            Debug.LogWarning("VehicleAudioPhysics needs a Car or Bike controller in its parents.");
            enabled = false;
            return;
        }

        if (engineAudio == null)
            Debug.LogWarning("Engine AudioSource not assigned.");
        if (driftAudio == null)
            Debug.LogWarning("Drift AudioSource not assigned.");
        if (collisionAudio == null)
            Debug.LogWarning("Collision AudioSource not assigned.");
    }

    private bool isEnabled
    {
        get
        {
            // if this object has a car controller, only its flag matters
            if (carController != null)
                return carController.isVehicleEnabled;

            // otherwise if it has a bike controller, only its flag matters
            if (bikeController != null)
                return bikeController.isVehicleEnabled;

            // neither? audio off
            return false;
        }
    }

    private float CurrentRPM
    {
        get
        {
            if (carController != null)
                return (float)carController.currentRPM.Get(carController.gameObject);
            if (bikeController != null)
                return (float)bikeController.currentRPM.Get(bikeController.gameObject);
            return 0f;
        }
    }

    private bool IsDrifting
    {
        get
        {
            if (carController != null) return carController.isDrifting;
            if (bikeController != null) return bikeController.isDrifting;
            return false;
        }
    }

    private bool Handbrake
    {
        get
        {
            if (carController != null) return carController.handbrakeInput;
            if (bikeController != null) return bikeController.handbrakeInput;
            return false;
        }
    }

    void Start()
    {
        if (engineAudio != null && engineClip != null)
        {
            engineAudio.clip = engineClip;
            engineAudio.loop = true;
            engineAudio.volume = 0f;
            engineAudio.pitch = idlePitch;
        }

        if (driftAudio != null && driftClip != null)
        {
            driftAudio.clip = driftClip;
            driftAudio.loop = true;
            driftAudio.volume = 0f;
        }
    }

    void Update()
    {
        if (!isEnabled)
        {
            if (audioWasEnabled) StopLoopingAudio();
            else SilenceLoopingAudio();
            audioWasEnabled = false;
            return;
        }

        if (!audioWasEnabled)
        {
            StartLoopingAudio();
            audioWasEnabled = true;
        }

        UpdateEngineAudio();
        UpdateDriftAudio();
    }

    void OnDisable()
    {
        StopLoopingAudio();
        audioWasEnabled = false;
    }

    private static void PrepareSource(AudioSource source)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.Stop();
    }

    private void StartLoopingAudio()
    {
        if (engineAudio != null && engineAudio.clip != null)
        {
            engineAudio.volume = 0f;
            engineAudio.pitch = idlePitch;
            engineAudio.Play();
        }

        if (driftAudio != null && driftAudio.clip != null)
        {
            driftAudio.volume = 0f;
            driftAudio.Play();
        }
    }

    private void StopLoopingAudio()
    {
        SilenceLoopingAudio();
        if (engineAudio != null) engineAudio.Stop();
        if (driftAudio != null) driftAudio.Stop();
    }

    private void SilenceLoopingAudio()
    {
        if (engineAudio != null) engineAudio.volume = 0f;
        if (driftAudio != null) driftAudio.volume = 0f;
    }

    void UpdateEngineAudio()
    {
        if (engineAudio == null)
            return;

        float currentRPM = CurrentRPM;
        float t = Mathf.InverseLerp(idleRPM, maxRPM, currentRPM);

        float targetPitch = Mathf.Lerp(idlePitch, maxPitch, t);
        float targetVolume = Mathf.Lerp(idleVolume, maxVolume, t);

        engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, targetPitch, Time.deltaTime * smoothFactor);
        engineAudio.volume = Mathf.Lerp(engineAudio.volume, targetVolume, Time.deltaTime * smoothFactor);
    }

    void UpdateDriftAudio()
    {
        if (driftAudio == null)
            return;

        bool shouldPlayDrift = IsDrifting || Handbrake;
        float targetVolume = shouldPlayDrift ? driftVolume : 0f;
        driftAudio.volume = Mathf.Lerp(driftAudio.volume, targetVolume, Time.deltaTime * driftVolumeSmooth);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isEnabled || collisionAudio == null || collisionClip == null)
            return;

        if (collision.relativeVelocity.magnitude > collisionThreshold)
        {
            collisionAudio.PlayOneShot(collisionClip, collisionVolume);
        }
    }
}
