using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile Device")]
    [Category("Tactile Device")]
    [Description("")]

    [Image(typeof(IconTactile), ColorTheme.Type.Yellow)]
    [Keywords("")]

    [Serializable]
    public class ControlPathButtonTactileDevice : ControlPathButton
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] FieldDeviceButton m_Button = new FieldDeviceButton();

        [NonSerialized] private string m_LastName;
        [NonSerialized] private string m_LastPath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string ControlPath
        {
            get
            {
                string name = this.m_Button.Name;
                if (string.IsNullOrEmpty(name))
                    return string.Empty;
                
                if (name == this.m_LastName)
                    return this.m_LastPath;

                this.m_LastName = name;
                this.m_LastPath = string.Concat("<", DeviceBuilder.LAYOUT_NAME, ">/", name);

                return this.m_LastPath;
            }
        }

    }
}