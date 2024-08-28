using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class NetworkChecker : MonoBehaviour
{
    // Sự kiện sẽ được kích hoạt khi không có kết nối mạng
    public UnityEvent OnNetworkDisconnected;

    // Tần suất kiểm tra kết nối mạng (đơn vị: giây)
    public float checkInterval = 5.0f;

    private void Start()
    {
        // Bắt đầu kiểm tra kết nối mạng định kỳ
        StartCoroutine(CheckNetworkConnection());
    }

    // Coroutine kiểm tra kết nối mạng
    private IEnumerator CheckNetworkConnection()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                // Không có kết nối mạng, kích hoạt sự kiện
                OnNetworkDisconnected?.Invoke();
            }
        }
    }
}
