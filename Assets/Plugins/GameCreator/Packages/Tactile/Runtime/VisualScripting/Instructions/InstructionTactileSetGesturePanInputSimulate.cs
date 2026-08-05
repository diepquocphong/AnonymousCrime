using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Pan Input Simulate")]
    [Category("Tactile/Gesture Pad/Set Pan Input Simulate")]
    [Description("Overrides the control path of a Gesture Pad's Pan Input Simulate")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Gesture Pad"
    )]

    [Parameter("Control Path", "The Vector2 control path to be set in Input Simulate")]

    [Image(typeof(IconGesture), ColorTheme.Type.Yellow)]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetGesturePanInputSimulate : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeReference] 
        private ControlPathVector2 m_ControlPath = new ControlPathVector2ConstantNone();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Pan Input Simulate = {this.m_ControlPath}";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad gesture) 
            {
                gesture.PanInputSimulate.OverrideControlPath(this.m_ControlPath);
            }

            return DefaultResult;
        }
    }
}