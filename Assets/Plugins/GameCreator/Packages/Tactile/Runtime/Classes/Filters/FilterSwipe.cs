using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class FilterSwipe
    {
        private enum Filter : byte
        {
            Any         = 0x0,
            Specific    = 0x1,
        }

        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField]
        private Filter m_Option = Filter.Any;

        [SerializeField] 
        private IdString m_SwipeID;

        // PROPERTIES: ----------------------------------------------------------------------------

        public bool IsAny => this.m_Option == Filter.Any;

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public bool Match(int compareHash)
        {
            return this.m_Option switch
            {
                Filter.Any => true,
                Filter.Specific => compareHash == this.m_SwipeID.Hash,
                _ => throw new NotImplementedException()
            };
        }
    }
}