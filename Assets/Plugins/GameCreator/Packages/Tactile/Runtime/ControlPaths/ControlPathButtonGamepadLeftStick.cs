using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad Left Stick")]
    [Category("Gamepad/Left Stick")]
    [Description("")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Yellow, typeof(OverlayArrowLeft))]
    [Keywords("")]

    [Serializable]
    public class ControlPathButtonGamepadLeftStick : ControlPathButton
    {
        private enum Stick : byte
        {
            Up,
            Down,
            Left,
            Right
        }

        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] private Stick m_LeftStick = Stick.Up;

        [NonSerialized] private string m_LastName;
        [NonSerialized] private string m_LastPath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string ControlPath
        {
            get
            {
                string buttonName = GetButtonName(this.m_LeftStick);
                if (string.IsNullOrEmpty(buttonName))
                    return string.Empty;
                
                if (buttonName == this.m_LastName)
                    return this.m_LastPath;

                this.m_LastName = buttonName;
                this.m_LastPath = "<Gamepad>/leftStick/" + buttonName;

                return this.m_LastPath;
            }
        }

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public ControlPathButtonGamepadLeftStick()
        { }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private static string GetButtonName(Stick stick)
        {
            return stick switch
            {
                Stick.Up        => "up",
                Stick.Down      => "down",
                Stick.Left      => "left",
                Stick.Right     => "right",
                _ => throw new NotImplementedException(),
            };
        }

    }
}