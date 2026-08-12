using System;
using System.Threading.Tasks;
using FranklinGame.Animations;
using GameCreator.Runtime.Characters;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Converts a strong, already-classified bike impact into a GC2 full-body
    /// ragdoll ejection. The seat anchor never moves: the rider is first released
    /// from BikeEntry, then GC2 owns the skeleton until recovery finishes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FranklinBikeImpactAudio))]
    [RequireComponent(typeof(FranklinArcadeBikeDriver))]
    [RequireComponent(typeof(BikeEntry))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FranklinBikeCrashRagdoll : MonoBehaviour
    {
        private const int CURRENT_CONFIGURATION_VERSION = 8;

        [Header("Strong Impact")]
        [Tooltip("Minimum accepted impact severity that ejects the rider.")]
        [Min(0.1f)] [SerializeField] private float m_MinEjectSeverity = 7f;
        [Tooltip("Seconds after an ejection before another crash can be handled.")]
        [Min(0f)] [SerializeField] private float m_CrashCooldown = 2f;

        [Header("Ejection Velocity")]
        [Range(0f, 1.5f)] [SerializeField] private float m_InheritBikeVelocity = 0.72f;
        [Min(0f)] [SerializeField] private float m_MinUpwardVelocity = 5.5f;
        [Min(0f)] [SerializeField] private float m_MaxUpwardVelocity = 8.5f;
        [Min(0f)] [SerializeField] private float m_ImpactAwayVelocity = 2.5f;
        [Min(0f)] [SerializeField] private float m_TumbleVelocity = 3.5f;

        [Header("Bike Fall And Rest")]
        [Tooltip("Initial roll velocity that makes the unoccupied bike fall sideways.")]
        [Min(0.1f)] [SerializeField] private float m_BikeToppleAngularVelocity = 3.4f;

        [Header("Recovery")]
        [SerializeField] private bool m_AutoRecover = true;
        [Min(0.25f)] [SerializeField] private float m_MinRagdollTime = 2.5f;
        [Tooltip("Seconds used to move the existing Main Camera Shot from the bike to the physical Player before the stand-up animation starts.")]
        [Min(0f)] [SerializeField] private float m_RecoveryCameraBlend = 0.45f;

        [SerializeField, HideInInspector] private FranklinBikeImpactAudio m_ImpactAudio;
        [SerializeField, HideInInspector] private FranklinArcadeBikeDriver m_Driver;
        [SerializeField, HideInInspector] private FranklinArcadeBikeRagdoll m_BikeRagdoll;
        [SerializeField, HideInInspector] private BikeEntry m_BikeEntry;
        [SerializeField, HideInInspector] private Rigidbody m_BikeBody;
        [SerializeField, HideInInspector] private int m_ConfigurationVersion;

        private bool m_IsHandlingCrash;
        private float m_NextCrashTime;
        private int m_AsyncVersion;
        private bool m_MissingRagdollWasReported;

        /// <summary>
        /// Raised only after GC2 has actually entered ragdoll for a seated rider.
        /// Bike health uses this confirmation instead of damaging the rider for
        /// every collision that was merely classified as heavy.
        /// </summary>
        public event Action<Character, float> EventRiderRagdollStarted;

        public bool IsConfigured =>
            this.m_ImpactAudio != null &&
            this.m_Driver != null &&
            this.m_BikeRagdoll != null &&
            this.m_BikeRagdoll.IsConfigured &&
            this.m_BikeEntry != null &&
            this.m_BikeBody != null;
        public bool HasCurrentConfiguration =>
            this.m_ConfigurationVersion >= CURRENT_CONFIGURATION_VERSION;

        public void Configure(
            FranklinBikeImpactAudio impactAudio,
            FranklinArcadeBikeDriver driver,
            FranklinArcadeBikeRagdoll bikeRagdoll,
            BikeEntry bikeEntry,
            Rigidbody bikeBody)
        {
            this.m_ImpactAudio = impactAudio;
            this.m_Driver = driver;
            this.m_BikeRagdoll = bikeRagdoll;
            this.m_BikeEntry = bikeEntry;
            this.m_BikeBody = bikeBody;
        }

        public void UpgradeConfigurationIfNeeded()
        {
            if (this.m_ConfigurationVersion >= CURRENT_CONFIGURATION_VERSION) return;

            this.m_BikeToppleAngularVelocity = 3.4f;
            this.m_ConfigurationVersion = CURRENT_CONFIGURATION_VERSION;
        }

        private void Awake()
        {
            this.ResolveReferences();
        }

        private void OnEnable()
        {
            this.ResolveReferences();
            if (this.m_ImpactAudio != null)
                this.m_ImpactAudio.EventHeavyImpact += this.OnHeavyImpact;
        }

        private void OnDisable()
        {
            if (this.m_ImpactAudio != null)
                this.m_ImpactAudio.EventHeavyImpact -= this.OnHeavyImpact;

            this.m_AsyncVersion++;
            this.m_IsHandlingCrash = false;
            this.m_BikeRagdoll?.SettleRagdoll();
        }

        private void OnHeavyImpact(Collision collision, float severity)
        {
            if (this.m_IsHandlingCrash || severity < this.m_MinEjectSeverity ||
                Time.unscaledTime < this.m_NextCrashTime)
            {
                return;
            }

            Character rider = this.m_BikeEntry != null
                ? this.m_BikeEntry.SeatedCharacter
                : null;
            if (rider == null) return;

            // The GC2 API still toggles IsRagdoll when the selected system is None.
            // Require the real Default system before releasing the rider from seat.
            if (rider.Ragdoll.Get<RagdollDefault>() == null)
            {
                if (!this.m_MissingRagdollWasReported)
                {
                    Debug.LogError(
                        "[Arcade Bikes] Strong-impact ejection requires GC2 Default " +
                        "Ragdoll on the Player Character.",
                        rider
                    );
                    this.m_MissingRagdollWasReported = true;
                }
                return;
            }

            this.m_IsHandlingCrash = true;
            this.m_NextCrashTime = Time.unscaledTime + this.m_CrashCooldown;
            int version = ++this.m_AsyncVersion;
            Vector3 ejectionVelocity = this.CalculateEjectionVelocity(collision, severity);
            float bikeFallSign = this.CalculateBikeFallSign(collision);

            if (this.m_BikeRagdoll == null ||
                !this.m_BikeRagdoll.ActivateRagdoll(
                    bikeFallSign,
                    this.m_BikeToppleAngularVelocity
                ))
            {
                this.m_IsHandlingCrash = false;
                return;
            }
            if (!this.m_BikeEntry.ReleaseForCrash(rider))
            {
                this.m_BikeRagdoll.SettleRagdoll();
                this.m_IsHandlingCrash = false;
                return;
            }

            this.StartRagdollEjection(rider, ejectionVelocity, severity, version);
        }

        private async void StartRagdollEjection(
            Character rider,
            Vector3 ejectionVelocity,
            float severity,
            int version)
        {
            try
            {
                await rider.Ragdoll.StartRagdoll();
                if (!this.IsCurrent(version) || rider == null) return;

                this.EventRiderRagdollStarted?.Invoke(rider, severity);
                this.ApplyEjectionVelocity(rider, ejectionVelocity);
                await this.WaitUnscaled(this.m_MinRagdollTime, version);
                if (!this.IsCurrent(version) || rider == null) return;

                if (this.m_AutoRecover && !rider.IsDead && rider.Ragdoll.IsRagdoll)
                {
                    FranklinBikeMainShotAim cameraAim = rider.GetComponentInChildren<
                        FranklinBikeMainShotAim
                    >(true);
                    if (cameraAim != null)
                    {
                        await cameraAim.LerpToRagdollPlayer(
                            rider,
                            this.m_RecoveryCameraBlend
                        );
                    }
                    if (!this.IsCurrent(version) || rider == null) return;
                    await rider.Ragdoll.StartRecover();
                    rider.GetComponentInChildren<FranklinAnimationBridge>(true)
                        ?.RestoreModelRootBaseline();
                }
                if (!this.IsCurrent(version) || rider == null) return;

                if (this.m_AutoRecover && !rider.IsDead && rider.Player != null)
                    rider.Player.IsControllable = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (rider != null && rider.Player != null)
                    rider.Player.IsControllable = true;
            }
            finally
            {
                if (version == this.m_AsyncVersion) this.m_IsHandlingCrash = false;
            }
        }

        private Vector3 CalculateEjectionVelocity(Collision collision, float severity)
        {
            Vector3 inheritedVelocity = this.m_BikeBody != null
                ? this.m_BikeBody.linearVelocity * this.m_InheritBikeVelocity
                : Vector3.zero;

            Vector3 away = -transform.forward;
            if (collision != null && collision.contactCount > 0)
            {
                away = collision.GetContact(0).normal;
            }
            away = Vector3.ProjectOnPlane(away, Vector3.up);
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            away.Normalize();

            float amount = Mathf.InverseLerp(
                this.m_MinEjectSeverity,
                Mathf.Max(this.m_MinEjectSeverity + 0.1f, this.m_MinEjectSeverity * 2f),
                severity
            );
            float upward = Mathf.Lerp(
                this.m_MinUpwardVelocity,
                this.m_MaxUpwardVelocity,
                amount
            );

            return inheritedVelocity +
                   Vector3.up * upward +
                   away * this.m_ImpactAwayVelocity;
        }

        private void ApplyEjectionVelocity(Character rider, Vector3 velocity)
        {
            Animator animator = rider.Animim.Animator;
            if (animator == null) return;

            Rigidbody[] bodies = animator.GetComponentsInChildren<Rigidbody>(true);
            foreach (Rigidbody body in bodies)
            {
                if (body == null || body.isKinematic) continue;
                body.linearVelocity = velocity;
            }

            Transform hips = animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : animator.transform;
            Rigidbody hipsBody = hips != null ? hips.GetComponent<Rigidbody>() : null;
            if (hipsBody != null && !hipsBody.isKinematic)
            {
                Vector3 tumbleAxis = Vector3.Cross(
                    Vector3.up,
                    Vector3.ProjectOnPlane(velocity, Vector3.up).normalized
                );
                if (tumbleAxis.sqrMagnitude < 0.001f) tumbleAxis = transform.right;
                hipsBody.AddTorque(
                    tumbleAxis.normalized * this.m_TumbleVelocity,
                    ForceMode.VelocityChange
                );
            }
        }

        private float CalculateBikeFallSign(Collision collision)
        {
            if (collision != null && collision.contactCount > 0)
            {
                float contactSide = Vector3.Dot(
                    collision.GetContact(0).normal,
                    transform.right
                );
                if (Mathf.Abs(contactSide) > 0.08f)
                    return contactSide > 0f ? 1f : -1f;
            }

            float lateralVelocity = this.m_BikeBody != null
                ? Vector3.Dot(this.m_BikeBody.linearVelocity, transform.right)
                : 0f;
            return lateralVelocity < 0f ? -1f : 1f;
        }

        private async Task WaitUnscaled(float duration, int version)
        {
            float deadline = Time.unscaledTime + Mathf.Max(0f, duration);
            while (version == this.m_AsyncVersion && Time.unscaledTime < deadline)
                await Task.Yield();
        }

        private bool IsCurrent(int version)
        {
            return this != null && this.isActiveAndEnabled &&
                   version == this.m_AsyncVersion;
        }

        private void ResolveReferences()
        {
            if (this.m_ImpactAudio == null)
                this.m_ImpactAudio = this.GetComponent<FranklinBikeImpactAudio>();
            if (this.m_Driver == null)
                this.m_Driver = this.GetComponent<FranklinArcadeBikeDriver>();
            if (this.m_BikeRagdoll == null)
                this.m_BikeRagdoll = this.GetComponent<FranklinArcadeBikeRagdoll>();
            if (this.m_BikeEntry == null)
                this.m_BikeEntry = this.GetComponent<BikeEntry>();
            if (this.m_BikeBody == null)
                this.m_BikeBody = this.GetComponent<Rigidbody>();
        }

        private void OnValidate()
        {
            this.m_MinEjectSeverity = Mathf.Max(0.1f, this.m_MinEjectSeverity);
            this.m_CrashCooldown = Mathf.Max(0f, this.m_CrashCooldown);
            this.m_MinUpwardVelocity = Mathf.Max(0f, this.m_MinUpwardVelocity);
            this.m_MaxUpwardVelocity = Mathf.Max(
                this.m_MinUpwardVelocity,
                this.m_MaxUpwardVelocity
            );
            this.m_ImpactAwayVelocity = Mathf.Max(0f, this.m_ImpactAwayVelocity);
            this.m_TumbleVelocity = Mathf.Max(0f, this.m_TumbleVelocity);
            this.m_BikeToppleAngularVelocity = Mathf.Max(
                0.1f,
                this.m_BikeToppleAngularVelocity
            );
            this.m_MinRagdollTime = Mathf.Max(0.25f, this.m_MinRagdollTime);
            this.ResolveReferences();
        }
    }
}
