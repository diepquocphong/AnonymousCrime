using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Push Button")]
    [Category("Tactile/Push Button/Is Push Button")]

    [Description(
        "Returns true if the control type of a Tactile Control is set to Push Button; " +
        "otherwise, returns false"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Image(typeof(IconPushButton), ColorTheme.Type.Green)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsPushButton : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is {this.m_TactileControl} a Push Button";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.ControlType is ControlTypePushButton;
        }
    }
}
