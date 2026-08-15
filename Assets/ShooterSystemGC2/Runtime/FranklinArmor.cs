using System;
using GameCreator.Runtime.Characters;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>Immutable result returned after Armor processes incoming damage.</summary>
    public readonly struct FranklinArmorDamageResult
    {
        public float IncomingDamage { get; }
        public float ArmorDamage { get; }
        public float HealthDamage { get; }
        public float ArmorBefore { get; }
        public float ArmorAfter { get; }
        public float Absorption { get; }

        public bool WasProtected => this.ArmorDamage > 0f;
        public bool WasDepleted => this.ArmorBefore > 0f && this.ArmorAfter <= 0f;

        internal FranklinArmorDamageResult(
            float incomingDamage,
            float armorDamage,
            float healthDamage,
            float armorBefore,
            float armorAfter,
            float absorption)
        {
            this.IncomingDamage = incomingDamage;
            this.ArmorDamage = armorDamage;
            this.HealthDamage = healthDamage;
            this.ArmorBefore = armorBefore;
            this.ArmorAfter = armorAfter;
            this.Absorption = absorption;
        }

        internal static FranklinArmorDamageResult Unprotected(float incomingDamage)
        {
            float damage = Mathf.Max(0f, incomingDamage);
            return new FranklinArmorDamageResult(damage, 0f, damage, 0f, 0f, 0f);
        }
    }

    /// <summary>
    /// Runtime Armor reserve for a GC2 Character. Armor never creates free mitigation:
    /// every prevented health point consumes one Armor point.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
    public sealed class FranklinArmor : MonoBehaviour
    {
        private const float EPSILON = 0.0001f;

        [SerializeField, Min(1f)] private float m_MaxArmor = 100f;
        [SerializeField, Min(0f)] private float m_StartingArmor;

        [NonSerialized] private float m_CurrentArmor;
        [NonSerialized] private bool m_IsInitialized;

        public float CurrentArmor
        {
            get
            {
                this.EnsureInitialized();
                return this.m_CurrentArmor;
            }
        }

        public float MaxArmor => Mathf.Max(1f, this.m_MaxArmor);
        public float StartingArmor => Mathf.Clamp(this.m_StartingArmor, 0f, this.MaxArmor);
        public float NormalizedArmor => Mathf.Clamp01(this.CurrentArmor / this.MaxArmor);
        public bool IsWearingArmor => this.CurrentArmor > EPSILON;

        public event Action<float, float> EventArmorChanged;
        public event Action<FranklinArmorDamageResult> EventDamageAbsorbed;
        public event Action EventArmorDepleted;

        private void Awake()
        {
            this.EnsureInitialized();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_MaxArmor = Mathf.Max(1f, this.m_MaxArmor);
            this.m_StartingArmor = Mathf.Clamp(
                this.m_StartingArmor,
                0f,
                this.m_MaxArmor
            );
        }
#endif

        /// <summary>Equips a new Armor reserve and optionally changes its capacity.</summary>
        public void Equip(float armorPoints, float maxArmor = 100f)
        {
            this.EnsureInitialized();
            float previousMax = this.MaxArmor;
            this.m_MaxArmor = Mathf.Max(1f, maxArmor);
            float value = Mathf.Clamp(armorPoints, 0f, this.MaxArmor);
            if (Mathf.Abs(this.m_CurrentArmor - value) > EPSILON)
            {
                this.SetArmorInternal(value);
            }
            else if (Mathf.Abs(previousMax - this.MaxArmor) > EPSILON)
            {
                this.EventArmorChanged?.Invoke(this.m_CurrentArmor, this.MaxArmor);
            }
        }

        /// <summary>Sets the current Armor reserve, clamped to zero and MaxArmor.</summary>
        public void SetArmor(float armorPoints)
        {
            this.EnsureInitialized();
            this.SetArmorInternal(Mathf.Clamp(armorPoints, 0f, this.MaxArmor));
        }

        /// <summary>Adds Armor and returns the amount actually restored.</summary>
        public float AddArmor(float armorPoints)
        {
            if (armorPoints <= 0f) return 0f;
            float before = this.CurrentArmor;
            this.SetArmor(before + armorPoints);
            return this.CurrentArmor - before;
        }

        /// <summary>Removes all Armor without removing the component.</summary>
        public void RemoveArmor()
        {
            this.SetArmor(0f);
        }

        /// <summary>
        /// Consumes Armor according to the weapon's absorption fraction and returns the
        /// remainder that must be subtracted from Character health.
        /// </summary>
        public FranklinArmorDamageResult AbsorbDamage(
            float incomingDamage,
            float absorption)
        {
            float damage = Mathf.Max(0f, incomingDamage);
            float ratio = Mathf.Clamp01(absorption);
            float before = this.CurrentArmor;
            if (damage <= 0f || ratio <= 0f || before <= EPSILON)
                return FranklinArmorDamageResult.Unprotected(damage);

            float armorDamage = Mathf.Min(before, damage * ratio);
            this.SetArmorInternal(before - armorDamage);

            FranklinArmorDamageResult result = new(
                damage,
                armorDamage,
                Mathf.Max(0f, damage - armorDamage),
                before,
                this.m_CurrentArmor,
                ratio
            );
            this.EventDamageAbsorbed?.Invoke(result);
            if (result.WasDepleted) this.EventArmorDepleted?.Invoke();
            return result;
        }

        private void EnsureInitialized()
        {
            if (this.m_IsInitialized) return;
            this.m_MaxArmor = Mathf.Max(1f, this.m_MaxArmor);
            this.m_CurrentArmor = Mathf.Clamp(
                this.m_StartingArmor,
                0f,
                this.m_MaxArmor
            );
            this.m_IsInitialized = true;
        }

        private void SetArmorInternal(float armorPoints)
        {
            float value = Mathf.Clamp(armorPoints, 0f, this.MaxArmor);
            if (Mathf.Abs(this.m_CurrentArmor - value) <= EPSILON) return;
            this.m_CurrentArmor = value;
            this.EventArmorChanged?.Invoke(this.m_CurrentArmor, this.MaxArmor);
        }
    }

    /// <summary>
    /// Public entry point used by pickups, shops, missions and other runtime systems.
    /// All target methods accept a Character root or any GameObject below that Character.
    /// </summary>
    public static class FranklinArmorAPI
    {
        public static FranklinArmor Get(GameObject target)
        {
            GameObject host = ResolveCharacterHost(target);
            return host != null ? host.GetComponent<FranklinArmor>() : null;
        }

        public static bool TryGet(GameObject target, out FranklinArmor armor)
        {
            armor = Get(target);
            return armor != null;
        }

        public static FranklinArmor Ensure(GameObject target)
        {
            GameObject host = ResolveCharacterHost(target);
            if (host == null) return null;
            return host.GetComponent<FranklinArmor>() ?? host.AddComponent<FranklinArmor>();
        }

        public static FranklinArmor Equip(
            GameObject target,
            float armorPoints = 100f,
            float maxArmor = 100f)
        {
            FranklinArmor armor = Ensure(target);
            if (armor == null) return null;
            armor.Equip(armorPoints, maxArmor);
            return armor;
        }

        public static float AddArmor(GameObject target, float armorPoints)
        {
            FranklinArmor armor = Ensure(target);
            return armor != null ? armor.AddArmor(armorPoints) : 0f;
        }

        public static bool SetArmor(GameObject target, float armorPoints)
        {
            FranklinArmor armor = Ensure(target);
            if (armor == null) return false;
            armor.SetArmor(armorPoints);
            return true;
        }

        public static bool RemoveArmor(GameObject target)
        {
            FranklinArmor armor = Get(target);
            if (armor == null) return false;
            armor.RemoveArmor();
            return true;
        }

        public static float GetCurrentArmor(GameObject target)
        {
            FranklinArmor armor = Get(target);
            return armor != null ? armor.CurrentArmor : 0f;
        }

        public static FranklinArmorDamageResult AbsorbDamage(
            GameObject target,
            float incomingDamage,
            float absorption)
        {
            FranklinArmor armor = Get(target);
            return armor != null
                ? armor.AbsorbDamage(incomingDamage, absorption)
                : FranklinArmorDamageResult.Unprotected(incomingDamage);
        }

        private static GameObject ResolveCharacterHost(GameObject target)
        {
            if (target == null) return null;
            Character character = target.GetComponentInParent<Character>() ??
                                  target.GetComponentInChildren<Character>(true);
            return character != null ? character.gameObject : null;
        }
    }
}
