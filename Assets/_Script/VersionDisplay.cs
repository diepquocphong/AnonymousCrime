using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VersionDisplay : MonoBehaviour
{
    // Tham chiếu đến đối tượng Text trong Canvas
    public TMP_Text versionText;

    void Start()
    {
        // Kiểm tra xem đối tượng Text đã được gán chưa
        if (versionText != null)
        {
            // Lấy phiên bản ứng dụng từ PlayerSettings
            string version = Application.version;
            // Hiển thị phiên bản lên UI Text
            versionText.text = "Version " + version;
        }
        else
        {
            Debug.LogError("Version Text is not assigned.");
        }
    }
}