using UnityEngine;
using NinjutsuGames.StateMachine.Runtime;

namespace NinjutsuGames.StateMachine.Editor
{
    [NodeCustomEditor(typeof(StartNode))]
    public class StartNodeView : BaseGameCreatorNodeView
    {
        public override Texture2D DefaultIcon => ICON_ARROW.Texture;
        
        public const string INFO_MESSAGE = "This is the entry point of the state machine. " +
                                           "It will be executed when the state machine is started.";
    }
}