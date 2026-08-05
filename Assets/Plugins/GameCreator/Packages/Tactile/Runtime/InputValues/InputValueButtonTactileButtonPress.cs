using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Button Press")]
    [Category("Tactile/Push Button/Button Press")]
    [Description("Detects when a Button control type is pressed")]

    [Image(typeof(IconPushButton), typeof(OverlayArrowDown))]
    [Keywords("Tactile", "Key", "Button", "Down")]
    
    [Serializable]
    public class InputValueButtonTactileButtonPress : TInputButton
    {
        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] private ControlReference m_TactileControl = new ControlReference();
        [NonSerialized] private IButtonType m_ButtonType;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        private IButtonType ButtonType
        {
            get
            {
                if (this.m_ButtonType == null)
                {
                    TactileControl control = this.m_TactileControl.Value;
                    if (control != null) this.m_ButtonType = control.ControlType as IButtonType;
                }

                return this.m_ButtonType;
            }
        }

        // INITIALIZERS: --------------------------------------------------------------------------

        public static InputPropertyButton Create()
        {
            return new InputPropertyButton(new InputValueButtonTactileButtonPress());
        }

        // UPDATE METHODS: ------------------------------------------------------------------------
        
        public override void OnStartup()
        { 
            if (ButtonType == null) return;
            TactileControl control = this.m_TactileControl.Value;
            control.TouchableArea.interaction.EventPress -= this.Run;
            control.TouchableArea.interaction.EventPress += this.Run;
        }

        public override void OnDispose()
        { 
            if (ButtonType == null) return;
            TactileControl control = this.m_TactileControl.Value;
            control.TouchableArea.interaction.EventPress -= this.Run;
        }

        private void Run(int pressCount)
        {
            this.ExecuteEventStart();
            this.ExecuteEventPerform();
        }
    }
}