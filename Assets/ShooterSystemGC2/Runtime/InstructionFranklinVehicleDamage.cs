using System;
using System.Threading.Tasks;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Shooter;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Applies Shooter damage to Franklin Bike/Car health and routes non-vehicle static
    /// ground/wall hits to the mobile bullet-decal batch.
    /// </summary>
    [Version(1, 1, 0)]
    [Title("Damage Franklin Vehicle")]
    [Category("Shooter/Vehicles/Damage Franklin Vehicle")]
    [Description("Damages hit Bikes/Cars and creates bullet marks on static ground/walls")]
    [Parameter("Damage", "Health removed for this projectile or pellet")]
    [Keywords("Bike", "Car", "Vehicle", "Health", "Damage", "Shooter")]
    [Serializable]
    public sealed class InstructionFranklinVehicleDamage : Instruction
    {
        [SerializeField, Min(0f)] private float m_Damage = 8f;
        [SerializeField] private bool m_IgnoreVehicleOccupiedByShooter = true;

        public override string Title => $"Damage Franklin Vehicle {this.m_Damage:0.##}";

        public float Damage => Mathf.Max(0f, this.m_Damage);

        public InstructionFranklinVehicleDamage()
        { }

        public InstructionFranklinVehicleDamage(float damage)
        {
            this.m_Damage = Mathf.Max(0f, damage);
        }

        protected override Task Run(Args args)
        {
            GameObject target = args.Target;
            if (target == null || this.m_Damage <= 0f) return DefaultResult;

            // A seated Character is parented below the vehicle. Hitting that
            // Character must remain a character hit, not leak into vehicle HP.
            if (target.GetComponentInParent<Character>() != null) return DefaultResult;

            FranklinBikeHealth bikeHealth = target.GetComponentInParent<FranklinBikeHealth>();
            if (bikeHealth != null)
            {
                if (this.IsShootersCurrentVehicle(args.Self, bikeHealth))
                    return DefaultResult;

                bikeHealth.ApplyDamage(this.m_Damage);
                this.AddBulletMark(target);
                return DefaultResult;
            }

            SimcadeCarHealth carHealth = target.GetComponentInParent<SimcadeCarHealth>();
            if (carHealth != null)
            {
                if (!this.IsShootersCurrentVehicle(args.Self, carHealth))
                {
                    carHealth.ApplyDamage(this.m_Damage);
                    this.AddBulletMark(target);
                }
                return DefaultResult;
            }

            this.AddSurfaceBulletMark(target);
            return DefaultResult;
        }

        private void AddBulletMark(GameObject hitObject)
        {
            // Damage is intentionally decoupled from physics. The visual system only stores
            // a transform-relative point and never applies force to the hit Rigidbody.
            // Vehicle damage was globally reduced for balance. Use its own visual range so
            // that balancing health values does not make every bullet mark unreadably small.
            float size = Mathf.Lerp(
                0.072f,
                0.145f,
                Mathf.InverseLerp(1.2f, 14f, this.Damage)
            );
            FranklinVehicleBulletDecalSystem.AddHit(
                hitObject,
                ShotData.LastHitPosition,
                ShotData.LastShooterDirection,
                size
            );
        }

        private void AddSurfaceBulletMark(GameObject hitObject)
        {
            float size = Mathf.Lerp(
                0.082f,
                0.15f,
                Mathf.InverseLerp(1.2f, 14f, this.Damage)
            );
            FranklinVehicleBulletDecalSystem.AddSurfaceHit(
                hitObject,
                ShotData.LastHitPosition,
                ShotData.LastShooterDirection,
                size
            );
        }

        private bool IsShootersCurrentVehicle(GameObject shooter, Component hitVehicle)
        {
            if (!this.m_IgnoreVehicleOccupiedByShooter || shooter == null ||
                hitVehicle == null)
            {
                return false;
            }

            Component sourceVehicle = hitVehicle is FranklinBikeHealth
                ? shooter.GetComponentInParent<FranklinBikeHealth>()
                : shooter.GetComponentInParent<SimcadeCarHealth>();
            return sourceVehicle == hitVehicle;
        }
    }
}
