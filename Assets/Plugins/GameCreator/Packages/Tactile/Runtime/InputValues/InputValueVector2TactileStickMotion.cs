using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick Motion")]
    [Category("Tactile/Analog Stick/Stick Motion")]
    [Description("Reads the motion value of an Analog Stick")]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile", "Move", "Joystick", "Primary", "Analog", "Touchstick")]
    
    [Serializable] [HideLabelsInEditor]
    public class InputValueVector2TactileStickMotion : TInputValueVector2
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private ControlReference m_TactileControl = new ControlReference();
        [NonSerialized] private ControlTypeAnalogStick m_ControlType;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public ControlTypeAnalogStick ControlType
        {
            get
            {
                if (this.m_ControlType == null)
                {
                    TactileControl control = this.m_TactileControl.Value;

                    if (control != null) 
                        this.m_ControlType = control.ControlType as ControlTypeAnalogStick;
                }

                return this.m_ControlType;
            }
        }

        // CREATE: --------------------------------------------------------------------------------

        public static InputPropertyValueVector2 Create()
        {
            return new InputPropertyValueVector2(new InputValueVector2TactileStickMotion());
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override Vector2 Read()
        {
            return this.ControlType != null ? this.ControlType.StickMotion : Vector2.zero;
        }
    }
}