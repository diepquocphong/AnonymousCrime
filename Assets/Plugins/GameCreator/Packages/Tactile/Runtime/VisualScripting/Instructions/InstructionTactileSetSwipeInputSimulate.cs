using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Input Simulate")]
    [Category("Tactile/Swipe Pad/Set Input Simulate")]
    [Description("Overrides the control path of a Swipe Pad's Input Simulate")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Swipe Pad"
    )]
    
    [Parameter("Swipe ID", "The id of the swipe direction to set")]

    [Parameter("Control Path", "The Button control path to be set in Input Simulate")]

    [Image(typeof(IconSwipe), ColorTheme.Type.Yellow)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetSwipeInputSimulate : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private IdString m_SwipeID = new IdString();

        [SerializeReference] 
        private ControlPathButton m_ControlPath = new ControlPathButtonConstantNone();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Input Simulate = {this.m_ControlPath}";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad swipePad) 
            {
                swipePad.Directions.GetDirectionByHash(this.m_SwipeID.Hash)?
                    .InputSimulate.OverrideControlPath(this.m_ControlPath);
            }

            return DefaultResult;
        }
    }
}