using System;
using System.Collections;
using Ashsvp;
using FranklinGame.UI;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Stats;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// One-shot terminal destruction for the Sim-Cade car. It keeps the frequent
    /// damage path allocation-free; renderer scanning, wheel rigidbodies and
    /// occupant handling happen only when health reaches zero.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimcadeCarHealth), typeof(SimcadeCarDriver))]
    public sealed class SimcadeCarDestruction : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        [Header("Vehicle")]
        [SerializeField] private SimcadeCarDriver m_Driver;
        [SerializeField] private CarEntry m_CarEntry;
        [SerializeField] private SimcadeVehicleController m_Controller;
        [SerializeField] private Rigidbody m_CarBody;

        [Header("Charred Appearance")]
        [SerializeField] private Color m_CharredCarColor =
            new Color(0.018f, 0.015f, 0.012f, 1f);
        [SerializeField] private Color m_CharredPlayerColor =
            new Color(0.028f, 0.022f, 0.018f, 1f);

        [Header("Detached Wheels")]
        [SerializeField, Min(0f)] private float m_WheelRadialSpeed = 5.5f;
        [SerializeField, Min(0f)] private float m_WheelUpwardSpeed = 3.2f;
        [SerializeField, Min(0f)] private float m_WheelSpinSpeed = 18f;
        [SerializeField, Min(0.1f)] private float m_WheelMinimumFlightTime = 0.85f;
        [SerializeField, Min(0.5f)] private float m_WheelMaximumFlightTime = 3.5f;
        [SerializeField, Min(0f)] private float m_WheelRestDuration = 5f;
        [SerializeField, Min(0.1f)] private float m_WheelSinkDuration = 0.8f;
        [SerializeField, Min(0.05f)] private float m_WheelSinkDistance = 0.48f;

        [Header("Wreck Cleanup")]
        [SerializeField, Min(1f)] private float m_WreckMinimumVisibleDuration = 15f;
        [SerializeField, Min(1f)] private float m_WreckHideDistance = 35f;
        [SerializeField, Range(0.25f, 2f)] private float m_WreckVisibilityCheckInterval = 0.5f;

        [Header("Occupant Ejection")]
        [SerializeField, Min(0.25f)] private float m_OccupantSideClearance = 1.35f;
        [SerializeField, Min(0f)] private float m_OccupantRadialSpeed = 1.8f;
        [SerializeField, Min(0f)] private float m_OccupantUpwardSpeed = 0.75f;
        [SerializeField, Range(0f, 1f)] private float m_OccupantInheritedVelocity = 0.2f;
        [SerializeField] private string m_PlayerHealthAttributeId = "hp";
        [SerializeField] private GameObject m_OccupantBurnFire;
        [SerializeField, Min(0.5f)] private float m_OccupantBurnDuration = 4f;

        [Header("Explosion Camera")]
        [Tooltip("Time used to move the active Car camera target back to the ragdoll Player.")]
        [SerializeField, Min(0f)] private float m_ExplosionCameraReturnDuration = 0.75f;

        private bool m_IsDestroyed;
        private MaterialPropertyBlock m_PropertyBlock;
        private Character m_CleanupPlayer;
        private Renderer[] m_WreckRenderers;
        private Plane[] m_FrustumPlanes;
        private float m_NextPlayerSearchAt;

        public bool IsDestroyed => m_IsDestroyed;
        public float WheelRestDuration => m_WheelRestDuration;
        public float WreckMinimumVisibleDuration => m_WreckMinimumVisibleDuration;
        public float WreckHideDistance => m_WreckHideDistance;
        public string PlayerHealthAttributeId => m_PlayerHealthAttributeId;
        public float ExplosionCameraReturnDuration => m_ExplosionCameraReturnDuration;
        public bool IsConfigured => m_Driver != null && m_CarEntry != null &&
            m_Controller != null && m_CarBody != null && m_OccupantBurnFire != null &&
            m_Controller.Wheels != null && m_Controller.Wheels.Length == 4 &&
            m_WheelRestDuration >= 4.9f && m_WreckMinimumVisibleDuration >= 15f &&
            m_WreckHideDistance >= 1f;

        private void Awake()
        {
            if (m_Driver == null) m_Driver = GetComponent<SimcadeCarDriver>();
            if (m_CarEntry == null) m_CarEntry = GetComponent<CarEntry>();
            if (m_Controller == null) m_Controller = GetComponent<SimcadeVehicleController>();
            if (m_CarBody == null) m_CarBody = GetComponent<Rigidbody>();
        }

        public void Configure(
            SimcadeCarDriver driver,
            CarEntry carEntry,
            SimcadeVehicleController controller,
            Rigidbody carBody,
            GameObject occupantBurnFire,
            string playerHealthAttributeId)
        {
            m_Driver = driver;
            m_CarEntry = carEntry;
            m_Controller = controller;
            m_CarBody = carBody;
            m_OccupantBurnFire = occupantBurnFire;
            m_PlayerHealthAttributeId = string.IsNullOrWhiteSpace(playerHealthAttributeId)
                ? "hp"
                : playerHealthAttributeId;
            m_ExplosionCameraReturnDuration = 0.75f;
        }

        public void TriggerDestruction(float explosionCameraHoldDuration = 1.25f)
        {
            if (m_IsDestroyed) return;
            m_IsDestroyed = true;

            Character driver = m_CarEntry != null ? m_CarEntry.SeatedCharacter : null;
            Character rearLeft = m_CarEntry != null
                ? m_CarEntry.RearLeftSeatedCharacter
                : null;
            Character rearRight = m_CarEntry != null
                ? m_CarEntry.RearRightSeatedCharacter
                : null;
            Character cameraPlayer = GetPlayerCharacter(driver) ??
                GetPlayerCharacter(rearLeft) ?? GetPlayerCharacter(rearRight);

            bool cameraHeld = m_Driver != null &&
                m_Driver.BeginDestructionCameraHold();
            m_Driver?.SetDestroyed();
            ApplyCharredAppearance(gameObject, m_CharredCarColor);
            DetachAndThrowWheels();

            EjectOccupant(driver, 0);
            if (rearLeft != driver) EjectOccupant(rearLeft, 1);
            if (rearRight != driver && rearRight != rearLeft) EjectOccupant(rearRight, 2);

            if (cameraHeld)
            {
                m_Driver.ReturnDestructionCameraToPlayer(
                    cameraPlayer,
                    Mathf.Max(0f, explosionCameraHoldDuration),
                    m_ExplosionCameraReturnDuration
                );
            }

            BeginWreckCleanup(driver, rearLeft, rearRight);
        }

        private void DetachAndThrowWheels()
        {
            if (m_Controller == null || m_Controller.Wheels == null) return;

            Vector3 origin = transform.position + Vector3.up * 0.3f;
            Vector3 inheritedVelocity = m_CarBody != null
                ? m_CarBody.linearVelocity * 0.25f
                : Vector3.zero;
            float wheelRadius = Mathf.Max(0.15f, m_Controller.wheelRadius * 0.82f);

            for (int i = 0; i < m_Controller.Wheels.Length; ++i)
            {
                Transform wheel = m_Controller.Wheels[i];
                if (wheel == null) continue;

                StopWheelPresentation(wheel);
                Vector3 radial = Vector3.ProjectOnPlane(wheel.position - origin, Vector3.up);
                if (radial.sqrMagnitude < 0.001f)
                {
                    radial = i % 2 == 0 ? -transform.right : transform.right;
                }
                radial.Normalize();

                wheel.SetParent(null, true);
                Collider wheelCollider = wheel.GetComponent<Collider>();
                if (wheelCollider == null)
                {
                    SphereCollider sphere = wheel.gameObject.AddComponent<SphereCollider>();
                    Vector3 scale = wheel.lossyScale;
                    float maxScale = Mathf.Max(
                        0.001f,
                        Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                    );
                    sphere.radius = wheelRadius / maxScale;
                    wheelCollider = sphere;
                }
                wheelCollider.enabled = true;

                Rigidbody wheelBody = wheel.GetComponent<Rigidbody>();
                if (wheelBody == null) wheelBody = wheel.gameObject.AddComponent<Rigidbody>();
                wheelBody.isKinematic = false;
                wheelBody.useGravity = true;
                wheelBody.detectCollisions = true;
                wheelBody.mass = 18f;
                wheelBody.linearDamping = 0.16f;
                wheelBody.angularDamping = 0.08f;
                wheelBody.maxAngularVelocity = Mathf.Max(20f, m_WheelSpinSpeed * 1.4f);
                wheelBody.interpolation = RigidbodyInterpolation.Interpolate;
                wheelBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                wheelBody.linearVelocity = inheritedVelocity +
                    radial * m_WheelRadialSpeed + Vector3.up * m_WheelUpwardSpeed;
                wheelBody.angularVelocity = UnityEngine.Random.onUnitSphere * m_WheelSpinSpeed;

                SimcadeDetachedWheelCleanup cleanup =
                    wheel.gameObject.AddComponent<SimcadeDetachedWheelCleanup>();
                cleanup.Configure(
                    wheelBody,
                    wheelCollider,
                    wheelRadius,
                    m_WheelMinimumFlightTime,
                    m_WheelMaximumFlightTime,
                    m_WheelRestDuration,
                    m_WheelSinkDuration,
                    m_WheelSinkDistance
                );
            }
        }

        private void BeginWreckCleanup(
            Character driver,
            Character rearLeft,
            Character rearRight)
        {
            m_CleanupPlayer = GetPlayerCharacter(driver) ??
                GetPlayerCharacter(rearLeft) ?? GetPlayerCharacter(rearRight) ??
                FindPlayerCharacter();
            m_WreckRenderers = GetComponentsInChildren<Renderer>(true);
            m_FrustumPlanes = new Plane[6];
            StartCoroutine(HideWreckWhenEligible());
        }

        private IEnumerator HideWreckWhenEligible()
        {
            yield return new WaitForSecondsRealtime(m_WreckMinimumVisibleDuration);
            WaitForSecondsRealtime interval =
                new WaitForSecondsRealtime(m_WreckVisibilityCheckInterval);

            while (gameObject.activeSelf)
            {
                if (m_CleanupPlayer == null &&
                    Time.unscaledTime >= m_NextPlayerSearchAt)
                {
                    m_CleanupPlayer = FindPlayerCharacter();
                    m_NextPlayerSearchAt = Time.unscaledTime + 4f;
                }
                Camera gameplayCamera = Camera.main;
                if (m_CleanupPlayer != null && gameplayCamera != null)
                {
                    float hideDistanceSquared = m_WreckHideDistance * m_WreckHideDistance;
                    bool playerIsFar =
                        (m_CleanupPlayer.transform.position - transform.position).sqrMagnitude >=
                        hideDistanceSquared;
                    if (playerIsFar && !IsVisibleFrom(gameplayCamera))
                    {
                        gameObject.SetActive(false);
                        yield break;
                    }
                }

                yield return interval;
            }
        }

        private bool IsVisibleFrom(Camera gameplayCamera)
        {
            if (m_WreckRenderers == null || m_WreckRenderers.Length == 0) return false;

            bool hasBounds = false;
            Bounds combinedBounds = default;
            for (int i = 0; i < m_WreckRenderers.Length; ++i)
            {
                Renderer renderer = m_WreckRenderers[i];
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    renderer is ParticleSystemRenderer || renderer is TrailRenderer ||
                    renderer is LineRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds) return false;
            GeometryUtility.CalculateFrustumPlanes(gameplayCamera, m_FrustumPlanes);
            return GeometryUtility.TestPlanesAABB(m_FrustumPlanes, combinedBounds);
        }

        private static Character GetPlayerCharacter(Character character)
        {
            return character != null && character.Player != null ? character : null;
        }

        private static Character FindPlayerCharacter()
        {
            Character[] characters = FindObjectsByType<Character>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
            for (int i = 0; i < characters.Length; ++i)
            {
                if (characters[i] != null && characters[i].Player != null)
                    return characters[i];
            }
            return null;
        }

        private static void StopWheelPresentation(Transform wheel)
        {
            WheelSkid[] skids = wheel.GetComponentsInChildren<WheelSkid>(true);
            for (int i = 0; i < skids.Length; ++i) skids[i].enabled = false;

            ParticleSystem[] particles = wheel.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; ++i)
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            AudioSource[] audioSources = wheel.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; ++i) audioSources[i].Stop();
        }

        private void EjectOccupant(Character character, int seatIndex)
        {
            if (character == null || m_CarEntry == null) return;

            Vector3 side = seatIndex == 2 ? transform.right : -transform.right;
            float longitudinal = seatIndex == 0 ? 0.15f : -0.65f;
            Vector3 ejectionPosition = GetCarCenter() +
                side * m_OccupantSideClearance +
                transform.forward * longitudinal + Vector3.up * 0.25f;
            Vector3 inheritedVelocity = m_CarBody != null
                ? m_CarBody.linearVelocity * m_OccupantInheritedVelocity
                : Vector3.zero;
            Vector3 ejectionVelocity = inheritedVelocity +
                side * m_OccupantRadialSpeed + Vector3.up * m_OccupantUpwardSpeed;

            // This async API performs the detach/physics restore synchronously
            // before its first await. Do it before setting Traits health to zero,
            // so any GC2 death reaction can never run while the Player is still
            // parented to a seat inside the car.
            _ = m_CarEntry.ForceEjectForDestructionAsync(
                character,
                ejectionPosition,
                character.transform.rotation,
                ejectionVelocity
            );

            bool isPlayer = character.Player != null;
            if (isPlayer)
            {
                ApplyCharredAppearance(character.gameObject, m_CharredPlayerColor);
                SetPlayerHealthToZero(character);
                FranklinMobileHud.SetControlsSuppressed(true);
                AttachBurnFire(character);
            }
        }

        private Vector3 GetCarCenter()
        {
            BoxCollider bodyCollider = GetComponent<BoxCollider>();
            return bodyCollider != null
                ? bodyCollider.bounds.center
                : transform.position + Vector3.up * 0.65f;
        }

        private void SetPlayerHealthToZero(Character character)
        {
            Traits traits = character.GetComponent<Traits>();
            if (traits == null) return;

            try
            {
                RuntimeAttributeData health = traits.RuntimeAttributes.Get(
                    m_PlayerHealthAttributeId
                );
                if (health != null) health.Value = 0d;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Explosion could not set Player Traits '{m_PlayerHealthAttributeId}' to 0: " +
                    exception.Message,
                    character
                );
            }
        }

        private void AttachBurnFire(Character character)
        {
            if (m_OccupantBurnFire == null) return;
            Animator animator = character.Animim?.Animator;
            Transform anchor = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : character.transform;
            if (anchor == null) anchor = character.transform;

            m_OccupantBurnFire.transform.SetParent(anchor, false);
            m_OccupantBurnFire.transform.localPosition = Vector3.zero;
            m_OccupantBurnFire.transform.localRotation = Quaternion.identity;
            m_OccupantBurnFire.SetActive(true);
            ParticleSystem[] particles =
                m_OccupantBurnFire.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; ++i)
            {
                particles[i].Clear(true);
                particles[i].Play(true);
            }
            StartCoroutine(StopOccupantFireAfterDelay(particles));
        }

        private IEnumerator StopOccupantFireAfterDelay(ParticleSystem[] particles)
        {
            yield return new WaitForSecondsRealtime(m_OccupantBurnDuration);
            for (int i = 0; i < particles.Length; ++i)
            {
                if (particles[i] != null)
                    particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            yield return new WaitForSecondsRealtime(1f);
            if (m_OccupantBurnFire != null) m_OccupantBurnFire.SetActive(false);
        }

        private void ApplyCharredAppearance(GameObject root, Color color)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            m_PropertyBlock ??= new MaterialPropertyBlock();
            for (int rendererIndex = 0; rendererIndex < renderers.Length; ++rendererIndex)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer || renderer is LineRenderer)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; ++materialIndex)
                {
                    Material material = materials[materialIndex];
                    if (material == null) continue;
                    m_PropertyBlock.Clear();
                    renderer.GetPropertyBlock(m_PropertyBlock, materialIndex);
                    if (material.HasProperty(BaseColorId))
                        m_PropertyBlock.SetColor(BaseColorId, color);
                    if (material.HasProperty(ColorId))
                        m_PropertyBlock.SetColor(ColorId, color);
                    if (material.HasProperty(EmissionColorId))
                        m_PropertyBlock.SetColor(EmissionColorId, Color.black);
                    if (material.HasProperty(EmissiveColorId))
                        m_PropertyBlock.SetColor(EmissiveColorId, Color.black);
                    renderer.SetPropertyBlock(m_PropertyBlock, materialIndex);
                }
            }
        }

        private void OnValidate()
        {
            m_WheelRadialSpeed = Mathf.Max(0f, m_WheelRadialSpeed);
            m_WheelUpwardSpeed = Mathf.Max(0f, m_WheelUpwardSpeed);
            m_WheelSpinSpeed = Mathf.Max(0f, m_WheelSpinSpeed);
            m_WheelMinimumFlightTime = Mathf.Max(0.1f, m_WheelMinimumFlightTime);
            m_WheelMaximumFlightTime = Mathf.Max(
                m_WheelMinimumFlightTime + 0.25f,
                m_WheelMaximumFlightTime
            );
            m_WheelRestDuration = Mathf.Max(0f, m_WheelRestDuration);
            m_WheelSinkDuration = Mathf.Max(0.1f, m_WheelSinkDuration);
            m_WheelSinkDistance = Mathf.Max(0.05f, m_WheelSinkDistance);
            m_WreckMinimumVisibleDuration = Mathf.Max(1f, m_WreckMinimumVisibleDuration);
            m_WreckHideDistance = Mathf.Max(1f, m_WreckHideDistance);
            m_WreckVisibilityCheckInterval = Mathf.Clamp(
                m_WreckVisibilityCheckInterval,
                0.25f,
                2f
            );
            m_OccupantSideClearance = Mathf.Max(0.25f, m_OccupantSideClearance);
            m_OccupantRadialSpeed = Mathf.Max(0f, m_OccupantRadialSpeed);
            m_OccupantUpwardSpeed = Mathf.Max(0f, m_OccupantUpwardSpeed);
            m_OccupantBurnDuration = Mathf.Max(0.5f, m_OccupantBurnDuration);
            m_ExplosionCameraReturnDuration = Mathf.Max(
                0f,
                m_ExplosionCameraReturnDuration
            );
            if (string.IsNullOrWhiteSpace(m_PlayerHealthAttributeId))
                m_PlayerHealthAttributeId = "hp";
        }
    }
}
