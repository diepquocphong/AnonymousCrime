using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PopularPoolManager : MonoBehaviour
{
    public GameObject objectPrefab; // Prefab đối tượng muốn sinh ra
    public Transform player; // Vị trí của Player
    public LayerMask markerLayer; // Layer MarkerPopulatorFW
    public float spawnRadius = 90f; // Bán kính sinh đối tượng
    public float maxSpawnDistance = 90f; // Khoảng cách tối đa để xóa đối tượng
    public int maxObjects = 10; // Số lượng đối tượng tối đa được sinh ra

    private List<GameObject> spawnedObjects = new List<GameObject>();

    void Update()
    {
        CheckMarkersAndSpawn();
        CheckObjectDistancesAndRemove();
    }

    void CheckMarkersAndSpawn()
    {
        // Tìm tất cả các đối tượng có Layer markerLayer trong khoảng spawnRadius
        Collider[] markers = Physics.OverlapSphere(player.position, spawnRadius, markerLayer);

        // Sắp xếp các markers theo khoảng cách gần nhất tới player
        List<Collider> sortedMarkers = new List<Collider>(markers);
        sortedMarkers.Sort((a, b) =>
            Vector3.Distance(player.position, a.transform.position).CompareTo(Vector3.Distance(player.position, b.transform.position))
        );

        foreach (var marker in sortedMarkers)
        {
            // Kiểm tra nếu số lượng đối tượng đã sinh ra chưa vượt quá giới hạn
            if (spawnedObjects.Count < maxObjects)
            {
                float distanceToMarker = Vector3.Distance(player.position, marker.transform.position);

                if (distanceToMarker <= spawnRadius)
                {
                    // Sinh đối tượng tại vị trí của marker gần nhất
                    GameObject newObject = Instantiate(objectPrefab, marker.transform.position, Quaternion.identity);
                    spawnedObjects.Add(newObject);
                }
            }
        }
    }

    void CheckObjectDistancesAndRemove()
    {
        // Duyệt qua tất cả các đối tượng đã sinh ra
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = spawnedObjects[i];
            if (Vector3.Distance(player.position, obj.transform.position) > maxSpawnDistance)
            {
                // Nếu khoảng cách giữa Player và đối tượng lớn hơn maxSpawnDistance, xóa đối tượng
                Destroy(obj);
                spawnedObjects.RemoveAt(i);
            }
        }
    }
}
