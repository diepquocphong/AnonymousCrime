using System;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace NinjutsuGames.StateMachine.Runtime
{
    public class BaseGameCreatorNode : BaseNode
    {
        public override bool isRenamable => true;
        public override bool needsInspector => true;
        public override bool hideControls => false;
        public override bool showControlsOnHover => false;
        public virtual bool useNetwork => true;

        public bool showControls { get; set; }
        
        [SerializeReference] public NetworkingSettings networkingSettings;

        public event Action<GameObject> EventStartRunning;
        public event Action<GameObject, bool> EventStopRunning;
        public event ProcessDelegate OnExecutionDisabled;
        public event ProcessDelegate OnExecutionEnabled;
        
        protected Args GetArgs(GameObject fallbackTarget)
        {
            return new Args(fallbackTarget);
        }

        protected void RunChildNodes(Args args)
        {
            var nodes = GetOutputNodes();
            var currentContext = args.Self;
            foreach (var baseNode in nodes)
            {
                var node = (BaseGameCreatorNode) baseNode;
                if (!node.CanExecute(currentContext)) continue;
                node.OnProcess(currentContext, args);
            }
        }

        protected void OnStartRunning(GameObject currentContext)
        {
            var id = NodeId(currentContext);
            if (IsContextRunning.Contains(id)) return;
            IsContextRunning.Add(id);
            EventStartRunning?.Invoke(currentContext);
        }

        protected void OnStopRunning(GameObject currentContext, bool runResult = true)
        {
            var id = NodeId(currentContext);
            if (!IsContextRunning.Contains(id)) return;
            IsContextRunning.Remove(id);
            EventStopRunning?.Invoke(currentContext, runResult);
        }

        public void Reset()
        {
            IsContextRunning.Clear();
        }

        protected override void Enable()
        {
            base.Enable();
            IsContextRunning.Clear();
        }

        protected override void Disable()
        {
            base.Disable();
            IsContextRunning.Clear();
        }

        public void Stop(GameObject context)
        {
            StopRunning(context);
        }
        
        public void Disable(GameObject context)
        {
            if(context)
            {
                var id = NodeId(context);
                if(!IsContextDisabled.Contains(id))
                {
                    IsContextDisabled.Add(id);
                }
            }
            else enabledForExecution = false;
            
            OnExecutionDisabled?.Invoke();
        }
        
        public void Enable(GameObject context)
        {
            if(context)
            {
                var id = NodeId(context);
                if(IsContextDisabled.Contains(id)) 
                {
                    IsContextDisabled.Remove(id);
                }
            }
            else enabledForExecution = true;

            OnExecutionEnabled?.Invoke();
        }

        protected virtual void StopRunning(GameObject context) {}

        public bool IsRunning(GameObject context)
        {
            var canExecute = !context ? enabledForExecution : IsContextDisabled.Contains(NodeId(context));
            return canExecute && IsContextRunning.Contains(NodeId(!context ? Context : context));
        }
    }
}