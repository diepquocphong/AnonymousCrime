using GameCreator.Runtime.Common;
using GameCreator.Runtime.Common.Audio;
using GameCreator.Runtime.Stats;
using UnityEngine;
using StatAttribute = GameCreator.Runtime.Stats.Attribute;

namespace FranklinGame.Melee
{
    /// <summary>
    /// Plays a short pain voice when the configured GC2 Traits Attribute decreases.
    /// This keeps damage audio on the damaged Character instead of the attacker.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Traits))]
    public sealed class FranklinDamagePainAudio : MonoBehaviour
    {
        [SerializeField] private Traits m_Traits;
        [SerializeField] private StatAttribute m_HealthAttribute;
        [SerializeField] private AudioClip[] m_PainVoices = new AudioClip[0];

        [Header("Pain Voice")]
        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.55f;
        [SerializeField] private Vector2 m_PitchRange = new Vector2(0.96f, 1.04f);
        [SerializeField, Min(0f)] private float m_MinInterval = 0.22f;

        private RuntimeAttributes m_RuntimeAttributes;
        private Args m_Args;
        private float m_NextPainTime;
        private int m_LastPainIndex = -1;

        private void Reset()
        {
            this.m_Traits = this.GetComponent<Traits>();
        }

        private void OnEnable()
        {
            if (this.m_Traits == null)
            {
                this.m_Traits = this.GetComponent<Traits>();
            }

            if (this.m_Traits == null || this.m_HealthAttribute == null) return;

            this.m_Args = new Args(this.gameObject);
            this.m_RuntimeAttributes = this.m_Traits.RuntimeAttributes;
            this.m_RuntimeAttributes.EventChange -= this.OnAttributeChange;
            this.m_RuntimeAttributes.EventChange += this.OnAttributeChange;
        }

        private void OnDisable()
        {
            if (this.m_RuntimeAttributes != null)
            {
                this.m_RuntimeAttributes.EventChange -= this.OnAttributeChange;
            }

            this.m_RuntimeAttributes = null;
            this.m_Args = null;
            this.m_NextPainTime = 0f;
            this.m_LastPainIndex = -1;
        }

        private void OnValidate()
        {
            this.m_Volume = Mathf.Clamp01(this.m_Volume);
            this.m_PitchRange.x = Mathf.Clamp(this.m_PitchRange.x, 0.5f, 1.5f);
            this.m_PitchRange.y = Mathf.Clamp(
                this.m_PitchRange.y,
                this.m_PitchRange.x,
                1.5f
            );
            this.m_MinInterval = Mathf.Max(0f, this.m_MinInterval);
        }

        private void OnAttributeChange(IdString attributeId)
        {
            if (this.m_RuntimeAttributes == null || this.m_HealthAttribute == null) return;
            if (attributeId.Hash != this.m_HealthAttribute.ID.Hash) return;
            if (this.m_RuntimeAttributes.LastChange >= -double.Epsilon) return;
            if (Time.unscaledTime < this.m_NextPainTime) return;

            AudioClip painVoice = this.SelectPainVoice();
            if (painVoice == null) return;

            this.m_NextPainTime = Time.unscaledTime + this.m_MinInterval;
            AudioConfigSoundEffect config = AudioConfigSoundEffect.Create(
                this.m_Volume,
                this.m_PitchRange,
                0f,
                TimeMode.UpdateMode.UnscaledTime,
                SpatialBlending.Spatial,
                this.gameObject
            );

            _ = AudioManager.Instance.SoundEffect.Play(
                painVoice,
                config,
                this.m_Args
            );
        }

        private AudioClip SelectPainVoice()
        {
            int availableCount = 0;
            for (int i = 0; i < this.m_PainVoices.Length; ++i)
            {
                if (this.m_PainVoices[i] == null || i == this.m_LastPainIndex) continue;
                availableCount += 1;
            }

            if (availableCount == 0)
            {
                return this.m_LastPainIndex >= 0 &&
                       this.m_LastPainIndex < this.m_PainVoices.Length
                    ? this.m_PainVoices[this.m_LastPainIndex]
                    : null;
            }

            int selectedCandidate = Random.Range(0, availableCount);
            for (int i = 0; i < this.m_PainVoices.Length; ++i)
            {
                if (this.m_PainVoices[i] == null || i == this.m_LastPainIndex) continue;
                if (selectedCandidate-- > 0) continue;

                this.m_LastPainIndex = i;
                return this.m_PainVoices[i];
            }

            return null;
        }
    }
}
