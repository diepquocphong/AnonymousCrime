using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[Version(2, 0, 1)]

[Title(" Change Image Fill Amount")]
[Description("Only for image components of type FILLED")]

[Category("UI/Change Image Fill Amount")]

[Keywords( "Image", "Fill", "Amount", "Change")]
[Image(typeof(IconUIImage), ColorTheme.Type.TextLight)]

[Parameter("Image", "The GameObject with the Image Component")]
[Parameter("Value", "The target value you want the Fill Amount to be Set")]

[Serializable]
public class InstructionUIChangeImageFillAmount : Instruction
{
    // MEMBERS: -------------------------------------------------------------------------------
    [SerializeField] private PropertyGetGameObject m_Image = GetGameObjectInstance.Create();
    [SerializeField] private PropertyGetDecimal m_Value = new PropertyGetDecimal();
    
    [SerializeField] private Transition m_Transition = new Transition();
    
    // PROPERTIES: ----------------------------------------------------------------------------

    public override string Title => $"Change {this.m_Image} Fill Amount to {this.m_Value}";
	
    // RUN METHOD: ----------------------------------------------------------------------------
    protected override async Task Run(Args args)
    {
        GameObject gameObject = this.m_Image.Get(args);
        double value = this.m_Value.Get(args);
        if (gameObject == null) return;
        
        Image image = gameObject.Get<Image>();
        if (image == null) return;

        if (image.type != Image.Type.Filled)
            return;
        
        // image.fillAmount = (float)value;
        
        float valueSource = image.fillAmount;
        float valueTarget = (float) value;
        
        ITweenInput tween = new TweenInput<float>(
            valueSource,
            valueTarget,
            this.m_Transition.Duration,
            (a, b, t) => image.fillAmount = Mathf.LerpUnclamped(a, b, t),
            Tween.GetHash(typeof(Transform), "fillAmount"),
            this.m_Transition.EasingType,
            this.m_Transition.Time
        );
            
        Tween.To(gameObject, tween);
        if (this.m_Transition.WaitToComplete) await this.Until(() => tween.IsFinished);

    }
}
