using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using UnityEngine.UI;

namespace Niam.Runtime.Tactile
{
    [Title("Fill Amount")]
    [Category("UI/Fill Amount")]
    [Description("Sets the fill amount of the given Image")]
    
    [Image(typeof(IconUIImage), ColorTheme.Type.Gray)]
    [Keywords("UI", "Progress", "Loading", "Image")]
    
    [Serializable]
    public class SetDecimalUIImageFillAmount : PropertyTypeSetNumber
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        private PropertyGetGameObject m_Image = GetGameObjectInstance.Create();

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string String => $"{this.m_Image} Fill Amount";

        // GETTERS: -------------------------------------------------------------------------------

        public override double Get(Args args)
        {
            Image control = this.m_Image.Get<Image>(args);
            return control != null ? control.fillAmount : 0f;
        }

        public override double Get(GameObject gameObject)
        {
            Image control = this.m_Image.Get<Image>(gameObject);
            return control != null ? control.fillAmount : 0f;
        }

        // SETTERS: -------------------------------------------------------------------------------

        public override void Set(double value, Args args)
        {
            Image control = this.m_Image.Get<Image>(args);
            if (control == null) return;

            control.fillAmount = (float) value;
        }

        public override void Set(double value, GameObject gameObject)
        {
            Image control = this.m_Image.Get<Image>(gameObject);
            if (control == null) return;

            control.fillAmount = (float) value;
        }

    }
}