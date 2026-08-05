using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Analog Stick")]
    [Category("Tactile/Analog Stick/Is Analog Stick")]

    [Description(
        "Returns true if the control type of a Tactile Control is set to Analog Stick; " +
        "otherwise, returns false"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Image(typeof(IconJoystick), ColorTheme.Type.Green)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsAnalogStick : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is {this.m_TactileControl} an Analog Stick";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.ControlType is ControlTypeAnalogStick;
        }
    }
}
