using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Cameras;

namespace Niam.Runtime.Tactile
{
    [Title("Finger Ray Point")]
    [Category("Tactile/Finger Ray Point")]
    
    [Description(
        "Gets the raycast hit point at finger position of the Tactile Control in world space"
    )]
    
    [Image(typeof(IconTactile), typeof(OverlayPhysics))]
    [Keywords("Tactile")]

    [Serializable]
    public class GetPositionTactileFingerRayPoint : PropertyTypeGetPosition
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

        [SerializeField] 
        private PropertyGetGameObject m_Plane = GetGameObjectNone.Create();

        // PROPERTIES: ----------------------------------------------------------------------------
        
        public override string String => 
            $"{this.m_TactileControl}.Finger[{this.m_FingerIndex}] Point";

        // GETTERS: -------------------------------------------------------------------------------

        public override Vector3 Get(Args args)
        {
            var control = this.m_TactileControl.Get<TactileControl>(args);
            if (control == null) return default;

            var camera = this.m_Camera.Get<Camera>(args);
            if (camera == null) return default;

            int index = (int) this.m_FingerIndex.Get(args);
            var finger = control.TouchableArea.GetFinger(index);
            if (finger == null) return default;

            var maxDistance = (float)this.m_MaxDistance.Get(args);
            var planeTransform = this.m_Plane.Get<Transform>(args);

            return this.GetRayPosition(camera, planeTransform, finger.screenPosition, maxDistance);
        }

        public override Vector3 Get(GameObject gameObject)
        {
            var control = this.m_TactileControl.Get<TactileControl>(gameObject);
            if (control == null) return default;

            var camera = this.m_Camera.Get<Camera>(gameObject);
            if (camera == null) return default;

            int index = (int) this.m_FingerIndex.Get(gameObject);
            var finger = control.TouchableArea.GetFinger(index);
            if (finger == null) return default;

            var maxDistance = (float)this.m_MaxDistance.Get(gameObject);
            var planeTransform = this.m_Plane.Get<Transform>(gameObject);

            return this.GetRayPosition(camera, planeTransform, finger.screenPosition, maxDistance);
        }

        private Vector3 GetRayPosition(
            Camera camera, Transform planeTransform, Vector3 position, float maxDistance)
        {
            Ray ray = camera.ScreenPointToRay(position);

            if (planeTransform != null)
            {
                Plane plane = new (planeTransform.up, planeTransform.position);
                if (plane.Raycast(ray, out float distance) && maxDistance > distance)
                {
                    maxDistance = distance;
                }
            }

            if (Physics.Raycast(ray, out RaycastHit m_hit, maxDistance, this.m_LayerMask))
            {
                return m_hit.point;
            }

            return ray.GetPoint(maxDistance);
        }

        public static PropertyGetPosition Create => new (new GetPositionTactileFingerRayPoint());

    }
}