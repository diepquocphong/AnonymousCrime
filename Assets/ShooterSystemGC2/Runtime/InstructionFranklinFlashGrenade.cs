using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace FranklinGame.Shooter
{
    [Version(1, 0, 0)]
    [Title("Spawn Franklin Flash Grenade")]
    [Category("Shooter/Spawn Franklin Flash Grenade")]
    [Description("Spawns a pooled mobile flash burst and screen flash at the latest Shooter hit")]
    [Serializable]
    public sealed class InstructionFranklinFlashGrenade : Instruction
    {
        [SerializeField, Min(0.1f)] private float m_Duration = 0.9f;
        [SerializeField, Min(1f)] private float m_Radius = 12f;
        [SerializeField] private AudioClip m_FlashClip;

        public float Duration => Mathf.Max(0.1f, this.m_Duration);
        public float Radius => Mathf.Max(1f, this.m_Radius);
        public AudioClip FlashClip => this.m_FlashClip;
        public override string Title => $"Flash {this.Radius:0.#}m / {this.Duration:0.#}s";

        public InstructionFranklinFlashGrenade()
        { }

        public InstructionFranklinFlashGrenade(float duration, float radius, AudioClip flashClip)
        {
            this.m_Duration = Mathf.Max(0.1f, duration);
            this.m_Radius = Mathf.Max(1f, radius);
            this.m_FlashClip = flashClip;
        }

        protected override Task Run(Args args)
        {
            Vector3 position = ShotData.LastHitPosition;
            if (position == Vector3.zero && args.Target != null)
                position = args.Target.transform.position;

            FranklinFlashGrenadePool.Spawn(position, this.Duration, this.Radius);
            FranklinThrowableAudioPool.Play(position, this.m_FlashClip, 0.9f, 1.28f, 34f);
            return DefaultResult;
        }
    }
}
