using UnityEditor;
using UnityEngine.UIElements;
using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    internal class InputControlTypeSelector : TypeSelectorListFancy
    {
        // CONSTRUCTOR: ---------------------------------------------------------------------------

        public InputControlTypeSelector(SerializedProperty propertyList, Button element)
            : base(propertyList, typeof(TInputControl), element)
        { }

        protected override void SetupActivator(Button element)
        {
            element.clicked += () => { 
                TypeSelectorFancyPopup.Open(
                    element, typeof(TInputControl), this.OnSelectType
                );
            };
        }
    }
}