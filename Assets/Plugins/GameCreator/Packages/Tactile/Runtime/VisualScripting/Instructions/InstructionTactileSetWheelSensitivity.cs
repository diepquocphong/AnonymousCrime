using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Sensitivity")]
    [Category("Tactile/Steering Wheel/Set Sensitivity")]
    [Description("Sets the sensitivity of a Steering Wheel")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Steering Wheel"
    )]

    [Parameter(
        "Sensitivity", 
        "The multiplier that adjusts how the Steering Wheel's rotation responds to user dragging"
    )]

    [Image(typeof(IconSteerWheel), ColorTheme.Type.Blue)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetWheelSensitivity : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();
        
        [SerializeField] 
        private PropertyGetDecimal m_Sensitivity = GetDecimalDecimal.Create(1f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => string.Format(
            "Set {0} Sensitivity = {1}",
            this.m_TactileControl,
            this.m_Sensitivity
        );

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSteeringWheel steerWheel) 
            {
                steerWheel.Sensitivity = (float) this.m_Sensitivity.Get(args);
            }

            return DefaultResult;
        }
    }
}