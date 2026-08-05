using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Pinch Delta")]
    [Category("Tactile/Gesture Pad/Pinch Delta")]
    [Description("Reads the pinch delta of a Gesture Pad")]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile", "Zoom", "Squeeze", "Focus", "Changed")]
    
    [Serializable] [HideLabelsInEditor]
    public class InputValueFloatTactilePinchDelta : TInputValueFloat
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
            return new InputPropertyValueFloat(new InputValueFloatTactilePinchDelta());
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override float Read()
        {
            return this.ControlType != null ? this.ControlType.PinchDelta : 0f;
        }
    }
}