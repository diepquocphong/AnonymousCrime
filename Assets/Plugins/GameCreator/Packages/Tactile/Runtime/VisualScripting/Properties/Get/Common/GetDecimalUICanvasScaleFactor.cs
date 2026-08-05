using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using UnityEngine.UI;

namespace Niam.Runtime.Tactile
{
    [Title("Scale Factor")]
    [Category("UI/Scale Factor")]
    [Description("Gets the scale factor of a Canvas")]
    
    [Image(typeof(IconUICanvas), ColorTheme.Type.Gray)]
    [Keywords("UI", "Progress", "Loading", "Image")]
    
    [Serializable]
    public class GetDecimalUICanvasScaleFactor : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_Canvas = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_Canvas} Scale Factor";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            var canvas = this.m_Canvas.Get<Canvas>(args);
            return canvas != null ? canvas.scaleFactor : 1f;
        }

        public override double Get(GameObject gameObject)
        {
            var canvas = this.m_Canvas.Get<Canvas>(gameObject);
            return canvas != null ? canvas.scaleFactor : 1f;
        }

    }
}