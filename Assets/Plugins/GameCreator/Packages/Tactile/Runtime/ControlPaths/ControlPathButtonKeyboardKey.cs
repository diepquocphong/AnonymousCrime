using System;
using UnityEngine;
using UnityEngine.InputSystem;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Title("Keyboard Key")]
    [Category("Keyboard/Key")]
    [Description("")]

    [Image(typeof(IconKey), ColorTheme.Type.Yellow)]
    [Keywords("")]

    [Serializable]
    public class ControlPathButtonKeyboardKey : ControlPathButton
    {
        internal static readonly string[] KeyNameArray = new string[]
        {
            "", "space", "enter", "tab", "backquote", "quote", "semicolon", "comma", "period", 
            "slash", "backslash", "leftbracket", "rightbracket", "minus", "equals", "a", "b", "c", 
            "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", 
            "u", "v", "w", "x", "y", "z", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", 
            "leftshift", "rightshift", "leftalt", "rightalt", "leftctrl", "rightctrl", "leftmeta", 
            "rightmeta", "contextmenu", "escape", "leftarrow", "rightarrow", "uparrow", 
            "downarrow", "backspace", "pagedown", "pageup", "home", "end", "insert", "delete", 
            "capslock", "numlock", "printscreen", "scrolllock", "pause", "numpadenter", 
            "numpaddivide", "numpadmultiply", "numpadplus", "numpadminus", "numpadperiod",
            "numpadequals", "numpad0", "numpad1", "numpad2", "numpad3", "numpad4", "numpad5", 
            "numpad6", "numpad7", "numpad8", "numpad9", "f1", "f2", "f3", "f4", "f5", "f6", "f7", 
            "f8", "f9", "f10", "f11", "f12", "oem1", "oem2", "oem3", "oem4", "oem5", "IMESelected", 
            "f13", "f14", "f15", "f16", "f17", "f18", "f19", "f20", "f21", "f22", "f23", "f24",
            "mediaPlayPause", "mediaRewind", "mediaForward"
        };

        // MEMBERS: -------------------------------------------------------------------------------
        
        [SerializeField] private Key m_Key = Key.Space;

        [NonSerialized] private string m_LastKey;
        [NonSerialized] private string m_LastPath;

        // PROPERTIES: ----------------------------------------------------------------------------

        public override string ControlPath
        {
            get
            {
                string keyName = GetKeyName(this.m_Key);
                if (string.IsNullOrEmpty(keyName))
                    return string.Empty;
                
                if (keyName == this.m_LastKey)
                    return this.m_LastPath;

                this.m_LastKey = keyName;
                this.m_LastPath = "<Keyboard>/" + keyName;

                return this.m_LastPath;
            }
        }

        // CONSTRUCTORS: --------------------------------------------------------------------------

        public ControlPathButtonKeyboardKey()
        { }

        public ControlPathButtonKeyboardKey(Key key)
        {
            this.m_Key = key;
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private static string GetKeyName(Key key)
        {
            int index = (int)key;
            
            return index >= 0 && index < KeyNameArray.Length 
                ? KeyNameArray[index] : "";
        }
    }
}