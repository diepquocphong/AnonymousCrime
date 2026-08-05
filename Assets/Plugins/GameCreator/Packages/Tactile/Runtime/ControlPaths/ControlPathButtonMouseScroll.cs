using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Mouse Scroll")]
    [Category("Mouse/Scroll")]
    [Description("")]

    [Image(typeof(IconScroll), ColorTheme.Type.Yellow)]
    [Keywords("Mice")]

    [Serializable]
    public class ControlPathButtonMouseScroll : ControlPathButton
    {
        private enum Scroll : byte
        {
            Up,
            Down
        }

        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private Scroll m_Scroll = Scroll.Up;

        [NonSerialized] private string m_LastName;
        [NonSerialized] private string m_LastPath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string ControlPath
        {
            get
            {
                string scrollName = GetScrollName(this.m_Scroll);
                if (string.IsNullOrEmpty(scrollName))
                    return string.Empty;
                
                if (scrollName == this.m_LastName)
                    return this.m_LastPath;

                this.m_LastName = scrollName;
                this.m_LastPath = "<Mouse>/scroll/" + scrollName;

                return this.m_LastPath;
            }
        }

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public ControlPathButtonMouseScroll()
        { }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private static string GetScrollName(Scroll scroll)
        {
            return scroll switch
            {
                Scroll.Up     => "up",
                Scroll.Down   => "down",
                _ => throw new NotImplementedException(),
            };
        }
    }
}