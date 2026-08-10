using System;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Bike-tuned light/heavy collision audio with the same pooled spark, flash and debris
    /// effect used by the Sim-Cade car.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FranklinBikeImpactAudio : SimcadeCarImpactAudio
    {
        /// <summary>
        /// Raised only for a cooldown-filtered impact classified as heavy by the
        /// bike profile. Severity is impulse per bike mass/contact speed.
        /// </summary>
        public event Action<Collision, float> EventHeavyImpact;

        public void ConfigureForBike(
            AudioSource audioSource,
            AudioClip lightImpactClip,
            AudioClip heavyImpactClip,
            GameObject impactEffectPrefab)
        {
            this.Configure(
                audioSource,
                lightImpactClip,
                heavyImpactClip,
                impactEffectPrefab
            );

            // The bike body is lighter and more exposed than a car shell. Use lower
            // normalized thresholds, a slightly brighter light hit and retain a deep
            // heavy hit without allowing multi-collider audio bursts.
            this.ConfigureImpactProfile(
                minContactSpeed: 0.85f,
                heavyImpactSpeed: 4.5f,
                maxImpactSpeed: 12f,
                impactCooldown: 0.14f,
                lightMinVolume: 0.35f,
                lightMaxVolume: 0.82f,
                lightPitch: 1.1f,
                heavyMinVolume: 0.82f,
                heavyMaxVolume: 1f,
                heavyPitch: 0.92f,
                lightSparkCount: 4,
                heavySparkCount: 12,
                heavyFlashCount: 3
            );
        }

        protected override void OnImpactAccepted(
            Collision collision,
            bool isHeavy,
            float severity)
        {
            base.OnImpactAccepted(collision, isHeavy, severity);
            if (isHeavy) this.EventHeavyImpact?.Invoke(collision, severity);
        }
    }
}
