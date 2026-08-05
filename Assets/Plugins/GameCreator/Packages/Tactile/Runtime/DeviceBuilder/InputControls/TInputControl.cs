using System;
using UnityEngine.InputSystem.Layouts;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Input Control")]
    [Image(typeof(IconCircleOutline), ColorTheme.Type.Yellow)]

    [Serializable]
    public abstract class TInputControl : TPolymorphicItem<TInputControl>
    {
        // PROPERTIES: ----------------------------------------------------------------------------
        
        public abstract string Name { get; }

        public abstract string DisplayName { get; }

        public abstract string Layout { get; }

        public abstract int ControlSizeInBits { get; }

        public sealed override string Title => string.Format(
            "[{0}] {1}     <i><color=#808080ff>{2}{3}", this.Layout,
            string.IsNullOrEmpty(this.DisplayName) ? this.Name : this.DisplayName,
            string.IsNullOrEmpty(this.Name) ? "" : $"path: <Tactile>/{this.Name}",
            $"     sizeInBits: {this.ControlSizeInBits}"
        );

        // ADD CONTROL METHODS: -------------------------------------------------------------------

        public abstract void AddControlToBuilder(
            InputControlLayout.Builder builder, ref int byteOffset, ref int bitOffset
        );
    }
}