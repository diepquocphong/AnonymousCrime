using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Point Within Area")]
    [Category("Tactile/Is Point Within Area")]

    [Description(
        "Returns true if the given screen point is within Tactile Control's touchable area; " +
        "otherwise, returns false"
    )]

    [Parameter("Point", "The screen point coordinate to check")]
    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Image(typeof(IconTactile), ColorTheme.Type.Yellow, typeof(OverlayDot))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsPointWithinArea : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField]
        private PropertyGetPosition m_Point = GetPositionVector2.Create();

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => 
            $"Is {this.m_Point} Within Area of {this.m_TactileControl}";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.TouchableArea.ContainsPoint(this.m_Point.Get(args));
        }
    }
}
