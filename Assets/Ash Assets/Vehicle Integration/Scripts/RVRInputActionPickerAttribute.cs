using UnityEngine;

public class RVRInputActionPickerAttribute : PropertyAttribute
{
    public readonly string AssetFieldName;

    public RVRInputActionPickerAttribute(string assetFieldName)
    {
        AssetFieldName = assetFieldName;
    }
}