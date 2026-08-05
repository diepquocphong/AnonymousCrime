using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine.InputSystem.EnhancedTouch;

namespace Niam.Runtime.Tactile
{
    [Title("Enable Touch Simulation")]
    [Category("Input System/Enable Touch Simulation")]

    [Description(
        "Enable simulating touch input from other kinds of Pointer devices such as mouse and " +
        "pen devices"
    )]
    
    [Image(typeof(IconTouch), ColorTheme.Type.Green)]
    [Keywords("Input", "Pen", "Mouse", "Finger", "Emulate")]

    [Serializable]
    public class InstructionEnableTouchSimulation : Instruction
    {
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"Enable Touch Simulation";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            #if UNITY_EDITOR
            TouchSimulation.Destroy();
            #endif
            
            TouchSimulation.Enable();

            return DefaultResult;
        }

    }
}