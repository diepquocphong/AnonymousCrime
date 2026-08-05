using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick Handle")]
    [Category("Tactile/Analog Stick/Stick Handle")]
    [Description("A reference to a handle of Analog Stick")]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]

    [Serializable]
    public class GetGameObjectTactileStickHandle : PropertyTypeGetGameObject
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.Handle";

        // GET & CREATE: --------------------------------------------------------------------------

        public override GameObject Get(Args args) => this.GetObject(args);

        private GameObject GetObject(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is IStickType stick)
            {
                return stick.Handle != null ? stick.Handle.gameObject : null;
            }

            return null;
        }

        public static PropertyGetGameObject Create => new PropertyGetGameObject(
            new GetGameObjectTactileStickHandle()
        );
        
        public override GameObject EditorValue => this.m_TactileControl.EditorValue;
        
    }
}