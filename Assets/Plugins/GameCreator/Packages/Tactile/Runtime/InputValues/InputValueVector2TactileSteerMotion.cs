using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Steer Motion")]
    [Category("Tactile/Steering Wheel/Steer Motion")]
    [Description("Reads the motion value of a Steering Wheel")]

    [Image(typeof(IconSteerWheel))]
    [Keywords("Tactile", "Car", "Wheel", "Rotate", "Angle", "Turn", "Roll", "Spin")]
    
    [Serializable] [HideLabelsInEditor]
    public class InputValueVector2TactileSteerMotion : TInputValueVector2
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

        // CREATE: --------------------------------------------------------------------------------

        public static InputPropertyValueVector2 Create()
        {
            return new InputPropertyValueVector2(new InputValueVector2TactileSteerMotion());
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override Vector2 Read()
        {
            if (this.ControlType == null) return Vector2.zero;

            float steer = this.ControlType.SteerMotion;
            return new Vector2(steer, 0f);
        }
    }
}