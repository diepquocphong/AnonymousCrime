using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Steer Wheel")]
    [Category("Tactile/Steering Wheel/Steer Wheel")]
    [Description("A reference to a wheel of Steering Wheel")]
    
    [Image(typeof(IconSteerWheel))]
    [Keywords("Tactile")]

    [Serializable]
    public class GetGameObjectTactileSteerWheel : PropertyTypeGetGameObject
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.Wheel";

        // GET & CREATE: --------------------------------------------------------------------------

        public override GameObject Get(Args args) => this.GetObject(args);

        private GameObject GetObject(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null)
            {
                if (control.ControlType is ControlTypeSteeringWheel wheel) 
                    return wheel.Wheel != null ? wheel.Wheel.gameObject : null;
            }

            return null;
        }

        public static PropertyGetGameObject Create => new PropertyGetGameObject(
            new GetGameObjectTactileSteerWheel()
        );

        public override GameObject EditorValue => this.m_TactileControl.EditorValue;

    }
}