using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Change Tactile ID")]
    [Category("Tactile/Change Tactile ID")]
    [Description("Changes a Tactile Control's unique ID")]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    [Parameter("ID", "The new unique ID of the tactile control")]

    [Image(typeof(IconID))]
    [Keywords("Tactile", "Unique", "Guid")]

    [Serializable]
    public class InstructionTactileChangeUniqueId : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField] 
        private PropertyGetString m_ID = GetStringGuid.Create;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => $"ID of {this.m_TactileControl} = {this.m_ID}";

        // RUN METHOD: ----------------------------------------------------------------------------
        
        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control == null) return DefaultResult;

            string id = this.m_ID.Get(args);
            if (string.IsNullOrEmpty(id)) return DefaultResult;

            IdString idString = new IdString(id);
            control.ChangeUniqueID(idString);

            return DefaultResult;
        }
    }
}