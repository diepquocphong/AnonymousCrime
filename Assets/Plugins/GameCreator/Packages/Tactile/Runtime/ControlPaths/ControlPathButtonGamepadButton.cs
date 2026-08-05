using System;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad Button")]
    [Category("Gamepad/Button")]
    [Description("")]

    [Image(typeof(IconGamepad), ColorTheme.Type.Yellow)]
    [Keywords("")]

    [Serializable]
    public class ControlPathButtonGamepadButton : ControlPathButton
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] private GamepadButton m_Button = GamepadButton.South;

        [NonSerialized] private string m_LastName;
        [NonSerialized] private string m_LastPath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string ControlPath
        {
            get
            {
                string buttonName = GetButtonName(this.m_Button);
                if (string.IsNullOrEmpty(buttonName))
                    return string.Empty;
                
                if (buttonName == this.m_LastName)
                    return this.m_LastPath;

                this.m_LastName = buttonName;
                this.m_LastPath = "<Gamepad>/" + buttonName;

                return this.m_LastPath;
            }
        }
        
        // CONSTRUCTORS: --------------------------------------------------------------------------

        public ControlPathButtonGamepadButton()
        { }

        public ControlPathButtonGamepadButton(GamepadButton button)
        {
            this.m_Button = button;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private string GetButtonName(GamepadButton btn)
        {
            return btn switch
            {
                GamepadButton.DpadUp        => "dpad/up",
                GamepadButton.DpadDown      => "dpad/down",
                GamepadButton.DpadLeft      => "dpad/left",
                GamepadButton.DpadRight     => "dpad/right",
                GamepadButton.North         => "buttonNorth",
                GamepadButton.East          => "buttonEast",
                GamepadButton.South         => "buttonSouth",
                GamepadButton.West          => "buttonWest",
                GamepadButton.LeftStick     => "leftStickPress",
                GamepadButton.RightStick    => "rightStickPress",
                GamepadButton.LeftShoulder  => "leftShoulder",
                GamepadButton.RightShoulder => "rightShoulder",
                GamepadButton.Start         => "start",
                GamepadButton.Select        => "select",
                GamepadButton.LeftTrigger or (GamepadButton)0xE   => "leftTrigger",
                GamepadButton.RightTrigger or (GamepadButton)0xF  => "rightTrigger",
                _ => throw new NotImplementedException(),
            };
        }

    }
}