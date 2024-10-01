using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace NinjutsuGames.StateMachine.Runtime
{
    [System.Serializable, NodeMenuItem("Branch Node")]
    public class BranchNode : BaseGameCreatorNode//, ICreateNodeFrom<Actions>
    {
        [Input("In", true), Vertical] public BranchPortIn input;
        [Output("Out"), Vertical] public BranchPortOut output;

        public Branch branch = new();

        public override string name => "Branch";
        
        public override string layoutStyle => "GraphProcessorStyles/BranchNode";

        protected override void Process(GameObject newContext, Args customArgs = null)
        {
            Context = newContext;
            if(!Application.isPlaying) return;
            if(!CanExecute(newContext)) return;
            
            var args = customArgs ?? GetArgs(newContext).Clone;
            var nodeId = NodeId(newContext);
            var runner = newContext.GetCached<BranchRunner>(nodeId);
            if(runner.IsRunning) return;

            OnStartRunning(newContext);

            runner.Run(branch.GetCachedData(nodeId), args, (result) =>
            {
                if(!Application.isPlaying) return;

                OnStopRunning(newContext, result);
                if(!result) RunChildNodes(args);
            });
        }
        
        protected override void StopRunning(GameObject context)
        {
            if(!Application.isPlaying) return;
            
            var runner = context.GetCached<BranchRunner>(NodeId(context));
            if(!runner) return;

            runner.Cancel();
            OnStopRunning(context);
        }
    }
}