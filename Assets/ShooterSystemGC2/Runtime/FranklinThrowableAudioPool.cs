using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// A fixed-size 3D audio pool for uncommon throwable effects. It never grows at runtime and
    /// does not create or destroy an AudioSource for each grenade.
    /// </summary>
    [DefaultExecutionOrder(640)]
    internal sealed class FranklinThrowableAudioPool : MonoBehaviour
    {
        private const int SOURCE_COUNT = 4;

        private sealed class Slot
        {
            public AudioSource Source;
            public uint Serial;
        }

        private static FranklinThrowableAudioPool s_Instance;
        private static uint s_Serial;

        private readonly Slot[] m_Slots = new Slot[SOURCE_COUNT];

        public static void Play(
            Vector3 position,
            AudioClip clip,
            float volume = 1f,
            float pitch = 1f,
            float maxDistance = 36f,
            float minDistance = 2f)
        {
            if (clip == null) return;
            EnsureInstance();
            s_Instance?.PlayInternal(
                position,
                clip,
                volume,
                pitch,
                maxDistance,
                minDistance
            );
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
            s_Serial = 0;
        }

        private static void EnsureInstance()
        {
            if (s_Instance != null) return;
            GameObject root = new("Franklin Throwable Audio Pool");
            DontDestroyOnLoad(root);
            s_Instance = root.AddComponent<FranklinThrowableAudioPool>();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            for (int i = 0; i < this.m_Slots.Length; ++i)
            {
                GameObject child = new($"Throwable Audio {i + 1}");
                child.transform.SetParent(this.transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 1f;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 2f;
                source.maxDistance = 36f;
                source.priority = 160;
                this.m_Slots[i] = new Slot { Source = source };
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        private void PlayInternal(
            Vector3 position,
            AudioClip clip,
            float volume,
            float pitch,
            float maxDistance,
            float minDistance)
        {
            Slot selected = this.m_Slots[0];
            for (int i = 0; i < this.m_Slots.Length; ++i)
            {
                Slot candidate = this.m_Slots[i];
                if (candidate?.Source == null) continue;
                if (!candidate.Source.isPlaying)
                {
                    selected = candidate;
                    break;
                }

                if (selected == null || candidate.Serial < selected.Serial)
                    selected = candidate;
            }

            if (selected?.Source == null) return;
            AudioSource source = selected.Source;
            source.Stop();
            source.transform.position = position;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            source.minDistance = Mathf.Max(0.1f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 1f, maxDistance);
            selected.Serial = ++s_Serial;
            source.Play();
        }
    }
}
