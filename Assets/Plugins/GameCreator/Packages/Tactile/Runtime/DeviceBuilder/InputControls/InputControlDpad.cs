using System;
using UnityEngine;
using Builder = UnityEngine.InputSystem.Layouts.InputControlLayout.Builder;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Dpad")]
    [Category("Dpad")]
    [Image(typeof(IconGamepadCross), ColorTheme.Type.TextLight)]

    [Serializable]
    public class InputControlDpad : TInputControl
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] protected string m_Name = "dpad";
        [SerializeField] protected string m_DisplayName = "D-Pad";

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Name => this.m_Name;
        public override string DisplayName => this.m_DisplayName;
        public override string Layout => "Dpad";
        public override int ControlSizeInBits => 4;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public InputControlDpad()
        { }

        public InputControlDpad(string name, string displayName)
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
                   .WithBitOffset((uint)bitOffset)
                   .WithSizeInBits((uint)this.ControlSizeInBits);

            this.AddChildControl(builder, "up", ref byteOffset, ref bitOffset);
            this.AddChildControl(builder, "down", ref byteOffset, ref bitOffset);
            this.AddChildControl(builder, "left", ref byteOffset, ref bitOffset);
            this.AddChildControl(builder, "right", ref byteOffset, ref bitOffset);
        }

        private void AddChildControl(
            Builder builder, string childName, ref int byteOffset, ref int bitOffset)
        {
            builder.AddControl($"{this.Name}/{childName}")
                   .WithByteOffset((uint)byteOffset)
                   .WithBitOffset((uint)bitOffset);

            bitOffset ++;
            byteOffset += bitOffset / 8;
            bitOffset %= 8;
        }
    }
}