using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// A GC2 Character seat behind the rider. It owns only the passenger pose,
    /// attachment and physics snapshot; BikeEntry remains the sole driver and
    /// camera/controller owner.
    /// </summary>
    [AddComponentMenu("Game Creator/Mechanics/Bike Passenger Seat")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BikeEntry))]
    public sealed class FranklinBikePassengerSeat : MonoBehaviour
    {
        private const int PassengerGravityLockKey = 0x50494C4C;
        private const int CurrentConfigurationVersion = 1;

        [Header("Passenger Targets")]
        [SerializeField] private Transform m_Seat;
        [SerializeField] private Transform m_EntryPointLeft;
        [SerializeField] private Transform m_EntryPointRight;
        [SerializeField] private Transform m_LeftHandTarget;
        [SerializeField] private Transform m_RightHandTarget;
        [SerializeField] private Transform m_LeftFootTarget;
        [SerializeField] private Transform m_RightFootTarget;

        [Header("Passenger Pose")]
        [SerializeField] private StateData m_SeatedState =
            new StateData(StateData.StateType.State);
        [SerializeField, Min(0)] private int m_StateLayer = 1;
        [SerializeField, Min(0f)] private float m_StateTransition = 0.16f;
        [SerializeField] private Vector3 m_SeatPositionOffset = Vector3.zero;
        [SerializeField] private Vector3 m_SeatRotationOffset = Vector3.zero;
        [SerializeField, Range(0f, 1f)] private float m_HandIKWeight = 0.78f;
        [SerializeField, Range(0f, 1f)] private float m_HandRotationWeight = 0.2f;
        [SerializeField, Range(0f, 1f)] private float m_FootIKWeight = 1f;

        [Header("Approach And Exit")]
        [SerializeField, Min(0.15f)] private float m_ApproachStopDistance = 0.22f;
        [SerializeField, Min(0.1f)] private float m_ApproachTimeout = 5f;
        [SerializeField, Min(0f)] private float m_AlignmentDuration = 0.25f;
        [SerializeField, Min(1)] private int m_MotionPriority = 11;
        [Tooltip("Passenger always exits to the authored left point when available.")]
        [SerializeField] private bool m_ExitToLeft = true;

        [Header("Safety")]
        [Tooltip("Reject passenger entry while there is no rider in the driver seat.")]
        [SerializeField] private bool m_RequireDriver = true;
        [SerializeField, HideInInspector] private int m_ConfigurationVersion;

        private BikeEntry m_BikeEntry;
        private FranklinBikeHealth m_BikeHealth;
        private FranklinArcadeBikeRagdoll m_BikeRagdoll;
        private Character m_Passenger;
        private CharacterIKSetter m_PassengerIk;
        private bool m_IsTransitioning;
        private int m_OperationVersion;
        private bool m_ApproachFinished;
        private bool m_ApproachSucceeded;
        private int m_ApproachVersion;
        private CharacterPhysicsSnapshot m_PhysicsSnapshot;
        private readonly List<Collider> m_ColliderBuffer = new List<Collider>(12);
        private readonly List<Rigidbody> m_RigidbodyBuffer = new List<Rigidbody>(8);

        private struct ColliderState
        {
            public Collider Collider;
            public bool Enabled;
        }

        private struct RigidbodyState
        {
            public Rigidbody Body;
            public bool IsKinematic;
            public bool UseGravity;
            public bool DetectCollisions;
            public RigidbodyConstraints Constraints;
        }

        private sealed class CharacterPhysicsSnapshot
        {
            public Character Character;
            public bool DriverCollision;
            public bool DriverUpdateKinematics;
            public Character.MovementType MovementType;
            public readonly List<ColliderState> Colliders = new List<ColliderState>(12);
            public readonly List<RigidbodyState> Rigidbodies = new List<RigidbodyState>(8);
        }

        public Character Passenger => m_Passenger;
        public bool IsOccupied => m_Passenger != null;
        public bool IsTransitioning => m_IsTransitioning;
        public bool IsConfigured => m_Seat != null && m_EntryPointLeft != null;
        public bool HasCurrentConfiguration =>
            m_ConfigurationVersion >= CurrentConfigurationVersion;

        public void Configure(
            Transform seat,
            Transform entryLeft,
            Transform entryRight,
            Transform leftHand,
            Transform rightHand,
            Transform leftFoot,
            Transform rightFoot,
            AnimationClip seatedPose,
            AvatarMask mask)
        {
            m_Seat = seat;
            m_EntryPointLeft = entryLeft;
            m_EntryPointRight = entryRight;
            m_LeftHandTarget = leftHand;
            m_RightHandTarget = rightHand;
            m_LeftFootTarget = leftFoot;
            m_RightFootTarget = rightFoot;
            if (seatedPose != null) m_SeatedState = new StateData(seatedPose, mask);
            m_ConfigurationVersion = CurrentConfigurationVersion;
        }

        public bool RequestEnter(Character character)
        {
            if (!CanEnter(character)) return false;
            _ = EnterAsync(character);
            return true;
        }

        public bool RequestExit(Character character)
        {
            if (character == null || character != m_Passenger || m_IsTransitioning)
                return false;
            _ = ExitAsync(character);
            return true;
        }

        public bool Toggle(Character character)
        {
            return character != null && character == m_Passenger
                ? RequestExit(character)
                : RequestEnter(character);
        }

        public bool ReleaseForCrash(Character character)
        {
            if (character == null || character != m_Passenger) return false;
            ++m_OperationVersion;
            m_IsTransitioning = false;
            ReleasePassenger(character, false);
            return true;
        }

        private bool CanEnter(Character character)
        {
            return character != null && character.Motion != null && IsConfigured &&
                   !m_IsTransitioning && m_Passenger == null &&
                   character != m_BikeEntry?.SeatedCharacter &&
                   (!m_RequireDriver || m_BikeEntry?.SeatedCharacter != null) &&
                   (m_BikeHealth == null || !m_BikeHealth.IsDestroyed) &&
                   (m_BikeRagdoll == null || !m_BikeRagdoll.IsRagdoll);
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            m_StateLayer = Mathf.Max(0, m_StateLayer);
            m_StateTransition = Mathf.Max(0f, m_StateTransition);
            m_ApproachStopDistance = Mathf.Max(0.15f, m_ApproachStopDistance);
            m_ApproachTimeout = Mathf.Max(0.1f, m_ApproachTimeout);
            m_AlignmentDuration = Mathf.Max(0f, m_AlignmentDuration);
            m_MotionPriority = Mathf.Max(1, m_MotionPriority);
        }

        private void LateUpdate()
        {
            if (m_Passenger == null || m_Seat == null) return;
            if (m_RequireDriver && m_BikeEntry?.SeatedCharacter == null &&
                !m_IsTransitioning)
            {
                RequestExit(m_Passenger);
                return;
            }
            m_Passenger.transform.localPosition = m_SeatPositionOffset;
            m_Passenger.transform.localRotation = Quaternion.Euler(m_SeatRotationOffset);
        }

        private void OnDestroy()
        {
            ++m_OperationVersion;
            if (m_Passenger != null) ReleasePassenger(m_Passenger, false);
        }

        private async Task EnterAsync(Character character)
        {
            m_IsTransitioning = true;
            int version = ++m_OperationVersion;
            bool wasControllable = character.Player?.IsControllable ?? false;
            if (character.Player != null) character.Player.IsControllable = false;

            try
            {
                Transform entryPoint = SelectNearestEntryPoint(character);
                if (!await MoveToEntryPoint(character, entryPoint, version) ||
                    !IsCurrent(version) || !CanEnterDuringTransition(character))
                {
                    return;
                }

                LockCharacterPhysics(character);
                character.transform.SetParent(m_Seat, false);
                character.transform.localPosition = m_SeatPositionOffset;
                character.transform.localRotation = Quaternion.Euler(m_SeatRotationOffset);
                m_Passenger = character;

                Animator animator = character.GetComponentInChildren<Animator>();
                m_PassengerIk = animator != null
                    ? animator.GetComponent<CharacterIKSetter>() ??
                      animator.gameObject.AddComponent<CharacterIKSetter>()
                    : null;
                if (m_PassengerIk != null)
                {
                    m_PassengerIk.SetIKTargets(
                        m_LeftHandTarget,
                        m_RightHandTarget,
                        m_LeftHandTarget != null ? m_HandIKWeight : 0f,
                        m_RightHandTarget != null ? m_HandIKWeight : 0f,
                        m_LeftHandTarget != null ? m_HandRotationWeight : 0f,
                        m_RightHandTarget != null ? m_HandRotationWeight : 0f
                    );
                    m_PassengerIk.SetFootIKTargets(
                        m_LeftFootTarget,
                        m_RightFootTarget,
                        m_LeftFootTarget != null ? m_FootIKWeight : 0f,
                        m_RightFootTarget != null ? m_FootIKWeight : 0f
                    );
                }

                ConfigState config = new ConfigState(
                    0f, 1f, 1f, m_StateTransition, m_StateTransition
                );
                _ = character.States.SetState(
                    m_SeatedState,
                    m_StateLayer,
                    BlendMode.Blend,
                    config
                );

                character.EventDie += OnPassengerUnavailable;
                character.Ragdoll.EventBeforeStartRagdoll += OnPassengerUnavailable;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (m_Passenger == character) ReleasePassenger(character, false);
            }
            finally
            {
                if (IsCurrent(version)) m_IsTransitioning = false;
                if (m_Passenger != character && character != null && character.Player != null)
                    character.Player.IsControllable = wasControllable;
            }
        }

        private async Task ExitAsync(Character character)
        {
            m_IsTransitioning = true;
            int version = ++m_OperationVersion;
            Transform exitPoint = m_ExitToLeft || m_EntryPointRight == null
                ? m_EntryPointLeft
                : SelectNearestEntryPoint(character);
            Vector3 exitPosition = exitPoint != null
                ? exitPoint.position
                : transform.position - transform.right * 0.8f;
            Quaternion exitRotation = exitPoint != null
                ? exitPoint.rotation
                : transform.rotation;

            ReleasePassenger(character, false);
            await AlignCharacter(character, exitPosition, exitRotation, version);
            if (IsCurrent(version) && character != null && character.Player != null)
                character.Player.IsControllable = true;
            if (IsCurrent(version)) m_IsTransitioning = false;
        }

        private void ReleasePassenger(Character character, bool restoreControl)
        {
            if (character == null) return;
            character.EventDie -= OnPassengerUnavailable;
            character.Ragdoll.EventBeforeStartRagdoll -= OnPassengerUnavailable;
            if (m_PassengerIk != null)
            {
                m_PassengerIk.SetIKTargets(null, null, 0f, 0f, 0f, 0f);
                m_PassengerIk.SetFootIKTargets(null, null, 0f, 0f);
                m_PassengerIk = null;
            }
            character.States.Stop(m_StateLayer, 0f, m_StateTransition);
            character.transform.SetParent(null, true);
            RestoreCharacterPhysics(character);
            if (restoreControl && character.Player != null)
                character.Player.IsControllable = true;
            if (m_Passenger == character) m_Passenger = null;
            Physics.SyncTransforms();
        }

        private void OnPassengerUnavailable()
        {
            Character character = m_Passenger;
            if (character == null) return;
            ++m_OperationVersion;
            m_IsTransitioning = false;
            ReleasePassenger(character, false);
        }

        private Transform SelectNearestEntryPoint(Character character)
        {
            if (m_EntryPointRight == null || character == null) return m_EntryPointLeft;
            float left = (character.transform.position - m_EntryPointLeft.position).sqrMagnitude;
            float right = (character.transform.position - m_EntryPointRight.position).sqrMagnitude;
            return right < left ? m_EntryPointRight : m_EntryPointLeft;
        }

        private async Task<bool> MoveToEntryPoint(
            Character character,
            Transform point,
            int operationVersion)
        {
            if (character == null || character.Motion == null || point == null) return false;
            m_ApproachFinished = false;
            m_ApproachSucceeded = false;
            int approachVersion = ++m_ApproachVersion;
            Vector3 feet = point.position - Vector3.up * (character.Motion.Height * 0.5f);
            character.Motion.MoveToLocation(
                new Location(feet, point.rotation),
                m_ApproachStopDistance,
                (callbackCharacter, success) =>
                {
                    if (approachVersion != m_ApproachVersion || callbackCharacter != character)
                        return;
                    m_ApproachSucceeded = success;
                    m_ApproachFinished = true;
                },
                m_MotionPriority
            );

            float deadline = Time.unscaledTime + m_ApproachTimeout;
            while (IsCurrent(operationVersion) && character != null &&
                   !m_ApproachFinished && Time.unscaledTime < deadline)
            {
                await Task.Yield();
            }
            if (!IsCurrent(operationVersion) || character == null) return false;

            Vector3 horizontal = Vector3.ProjectOnPlane(
                character.transform.position - point.position,
                Vector3.up
            );
            bool closeEnough = horizontal.sqrMagnitude <=
                               Mathf.Pow(m_ApproachStopDistance + 0.08f, 2f);
            if (!m_ApproachSucceeded && !closeEnough) return false;
            await AlignCharacter(character, point.position, point.rotation, operationVersion);
            return IsCurrent(operationVersion) && character != null;
        }

        private async Task AlignCharacter(
            Character character,
            Vector3 targetPosition,
            Quaternion targetRotation,
            int version)
        {
            if (character == null) return;
            Vector3 startPosition = character.transform.position;
            Quaternion startRotation = character.transform.rotation;
            float elapsed = 0f;
            while (IsCurrent(version) && character != null && elapsed < m_AlignmentDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = m_AlignmentDuration > 0f
                    ? Mathf.SmoothStep(0f, 1f, elapsed / m_AlignmentDuration)
                    : 1f;
                character.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, targetPosition, t),
                    Quaternion.Slerp(startRotation, targetRotation, t)
                );
                await Task.Yield();
            }
            if (IsCurrent(version) && character != null)
                character.transform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        private bool CanEnterDuringTransition(Character character)
        {
            return character != null && m_Passenger == null &&
                   character != m_BikeEntry?.SeatedCharacter &&
                   (!m_RequireDriver || m_BikeEntry?.SeatedCharacter != null) &&
                   (m_BikeHealth == null || !m_BikeHealth.IsDestroyed) &&
                   (m_BikeRagdoll == null || !m_BikeRagdoll.IsRagdoll);
        }

        private bool IsCurrent(int version)
        {
            return this != null && isActiveAndEnabled && version == m_OperationVersion;
        }

        private void LockCharacterPhysics(Character character)
        {
            RestoreCharacterPhysics(m_PhysicsSnapshot?.Character);
            CharacterPhysicsSnapshot snapshot = new CharacterPhysicsSnapshot
            {
                Character = character,
                DriverCollision = character.Driver?.Collision ?? false,
                DriverUpdateKinematics = character.Driver?.UpdateKinematics ?? false,
                MovementType = character.Motion?.MovementType ?? Character.MovementType.None
            };

            character.Motion?.StopToDirection();
            character.Motion?.StopFollowingTarget();
            if (character.Motion != null)
                character.Motion.MovementType = Character.MovementType.None;
            if (character.Driver != null)
            {
                character.Driver.ResetVerticalVelocity();
                character.Driver.ForceGrounded(true);
                character.Driver.SetGravityInfluence(PassengerGravityLockKey, 0f);
                character.Driver.UpdateKinematics = false;
                character.Driver.Collision = false;
            }

            m_ColliderBuffer.Clear();
            character.GetComponentsInChildren(true, m_ColliderBuffer);
            foreach (Collider collider in m_ColliderBuffer)
            {
                snapshot.Colliders.Add(new ColliderState
                {
                    Collider = collider,
                    Enabled = collider.enabled
                });
                collider.enabled = false;
            }
            m_ColliderBuffer.Clear();

            m_RigidbodyBuffer.Clear();
            character.GetComponentsInChildren(true, m_RigidbodyBuffer);
            foreach (Rigidbody body in m_RigidbodyBuffer)
            {
                snapshot.Rigidbodies.Add(new RigidbodyState
                {
                    Body = body,
                    IsKinematic = body.isKinematic,
                    UseGravity = body.useGravity,
                    DetectCollisions = body.detectCollisions,
                    Constraints = body.constraints
                });
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.detectCollisions = false;
                body.isKinematic = true;
                body.constraints = RigidbodyConstraints.FreezeAll;
            }
            m_RigidbodyBuffer.Clear();
            m_PhysicsSnapshot = snapshot;
            Physics.SyncTransforms();
        }

        private void RestoreCharacterPhysics(Character character)
        {
            CharacterPhysicsSnapshot snapshot = m_PhysicsSnapshot;
            if (snapshot == null || snapshot.Character != character) return;
            foreach (RigidbodyState state in snapshot.Rigidbodies)
            {
                if (state.Body == null) continue;
                state.Body.constraints = state.Constraints;
                state.Body.useGravity = state.UseGravity;
                state.Body.detectCollisions = state.DetectCollisions;
                state.Body.isKinematic = state.IsKinematic;
                state.Body.linearVelocity = Vector3.zero;
                state.Body.angularVelocity = Vector3.zero;
            }
            foreach (ColliderState state in snapshot.Colliders)
                if (state.Collider != null) state.Collider.enabled = state.Enabled;
            if (character.Driver != null)
            {
                character.Driver.RemoveGravityInfluence(PassengerGravityLockKey);
                character.Driver.UpdateKinematics = snapshot.DriverUpdateKinematics;
                character.Driver.Collision = snapshot.DriverCollision;
                character.Driver.ResetVerticalVelocity();
                character.Driver.ForceGrounded(false);
            }
            if (character.Motion != null)
                character.Motion.MovementType = snapshot.MovementType;
            snapshot.Character = null;
            snapshot.Colliders.Clear();
            snapshot.Rigidbodies.Clear();
            m_PhysicsSnapshot = null;
        }

        private void ResolveReferences()
        {
            if (m_BikeEntry == null) m_BikeEntry = GetComponent<BikeEntry>();
            if (m_BikeHealth == null) m_BikeHealth = GetComponent<FranklinBikeHealth>();
            if (m_BikeRagdoll == null)
                m_BikeRagdoll = GetComponent<FranklinArcadeBikeRagdoll>();
        }
    }
}
