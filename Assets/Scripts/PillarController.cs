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

    [Header("Movement Settings")]
    public float movementSpeed = 2f;        // Tốc độ di chuyển sang trái/phải
    public float movementDistance = 3f;     // Khoảng cách di chuyển tối đa sang mỗi bên
    public bool moveFirstPillar = false;    // Có di chuyển pillar đầu tiên hay không

    // Private variables
    private List<Transform> spawnedPillars = new List<Transform>();
    private Dictionary<Transform, GameObject> pillarRewards = new Dictionary<Transform, GameObject>();
    private Dictionary<Transform, Vector3> pillarStartPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, float> pillarMovementDirections = new Dictionary<Transform, float>();
    private List<Transform> stationaryPillars = new List<Transform>(); // Danh sách các trụ đứng yên
    private Transform lastSpawnPoint;
    private PlayerController player;
    private BridgeController bridgeController;
    private GameManager gameManager;
    private Transform currentPlayerPillar;
    private Transform targetPillar; // Trụ mà người chơi đang nhắm tới

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

    void Update()
    {
        // Di chuyển các pillars không đứng yên
        MovePillars();

        // Kiểm tra nếu người chơi ấn nút C (bắt đầu xây cầu)
        if (Input.GetKeyDown(KeyCode.C) && player != null)
        {
            // Tìm trụ đích mà người chơi đang nhắm tới
            Transform playerPillar = player.GetCurrentPillar();
            if (playerPillar != null)
            {
                // Tìm trụ đích dựa trên hướng người chơi nhìn
                targetPillar = FindTargetPillar(playerPillar);

                // Nếu tìm thấy trụ đích, cho nó đứng im ngay lập tức
                if (targetPillar != null)
                {
                    StopPillarMovement(targetPillar);
                    Debug.Log("Đã dừng chuyển động của trụ đích: " + targetPillar.name);
                }
            }
        }
    }

    // Di chuyển các pillars qua trái phải
    void MovePillars()
    {
        foreach (Transform pillar in spawnedPillars)
        {
            // Bỏ qua pillars đã được đánh dấu là đứng yên
            if (stationaryPillars.Contains(pillar))
                continue;

            // Bỏ qua pillar đầu tiên nếu không muốn di chuyển nó
            if (!moveFirstPillar && pillar == spawnedPillars[0])
                continue;

            // Di chuyển các trụ còn lại
            if (pillarStartPositions.ContainsKey(pillar))
            {
                Vector3 startPos = pillarStartPositions[pillar];
                float direction = pillarMovementDirections[pillar];

                float xOffset = Mathf.Sin(Time.time * movementSpeed * direction) * movementDistance;
                Vector3 newPosition = startPos + new Vector3(xOffset, 0, 0);

                pillar.position = newPosition;
            }
        }
    }

    // Tìm trụ đích dựa trên hướng người chơi nhìn
    Transform FindTargetPillar(Transform currentPillar)
    {
        if (player == null) return null;

        Vector3 playerPosition = player.transform.position;
        Vector3 playerForward = player.transform.forward;
        playerForward.y = 0; // Loại bỏ hướng lên xuống
        playerForward.Normalize();

        Transform bestTarget = null;
        float bestAngle = float.MaxValue;
        float bestDistance = float.MaxValue;

        foreach (Transform pillar in spawnedPillars)
        {
            // Bỏ qua pillar hiện tại
            if (pillar == currentPillar) continue;

            Vector3 toPillar = pillar.position - playerPosition;
            toPillar.y = 0; // Chỉ xét trong mặt phẳng ngang
            toPillar.Normalize();

            // Tính góc giữa hướng người chơi và hướng đến trụ
            float angle = Vector3.Angle(playerForward, toPillar);

            // Tính khoảng cách đến trụ
            float distance = Vector3.Distance(playerPosition, pillar.position);

            // Nếu góc nhỏ (trụ nằm trong tầm nhìn) và khoảng cách hợp lý
            if (angle < 45f && distance < maxPillarDistance && distance > minPillarDistance)
            {
                if (angle < bestAngle || (Mathf.Approximately(angle, bestAngle) && distance < bestDistance))
                {
                    bestAngle = angle;
                    bestDistance = distance;
                    bestTarget = pillar;
                }
            }
        }

        return bestTarget;
    }

    // Khởi tạo khu vực chơi với các trụ ban đầu
    void InitializePlayArea()
    {
        // Xóa tất cả trụ hiện có
        ClearAllPillars();

        // Tạo trụ đầu tiên ở vị trí (0,0,0)
        Vector3 startPosition = Vector3.zero;
        Transform firstPillar = SpawnPillar(startPosition, false); // Không tạo reward cho pillar đầu tiên

        // Đảm bảo trụ đầu tiên luôn đứng yên
        if (!stationaryPillars.Contains(firstPillar))
        {
            stationaryPillars.Add(firstPillar);
        }

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

            if (gameManager != null)
            {
                gameManager.isStarted = true;
            }

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

        // Lưu vị trí ban đầu cho chuyển động
        pillarStartPositions[pillarTransform] = position;

        // Gán hướng chuyển động ngẫu nhiên (1 hoặc -1)
        pillarMovementDirections[pillarTransform] = Random.value > 0.5f ? 1f : -1f;

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
            Vector3 pos = rewardTransform.position;
            rewardTransform.position = new Vector3(pos.x, newY, pos.z);

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

                // Khi người chơi đứng trên trụ, thêm vào danh sách trụ đứng yên
                if (!stationaryPillars.Contains(playerPillar))
                {
                    stationaryPillars.Add(playerPillar);
                }

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

    // Dừng di chuyển của một trụ cụ thể (gọi khi cầu nối thành công hoặc khi ấn C)
    public void StopPillarMovement(Transform pillar)
    {
        if (pillar == null) return;

        Debug.Log("Đang dừng chuyển động của trụ: " + pillar.name);

        // Đảm bảo trụ không di chuyển
        if (!stationaryPillars.Contains(pillar))
        {
            stationaryPillars.Add(pillar);
        }

        // Cập nhật lại vị trí bắt đầu để đảm bảo trụ đứng yên tại vị trí hiện tại
        pillarStartPositions[pillar] = pillar.position;
    }

    // Dừng di chuyển tất cả các trụ (không cần thiết với logic mới)
    public void PauseAllPillarMovement()
    {
        // Trong logic mới, chúng ta không cần dừng tất cả các trụ
        // Thay vào đó, chúng ta dừng trụ đích khi ấn C
    }

    // Cho phép trụ di chuyển lại (nếu cần)
    public void ResumeAllPillarMovement()
    {
        // Trong logic mới, khi cầu không kết nối được với trụ
        // chúng ta không làm gì cả vì các trụ không phải trụ đích vẫn di chuyển bình thường
    }

    // Cho phép các trụ tiếp tục di chuyển trừ trụ đã kết nối
    public void ResumeAllPillarMovementExcept(Transform exceptPillar)
    {
        // Đánh dấu pillar cần giữ yên
        if (exceptPillar != null && !stationaryPillars.Contains(exceptPillar))
        {
            stationaryPillars.Add(exceptPillar);
        }

        Debug.Log("Đảm bảo trụ " + (exceptPillar != null ? exceptPillar.name : "none") + " đứng yên");
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

        // Xóa danh sách trụ đứng yên
        stationaryPillars.Clear();

        // Xóa các Dictionary lưu trữ
        pillarStartPositions.Clear();
        pillarMovementDirections.Clear();

        // Reset biến
        targetPillar = null;
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