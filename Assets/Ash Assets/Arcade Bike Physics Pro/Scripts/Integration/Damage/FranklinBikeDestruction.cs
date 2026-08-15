using System;
using System.Collections;
using FranklinGame.UI;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Stats;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Terminal Bike destruction. The real Arcade Bike Rigidbody becomes a
    /// dynamic fallen wreck; both complete wheel targets and one central mechanical
    /// assembly become short-lived physical debris. The Main Camera is untouched.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FranklinBikeHealth), typeof(FranklinArcadeBikeDriver))]
    public sealed class FranklinBikeDestruction : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        [Header("Bike")]
        [SerializeField] private FranklinBikeHealth m_Health;
        [SerializeField] private FranklinArcadeBikeDriver m_Driver;
        [SerializeField] private FranklinArcadeBikeRagdoll m_BikeRagdoll;
        [SerializeField] private BikeEntry m_BikeEntry;
        [SerializeField] private FranklinBikePassengerSeat m_PassengerSeat;
        [SerializeField] private Rigidbody m_BikeBody;
        [SerializeField] private Renderer[] m_BikeRenderers = Array.Empty<Renderer>();

        [Header("Explosion Physics")]
        [SerializeField, Min(0f)] private float m_UpwardVelocity = 2.2f;
        [SerializeField, Min(0f)] private float m_SideVelocity = 1.4f;
        [SerializeField, Min(0f)] private float m_ToppleAngularVelocity = 3.8f;

        [Header("Detached Explosion Parts")]
        [SerializeField] private Transform m_FrontWheelDebris;
        [SerializeField] private Transform m_RearWheelDebris;
        [SerializeField] private Transform m_EngineDebris;
        [SerializeField, Min(0f)] private float m_WheelSideSpeed = 4.4f;
        [SerializeField, Min(0f)] private float m_WheelUpwardSpeed = 3f;
        [SerializeField, Min(0f)] private float m_WheelSpinSpeed = 18f;
        [SerializeField, Min(0f)] private float m_EngineSideSpeed = 3.2f;
        [SerializeField, Min(0f)] private float m_EngineUpwardSpeed = 2.6f;
        [SerializeField, Min(0f)] private float m_EngineSpinSpeed = 9f;
        [SerializeField, Min(0.1f)] private float m_DebrisMinimumFlightTime = 0.7f;
        [SerializeField, Min(0.5f)] private float m_DebrisMaximumFlightTime = 3.25f;
        [SerializeField, Min(0f)] private float m_DebrisRestDuration = 5f;
        [SerializeField, Min(0.1f)] private float m_DebrisSinkDuration = 0.8f;
        [SerializeField, Min(0.05f)] private float m_DebrisSinkDistance = 0.42f;

        [Header("Player Damage")]
        [SerializeField] private string m_PlayerHealthAttributeId = "hp";
        [SerializeField] private GameObject m_OccupantBurnFire;
        [SerializeField, Min(0.5f)] private float m_OccupantBurnDuration = 4f;

        [Header("Charred Appearance")]
        [SerializeField] private Color m_CharredBikeColor =
            new Color(0.018f, 0.015f, 0.012f, 1f);
        [SerializeField] private Color m_CharredPlayerColor =
            new Color(0.028f, 0.022f, 0.018f, 1f);

        private bool m_IsDestroyed;
        private Character m_CapturedOccupant;
        private Character m_CapturedPassenger;
        private MaterialPropertyBlock m_PropertyBlock;

        public bool IsDestroyed => m_IsDestroyed;
        public bool HasDetachablePartsConfiguration =>
            m_FrontWheelDebris != null &&
            m_RearWheelDebris != null &&
            m_EngineDebris != null;
        public bool IsConfigured => m_Health != null && m_Driver != null &&
            m_BikeRagdoll != null && m_BikeEntry != null && m_BikeBody != null &&
            m_BikeRenderers != null && m_BikeRenderers.Length > 0 &&
            m_OccupantBurnFire != null && HasDetachablePartsConfiguration;

        public void Configure(
            FranklinBikeHealth health,
            FranklinArcadeBikeDriver driver,
            FranklinArcadeBikeRagdoll bikeRagdoll,
            BikeEntry bikeEntry,
            Rigidbody bikeBody,
            Renderer[] bikeRenderers,
            Transform frontWheelDebris,
            Transform rearWheelDebris,
            Transform engineDebris,
            GameObject occupantBurnFire,
            string playerHealthAttributeId)
        {
            m_Health = health;
            m_Driver = driver;
            m_BikeRagdoll = bikeRagdoll;
            m_BikeEntry = bikeEntry;
            m_BikeBody = bikeBody;
            m_BikeRenderers = bikeRenderers ?? Array.Empty<Renderer>();
            m_FrontWheelDebris = frontWheelDebris;
            m_RearWheelDebris = rearWheelDebris;
            m_EngineDebris = engineDebris;
            m_OccupantBurnFire = occupantBurnFire;
            m_PlayerHealthAttributeId = string.IsNullOrWhiteSpace(playerHealthAttributeId)
                ? "hp"
                : playerHealthAttributeId;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (m_Health != null)
            {
                m_Health.EventDestroyed += CaptureOccupant;
                m_Health.EventRestored += ClearCapturedOccupant;
            }
        }

        private void OnDisable()
        {
            if (m_Health != null)
            {
                m_Health.EventDestroyed -= CaptureOccupant;
                m_Health.EventRestored -= ClearCapturedOccupant;
            }
        }

        private void CaptureOccupant()
        {
            if (m_BikeEntry != null && m_BikeEntry.SeatedCharacter != null)
                m_CapturedOccupant = m_BikeEntry.SeatedCharacter;
            if (m_PassengerSeat != null && m_PassengerSeat.Passenger != null)
                m_CapturedPassenger = m_PassengerSeat.Passenger;
        }

        private void ClearCapturedOccupant()
        {
            if (!m_IsDestroyed)
            {
                m_CapturedOccupant = null;
                m_CapturedPassenger = null;
            }
        }

        public void TriggerDestruction()
        {
            if (m_IsDestroyed) return;
            m_IsDestroyed = true;
            m_Health?.SetTerminallyDestroyed();

            Character occupant = m_CapturedOccupant ??
                (m_BikeEntry != null ? m_BikeEntry.SeatedCharacter : null);
            Character passenger = m_CapturedPassenger ??
                (m_PassengerSeat != null ? m_PassengerSeat.Passenger : null);
            float fallSign = CalculateFallSign();
            if (m_BikeRagdoll != null && !m_BikeRagdoll.IsRagdoll)
                m_BikeRagdoll.ActivateRagdoll(fallSign, m_ToppleAngularVelocity);

            if (occupant != null && m_BikeEntry != null &&
                m_BikeEntry.SeatedCharacter == occupant)
            {
                m_BikeEntry.ReleaseForCrash(occupant);
            }
            if (passenger != null && m_PassengerSeat != null &&
                m_PassengerSeat.Passenger == passenger)
            {
                m_PassengerSeat.ReleaseForCrash(passenger);
            }

            // A parked fallen Bike can be kinematic. Explosion always restores a
            // real dynamic wreck before applying any force.
            m_Driver?.KeepCrashRagdollDynamic();
            if (m_BikeBody != null)
            {
                m_BikeBody.isKinematic = false;
                m_BikeBody.useGravity = true;
                m_BikeBody.constraints = RigidbodyConstraints.None;
                Vector3 side = transform.right * (fallSign * m_SideVelocity);
                m_BikeBody.AddForce(
                    Vector3.up * m_UpwardVelocity + side,
                    ForceMode.VelocityChange
                );
                m_BikeBody.AddTorque(
                    transform.forward * (-fallSign * m_ToppleAngularVelocity),
                    ForceMode.VelocityChange
                );
            }
            m_Driver?.SetDamageLocked(true);
            ApplyCharredAppearance(m_BikeRenderers, m_CharredBikeColor);
            DetachAndThrowExplosionParts(fallSign);

            if (occupant != null && occupant.Player != null)
            {
                Renderer[] playerRenderers = occupant.GetComponentsInChildren<Renderer>(true);
                ApplyCharredAppearance(playerRenderers, m_CharredPlayerColor);
                SetPlayerHealthToZero(occupant);
                FranklinMobileHud.SetControlsSuppressed(true);
                AttachBurnFire(occupant);
            }
            if (passenger != null && passenger.Player != null)
            {
                Renderer[] passengerRenderers =
                    passenger.GetComponentsInChildren<Renderer>(true);
                ApplyCharredAppearance(passengerRenderers, m_CharredPlayerColor);
                SetPlayerHealthToZero(passenger);
            }
        }

        private void DetachAndThrowExplosionParts(float fallSign)
        {
            ResolveDetachablePartReferences();
            Collider[] wreckColliders = GetComponentsInChildren<Collider>(true);
            Vector3 inheritedVelocity = m_BikeBody != null
                ? m_BikeBody.linearVelocity * 0.25f
                : Vector3.zero;

            ThrowWheel(
                m_FrontWheelDebris,
                -1f,
                inheritedVelocity,
                wreckColliders
            );
            ThrowWheel(
                m_RearWheelDebris,
                1f,
                inheritedVelocity,
                wreckColliders
            );
            ThrowEngine(
                m_EngineDebris,
                fallSign,
                inheritedVelocity,
                wreckColliders
            );
            m_BikeRagdoll?.RefreshAfterTerminalPartDetachment();
        }

        private void ThrowWheel(
            Transform wheel,
            float sideSign,
            Vector3 inheritedVelocity,
            Collider[] wreckColliders)
        {
            if (wheel == null || wheel == transform ||
                !wheel.IsChildOf(transform))
            {
                return;
            }

            StopDetachedPresentation(wheel);
            float radius = CalculateWheelRadius(wheel);
            wheel.SetParent(null, true);

            SphereCollider wheelCollider = wheel.GetComponent<SphereCollider>();
            if (wheelCollider == null)
                wheelCollider = wheel.gameObject.AddComponent<SphereCollider>();
            Vector3 scale = wheel.lossyScale;
            float maximumScale = Mathf.Max(
                0.001f,
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))
            );
            wheelCollider.center = Vector3.zero;
            wheelCollider.radius = radius / maximumScale;
            wheelCollider.isTrigger = false;
            wheelCollider.enabled = true;

            Vector3 velocity = inheritedVelocity +
                transform.right * (sideSign * m_WheelSideSpeed) +
                Vector3.up * m_WheelUpwardSpeed;
            Rigidbody body = ConfigureDetachedRigidbody(
                wheel,
                14f,
                velocity,
                UnityEngine.Random.onUnitSphere * m_WheelSpinSpeed
            );
            IgnoreWreckCollisions(wheel, wreckColliders);

            FranklinBikeDetachedPartCleanup cleanup =
                wheel.GetComponent<FranklinBikeDetachedPartCleanup>();
            if (cleanup == null)
                cleanup = wheel.gameObject.AddComponent<FranklinBikeDetachedPartCleanup>();
            cleanup.Configure(
                body,
                true,
                radius,
                m_DebrisMinimumFlightTime,
                m_DebrisMaximumFlightTime,
                m_DebrisRestDuration,
                m_DebrisSinkDuration,
                m_DebrisSinkDistance
            );
        }

        private void ThrowEngine(
            Transform engine,
            float fallSign,
            Vector3 inheritedVelocity,
            Collider[] wreckColliders)
        {
            if (engine == null || engine == transform ||
                !engine.IsChildOf(transform))
            {
                return;
            }

            StopDetachedPresentation(engine);
            engine.SetParent(null, true);
            EnsureEngineCollider(engine);

            Vector3 side = transform.right * (
                (Mathf.Abs(fallSign) > 0.01f ? fallSign : 1f) * m_EngineSideSpeed
            );
            Rigidbody body = ConfigureDetachedRigidbody(
                engine,
                34f,
                inheritedVelocity + side + Vector3.up * m_EngineUpwardSpeed,
                UnityEngine.Random.onUnitSphere * m_EngineSpinSpeed
            );
            IgnoreWreckCollisions(engine, wreckColliders);

            FranklinBikeDetachedPartCleanup cleanup =
                engine.GetComponent<FranklinBikeDetachedPartCleanup>();
            if (cleanup == null)
                cleanup = engine.gameObject.AddComponent<FranklinBikeDetachedPartCleanup>();
            cleanup.Configure(
                body,
                false,
                0.25f,
                m_DebrisMinimumFlightTime,
                m_DebrisMaximumFlightTime,
                m_DebrisRestDuration,
                m_DebrisSinkDuration,
                m_DebrisSinkDistance
            );
        }

        private static Rigidbody ConfigureDetachedRigidbody(
            Transform part,
            float mass,
            Vector3 velocity,
            Vector3 angularVelocity)
        {
            Rigidbody body = part.GetComponent<Rigidbody>();
            if (body == null) body = part.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            body.constraints = RigidbodyConstraints.None;
            body.mass = Mathf.Max(1f, mass);
            body.linearDamping = 0.16f;
            body.angularDamping = 0.1f;
            body.maxAngularVelocity = Mathf.Max(20f, angularVelocity.magnitude * 1.4f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;
            return body;
        }

        private static void EnsureEngineCollider(Transform engine)
        {
            Collider[] colliders = engine.GetComponentsInChildren<Collider>(true);
            bool hasCollider = false;
            for (int i = 0; i < colliders.Length; ++i)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.isTrigger) continue;
                collider.enabled = true;
                hasCollider = true;
            }
            if (hasCollider) return;

            Renderer[] renderers = engine.GetComponentsInChildren<Renderer>(true);
            if (!TryGetCombinedBounds(renderers, out Bounds bounds)) return;
            BoxCollider box = engine.gameObject.AddComponent<BoxCollider>();
            box.center = engine.InverseTransformPoint(bounds.center);
            Vector3 scale = engine.lossyScale;
            box.size = new Vector3(
                bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z))
            ) * 0.82f;
        }

        private static float CalculateWheelRadius(Transform wheel)
        {
            Renderer[] renderers = wheel.GetComponentsInChildren<Renderer>(true);
            if (!TryGetCombinedBounds(renderers, out Bounds bounds)) return 0.25f;
            return Mathf.Clamp(
                Mathf.Max(bounds.extents.y, bounds.extents.z),
                0.15f,
                0.6f
            );
        }

        private static bool TryGetCombinedBounds(
            Renderer[] renderers,
            out Bounds result)
        {
            result = default;
            bool initialized = false;
            if (renderers == null) return false;
            for (int i = 0; i < renderers.Length; ++i)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer || renderer is LineRenderer)
                {
                    continue;
                }
                if (!initialized)
                {
                    result = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(renderer.bounds);
                }
            }
            return initialized;
        }

        private static void IgnoreWreckCollisions(
            Transform debris,
            Collider[] wreckColliders)
        {
            if (debris == null || wreckColliders == null) return;
            Collider[] debrisColliders = debris.GetComponentsInChildren<Collider>(true);
            for (int debrisIndex = 0; debrisIndex < debrisColliders.Length; ++debrisIndex)
            {
                Collider debrisCollider = debrisColliders[debrisIndex];
                if (debrisCollider == null || !debrisCollider.enabled) continue;
                for (int wreckIndex = 0; wreckIndex < wreckColliders.Length; ++wreckIndex)
                {
                    Collider wreckCollider = wreckColliders[wreckIndex];
                    if (wreckCollider == null || wreckCollider == debrisCollider ||
                        wreckCollider.transform.IsChildOf(debris))
                    {
                        continue;
                    }
                    Physics.IgnoreCollision(debrisCollider, wreckCollider, true);
                }
            }
        }

        private static void StopDetachedPresentation(Transform part)
        {
            ParticleSystem[] particles = part.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; ++i)
            {
                particles[i].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }
            AudioSource[] audioSources = part.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; ++i)
                audioSources[i].Stop();
        }

        private float CalculateFallSign()
        {
            float roll = Vector3.Dot(transform.right, Vector3.up);
            if (Mathf.Abs(roll) > 0.08f) return roll > 0f ? -1f : 1f;
            float lateralVelocity = m_BikeBody != null
                ? Vector3.Dot(m_BikeBody.linearVelocity, transform.right)
                : 0f;
            return lateralVelocity < 0f ? -1f : 1f;
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
                if (health != null) health.Value = health.MinValue;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Bike explosion could not damage Player Traits " +
                    $"'{m_PlayerHealthAttributeId}': {exception.Message}",
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

        private void ApplyCharredAppearance(Renderer[] renderers, Color color)
        {
            if (renderers == null) return;
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

        private void ResolveReferences()
        {
            if (m_Health == null) m_Health = GetComponent<FranklinBikeHealth>();
            if (m_Driver == null) m_Driver = GetComponent<FranklinArcadeBikeDriver>();
            if (m_BikeRagdoll == null)
                m_BikeRagdoll = GetComponent<FranklinArcadeBikeRagdoll>();
            if (m_BikeEntry == null) m_BikeEntry = GetComponent<BikeEntry>();
            if (m_PassengerSeat == null)
                m_PassengerSeat = GetComponent<FranklinBikePassengerSeat>();
            if (m_BikeBody == null) m_BikeBody = GetComponent<Rigidbody>();
            ResolveDetachablePartReferences();
        }

        private void ResolveDetachablePartReferences()
        {
            if (m_FrontWheelDebris == null)
                m_FrontWheelDebris = FindNamedTransform("FrontWheelTarget");
            if (m_RearWheelDebris == null)
                m_RearWheelDebris = FindNamedTransform("RearWheelTarget");
            if (m_EngineDebris == null)
                m_EngineDebris = FindEngineDebrisTransform();
        }

        private Transform FindNamedTransform(string objectName)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; ++i)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == objectName)
                    return candidate;
            }
            return null;
        }

        private Transform FindEngineDebrisTransform()
        {
            Transform bikeBody = FindNamedTransform("BikeBody");
            if (bikeBody == null) return null;

            Transform best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < bikeBody.childCount; ++i)
            {
                Transform candidate = bikeBody.GetChild(i);
                if (candidate == null || candidate.GetComponent<Renderer>() == null ||
                    candidate.localPosition.z > -0.15f)
                {
                    continue;
                }

                string name = candidate.name;
                float score = -Mathf.Abs(candidate.localPosition.z + 0.45f) * 10f;
                if (name.IndexOf("Body_Solid", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 40f;
                else if (name.IndexOf(
                             "Texture_04_Part",
                             StringComparison.OrdinalIgnoreCase
                         ) >= 0)
                    score += 30f;
                else if (name.IndexOf(
                             "Texture_02_Part",
                             StringComparison.OrdinalIgnoreCase
                         ) >= 0)
                    score += 15f;
                else
                    continue;

                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        private void OnValidate()
        {
            m_UpwardVelocity = Mathf.Max(0f, m_UpwardVelocity);
            m_SideVelocity = Mathf.Max(0f, m_SideVelocity);
            m_ToppleAngularVelocity = Mathf.Max(0f, m_ToppleAngularVelocity);
            m_WheelSideSpeed = Mathf.Max(0f, m_WheelSideSpeed);
            m_WheelUpwardSpeed = Mathf.Max(0f, m_WheelUpwardSpeed);
            m_WheelSpinSpeed = Mathf.Max(0f, m_WheelSpinSpeed);
            m_EngineSideSpeed = Mathf.Max(0f, m_EngineSideSpeed);
            m_EngineUpwardSpeed = Mathf.Max(0f, m_EngineUpwardSpeed);
            m_EngineSpinSpeed = Mathf.Max(0f, m_EngineSpinSpeed);
            m_DebrisMinimumFlightTime = Mathf.Max(0.1f, m_DebrisMinimumFlightTime);
            m_DebrisMaximumFlightTime = Mathf.Max(
                m_DebrisMinimumFlightTime + 0.25f,
                m_DebrisMaximumFlightTime
            );
            m_DebrisRestDuration = Mathf.Max(0f, m_DebrisRestDuration);
            m_DebrisSinkDuration = Mathf.Max(0.1f, m_DebrisSinkDuration);
            m_DebrisSinkDistance = Mathf.Max(0.05f, m_DebrisSinkDistance);
            m_OccupantBurnDuration = Mathf.Max(0.5f, m_OccupantBurnDuration);
            if (string.IsNullOrWhiteSpace(m_PlayerHealthAttributeId))
                m_PlayerHealthAttributeId = "hp";
            ResolveReferences();
        }
    }
}
