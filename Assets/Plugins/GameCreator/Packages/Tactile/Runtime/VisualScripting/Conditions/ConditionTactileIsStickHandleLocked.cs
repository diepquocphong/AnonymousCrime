using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Handle Locked")]
    [Category("Tactile/Analog Stick/Is Handle Locked")]

    [Description(
        "Returns true if a Analog Stick's handle is locked; otherwise, returns false"
    )]

    [Parameter(
        "Tactile Control",
        "The game object with Tactile Control component attached with Stick control type"
    )]
    
    [Image(typeof(IconJoystick), ColorTheme.Type.Red)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsStickHandleLocked : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is {this.m_TactileControl} Locked";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
                return stick.IsLocked;

            return false;
        }
    }
}
