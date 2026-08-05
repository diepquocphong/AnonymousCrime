using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Pan Delta")]
    [Category("Tactile/Gesture Pad/Pan Delta")]
    [Description("Reads the pan delta of a Gesture Pad")]

    [Image(typeof(IconGesture))]
    [Keywords("Tactile", "Move", "Look", "Changed")]
    
    [Serializable] [HideLabelsInEditor]
    public class InputValueVector2TactilePanDelta : TInputValueVector2
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
            return new InputPropertyValueVector2(new InputValueVector2TactilePanDelta());
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override Vector2 Read()
        {
            return this.ControlType != null ? this.ControlType.PanDelta : Vector2.zero;
        }
    }
}