using System;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Mouse Button")]
    [Category("Mouse/Button")]
    [Description("")]

    [Image(typeof(IconMouse), ColorTheme.Type.Yellow)]
    [Keywords("Mice")]

    [Serializable]
    public class ControlPathButtonMouseButton : ControlPathButton
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] private MouseButton m_Button = MouseButton.Left;

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
                this.m_LastPath = "<Mouse>/" + buttonName;

                return this.m_LastPath;
            }
        }

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public ControlPathButtonMouseButton()
        { }

        public ControlPathButtonMouseButton(MouseButton button)
        {
            this.m_Button = button;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private static string GetButtonName(MouseButton btn)
        {
            return btn switch
            {
                MouseButton.Left    => "leftButton",
                MouseButton.Right   => "rightButton",
                MouseButton.Middle  => "middleButton",
                MouseButton.Forward => "forwardButton",
                MouseButton.Back    => "backButton",
                _ => throw new NotImplementedException(),
            };
        }
    }
}