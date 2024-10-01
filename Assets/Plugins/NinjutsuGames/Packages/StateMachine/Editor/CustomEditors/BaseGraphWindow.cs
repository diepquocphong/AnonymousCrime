using System;
using System.Linq;
using NinjutsuGames.StateMachine.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NinjutsuGames.StateMachine.Editor
{
    [Serializable]
    public abstract class BaseGraphWindow : EditorWindow
    {
        protected VisualElement rootView;
        protected VisualElement emptyView;
        protected BaseGraphView graphView;

        [SerializeField] protected StateMachineAsset graph;

        private readonly string graphWindowStyle = "GraphProcessorStyles/BaseGraphView";

        public bool IsGraphLoaded => graphView != null && graphView.graph != null;

        private bool reloadWorkaround;

        public event Action<StateMachineAsset> graphLoaded;
        public event Action<StateMachineAsset> graphUnloaded;

        /// <summary>
        /// Called by Unity when the window is enabled / opened
        /// </summary>
        protected virtual void OnEnable()
        {
            InitializeRootView();

            if (graph != null)
                LoadGraph();
            else
                reloadWorkaround = true;
        }

        protected virtual void Update()
        {
            // Workaround for the Refresh option of the editor window:
            // When Refresh is clicked, OnEnable is called before the serialized data in the
            // editor window is deserialized, causing the graph view to not be loaded
            if (reloadWorkaround && graph != null)
            {
                LoadGraph();
                reloadWorkaround = false;
            }
        }

        private void LoadGraph()
        {
            // We wait for the graph to be initialized
            if (graph.isEnabled) InitializeGraph(graph);
            else graph.onEnabled += () => InitializeGraph(graph);
        }

        /// <summary>
        /// Called by Unity when the window is disabled (happens on domain reload)
        /// </summary>
        protected virtual void OnDisable()
        {
            if (graph != null && graphView != null) graphView.SaveGraphToDisk();
        }

        /// <summary>
        /// Called by Unity when the window is closed
        /// </summary>
        protected virtual void OnDestroy()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnFocus()
        {
            if(graph != null)
            {
                StateMachineAsset.Active = graph;
            }
        }

        private void InitializeRootView()
        {
            rootView = rootVisualElement;
            rootView.name = "graphRootView";
            rootView.styleSheets.Add(Resources.Load<StyleSheet>(graphWindowStyle));
            emptyView = new VisualElement
            {
                name = "emptyView",
                style =
                {
                    flexGrow = 1
                }
            };
            var titleLabel = new Label("Select a State Machine");
            titleLabel.AddToClassList("state-machine-empty");
            emptyView.Add(titleLabel);
            rootView.Add(emptyView);
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (!Selection.activeGameObject) return;
            var runner = Selection.activeGameObject.GetComponent<StateMachineRunner>();
            if(runner && runner.stateMachineAsset) InitializeGraph(runner.stateMachineAsset);
        }

        public void InitializeGraph(StateMachineAsset graph)
        {
            if (this.graph != null && graph != this.graph)
            {
                // Save the graph to the disk
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssets();
                // Unload the graph
                graphUnloaded?.Invoke(graph);
            }

            this.graph = graph;
            if (graph != null)
            {
                StateMachineAsset.Active = graph;
            }

            graphLoaded?.Invoke(graph);

            if (graphView != null) rootView.Remove(graphView);

            //Initialize will provide the BaseGraphView
            InitializeWindow(graph);

            graphView = rootView.Children().FirstOrDefault(e => e is BaseGraphView) as BaseGraphView;

            if (graphView == null)
            {
                Debug.LogError("GraphView has not been added to the BaseGraph root view !");
                return;
            }

            graphView.Initialize(graph);

            InitializeGraphView(graphView);

            if (graph.IsLinkedToScene()) LinkGraphWindowToScene(graph.GetLinkedScene());
            else graph.onSceneLinked += LinkGraphWindowToScene;
            return;

            void LinkGraphWindowToScene(Scene scene)
            {
                EditorSceneManager.sceneClosed += CloseWindowWhenSceneIsClosed;
                return;

                void CloseWindowWhenSceneIsClosed(Scene closedScene)
                {
                    EditorApplication.delayCall += ShouldClose;
                    return;
                    void ShouldClose()
                    {
                        EditorApplication.delayCall -= ShouldClose;
                        
                        // Find StateMachineRunner in current scene
                        var stateMachineRunner = FindFirstObjectByType<StateMachineRunner>();
                        if (stateMachineRunner && graph == null )
                        {
                            graph = stateMachineRunner.stateMachineAsset;
                        }

                        if (scene.Equals(SceneManager.GetActiveScene()))
                        {
                            InitializeGraph(graph);
                            return;
                        }
                    
                        // Close();
                        Unload();
                        EditorSceneManager.sceneClosed -= CloseWindowWhenSceneIsClosed;
                    }
                }
            }
        }
        
        private void Unload()
        {
            if(graphView != null) rootView.Remove(graphView);
            graphView = null;
        }

        public virtual void OnGraphDeleted()
        {
            if (graph != null && graphView != null)
                rootView.Remove(graphView);

            graphView = null;
        }

        protected abstract void InitializeWindow(StateMachineAsset graph);

        protected virtual void InitializeGraphView(BaseGraphView view)
        {
        }
    }
}