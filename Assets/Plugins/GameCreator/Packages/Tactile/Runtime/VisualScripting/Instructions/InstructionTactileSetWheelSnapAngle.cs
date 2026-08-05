using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Snap Angle")]
    [Category("Tactile/Steering Wheel/Set Snap Angle")]
    [Description("Sets the sensitivity of a Steering Wheel")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Steering Wheel"
    )]

    [Parameter(
        "Can Snap", 
        "Whether to snap the rotation angle of the Steering Wheel"
    )]
    [Parameter(
        "Snap Angle", 
        "The angle at which the Steering Wheel snaps to the nearest fixed angle"
    )]

    [Image(typeof(IconSteerWheel))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetWheelSnapAngle : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_CanSnap = GetBoolFalse.Create;
        
        [SerializeField] 
        private PropertyGetDecimal m_SnapAngle = GetDecimalDecimal.Create(45f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => string.Format(
            "Set {0} Snap Angle = {1}",
            this.m_TactileControl,
            this.m_CanSnap,
            this.m_SnapAngle
        );

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSteeringWheel steerWheel) 
            {
                steerWheel.CanSnap = this.m_CanSnap.Get(args);
                steerWheel.SnapAngle = (float) this.m_SnapAngle.Get(args);
            }

            return DefaultResult;
        }
    }
}