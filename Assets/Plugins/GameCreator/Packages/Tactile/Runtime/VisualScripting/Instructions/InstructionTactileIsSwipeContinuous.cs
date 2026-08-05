using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Continuous")]
    [Category("Tactile/Swipe Pad/Is Continuous")]
    [Description("Sets the continuous of a Swipe Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Swipe Pad"
    )]

    [Parameter(
        "Is Continuous", 
        "Whether the swipe actions are continuous or only triggered once per swipe"
    )]

    [Image(typeof(IconSwipe))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileIsSwipeContinuous : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetBool m_IsContinuous = GetBoolTrue.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Continuous = {this.m_IsContinuous}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeSwipePad pad) 
            {
                pad.IsContinuous = this.m_IsContinuous.Get(args);
            }

            return DefaultResult;
        }
    }
}