using System;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class FieldDeviceVector2 : TFieldDeviceInput
    {
        public FieldDeviceVector2() : base() { }
        public FieldDeviceVector2(string name, string displayName) : base(name, displayName) { }
    }
}