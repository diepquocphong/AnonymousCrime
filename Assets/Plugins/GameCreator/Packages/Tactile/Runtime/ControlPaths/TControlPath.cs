using System;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public abstract class TControlPath<T>
    {
        public abstract string ControlPath { get; }
        public virtual T PreprocessInput(T input) => input;

        public override string ToString()
        {
            string controlPath = ControlPath;
            return !string.IsNullOrEmpty(controlPath) 
                ? ControlPath : "<None>";
        }
    }
}