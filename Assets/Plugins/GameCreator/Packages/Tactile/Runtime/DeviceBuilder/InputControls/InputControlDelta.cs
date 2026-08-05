using System;
using Builder = UnityEngine.InputSystem.Layouts.InputControlLayout.Builder;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace Niam.Runtime.Tactile
{
    [Title("Delta")]
    [Category("Delta")]
    [Image(typeof(IconDelta), ColorTheme.Type.TextLight)]

    [Serializable]
    public class InputControlDelta : TInputControl
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] protected string m_Name = "delta";
        [SerializeField] protected string m_DisplayName = "Delta";

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Name => this.m_Name;
        public override string DisplayName => this.m_DisplayName;
        public override string Layout => "Delta";
        public override int ControlSizeInBits => 64;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public InputControlDelta()
        { }

        public InputControlDelta(string name, string displayName)
        { 
            this.m_Name = name;
            this.m_DisplayName = displayName;
        }

        // ADD CONTROL METHODS: -------------------------------------------------------------------

        public override void AddControlToBuilder(Builder builder, ref int byteOffset, ref int bitOffset)
        {
            if (bitOffset > 0)
            {
                byteOffset++;
                bitOffset = 0;
            }

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