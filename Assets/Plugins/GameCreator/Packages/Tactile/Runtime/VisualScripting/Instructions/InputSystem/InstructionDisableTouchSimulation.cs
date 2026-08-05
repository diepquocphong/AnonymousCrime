using System;
using System.Threading.Tasks;
using UnityEngine.InputSystem.EnhancedTouch;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Disable Touch Simulation")]
    [Category("Input System/Disable Touch Simulation")]

    [Description(
        "Disable simulating touch input from other kinds of Pointer devices such as mouse and " +
        "pen devices"
    )]

    [Image(typeof(IconTouch), ColorTheme.Type.Red)]
    [Keywords("Input", "Pen", "Mouse", "Finger", "Emulate")]

    [Serializable]
    public class InstructionDisableTouchSimulation : Instruction
    {
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Disable Touch Simulation";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TouchSimulation.Disable();
            return DefaultResult;
        }

    }
}