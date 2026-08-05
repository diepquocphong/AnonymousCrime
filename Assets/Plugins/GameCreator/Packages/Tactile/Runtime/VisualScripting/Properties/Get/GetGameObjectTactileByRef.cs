using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Tactile Control by Ref")]
    [Category("Tactile/Tactile Control by Ref")]
    [Description("A reference to a Tactile Control component")]
    
    [Image(typeof(IconTactile))]
    [Keywords("Tactile")]

    [Serializable] [HideLabelsInEditor]
    public class GetGameObjectTactileByRef : PropertyTypeGetGameObject
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private TactileControl m_TactileControl;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_TactileControl}";

        // GET & CREATE: --------------------------------------------------------------------------

        public override GameObject Get(Args args) => this.GetGameObject();

        public override GameObject Get(GameObject gameObject) => this.GetGameObject();

        public static PropertyGetGameObject Create() => new PropertyGetGameObject(
            new GetGameObjectTactileByRef()
        );

        public static PropertyGetGameObject Create(TactileControl control) =>
            new PropertyGetGameObject(new GetGameObjectTactileByRef() {
                m_TactileControl = control
        });

        private GameObject GetGameObject() => this.m_TactileControl != null
            ? this.m_TactileControl.gameObject : null;

        // EDITOR: --------------------------------------------------------------------------------

        public override GameObject EditorValue => this.GetGameObject();
    }
}