using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Is Interactable")]
    [Category("Tactile/Is Interactable")]

    [Description(
        "Returns true if a Tactile Control is interactable; otherwise, returns false"
    )]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class ConditionTactileIsInteractable : Condition
    {
        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        protected override string Summary => $"Is {this.m_TactileControl} Interactable";
        
        // RUN METHOD: ----------------------------------------------------------------------------

        protected override bool Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.Interactable;
        }
    }
}
