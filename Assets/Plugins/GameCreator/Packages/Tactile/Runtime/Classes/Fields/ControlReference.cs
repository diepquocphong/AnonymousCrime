using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class ControlReference
    {
        private enum Option : byte
        {
            ByRef   = 0x0,
            ByID    = 0x1
        }

        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private Option m_Option;
        [SerializeField] private TactileControl m_Control;
        [SerializeField] private IdString m_ID;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public IdString ID => this.m_ID;

        public TactileControl Value => this.m_Option == Option.ByID
            ? TactileControl.GetControlByID(this.m_ID) : this.m_Control;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public ControlReference()
        { }

        public ControlReference(IdString id) : this()
        { 
            this.m_ID = id;
        }

        public ControlReference(TactileControl control) : this()
        { 
            this.m_Control = control;
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public override string ToString()
        {
            return this.m_Option switch
            {
                Option.ByRef => this.m_Control != null ? this.m_Control.name : "(none)",
                Option.ByID => $"Tactile[{this.m_ID.String}]",
                _ => "(unknown)",
            };
        }

    }
}