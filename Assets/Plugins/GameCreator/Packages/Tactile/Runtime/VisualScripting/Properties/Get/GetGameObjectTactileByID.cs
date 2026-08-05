using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile Control by ID")]
    [Category("Tactile/Tactile Control by ID")]
    [Description("Gets the instance of a Tactile Control with the given ID")]
    
    [Image(typeof(IconID))]
    [Keywords("Tactile")]

    [Serializable]
    public class GetGameObjectTactileByID : PropertyTypeGetGameObject
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private PropertyGetString m_ID = new PropertyGetString("my-tactile-id");
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"Tactile[{this.m_ID}]";

        // GET & CREATE: --------------------------------------------------------------------------

        public override GameObject Get(Args args) => this.GetObject(args);

        private GameObject GetObject(Args args)
        {
            string id = this.m_ID.Get(args);
            TactileControl control = TactileControl.GetControlByID(id);
            return control != null ? control.gameObject : null;
        }

        public static PropertyGetGameObject Create => new PropertyGetGameObject(
            new GetGameObjectTactileByID()
        );
        
        // EDITOR: --------------------------------------------------------------------------------

        public override GameObject EditorValue
        {
            get
            {
                #if UNITY_6000_0_OR_NEWER
                var instances = UnityEngine.Object.FindObjectsByType<TactileControl>(
                    FindObjectsSortMode.None
                );
                #else
                var instances = UnityEngine.Object.FindObjectsOfType<TactileControl>();
                #endif

                string id = this.m_ID.ToString();
                if (string.IsNullOrEmpty(id)) return null;
                
                int hash = id.GetHashCode();
                foreach (TactileControl instance in instances)
                {
                    if (instance.UniqueID.GetHashCode() == hash)
                        return instance.gameObject;
                }

                return null;
            }
        }
    }
}