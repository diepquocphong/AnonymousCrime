using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Pressing")]
    [Category("Tactile/Is Pressing")]

    [Description(
        "Returns true if a Tactile Control is being pressed; otherwise, returns false"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Image(typeof(IconTactile), ColorTheme.Type.Blue)]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsPressing : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string Summary => $"Is {this.m_TactileControl} Pressing";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.TouchableArea.interaction.IsPressed;
        }
    }
}
