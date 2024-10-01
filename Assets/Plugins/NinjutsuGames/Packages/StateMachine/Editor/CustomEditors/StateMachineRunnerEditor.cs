using System;
using System.Linq;
using GameCreator.Editor.Common;
using GameCreator.Editor.Variables;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Variables;
using NinjutsuGames.StateMachine.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NinjutsuGames.StateMachine.Editor
{
    public class NameListView : TListView<Runtime.NameVariableRuntime>
    {
        private const string USS_PATH = EditorPaths.VARIABLES + "StyleSheets/NameList";

        // PROPERTIES: ----------------------------------------------------------------------------

        protected override string USSPath => USS_PATH;

        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public NameListView(Runtime.NameVariableRuntime runtime) : base(runtime)
        {
            runtime.EventChange += OnChange;
        }

        private void OnChange(string name)
        {
            Refresh();
        }

        // IMPLEMENTATIONS: -----------------------------------------------------------------------

        protected override void Refresh()
        {
            base.Refresh();
            if (m_Runtime?.GetEnumerator() == null) return;

            foreach (var variable in m_Runtime)
            {
                Add(new NameVariableView(variable));
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.NameVariableRuntime))]
    public class NameVariablesRuntimeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            var list = property.FindPropertyRelative("m_List");
            var runtime = property.GetValue<Runtime.NameVariableRuntime>();

            var target = property.serializedObject.targetObject;
            var isPrefab = PrefabUtility.IsPartOfPrefabAsset(target);
            switch (EditorApplication.isPlayingOrWillChangePlaymode && !isPrefab)
            {
                case true:
                    root.Add(new NameListView(runtime));
                    break;

                case false:
                    root.Add(new RunnerNameListTool(list));
                    break;
            }

            return root;
        }
    }

    [CustomEditor(typeof(StateMachineRunner))]
    public class StateMachineRunnerEditor : UnityEditor.Editor
    {
        private const string USS_PATH = EditorPaths.COMMON + "Structures/Save/StyleSheets/Save";
        public const string CLASS_HEAD = "gc-save-root";
        public const string NAME_HEAD = "GC-ListName-List-Head";
        private const float BUTTON_WIDTH = 70;

        private static readonly Length ERROR_MARGIN = new Length(10, LengthUnit.Pixel);
        private const string ERR_DUPLICATE_ID = "Another Variable component has the same ID";
        private const string CLASS_HEAD_BUTTON = "gc-save-btn";
        private static IIcon ICON_Embed;
        private static IIcon ICON_Detach;
        private static IIcon ICON_Open;
        private static IIcon ICON_OpenNew;
        private static IIcon ICON_Delete;
        
        // MEMBERS: -------------------------------------------------------------------------------

        private ErrorMessage m_Error;
        private StateMachineRunner runner;
        private RunnerNameListTool fieldRunnerList;
        private SerializedProperty propertyList;
        private StateMachineAsset _lastAsset;
        private Button embedButton;
        private Image embedIcon;
        private Label embedLabel;
        private Button deleteButton;
        private PropertyField fieldGraph;
        private VisualElement root;
        private IVisualElementScheduledItem _changeSchedule;

        public static StateMachineRunnerEditor Instance { get; private set; }
        public static Action OnListChanged;

        // PAINT METHOD: --------------------------------------------------------------------------

        public override VisualElement CreateInspectorGUI()
        {
            Instance = this;
            runner = target as StateMachineRunner;

            root = new VisualElement
            {
                style =
                {
                    marginTop = new StyleLength(5)
                }
            };
            
            var sheets = StyleSheetUtils.Load(USS_PATH);
            foreach (var styleSheet in sheets) root.styleSheets.Add(styleSheet);

            var graph = serializedObject.FindProperty("stateMachineAsset");
            var runtime = serializedObject.FindProperty("m_Runtime");
            var saveUniqueID = serializedObject.FindProperty("m_SaveUniqueID");

            fieldGraph = new PropertyField(graph);

            fieldRunnerList = new RunnerNameListTool(runtime.FindPropertyRelative("m_List"));
            propertyList = runtime.FindPropertyRelative("m_List").FindPropertyRelative("m_Source");
            
            _lastAsset = runner.stateMachineAsset;

            var fieldRuntime = new PropertyField(runtime);
            var fieldSaveUniqueID = new PropertyField(saveUniqueID);
            m_Error = new ErrorMessage(ERR_DUPLICATE_ID)
            {
                style = {marginTop = ERROR_MARGIN}
            };

            var head = new VisualElement
            {
                name = NAME_HEAD,
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center
                }
            };
            // head.AddToClassList(CLASS_HEAD);
            var left = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexStart
                }
            };
            var right = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd
                }
            };
            head.Add(left);
            head.Add(right);
            
            ICON_Open ??= new IconEdit(ColorTheme.Type.TextLight);
            ICON_OpenNew ??= new IconHotspot(ColorTheme.Type.TextLight);
            ICON_Embed ??= new IconCubeSolid(ColorTheme.Type.TextLight);
            ICON_Detach ??= new IconCubeOutline(ColorTheme.Type.TextLight);
            ICON_Delete ??= new IconCancel(ColorTheme.Type.TextLight);
            
            var openButton = new Button(() =>
            {
                var graphAsset = runner.stateMachineAsset;
                if (graphAsset == null) return;
                var window = EditorWindow.GetWindow<StateMachineGraphWindow>();
                window.InitializeGraph(graphAsset);
            })
            {
                tooltip = "Open State Machine",
            };
            openButton.AddToClassList(CLASS_HEAD_BUTTON);
            var image2 = new Image { image = ICON_Open.Texture };
            var label2 = new Label("Open");
            image2.AddToClassList("gc-save-image");
            label2.AddToClassList("gc-save-label");
            openButton.Add(image2);
            // openButton.Add(label2);
            // openButton.style.width = new StyleLength(60f);
            
            var openNewButton = new Button(() =>
            {
                var graphAsset = runner.stateMachineAsset;
                if (graphAsset == null) return;
                var window = EditorWindow.CreateWindow<StateMachineGraphWindow>();
                window.InitializeGraph(graphAsset);
            })
            {
                tooltip = "Open in a new window",
            };
            openNewButton.AddToClassList(CLASS_HEAD_BUTTON);
            var image = new Image { image = ICON_OpenNew.Texture };
            var label = new Label("New Window");
            image.AddToClassList("gc-save-image");
            label.AddToClassList("gc-save-label");
            openNewButton.Add(image);
            // openNewButton.Add(label);
            // openNewButton.style.width = new StyleLength(110f);

            embedButton = new Button(ToggleEmbed)
            {
                tooltip = runner.isEmbedded ? "Detach State Machine Asset" : "Embed State Machine Asset",
                style =
                {
                    width = new StyleLength(BUTTON_WIDTH)
                }
            };
            embedIcon = new Image { image = runner.isEmbedded ? ICON_Detach.Texture : ICON_Embed.Texture };
            embedIcon.AddToClassList("gc-save-image");
            embedLabel = new Label(runner.isEmbedded ? "Detach" : "Embed");
            embedLabel.AddToClassList("gc-save-label");
            embedButton.AddToClassList(CLASS_HEAD_BUTTON);
            embedButton.Add(embedIcon);
            embedButton.Add(embedLabel);

            deleteButton = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Clear embedded State Machine",
                        "Are you sure you want to clear the embedded data? This action is permanent.", "Yes", "No")) return;
                runner.stateMachineAsset = runner.originalAsset;
                runner.originalAsset = null;
                runner.cloneAsset = null;
                runner.isEmbedded = false;
                DestroyImmediate(runner.cloneAsset);
                UpdateVisualState();
                SetActiveStateMachine();
            })
            {
                tooltip = "Clear embedded State Machine data",
                style =
                {
                    width = new StyleLength(60f)
                }
            };

            var deleteIcon = new Image { image = ICON_Delete.Texture };
            deleteIcon.AddToClassList("gc-save-image");
            deleteButton.AddToClassList(CLASS_HEAD_BUTTON);
            deleteButton.Add(deleteIcon);
            var deleteLabel = new Label("Clear");
            deleteLabel.AddToClassList("gc-save-label");
            deleteButton.Add(deleteLabel);
            
            UpdateVisualState();

            left.Add(openButton);
            left.Add(openNewButton);
            
            right.Add(deleteButton);
            right.Add(embedButton);
            
            root.Add(fieldGraph);
            root.Add(new SpaceSmall());
            root.Add(head);
            root.Add(new SpaceSmall());

            root.RegisterCallback<SerializedPropertyChangeEvent>(evt => OnChanged(0));
            
            SyncVariables(true);

            switch (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                case true:
                    root.Add(fieldRuntime);
                    break;

                case false:
                    SyncVariables();
                    root.Add(fieldRunnerList);
                    fieldRunnerList.EventChangeSize -= OnChanged;
                    fieldRunnerList.EventChangeSize += OnChanged;
                    GraphInspector.OnListChanged += OnAssetVariableChange;
                    BlackboardView.OnListChanged += OnAssetVariableChange;
                    break;
            }
            
            root.Add(m_Error);
            root.Add(fieldSaveUniqueID);

            RefreshErrorID();
            
            fieldGraph.RegisterValueChangeCallback(OnGraphChange);
            fieldSaveUniqueID.RegisterValueChangeCallback(_ => { RefreshErrorID(); });
            return root;

            void ToggleEmbed()
            {
                if(!runner.isEmbedded) EmbedAsset();
                else DetachAsset();
            }
        }

        private void SetActiveStateMachine()
        {
            if(!runner.stateMachineAsset) return;
            StateMachineAsset.Active = runner.stateMachineAsset;
            var window = EditorWindow.GetWindow<StateMachineGraphWindow>();
            window.InitializeGraph(runner.stateMachineAsset);
        }

        private void OpenGraph()
        {
            root.schedule.Execute(SetActiveStateMachine).ExecuteLater(10);
        }

        private void UpdateVisualState()
        {
            fieldGraph.SetEnabled(!runner.isEmbedded);
            embedButton.tooltip =
                runner.isEmbedded ? "Detach State Machine Asset" : "Embed State Machine Asset";
            embedIcon.image = runner.isEmbedded ? ICON_Detach.Texture : ICON_Embed.Texture;
            embedLabel.text = runner.isEmbedded ? "Detach" : "Embed";
            deleteButton.SetEnabled(runner.cloneAsset != null || runner.isEmbedded);
        }

        private StateMachineAsset GetCopy()
        {
            StateMachineAsset newInstance = default;
            if (!runner.stateMachineAsset)
            {
                newInstance = CreateInstance<StateMachineAsset>();
                newInstance.name = $"{runner.gameObject.name} State Machine";
            }
            else
            {
                newInstance = Instantiate(runner.stateMachineAsset);
            }
            return newInstance;
        }
        

        private void EmbedAsset()
        {
            var newInstance = runner.cloneAsset ?? GetCopy();
            runner.originalAsset = runner.stateMachineAsset;
            runner.stateMachineAsset = newInstance;
            runner.stateMachineAsset.LinkToScene(runner.gameObject.scene);
            runner.isEmbedded = true;
            
            UpdateVisualState();
            OpenGraph();
        }
        
        private void DetachAsset()
        {
            runner.cloneAsset = runner.stateMachineAsset;
            runner.stateMachineAsset = runner.originalAsset;
            runner.isEmbedded = false;
            
            UpdateVisualState();
            OpenGraph();
        }

        private void OnDisable()
        {
            Instance = null;
            GraphInspector.OnListChanged -= OnAssetVariableChange;
            BlackboardView.OnListChanged -= OnAssetVariableChange;
        }

        private void OnAssetVariableChange()
        {
            if(fieldRunnerList == null) return;
            SyncVariables(true);
        }

        private void OnChanged(int size)
        {
            EditorApplication.delayCall += SyncStateMachineVariables;
            OnListChanged?.Invoke();
        }

        private void SyncStateMachineVariables()
        {
            if(!runner.stateMachineAsset) return;
            // Debug.Log($"SyncStateMachineVariables");
            runner.stateMachineAsset.SyncVariablesInternal(runner.Runtime.List);
            EditorApplication.delayCall -= SyncStateMachineVariables;
        }

        private void OnGraphChange(SerializedPropertyChangeEvent evt)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            
            if (runner.stateMachineAsset == _lastAsset) return;

            serializedObject.Update();
            fieldRunnerList.PropertyList.ClearArray();
            fieldRunnerList.Clear();
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (runner.stateMachineAsset != _lastAsset)
            {
                _lastAsset = runner.stateMachineAsset;
                SyncVariables();
                _changeSchedule?.Pause();
                _changeSchedule = root.schedule.Execute(SetActiveStateMachine);
                _changeSchedule.ExecuteLater(500);
            }

            fieldRunnerList.EventChangeSize -= OnChanged;
            fieldRunnerList.EventChangeSize += OnChanged;
        }

        private void SyncVariables(bool remove = false)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(fieldRunnerList == null) return;
            if(runner.stateMachineAsset == null) return;
            if(runner.stateMachineAsset.NameList == null) return;
            
            runner = target as StateMachineRunner;
            _lastAsset = runner.stateMachineAsset;
            
            serializedObject.Update();
            
            // Copy missing variables from the State Machine
            for (var i = 0; i < runner.stateMachineAsset.NameList.Names.Length; i++)
            {
                var nameVar = runner.stateMachineAsset.NameList.Names[i];
                var exists = runner.Runtime.List.Names.Any(v => v == nameVar);
                if(exists)
                {
                    if(i >= runner.Runtime.List.Names.Length) continue;
                    if(i >= runner.stateMachineAsset.NameList.Names.Length) continue;
                    var assetIndex = runner.stateMachineAsset.NameList.Names.ToList().IndexOf(nameVar);
                    var runnerIndex = runner.Runtime.List.Names.ToList().IndexOf(nameVar);
                    if (runner.stateMachineAsset.NameList.Get(assetIndex).Type != runner.Runtime.List.Get(runnerIndex).Type)
                    {
                        // Debug.LogWarning($"Variable {nameVar} has different type in State Machine and Runtime. From: {runner.Runtime.List.Get(runnerIndex).Type} to: {runner.stateMachineAsset.NameList.Get(assetIndex).Type}");
                        fieldRunnerList.DeleteItem(runnerIndex);
                        // runner.stateMachineAsset.NameList.Get(assetIndex).Value = runner.Runtime.List.Get(runnerIndex).Value;
                        // runner.Runtime.List.Get(runnerIndex).Value = runner.stateMachineAsset.NameList.Get(assetIndex).Value;
                    }
                    continue;
                }
                var value = runner.stateMachineAsset.NameList.Get(i);
                fieldRunnerList.InsertItem(propertyList.arraySize, value.Copy);
            }

            // Remove variables that doesn't exist in the State Machine
            if (remove)
            {
                var removeList = runner.Runtime.List.Names.Where(v => !runner.stateMachineAsset.NameList.Names.Contains(v));
                for (var i = 0; i < runner.Runtime.List.Names.Length; i++)
                {
                    var nameVar = runner.Runtime.List.Names[i];
                    if(removeList.Contains(nameVar))
                    {
                        // Debug.Log($"Removing variable that doesn't exist in State Machine {nameVar} with index {i}");
                        fieldRunnerList.DeleteItem(i);
                    }
                }
            }
            fieldRunnerList?.Refresh();
            serializedObject.Update();
            EditorUtility.SetDirty(runner);
        }

        private void RefreshErrorID()
        {
            serializedObject.Update();
            m_Error.style.display = DisplayStyle.None;

            if (PrefabUtility.IsPartOfPrefabAsset(target)) return;

            var saveUniqueID = serializedObject.FindProperty("m_SaveUniqueID");

            var itemID = saveUniqueID
                .FindPropertyRelative(SaveUniqueIDDrawer.PROP_UNIQUE_ID)
                .FindPropertyRelative(UniqueIDDrawer.SERIALIZED_ID)
                .FindPropertyRelative(IdStringDrawer.NAME_STRING)
                .stringValue;

            var variables = FindObjectsOfType<TLocalVariables>(true);
            foreach (var variable in variables)
            {
                if (variable.SaveID != itemID || variable == target) continue;
                m_Error.style.display = DisplayStyle.Flex;

                return;
            }
        }
    }
}