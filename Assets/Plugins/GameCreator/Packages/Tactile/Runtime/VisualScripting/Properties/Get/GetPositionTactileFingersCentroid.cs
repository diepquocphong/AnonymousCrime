using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Fingers Centroid")]
    [Category("Tactile/Fingers Centroid")]
    [Description("Gets the center position of the fingers on the Tactile Control in screen space")]
    
    [Image(typeof(IconTactile), typeof(OverlayDot))]
    [Keywords("Tactile")]

    [Serializable]
    public class GetPositionTactileFingersCentroid : PropertyTypeGetPosition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl} Fingers Centroid";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null ? control.TouchableArea.fingersCentroid : default;
        }

        public override Vector3 Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return control != null ? control.TouchableArea.fingersCentroid : default;
        }

        public static PropertyGetPosition Create => new PropertyGetPosition(
            new GetPositionTactileFingersCentroid()
        );

    }
}