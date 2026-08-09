using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Runs the paired GC2 carjacking gestures on the single Sim-Cade car.
    /// The NPC and Player keep their own GC2 Character graphs; no Animator
    /// Controller swapping is required.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CarEntry), typeof(SimcadeCarDriver))]
    public sealed class SimcadeCarjacking : MonoBehaviour, ICarOccupiedEntryHandler
    {
        [Header("Initial GC2 NPC Driver")]
        [SerializeField] private CarEntry m_CarEntry;
        [SerializeField] private GameObject m_NpcDriverPrefab;
        [SerializeField] private bool m_SpawnNpcDriverOnStart = true;

        [Header("Paired Carjacking Animations - Driver Left Door")]
        [SerializeField] private AnimationClip m_AttackerKickOut;
        [SerializeField] private AnimationClip m_VictimGetKickedOut;
        [SerializeField] private Transform m_VictimLandingPoint;
        [SerializeField, Range(0f, 0.5f)] private float m_TransitionIn = 0.05f;
        [SerializeField, Range(0f, 0.5f)] private float m_TransitionOut = 0.1f;
        [SerializeField, Range(0.5f, 2f)] private float m_CarjackingAnimationSpeed = 1.7f;
        [Tooltip("Hands control directly to Enter Car before the pull clip fades to idle.")]
        [SerializeField, Range(0.5f, 0.98f)] private float m_EnterHandoffNormalizedTime = 0.78f;

        [Header("Player Grab IK")]
        [SerializeField] private AnimationCurve m_PrimaryGrabIKWeight = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.92f, 1f),
            new Keyframe(1f, 0f)
        );
        [SerializeField] private AnimationCurve m_SecondaryGrabIKWeight = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.34f, 1f),
            new Keyframe(0.5f, 0f),
            new Keyframe(1f, 0f)
        );
        [SerializeField, Min(0f)] private float m_NeckGrabSurfaceOffset = 0.06f;
        [SerializeField, Min(0f)] private float m_ShoulderGrabSurfaceOffset = 0.045f;
        [SerializeField, Min(0.1f)] private float m_MaxGrabReach = 1.35f;

        [Header("Player Pull Root Control")]
        [SerializeField, Range(0f, 90f)] private float m_MaxPullYawFromEntry = 55f;
        [SerializeField, Min(0f)] private float m_PullFacingSharpness = 18f;

        private SimcadeCarDriver m_Driver;
        private Quaternion m_DoorClosedRotation;
        private bool m_IsTransitioning;
        private Character m_SpawnedNpcDriver;
        private CharacterIKSetter m_GrabIKSetter;
        private Transform m_GrabNeckTarget;
        private Transform m_GrabShoulderTarget;
        private Transform m_VictimNeck;
        private Transform m_VictimShoulder;

        public bool IsTransitioning => this.m_IsTransitioning;
        public Transform VictimLandingPoint => this.m_VictimLandingPoint;
        public Character SpawnedNpcDriver => this.m_SpawnedNpcDriver;

        private void Awake()
        {
            if (this.m_CarEntry == null) this.m_CarEntry = this.GetComponent<CarEntry>();
            this.m_Driver = this.GetComponent<SimcadeCarDriver>();
            if (this.m_CarEntry?.doorTransform != null)
                this.m_DoorClosedRotation = this.m_CarEntry.doorTransform.localRotation;
            this.EnsureGrabTargets();
        }

        private void OnDestroy()
        {
            if (this.m_GrabNeckTarget != null) Destroy(this.m_GrabNeckTarget.gameObject);
            if (this.m_GrabShoulderTarget != null)
                Destroy(this.m_GrabShoulderTarget.gameObject);
        }

        private void Start()
        {
            if (this.m_SpawnNpcDriverOnStart &&
                this.m_CarEntry != null &&
                this.m_CarEntry.SeatedCharacter == null)
            {
                this.SpawnInitialNpcDriver();
            }
        }

        public bool CanCarjack(Character attacker)
        {
            Character victim = this.m_CarEntry != null
                ? this.m_CarEntry.SeatedCharacter
                : null;
            return this.isActiveAndEnabled &&
                !this.m_IsTransitioning &&
                attacker != null &&
                victim != null &&
                victim != attacker &&
                !victim.IsPlayer &&
                this.m_CarEntry.entryStandingPoint != null &&
                this.m_CarEntry.entryParent != null &&
                this.m_AttackerKickOut != null &&
                this.m_VictimGetKickedOut != null;
        }

        public bool TryBeginCarjack(Character attacker)
        {
            if (!this.CanCarjack(attacker)) return false;
            this.m_IsTransitioning = true;
            _ = this.RunCarjackingAsync(attacker);
            return true;
        }

        public bool TryEnterOccupiedCar(Character character)
        {
            return this.TryBeginCarjack(character);
        }

        private void SpawnInitialNpcDriver()
        {
            if (this.m_NpcDriverPrefab == null)
            {
                Debug.LogWarning("Sim-Cade carjacking has no NPC.prefab assigned", this);
                return;
            }

            GameObject npcObject = Instantiate(this.m_NpcDriverPrefab);
            npcObject.name = "NPC Drive Vehicle";
            Character npc = npcObject.GetComponent<Character>();
            if (npc == null || npc.IsPlayer || !this.m_CarEntry.SeatOccupantInstant(npc))
            {
                Debug.LogError(
                    "NPC driver must be a non-player GC2 Character and the driver seat must be empty",
                    this
                );
                Destroy(npcObject);
                return;
            }

            this.m_SpawnedNpcDriver = npc;
        }

        private async Task RunCarjackingAsync(Character attacker)
        {
            Character victim = this.m_CarEntry != null
                ? this.m_CarEntry.SeatedCharacter
                : null;
            bool attackerSeated = false;
            bool doorIsOpen = false;
            bool victimWasReleased = false;
            bool victimPhysicsWasRestored = false;
            Task victimReleaseTask = null;

            try
            {
                if (attacker == null || victim == null || this.m_CarEntry == null) return;

                this.m_Driver?.SetVehicleEnabled(false);
                if (attacker.Player != null) attacker.Player.IsControllable = false;
                if (!await this.m_CarEntry.MoveCharacterToEntryStandingPointAsync(attacker))
                    return;
                if (attacker.Driver != null) attacker.Driver.Collision = false;

                if (!this.m_CarEntry.ReleaseOccupantForCarjacking(victim)) return;
                victimWasReleased = true;

                Transform seat = this.m_CarEntry.entryParent;
                victim.transform.SetPositionAndRotation(seat.position, seat.rotation);

                await this.SetDoorOpenAsync(true);
                doorIsOpen = true;
                await this.PlayPairedCarjackingAsync(attacker, victim);

                // Start landing the NPC and entering the Player in the same
                // frame. Waiting for the NPC's two-frame grounding handshake
                // before entry exposed an upright Player pose inside the car.
                victimReleaseTask = this.CompleteVictimLandingAsync(victim);

                if (attacker == null || this.m_CarEntry == null)
                {
                    await victimReleaseTask;
                    victimPhysicsWasRestored = true;
                    return;
                }
                if (attacker.Driver != null) attacker.Driver.Collision = true;

                Task<bool> enterTask = this.m_CarEntry.EnterThroughOpenDoorAsync(attacker);
                await victimReleaseTask;
                victimPhysicsWasRestored = true;
                attackerSeated = await enterTask;

                // The same door stays open for pulling the NPC and entering the
                // seat. It closes exactly once after the Player is attached.
                await this.SetDoorOpenAsync(false);
                doorIsOpen = false;

                attackerSeated = attackerSeated && this.m_CarEntry != null &&
                    this.m_CarEntry.SeatedCharacter == attacker;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                this.ClearGrabIK();
                if (doorIsOpen) await this.SetDoorOpenAsync(false);
                if (victimWasReleased && !victimPhysicsWasRestored &&
                    victim != null && this.m_CarEntry != null)
                {
                    if (victimReleaseTask != null) await victimReleaseTask;
                    else await this.CompleteVictimLandingAsync(victim);
                }

                if (!attackerSeated && attacker != null)
                {
                    if (attacker.Driver != null) attacker.Driver.Collision = true;
                    if (attacker.Player != null) attacker.Player.IsControllable = true;
                }

                this.m_IsTransitioning = false;
            }
        }

        private async Task PlayGestureAsync(
            Character character,
            AnimationClip clip,
            bool useRootMotion)
        {
            if (character == null || clip == null) return;
            ConfigGesture config = new ConfigGesture(
                0f,
                clip.length,
                this.m_CarjackingAnimationSpeed,
                useRootMotion,
                this.m_TransitionIn,
                this.m_TransitionOut
            );
            await character.Gestures.CrossFade(
                clip,
                null,
                BlendMode.Blend,
                config,
                true
            );
        }

        private async Task PlayPairedCarjackingAsync(
            Character attacker,
            Character victim)
        {
            if (attacker == null || victim == null) return;

            Vector3 victimStartPosition = this.m_CarEntry.entryParent.position;
            Quaternion victimStartRotation = this.m_CarEntry.entryParent.rotation;
            Vector3 attackerStartPosition = this.m_CarEntry.entryStandingPoint.position;
            Quaternion attackerStartRotation = this.m_CarEntry.entryStandingPoint.rotation;
            Vector3 victimEndPosition = this.m_VictimLandingPoint != null
                ? this.m_VictimLandingPoint.position
                : victimStartPosition;
            Quaternion victimEndRotation = this.m_VictimLandingPoint != null
                ? this.m_VictimLandingPoint.rotation
                : victimStartRotation;

            this.BeginGrabIK(attacker, victim);
            _ = this.PlayGestureAsync(
                attacker,
                this.m_AttackerKickOut,
                false
            );
            // The car owns the victim root path. The animation supplies only the
            // Humanoid body motion so GC2 physics/root motion cannot launch the NPC.
            _ = this.PlayGestureAsync(
                victim,
                this.m_VictimGetKickedOut,
                false
            );

            float fullDuration = Mathf.Max(
                this.m_AttackerKickOut.length,
                this.m_VictimGetKickedOut.length
            ) / Mathf.Max(0.01f, this.m_CarjackingAnimationSpeed);
            float handoffTime = Mathf.Clamp01(this.m_EnterHandoffNormalizedTime);
            float duration = fullDuration * handoffTime;
            float startedAt = Time.time;
            float pullProgress = 0f;
            while (pullProgress < 1f && attacker != null && victim != null)
            {
                pullProgress = Mathf.Clamp01(
                    (Time.time - startedAt) / Mathf.Max(0.01f, duration)
                );
                float positionBlend = Mathf.SmoothStep(0f, 1f, pullProgress);
                victim.transform.SetPositionAndRotation(
                    Vector3.Lerp(victimStartPosition, victimEndPosition, positionBlend),
                    Quaternion.Slerp(victimStartRotation, victimEndRotation, positionBlend)
                );
                this.UpdateAttackerPullRoot(
                    attacker,
                    victim,
                    attackerStartPosition,
                    attackerStartRotation
                );
                this.UpdateGrabIK(attacker, victim, pullProgress);
                await Task.Yield();
            }

            if (victim != null)
            {
                victim.transform.SetPositionAndRotation(
                    victimEndPosition,
                    victimEndRotation
                );
            }
            // Do not stop the attacker gesture here. EnterCar cross-fades it
            // directly into the entry clip, preventing an idle/standing frame.
            this.ClearGrabIK();
        }

        private void UpdateAttackerPullRoot(
            Character attacker,
            Character victim,
            Vector3 startPosition,
            Quaternion startRotation)
        {
            attacker.transform.position = startPosition;

            Vector3 direction = victim.Eyes - attacker.Eyes;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f) return;

            float entryYaw = startRotation.eulerAngles.y;
            float desiredYaw = Quaternion.LookRotation(direction.normalized, Vector3.up)
                .eulerAngles.y;
            float yawDelta = Mathf.Clamp(
                Mathf.DeltaAngle(entryYaw, desiredYaw),
                -this.m_MaxPullYawFromEntry,
                this.m_MaxPullYawFromEntry
            );
            Vector3 entryEuler = startRotation.eulerAngles;
            Quaternion targetRotation = Quaternion.Euler(
                entryEuler.x,
                entryYaw + yawDelta,
                entryEuler.z
            );
            float blend = 1f - Mathf.Exp(-this.m_PullFacingSharpness * Time.deltaTime);
            attacker.transform.rotation = Quaternion.Slerp(
                attacker.transform.rotation,
                targetRotation,
                blend
            );
        }

        private void BeginGrabIK(Character attacker, Character victim)
        {
            this.ClearGrabIK();
            this.EnsureGrabTargets();
            Animator attackerAnimator = attacker?.GetComponentInChildren<Animator>(true);
            Animator victimAnimator = victim?.GetComponentInChildren<Animator>(true);
            if (attackerAnimator == null || victimAnimator == null ||
                !attackerAnimator.isHuman || !victimAnimator.isHuman) return;

            this.m_GrabIKSetter = attackerAnimator.GetComponent<CharacterIKSetter>();
            if (this.m_GrabIKSetter == null)
                this.m_GrabIKSetter = attackerAnimator.gameObject.AddComponent<CharacterIKSetter>();

            this.m_VictimNeck = victimAnimator.GetBoneTransform(HumanBodyBones.Neck) ??
                victimAnimator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftShoulder = victimAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder) ??
                victimAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightShoulder = victimAnimator.GetBoneTransform(HumanBodyBones.RightShoulder) ??
                victimAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (leftShoulder == null) this.m_VictimShoulder = rightShoulder;
            else if (rightShoulder == null) this.m_VictimShoulder = leftShoulder;
            else
            {
                this.m_VictimShoulder =
                    (leftShoulder.position - attacker.transform.position).sqrMagnitude <
                    (rightShoulder.position - attacker.transform.position).sqrMagnitude
                        ? leftShoulder
                        : rightShoulder;
            }

            if (this.m_VictimNeck == null || this.m_VictimShoulder == null)
            {
                this.ClearGrabIK();
                return;
            }

            this.m_GrabNeckTarget.gameObject.SetActive(true);
            this.m_GrabShoulderTarget.gameObject.SetActive(true);
        }

        private void EnsureGrabTargets()
        {
            if (this.m_GrabNeckTarget == null)
            {
                this.m_GrabNeckTarget = new GameObject("Runtime Neck Grab IK").transform;
                this.m_GrabNeckTarget.SetParent(this.transform, false);
                this.m_GrabNeckTarget.gameObject.SetActive(false);
            }

            if (this.m_GrabShoulderTarget == null)
            {
                this.m_GrabShoulderTarget = new GameObject("Runtime Shoulder Grab IK").transform;
                this.m_GrabShoulderTarget.SetParent(this.transform, false);
                this.m_GrabShoulderTarget.gameObject.SetActive(false);
            }
        }

        private void UpdateGrabIK(Character attacker, Character victim, float normalized)
        {
            if (this.m_GrabIKSetter == null || this.m_GrabNeckTarget == null ||
                this.m_GrabShoulderTarget == null || this.m_VictimNeck == null ||
                this.m_VictimShoulder == null) return;

            Vector3 towardAttacker = attacker.Eyes - this.m_VictimNeck.position;
            if (towardAttacker.sqrMagnitude < 0.0001f) towardAttacker = -victim.transform.forward;
            towardAttacker.Normalize();

            this.m_GrabNeckTarget.position = this.m_VictimNeck.position +
                towardAttacker * this.m_NeckGrabSurfaceOffset;
            this.m_GrabShoulderTarget.position = this.m_VictimShoulder.position +
                towardAttacker * this.m_ShoulderGrabSurfaceOffset;
            Quaternion handRotation = Quaternion.LookRotation(
                -towardAttacker,
                victim.transform.up
            );
            this.m_GrabNeckTarget.rotation = handRotation;
            this.m_GrabShoulderTarget.rotation = handRotation;

            float reach = Vector3.Distance(attacker.Eyes, this.m_VictimNeck.position);
            float reachWeight = 1f - Mathf.SmoothStep(
                this.m_MaxGrabReach * 0.82f,
                this.m_MaxGrabReach,
                reach
            );
            float primaryWeight = Mathf.Clamp01(
                (this.m_PrimaryGrabIKWeight?.Evaluate(normalized) ?? 0f) * reachWeight
            );
            float secondaryWeight = Mathf.Clamp01(
                (this.m_SecondaryGrabIKWeight?.Evaluate(normalized) ?? 0f) * reachWeight
            );
            this.m_GrabIKSetter.SetIKTargets(
                this.m_GrabShoulderTarget,
                this.m_GrabNeckTarget,
                secondaryWeight,
                primaryWeight
            );
        }

        private void ClearGrabIK()
        {
            if (this.m_GrabIKSetter != null)
                this.m_GrabIKSetter.SetIKTargets(null, null, 0f, 0f);

            if (this.m_GrabNeckTarget != null)
                this.m_GrabNeckTarget.gameObject.SetActive(false);
            if (this.m_GrabShoulderTarget != null)
                this.m_GrabShoulderTarget.gameObject.SetActive(false);

            this.m_GrabIKSetter = null;
            this.m_VictimNeck = null;
            this.m_VictimShoulder = null;
        }

        private Task PlaceVictimAtLandingPointAsync(Character victim)
        {
            if (victim == null) return Task.CompletedTask;
            if (this.m_VictimLandingPoint != null)
            {
                return this.m_CarEntry.CompleteOccupantReleaseAsync(
                    victim,
                    this.m_VictimLandingPoint.position,
                    this.m_VictimLandingPoint.rotation
                );
            }

            return this.m_CarEntry.CompleteOccupantReleaseAsync(
                victim,
                victim.transform.position,
                victim.transform.rotation
            );
        }

        private async Task CompleteVictimLandingAsync(Character victim)
        {
            if (victim == null) return;
            await this.PlaceVictimAtLandingPointAsync(victim);
            if (victim != null)
            {
                // Only reveal the base graph after collision is stable and GC2
                // has registered a grounded frame at the authored landing point.
                victim.Gestures.Stop(0f, 0.08f);
            }
        }

        private async Task SetDoorOpenAsync(bool open)
        {
            if (this.m_CarEntry?.doorTransform == null) return;

            Transform door = this.m_CarEntry.doorTransform;
            Quaternion start = door.localRotation;
            Quaternion target = open
                ? Quaternion.Euler(this.m_CarEntry.doorOpenRotation)
                : this.m_DoorClosedRotation;
            float duration = Mathf.Max(0.01f, this.m_CarEntry.doorRotationDuration);
            float elapsed = 0f;

            while (elapsed < duration && door != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                progress = progress * progress * (3f - 2f * progress);
                door.localRotation = Quaternion.Slerp(start, target, progress);
                await Task.Yield();
            }

            if (door != null) door.localRotation = target;
        }
    }
}
