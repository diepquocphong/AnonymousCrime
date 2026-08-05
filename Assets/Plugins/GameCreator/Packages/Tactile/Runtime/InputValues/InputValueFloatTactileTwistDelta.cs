using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Twist Delta")]
    [Category("Tactile/Gesture Pad/Twist Delta")]
    [Description("Reads the twist delta of a Gesture Pad")]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile", "Rotate", "Angle", "Turn", "Roll", "Spin", "Changed")]
    
    [Serializable] [HideLabelsInEditor]
    public class InputValueFloatTactileTwistDelta : TInputValueFloat
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private ControlReference m_TactileControl = new ControlReference();
        [NonSerialized] private ControlTypeGesturePad m_ControlType;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public ControlTypeGesturePad ControlType
        {
            get
            {
                if (this.m_ControlType == null)
                {
                    TactileControl control = this.m_TactileControl.Value;
                    
                    if (control != null) 
                        this.m_ControlType = control.ControlType as ControlTypeGesturePad;
                }
                
                return this.m_ControlType;
            }
        }
        
        public override bool IsDeltaControl => false;

        // CREATE: --------------------------------------------------------------------------------

        public static InputPropertyValueFloat Create()
        {
            return new InputPropertyValueFloat(new InputValueFloatTactileTwistDelta());
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override float Read()
        {
            return this.ControlType != null ? this.ControlType.TwistDelta : 0f;
        }
    }
}