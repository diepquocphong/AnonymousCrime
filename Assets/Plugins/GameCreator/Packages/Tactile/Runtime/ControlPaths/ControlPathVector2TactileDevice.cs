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
    public class ControlPathVector2TactileDevice : ControlPathVector2
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] FieldDeviceVector2 m_Vector = new FieldDeviceVector2();

        [NonSerialized] private string m_LastName;
        [NonSerialized] private string m_LastPath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string ControlPath
        {
            get
            {
                string name = this.m_Vector.Name;
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