using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Pinch Delta")]
    [Category("Tactile/Gesture Pad/Pinch Delta")]
    [Description("Reads the pinch delta of a Gesture Pad")]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile", "Zoom", "Squeeze", "Focus", "Enlarge")]
    
    [Serializable] [HideLabelsInEditor]
    public class InputValueVector2TactilePinchScale : TInputValueVector2
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
            return new InputPropertyValueVector2(new InputValueVector2TactilePinchScale());
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override Vector2 Read()
        {
            if (this.ControlType == null) return Vector2.zero;

            float pinch = this.ControlType.PinchDelta;
            return new Vector2(pinch, pinch);
        }
    }
}