using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using UnityEngine.UI;

namespace Niam.Runtime.Tactile
{
    [Title("Fill Amount")]
    [Category("UI/Fill Amount")]
    [Description("Gets the fill amount of the given Image")]
    
    [Image(typeof(IconUIImage), ColorTheme.Type.Gray)]
    [Keywords("UI", "Progress", "Loading", "Image")]
    
    [Serializable]
    public class GetDecimalUIImageFillAmount : PropertyTypeGetDecimal
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_Image = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_Image} Fill Amount";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            var image = this.m_Image.Get<Image>(args);
            return image != null ? image.fillAmount : 0f;
        }

        public override double Get(GameObject gameObject)
        {
            var image = this.m_Image.Get<Image>(gameObject);
            return image != null ? image.fillAmount : 0f;
        }

    }
}