using System;
using System.Threading.Tasks;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using GameCreator.Runtime.Stats;
using GameCreator.Runtime.VisualScripting;
using PGBloodFactory = PampelGames.BloodFactory.BloodFactory;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Applies per-weapon Shooter damage to the GC2 Traits hp Attribute. Head hits are
    /// resolved from an authored Head collider when available and otherwise from the
    /// actual Shooter hit point around the Humanoid Head bone.
    /// </summary>
    [Version(1, 0, 0)]
    [Title("Damage Franklin Character")]
    [Category("Shooter/Characters/Damage Franklin Character")]
    [Description("Damages a Character's GC2 Traits hp with headshot and Helmet protection")]
    [Parameter("Body Damage", "Health removed by a normal projectile or pellet hit")]
    [Parameter("Head Multiplier", "Multiplier applied when the hit reaches the Head bone")]
    [Parameter("Helmet Multiplier", "Fraction of headshot damage applied through a Helmet")]
    [Parameter("Helmet Impulse", "Impulse used to knock the Helmet away on the first head hit")]
    [Parameter("Armor Absorption", "Fraction of damage Armor can absorb for this weapon")]
    [Keywords("Character", "Traits", "Health", "HP", "Damage", "Headshot", "Helmet")]
    [Serializable]
    public sealed class InstructionFranklinCharacterDamage : Instruction
    {
        private const float DIRECTION_EPSILON = 0.0001f;
        private const float BLOOD_HIT_INTERVAL = 0.04f;
        private const int BLOOD_COLLISION_MASK =
            (1 << 0) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10);

        [SerializeField] private string m_HealthAttributeId = "hp";
        [SerializeField, Min(0f)] private float m_BodyDamage = 25f;
        [SerializeField, Min(1f)] private float m_HeadMultiplier = 3f;
        [SerializeField, Range(0f, 1f)] private float m_HelmetMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float m_HelmetImpulse = 8f;
        [SerializeField, Range(0f, 1f)] private float m_ArmorAbsorption = 0.65f;
        [SerializeField] private GameObject m_BloodHitEffect;
        [SerializeField, Min(0.1f)] private float m_BloodEffectLifetime = 4f;

        [NonSerialized] private bool m_HasWarnedMissingHealth;
        [NonSerialized] private int m_LastBloodVictimId;
        [NonSerialized] private float m_LastBloodTime = float.NegativeInfinity;

        public float BodyDamage => Mathf.Max(0f, this.m_BodyDamage);
        public float HeadMultiplier => Mathf.Max(1f, this.m_HeadMultiplier);
        public float HelmetMultiplier => Mathf.Clamp01(this.m_HelmetMultiplier);
        public float HelmetImpulse => Mathf.Max(0f, this.m_HelmetImpulse);
        public float ArmorAbsorption => Mathf.Clamp01(this.m_ArmorAbsorption);
        public GameObject BloodHitEffect => this.m_BloodHitEffect;
        public float BloodEffectLifetime => Mathf.Max(0.1f, this.m_BloodEffectLifetime);

        public override string Title =>
            $"Damage Character {this.BodyDamage:0.##} (Head x{this.HeadMultiplier:0.##})";

        public InstructionFranklinCharacterDamage()
        { }

        public InstructionFranklinCharacterDamage(
            float bodyDamage,
            float headMultiplier,
            float helmetMultiplier,
            float helmetImpulse,
            float armorAbsorption,
            GameObject bloodHitEffect)
        {
            this.m_BodyDamage = Mathf.Max(0f, bodyDamage);
            this.m_HeadMultiplier = Mathf.Max(1f, headMultiplier);
            this.m_HelmetMultiplier = Mathf.Clamp01(helmetMultiplier);
            this.m_HelmetImpulse = Mathf.Max(0f, helmetImpulse);
            this.m_ArmorAbsorption = Mathf.Clamp01(armorAbsorption);
            this.m_BloodHitEffect = bloodHitEffect;
        }

        protected override Task Run(Args args)
        {
            GameObject hitObject = args.Target;
            if (hitObject == null || this.BodyDamage <= 0f) return DefaultResult;

            Character victim = hitObject.GetComponentInParent<Character>();
            if (victim == null) return DefaultResult;

            Character shooter = args.Self != null
                ? args.Self.GetComponentInParent<Character>()
                : null;
            if (victim == shooter) return DefaultResult;

            Traits traits = victim.GetComponent<Traits>() ??
                            victim.GetComponentInChildren<Traits>(true);
            if (traits == null) return DefaultResult;

            RuntimeAttributeData health;
            try
            {
                health = traits.RuntimeAttributes.Get(this.m_HealthAttributeId);
            }
            catch (Exception exception)
            {
                if (!this.m_HasWarnedMissingHealth)
                {
                    this.m_HasWarnedMissingHealth = true;
                    Debug.LogWarning(
                        $"Shooter damage could not find Traits Attribute " +
                        $"'{this.m_HealthAttributeId}' on {victim.name}: {exception.Message}",
                        victim
                    );
                }
                return DefaultResult;
            }

            if (health == null || health.Value <= health.MinValue) return DefaultResult;

            Vector3 hitPoint = ShotData.LastHitPosition;
            this.SpawnBloodHit(args.Self, victim, hitPoint);
            bool headshot = IsHeadshot(hitObject, victim, hitPoint);
            float damage = this.BodyDamage * (headshot ? this.HeadMultiplier : 1f);

            if (headshot)
            {
                FranklinBikeHelmetController helmet =
                    victim.GetComponent<FranklinBikeHelmetController>() ??
                    victim.GetComponentInChildren<FranklinBikeHelmetController>(true);
                if (helmet != null && helmet.IsProtectingHead)
                {
                    damage *= this.HelmetMultiplier;
                    Vector3 direction = ResolveImpactDirection(args.Self, hitPoint);
                    Vector3 impulse = direction * this.HelmetImpulse +
                                      Vector3.up * (this.HelmetImpulse * 0.32f);
                    helmet.KnockOffHelmet(hitPoint, impulse);
                }
            }

            FranklinArmorDamageResult armorResult = FranklinArmorAPI.AbsorbDamage(
                victim.gameObject,
                damage,
                this.ArmorAbsorption
            );
            health.Value -= Mathf.Max(0f, armorResult.HealthDamage);
            return DefaultResult;
        }

        private void SpawnBloodHit(GameObject shooter, Character victim, Vector3 hitPoint)
        {
            if (this.m_BloodHitEffect == null) return;

            int victimId = victim.GetInstanceID();
            float now = UnityEngine.Time.time;
            if (this.m_LastBloodVictimId == victimId &&
                now - this.m_LastBloodTime < BLOOD_HIT_INTERVAL)
            {
                return;
            }

            this.m_LastBloodVictimId = victimId;
            this.m_LastBloodTime = now;

            Vector3 direction = ResolveImpactDirection(shooter, hitPoint);
            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(direction, up);
            GameObject effect = UnityEngine.Object.Instantiate(
                this.m_BloodHitEffect,
                hitPoint + direction * 0.015f,
                rotation
            );

            PGBloodFactory factory = effect.GetComponent<PGBloodFactory>() ??
                                     effect.GetComponentInChildren<PGBloodFactory>(true);
            if (factory == null)
            {
                UnityEngine.Object.Destroy(effect);
                return;
            }

            factory.executeOnAwake = false;
            factory.collisionLayer = (LayerMask) BLOOD_COLLISION_MASK;
            factory.Execute();
            UnityEngine.Object.Destroy(effect, this.BloodEffectLifetime);
        }

        private static bool IsHeadshot(
            GameObject hitObject,
            Character victim,
            Vector3 hitPoint)
        {
            string hitName = hitObject.name;
            if (hitName.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hitName.IndexOf("skull", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hitName.IndexOf("helmet", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            Animator animator = victim.Animim?.Animator;
            if (animator == null) animator = victim.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman) return false;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return false;

            Transform hitTransform = hitObject.transform;
            if (hitTransform == head || hitTransform.IsChildOf(head)) return true;

            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            float headRadius = neck != null
                ? Vector3.Distance(head.position, neck.position) * 1.65f
                : 0.26f;
            headRadius = Mathf.Clamp(headRadius, 0.2f, 0.36f);
            return (hitPoint - head.position).sqrMagnitude <= headRadius * headRadius;
        }

        private static Vector3 ResolveImpactDirection(GameObject shooter, Vector3 hitPoint)
        {
            Vector3 direction = ShotData.LastShooterDirection;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude > DIRECTION_EPSILON)
                return direction.normalized;

            if (shooter != null)
            {
                direction = hitPoint - shooter.transform.position;
                if (direction.sqrMagnitude > DIRECTION_EPSILON)
                    return direction.normalized;
            }

            return Vector3.forward;
        }
    }
}
