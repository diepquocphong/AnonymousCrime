using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Gamepad Right Stick")]
    [Category("Gamepad/Right Stick")]
    [Description("")]

    [Image(typeof(IconJoystick), ColorTheme.Type.Yellow, typeof(OverlayArrowRight))]
    [Keywords("")]

    [Serializable]
    public class ControlPathButtonGamepadRightStick : ControlPathButton
    {
        private enum Stick : byte
        {
            Up,
            Down,
            Left,
            Right
        }

        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] private Stick m_RightStick = Stick.Up;

        [NonSerialized] private string m_LastName;
        [NonSerialized] private string m_LastPath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string ControlPath
        {
            get
            {
                string buttonName = GetButtonName(this.m_RightStick);
                if (string.IsNullOrEmpty(buttonName))
                    return string.Empty;
                
                if (buttonName == this.m_LastName)
                    return this.m_LastPath;

                this.m_LastName = buttonName;
                this.m_LastPath = "<Gamepad>/rightStick/" + buttonName;

                return this.m_LastPath;
            }
        }

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public ControlPathButtonGamepadRightStick()
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