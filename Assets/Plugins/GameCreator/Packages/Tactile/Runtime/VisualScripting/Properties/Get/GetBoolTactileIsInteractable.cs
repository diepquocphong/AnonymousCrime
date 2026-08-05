using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Is Interactable")]
    [Category("Tactile/Is Interactable")]
    [Description("Gets true if a Tactile Control is interactable; Otherwise, false")]
    
    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetBoolTactileIsInteractable : PropertyTypeGetBool
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"is {this.m_TactileControl} Interactable";

        // GETTERS: -------------------------------------------------------------------------------

        public override bool Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null && control.Interactable;
        }

        public override bool Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return control != null && control.Interactable;
        }

    }
}