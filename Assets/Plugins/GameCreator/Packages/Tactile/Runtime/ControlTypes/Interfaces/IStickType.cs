using UnityEngine;

namespace Niam.Runtime.Tactile 
{
    interface IStickType
    {
        public Vector2 StickSize { get; }
        public Vector2 StickMotion { get; }
        public Vector2 StickRawMotion { get; }

        public RectTransform Surface { get; }
        public RectTransform Handle { get; }
        public RectTransform Arrow { get; }
    }
}