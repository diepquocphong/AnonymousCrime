using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick Arrow")]
    [Category("Tactile/Analog Stick/Stick Arrow")]
    [Description("A reference to an arrow of Analog Stick")]
    
    [Image(typeof(IconJoystick))]
    [Keywords("Tactile")]

    [Serializable]
    public class GetGameObjectTactileStickArrow : PropertyTypeGetGameObject
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}.Arrow";

        // GET & CREATE: --------------------------------------------------------------------------

        public override GameObject Get(Args args) => this.GetObject(args);

        private GameObject GetObject(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null && control.ControlType is IStickType stick)
            {
                return stick.Arrow != null ? stick.Arrow.gameObject : null;
            }

            return null;
        }

        public static PropertyGetGameObject Create => new PropertyGetGameObject(
            new GetGameObjectTactileStickArrow()
        );
        
        public override GameObject EditorValue => this.m_TactileControl.EditorValue;
        
    }
}