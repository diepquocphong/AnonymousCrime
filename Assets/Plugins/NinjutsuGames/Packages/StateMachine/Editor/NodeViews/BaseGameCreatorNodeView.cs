using System.Linq;
using System.Reflection;
using GameCreator.Editor.Common;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using NinjutsuGames.StateMachine.Runtime;
using NinjutsuGames.StateMachine.Runtime.Common;
using UnityEditor.UIElements;

namespace NinjutsuGames.StateMachine.Editor
{
    public class BaseGameCreatorNodeView : BaseNodeView
    {
        protected static readonly IIcon ICON_INSTRUCTION = new IconInstructions(ColorTheme.Type.Blue);
        protected static readonly IIcon ICON_CONDITION = new IconConditions(ColorTheme.Type.Green);
        protected static readonly IIcon ICON_BRANCH = new IconBranch(ColorTheme.Type.Green);
        protected static readonly IIcon ICON_STATE_MACHINE = new IconStateMachine(ColorTheme.Type.Blue);
        protected static readonly IIcon ICON_EVENT = new IconTriggers(ColorTheme.Type.Yellow);
        protected static readonly IIcon ICON_NONE = new IconMarker(ColorTheme.Type.White);
        protected static readonly IIcon ICON_ARROW = new IconArrowRight(ColorTheme.Type.White);
        protected static readonly IIcon ICON_COLLAPSED = new IconChevronLeft(ColorTheme.Type.White);
        protected static readonly IIcon ICON_EXPANDED = new IconChevronDown(ColorTheme.Type.White);

        protected Image icon = new Image();

        public virtual Texture2D DefaultIcon => ICON_NONE.Texture;
        
        public virtual string DefaultIconName => null;

        public Image Icon => icon;

        protected Label counter;

        public void SetIcon(Texture iconTexture)
        {
            icon.image = iconTexture;
        }

        protected Texture2D GetIcon(SerializedProperty property)
        {
            var fieldType = TypeUtils.GetTypeFromProperty(property, true);
            var iconAttribute = fieldType?
                .GetCustomAttributes<ImageAttribute>()
                .FirstOrDefault();

            return iconAttribute != null ? iconAttribute.Image : Texture2D.whiteTexture;
        }

        public override void Enable()
        {
            UpdateExecutionStateView();
            var node = (BaseGameCreatorNode) this.nodeTarget;
            // if(EditorApplication.isPlaying)
            {
                node.EventStartRunning -= StartRunning;
                node.EventStopRunning -= StopRunning;
                
                node.EventStartRunning += StartRunning;
                node.EventStopRunning += StopRunning;
            }
            owner.graph.onGraphChanges += UpdateVisuals;
            SetIcon(DefaultIcon);
            icon.AddToClassList("node-icon");
            titleContainer.Insert(0, icon);
            UpdateIcon();
            UpdateName();
            InjectCustomStyle();

            if (node.hideControls) return;
            
            var expandIcon = new Image
            {
                image = ICON_COLLAPSED.Texture,
            };
            expandIcon.AddToClassList("expand-icon");
            var expandButton = new Button(() =>
            {
                node.showControls = !node.showControls;
                expandIcon.image = node.showControls ? ICON_EXPANDED.Texture : ICON_COLLAPSED.Texture;
                controlsContainer.style.display = node.showControls ? DisplayStyle.Flex : DisplayStyle.None;
                if(node.showControls)
                {
                    if(owner.selection.Contains(this)) owner.RemoveFromSelection(this);
                    owner.AddToSelection(this);
                }
                else if(owner.selection.Contains(this))
                {
                    owner.RemoveFromSelection(this);
                }
            });
            expandButton.Add(expandIcon);
            expandButton.AddToClassList("expand-button");
            titleButtonContainer.Add(expandButton);

            controlsContainer.style.display = node.showControls ? DisplayStyle.Flex : DisplayStyle.None;
            DrawDefaultInspector();
        }

        private void UpdateVisuals(GraphChanges obj)
        {
            UpdateIcon();
            UpdateName();
            Update();
        }

        public override void Disable()
        {
            base.Disable();
            
            var node = (BaseGameCreatorNode) nodeTarget;
            node.EventStartRunning -= StartRunning;
            node.EventStopRunning -= StopRunning;
        }

        public void Reset()
        {
            var node = (BaseGameCreatorNode) nodeTarget;
            node.Reset();
            node.EventStartRunning -= StartRunning;
            node.EventStopRunning -= StopRunning;
                
            node.EventStartRunning += StartRunning;
            node.EventStopRunning += StopRunning;
        }

        private void StartRunning(GameObject context)
        {
            owner.eventQueue.AddEvent(() => Highlight(context));
        }

        private void StopRunning(GameObject context, bool runningResult)
        {
            owner.eventQueue.AddEvent(() => UnHighlight(context, runningResult));
        }

        protected void InjectCustomStyle()
        {
            var border = this.Q("node-border");
            var overflowStyle = border.style.overflow;
            overflowStyle.value = Overflow.Visible;
            border.style.overflow = overflowStyle;

            // var selectionBorder = this.Q("selection-border");
            // selectionBorder.SendToBack();
        }

        protected Texture2D GetIcon(string typeName)
        {
            var fieldType = TypeUtils.GetTypeFromName(typeName);
            return fieldType?.GetCustomAttributes<ImageAttribute>().FirstOrDefault()?.Image; 
        }
        
        protected string GetName(string typeName)
        {
            if(string.IsNullOrEmpty(typeName)) return default;
            var fieldType = TypeUtils.GetTypeFromName(typeName);
            return fieldType?.GetCustomAttributes<TitleAttribute>().FirstOrDefault()?.Title;
        }
        
        public virtual void UpdateIcon()
        {
            var nodeIcon = DefaultIcon;
            if (!string.IsNullOrEmpty(DefaultIconName))
            {
                var newIcon = GetIcon(DefaultIconName);
                if (newIcon != null) nodeIcon = newIcon;
            }
            SetIcon(nodeIcon);
        }
        
        protected virtual void UpdateName()
        {
            if(nodeTarget is ExitNode or StartNode) return;
            if(!string.IsNullOrEmpty(nodeTarget.nodeCustomName)) return;
            var newName = GetName(DefaultIconName);
            switch (nodeTarget)
            {
                case StateMachineNode node:
                    newName = node.stateMachine == null ? "State Machine" : node.stateMachine.name;
                    break;
                case ActionsNode:
                    if(string.IsNullOrEmpty(newName)) newName = "Actions";
                    break;
                case ConditionsNode:
                    if(string.IsNullOrEmpty(newName)) newName = "Conditions";
                    break;
                case BranchNode:
                    if(string.IsNullOrEmpty(newName)) newName = "Branch";
                    break;
                case TriggerNode:
                    if(string.IsNullOrEmpty(newName)) newName = "Trigger";
                    break;
            }

            if(string.IsNullOrEmpty(newName)) return;
            title = newName;
        }

        public virtual void Update()
        {
            
        }
        
        protected void AddCounter(int count)
        {
            counter = new Label();
            counter.AddToClassList("counter");
            counter.text = count.ToString();
            titleContainer.Add(counter);
        }
        
        protected void UpdateCounter(int count)
        {
            counter.text = count.ToString();
        }
    }
}