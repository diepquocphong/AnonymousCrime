using System;
using System.Threading.Tasks;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;

namespace Niam.Runtime.Tactile
{
    [Title("Set Raycast")]
    [Category("Tactile/Set Raycast")]
    [Description("Configures raycast settings for the Tactile Control's touchable area")]

    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]
    
    [Parameter(
        "Raycast", 
        "Whether to cast a ray at the touched point to check if another graphic element " +
        "is above. Specify a LayerMask to filter the objects hit by the raycast"
    )]

    [Image(typeof(IconArea), typeof(OverlayPhysics))]
    [Keywords("Tactile")]

    [Serializable]
    public class InstructionTactileSetTouchableAreaRaycast : Instruction
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [SerializeField]
        private EnablerLayerMask m_Raycast = new EnablerLayerMask(true, 0x20);

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Title => 
            $"Set {this.m_TactileControl} Raycast = {this.m_Raycast.IsEnabled}";

        // RUN METHOD: ----------------------------------------------------------------------------

        protected override Task Run(Args args)
        {
            TactileControl control = this.m_TactileControl.Get<TactileControl>(args);
            if (control != null)
            {
                control.TouchableArea.CanRaycast = this.m_Raycast.IsEnabled;
                control.TouchableArea.RaycastLayerMask = this.m_Raycast.Value;
            }

            return DefaultResult;
        }
    }
}