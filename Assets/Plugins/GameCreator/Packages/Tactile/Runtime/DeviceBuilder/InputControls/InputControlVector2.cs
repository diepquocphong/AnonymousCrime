using System;
using UnityEngine;
using Builder = UnityEngine.InputSystem.Layouts.InputControlLayout.Builder;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Vector 2")]
    [Category("Vector 2")]
    [Image(typeof(IconPosition), ColorTheme.Type.TextLight)]

    [Serializable]
    public class InputControlVector2 : TInputControl
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] protected string m_Name = "position";
        [SerializeField] protected string m_DisplayName = "Position";

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Name => this.m_Name;
        public override string DisplayName => this.m_DisplayName;
        public override string Layout => "Vector2";
        public override int ControlSizeInBits => 64;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public InputControlVector2()
        { }

        public InputControlVector2(string name, string displayName)
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