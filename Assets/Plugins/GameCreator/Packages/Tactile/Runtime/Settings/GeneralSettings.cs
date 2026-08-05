using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    public class GeneralSettings : AssetRepository<GeneralRepository>
    {
        public override IIcon Icon => new IconTactile(ColorTheme.Type.TextLight);
        public override string Name => "Tactile";

        public override int Priority => 60;
    }
}