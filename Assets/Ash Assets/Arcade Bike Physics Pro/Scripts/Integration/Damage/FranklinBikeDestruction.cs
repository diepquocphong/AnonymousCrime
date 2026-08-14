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
    /// dynamic fallen wreck; wheels stay attached and the Main Camera is untouched.
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
        public bool IsConfigured => m_Health != null && m_Driver != null &&
            m_BikeRagdoll != null && m_BikeEntry != null && m_BikeBody != null &&
            m_BikeRenderers != null && m_BikeRenderers.Length > 0 &&
            m_OccupantBurnFire != null;

        public void Configure(
            FranklinBikeHealth health,
            FranklinArcadeBikeDriver driver,
            FranklinArcadeBikeRagdoll bikeRagdoll,
            BikeEntry bikeEntry,
            Rigidbody bikeBody,
            Renderer[] bikeRenderers,
            GameObject occupantBurnFire,
            string playerHealthAttributeId)
        {
            m_Health = health;
            m_Driver = driver;
            m_BikeRagdoll = bikeRagdoll;
            m_BikeEntry = bikeEntry;
            m_BikeBody = bikeBody;
            m_BikeRenderers = bikeRenderers ?? Array.Empty<Renderer>();
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
        }

        private void OnValidate()
        {
            m_UpwardVelocity = Mathf.Max(0f, m_UpwardVelocity);
            m_SideVelocity = Mathf.Max(0f, m_SideVelocity);
            m_ToppleAngularVelocity = Mathf.Max(0f, m_ToppleAngularVelocity);
            m_OccupantBurnDuration = Mathf.Max(0.5f, m_OccupantBurnDuration);
            if (string.IsNullOrWhiteSpace(m_PlayerHealthAttributeId))
                m_PlayerHealthAttributeId = "hp";
            ResolveReferences();
        }
    }
}
