using System;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class FieldDeviceButton : TFieldDeviceInput
    {
        public FieldDeviceButton() { }
        public FieldDeviceButton(string name, string displayName) : base(name, displayName) { }
    }
}