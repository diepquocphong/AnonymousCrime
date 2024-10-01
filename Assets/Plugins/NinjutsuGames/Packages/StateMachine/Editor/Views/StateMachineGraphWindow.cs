using GameCreator.Editor.Hub;
using GameCreator.Runtime.Common;
using NinjutsuGames.StateMachine.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NinjutsuGames.StateMachine.Editor
{

	public class StateMachineGraphWindow : BaseGraphWindow
	{
		private const int MIN_WIDTH = 800;
		private const int MIN_HEIGHT = 600;
		private static IIcon icon;

		private CustomToolbarView toolbarView;
		private MiniMapView minimap;
		
		private Label titleLabel;
		private Label pathLabel;

		protected override void OnDestroy()
		{
			base.OnDestroy();
			graphView?.Dispose();
		}

		protected override void InitializeWindow(StateMachineAsset graph)
		{
			icon ??= new IconWindowHub(ColorTheme.Type.TextLight);
			titleContent = new GUIContent("State Machine", icon.Texture);
			minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);

			if (graphView == null)
			{
				graphView = new StateMachineGraphView(this);
				graphView.initialized += UpdateInfo;
				minimap = new MiniMapView(graphView)
				{
					anchored = true,
					style =
					{
						backgroundColor = Color.clear,
						borderBottomColor = Color.clear,
						borderLeftColor = Color.clear,
						borderRightColor = Color.clear,
						borderTopColor = Color.clear,
					}
				};
				minimap.SetPosition(new Rect(1, 20, 200, 100));
				minimap.UpdatePresenterPosition();
				graphView.Add(minimap);
				
				toolbarView = new CustomToolbarView(graphView, minimap);
				graphView.Add(toolbarView);
				
				var grid = new GridBackground();
				graphView.Insert(0, grid);
				grid.StretchToParentSize();
			}
			graphView.graph = graph;
			rootView.Add(graphView);
		}
		
		private void UpdateInfo()
		{
			if(titleLabel == null)
			{
				var root = new VisualElement();
				root.name = "StateMachineInfo";
				titleLabel = new Label();
				titleLabel.AddToClassList("StateMachineTitle");
				pathLabel = new Label();
				pathLabel.AddToClassList("StateMachinePath");
				root.Add(titleLabel);
				root.Add(pathLabel);
				graphView.Insert(1, root);
				root.pickingMode = PickingMode.Ignore;
				root.StretchToParentSize();
			}

			var path = string.Empty;
			if (StateMachineAsset.Active)
			{
				path = AssetDatabase.GetAssetPath(StateMachineAsset.Active);
				if (string.IsNullOrEmpty(path))
				{
					path = SceneManager.GetActiveScene().path;
				}
			}
			titleLabel.text = StateMachineAsset.Active ? StateMachineAsset.Active.name : "Select a State Machine";
			pathLabel.text = path;
		}

		protected override void InitializeGraphView(BaseGraphView view)
		{
			toolbarView.UpdateButtonStatus();
		}
	}
}
