using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Button Value")]
    [Category("Tactile/Push Button/Button Value")]
    [Description("Gets the current value (0 or 1) of a Push Button")]
    
    [Image(typeof(IconPushButton))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileButtonValue : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.ButtonValue";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is IButtonType button) 
            {
                return button.Value;
            }

            return 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control != null && control.ControlType is IButtonType button) 
            {
                return button.Value;
            }
            
            return 0f;
        }

    }
}