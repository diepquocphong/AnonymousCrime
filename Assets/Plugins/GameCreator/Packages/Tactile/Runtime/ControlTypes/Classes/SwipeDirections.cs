using System;
using UnityEngine;

namespace Niam.Runtime.Tactile 
{
    [Serializable]
    public class SwipeDirections
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private SwipeDirection[] m_Values = new []
        {
            new SwipeDirection("swipe-up", 0f, 90f),
            new SwipeDirection("swipe-right", 90f, 90f),
            new SwipeDirection("swipe-down", 180f, 90f),
            new SwipeDirection("swipe-left", 270f, 90f)
        };

        // PROPERTIES: ----------------------------------------------------------------------------

        public int Length => this.m_Values.Length;

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void OnEnabled(TactileControl control)
        {
            for (int i = 0; i < this.Length; i++) this.m_Values[i].Enable(control);
        }

        public void OnDisabled()
        {
            for (int i = 0; i < this.Length; i++) this.m_Values[i].Disable();
        }

        public SwipeDirection GetDirectionByIndex(int index)
        {
            if (index < 0 || index >= this.Length)
                return null;
                
            return this.m_Values[index];
        }

        public SwipeDirection GetDirectionByHash(int hash)
        {
            for (int i = 0; i < this.Length; i++)
            {
                if (this.m_Values[i].Hash == hash) 
                    return this.m_Values[i];
            }

            return null;
        }

        public int CheckForDirections(float input, int fingers = -1)
        {
            for (int i = 0; i < this.Length; i++)
            {
                SwipeDirection value = this.m_Values[i];

                if (!value.IsActive) continue;
                if (fingers != -1 && value.Fingers != fingers) continue;
                if (!value.IsWithinBounds(input)) continue;

                value.SimulateInputButton();
                return value.Hash;
            }

            return 0;
        }

    }
}