using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace FranklinGame.Shooter
{
    [Version(1, 0, 0)]
    [Title("Spawn Franklin Smoke Grenade")]
    [Category("Shooter/Spawn Franklin Smoke Grenade")]
    [Description("Spawns a pooled, mobile-budget smoke screen at the latest Shooter hit")]
    [Serializable]
    public sealed class InstructionFranklinSmokeGrenade : Instruction
    {
        [SerializeField, Min(1f)] private float m_Duration = 18f;
        [SerializeField, Min(0.5f)] private float m_Radius = 4.5f;
        [SerializeField] private AudioClip m_HissClip;

        public float Duration => Mathf.Max(1f, this.m_Duration);
        public float Radius => Mathf.Max(0.5f, this.m_Radius);
        public AudioClip HissClip => this.m_HissClip;
        public override string Title => $"Smoke {this.Radius:0.#}m / {this.Duration:0.#}s";

        public InstructionFranklinSmokeGrenade()
        { }

        public InstructionFranklinSmokeGrenade(
            float duration,
            float radius,
            AudioClip hissClip)
        {
            this.m_Duration = Mathf.Max(1f, duration);
            this.m_Radius = Mathf.Max(0.5f, radius);
            this.m_HissClip = hissClip;
        }

        protected override Task Run(Args args)
        {
            Vector3 position = ShotData.LastHitPosition;
            if (position == Vector3.zero && args.Target != null)
                position = args.Target.transform.position;

            FranklinSmokeCloudPool.Spawn(position, this.Duration, this.Radius);
            FranklinThrowableAudioPool.Play(position, this.m_HissClip, 0.72f, 0.92f, 28f);
            return DefaultResult;
        }
    }
}
