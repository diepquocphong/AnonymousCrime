using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using System.Collections.Generic;
using GameCreator.Editor.Common;
using Object = UnityEngine.Object;

namespace NinjutsuGames.StateMachine.Editor
{
    /// <summary>
    /// Custom editor of the node inspector, you can inherit from this class to customize your node inspector.
    /// </summary>
    [CustomEditor(typeof(NodeInspector))]
    public class NodeInspectorEditor : UnityEditor.Editor
    {
        private NodeInspector inspector;
        private VisualElement root;
        private VisualElement selectedNodeList;
        private VisualElement placeholder;
        private const string INFO_MESSAGE = "Select a node to show it's settings in the inspector";

        protected virtual void OnEnable()
        {
            inspector = target as NodeInspector;
            inspector.nodeSelectionUpdated += UpdateNodeInspectorList;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            root = new VisualElement();
            selectedNodeList = new VisualElement();
            var styleSheet = Resources.Load<StyleSheet>("GraphProcessorStyles/InspectorView");
            if(styleSheet != null) selectedNodeList.styleSheets.Add(styleSheet);
            root.Add(selectedNodeList);
            placeholder = new InfoMessage(INFO_MESSAGE);
            placeholder.AddToClassList("PlaceHolder");
            UpdateNodeInspectorList();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange mode)
        {
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                UpdateNodeInspectorList();
            }
        }

        protected override void OnHeaderGUI()
        {
            // base.OnHeaderGUI();
        }

        protected virtual void OnDisable()
        {
            inspector.nodeSelectionUpdated -= UpdateNodeInspectorList;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        public override VisualElement CreateInspectorGUI() => root;

        protected virtual void UpdateNodeInspectorList()
        {
            selectedNodeList.Clear();

            if (inspector.selectedNodes.Count == 0)
            {
                selectedNodeList.Add(placeholder);
            }

            foreach (var nodeView in inspector.selectedNodes)
            {
                var block = CreateNodeBlock(nodeView);
                selectedNodeList.Add(block);
                
                if (nodeView is StartNodeView or ExitNodeView)
                {
                    var description = new InfoMessage(nodeView is StartNodeView ? StartNodeView.INFO_MESSAGE : ExitNodeView.INFO_MESSAGE);
                    EditorApplication.delayCall += () =>
                    {
                        block.ElementAt(1).Add(description);
                    };
                }
                /*if (nodeView is not TriggerNodeView triggerNode) continue;
                
                var documentation = new DocumentationStateMachine((triggerNode.nodeTarget as TriggerNode)?.triggerEvent.GetType());
                EditorApplication.delayCall += () =>
                {
                    block.ElementAt(1).Add(documentation);
                };
                
                triggerNode.OnUpdated += () =>
                {
                    documentation.Update((triggerNode.nodeTarget as TriggerNode)?.triggerEvent.GetType());
                };*/
            }
        }

        protected VisualElement CreateNodeBlock(BaseNodeView nodeView)
        {
            var view = new VisualElement();

            if(nodeView is BaseGameCreatorNodeView gcNodeView)
            {
                var header = new VisualElement();
                header.name = "Header";
                header.AddToClassList("Header");
                var icon = new Image
                {
                    image = gcNodeView.DefaultIcon,
                    style =
                    {
                        marginLeft = 0,
                        marginTop = 0,
                        marginBottom = 10,
                        width = 20,
                        height = 20
                    }
                };
                header.Add(icon);
                // var state = gcNodeView.nodeTarget.enabledForExecution ? string.Empty : "(Disabled)";
                var label = new Label(string.IsNullOrEmpty(nodeView.nodeTarget.nodeCustomName) ? nodeView.nodeTarget.name : nodeView.nodeTarget.nodeCustomName);
                header.Add(label);
                
                if (nodeView is TriggerNodeView triggerNode)
                {
                    var helpIcon = new Image
                    {
                        image = BaseNodeView.ICON_HELP.Texture,
                    };
                    var helpButton = new Button(triggerNode.ShowHelp);
                    helpButton.Add(helpIcon);
                    helpButton.AddToClassList("Help");
                    header.Add(helpButton);
                }
                
                if (gcNodeView.nodeTarget.isLocked)
                {
                    var lockIcon = new Image
                    {
                        image = BaseNodeView.ICON_LOCK.Texture,
                    };
                    var lockButton = new Button(nodeView.ChangeLockStatus);
                    lockButton.Add(lockIcon);
                    lockButton.AddToClassList("Locked");
                    header.Add(lockButton);
                }
                
                if (!gcNodeView.nodeTarget.enabledForExecution)
                {
                    var disabledIcon = new Image
                    {
                        image = BaseNodeView.ICON_DISABLE.Texture,
                    };
                    var disabledButton = new Button(nodeView.ToggleExecutionState);
                    disabledButton.Add(disabledIcon);
                    disabledButton.AddToClassList("Disabled");
                    header.Add(disabledButton);
                }
                
                view.Add(header);
            }
            else view.Add(new Label(nodeView.nodeTarget.name));

            var tmp = nodeView.controlsContainer;
            nodeView.controlsContainer = view;
            nodeView.Enable(true);
            nodeView.controlsContainer.AddToClassList("NodeControls");
            var block = nodeView.controlsContainer;
            nodeView.controlsContainer = tmp;
            return block;
        }
    }

    /// <summary>
    /// Node inspector object, you can inherit from this class to customize your node inspector.
    /// </summary>
    public class NodeInspector : ScriptableObject
    {
        /// <summary>Previously selected object by the inspector</summary>
        public Object previouslySelectedObject;

        /// <summary>List of currently selected nodes</summary>
        public HashSet<BaseNodeView> selectedNodes { get; private set; } = new HashSet<BaseNodeView>();

        /// <summary>Triggered when the selection is updated</summary>
        public event Action nodeSelectionUpdated;

        /// <summary>Updates the selection from the graph</summary>
        public virtual void UpdateSelectedNodes(HashSet<BaseNodeView> views)
        {
            selectedNodes = views;
            nodeSelectionUpdated?.Invoke();
        }

        public virtual void RefreshNodes() => nodeSelectionUpdated?.Invoke();

        public virtual void NodeViewRemoved(BaseNodeView view)
        {
            selectedNodes.Remove(view);
            nodeSelectionUpdated?.Invoke();
        }
    }
}