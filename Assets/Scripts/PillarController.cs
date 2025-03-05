using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GameRewards;

public class PillarController : MonoBehaviour
{
    [Header("Pillar Settings")]
    public GameObject pillarPrefab;
    public int initialPillarCount = 2;
    public float minPillarDistance = 5f;
    public float maxPillarDistance = 15f;
    public float pillarHeight = 5f;

    [Header("Reward Settings")]
    public GameObject rewardPrefab;
    public float rewardHeightOffset = 1.5f;
    public int pointsPerReward = 10;
    public bool spawnRewardOnFirstPillar = false; // Không tạo reward trên pillar đầu tiên

    // Private variables
    private List<Transform> spawnedPillars = new List<Transform>();
    private Dictionary<Transform, GameObject> pillarRewards = new Dictionary<Transform, GameObject>();
    private Transform lastSpawnPoint;
    private PlayerController player;
    private BridgeController bridgeController;
    private GameManager gameManager;
    private Transform currentPlayerPillar;

    void Start()
    {
        // Tìm các component cần thiết
        player = FindObjectOfType<PlayerController>();
        bridgeController = FindObjectOfType<BridgeController>();
        gameManager = FindObjectOfType<GameManager>();

        // Kiểm tra xem có pillar prefab không
        if (pillarPrefab == null)
        {
            Debug.LogError("Chưa thiết lập Pillar Prefab!");
            return;
        }

        // Khởi tạo khu vực chơi với pillar ban đầu
        InitializePlayArea();

        // Bắt đầu kiểm tra người chơi trên trụ
        StartCoroutine(CheckPlayerPillar());
    }

    // Khởi tạo khu vực chơi với các trụ ban đầu
    void InitializePlayArea()
    {
        // Xóa tất cả trụ hiện có
        ClearAllPillars();

        // Tạo trụ đầu tiên ở vị trí (0,0,0)
        Vector3 startPosition = Vector3.zero;
        Transform firstPillar = SpawnPillar(startPosition, false); // Không tạo reward cho pillar đầu tiên

        // Tạo trụ thứ hai phía trước trụ đầu tiên
        Vector3 secondPosition = startPosition + Vector3.forward * minPillarDistance;
        lastSpawnPoint = SpawnPillar(secondPosition, true); // Tạo reward cho pillar thứ hai

        // Đặt người chơi lên trụ đầu tiên
        StartCoroutine(PositionPlayerOnFirstPillar());
    }

    // Di chuyển người chơi lên trụ đầu tiên
    IEnumerator PositionPlayerOnFirstPillar()
    {
        // Đợi vật lý ổn định
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        if (player != null && spawnedPillars.Count > 0)
        {
            Transform firstPillar = spawnedPillars[0];

            // Tính vị trí trên đỉnh trụ
            float pillarTopY = firstPillar.position.y + (firstPillar.localScale.y / 2);
            Vector3 playerPos = firstPillar.position;
            playerPos.y = pillarTopY + 1.0f; // Thêm 1 đơn vị trên đỉnh trụ

            // Đặt người chơi vào vị trí
            player.transform.position = playerPos;

            // Đặt vận tốc về 0 nếu có Rigidbody
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            gameManager.isStarted = true;

            currentPlayerPillar = firstPillar;
        }
    }

    // Tạo một trụ mới tại vị trí cụ thể
    Transform SpawnPillar(Vector3 position, bool createReward = true)
    {
        // Tạo trụ từ prefab
        GameObject pillarObj = Instantiate(pillarPrefab, position, Quaternion.identity);
        Transform pillarTransform = pillarObj.transform;

        // Điều chỉnh chiều cao trụ
        Vector3 scale = pillarTransform.localScale;
        scale.y = pillarHeight;
        pillarTransform.localScale = scale;

        // Điều chỉnh vị trí Y để trụ đứng trên mặt đất
        position.y = pillarHeight / 2;
        pillarTransform.position = position;

        // Thêm vào danh sách trụ
        spawnedPillars.Add(pillarTransform);

        // Đặt tên để dễ debug
        pillarObj.name = "Pillar_" + (spawnedPillars.Count - 1);

        // Đặt tag để nhận diện
        pillarObj.tag = "Pillar";

        // Tạo phần thưởng trên trụ nếu được yêu cầu
        if (createReward && rewardPrefab != null)
        {
            // Không tạo reward trên pillar đầu tiên nếu spawnRewardOnFirstPillar = false
            if (spawnedPillars.Count > 1 || spawnRewardOnFirstPillar)
            {
                SpawnRewardOnPillar(pillarTransform);
            }
        }

        return pillarTransform;
    }

    // Tạo phần thưởng trên trụ
    void SpawnRewardOnPillar(Transform pillarTransform)
    {
        // Nếu không có prefab phần thưởng, không tạo
        if (rewardPrefab == null) return;

        // Tính vị trí phần thưởng trên đỉnh trụ
        Vector3 rewardPosition = pillarTransform.position;
        rewardPosition.y = pillarTransform.position.y + (pillarTransform.localScale.y / 2) + rewardHeightOffset;

        // Tạo phần thưởng
        GameObject reward = Instantiate(rewardPrefab, rewardPosition, Quaternion.identity);

        // Gắn reward là con của trụ
        reward.transform.parent = pillarTransform;

        // Xoay ngẫu nhiên để sinh động
        reward.transform.Rotate(0, Random.Range(0, 360), 0);

        // Thêm RewardItem component nếu chưa có
        RewardItem rewardItem = reward.GetComponent<RewardItem>();
        if (rewardItem == null)
        {
            rewardItem = reward.AddComponent<RewardItem>();
        }

        // Thiết lập giá trị điểm
        rewardItem.pointValue = pointsPerReward;

        // Lưu tham chiếu đến phần thưởng
        pillarRewards[pillarTransform] = reward;

        // Thêm animation nhẹ nhàng
        StartCoroutine(AnimateReward(reward.transform));
    }

    // Tạo animation cho phần thưởng
    IEnumerator AnimateReward(Transform rewardTransform)
    {
        if (rewardTransform == null) yield break;

        float rotationSpeed = 30f; // độ/giây
        float bobSpeed = 1f; // chu kỳ/giây
        float bobHeight = 0.2f; // chiều cao lên xuống
        Vector3 startPos = rewardTransform.position;

        while (rewardTransform != null)
        {
            // Xoay
            rewardTransform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // Lên xuống
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2) * bobHeight;
            rewardTransform.position = new Vector3(startPos.x, newY, startPos.z);

            yield return null;
        }
    }

    // Kiểm tra người chơi đang đứng trên trụ nào
    IEnumerator CheckPlayerPillar()
    {
        yield return new WaitForSeconds(0.5f); // Đợi mọi thứ khởi tạo hoàn tất

        while (true)
        {
            // Kiểm tra xem người chơi có tồn tại không
            if (player == null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Kiểm tra xem người chơi đang đứng trên trụ nào
            Transform playerPillar = player.GetCurrentPillar();

            if (playerPillar != null && playerPillar != currentPlayerPillar)
            {
                // Người chơi đã di chuyển đến trụ mới
                currentPlayerPillar = playerPillar;

                // Nếu đây là trụ cuối cùng trong danh sách, tạo trụ mới
                if (playerPillar == spawnedPillars[spawnedPillars.Count - 1])
                {
                    SpawnNextPillar();
                }

                // Thu thập phần thưởng nếu có
                CollectPillarReward(playerPillar);
            }

            // Kiểm tra xem người chơi có rơi khỏi khu vực chơi không
            if (playerPillar == null && player.transform.position.y < -10f)
            {
                // Người chơi rơi xuống quá sâu, đặt lại lên trụ đầu tiên
                StartCoroutine(PositionPlayerOnFirstPillar());
            }

            // Đợi một chút trước khi kiểm tra tiếp
            yield return new WaitForSeconds(0.1f);
        }
    }

    // Tạo trụ tiếp theo
    void SpawnNextPillar()
    {
        // Tính hướng ngẫu nhiên
        Vector3 direction = GetRandomDirection();

        // Khoảng cách ngẫu nhiên giữa min và max
        float distance = Random.Range(minPillarDistance, maxPillarDistance);

        // Tính vị trí mới
        Vector3 newPosition = lastSpawnPoint.position + direction * distance;

        // Đặt Y về 0 (mặt đất) trước khi tạo
        newPosition.y = 0;

        // Tạo trụ mới với reward
        lastSpawnPoint = SpawnPillar(newPosition, true);

        Debug.Log("Đã tạo trụ mới với phần thưởng tại: " + newPosition);
    }

    // Lấy hướng ngẫu nhiên để tạo trụ tiếp theo
    Vector3 GetRandomDirection()
    {
        // Chọn hướng ngẫu nhiên không hướng thẳng về phía sau
        float angle = Random.Range(-120f, 120f); // -120 đến 120 độ từ hướng trước
        Quaternion rotation = Quaternion.Euler(0, angle, 0);
        return rotation * Vector3.forward;
    }

    // Thu thập phần thưởng trên trụ
    void CollectPillarReward(Transform pillar)
    {
        // Kiểm tra xem có phần thưởng trên trụ này không
        if (pillarRewards.ContainsKey(pillar) && pillarRewards[pillar] != null)
        {
            GameObject reward = pillarRewards[pillar];

            // Lấy giá trị điểm
            int points = pointsPerReward;
            RewardItem rewardItem = reward.GetComponent<RewardItem>();
            if (rewardItem != null)
            {
                points = rewardItem.pointValue;
            }

            // Thêm điểm cho người chơi
            if (gameManager != null)
            {
                gameManager.AddPoints(points);
            }

            // Xóa phần thưởng
            Destroy(reward);
            pillarRewards.Remove(pillar);

            Debug.Log("Đã thu thập phần thưởng từ trụ: " + pillar.name);
        }
    }

    // Xóa tất cả các trụ
    public void ClearAllPillars()
    {
        // Xóa phần thưởng
        foreach (GameObject reward in pillarRewards.Values)
        {
            if (reward != null)
            {
                Destroy(reward);
            }
        }
        pillarRewards.Clear();

        // Xóa trụ
        foreach (Transform pillar in spawnedPillars)
        {
            if (pillar != null)
            {
                Destroy(pillar.gameObject);
            }
        }
        spawnedPillars.Clear();
    }

    // Reset khu vực chơi cho game mới
    public void ResetPlayArea()
    {
        InitializePlayArea();
    }

    // Lấy danh sách tất cả các trụ
    public List<Transform> GetAllPillars()
    {
        return spawnedPillars;
    }

    // Lấy trụ cuối cùng được tạo
    public Transform GetLastPillar()
    {
        if (spawnedPillars.Count > 0)
        {
            return spawnedPillars[spawnedPillars.Count - 1];
        }
        return null;
    }
}