using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Compare Magnitude")]
    [Category("Math/Geometry/Compare Magnitude")]
    
    [Description(
        "Returns true if a comparison of the magnitude of point is satisfied; Otherwise, " + 
        "returns false"
    )]
    
    [Parameter("Point", "The Position that represents a point in space")]
    [Parameter("Comparison", "The comparison operation performed between both values")]
    [Parameter("Magnitude", "The magnitude value compared against")]
    
    [Keywords("Position", "Vector", "Distance", "Length")]
    [Keywords("Equals", "Different", "Greater", "Larger", "Smaller")]
    [Image(typeof(IconCompass), ColorTheme.Type.Yellow)]

    [Serializable]
    public class ConditionMathCompareMagnitude : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] private PropertyGetPosition m_Point = new PropertyGetPosition();
        [SerializeField] private CompareMagnitude m_Magnitude = new CompareMagnitude();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        protected override string Summary => $"Magnitude of {this.m_Point} {this.m_Magnitude}";
        
        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            return this.m_Magnitude.Match(this.m_Point.Get(args), args);
        }
    }
}
