using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Melee;
using GameCreator.Runtime.Shooter;
using UnityEngine;
using UnityEngine.Serialization;

namespace FranklinGame.Melee
{
    /// <summary>
    /// Equips the GC2 unarmed weapon, activates combat locomotion from Fight input and
    /// chooses the least-used attack from keys A-H. GC2 selects the corresponding
    /// single-animation Skill from its Combos asset.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
    public sealed class FranklinMeleeController : MonoBehaviour
    {
        private const float BASE_SPEED_ANTICIPATION = 0.6f;
        private const float BASE_SPEED_STRIKE = 1.4f / 3f;
        private const float BASE_SPEED_RECOVERY = 1f;
        private const float BASE_REACTION_TRANSITION_IN = 0.08f;
        private const float BASE_REACTION_TRANSITION_OUT = 0.12f;

        private static readonly FieldInfo SKILL_SPEED_ANTICIPATION =
            typeof(Skill).GetField(
                "m_SpeedAnticipation",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo SKILL_SPEED_STRIKE =
            typeof(Skill).GetField(
                "m_SpeedStrike",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo SKILL_SPEED_RECOVERY =
            typeof(Skill).GetField(
                "m_SpeedRecovery",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo SKILL_TRAIL_IS_ACTIVE =
            typeof(SkillTrail).GetField(
                "m_IsActive",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo REACTION_SPEED =
            typeof(Reaction).GetField(
                "m_Speed",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo REACTION_TRANSITION_IN =
            typeof(Reaction).GetField(
                "m_TransitionIn",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo REACTION_TRANSITION_OUT =
            typeof(Reaction).GetField(
                "m_TransitionOut",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        private static readonly MeleeKey[] ATTACK_KEYS =
        {
            MeleeKey.A,
            MeleeKey.B,
            MeleeKey.C,
            MeleeKey.D,
            MeleeKey.E,
            MeleeKey.F,
            MeleeKey.G,
            MeleeKey.H
        };

        [SerializeField] private Character m_Character;
        [SerializeField] private MeleeWeapon m_UnarmedWeapon;
        [SerializeField] private Skill[] m_AttackSkills = Array.Empty<Skill>();
        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("Global multiplier for all attack Skill phase speeds. 1 is the authored speed.")]
        private float m_GlobalSkillSpeed = 1f;
        [SerializeField]
        [Tooltip("Master trail switch for all attack Skills. Enabled keeps each Skill's authored trail setting.")]
        private bool m_UseGlobalSkillTrail = true;
        [SerializeField, Min(0f)] private float m_AttackMovementSpeed = 0.5f;
        [SerializeField, Min(0.05f)] private float m_InputBuffer = 1.65f;
        [SerializeField, Min(0.25f)] private float m_AttackStateTimeout = 3.3f;
        [SerializeField] private bool m_EquipOnStart = true;

        [Header("Unarmed Combat Locomotion")]
        [SerializeField] private StateBasicLocomotion m_UnarmedCombatLocomotion;
        [SerializeField, Min(0)] private int m_CombatLocomotionLayer = 1;
        [SerializeField, Min(0)]
        [Tooltip("Jog/Sprint State layer. Any active State here immediately cancels combat locomotion.")]
        private int m_JogSprintLayer = 2;
        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds without another Fight input before combat locomotion returns to Walk.")]
        private float m_CombatLocomotionIdleTimeout = 2f;
        [SerializeField, Range(0f, 0.5f)]
        private float m_CombatLocomotionTransition = 0.15f;

        [Header("Sidestep Dodge")]
        [SerializeField] private AnimationClip m_SidestepLeft;
        [SerializeField] private AnimationClip m_SidestepRight;
        [SerializeField, Min(0.1f)] private float m_SidestepVelocity = 2f;
        [SerializeField, Min(0.05f)] private float m_SidestepDuration = 0.3f;
        [SerializeField, Range(0f, 1f)] private float m_SidestepGravity = 1f;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Invincibility window at the start of a GC2 sidestep dodge.")]
        private float m_SidestepInvincibility = 0.16f;
        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("Multiplier applied after matching the clip to Sidestep Duration.")]
        private float m_SidestepAnimationSpeed = 0.5f;
        [SerializeField, Range(0f, 0.5f)] private float m_SidestepTransitionIn = 0.08f;
        [SerializeField, Range(0f, 0.5f)] private float m_SidestepTransitionOut = 0.12f;

        [Header("Global Melee Animation Ease")]
        [SerializeField] private bool m_UseAnimationEase = true;
        [SerializeField] private Easing.Type m_AnimationEase = Easing.Type.QuadInOut;
        [SerializeField, Min(0f)] private float m_EaseInDuration = 0.3f;
        [SerializeField, Min(0f)] private float m_EaseOutDuration = 0.42f;
        [SerializeField, Range(0.05f, 1f)]
        private float m_EaseEdgeSpeedMultiplier = 0.6f;

        [Header("Global Melee Hit Reaction Ease")]
        [SerializeField, HideInInspector] private MeleeReaction m_HitReaction;
        [SerializeField, HideInInspector] private AnimationClip[] m_ReactionAnimations =
            Array.Empty<AnimationClip>();
        [SerializeField] private bool m_UseReactionEase = true;
        [SerializeField] private Easing.Type m_ReactionEase = Easing.Type.QuadInOut;
        [SerializeField, Min(0f)] private float m_ReactionEaseInDuration = 0.08f;
        [SerializeField, Min(0f)] private float m_ReactionEaseOutDuration = 0.12f;
        [SerializeField, Range(0.05f, 1f)]
        private float m_ReactionEdgeSpeedMultiplier = 0.65f;
        [FormerlySerializedAs("m_ReactionBaseSpeed")]
        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("Global multiplier for reaction clips, state duration, transitions and ease. 1 is the authored speed.")]
        private float m_GlobalReactionSpeed = 1f;

        [Header("Mobile Performance")]
        [SerializeField, Range(15, 60)] private int m_EaseUpdatesPerSecond = 30;

        private Task m_EquipTask;
        private Coroutine m_ResetAttackRoutine;
        private Coroutine m_AttackEaseRoutine;
        private Coroutine m_AttackMovementRoutine;
        private Coroutine m_ReactionEaseRoutine;
        private Coroutine m_CombatLocomotionRoutine;
        private readonly int[] m_SkillUsageCounts = new int[ATTACK_KEYS.Length];
        private readonly int[] m_SkillSelectionCandidates = new int[ATTACK_KEYS.Length];
        private bool[] m_AuthoredSkillTrailStates = Array.Empty<bool>();
        private bool m_HasCachedSkillTrailStates;
        private int m_LastAttackIndex = -1;
        private Args m_SelfArgs;
        private MeleeStance m_MeleeStance;
        private MeleePhase m_PreviousMeleePhase = MeleePhase.None;
        private float[] m_ReactionDurations = Array.Empty<float>();
        private float m_PreAttackMovementSpeed;
        private bool m_IsAttackMovementLimited;
        private bool m_IsCombatLocomotionActive;
        private bool m_IsSidestepping;

        public bool IsReady =>
            this.m_Character != null &&
            this.m_UnarmedWeapon != null &&
            this.m_Character.Combat.IsEquipped(this.m_UnarmedWeapon) &&
            !this.HasShooterWeaponEquipped;

        private bool HasShooterWeaponEquipped =>
            this.m_Character?.Combat.GetActiveWeapon<ShooterWeapon>() != null;

        public float GlobalSkillSpeed
        {
            get => this.m_GlobalSkillSpeed;
            set
            {
                this.m_GlobalSkillSpeed = Mathf.Clamp(value, 0.1f, 3f);
                this.ApplyGlobalSkillSpeed();
                this.ApplyEffectiveInputBuffer();
            }
        }

        public bool UseGlobalSkillTrail
        {
            get => this.m_UseGlobalSkillTrail;
            set
            {
                this.m_UseGlobalSkillTrail = value;
                this.ApplyGlobalSkillTrail();
            }
        }

        public float GlobalReactionSpeed
        {
            get => this.m_GlobalReactionSpeed;
            set
            {
                this.m_GlobalReactionSpeed = Mathf.Clamp(value, 0.1f, 3f);
                this.ApplyGlobalReactionSpeed();
                this.CacheReactionDurations();
            }
        }

        private float EffectiveInputBuffer =>
            this.m_InputBuffer / Mathf.Max(this.m_GlobalSkillSpeed, 0.1f);

        private float EffectiveAttackTimeout =>
            this.m_AttackStateTimeout / Mathf.Max(this.m_GlobalSkillSpeed, 0.1f);

        private void Reset()
        {
            this.m_Character = this.GetComponent<Character>();
        }

        private void Awake()
        {
            if (this.m_Character == null)
            {
                this.m_Character = this.GetComponent<Character>();
            }

            if (this.m_Character != null)
            {
                this.m_SelfArgs = new Args(this.m_Character.gameObject);
                this.m_MeleeStance =
                    this.m_Character.Combat.RequestStance<MeleeStance>();
            }

            this.ApplyGlobalSkillSpeed();
            this.ApplyGlobalSkillTrail();
            this.ApplyGlobalReactionSpeed();
            this.ApplyEffectiveInputBuffer();
            this.CacheReactionDurations();
        }

        private void OnEnable()
        {
            if (this.m_Character == null)
            {
                this.m_Character = this.GetComponent<Character>();
            }

            if (this.m_Character == null) return;

            this.m_Character.Combat.EventEquip -= this.OnWeaponEquipped;
            this.m_Character.Combat.EventEquip += this.OnWeaponEquipped;

            if (this.HasShooterWeaponEquipped)
            {
                this.CancelAllMeleeStates();
            }
        }

        private void OnValidate()
        {
            this.m_GlobalSkillSpeed = Mathf.Clamp(this.m_GlobalSkillSpeed, 0.1f, 3f);
            this.m_GlobalReactionSpeed = Mathf.Clamp(
                this.m_GlobalReactionSpeed,
                0.1f,
                3f
            );
            if (!Application.isPlaying) return;

            this.ApplyGlobalSkillSpeed();
            this.ApplyGlobalSkillTrail();
            this.ApplyGlobalReactionSpeed();
            this.ApplyEffectiveInputBuffer();
            this.CacheReactionDurations();
        }

        private void Update()
        {
            if (this.m_MeleeStance == null) return;

            MeleePhase phase = this.m_MeleeStance.CurrentPhase;
            if (!this.m_UseReactionEase)
            {
                this.StopReactionEase();
                this.m_PreviousMeleePhase = phase;
                return;
            }

            if (phase == MeleePhase.Reaction &&
                this.m_PreviousMeleePhase != MeleePhase.Reaction)
            {
                this.RestartReactionEase();
            }
            else if (phase != MeleePhase.Reaction &&
                     this.m_PreviousMeleePhase == MeleePhase.Reaction)
            {
                this.StopReactionEase();
            }

            this.m_PreviousMeleePhase = phase;
        }

        private void LateUpdate()
        {
            if (!this.m_IsCombatLocomotionActive ||
                !this.IsJogSprintStateActive())
            {
                return;
            }

            this.CancelCombatLocomotionTimeout();
            this.ReleaseAttackMovementLimitForJogSprint();
            this.StopCombatLocomotion(this.m_CombatLocomotionTransition);
        }

        private bool IsJogSprintStateActive()
        {
            return this.m_Character?.States != null &&
                   !this.m_Character.States.IsAvailable(this.m_JogSprintLayer);
        }

        private async void Start()
        {
            if (!this.m_EquipOnStart) return;

            try
            {
                await this.EnsureEquipped();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>Called by the mobile Fight button.</summary>
        public async void Fight()
        {
            if (!this.isActiveAndEnabled) return;

            if (this.HasShooterWeaponEquipped)
            {
                this.CancelAllMeleeStates();
                return;
            }

            try
            {
                await this.EnsureEquipped();
                if (!this.IsReady) return;

                this.ActivateCombatLocomotion();
                this.RestartCombatLocomotionTimeout();

                MeleeStance stance = this.m_MeleeStance ??
                    this.m_Character.Combat.RequestStance<MeleeStance>();
                stance.BufferWindow = this.EffectiveInputBuffer;

                int attackIndex = this.SelectLeastUsedAttack();
                if (attackIndex < 0) return;

                MeleeKey attackKey = ATTACK_KEYS[attackIndex];

                stance.InputExecute(attackKey);
                this.RestartAttackStateTimeout();
                this.RestartAttackEase(attackIndex, stance);
                this.RestartAttackMovementLimit(stance);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>Called by the mobile Sidestep Left button.</summary>
        public void SidestepLeft()
        {
            this.StartSidestep(-1f, this.m_SidestepLeft);
        }

        /// <summary>Called by the mobile Sidestep Right button.</summary>
        public void SidestepRight()
        {
            this.StartSidestep(1f, this.m_SidestepRight);
        }

        private async void StartSidestep(float directionSign, AnimationClip animationClip)
        {
            if (!this.isActiveAndEnabled || animationClip == null ||
                this.m_Character == null || this.m_IsSidestepping ||
                this.HasShooterWeaponEquipped)
            {
                return;
            }

            bool ownsSidestep = false;
            try
            {
                await this.EnsureEquipped();
                if (!this.IsReady || this.m_IsSidestepping ||
                    this.HasShooterWeaponEquipped)
                {
                    return;
                }

                MeleeStance stance = this.m_MeleeStance ??
                    this.m_Character.Combat.RequestStance<MeleeStance>();
                if (stance.CurrentPhase != MeleePhase.None ||
                    !this.m_Character.Dash.CanDash())
                {
                    return;
                }

                this.ActivateCombatLocomotion();
                this.RestartCombatLocomotionTimeout();

                Vector3 direction = this.m_Character.transform.right * directionSign;
                float duration = Mathf.Max(this.m_SidestepDuration, 0.05f);
                float transitionOut = Mathf.Min(this.m_SidestepTransitionOut, duration);
                float animationSpeed = Mathf.Max(
                    animationClip.length / duration * this.m_SidestepAnimationSpeed,
                    0.1f
                );

                this.m_IsSidestepping = true;
                ownsSidestep = true;
                Task dashTask = this.m_Character.Dash.Execute(
                    direction,
                    Mathf.Max(this.m_SidestepVelocity, 0.1f),
                    this.m_SidestepGravity,
                    duration,
                    transitionOut
                );
                this.m_Character.Busy.MakeLegsBusy();
                if (this.m_SidestepInvincibility > 0f)
                {
                    this.m_Character.Combat.Invincibility.Set(
                        Mathf.Min(this.m_SidestepInvincibility, duration)
                    );
                }

                ConfigGesture config = new ConfigGesture(
                    0f,
                    animationClip.length,
                    animationSpeed,
                    false,
                    Mathf.Min(this.m_SidestepTransitionIn, duration),
                    transitionOut
                );
                _ = this.m_Character.Gestures.CrossFade(
                    animationClip,
                    null,
                    BlendMode.Blend,
                    config,
                    true
                );

                await dashTask;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                if (ownsSidestep) this.m_IsSidestepping = false;
            }
        }

        private int SelectLeastUsedAttack()
        {
            int availableSkills = 0;
            for (int i = 0; i < ATTACK_KEYS.Length; ++i)
            {
                if (this.IsAttackSkillAvailable(i)) availableSkills += 1;
            }

            if (availableSkills == 0) return -1;

            bool excludeLast = availableSkills > 1;
            int minimumUsage = int.MaxValue;
            int candidateCount = 0;

            for (int i = 0; i < ATTACK_KEYS.Length; ++i)
            {
                if (!this.IsAttackSkillAvailable(i)) continue;
                if (excludeLast && i == this.m_LastAttackIndex) continue;

                int usage = this.m_SkillUsageCounts[i];
                if (usage < minimumUsage)
                {
                    minimumUsage = usage;
                    candidateCount = 0;
                    this.m_SkillSelectionCandidates[candidateCount++] = i;
                }
                else if (usage == minimumUsage)
                {
                    this.m_SkillSelectionCandidates[candidateCount++] = i;
                }
            }

            if (candidateCount == 0) return this.m_LastAttackIndex;

            int selected = this.m_SkillSelectionCandidates[
                UnityEngine.Random.Range(0, candidateCount)
            ];
            this.m_SkillUsageCounts[selected] += 1;
            this.m_LastAttackIndex = selected;

            this.NormalizeSkillUsageCounts();
            return selected;
        }

        private bool IsAttackSkillAvailable(int index)
        {
            return index >= 0 &&
                   index < this.m_AttackSkills.Length &&
                   this.m_AttackSkills[index] != null;
        }

        private void NormalizeSkillUsageCounts()
        {
            int minimumUsage = int.MaxValue;
            for (int i = 0; i < ATTACK_KEYS.Length; ++i)
            {
                if (!this.IsAttackSkillAvailable(i)) continue;
                minimumUsage = Mathf.Min(minimumUsage, this.m_SkillUsageCounts[i]);
            }

            if (minimumUsage <= 0 || minimumUsage == int.MaxValue) return;

            for (int i = 0; i < ATTACK_KEYS.Length; ++i)
            {
                if (!this.IsAttackSkillAvailable(i)) continue;
                this.m_SkillUsageCounts[i] -= minimumUsage;
            }
        }

        private async Task EnsureEquipped()
        {
            if (this.m_Character == null || this.m_UnarmedWeapon == null) return;
            if (this.m_Character.Combat.IsEquipped(this.m_UnarmedWeapon)) return;

            if (this.m_EquipTask != null)
            {
                await this.m_EquipTask;
                return;
            }

            this.m_EquipTask = this.m_Character.Combat.Equip(
                this.m_UnarmedWeapon,
                null,
                this.m_SelfArgs ?? new Args(this.m_Character.gameObject)
            );

            try
            {
                await this.m_EquipTask;
                MeleeStance stance = this.m_MeleeStance ??
                    this.m_Character.Combat.RequestStance<MeleeStance>();
                stance.BufferWindow = this.EffectiveInputBuffer;
            }
            finally
            {
                this.m_EquipTask = null;
            }
        }

        private void RestartAttackStateTimeout()
        {
            if (this.m_ResetAttackRoutine != null)
            {
                this.StopCoroutine(this.m_ResetAttackRoutine);
            }

            this.m_ResetAttackRoutine = this.StartCoroutine(this.ResetAttackStateAfterTimeout());
        }

        private void ActivateCombatLocomotion()
        {
            if (this.m_IsCombatLocomotionActive ||
                this.m_UnarmedCombatLocomotion == null ||
                this.m_Character?.States == null)
            {
                return;
            }

            this.m_IsCombatLocomotionActive = true;
            _ = this.m_Character.States.SetState(
                this.m_UnarmedCombatLocomotion,
                this.m_CombatLocomotionLayer,
                BlendMode.Blend,
                new ConfigState(
                    0f,
                    1f,
                    1f,
                    this.m_CombatLocomotionTransition,
                    this.m_CombatLocomotionTransition
                )
            );
        }

        private void RestartCombatLocomotionTimeout()
        {
            this.CancelCombatLocomotionTimeout();

            this.m_CombatLocomotionRoutine = this.StartCoroutine(
                this.StopCombatLocomotionAfterInactivity()
            );
        }

        private void CancelCombatLocomotionTimeout()
        {
            if (this.m_CombatLocomotionRoutine == null) return;

            this.StopCoroutine(this.m_CombatLocomotionRoutine);
            this.m_CombatLocomotionRoutine = null;
        }

        private IEnumerator StopCombatLocomotionAfterInactivity()
        {
            float stopAt = Time.unscaledTime + this.m_CombatLocomotionIdleTimeout;
            while (Time.unscaledTime < stopAt) yield return null;

            MeleeStance stance = this.m_MeleeStance;
            while (stance != null && IsAttackPhase(stance.CurrentPhase))
            {
                yield return null;
            }

            this.m_CombatLocomotionRoutine = null;
            this.StopCombatLocomotion(this.m_CombatLocomotionTransition);
        }

        private void StopCombatLocomotion(float transition)
        {
            if (!this.m_IsCombatLocomotionActive) return;

            this.m_IsCombatLocomotionActive = false;
            this.m_Character?.States?.Stop(
                this.m_CombatLocomotionLayer,
                0f,
                transition
            );
        }

        private void RestartAttackEase(int attackIndex, MeleeStance stance)
        {
            if (this.m_AttackEaseRoutine != null)
            {
                this.StopCoroutine(this.m_AttackEaseRoutine);
                this.m_AttackEaseRoutine = null;
            }

            if (!this.m_UseAnimationEase ||
                attackIndex < 0 ||
                attackIndex >= this.m_AttackSkills.Length)
            {
                return;
            }

            Skill skill = this.m_AttackSkills[attackIndex];
            if (skill == null || skill.Animation == null) return;

            this.m_AttackEaseRoutine = this.StartCoroutine(
                this.ApplyAttackEase(skill, stance)
            );
        }

        private void RestartAttackMovementLimit(MeleeStance stance)
        {
            if (this.m_AttackMovementRoutine != null)
            {
                this.StopCoroutine(this.m_AttackMovementRoutine);
                this.m_AttackMovementRoutine = null;
            }

            this.m_AttackMovementRoutine = this.StartCoroutine(
                this.ApplyAttackMovementLimit(stance)
            );
        }

        private void ReleaseAttackMovementLimitForJogSprint()
        {
            if (this.m_AttackMovementRoutine != null)
            {
                this.StopCoroutine(this.m_AttackMovementRoutine);
                this.m_AttackMovementRoutine = null;
            }

            // FranklinAnimationBridge owns LinearSpeed once layer 2 enters Jog/Sprint.
            // Do not restore m_PreAttackMovementSpeed here because that stale Walk value
            // would overwrite the Jog/Sprint speed that the bridge just selected.
            this.m_IsAttackMovementLimited = false;
        }

        private IEnumerator ApplyAttackMovementLimit(MeleeStance stance)
        {
            float waitStartedAt = Time.unscaledTime;
            while (stance.CurrentPhase == MeleePhase.None)
            {
                if (this.IsJogSprintStateActive())
                {
                    this.m_IsAttackMovementLimited = false;
                    this.m_AttackMovementRoutine = null;
                    yield break;
                }

                if (Time.unscaledTime - waitStartedAt > this.EffectiveInputBuffer)
                {
                    this.RestoreAttackMovementSpeed();
                    this.m_AttackMovementRoutine = null;
                    yield break;
                }

                yield return null;
            }

            if (!IsAttackPhase(stance.CurrentPhase))
            {
                this.RestoreAttackMovementSpeed();
                this.m_AttackMovementRoutine = null;
                yield break;
            }

            this.EnterAttackMovementLimit();
            while (IsAttackPhase(stance.CurrentPhase))
            {
                if (this.IsJogSprintStateActive())
                {
                    // Preserve the speed written by FranklinAnimationBridge in this frame.
                    this.m_IsAttackMovementLimited = false;
                    this.m_AttackMovementRoutine = null;
                    yield break;
                }

                if (this.m_Character?.Motion != null)
                {
                    this.m_Character.Motion.LinearSpeed = this.m_AttackMovementSpeed;
                }

                yield return null;
            }

            this.RestoreAttackMovementSpeed();
            this.m_AttackMovementRoutine = null;
        }

        private void EnterAttackMovementLimit()
        {
            if (this.m_IsAttackMovementLimited || this.m_Character?.Motion == null) return;

            this.m_PreAttackMovementSpeed = this.m_Character.Motion.LinearSpeed;
            this.m_IsAttackMovementLimited = true;
            this.m_Character.Motion.LinearSpeed = this.m_AttackMovementSpeed;
        }

        private void RestoreAttackMovementSpeed()
        {
            if (!this.m_IsAttackMovementLimited) return;

            if (this.m_Character?.Motion != null)
            {
                this.m_Character.Motion.LinearSpeed = this.m_PreAttackMovementSpeed;
            }

            this.m_IsAttackMovementLimited = false;
        }

        private IEnumerator ApplyAttackEase(Skill skill, MeleeStance stance)
        {
            yield return null;

            float waitStartedAt = Time.unscaledTime;
            while (stance.CurrentPhase == MeleePhase.None)
            {
                if (Time.unscaledTime - waitStartedAt > this.EffectiveInputBuffer)
                {
                    this.m_AttackEaseRoutine = null;
                    yield break;
                }

                yield return null;
            }

            AttackSpeed speeds = skill.GetSpeed(this.m_SelfArgs);
            AttackDuration duration = skill.GetDuration(speeds, this.m_SelfArgs);
            float speed = Mathf.Max(this.m_GlobalSkillSpeed, 0.1f);
            float easeInDuration = this.m_EaseInDuration / speed;
            float easeOutDuration = this.m_EaseOutDuration / speed;
            float estimatedDuration = Mathf.Max(
                duration.Total,
                easeInDuration + easeOutDuration,
                0.01f
            );
            float startedAt = Time.unscaledTime;
            float updateInterval = 1f / Mathf.Max(this.m_EaseUpdatesPerSecond, 1);
            float nextUpdateAt = startedAt;

            while (IsAttackPhase(stance.CurrentPhase))
            {
                float elapsed = Time.unscaledTime - startedAt;
                if (Time.unscaledTime < nextUpdateAt)
                {
                    yield return null;
                    continue;
                }
                nextUpdateAt = Time.unscaledTime + updateInterval;

                float phaseSpeed = GetPhaseSpeed(stance.CurrentPhase, speeds);
                float multiplier = FranklinMeleeEase.Evaluate(
                    elapsed,
                    estimatedDuration,
                    this.m_AnimationEase,
                    easeInDuration,
                    easeOutDuration,
                    this.m_EaseEdgeSpeedMultiplier
                );

                this.m_Character.Gestures.SetSpeed(
                    skill.Animation,
                    Mathf.Max(0.01f, phaseSpeed * multiplier)
                );

                yield return null;
            }

            this.m_AttackEaseRoutine = null;
        }

        private void RestartReactionEase()
        {
            this.StopReactionEase();
            if (this.m_ReactionAnimations.Length == 0) return;

            this.m_ReactionEaseRoutine = this.StartCoroutine(
                this.ApplyReactionEase()
            );
        }

        private IEnumerator ApplyReactionEase()
        {
            float startedAt = Time.unscaledTime;
            float updateInterval = 1f / Mathf.Max(this.m_EaseUpdatesPerSecond, 1);
            float nextUpdateAt = startedAt;
            float globalReactionSpeed = Mathf.Clamp(
                this.m_GlobalReactionSpeed,
                0.1f,
                3f
            );
            float easeInDuration =
                this.m_ReactionEaseInDuration / globalReactionSpeed;
            float easeOutDuration =
                this.m_ReactionEaseOutDuration / globalReactionSpeed;

            while (this.m_MeleeStance != null &&
                   this.m_MeleeStance.CurrentPhase == MeleePhase.Reaction)
            {
                float elapsed = Time.unscaledTime - startedAt;
                if (Time.unscaledTime < nextUpdateAt)
                {
                    yield return null;
                    continue;
                }
                nextUpdateAt = Time.unscaledTime + updateInterval;

                for (int i = 0; i < this.m_ReactionAnimations.Length; ++i)
                {
                    AnimationClip animationClip = this.m_ReactionAnimations[i];
                    if (animationClip == null) continue;

                    float multiplier = FranklinMeleeEase.Evaluate(
                        elapsed,
                        this.m_ReactionDurations[i],
                        this.m_ReactionEase,
                        easeInDuration,
                        easeOutDuration,
                        this.m_ReactionEdgeSpeedMultiplier
                    );
                    this.m_Character.Gestures.SetSpeed(
                        animationClip,
                        Mathf.Max(0.01f, globalReactionSpeed * multiplier)
                    );
                }

                yield return null;
            }

            this.m_ReactionEaseRoutine = null;
        }

        private void CacheReactionDurations()
        {
            float globalReactionSpeed = Mathf.Clamp(
                this.m_GlobalReactionSpeed,
                0.1f,
                3f
            );
            float easeInDuration =
                this.m_ReactionEaseInDuration / globalReactionSpeed;
            float easeOutDuration =
                this.m_ReactionEaseOutDuration / globalReactionSpeed;

            this.m_ReactionDurations = new float[this.m_ReactionAnimations.Length];
            for (int i = 0; i < this.m_ReactionAnimations.Length; ++i)
            {
                AnimationClip animationClip = this.m_ReactionAnimations[i];
                this.m_ReactionDurations[i] = Mathf.Max(
                    animationClip != null
                        ? animationClip.length / globalReactionSpeed
                        : 0f,
                    easeInDuration + easeOutDuration,
                    0.01f
                );
            }
        }

        private void StopReactionEase()
        {
            if (this.m_ReactionEaseRoutine == null) return;
            this.StopCoroutine(this.m_ReactionEaseRoutine);
            this.m_ReactionEaseRoutine = null;
        }

        private void ApplyGlobalReactionSpeed()
        {
            float speed = Mathf.Clamp(this.m_GlobalReactionSpeed, 0.1f, 3f);
            FranklinMeleeReactionEase.SetGlobalReactionSpeed(speed);

            if (this.m_HitReaction == null ||
                REACTION_SPEED == null ||
                REACTION_TRANSITION_IN == null ||
                REACTION_TRANSITION_OUT == null)
            {
                return;
            }

            REACTION_SPEED.SetValue(
                this.m_HitReaction,
                new PropertyGetDecimal(speed)
            );
            REACTION_TRANSITION_IN.SetValue(
                this.m_HitReaction,
                BASE_REACTION_TRANSITION_IN / speed
            );
            REACTION_TRANSITION_OUT.SetValue(
                this.m_HitReaction,
                BASE_REACTION_TRANSITION_OUT / speed
            );
        }

        private void ApplyGlobalSkillSpeed()
        {
            if (SKILL_SPEED_ANTICIPATION == null ||
                SKILL_SPEED_STRIKE == null ||
                SKILL_SPEED_RECOVERY == null)
            {
                return;
            }

            float speed = Mathf.Clamp(this.m_GlobalSkillSpeed, 0.1f, 3f);
            foreach (Skill skill in this.m_AttackSkills)
            {
                if (skill == null) continue;

                SKILL_SPEED_ANTICIPATION.SetValue(
                    skill,
                    new PropertyGetDecimal(BASE_SPEED_ANTICIPATION * speed)
                );
                SKILL_SPEED_STRIKE.SetValue(
                    skill,
                    new PropertyGetDecimal(BASE_SPEED_STRIKE * speed)
                );
                SKILL_SPEED_RECOVERY.SetValue(
                    skill,
                    new PropertyGetDecimal(BASE_SPEED_RECOVERY * speed)
                );
            }
        }

        private void ApplyGlobalSkillTrail()
        {
            if (SKILL_TRAIL_IS_ACTIVE == null) return;

            this.CacheAuthoredSkillTrailStates();
            for (int i = 0; i < this.m_AttackSkills.Length; ++i)
            {
                Skill skill = this.m_AttackSkills[i];
                if (skill?.Trail == null) continue;

                bool authoredState =
                    i < this.m_AuthoredSkillTrailStates.Length &&
                    this.m_AuthoredSkillTrailStates[i];
                SKILL_TRAIL_IS_ACTIVE.SetValue(
                    skill.Trail,
                    this.m_UseGlobalSkillTrail && authoredState
                );
            }
        }

        private void CacheAuthoredSkillTrailStates()
        {
            if (this.m_HasCachedSkillTrailStates || SKILL_TRAIL_IS_ACTIVE == null) return;

            this.m_AuthoredSkillTrailStates = new bool[this.m_AttackSkills.Length];
            for (int i = 0; i < this.m_AttackSkills.Length; ++i)
            {
                Skill skill = this.m_AttackSkills[i];
                if (skill?.Trail == null) continue;

                object value = SKILL_TRAIL_IS_ACTIVE.GetValue(skill.Trail);
                this.m_AuthoredSkillTrailStates[i] = value is bool isActive && isActive;
            }

            this.m_HasCachedSkillTrailStates = true;
        }

        private void ApplyEffectiveInputBuffer()
        {
            if (this.m_MeleeStance != null)
            {
                this.m_MeleeStance.BufferWindow = this.EffectiveInputBuffer;
            }
        }

        private static float GetPhaseSpeed(MeleePhase phase, AttackSpeed speeds)
        {
            return phase switch
            {
                MeleePhase.Strike => speeds.Strike,
                MeleePhase.Recovery => speeds.Recovery,
                _ => speeds.Anticipation
            };
        }

        private static bool IsAttackPhase(MeleePhase phase)
        {
            return phase == MeleePhase.Anticipation ||
                   phase == MeleePhase.Strike ||
                   phase == MeleePhase.Recovery;
        }

        private IEnumerator ResetAttackStateAfterTimeout()
        {
            float resetAt = Time.unscaledTime + this.EffectiveAttackTimeout;
            while (Time.unscaledTime < resetAt) yield return null;
            this.m_ResetAttackRoutine = null;

            if (this.m_Character == null) yield break;

            MeleeStance stance = this.m_MeleeStance ??
                this.m_Character.Combat.RequestStance<MeleeStance>();
            if (IsAttackPhase(stance.CurrentPhase))
            {
                stance.ForceCancel();
            }
        }

        private void OnWeaponEquipped(IWeapon weapon, GameObject instance)
        {
            if (weapon is ShooterWeapon)
            {
                this.CancelAllMeleeStates();
            }
        }

        /// <summary>
        /// Immediately releases every state, gesture and movement override owned by melee.
        /// Shooter calls this at the beginning of equip; EventEquip is the fallback for
        /// weapons equipped through other GC2 workflows.
        /// </summary>
        public void CancelAllMeleeStates()
        {
            this.CancelCombatLocomotionTimeout();
            this.StopCombatLocomotion(0f);

            if (this.m_ResetAttackRoutine != null)
            {
                this.StopCoroutine(this.m_ResetAttackRoutine);
                this.m_ResetAttackRoutine = null;
            }

            if (this.m_AttackEaseRoutine != null)
            {
                this.StopCoroutine(this.m_AttackEaseRoutine);
                this.m_AttackEaseRoutine = null;
            }

            if (this.m_AttackMovementRoutine != null)
            {
                this.StopCoroutine(this.m_AttackMovementRoutine);
                this.m_AttackMovementRoutine = null;
            }

            this.StopReactionEase();
            this.m_PreviousMeleePhase = MeleePhase.None;

            if (this.m_IsSidestepping && this.m_Character?.Dash.IsDashing == true)
            {
                this.m_Character.Dash.Cancel();
            }
            this.m_IsSidestepping = false;

            this.m_MeleeStance?.ForceCancel();
            this.StopMeleeGesturesImmediately();
            this.RestoreAttackMovementSpeed();

            // Stop layer 1 even if the local flag was desynchronized by another system.
            this.m_Character?.States?.Stop(this.m_CombatLocomotionLayer, 0f, 0f);
        }

        private void StopMeleeGesturesImmediately()
        {
            if (this.m_Character?.Gestures == null) return;

            foreach (Skill skill in this.m_AttackSkills)
            {
                if (skill?.Animation != null)
                {
                    this.m_Character.Gestures.Stop(skill.Animation, 0f, 0f);
                }
            }

            foreach (AnimationClip reaction in this.m_ReactionAnimations)
            {
                if (reaction != null)
                {
                    this.m_Character.Gestures.Stop(reaction, 0f, 0f);
                }
            }

            if (this.m_SidestepLeft != null)
            {
                this.m_Character.Gestures.Stop(this.m_SidestepLeft, 0f, 0f);
            }
            if (this.m_SidestepRight != null)
            {
                this.m_Character.Gestures.Stop(this.m_SidestepRight, 0f, 0f);
            }
        }

        private void OnDisable()
        {
            if (this.m_Character != null)
            {
                this.m_Character.Combat.EventEquip -= this.OnWeaponEquipped;
            }

            this.CancelAllMeleeStates();

            this.m_EquipTask = null;

            this.RestoreAuthoredSkillSpeed();
            this.RestoreAuthoredSkillTrail();
            this.RestoreAuthoredReactionSpeed();

        }

        private void RestoreAuthoredSkillSpeed()
        {
            if (SKILL_SPEED_ANTICIPATION == null ||
                SKILL_SPEED_STRIKE == null ||
                SKILL_SPEED_RECOVERY == null)
            {
                return;
            }

            foreach (Skill skill in this.m_AttackSkills)
            {
                if (skill == null) continue;
                SKILL_SPEED_ANTICIPATION.SetValue(
                    skill,
                    new PropertyGetDecimal(BASE_SPEED_ANTICIPATION)
                );
                SKILL_SPEED_STRIKE.SetValue(
                    skill,
                    new PropertyGetDecimal(BASE_SPEED_STRIKE)
                );
                SKILL_SPEED_RECOVERY.SetValue(
                    skill,
                    new PropertyGetDecimal(BASE_SPEED_RECOVERY)
                );
            }
        }

        private void RestoreAuthoredReactionSpeed()
        {
            FranklinMeleeReactionEase.SetGlobalReactionSpeed(1f);

            if (this.m_HitReaction == null ||
                REACTION_SPEED == null ||
                REACTION_TRANSITION_IN == null ||
                REACTION_TRANSITION_OUT == null)
            {
                return;
            }

            REACTION_SPEED.SetValue(
                this.m_HitReaction,
                new PropertyGetDecimal(1f)
            );
            REACTION_TRANSITION_IN.SetValue(
                this.m_HitReaction,
                BASE_REACTION_TRANSITION_IN
            );
            REACTION_TRANSITION_OUT.SetValue(
                this.m_HitReaction,
                BASE_REACTION_TRANSITION_OUT
            );
        }

        private void RestoreAuthoredSkillTrail()
        {
            if (!this.m_HasCachedSkillTrailStates || SKILL_TRAIL_IS_ACTIVE == null) return;

            int skillCount = Mathf.Min(
                this.m_AttackSkills.Length,
                this.m_AuthoredSkillTrailStates.Length
            );
            for (int i = 0; i < skillCount; ++i)
            {
                Skill skill = this.m_AttackSkills[i];
                if (skill?.Trail == null) continue;

                SKILL_TRAIL_IS_ACTIVE.SetValue(
                    skill.Trail,
                    this.m_AuthoredSkillTrailStates[i]
                );
            }
        }
    }
}
