using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Max Angle")]
    [Category("Tactile/Steering Wheel/Set Max Angle")]
    [Description("Sets the max angle of a Steering Wheel")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Steering Wheel"
    )]

    [Parameter(
        "Has Max Angle", 
        "Whether to clamp the rotation angle of the steering wheel"
    )]
    [Parameter(
        "Max Angle", 
        "The maximum angle the Steering Wheel can rotate in either direction"
    )]

    [Image(typeof(IconSteerWheel))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetWheelMaxAngle : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_HasMaxAngle = GetBoolTrue.Create;

        [SerializeField] 
        private PropertyGetDecimal m_MaxAngle = GetDecimalDecimal.Create(360f);
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => string.Format(
            "Set {0} Max Angle = {1}",
            this.m_TactileControl,
            this.m_HasMaxAngle,
            this.m_MaxAngle
        );

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSteeringWheel steerWheel) 
            {
                steerWheel.HasMaxAngle = this.m_HasMaxAngle.Get(args);
                steerWheel.MaxAngle = (float) this.m_MaxAngle.Get(args);
            }

            return DefaultResult;
        }
    }
}