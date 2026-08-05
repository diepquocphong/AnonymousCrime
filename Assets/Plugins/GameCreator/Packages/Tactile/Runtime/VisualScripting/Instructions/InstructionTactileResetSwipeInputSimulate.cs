using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Reset Input Simulate")]
    [Category("Tactile/Swipe Pad/Reset Input Simulate")]
    [Description("Resets the control path of a Swipe Pad's Input Simulate")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Swipe Pad"
    )]

    [Parameter("Swipe ID", "The id of the swipe direction to set")]

    [Image(typeof(IconSwipe), ColorTheme.Type.Yellow)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileResetSwipeInputSimulate : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private IdString m_SwipeID = new IdString();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl}[{this.m_SwipeID}] Input Simulate";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad swipePad) 
            {
                swipePad.Directions.GetDirectionByHash(this.m_SwipeID.Hash)?
                    .InputSimulate.ResetOverrideControlPath();
            }

            return DefaultResult;
        }
    }
}