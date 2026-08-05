using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Max Sample Deviation")]
    [Category("Tactile/Swipe Pad/Set Max Sample Deviation")]
    [Description("Sets the maximum samples deviation of a Swipe Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Swipe Pad"
    )]

    [Parameter(
        "Max Sample Deviation", 
        "The maximum allowable deviation of any sample point from the straight line"
    )]

    [Image(typeof(IconSwipe))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetSwipeMaxSampleDeviation : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_MaxSampleDeviation = GetDecimalDecimal.Create(150f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Min Sample Distance = {this.m_MaxSampleDeviation}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad pad) 
            {
                pad.MaxSampleDeviation = (float) this.m_MaxSampleDeviation.Get(args);
            }

            return DefaultResult;
        }
    }
}