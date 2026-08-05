using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.Audio;

namespace GameCreator.Runtime.VisualScripting
{
    [Version(0, 2, 0)]

    [Title("Audio Mixer Group Volume")]
    [Description("Sets the volume (in dB) of an assigned Audio Mixer Group using an exposed parameter, with optional fade")]

    [Category("Audio/Audio Mixer Group Volume")]

    [Parameter("Audio Mixer Group", "The Audio Mixer Group to control")]
    [Parameter("Parameter Name", "The name of the exposed parameter controlling volume")]
    [Parameter("Volume Value", "The value (in dB) to which the volume is set")]
    [Parameter("Fade Duration", "Time in seconds to fade from the current volume to the target volume. 0 = instant")]

    [Keywords("Audio", "Volume", "Mixer", "Group", "Control", "Fade")]
    [Image(typeof(IconAudioMixer), ColorTheme.Type.Yellow)]

    [Serializable]
    public class InstructionAudioMixerGroupVolume : Instruction
    {
        // The Audio Mixer Group assigned in the Inspector
        [SerializeField] private AudioMixerGroup m_AudioMixerGroup;

        // The name of the exposed parameter on the mixer (e.g. "Volume (Master)" or "Volume")
        [SerializeField] private PropertyGetString m_ParameterName = new PropertyGetString("Volume");

        // The target value (in decibels) to set for the parameter
        [SerializeField] private PropertyGetDecimal m_VolumeValue = new PropertyGetDecimal(0f);

        // How long it takes to fade from the current volume to the target volume (in seconds)
        [SerializeField] private float m_FadeDuration = 0f;

        public override string Title => string.Format(
            "Audio Mixer Group {0} set '{1}' = {2} dB",
            this.m_AudioMixerGroup != null ? this.m_AudioMixerGroup.name : "(none)",
            this.m_ParameterName,
            this.m_VolumeValue
        );

        protected override async Task Run(Args args)
        {
            if (this.m_AudioMixerGroup == null)
            {
                Debug.LogWarning("No AudioMixerGroup assigned to this instruction.");
                return;
            }

            // Retrieve the Audio Mixer asset from the group
            AudioMixer mixer = this.m_AudioMixerGroup.audioMixer;
            if (mixer == null)
            {
                Debug.LogWarning($"AudioMixerGroup '{this.m_AudioMixerGroup.name}' does not have a valid AudioMixer assigned.");
                return;
            }

            // Get the actual parameter name & target value
            string parameterName = this.m_ParameterName.Get(args);
            float targetValue = (float)this.m_VolumeValue.Get(args);

            // Check if the parameter actually exists by trying to get its current value
            float currentValue;
            bool found = mixer.GetFloat(parameterName, out currentValue);
            if (!found)
            {
                Debug.LogWarning($"Exposed parameter '{parameterName}' not found in AudioMixer '{mixer.name}'.");
                return;
            }

            // If no fade is desired or fade duration <= 0, set volume immediately
            if (this.m_FadeDuration <= 0f)
            {
                mixer.SetFloat(parameterName, targetValue);
                return;
            }

            // Otherwise, fade from currentValue to targetValue over m_FadeDuration seconds
            float duration = this.m_FadeDuration;
            float timeElapsed = 0f;

            while (timeElapsed < duration)
            {
                timeElapsed += 0.1f; // or however many seconds per step
                float t = Mathf.Clamp01(timeElapsed / duration);

                float newValue = Mathf.Lerp(currentValue, targetValue, t);
                mixer.SetFloat(parameterName, newValue);

                // Wait 100 ms between each iteration
                await Task.Delay(100);
            }

            mixer.SetFloat(parameterName, targetValue);
        }
    }
}
