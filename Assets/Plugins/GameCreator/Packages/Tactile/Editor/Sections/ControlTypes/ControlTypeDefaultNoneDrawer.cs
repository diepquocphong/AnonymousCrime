using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

using GameCreator.Editor.Common;
using Niam.Runtime.Tactile;

namespace Niam.Editor.Tactile 
{
    [CustomPropertyDrawer(typeof(ControlTypeDefaultNone))]
    public class ControlTypeDefaultNoneDrawer : TControlTypeDrawer
    {
        protected override void BuildBody(VisualElement body, SerializedProperty property)
        {
            var message = new InfoMessage("Nothing to see here...")
            {
                style = {
                    backgroundColor = new Color(1f, 1f, 1f, 0f),

                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,

                    marginTop = 0,
                    marginBottom = 0,
                    marginLeft = 0,
                    marginRight = 0,

                    paddingTop = 0,
                    paddingBottom = 0,
                }
            };

            var image = message.Q<Image>();
            if (image != null)
            {
                image.style.width = 18f;
                image.style.height = 18f;
            }

            body.Add(message);
        }

    }
}