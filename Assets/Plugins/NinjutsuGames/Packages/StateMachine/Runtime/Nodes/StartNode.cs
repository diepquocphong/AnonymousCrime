using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace NinjutsuGames.StateMachine.Runtime
{
    public class StartNode : BaseGameCreatorNode
    {
        [Output("Out")] public EntryPort output;
        public override string layoutStyle => "GraphProcessorStyles/EntryNode";

        public override string name => "Start";

        public override Color color => new(0.24f, 0.15f, 0.48f);

        public override bool deletable => false;
        public override bool isRenamable => false;
        
        public InstructionList instructions = new();
        
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

            runner.Run(instructions.Clone(), args.Clone, (args1) =>
            {
                if(!Application.isPlaying) return;
                OnStopRunning(newContext);
                RunChildNodes(args1);
            });
        }
    }
}