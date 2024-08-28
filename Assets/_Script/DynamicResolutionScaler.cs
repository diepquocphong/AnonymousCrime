using UnityEngine;

public class DynamicResolutionScaler : MonoBehaviour
{
    // FPS mục tiêu
    public int targetFPS = 60;

    // Tỷ lệ nhỏ nhất và lớn nhất
    public float minScaleFactor = 0.5f;
    public float maxScaleFactor = 1f;

    // Tốc độ thay đổi của tỷ lệ
    public float scaleFactorChangeRate = 0.1f;

    // Độ phân giải tham chiếu
    public Vector2 referenceResolution = new Vector2(1920, 1080);

    private float currentScaleFactor = 1f;

    void Start()
    {
        // Đặt FPS mục tiêu
        Application.targetFrameRate = targetFPS;
    }

    void Update()
    {
        // Tính toán tải GPU hiện tại
        float gpuLoad = Mathf.Clamp01((float)SystemInfo.graphicsMemorySize / SystemInfo.graphicsMemorySize);

        // Tính toán tỷ lệ mong muốn dựa trên tải GPU
        float desiredScaleFactor = Mathf.Lerp(minScaleFactor, maxScaleFactor, gpuLoad);

        // Điều chỉnh mịn tỷ lệ hiện tại đến tỷ lệ mong muốn
        currentScaleFactor = Mathf.Lerp(currentScaleFactor, desiredScaleFactor, scaleFactorChangeRate * Time.deltaTime);

        // Tính toán độ phân giải được thu nhỏ
        int screenWidth = Mathf.RoundToInt(referenceResolution.x * currentScaleFactor);
        int screenHeight = Mathf.RoundToInt(referenceResolution.y * currentScaleFactor);

        // Đặt độ phân giải
        Screen.SetResolution(screenWidth, screenHeight, true);
    }
}
