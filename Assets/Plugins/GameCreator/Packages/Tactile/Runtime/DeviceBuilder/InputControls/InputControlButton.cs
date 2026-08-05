using System;
using Builder = UnityEngine.InputSystem.Layouts.InputControlLayout.Builder;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace Niam.Runtime.Tactile
{
    [Title("Button")]
    [Category("Button")]
    [Image(typeof(IconPushButton), ColorTheme.Type.TextLight)]

    [Serializable]
    public class InputControlButton : TInputControl
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] protected string m_Name = "button";
        [SerializeField] protected string m_DisplayName = "Button";

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Name => this.m_Name;
        public override string DisplayName => this.m_DisplayName;
        public override string Layout => "Button";
        public override int ControlSizeInBits => 1;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public InputControlButton()
        { }

        public InputControlButton(string name, string displayName)
        { 
            this.m_Name = name;
            this.m_DisplayName = displayName;
        }

        // ADD CONTROL METHODS: -------------------------------------------------------------------

        public override void AddControlToBuilder(Builder builder, ref int byteOffset, ref int bitOffset)
        {
            builder.AddControl(this.Name)
                   .WithDisplayName(this.DisplayName)
                   .WithLayout(this.Layout)
                   .WithByteOffset((uint)byteOffset)
                   .WithBitOffset((uint)bitOffset);

            bitOffset += this.ControlSizeInBits;
            byteOffset += bitOffset / 8;
            bitOffset %= 8;
        }
    }
}