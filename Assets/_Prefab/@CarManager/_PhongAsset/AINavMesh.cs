using UnityEngine;
using UnityEngine.AI;

public class AINavMesh : MonoBehaviour
{
    public Transform target;  // Biến công khai target để chỉ định đối tượng mục tiêu
    public float minDistance = 1.0f;  // Khoảng cách tối thiểu để dừng lại (nếu cần dừng khi quá gần)

    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();  // Lấy component NavMeshAgent
    }

    void Update()
    {
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            Debug.Log("Distance to target: " + distance);

            // AI sẽ luôn di chuyển về phía target
            if (distance > minDistance)  // Chỉ dừng lại nếu quá gần mục tiêu
            {
                agent.SetDestination(target.position);  // Di chuyển tới target
            }
            else
            {
                agent.ResetPath();  // Dừng lại nếu đến gần hơn minDistance
            }
        }
        else
        {
            Debug.Log("Target is not set!");
        }
    }
}
