using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Handle Damping")]
    [Category("Tactile/Analog Stick/Set Handle Damping")]
    [Description("Sets the damping of an Analog Stick's handle")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Analog Stick"
    )]

    [Parameter("Dragging", "The smoothness applied to the handle's movement for dragging")]
    [Parameter("Recenter", "The smoothness applied to the handle's movement for recenter")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Blue)]
    [Keywords("Tactile", "Lerp", "Interpolate", "Smoothness")]

    [Serializable]
    public class InstructionTactileSetStickHandleDamping : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_Dragging = GetDecimalDecimal.Create(0.125);

        [SerializeField] 
        private PropertyGetDecimal m_Recenter = GetDecimalDecimal.Create(0.925);
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => string.Format(
            "Set {0} Handle Damping = ({1:N3}, {2:N3})",
            this.m_TactileControl,
            this.m_Dragging,
            this.m_Recenter
        );

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeAnalogStick stick) 
            {
                stick.HandleDamping = new Vector2(
                    (float) this.m_Dragging.Get(args),
                    (float) this.m_Recenter.Get(args)
                );
            }

            return DefaultResult;
        }
    }
}