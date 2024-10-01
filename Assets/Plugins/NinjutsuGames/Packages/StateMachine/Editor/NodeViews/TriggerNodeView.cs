using GameCreator.Editor.Common;
using UnityEngine;
using NinjutsuGames.StateMachine.Runtime;

namespace NinjutsuGames.StateMachine.Editor
{
    [NodeCustomEditor(typeof(TriggerNode))]
    public class TriggerNodeView : BaseGameCreatorNodeView
    {
        public override Texture2D DefaultIcon => ICON_EVENT.Texture;
        public override string DefaultIconName => ((TriggerNode)nodeTarget).triggerEvent == null ? string.Empty :((TriggerNode)nodeTarget).triggerEvent.GetType().Name;

        public override void ShowHelp()
        {
            var triggerNode = (TriggerNode)nodeTarget;
            DocumentationPopup.Open(triggerNode.triggerEvent?.GetType());
        }
    }
}