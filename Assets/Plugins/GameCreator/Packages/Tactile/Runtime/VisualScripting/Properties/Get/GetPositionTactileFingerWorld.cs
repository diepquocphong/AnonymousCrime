using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Finger World Point")]
    [Category("Tactile/Finger World Point")]
    [Description("Gets the position of the finger on the Tactile Control in world space")]
    
    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]

    [Serializable]
    public class GetPositionTactileFingerWorld : PropertyTypeGetPosition
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        [UnityEngine.Serialization.FormerlySerializedAs("m_Index")] 
        private PropertyGetDecimal m_FingerIndex = GetDecimalConstantZero.Create;

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => $"{this.m_TactileControl}.Finger[{this.m_FingerIndex}]";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control == null) return default;

            int index = (int) this.m_FingerIndex.Get(args);
            if (control.TouchableArea.fingerCount <= index) return default;

            Camera camera = ShortcutMainCamera.Get<Camera>();
            if (camera == null) return default;

            var point = control.TouchableArea.GetFinger(index).screenPosition;
            return camera.ScreenToWorldPoint(point);
        }

        public override Vector3 Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control == null) return default;

            int index = (int) this.m_FingerIndex.Get(gameObject);
            if (control.TouchableArea.fingerCount <= index) return default;

            Camera camera = ShortcutMainCamera.Get<Camera>();
            if (camera == null) return default;

            var point = control.TouchableArea.GetFinger(index).screenPosition;
            return camera.ScreenToWorldPoint(point);
        }

        public static PropertyGetPosition Create => new PropertyGetPosition(
            new GetPositionTactileFingerWorld()
        );

    }
}