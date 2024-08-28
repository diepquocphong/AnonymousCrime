using GameCreator.Runtime.Common;

namespace GameCreator.Tools.Runtime.Footsteps
{
    public class FootstepSettings : AssetRepository<FootstepRepository>
    {
        public override IIcon Icon => new IconFootprint(ColorTheme.Type.TextLight);
        public override string Name => "Footsteps";

        public override int Priority => 99;
    }
}