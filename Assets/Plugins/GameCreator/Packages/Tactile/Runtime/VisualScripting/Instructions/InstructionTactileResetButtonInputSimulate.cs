using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Reset Input Simulate")]
    [Category("Tactile/Push Button/Reset Input Simulate")]
    [Description("Resets the control path of a Button type's Input Simulate")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Button"
    )]

    [Image(typeof(IconPushButton), ColorTheme.Type.Yellow)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileResetButtonInputSimulate : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Reset {this.m_TactileControl} Input Simulate";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is TButtonType button) 
            {
                button.InputSimulate.ResetOverrideControlPath();
            }

            return DefaultResult;
        }
    }
}