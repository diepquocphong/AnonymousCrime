using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Niam.Runtime.Tactile
{
    public static class Theme
    {
        public static Color MainColor
        {
            get
            {
                #if UNITY_EDITOR
                return EditorGUIUtility.isProSkin ? LightColor : DarkColor;
                #else
                return LightColor;
                #endif
            }
        }

        public static Color LightColor => new (1f, 0.9882353f, 0.7098039f);
        public static Color DarkColor => new (0.750f, 0.741f, 0.532f);
    }
}