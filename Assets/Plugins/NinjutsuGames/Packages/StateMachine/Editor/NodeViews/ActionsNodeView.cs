using UnityEngine;
using NinjutsuGames.StateMachine.Runtime;

namespace NinjutsuGames.StateMachine.Editor
{
    [NodeCustomEditor(typeof(ActionsNode))]
    public class ActionsNodeView : BaseGameCreatorNodeView
    {
        public override Texture2D DefaultIcon => ICON_INSTRUCTION.Texture;
        public override string DefaultIconName => ((ActionsNode)nodeTarget).instructions.Length > 0 ? ((ActionsNode)nodeTarget).instructions.Get(0).GetType().Name : null;

        public override void Enable()
        {
            base.Enable();
            
            var node = (ActionsNode) nodeTarget;
            AddCounter(node.instructions.Length);
        }
        
        public override void Update()
        {
            if (nodeTarget is ActionsNode n) UpdateCounter(n.instructions.Length);
        }
    }
}