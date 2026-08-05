using System;
using UnityEngine;
using Builder = UnityEngine.InputSystem.Layouts.InputControlLayout.Builder;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Stick")]
    [Category("Stick")]
    [Image(typeof(IconStick), ColorTheme.Type.TextLight)]

    [Serializable]
    public class InputControlStick : TInputControl
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] protected string m_Name = "stick";
        [SerializeField] protected string m_DisplayName = "Stick";

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string Name => this.m_Name;
        public override string DisplayName => this.m_DisplayName;
        public override string Layout => "Stick";
        public override int ControlSizeInBits => 64;

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public InputControlStick()
        { }

        public InputControlStick(string name, string displayName)
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