using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FranklinGame.AirSystem;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using GameCreator.Runtime.Stats;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Applies one radial, health-only explosion. The non-alloc overlap and root de-duplication
    /// keep a character or vehicle with many colliders from receiving damage more than once.
    /// </summary>
    [Version(1, 3, 0)]
    [Title("Damage Franklin Explosion")]
    [Category("Shooter/Damage Franklin Explosion")]
    [Description("Damages Characters, Bikes, Cars and Drones around the latest Shooter hit point")]
    [Serializable]
    public sealed class InstructionFranklinExplosionDamage : Instruction
    {
        private const int MAX_OVERLAPS = 96;
        private static readonly Collider[] OVERLAPS = new Collider[MAX_OVERLAPS];
        private static readonly HashSet<int> DAMAGED_ROOTS = new();

        [SerializeField, Min(0.1f)] private float m_Radius = 4f;
        [SerializeField, Min(0f)] private float m_CharacterDamage = 100f;
        [SerializeField, Min(0f)] private float m_VehicleDamage = 18f;
        [Tooltip("A direct RPG impact immediately destroys a Bike or Car. " +
                 "Nearby vehicles still use the radial vehicle damage and falloff.")]
        [SerializeField] private bool m_DestroyGroundVehiclesOnDirectHit;
        [SerializeField, Min(0f)] private float m_DirectVehicleForwardVelocity = 2.8f;
        [SerializeField, Min(0f)] private float m_DirectVehicleUpwardVelocity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float m_EdgeMultiplier = 0.35f;
        [SerializeField, Range(0f, 1f)] private float m_ArmorAbsorption = 0.25f;
        [SerializeField, Range(0f, 6f)] private float m_RagdollVelocity = 3.2f;
        [SerializeField, Range(0f, 1f)] private float m_UpwardModifier = 0.35f;
        [SerializeField, Min(0.25f)] private float m_RagdollDuration = 1.45f;
        [SerializeField] private AudioClip m_ExplosionClip;
        [SerializeField] private AudioClip m_DistantExplosionClip;
        [SerializeField, Min(2f)] private float m_NearAudioDistance = 42f;
        [SerializeField, Min(2f)] private float m_DistantAudioDistance = 260f;
        [SerializeField] private string m_HealthAttributeId = "hp";

        public float Radius => Mathf.Max(0.1f, this.m_Radius);
        public float CharacterDamage => Mathf.Max(0f, this.m_CharacterDamage);
        public float VehicleDamage => Mathf.Max(0f, this.m_VehicleDamage);
        public bool DestroyGroundVehiclesOnDirectHit =>
            this.m_DestroyGroundVehiclesOnDirectHit;
        public float DirectVehicleForwardVelocity =>
            Mathf.Max(0f, this.m_DirectVehicleForwardVelocity);
        public float DirectVehicleUpwardVelocity =>
            Mathf.Max(0f, this.m_DirectVehicleUpwardVelocity);
        public float EdgeMultiplier => Mathf.Clamp01(this.m_EdgeMultiplier);
        public float ArmorAbsorption => Mathf.Clamp01(this.m_ArmorAbsorption);
        public float RagdollVelocity => Mathf.Clamp(this.m_RagdollVelocity, 0f, 6f);
        public float RagdollDuration => Mathf.Max(0.25f, this.m_RagdollDuration);
        public AudioClip ExplosionClip => this.m_ExplosionClip;
        public AudioClip DistantExplosionClip => this.m_DistantExplosionClip;
        public float NearAudioDistance => Mathf.Max(2f, this.m_NearAudioDistance);
        public float DistantAudioDistance => Mathf.Max(2f, this.m_DistantAudioDistance);

        public override string Title =>
            $"Explosion {this.CharacterDamage:0.#} / {this.VehicleDamage:0.#} ({this.Radius:0.#}m)";

        public InstructionFranklinExplosionDamage()
        { }

        public InstructionFranklinExplosionDamage(
            float radius,
            float characterDamage,
            float vehicleDamage,
            float edgeMultiplier,
            float armorAbsorption,
            float ragdollVelocity,
            float upwardModifier,
            float ragdollDuration,
            AudioClip explosionClip,
            AudioClip distantExplosionClip = null,
            float nearAudioDistance = 42f,
            float distantAudioDistance = 260f,
            bool destroyGroundVehiclesOnDirectHit = false,
            float directVehicleForwardVelocity = 2.8f,
            float directVehicleUpwardVelocity = 0.35f)
        {
            this.m_Radius = Mathf.Max(0.1f, radius);
            this.m_CharacterDamage = Mathf.Max(0f, characterDamage);
            this.m_VehicleDamage = Mathf.Max(0f, vehicleDamage);
            this.m_EdgeMultiplier = Mathf.Clamp01(edgeMultiplier);
            this.m_ArmorAbsorption = Mathf.Clamp01(armorAbsorption);
            this.m_RagdollVelocity = Mathf.Clamp(ragdollVelocity, 0f, 6f);
            this.m_UpwardModifier = Mathf.Clamp01(upwardModifier);
            this.m_RagdollDuration = Mathf.Max(0.25f, ragdollDuration);
            this.m_ExplosionClip = explosionClip;
            this.m_DistantExplosionClip = distantExplosionClip;
            this.m_NearAudioDistance = Mathf.Max(2f, nearAudioDistance);
            this.m_DistantAudioDistance = Mathf.Max(2f, distantAudioDistance);
            this.m_DestroyGroundVehiclesOnDirectHit =
                destroyGroundVehiclesOnDirectHit;
            this.m_DirectVehicleForwardVelocity =
                Mathf.Max(0f, directVehicleForwardVelocity);
            this.m_DirectVehicleUpwardVelocity =
                Mathf.Max(0f, directVehicleUpwardVelocity);
        }

        protected override Task Run(Args args)
        {
            Vector3 center = ShotData.LastHitPosition;
            if (center == Vector3.zero && args.Target != null)
                center = args.Target.transform.position;

            // GC2 passes the exact collider GameObject as Args.Target for this hit.
            // Prefer it over the shared last-hit cache so concurrent projectiles
            // cannot redirect the direct-vehicle one-hit rule to another target.
            GameObject impactObject = args.Target != null
                ? args.Target
                : ShotData.LastHitObject;
            this.AddExplosionDecal(impactObject, center);

            FranklinThrowableAudioPool.Play(
                center,
                this.m_ExplosionClip,
                1f,
                1f,
                this.NearAudioDistance,
                Mathf.Clamp(this.NearAudioDistance * 0.08f, 2f, 12f)
            );
            if (this.m_DistantExplosionClip != null &&
                this.m_DistantExplosionClip != this.m_ExplosionClip)
            {
                FranklinThrowableAudioPool.Play(
                    center,
                    this.m_DistantExplosionClip,
                    0.72f,
                    1f,
                    this.DistantAudioDistance,
                    Mathf.Clamp(this.DistantAudioDistance * 0.1f, 16f, 36f)
                );
            }

            DAMAGED_ROOTS.Clear();
            FranklinBikeHealth directBike = null;
            SimcadeCarHealth directCar = null;
            if (this.DestroyGroundVehiclesOnDirectHit && impactObject != null &&
                ResolveCharacter(impactObject) == null)
            {
                directBike = ResolveReceiver<FranklinBikeHealth>(impactObject);
                if (directBike != null)
                {
                    directBike.SetNormalizedHealth(0f);
                    DAMAGED_ROOTS.Add(directBike.GetInstanceID());
                }
                else
                {
                    directCar = ResolveReceiver<SimcadeCarHealth>(impactObject);
                    if (directCar != null)
                    {
                        directCar.SetNormalizedHealth(0f);
                        DAMAGED_ROOTS.Add(directCar.GetInstanceID());
                    }
                }
            }

            int count = Physics.OverlapSphereNonAlloc(
                center,
                this.Radius,
                OVERLAPS,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < count; ++i)
            {
                Collider hit = OVERLAPS[i];
                OVERLAPS[i] = null;
                if (hit == null) continue;

                Character character = ResolveCharacter(hit);
                if (character != null)
                {
                    if (!DAMAGED_ROOTS.Add(character.GetInstanceID()))
                        continue;

                    float scale = this.GetFalloff(center, hit.ClosestPoint(center));
                    bool alive = this.ApplyCharacterDamage(
                        character,
                        this.CharacterDamage * scale
                    );
                    FranklinExplosionRagdollImpulse.Apply(
                        character,
                        center,
                        this.RagdollVelocity * scale,
                        this.m_UpwardModifier,
                        this.RagdollDuration,
                        alive
                    );
                    continue;
                }

                FranklinBikeHealth bike = ResolveReceiver<FranklinBikeHealth>(hit);
                if (bike != null)
                {
                    if (!DAMAGED_ROOTS.Add(bike.GetInstanceID())) continue;

                    bike.ApplyDamage(
                        this.VehicleDamage * this.GetFalloff(center, hit.ClosestPoint(center))
                    );
                    continue;
                }

                SimcadeCarHealth car = ResolveReceiver<SimcadeCarHealth>(hit);
                if (car != null)
                {
                    if (!DAMAGED_ROOTS.Add(car.GetInstanceID())) continue;

                    car.ApplyDamage(
                        this.VehicleDamage * this.GetFalloff(center, hit.ClosestPoint(center))
                    );
                    continue;
                }

                DroneHealth drone = ResolveReceiver<DroneHealth>(hit);
                if (drone == null || !DAMAGED_ROOTS.Add(drone.GetInstanceID()))
                    continue;

                drone.ApplyDamage(
                    this.VehicleDamage * this.GetFalloff(center, hit.ClosestPoint(center))
                );
            }

            Vector3 shotDirection = ShooterWeapon.LastShotData.ShootDirection;
            if (shotDirection.sqrMagnitude <= 0.000001f)
                shotDirection = ShotData.LastShooterDirection;
            TriggerImmediateGroundVehicleExplosion(
                directBike,
                directCar,
                shotDirection,
                ShotData.LastHitNormal,
                this.DirectVehicleForwardVelocity,
                this.DirectVehicleUpwardVelocity
            );
            DAMAGED_ROOTS.Clear();
            return DefaultResult;
        }

        private static void TriggerImmediateGroundVehicleExplosion(
            FranklinBikeHealth bike,
            SimcadeCarHealth car,
            Vector3 shotDirection,
            Vector3 hitNormal,
            float forwardVelocity,
            float upwardVelocity)
        {
            if (bike != null)
            {
                Rigidbody body = ResolveVehicleBody(bike);
                FranklinBikeDestruction destruction =
                    bike.GetComponent<FranklinBikeDestruction>();
                if (destruction == null)
                {
                    destruction = bike.GetComponentInChildren<FranklinBikeDestruction>(
                        true
                    );
                }
                bool wasDestroyed = destruction != null && destruction.IsDestroyed;

                FranklinBikeDamageEffects effects =
                    bike.GetComponent<FranklinBikeDamageEffects>();
                if (effects == null)
                {
                    effects = bike.GetComponentInChildren<FranklinBikeDamageEffects>(
                        true
                    );
                }
                // The RPG impact already owns the pooled explosion burst/audio.
                // The vehicle pipeline only transitions to fire + terminal wreck.
                effects?.TriggerImmediateExplosion(false);

                destruction?.TriggerDestruction();
                if (!wasDestroyed && destruction != null && destruction.IsDestroyed)
                {
                    ApplyShooterVehicleImpulse(
                        body,
                        shotDirection,
                        hitNormal,
                        forwardVelocity,
                        upwardVelocity
                    );
                }
                return;
            }

            if (car == null) return;
            SimcadeCarDestruction carDestruction =
                car.GetComponent<SimcadeCarDestruction>();
            if (carDestruction == null)
            {
                carDestruction = car.GetComponentInChildren<SimcadeCarDestruction>(true);
            }
            bool carWasDestroyed = carDestruction != null && carDestruction.IsDestroyed;

            SimcadeCarDamageEffects carEffects =
                car.GetComponent<SimcadeCarDamageEffects>();
            if (carEffects == null)
            {
                carEffects = car.GetComponentInChildren<SimcadeCarDamageEffects>(true);
            }
            carEffects?.TriggerImmediateExplosion(false);

            carDestruction?.TriggerDestruction();
            if (!carWasDestroyed && carDestruction != null &&
                carDestruction.IsDestroyed)
            {
                carDestruction.QueueDirectShooterImpulse(
                    shotDirection,
                    hitNormal,
                    forwardVelocity,
                    upwardVelocity
                );
            }
        }

        private static Rigidbody ResolveVehicleBody(Component vehicle)
        {
            if (vehicle == null) return null;
            Rigidbody body = vehicle.GetComponent<Rigidbody>();
            return body != null ? body : vehicle.GetComponentInParent<Rigidbody>();
        }

        private static void ApplyShooterVehicleImpulse(
            Rigidbody body,
            Vector3 shotDirection,
            Vector3 hitNormal,
            float forwardVelocity,
            float upwardVelocity)
        {
            if (body == null || body.isKinematic) return;

            Vector3 direction = Vector3.ProjectOnPlane(shotDirection, Vector3.up);
            if (direction.sqrMagnitude <= 0.000001f)
                direction = Vector3.ProjectOnPlane(-hitNormal, Vector3.up);
            if (direction.sqrMagnitude <= 0.000001f)
                direction = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up);
            if (direction.sqrMagnitude <= 0.000001f) return;

            // Match GC2 Shooter's Impulse flow. Multiplying the authored velocity
            // change by mass keeps the 200 kg Bike and 1000 kg Car response equal.
            Vector3 velocityChange =
                direction.normalized * Mathf.Max(0f, forwardVelocity) +
                Vector3.up * Mathf.Max(0f, upwardVelocity);
            if (velocityChange.sqrMagnitude <= 0.000001f) return;

            body.WakeUp();
            body.AddForce(
                velocityChange * Mathf.Max(0.01f, body.mass),
                ForceMode.Impulse
            );
        }

        private void AddExplosionDecal(GameObject impactObject, Vector3 center)
        {
            if (impactObject == null) return;
            // Characters use blood/ragdoll feedback. Never classify a capsule or body
            // collider as a wall and attach a large scorch quad to it.
            if (ResolveCharacter(impactObject) != null) return;

            Collider collider = impactObject.GetComponent<Collider>();
            if (collider == null) collider = impactObject.GetComponentInParent<Collider>();
            bool followsVehicle = collider != null &&
                (ResolveReceiver<FranklinBikeHealth>(collider) != null ||
                 ResolveReceiver<SimcadeCarHealth>(collider) != null ||
                 ResolveReceiver<DroneHealth>(collider) != null);
            FranklinVehicleBulletDecalSystem.AddExplosionHit(
                impactObject,
                center,
                ShotData.LastShooterDirection,
                ShotData.LastHitNormal,
                followsVehicle,
                followsVehicle ? 1.6f : 2.4f
            );
        }

        private static T ResolveReceiver<T>(Collider collider) where T : Component
        {
            if (collider == null) return null;

            T receiver = collider.GetComponentInParent<T>();
            if (receiver != null) return receiver;

            Rigidbody body = collider.attachedRigidbody;
            return body != null ? body.GetComponentInParent<T>() : null;
        }

        private static T ResolveReceiver<T>(GameObject source) where T : Component
        {
            if (source == null) return null;

            T receiver = source.GetComponentInParent<T>();
            if (receiver != null) return receiver;

            Collider collider = source.GetComponent<Collider>();
            if (collider != null)
            {
                receiver = ResolveReceiver<T>(collider);
                if (receiver != null) return receiver;
            }

            return source.GetComponentInChildren<T>(true);
        }

        private static Character ResolveCharacter(Collider collider)
        {
            if (collider == null) return null;

            Character character = collider.GetComponentInParent<Character>();
            if (character != null) return character;

            FranklinRagdollCharacterProxy proxy =
                collider.GetComponentInParent<FranklinRagdollCharacterProxy>();
            return proxy != null ? proxy.Character : null;
        }

        private static Character ResolveCharacter(GameObject source)
        {
            if (source == null) return null;

            Character character = source.GetComponentInParent<Character>();
            if (character != null) return character;

            FranklinRagdollCharacterProxy proxy =
                source.GetComponentInParent<FranklinRagdollCharacterProxy>();
            return proxy != null ? proxy.Character : null;
        }

        private float GetFalloff(Vector3 center, Vector3 point)
        {
            float ratio = Mathf.Clamp01(Vector3.Distance(center, point) / this.Radius);
            return Mathf.Lerp(1f, this.EdgeMultiplier, ratio);
        }

        private bool ApplyCharacterDamage(Character victim, float damage)
        {
            if (damage <= 0f) return true;

            Traits traits = victim.GetComponent<Traits>() ??
                            victim.GetComponentInChildren<Traits>(true);
            if (traits == null) return true;

            RuntimeAttributeData health;
            try
            {
                health = traits.RuntimeAttributes.Get(this.m_HealthAttributeId);
            }
            catch
            {
                return true;
            }

            if (health == null || health.Value <= health.MinValue) return false;
            FranklinArmorDamageResult armor = FranklinArmorAPI.AbsorbDamage(
                victim.gameObject,
                damage,
                this.ArmorAbsorption
            );
            health.Value -= Mathf.Max(0f, armor.HealthDamage);
            return health.Value > health.MinValue;
        }
    }
}
