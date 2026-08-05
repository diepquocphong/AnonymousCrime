using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Finger Count")]
    [Category("Tactile/Finger Count")]
    [Description("Gets the total number of fingers interacting with a Tactile Control")]
    
    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileFingerCount : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.Fingers";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null ? control.TouchableArea.fingerCount : 0f;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return control != null ? control.TouchableArea.fingerCount : 0f;
        }

    }
}