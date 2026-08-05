using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Button Release")]
    [Category("Tactile/Push Button/Button Release")]
    [Description("Detects when a Button control type is released")]

    [Image(typeof(IconPushButton), typeof(OverlayArrowUp))]
    [Keywords("Tactile", "Key", "Button", "Up")]
    
    [Serializable]
    public class InputValueButtonTactileButtonRelease : TInputButton
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
            return new InputPropertyButton(new InputValueButtonTactileButtonRelease());
        }

        // UPDATE METHODS: ------------------------------------------------------------------------
        
        public override void OnStartup()
        { 
            if (ButtonType == null) return;
            TactileControl control = this.m_TactileControl.Value;
            control.TouchableArea.interaction.EventRelease -= this.Run;
            control.TouchableArea.interaction.EventRelease += this.Run;
        }

        public override void OnDispose()
        { 
            if (ButtonType == null) return;
            TactileControl control = this.m_TactileControl.Value;
            control.TouchableArea.interaction.EventRelease -= this.Run;
        }

        private void Run(int releaseCount)
        {
            this.ExecuteEventStart();
            this.ExecuteEventPerform();
        }
    }
}