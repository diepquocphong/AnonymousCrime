using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Finger Last Index")]
    [Category("Tactile/Finger Last Index")]
    [Description(
        "Gets the last index of fingers interacting with a Tactile Control; Gets -1 if no fingers"
    )]
    
    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]
    
    [Serializable]
    public class GetDecimalTactileFingerLastIndex : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.Fingers - 1";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null ? control.TouchableArea.fingerCount - 1 : -1;
        }

        public override double Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return control != null ? control.TouchableArea.fingerCount - 1 : -1;
        }

    }
}