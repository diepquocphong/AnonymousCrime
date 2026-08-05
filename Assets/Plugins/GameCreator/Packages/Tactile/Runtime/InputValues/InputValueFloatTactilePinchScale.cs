using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Pinch Scale")]
    [Category("Tactile/Gesture Pad/Pinch Scale")]
    [Description("Reads the pinch scale of a Gesture Pad")]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile", "Zoom", "Squeeze", "Focus", "Enlarged")]
    
    [Serializable] [HideLabelsInEditor]
    public class InputValueFloatTactilePinchScale : TInputValueFloat
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
            return new InputPropertyValueFloat(new InputValueFloatTactilePinchScale());
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override float Read()
        {
            return this.ControlType != null ? this.ControlType.PinchScale : 1f;
        }
    }
}