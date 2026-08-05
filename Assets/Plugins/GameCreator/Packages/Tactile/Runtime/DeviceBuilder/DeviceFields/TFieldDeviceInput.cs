using System;
using UnityEngine;

namespace Niam.Runtime.Tactile
{
    [Serializable] 
    public abstract class TFieldDeviceInput
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField, HideInInspector] protected string m_Name;
        [SerializeField, HideInInspector] protected string m_DisplayName;

        // PROPERTIES: ----------------------------------------------------------------------------

        public string Name => this.m_Name;
        public string DisplayName => this.m_DisplayName;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public TFieldDeviceInput() { }
        public TFieldDeviceInput(string name, string displayName) : this()
        {
            this.m_Name = name;
            this.m_DisplayName = displayName;
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override string ToString()
        {
            return string.IsNullOrEmpty(this.m_DisplayName) 
                ? string.IsNullOrEmpty(this.m_Name) ? "<empty>" : this.m_Name
                : this.m_DisplayName;
        }
    }
}