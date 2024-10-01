using System;
using System.Collections.Generic;
using System.Linq;
using GameCreator.Runtime.Common;
using UnityEngine;

// using Unity.Entities;

namespace NinjutsuGames.StateMachine.Runtime
{
    /// <summary>
    /// Graph processor
    /// </summary>
    public class StateMachineGraphProcessor : BaseGraphProcessor
    {
        /// <summary>
        /// Manage graph scheduling and processing
        /// </summary>
        /// <param name="graph">Graph to be processed</param>
        /// <param name="context"></param>
        public StateMachineGraphProcessor(StateMachineAsset graph, GameObject context) : base(graph, context) {}

        public override void UpdateComputeOrder() {}

        /// <summary>
        /// Process all the nodes following the compute order.
        /// </summary>
        public override void Run(Action<Args> onFinish)
        {
            var args = new Args(context);
            var initialNodes = graph.nodes.Where(n => n is TriggerNode or StartNode);
            
            var exitNode = graph.nodes.FirstOrDefault(n => n is ExitNode) as ExitNode;
            exitNode!.OnFinish -= onFinish;
            exitNode.OnFinish += onFinish;
            
            var baseNodes = initialNodes as BaseNode[] ?? initialNodes.ToArray();
            foreach (var node in baseNodes)
            {
                node.OnProcess(context, args);
            }
        }
    }
}