using System;
using System.Collections;
using System.Collections.Generic;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Melee;
using UnityEngine;
using UnityEngine.Serialization;

namespace FranklinGame.Melee
{
    /// <summary>
    /// Applies a shared speed curve to the GC2 hit-reaction gesture currently playing.
    /// Only the active clip responds to SetSpeed, so one profile can contain every
    /// directional reaction animation.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
    public sealed class FranklinMeleeReactionEase : MonoBehaviour
    {
        private static readonly HashSet<FranklinMeleeReactionEase> ACTIVE_DRIVERS =
            new HashSet<FranklinMeleeReactionEase>();
        private static float s_GlobalReactionSpeed = 1f;

        [SerializeField, HideInInspector] private Character m_Character;
        [SerializeField, HideInInspector] private AnimationClip[] m_ReactionAnimations =
            Array.Empty<AnimationClip>();
        [SerializeField, HideInInspector] private bool m_UseEase = true;
        [SerializeField, HideInInspector] private Easing.Type m_Ease = Easing.Type.QuadInOut;
        [SerializeField, HideInInspector, Min(0f)] private float m_EaseInDuration = 0.08f;
        [SerializeField, HideInInspector, Min(0f)] private float m_EaseOutDuration = 0.12f;
        [SerializeField, HideInInspector, Range(0.05f, 1f)]
        private float m_EdgeSpeedMultiplier = 0.65f;
        [FormerlySerializedAs("m_BaseSpeed")]
        [SerializeField, HideInInspector, Range(0.1f, 3f)]
        private float m_GlobalReactionSpeed = 1f;
        [SerializeField, HideInInspector, Range(15, 60)]
        private int m_UpdatesPerSecond = 30;

        private MeleeStance m_Stance;
        private Coroutine m_EaseRoutine;
        private MeleePhase m_PreviousPhase = MeleePhase.None;
        private float[] m_ReactionDurations = Array.Empty<float>();

        public static void SetGlobalReactionSpeed(float speed)
        {
            s_GlobalReactionSpeed = Mathf.Clamp(speed, 0.1f, 3f);
            foreach (FranklinMeleeReactionEase driver in ACTIVE_DRIVERS)
            {
                if (driver != null) driver.CacheReactionDurations();
            }
        }

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
                this.m_Stance = this.m_Character.Combat.RequestStance<MeleeStance>();
            }

            this.CacheReactionDurations();
        }

        private void OnEnable()
        {
            ACTIVE_DRIVERS.Add(this);
            this.CacheReactionDurations();
        }

        private void Update()
        {
            if (this.m_Stance == null) return;
            if (!this.m_UseEase)
            {
                this.StopEase();
                this.m_PreviousPhase = this.m_Stance.CurrentPhase;
                return;
            }

            MeleePhase phase = this.m_Stance.CurrentPhase;
            if (phase == MeleePhase.Reaction && this.m_PreviousPhase != MeleePhase.Reaction)
            {
                this.RestartEase();
            }
            else if (phase != MeleePhase.Reaction &&
                     this.m_PreviousPhase == MeleePhase.Reaction)
            {
                this.StopEase();
            }

            this.m_PreviousPhase = phase;
        }

        public void Configure(
            Character character,
            AnimationClip[] reactionAnimations,
            bool useEase,
            Easing.Type ease,
            float easeInDuration,
            float easeOutDuration,
            float edgeSpeedMultiplier,
            float globalReactionSpeed,
            int updatesPerSecond)
        {
            this.m_Character = character != null ? character : this.GetComponent<Character>();
            this.m_ReactionAnimations = reactionAnimations ?? Array.Empty<AnimationClip>();
            this.m_UseEase = useEase;
            this.m_Ease = ease;
            this.m_EaseInDuration = Mathf.Max(0f, easeInDuration);
            this.m_EaseOutDuration = Mathf.Max(0f, easeOutDuration);
            this.m_EdgeSpeedMultiplier = Mathf.Clamp(edgeSpeedMultiplier, 0.05f, 1f);
            this.m_GlobalReactionSpeed = Mathf.Clamp(globalReactionSpeed, 0.1f, 3f);
            this.m_UpdatesPerSecond = Mathf.Clamp(updatesPerSecond, 15, 60);

            if (Application.isPlaying)
            {
                SetGlobalReactionSpeed(this.m_GlobalReactionSpeed);
            }

            this.CacheReactionDurations();

            if (!this.m_UseEase) this.StopEase();

            if (Application.isPlaying && this.m_Character != null)
            {
                this.m_Stance = this.m_Character.Combat.RequestStance<MeleeStance>();
            }
        }

        private void RestartEase()
        {
            this.StopEase();
            if (this.m_ReactionAnimations.Length == 0) return;
            this.m_EaseRoutine = this.StartCoroutine(this.ApplyEase());
        }

        private IEnumerator ApplyEase()
        {
            float startedAt = Time.unscaledTime;
            float updateInterval = 1f / this.m_UpdatesPerSecond;
            float nextUpdateAt = startedAt;
            float globalReactionSpeed = Mathf.Clamp(
                s_GlobalReactionSpeed,
                0.1f,
                3f
            );
            float easeInDuration = this.m_EaseInDuration / globalReactionSpeed;
            float easeOutDuration = this.m_EaseOutDuration / globalReactionSpeed;

            while (this.m_Stance != null &&
                   this.m_Stance.CurrentPhase == MeleePhase.Reaction)
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
                        this.m_Ease,
                        easeInDuration,
                        easeOutDuration,
                        this.m_EdgeSpeedMultiplier
                    );
                    this.m_Character.Gestures.SetSpeed(
                        animationClip,
                        Mathf.Max(0.01f, globalReactionSpeed * multiplier)
                    );
                }

                yield return null;
            }

            this.m_EaseRoutine = null;
        }

        private void CacheReactionDurations()
        {
            float globalReactionSpeed = Mathf.Clamp(
                Application.isPlaying
                    ? s_GlobalReactionSpeed
                    : this.m_GlobalReactionSpeed,
                0.1f,
                3f
            );
            float easeInDuration = this.m_EaseInDuration / globalReactionSpeed;
            float easeOutDuration = this.m_EaseOutDuration / globalReactionSpeed;

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

        private void StopEase()
        {
            if (this.m_EaseRoutine == null) return;
            this.StopCoroutine(this.m_EaseRoutine);
            this.m_EaseRoutine = null;
        }

        private void OnDisable()
        {
            ACTIVE_DRIVERS.Remove(this);
            this.StopEase();
            this.m_PreviousPhase = MeleePhase.None;
        }
    }

    internal static class FranklinMeleeEase
    {
        public static float Evaluate(
            float elapsed,
            float duration,
            Easing.Type ease,
            float easeInDuration,
            float easeOutDuration,
            float edgeSpeedMultiplier)
        {
            float multiplier = 1f;

            if (easeInDuration > float.Epsilon && elapsed < easeInDuration)
            {
                float progress = Mathf.Clamp01(elapsed / easeInDuration);
                multiplier = Easing.GetEase(
                    ease,
                    edgeSpeedMultiplier,
                    1f,
                    progress
                );
            }

            float easeOutStart = Mathf.Max(duration - easeOutDuration, 0f);
            if (easeOutDuration > float.Epsilon && elapsed > easeOutStart)
            {
                float progress = Mathf.Clamp01((elapsed - easeOutStart) / easeOutDuration);
                float easeOut = Easing.GetEase(
                    ease,
                    1f,
                    edgeSpeedMultiplier,
                    progress
                );
                multiplier = Mathf.Min(multiplier, easeOut);
            }

            return multiplier;
        }
    }
}
