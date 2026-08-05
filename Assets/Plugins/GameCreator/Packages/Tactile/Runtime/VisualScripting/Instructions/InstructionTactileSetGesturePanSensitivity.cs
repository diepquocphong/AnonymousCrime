using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Pan Sensitivity")]
    [Category("Tactile/Gesture Pad/Set Pan Sensitivity")]
    [Description("Sets the sensitivity for panning of a Gesture Pad")]

    [Parameter(
        "Tactile Control", 
        "The game object with Tactile Control component attached with a control type " +
        "Gesture Pad"
    )]

    [Parameter(
        "Sensitivity X", 
        "The multiplier that adjusts the delta x value of pan"
    )]

    [Parameter(
        "Sensitivity Y", 
        "The multiplier that adjusts the delta y value of pan"
    )]

    [Image(typeof(IconGesture), ColorTheme.Type.Blue)]
    [Keywords("Tactile", "Multiplier")]

    [Serializable]
    public class InstructionTactileSetGesturePanSensitivity : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_SensitivityX = GetDecimalDecimal.Create(1f);

        [SerializeField] 
        private PropertyGetDecimal m_SensitivityY = GetDecimalDecimal.Create(1f);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => string.Format(
            "Set {0} Pan Sensitivity = ({1}, {2})",
            this.m_TactileControl,
            this.m_SensitivityX,
            this.m_SensitivityY
        );

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is ControlTypeGesturePad pad) 
            {
                pad.PanSensitivity = new Vector2(
                    (float) this.m_SensitivityX.Get(args),
                    (float) this.m_SensitivityY.Get(args)
                );
            }

            return DefaultResult;
        }
    }
}