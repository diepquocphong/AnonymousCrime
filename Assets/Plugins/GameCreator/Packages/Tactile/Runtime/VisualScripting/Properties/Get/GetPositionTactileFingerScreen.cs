using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Finger Screen Point")]
    [Category("Tactile/Finger Screen Point")]
    [Description("Gets the position of the finger on the Tactile Control in screen space")]
    
    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]

    [Serializable]
    public class GetPositionTactileFingerScreen : PropertyTypeGetPosition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        [UnityEngine.Serialization.FormerlySerializedAs("m_Index")] 
        private PropertyGetDecimal m_FingerIndex = GetDecimalConstantZero.Create;

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => 
            $"{this.m_TactileControl}.Finger[{this.m_FingerIndex}] Point";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control == null) return default;

            int index = (int) this.m_FingerIndex.Get(args);
            return control.TouchableArea.GetFinger(index)?.screenPosition ?? default;
        }

        public override Vector3 Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control == null) return default;

            int index = (int) this.m_FingerIndex.Get(gameObject);
            return control.TouchableArea.GetFinger(index)?.screenPosition ?? default;
        }

        public static PropertyGetPosition Create => new PropertyGetPosition(
            new GetPositionTactileFingerScreen()
        );

    }
}