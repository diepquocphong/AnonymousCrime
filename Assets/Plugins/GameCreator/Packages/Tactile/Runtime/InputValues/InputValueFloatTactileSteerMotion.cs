using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Steer Motion")]
    [Category("Tactile/Steering Wheel/Steer Motion")]
    [Description("Reads the Steering Wheel's value from Tactile Control")]

    [Image(typeof(IconWheel))]
    [Keywords("Tactile", "Car", "Wheel", "Rotate", "Angle", "Turn", "Roll", "Spin")]
    
    [Serializable] [HideLabelsInEditor]
    public class InputValueFloatTactileSteerMotion : TInputValueFloat
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private ControlReference m_TactileControl = new ControlReference();
        [NonSerialized] private ControlTypeSteeringWheel m_ControlType;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public ControlTypeSteeringWheel ControlType
        {
            get
            {
                if (this.m_ControlType == null)
                {
                    TactileControl control = this.m_TactileControl.Value;
                    
                    if (control != null) 
                        this.m_ControlType = control.ControlType as ControlTypeSteeringWheel;
                }

                return this.m_ControlType;
            }
        }
        
        public override bool IsDeltaControl => false;

        // CREATE: --------------------------------------------------------------------------------

        public static InputPropertyValueFloat Create()
        {
            return new InputPropertyValueFloat(new InputValueFloatTactileSteerMotion());
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override float Read()
        {
            return this.ControlType != null ? this.ControlType.SteerMotion : 0f;
        }
    }
}