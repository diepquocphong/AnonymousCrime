using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Interactable")]
    [Category("Tactile/Is Interactable")]
    [Description("Allow or restrict a Tactile Control to be interacted")]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    [Parameter("Is Interactable", "Whether to set the Tactile Control to be interactable or not")]

    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileIsInteractable : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] private PropertyGetBool m_IsInteractable = GetBoolTrue.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Is {this.m_TactileControl} Interactable = {this.m_IsInteractable}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null) control.Interactable = this.m_IsInteractable.Get(args);

            return DefaultResult;
        }
    }
}