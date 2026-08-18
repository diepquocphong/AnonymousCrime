using System;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.Shooter
{
    [Serializable]
    public abstract class TShotProjectile : TShot
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] protected PropertyGetGameObject m_Prefab = GetGameObjectInstance.Create();
        [SerializeField] private PropertyGetDecimal m_Delay = GetDecimalConstantZero.Create; 
        [SerializeField] private bool m_AimAtSightPoint;
        
        // RUN METHOD: ----------------------------------------------------------------------------
        
        public override bool Run(Args args,
            ShooterWeapon weapon,
            MaterialSoundsAsset impactSound,
            PropertyGetInstantiate impactEffect,
            float chargeRatio,
            float pullTime)
        {
            Character character = args.Self.Get<Character>();
            
            WeaponData weaponData = character.Combat.RequestStance<ShooterStance>().Get(weapon);
            weaponData.CombatArgs.ChangeTarget(null);
            
            SightItem sight = weapon.Sights.Get(weaponData.SightId);
            if (sight?.Sight == null) return false;
            
            MuzzleData muzzle = sight.Sight.GetMuzzle(
                weaponData.WeaponArgs,
                weapon
            );

            int projectilesUsed = weapon.Fire.ProjectilesPerShot(weaponData.WeaponArgs);
            int cartridgesUsed = weapon.Fire.CartridgesPerShot(weaponData.WeaponArgs);
            GameObject prop = character.Combat.GetProp(weapon);

            for (int i = 0; i < projectilesUsed; ++i)
            {
                Vector3 spreadDirection = this.GetShotDirection(
                    sight.Sight,
                    muzzle,
                    weaponData.WeaponArgs,
                    weapon
                );
                
                ShotData shotData = new ShotData(
                    character,
                    weapon, weaponData.SightId,
                    prop,
                    muzzle.Position,
                    spreadDirection,
                    impactSound,
                    impactEffect,
                    i == 0 ? cartridgesUsed : 0,
                    chargeRatio,
                    (float) this.m_Delay.Get(weaponData.WeaponArgs),
                    pullTime
                );

                if (weapon.CanShoot(shotData, weaponData.WeaponArgs) == false) return false;
            
                GameObject prefab = this.m_Prefab.Get(weaponData.WeaponArgs);
                GameObject projectile = PoolManager.Instance.Pick(
                    prefab,
                    muzzle.Position,
                    Quaternion.LookRotation(spreadDirection),
                    1
                );
            
                shotData.UpdateProjectile(projectile);
                weapon.OnShoot(shotData, weaponData.WeaponArgs);
            
                this.OnRun(weaponData, shotData);
            }
            
            return true;
        }

        // PRIVATE METHODS: ----------------------------------------------------------------------

        private Vector3 GetShotDirection(
            Sight sight,
            MuzzleData muzzle,
            Args args,
            ShooterWeapon weapon)
        {
            if (!this.m_AimAtSightPoint)
                return sight.GetSpreadDirection(args, weapon);

            // Large projectiles can visibly lag a fast camera orbit when they inherit the
            // animated muzzle rotation. This opt-in mode resolves the native GC2 Aim point
            // again at the exact release frame and converges from the real muzzle. The
            // projectile still starts at the muzzle, so nearby physical cover remains valid.
            Vector3 direction = sight.Aim.GetPoint(args) - muzzle.Position;
            return direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : muzzle.Direction.normalized;
        }
        
        // ABSTRACT METHODS: ----------------------------------------------------------------------

        protected abstract void OnRun(WeaponData weaponData, ShotData shotData);
    }
}
