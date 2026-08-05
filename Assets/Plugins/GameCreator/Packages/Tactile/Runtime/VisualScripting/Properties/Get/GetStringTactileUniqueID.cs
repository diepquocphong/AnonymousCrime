using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile ID")]
    [Category("Tactile/Tactile ID")]
    [Description("Gets the unique ID of a Tactile Control")]

    [Image(typeof(IconID))]
    [Keywords("Tactile", "Unique", "Guid")]
    
    [Serializable] [HideLabelsInEditor]
    public class GetStringTactileUniqueID : PropertyTypeGetString
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl} Unique ID";

        // GET & CREATE: --------------------------------------------------------------------------

        public override string Get(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            return control != null ? control.UniqueID : string.Empty;
        }

        public override string Get(GameObject gameObject)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(gameObject);
            return control != null ? control.UniqueID : string.Empty;
        }

        public static PropertyGetString Create => new PropertyGetString(
            new GetStringTactileUniqueID()
        );

    }

}