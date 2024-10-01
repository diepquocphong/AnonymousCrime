using GameCreator.Runtime.Common;
using UnityEngine;

namespace NinjutsuGames.StateMachine.Runtime
{
    [System.Serializable, NodeMenuItem("StateMachine Node")]
    public class StateMachineNode : BaseGameCreatorNode, ICreateNodeFrom<StateMachineAsset>
    {
        [Input("In", true)] public StateMachinePortIn input;
        [Output("Out")] public StateMachinePortOut output;

        public StateMachineAsset stateMachine;
        public override string name => "State Machine";
        public override string layoutStyle => "GraphProcessorStyles/StateMachineNode";
        public override bool useNetwork => false;

        protected override void Process(GameObject newContext, Args customArgs = null)
        {
            Context = newContext;

            if(!Application.isPlaying) return;
            if (!stateMachine) return;
            if(!CanExecute(newContext)) return;

            OnStartRunning(newContext);
            var processor = new StateMachineGraphProcessor(stateMachine, newContext);
            processor.Run(RunChildNodes);
            OnStopRunning(newContext);
        }

        public bool InitializeNodeFromObject(StateMachineAsset value)
        {
            var result = value && value != StateMachineAsset.Active;
            if (!result) return false;
            nodeCustomName = value.name;
            stateMachine = value;
            return true;
        }
    }
}