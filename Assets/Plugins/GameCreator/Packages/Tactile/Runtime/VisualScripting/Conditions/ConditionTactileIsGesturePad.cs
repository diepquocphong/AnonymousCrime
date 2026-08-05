using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Gesture Pad")]
    [Category("Tactile/Gesture Pad/Is Gesture Pad")]

    [Description(
        "Returns true if the control type of a Tactile Control is set to Gesture Pad; " +
        "otherwise, returns false"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Image(typeof(IconGesture), ColorTheme.Type.Green)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsGesturePad : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is {this.m_TactileControl} a Gesture Pad";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.ControlType is ControlTypeGesturePad;
        }
    }
}
