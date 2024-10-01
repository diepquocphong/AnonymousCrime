using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace NinjutsuGames.StateMachine.Runtime
{
    [Serializable, NodeMenuItem("Actions Node")]
    public class ActionsNode : BaseGameCreatorNode, ICreateNodeFrom<Actions>
    {
        [Input("In", true)] public ActionPortIn input;
        [Input("In", true), Vertical] public TriggerPortIn input2;
        [Output("Out")] public ActionPortOut output;
        
        public InstructionList instructions = new();

        public override string name => "Actions";
        public override string layoutStyle => "GraphProcessorStyles/ActionsNode";

        protected override void Process(GameObject newContext, Args customArgs = null)
        {
            Context = newContext;
            if(!Application.isPlaying) return;
            if(!CanExecute(newContext)) return;
            
            var nodeId = NodeId(newContext);
            var args = customArgs ?? GetArgs(newContext);
            var runner = newContext.GetCached<ActionsRunner>(nodeId);
            if(runner.IsRunning) return;
            OnStartRunning(newContext);

            runner.Run(instructions.GetCachedData(nodeId), args.Clone, (args1) =>
            {
                if(!Application.isPlaying) return;
                OnStopRunning(args1.Self ? args1.Self : args1.Target);
                RunChildNodes(args1);
            });
        }
        
        public bool InitializeNodeFromObject(Actions value)
        {
#if UNITY_EDITOR
            instructions = value.GetInstructionsList().Clone();
            return instructions != null;
#else
            return true;
#endif
        }
        
        protected override void StopRunning(GameObject context)
        {
            if(!Application.isPlaying) return;
            
            var runner = context.GetCached<ActionsRunner>(NodeId(context));
            if(!runner) return;
            runner.Cancel();
            OnStopRunning(context);
        }
    }
}