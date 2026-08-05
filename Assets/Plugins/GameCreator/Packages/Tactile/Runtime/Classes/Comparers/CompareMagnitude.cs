using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class CompareMagnitude
    {
        private enum Comparison : byte
        {
            Equals,
            Different,
            Less,
            Greater,
            LessOrEqual,
            GreaterOrEqual
        }
        
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] private Comparison m_Comparison = Comparison.GreaterOrEqual;
        [SerializeField] private PropertyGetDecimal m_CompareTo = new PropertyGetDecimal(1f);
        
        // CONSTRUCTORS: --------------------------------------------------------------------------

        public CompareMagnitude()
        { }

        public CompareMagnitude(PropertyGetDecimal number) : this()
        {
            this.m_CompareTo = number;
        }
        
        public CompareMagnitude(float value) : this(new PropertyGetDecimal(value))
        { }
        
        public CompareMagnitude(double value) : this(new PropertyGetDecimal(value))
        { }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public bool Match(Vector3 value, Args args)
        {
            float a = value.sqrMagnitude;
            float b = (float) this.m_CompareTo.Get(args);
            b *= b;

            return this.m_Comparison switch
            {
                Comparison.Equals => Mathf.Approximately(a, b),
                Comparison.Different => !Mathf.Approximately(a, b),
                Comparison.Less => (a < b) && !Mathf.Approximately(a, b),
                Comparison.Greater => (a > b) && !Mathf.Approximately(a, b),
                Comparison.LessOrEqual => (a < b) || Mathf.Approximately(a, b),
                Comparison.GreaterOrEqual => (a > b) || Mathf.Approximately(a, b),
                _ => throw new ArgumentOutOfRangeException($"Enum '{this.m_Comparison}' not found")
            };
        }

        // STRING: --------------------------------------------------------------------------------

        public override string ToString()
        {
            string operation = this.m_Comparison switch
            {
                Comparison.Equals => "=",
                Comparison.Different => "≠",
                Comparison.Less => "<",
                Comparison.Greater => ">",
                Comparison.LessOrEqual => "≤",
                Comparison.GreaterOrEqual => "≥",
                _ => string.Empty
            };
            
            return $"{operation} {this.m_CompareTo}";
        }
    }
}