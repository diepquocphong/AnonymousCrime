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
    public class InputValueVector2TactileTwistDelta : TInputValueVector2
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

        // CREATE: --------------------------------------------------------------------------------

        public static InputPropertyValueVector2 Create()
        {
            return new InputPropertyValueVector2(new InputValueVector2TactileTwistDelta());
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override Vector2 Read()
        {
            if (this.ControlType == null) return Vector2.zero;

            float twist = this.ControlType.TwistDelta;
            return new Vector2(twist, twist);
        }
    }
}