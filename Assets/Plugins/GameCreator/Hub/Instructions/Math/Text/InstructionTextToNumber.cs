using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

[Version(1, 0, 0)]

[Title("Text To Number")]
[Description("Converts text value to number value")]
[Category("Math/Text/Text To Number")]

[Parameter("Text", "The string value to convert")]
[Parameter("Number", "The numeric object to set the result to")]

[Keywords("Text", "String", "Number", "Convert")]
[Image(typeof(IconArrowCircleDown), ColorTheme.Type.Yellow)]

[Serializable]
public class InstructionTextToNumber : Instruction
{

    public PropertyGetString m_Text = new PropertyGetString();
    public PropertySetNumber m_Number = new PropertySetNumber();

    public override string Title => $"Convert text {this.m_Text} to number {this.m_Number}";

    protected override Task Run(Args args)
    {
        bool result = double.TryParse(this.m_Text.Get(args), out var number);
        if (result == true)
        {
            this.m_Number.Set(number, args);
        }
        else
        {
            Debug.LogError(string.Format("Instruction Text To Number: Conversion error of value {0}. Result is set to 0", this.m_Text.Get(args)));
            this.m_Number.Set(0, args);
        }
        return DefaultResult;
    }
}
