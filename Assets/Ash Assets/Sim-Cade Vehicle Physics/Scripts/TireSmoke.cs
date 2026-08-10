using UnityEngine;

namespace Ashsvp
{
    public class TireSmoke : MonoBehaviour
    {
        private ParticleSystem smoke;
        private float baseEmissionRate;
        private float lastIntensity = -1f;

        private void Awake()
        {
            smoke = GetComponent<ParticleSystem>();
            baseEmissionRate = smoke.emission.rateOverTimeMultiplier;
            smoke.Stop();
        }

        public void playSmoke()
        {
            playSmoke(1f);
        }

        public void playSmoke(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (intensity <= 0.001f)
            {
                stopSmoke();
                return;
            }

            if (Mathf.Abs(intensity - lastIntensity) > 0.005f)
            {
                ParticleSystem.EmissionModule emission = smoke.emission;
                emission.rateOverTimeMultiplier = baseEmissionRate * intensity;
                lastIntensity = intensity;
            }

            if (!smoke.isPlaying) smoke.Play();
        }

        public void stopSmoke()
        {
            if (smoke.isPlaying) smoke.Stop();
        }
    }
}
