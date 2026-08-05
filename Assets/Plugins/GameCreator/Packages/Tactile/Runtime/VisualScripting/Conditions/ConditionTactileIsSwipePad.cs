using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Swipe Pad")]
    [Category("Tactile/Swipe Pad/Is Swipe Pad")]

    [Description(
        "Returns true if the control type of a Tactile Control is set to Swipe Pad; " +
        "otherwise, returns false"
    )]

    [Parameter(
        "Tactile Control",
        "The game object with Tactile Control component attached with Swipe Pad control type"
    )]
    
    [Image(typeof(IconSwipe), ColorTheme.Type.Green)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsSwipePad : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is {this.m_TactileControl} a Swipe Pad";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.ControlType is ControlTypeSwipePad;
        }
    }
}
