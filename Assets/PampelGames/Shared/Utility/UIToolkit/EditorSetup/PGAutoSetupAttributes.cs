// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.Shared.Utility
{
    internal class PGAutoSetupAttributes
    {
    }

    /********************************************************************************************************************************/

    [AttributeUsage(AttributeTargets.Class)]
    public class PGEditorAutoAttribute : PropertyAttribute
    {
    }

    /********************************************************************************************************************************/

    public class PGHideAttribute : PropertyAttribute
    {
    }

    /// <summary>
    ///     Searches for the toggle elements and registers PGDisplayStyleFlex.
    /// </summary>
    public class PGDisplaySelectionAttribute : PropertyAttribute
    {
        public string[] ElementNames { get; }
        public bool DisplayBool { get; } // Used for booleans  
        public int[] DisplayEnums { get; } // Used for enums
        public bool Any { get; }

        /// <param name="elementNames">Boolean fields. Array example: new[]{nameof(randomSeed)}.</param>
        /// <param name="any">If true, only one of the elements needs to be true/false.</param>
        public PGDisplaySelectionAttribute(string[] elementNames, bool displayBool = true, bool any = true)
        {
            ElementNames = elementNames;
            DisplayBool = displayBool;
            Any = any;
        }

        /// <param name="elementNames">Enum fields. Array example: new[]{nameof(randomSeed)}.</param>
        /// <param name="displayEnums">Enum indexes. Array example: new[]{1, 2}.</param>
        /// <param name="any">If true, only one of the elements needs to match.</param>
        public PGDisplaySelectionAttribute(string[] elementNames, int[] displayEnums, bool any = true)
        {
            ElementNames = elementNames;
            DisplayEnums = displayEnums;
            Any = any;
        }
    }

    public class PGInsertAtAttribute : PropertyAttribute
    {
        public int Index { get; }

        public PGInsertAtAttribute(int index)
        {
            Index = index;
        }
    }

    public class PGClampAttribute : PropertyAttribute
    {
        public float MinValue { get; }
        public float MaxValue { get; }

        public PGClampAttribute(float minValue = 0f, float maxValue = float.MaxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }

    public class PGTagFieldAttribute : PropertyAttribute
    {
    }

    /// <summary>
    ///     This is for an integer using a single layer.
    ///     LayerMaskFields from LayerMasks are created automatically.
    /// </summary>
    public class PGLayerFieldAttribute : PropertyAttribute
    {
    }

    /// <summary>
    ///     Creates a ToolbarToggle instead of a Toggle (booleans only).
    /// </summary>
    public class PGToolbarToggleAttribute : PropertyAttribute
    {
    }

    public class PGSliderAttribute : PropertyAttribute
    {
        public float LowValue { get; }
        public float HighValue { get; }

        public PGSliderAttribute()
        {
            LowValue = 0f;
            HighValue = 1f;
        }

        public PGSliderAttribute(float lowValue, float highValue)
        {
            LowValue = lowValue;
            HighValue = highValue;
        }
    }

    public class PGMinMaxSliderAttribute : PropertyAttribute
    {
        public float LowLimit { get; }
        public float HighLimit { get; }

        public PGMinMaxSliderAttribute()
        {
            LowLimit = 0f;
            HighLimit = 1f;
        }

        public PGMinMaxSliderAttribute(float lowLimit, float highLimit)
        {
            LowLimit = lowLimit;
            HighLimit = highLimit;
        }
    }

    public class PGListViewLODAttribute : PropertyAttribute
    {
    }

    public class PGLabelAttribute : PropertyAttribute
    {
        public string Label { get; }

        public PGLabelAttribute(string label)
        {
            Label = label;
        }
    }

    public class PGHeaderAttribute : PropertyAttribute
    {
        public string Header { get; }
        public HeaderType HeaderType { get; }

        public PGHeaderAttribute(string header, HeaderType headerType)
        {
            Header = header;
            HeaderType = headerType;
        }
    }

    public enum HeaderType
    {
        Small,
        Big
    }

    /// <summary>
    ///     Can be used to add multiple elements to one Visual Element wrapper.
    /// </summary>
    public class PGGroupAttribute : PropertyAttribute
    {
        public string GroupName { get; }
        public FlexDirection FlexDirection { get; }

        public PGGroupAttribute(string groupName, FlexDirection flexDirection = FlexDirection.Column)
        {
            GroupName = groupName;
            FlexDirection = flexDirection;
        }
    }

    /// <summary>
    ///     Changes the Vector2Field/Vector2IntField X and Y label (default is "X" and "Y")
    /// </summary>
    public class PGVectorComponentLabelAttribute : PropertyAttribute
    {
        public string LabelX { get; }
        public string LabelY { get; }
        public float LabelFlexGrow { get; }
        public TextAnchor TextAnchor { get; }

        public PGVectorComponentLabelAttribute(string labelX, string labelY, float labelFlexGrow = 0.25f,
            TextAnchor textAnchor = TextAnchor.MiddleCenter)
        {
            LabelX = labelX;
            LabelY = labelY;
            LabelFlexGrow = labelFlexGrow;
            TextAnchor = textAnchor;
        }
    }

    /// <summary>
    ///     Must be declared on a string field.
    /// </summary>
    public class PGHelpBoxAttribute : PropertyAttribute
    {
        public string HelpBoxText { get; }
        public HelpBoxMessageType MessageType { get; }
        
        public PGHelpBoxAttribute(string helpBoxText, HelpBoxMessageType messageType = HelpBoxMessageType.Info)
        {
            HelpBoxText = helpBoxText;
            MessageType = messageType;
        }
    }

    /// <summary>
    ///     To ignore a value, set it to float.MinValue.
    /// </summary>
    public class PGMarginAttribute : PropertyAttribute
    {
        public float MarginLeft { get; }
        public float MarginRight { get; }
        public float MarginTop { get; }
        public float MarginBottom { get; }

        public PGMarginAttribute(float marginLeft = 10f)
        {
            MarginLeft = marginLeft;
            MarginRight = float.MinValue;
            MarginTop = float.MinValue;
            MarginBottom = float.MinValue;
        }

        public PGMarginAttribute(float marginTop, float marginBottom)
        {
            MarginLeft = float.MinValue;
            MarginRight = float.MinValue;
            MarginTop = marginTop;
            MarginBottom = marginBottom;
        }

        public PGMarginAttribute(float marginLeft, float marginRight, float marginTop, float marginBottom)
        {
            MarginLeft = marginLeft;
            MarginRight = marginRight;
            MarginTop = marginTop;
            MarginBottom = marginBottom;
        }
    }

    public class PGBottomLineAttribute : PropertyAttribute
    {
    }

    public class PGTopLineAttribute : PropertyAttribute
    {
    }


    /// <summary>
    ///     Must be declared on a string field.
    /// </summary>
    public class PGButtonMethodAttribute : PropertyAttribute
    {
        public string MethodName { get; }
        public float Height { get; }

        public PGButtonMethodAttribute(string methodName, float height = 30f)
        {
            MethodName = methodName;
            Height = height;
        }
    }
}