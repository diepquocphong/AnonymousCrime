using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Cameras;

namespace Niam.Runtime.Tactile
{
    [Title("Finger Ray Hit")]
    [Category("Tactile/Finger Ray Hit")]
    
    [Description(
        "Gets the raycast hit point at finger position of the Tactile Control in world space"
    )]
    
    [Image(typeof(IconTactile), typeof(OverlayPhysics))]
    [Keywords("Tactile")]

    [Serializable]
    public class GetGameObjectTactileFingerRayHit : PropertyTypeGetGameObject
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetDecimal m_FingerIndex = GetDecimalConstantZero.Create;

        [SerializeField] 
        private PropertyGetGameObject m_Camera = GetGameObjectCameraMain.Create;

        [SerializeField] 
        private LayerMask m_LayerMask = 1;

        [SerializeField] 
        private PropertyGetDecimal m_MaxDistance = GetDecimalDecimal.Create(1_000f);

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => 
            $"{this.m_TactileControl}.Finger[{this.m_FingerIndex}] Hit";

        // GETTERS: -------------------------------------------------------------------------------

        public override GameObject Get(Args args)
        {
            var control = this.m_TactileControl.Get<TactileControl>(args);
            if (control == null) return null;

            var camera = this.m_Camera.Get<Camera>(args);
            if (camera == null) return null;

            int index = (int) this.m_FingerIndex.Get(args);
            var finger = control.TouchableArea.GetFinger(index);
            if (finger == null) return null;

            var maxDistance = (float)this.m_MaxDistance.Get(args);
            return this.GetRayPosition(camera, finger.screenPosition, maxDistance);
        }

        public override GameObject Get(GameObject gameObject)
        {
            var control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control == null) return null;

            var camera = this.m_Camera.Get<Camera>(gameObject);
            if (camera == null) return null;

            int index = (int) this.m_FingerIndex.Get(gameObject);
            var finger = control.TouchableArea.GetFinger(index);
            if (finger == null) return null;

            var maxDistance = (float)this.m_MaxDistance.Get(gameObject);
            return this.GetRayPosition(camera, finger.screenPosition, maxDistance);
        }

        private GameObject GetRayPosition(Camera camera, Vector3 position, float maxDistance)
        {
            Ray ray = camera.ScreenPointToRay(position);
            if (Physics.Raycast(ray, out RaycastHit m_hit, maxDistance, this.m_LayerMask))
            {
                return m_hit.transform.gameObject;
            }

            return null;
        }

        public static PropertyGetGameObject Create => new (new GetGameObjectTactileFingerRayHit());

    }
}