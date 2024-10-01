using GameCreator.Editor.Common;
using UnityEngine;
using NinjutsuGames.StateMachine.Runtime;

namespace NinjutsuGames.StateMachine.Editor
{
    [NodeCustomEditor(typeof(ExitNode))]
    public class ExitNodeView : BaseGameCreatorNodeView
    {
        public override Texture2D DefaultIcon => ICON_ARROW.Texture;
        
        public const string INFO_MESSAGE = "This is the exit point of the state machine.";
    }
}