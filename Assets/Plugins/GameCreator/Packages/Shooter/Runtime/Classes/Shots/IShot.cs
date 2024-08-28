using GameCreator.Runtime.Common;

namespace GameCreator.Runtime.Shooter
{
    public interface IShot
    {
        bool Run(
            Args args,
            ShooterWeapon weapon,
            MaterialSoundsAsset impact,
            float chargeRatio,
            float pullTime
        );
    }
}