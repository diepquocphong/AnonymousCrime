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

        [Header("Paired Carjacking Animations - Both Front Doors")]
        [SerializeField] private AnimationClip m_AttackerKickOut;
        [SerializeField] private AnimationClip m_VictimGetKickedOut;
        [SerializeField] private Transform m_VictimLandingPoint;
        [SerializeField, Range(0f, 0.5f)] private float m_TransitionIn = 0.03f;
        [SerializeField, Range(0f, 0.5f)] private float m_TransitionOut = 0.1f;
        [SerializeField, Range(0.5f, 2f)] private float m_CarjackingAnimationSpeed = 2f;
        [Tooltip("Hands control directly to Enter Car before the pull clip fades to idle.")]
        [SerializeField, Range(0.5f, 0.98f)] private float m_EnterHandoffNormalizedTime = 0.7f;

        [Header("Player Grab IK")]
        [SerializeField] private AnimationCurve m_PrimaryGrabIKWeight = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.025f, 0f),
            new Keyframe(0.12f, 1f),
            new Keyframe(0.9f, 1f),
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

        [Header("Passenger-seat push posture")]
        [Tooltip("Maximum upper-body turn toward the NPC while pushing from the passenger seat.")]
        [SerializeField, Range(0f, 45f)] private float m_PassengerPushTorsoYaw = 38f;
        [Tooltip("Small sideways/forward lean used to put body weight behind the push.")]
        [SerializeField, Range(0f, 25f)] private float m_PassengerPushTorsoLean = 20f;
        [SerializeField, Range(0f, 1f)] private float m_PassengerPushHeadLookWeight = 0.95f;
        [Tooltip("Right hand braces on the steering wheel while the left hand pushes the NPC.")]
        [SerializeField, Range(0f, 1f)] private float m_PassengerPushRightBraceIKWeight = 1f;

        [Header("Player Pull Root Control")]
        [SerializeField, Range(0f, 90f)] private float m_MaxPullYawFromEntry = 55f;
        [SerializeField, Min(0f)] private float m_PullFacingSharpness = 18f;

        private SimcadeCarDriver m_Driver;
        private Quaternion m_DriverDoorClosedRotation;
        private Quaternion m_PassengerDoorClosedRotation;
        private CarEntrySideMode m_ActiveEntrySide = CarEntrySideMode.DriverDoor;
        private bool m_IsTransitioning;
        private Character m_SpawnedNpcDriver;
        private CharacterIKSetter m_GrabIKSetter;
        private Transform m_GrabNeckTarget;
        private Transform m_GrabShoulderTarget;
        private Transform m_VictimNeck;
        private Transform m_VictimShoulder;
        private Transform m_VictimPushLookTarget;

        public bool IsTransitioning => this.m_IsTransitioning;
        public Transform VictimLandingPoint => this.m_VictimLandingPoint;
        public Character SpawnedNpcDriver => this.m_SpawnedNpcDriver;

        private void Awake()
        {
            if (this.m_CarEntry == null) this.m_CarEntry = this.GetComponent<CarEntry>();
            this.m_Driver = this.GetComponent<SimcadeCarDriver>();
            if (this.m_CarEntry?.doorTransform != null)
                this.m_DriverDoorClosedRotation =
                    this.m_CarEntry.doorTransform.localRotation;
            if (this.m_CarEntry?.passengerDoorTransform != null)
                this.m_PassengerDoorClosedRotation =
                    this.m_CarEntry.passengerDoorTransform.localRotation;
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

        public bool TryBeginCarjack(
            Character attacker,
            CarEntrySideMode requestedSide = CarEntrySideMode.DriverDoor)
        {
            if (!this.CanCarjack(attacker)) return false;
            bool passengerSide = requestedSide == CarEntrySideMode.PassengerDoor;
            if (passengerSide &&
                this.m_CarEntry.passengerDoorTransform == null)
            {
                return false;
            }

            this.m_ActiveEntrySide = passengerSide
                ? CarEntrySideMode.PassengerDoor
                : CarEntrySideMode.DriverDoor;
            this.m_IsTransitioning = true;
            _ = this.RunCarjackingAsync(attacker);
            return true;
        }

        public bool TryEnterOccupiedCar(
            Character character,
            CarEntrySideMode requestedSide)
        {
            return this.TryBeginCarjack(character, requestedSide);
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
            bool driverDoorIsOpen = false;
            bool passengerDoorIsOpen = false;
            bool victimWasReleased = false;
            bool victimPhysicsWasRestored = false;
            Task victimReleaseTask = null;

            try
            {
                if (attacker == null || victim == null || this.m_CarEntry == null) return;

                this.m_Driver?.SetVehicleEnabled(false);
                if (attacker.Player != null) attacker.Player.IsControllable = false;
                this.m_CarEntry.PrepareOccupiedEntrySide(
                    attacker,
                    this.m_ActiveEntrySide
                );
                if (!await this.m_CarEntry.MoveCharacterToEntryStandingPointAsync(attacker))
                    return;

                bool passengerSide = this.m_ActiveEntrySide ==
                    CarEntrySideMode.PassengerDoor;
                // Capture the Player's original physics before disabling GC2
                // collision. The passenger stage cannot reuse the NPC driver's
                // snapshot because both Characters coexist in the cabin.
                if (passengerSide)
                    this.m_CarEntry.LockPassengerCarjackingPhysics(attacker);
                if (attacker.Driver != null) attacker.Driver.Collision = false;
                if (passengerSide)
                {
                    passengerDoorIsOpen = true;
                    if (!await this.m_CarEntry.EnterPassengerSeatForCarjackingAsync(attacker))
                        return;

                    await this.SetDoorOpenAsync(
                        false,
                        CarEntrySideMode.PassengerDoor
                    );
                    passengerDoorIsOpen = false;

                    if (!this.m_CarEntry.ReleaseOccupantForCarjacking(victim)) return;
                    victimWasReleased = true;
                    Transform seat = this.m_CarEntry.entryParent;
                    victim.transform.SetPositionAndRotation(seat.position, seat.rotation);

                    driverDoorIsOpen = true;
                    await this.PlayPassengerSeatPushAsync(attacker, victim);
                    victimReleaseTask = this.CompleteVictimLandingAsync(victim);

                    if (attacker == null || this.m_CarEntry == null)
                    {
                        await victimReleaseTask;
                        victimPhysicsWasRestored = true;
                        return;
                    }

                    attackerSeated = await this.m_CarEntry
                        .CompletePassengerCarjackingEntryAsync(attacker);
                    await victimReleaseTask;
                    victimPhysicsWasRestored = true;
                }
                else
                {
                    // Driver-side carjacking uses the opening part of Enter Car
                    // so the body turns/reaches before the left door moves.
                    driverDoorIsOpen = true;
                    if (!await this.m_CarEntry
                            .OpenPreparedEntryDoorWithCharacterAsync(attacker))
                    {
                        return;
                    }
                    if (!this.m_CarEntry.ReleaseOccupantForCarjacking(victim)) return;
                    victimWasReleased = true;

                    Transform seat = this.m_CarEntry.entryParent;
                    victim.transform.SetPositionAndRotation(seat.position, seat.rotation);
                    await this.PlayPairedCarjackingAsync(attacker, victim);
                    victimReleaseTask = this.CompleteVictimLandingAsync(victim);

                    if (attacker == null || this.m_CarEntry == null)
                    {
                        await victimReleaseTask;
                        victimPhysicsWasRestored = true;
                        return;
                    }
                    if (attacker.Driver != null) attacker.Driver.Collision = true;

                    Task<bool> enterTask = this.m_CarEntry.EnterThroughOpenDoorAsync(
                        attacker,
                        CarEntrySideMode.DriverDoor
                    );
                    await victimReleaseTask;
                    victimPhysicsWasRestored = true;
                    attackerSeated = await enterTask;
                }

                await this.SetDoorOpenAsync(false, CarEntrySideMode.DriverDoor);
                driverDoorIsOpen = false;

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
                this.m_CarEntry?.SetPassengerPushArmFree(false);
                if (passengerDoorIsOpen)
                    await this.SetDoorOpenAsync(false, CarEntrySideMode.PassengerDoor);
                if (driverDoorIsOpen)
                    await this.SetDoorOpenAsync(false, CarEntrySideMode.DriverDoor);
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

                this.m_CarEntry?.ClearPreparedEntrySide();
                this.m_ActiveEntrySide = CarEntrySideMode.DriverDoor;
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

            AnimationClip attackerClip = this.m_AttackerKickOut;
            AnimationClip victimClip = this.m_VictimGetKickedOut;
            Transform victimLanding = this.m_VictimLandingPoint;
            Transform attackerStanding = this.m_CarEntry.ActiveEntryStandingPoint;
            Vector3 victimStartPosition = this.m_CarEntry.entryParent.position;
            Quaternion victimStartRotation = this.m_CarEntry.entryParent.rotation;
            Vector3 attackerStartPosition = attackerStanding != null
                ? attackerStanding.position
                : attacker.transform.position;
            Quaternion attackerStartRotation = attackerStanding != null
                ? attackerStanding.rotation
                : attacker.transform.rotation;
            Vector3 victimEndPosition = victimLanding != null
                ? victimLanding.position
                : victimStartPosition;
            Quaternion victimEndRotation = victimLanding != null
                ? victimLanding.rotation
                : victimStartRotation;

            this.BeginGrabIK(attacker, victim);
            _ = this.PlayGestureAsync(
                attacker,
                attackerClip,
                false
            );
            // The car owns the victim root path. The animation supplies only the
            // Humanoid body motion so GC2 physics/root motion cannot launch the NPC.
            _ = this.PlayGestureAsync(
                victim,
                victimClip,
                false
            );

            float fullDuration = Mathf.Max(
                attackerClip.length,
                victimClip.length
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

        private async Task PlayPassengerSeatPushAsync(
            Character attacker,
            Character victim)
        {
            if (attacker == null || victim == null ||
                this.m_VictimGetKickedOut == null)
            {
                return;
            }

            Transform landing = this.m_VictimLandingPoint;
            Vector3 victimStartPosition = this.m_CarEntry.entryParent.position;
            Quaternion victimStartRotation = this.m_CarEntry.entryParent.rotation;
            Vector3 victimEndPosition = landing != null
                ? landing.position
                : victimStartPosition;
            Quaternion victimEndRotation = landing != null
                ? landing.rotation
                : victimStartRotation;
            Transform passengerSeat = this.m_CarEntry.passengerEntryCabinPoint;

            this.m_CarEntry.SetPassengerPushArmFree(true);
            this.BeginPassengerPushIK(attacker, victim);
            _ = this.PlayGestureAsync(victim, this.m_VictimGetKickedOut, false);

            float duration = this.m_VictimGetKickedOut.length /
                Mathf.Max(0.01f, this.m_CarjackingAnimationSpeed) *
                Mathf.Clamp01(this.m_EnterHandoffNormalizedTime);
            float startedAt = Time.time;
            float progress = 0f;
            Task driverDoorTask = null;

            while (progress < 1f && attacker != null && victim != null)
            {
                progress = Mathf.Clamp01(
                    (Time.time - startedAt) / Mathf.Max(0.01f, duration)
                );
                if (driverDoorTask == null && progress >= 0.06f)
                {
                    // The left door begins opening only when the inside shove
                    // reaches the NPC; it never opens merely because a seat is occupied.
                    driverDoorTask = this.SetDoorOpenAsync(
                        true,
                        CarEntrySideMode.DriverDoor
                    );
                }

                float pushProgress = Mathf.InverseLerp(0.06f, 1f, progress);
                // Strong ease-out: most of the displacement happens immediately
                // after contact, then settles naturally at the landing anchor.
                float positionBlend = 1f -
                    (1f - pushProgress) * (1f - pushProgress);
                victim.transform.SetPositionAndRotation(
                    Vector3.Lerp(victimStartPosition, victimEndPosition, positionBlend),
                    Quaternion.Slerp(victimStartRotation, victimEndRotation, positionBlend)
                );
                if (passengerSeat != null)
                {
                    attacker.transform.SetPositionAndRotation(
                        passengerSeat.position,
                        passengerSeat.rotation
                    );
                }
                this.UpdatePassengerPushIK(attacker, victim, progress);
                await Task.Yield();
            }

            if (driverDoorTask == null)
            {
                driverDoorTask = this.SetDoorOpenAsync(
                    true,
                    CarEntrySideMode.DriverDoor
                );
            }
            await driverDoorTask;

            if (victim != null)
            {
                victim.transform.SetPositionAndRotation(
                    victimEndPosition,
                    victimEndRotation
                );
            }
            this.ClearGrabIK();
            this.m_CarEntry.SetPassengerPushArmFree(false);
        }

        private void BeginPassengerPushIK(Character attacker, Character victim)
        {
            this.ClearGrabIK();
            this.EnsureGrabTargets();
            Animator attackerAnimator = attacker?.GetComponentInChildren<Animator>(true);
            Animator victimAnimator = victim?.GetComponentInChildren<Animator>(true);
            if (attackerAnimator == null || victimAnimator == null ||
                !attackerAnimator.isHuman || !victimAnimator.isHuman)
            {
                return;
            }

            this.m_GrabIKSetter = attackerAnimator.GetComponent<CharacterIKSetter>();
            if (this.m_GrabIKSetter == null)
                this.m_GrabIKSetter =
                    attackerAnimator.gameObject.AddComponent<CharacterIKSetter>();

            this.m_GrabIKSetter.SetBeforeHandIK(
                this.m_CarEntry.ApplyPassengerPushPoseBeforeIK
            );

            this.m_VictimShoulder = victimAnimator.GetBoneTransform(
                HumanBodyBones.RightShoulder
            ) ?? victimAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            this.m_VictimPushLookTarget = victimAnimator.GetBoneTransform(
                HumanBodyBones.Head
            ) ?? victimAnimator.GetBoneTransform(HumanBodyBones.Neck) ??
                this.m_VictimShoulder;
            if (this.m_VictimShoulder == null)
            {
                this.ClearGrabIK();
                return;
            }
            this.m_GrabShoulderTarget.gameObject.SetActive(true);
        }

        private void UpdatePassengerPushIK(
            Character attacker,
            Character victim,
            float normalized)
        {
            float curveWeight = Mathf.Clamp01(
                this.m_PrimaryGrabIKWeight?.Evaluate(normalized) ?? 0f
            );
            this.m_CarEntry.ConfigurePassengerPushPose(
                this.m_VictimPushLookTarget,
                curveWeight,
                this.m_PassengerPushTorsoYaw,
                this.m_PassengerPushTorsoLean,
                this.m_PassengerPushHeadLookWeight
            );

            if (this.m_GrabIKSetter == null || this.m_GrabShoulderTarget == null ||
                this.m_VictimShoulder == null)
            {
                return;
            }

            Vector3 towardAttacker = attacker.Eyes - this.m_VictimShoulder.position;
            if (towardAttacker.sqrMagnitude < 0.0001f)
                towardAttacker = attacker.transform.right;
            towardAttacker.Normalize();
            this.m_GrabShoulderTarget.position = this.m_VictimShoulder.position +
                towardAttacker * this.m_ShoulderGrabSurfaceOffset;
            this.m_GrabShoulderTarget.rotation = Quaternion.LookRotation(
                -towardAttacker,
                victim.transform.up
            );

            float reach = Vector3.Distance(
                attacker.Eyes,
                this.m_VictimShoulder.position
            );
            float reachWeight = 1f - Mathf.SmoothStep(
                this.m_MaxGrabReach * 0.82f,
                this.m_MaxGrabReach,
                reach
            );
            float weight = Mathf.Clamp01(curveWeight * reachWeight);
            Transform steeringBrace = this.m_CarEntry.steeringWheelRightHandTarget;
            float braceWeight = steeringBrace != null
                ? curveWeight * this.m_PassengerPushRightBraceIKWeight
                : 0f;
            this.m_CarEntry.ConfigurePassengerPushHands(
                this.m_GrabShoulderTarget,
                steeringBrace,
                weight,
                braceWeight
            );
            this.m_GrabIKSetter.SetIKTargets(
                this.m_GrabShoulderTarget,
                steeringBrace,
                weight,
                braceWeight
            );
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
            {
                this.m_GrabIKSetter.SetIKTargets(null, null, 0f, 0f);
                this.m_GrabIKSetter.SetBeforeHandIK(null);
            }

            if (this.m_GrabNeckTarget != null)
                this.m_GrabNeckTarget.gameObject.SetActive(false);
            if (this.m_GrabShoulderTarget != null)
                this.m_GrabShoulderTarget.gameObject.SetActive(false);

            this.m_GrabIKSetter = null;
            this.m_VictimNeck = null;
            this.m_VictimShoulder = null;
            this.m_VictimPushLookTarget = null;
            this.m_CarEntry?.ConfigurePassengerPushPose(null, 0f, 0f, 0f, 0f);
        }

        private Task PlaceVictimAtLandingPointAsync(Character victim)
        {
            if (victim == null) return Task.CompletedTask;
            Transform landing = this.m_VictimLandingPoint;
            if (landing != null)
            {
                return this.m_CarEntry.CompleteOccupantReleaseAsync(
                    victim,
                    landing.position,
                    landing.rotation
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

        private async Task SetDoorOpenAsync(
            bool open,
            CarEntrySideMode side)
        {
            if (this.m_CarEntry == null) return;
            if (!this.m_CarEntry.CanAnimateDoor(side)) return;

            bool passengerSide = side ==
                CarEntrySideMode.PassengerDoor;
            Transform door = passengerSide
                ? this.m_CarEntry.passengerDoorTransform
                : this.m_CarEntry.doorTransform;
            if (door == null) return;
            Quaternion start = door.localRotation;
            Quaternion target = open
                ? Quaternion.Euler(
                    passengerSide
                        ? this.m_CarEntry.passengerDoorOpenRotation
                        : this.m_CarEntry.doorOpenRotation
                )
                : passengerSide
                    ? this.m_PassengerDoorClosedRotation
                    : this.m_DriverDoorClosedRotation;
            float duration = Mathf.Max(0.01f, this.m_CarEntry.doorRotationDuration);
            float elapsed = 0f;

            this.m_CarEntry.PlayEntryDoorSound(side, open);

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
