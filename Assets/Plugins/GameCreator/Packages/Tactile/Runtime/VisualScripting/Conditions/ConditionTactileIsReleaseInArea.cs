using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Released Within Area")]
    [Category("Tactile/Is Released Within Area")]

    [Description(
        "Returns true if a Tactile Control's last released finger is within its " +
        "touchable area; otherwise, returns false"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Image(typeof(IconTactile), ColorTheme.Type.Red, typeof(OverlayArrowUp))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsReleaseInArea : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is {this.m_TactileControl} Released in Area";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.TouchableArea.isLastReleaseIsInArea;
        }
    }
}
