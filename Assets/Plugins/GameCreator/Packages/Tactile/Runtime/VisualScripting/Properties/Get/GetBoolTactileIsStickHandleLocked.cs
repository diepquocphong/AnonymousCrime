using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Is Handle Locked")]
    [Category("Tactile/Analog Stick/Is Handle Locked")]
    
    [Description(
        "Gets true if an Analog Stick's handle is locked; Otherwise, false"
    )]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetBoolTactileIsStickHandleLocked : PropertyTypeGetBool
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"is {this.m_TactileControl} Locked";

        // GETTERS: -------------------------------------------------------------------------------

        public override bool Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
                return stick.IsLocked;

            return false;
        }

        public override bool Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
                return stick.IsLocked;

            return false;
        }

    }
}