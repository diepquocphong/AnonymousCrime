using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Recenter")]
    [Category("Tactile/Steering Wheel/Is Recenter")]
    [Description("Sets the recenter of a Steering Wheel")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Steering Wheel"
    )]

    [Parameter(
        "Is Recenter", 
        "Whether to restore the original rotation of the Steering Wheel"
    )]

    [Image(typeof(IconSteerWheel))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileIsWheelRecenter : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_IsRecenter = GetBoolTrue.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Recenter = {this.m_IsRecenter}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSteeringWheel steerWheel) 
            {
                steerWheel.Recenter = this.m_IsRecenter.Get(args);
            }

            return DefaultResult;
        }
    }
}