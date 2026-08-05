using System;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class FieldDeviceFloat : TFieldDeviceInput
    {
        public FieldDeviceFloat() { }
        public FieldDeviceFloat(string name, string displayName) : base(name, displayName) { }
    }
}