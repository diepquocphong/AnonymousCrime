using GameCreator.Editor.Common;
using GameCreator.Tools.Runtime.Footsteps;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameCreator.Tools.Editor.Footsteps
{
    [CustomEditor(typeof(FootstepsGizmos))]
    public class FootstepsGizmosEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();

            StyleSheet[] styleSheets = StyleSheetUtils.Load();
            foreach (StyleSheet styleSheet in styleSheets) root.styleSheets.Add(styleSheet);

            Button button = new Button(SelectFootsteps)
            {
                text = "Edit Footsteps",
                style = { height = new Length(25f, LengthUnit.Pixel) }
            };
            
            root.Add(new SpaceSmaller());
            root.Add(button);

            return root;
        }

        private static void SelectFootsteps()
        {
            SettingsWindow.OpenWindow(FootstepRepository.REPOSITORY_ID);
        }
    }
}
