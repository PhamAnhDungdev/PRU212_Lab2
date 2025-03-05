using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#region BridgeController
/// <summary>
/// Quản lý việc tạo, cập nhật và xóa các cây cầu trong trò chơi.
/// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
/// Ngày sửa: None
/// </summary>
/// <param name="None">Không có tham số truyền vào.</param>
/// <returns>Không trả về giá trị.</returns>
/// <remarks>
/// - Cho phép người chơi tạo cầu bằng cách nhấn và giữ phím C.
/// - Chiều dài cầu phụ thuộc vào thời gian người chơi giữ phím.
/// - Tự động tạo chướng ngại vật ngẫu nhiên trên cầu sau khi hoàn thành.
/// - Xử lý va chạm và tương tác giữa cầu với người chơi và các đối tượng khác.
/// </remarks>
#endregion
public class BridgeController : MonoBehaviour
{
    [Header("Bridge Settings")]
    public GameObject bridgePrefab; // Prefab của cầu sẽ được tạo ra
    public float minBridgeLength = 2f; // Độ dài tối thiểu của cầu
    public float maxBridgeLength = 20f; // Độ dài tối đa của cầu
    public float bridgeWidth = 1.5f; // Độ rộng của cầu
    public float bridgeHeight = 0.1f; // Độ dày (chiều cao) của cầu
    public float bridgeBuildSpeed = 5f; // Tốc độ tăng độ dài cầu (đơn vị/giây)

    [Header("Obstacle Settings")]
    public GameObject[] obstaclePrefabs; // Mảng các prefab chướng ngại vật
    public float obstacleSpawnChance = 0.7f; // Tỉ lệ xuất hiện chướng ngại vật (0-1)
    public float minObstacleSpacing = 2f; // Khoảng cách tối thiểu giữa các chướng ngại vật
    public float obstacleHeightOffset = 0.5f; // Độ cao của chướng ngại vật trên cầu

    [Header("Visual Effects")]
    public Material validBridgeMaterial; // Vật liệu hiển thị khi cầu ở trạng thái hợp lệ
    public Material invalidBridgeMaterial; // Vật liệu hiển thị khi cầu ở trạng thái không hợp lệ

    // Private variables
    private bool isBuildingBridge = false; // Cờ đánh dấu đang trong quá trình xây dựng cầu
    private float currentBridgeLength = 0f; // Độ dài hiện tại của cầu đang được xây dựng
    private float buildStartTime; // Thời điểm bắt đầu xây dựng cầu
    private GameObject activeBridge; // Cầu đang được xây dựng
    private Vector3 bridgeStartPosition; // Vị trí đỉnh cầu
    private Vector3 bridgeDirection; // Hướng của cầu
    private List<GameObject> activeBridges = new List<GameObject>(); // Danh sách các cầu đã tạo
    private Dictionary<GameObject, List<GameObject>> bridgeObstacles = new Dictionary<GameObject, List<GameObject>>(); // Chướng ngại vật trên mỗi cầu

    // References
    private PlayerController player; // Tham chiếu đến người chơi
    private PillarController pillarController; // Tham chiếu đến bộ điều khiển trụ
    private GameManager gameManager; // Tham chiếu đến game manager

    #region InitializationMethod
    /// <summary>
    /// Khởi tạo các tham chiếu cần thiết và kiểm tra các điều kiện ban đầu.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="None">Không có tham số truyền vào.</param>
    /// <returns>Không trả về giá trị.</returns>
    /// <remarks>
    /// - Tìm và lưu trữ tham chiếu đến PlayerController, PillarController và GameManager.
    /// - Kiểm tra xem bridgePrefab đã được thiết lập chưa, nếu chưa sẽ hiển thị thông báo lỗi.
    /// - Kiểm tra và cảnh báo nếu không có prefab chướng ngại vật nào được thiết lập.
    /// </remarks>
    #endregion
    void Start()
    {
        // Tìm Player và PillarController trong scene
        player = FindObjectOfType<PlayerController>();
        pillarController = FindObjectOfType<PillarController>();
        gameManager = FindObjectOfType<GameManager>();

        // Kiểm tra xem có bridge prefab chưa
        if (bridgePrefab == null)
        {
            Debug.LogError("Chưa thiết lập Bridge Prefab cho BridgeController!");
        }

        // Kiểm tra xem có obstacle prefabs không
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
        {
            Debug.LogWarning("Không có Obstacle Prefabs! Sẽ không tạo chướng ngại vật.");
        }
    }

    #region UpdateMethod
    /// <summary>
    /// Xử lý đầu vào từ người chơi và cập nhật trạng thái của cầu mỗi frame.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="None">Không có tham số truyền vào.</param>
    /// <returns>Không trả về giá trị.</returns>
    /// <remarks>
    /// - Kiểm tra trạng thái trò chơi trước khi xử lý đầu vào.
    /// - Nếu người chơi nhấn phím C và chưa đang xây cầu, bắt đầu xây cầu.
    /// - Nếu người chơi giữ phím C và đang xây cầu, cập nhật chiều dài cầu.
    /// - Nếu người chơi thả phím C và đang xây cầu, hoàn thành việc xây cầu.
    /// </remarks>
    #endregion
    void Update()
    {
        // Kiểm tra nếu game kết thúc hoặc tạm dừng
        if (gameManager != null && (gameManager.IsGameOver() || gameManager.IsGamePaused()))
        {
            return;
        }

        // Khi nhấn C, bắt đầu tạo cầu
        if (Input.GetKeyDown(KeyCode.C) && !isBuildingBridge)
        {
            StartBuildingBridge();
        }

        // Khi đang giữ C, cập nhật chiều dài cầu
        if (Input.GetKey(KeyCode.C) && isBuildingBridge)
        {
            UpdateBridgeWhileBuilding();
        }

        // Khi thả C, kết thúc xây cầu
        if (Input.GetKeyUp(KeyCode.C) && isBuildingBridge)
        {
            FinishBuildingBridge();
        }
    }

    #region BridgeCreationMethod
    /// <summary>
    /// Bắt đầu quá trình xây dựng cầu mới từ vị trí hiện tại của người chơi.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="None">Không có tham số truyền vào.</param>
    /// <returns>Không trả về giá trị.</returns>
    /// <remarks>
    /// - Lấy thông tin về trụ hiện tại người chơi đang đứng.
    /// - Tính toán vị trí đỉnh trụ làm điểm bắt đầu của cầu.
    /// - Xác định hướng của cầu dựa trên hướng nhìn của người chơi.
    /// - Thiết lập các giá trị ban đầu và tạo cầu với chiều dài tối thiểu.
    /// </remarks>
    #endregion
    void StartBuildingBridge()
    {
        // Lấy pillar mà player đang đứng
        Transform currentPillar = player.GetCurrentPillar();

        if (currentPillar == null)
        {
            Debug.LogWarning("Không thể tạo cầu - Player không đứng trên pillar nào!");
            return;
        }

        // Tính toán vị trí đỉnh pillar
        Vector3 pillarTop = GetPillarTopPosition(currentPillar);
        Debug.Log("Bắt đầu tạo cầu từ đỉnh trụ: " + pillarTop);

        // Hướng nhìn ngang của player
        Vector3 playerLookDir = player.transform.forward;
        playerLookDir.y = 0;
        playerLookDir.Normalize();

        // Thiết lập các giá trị ban đầu
        isBuildingBridge = true;
        bridgeStartPosition = pillarTop;
        bridgeDirection = playerLookDir;
        currentBridgeLength = minBridgeLength;
        buildStartTime = Time.time;

        // Tạo cầu mới với chiều dài ban đầu
        CreateInitialBridge();
    }

    #region PillarTopCalculationMethod
    /// <summary>
    /// Tính toán vị trí đỉnh của một trụ cho điểm bắt đầu của cầu.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="pillar">Transform của trụ cần tính toán vị trí đỉnh.</param>
    /// <returns>Vector3 chứa tọa độ đỉnh của trụ.</returns>
    /// <remarks>
    /// - Lấy vị trí trung tâm của trụ.
    /// - Tính toán chiều cao của trụ dựa trên scale.y.
    /// - Trả về vị trí đỉnh bằng cách cộng một nửa chiều cao vào tọa độ y của trung tâm trụ.
    /// </remarks>
    #endregion
    Vector3 GetPillarTopPosition(Transform pillar)
    {
        if (pillar == null) return Vector3.zero;

        // Lấy vị trí trung tâm của pillar
        Vector3 pillarPosition = pillar.position;

        // Lấy chiều cao của pillar (scale.y)
        float pillarHeight = pillar.localScale.y;

        // Tính toán vị trí đỉnh (pillar center + half height)
        Vector3 topPosition = pillarPosition;
        topPosition.y = pillarPosition.y + (pillarHeight / 2);

        return topPosition;
    }

    #region InitialBridgeCreationMethod
    /// <summary>
    /// Tạo cầu ban đầu tại vị trí đỉnh trụ với hướng và chiều dài đã xác định.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="None">Không có tham số truyền vào.</param>
    /// <returns>Không trả về giá trị.</returns>
    /// <remarks>
    /// - Tạo instance mới từ bridgePrefab.
    /// - Thiết lập kích thước, hướng và vị trí cho cầu.
    /// - Xoay cầu theo hướng người chơi nhìn.
    /// - Đặt tag "Bridge" cho cầu và bật collider.
    /// - Khởi tạo danh sách trống để lưu trữ chướng ngại vật trên cầu này.
    /// </remarks>
    #endregion
    void CreateInitialBridge()
    {
        // Tạo bridge mới
        activeBridge = Instantiate(bridgePrefab);

        // Thiết lập kích thước ban đầu
        Vector3 scale = activeBridge.transform.localScale;
        scale.y = currentBridgeLength; // Độ dài theo trục Y
        scale.x = bridgeWidth;         // Độ rộng theo trục X
        scale.z = bridgeHeight;        // Độ dày theo trục Z
        activeBridge.transform.localScale = scale;

        // Thiết lập rotation để cầu nằm ngang theo hướng người chơi nhìn
        Quaternion targetRotation = Quaternion.LookRotation(bridgeDirection);
        targetRotation *= Quaternion.Euler(90, 0, 0); // Xoay 90 độ để cầu nằm ngang
        activeBridge.transform.rotation = targetRotation;

        // Thiết lập vị trí sao cho một đầu cầu ở đúng vị trí đỉnh trụ
        // Lưu ý: Pivot của bridge ở tâm, nên cần điều chỉnh vị trí
        Vector3 position = bridgeStartPosition + bridgeDirection * (currentBridgeLength / 2);
        activeBridge.transform.position = position;

        Debug.Log("Đã tạo cầu tại: " + bridgeStartPosition + " hướng về: " + bridgeDirection);

        // Đặt tag để nhận diện
        activeBridge.tag = "Bridge";

        // Bật collider
        Collider[] colliders = activeBridge.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = true;
        }

        // Tạo đối tượng để lưu trữ danh sách chướng ngại vật cho cầu này
        bridgeObstacles[activeBridge] = new List<GameObject>();
    }

    #region BridgeUpdateMethod
    /// <summary>
    /// Cập nhật chiều dài cầu trong quá trình người chơi giữ phím C.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="None">Không có tham số truyền vào.</param>
    /// <returns>Không trả về giá trị.</returns>
    /// <remarks>
    /// - Tính toán thời gian đã giữ phím để xác định độ dài mới của cầu.
    /// - Giới hạn độ dài cầu trong khoảng từ minBridgeLength đến maxBridgeLength.
    /// - Cập nhật kích thước cầu theo độ dài mới.
    /// - Điều chỉnh vị trí cầu để một đầu vẫn ở đúng vị trí đỉnh trụ.
    /// </remarks>
    #endregion
    void UpdateBridgeWhileBuilding()
    {
        if (activeBridge == null) return;

        // Tính thời gian đã giữ phím
        float timeHeld = Time.time - buildStartTime;

        // Tính toán độ dài mới dựa trên thời gian và tốc độ xây dựng
        float newLength = Mathf.Clamp(
            minBridgeLength + timeHeld * bridgeBuildSpeed,
            minBridgeLength,
            maxBridgeLength
        );

        // Nếu độ dài không thay đổi, không cần cập nhật
        if (Mathf.Approximately(newLength, currentBridgeLength))
            return;

        // Cập nhật độ dài hiện tại
        currentBridgeLength = newLength;

        // Cập nhật kích thước cầu
        Vector3 scale = activeBridge.transform.localScale;
        scale.y = currentBridgeLength;
        activeBridge.transform.localScale = scale;

        // Cập nhật vị trí để đảm bảo một đầu cầu vẫn ở vị trí đỉnh trụ
        Vector3 position = bridgeStartPosition + bridgeDirection * (currentBridgeLength / 2);
        activeBridge.transform.position = position;
    }

    #region FinishBridgeMethod
    /// <summary>
    /// Hoàn thành việc xây dựng cầu khi người chơi thả phím C.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="None">Không có tham số truyền vào.</param>
    /// <returns>Không trả về giá trị.</returns>
    /// <remarks>
    /// - Thêm cầu vào danh sách activeBridges để quản lý.
    /// - Tạo chướng ngại vật ngẫu nhiên trên cầu.
    /// - Tìm trụ gần nhất với đầu cầu và thông báo cho GameManager.
    /// - Đặt lại trạng thái isBuildingBridge và activeBridge.
    /// </remarks>
    #endregion
    void FinishBuildingBridge()
    {
        if (activeBridge == null) return;

        // Thêm vào danh sách cầu đã tạo
        activeBridges.Add(activeBridge);

        // Tạo chướng ngại vật trên cầu
        SpawnObstaclesOnBridge(activeBridge);

        // Thông báo cho GameManager
        if (gameManager != null)
        {
            // Tìm trụ gần nhất với đầu cầu
            Vector3 bridgeEndPoint = bridgeStartPosition + bridgeDirection * currentBridgeLength;
            Transform targetPillar = FindNearestPillarToPoint(bridgeEndPoint);

            if (targetPillar != null)
            {
                gameManager.OnBridgePlaced(targetPillar);
            }
            else
            {
                gameManager.OnBridgePlaced(null);
            }
        }

        // Reset các biến
        isBuildingBridge = false;
        activeBridge = null;

        Debug.Log("Hoàn thành xây dựng cầu với chiều dài: " + currentBridgeLength);
    }

    #region ObstacleSpawnMethod
    /// <summary>
    /// Tạo chướng ngại vật ngẫu nhiên trên cầu sau khi xây dựng hoàn tất.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="bridge">Đối tượng cầu cần tạo chướng ngại vật.</param>
    /// <returns>Không trả về giá trị.</returns>
    /// <remarks>
    /// - Xác định chiều dài và hướng của cầu để đặt chướng ngại vật.
    /// - Tính toán số lượng chướng ngại vật có thể đặt dựa trên chiều dài cầu và khoảng cách tối thiểu.
    /// - Đặt chướng ngại vật tại các vị trí ngẫu nhiên dọc theo cầu.
    /// - Thêm offset chiều cao để chướng ngại vật nằm trên mặt cầu.
    /// - Gán tag "Obstacle" cho chướng ngại vật và thêm vào danh sách quản lý.
    /// </remarks>
    #endregion
    void SpawnObstaclesOnBridge(GameObject bridge)
    {
        // Kiểm tra xem có obstacle prefabs không
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
            return;

        // Lấy chiều dài và hướng của cầu
        float bridgeLength = bridge.transform.localScale.y;
        Vector3 bridgeDirection = bridge.transform.up;

        // Điểm bắt đầu là đỉnh cầu (điểm gần trụ)
        Vector3 startPoint = bridge.transform.position - bridgeDirection * (bridgeLength / 2);

        // Khoảng cách từ đầu cầu để bắt đầu đặt chướng ngại vật (tránh đặt ngay đầu cầu)
        float startOffset = 1.0f;

        // Vị trí đầu tiên có thể đặt chướng ngại vật
        float currentPosition = startOffset;

        // Danh sách vị trí đã có chướng ngại vật
        List<float> obstaclePositions = new List<float>();

        // Số chướng ngại vật tối đa có thể đặt
        int maxPossibleObstacles = Mathf.FloorToInt((bridgeLength - startOffset * 2) / minObstacleSpacing);

        // Số chướng ngại vật thực tế sẽ tạo (ngẫu nhiên)
        int actualObstacles = Random.Range(0, maxPossibleObstacles + 1);

        // Đảm bảo có danh sách để lưu chướng ngại vật của cầu này
        if (!bridgeObstacles.ContainsKey(bridge))
            bridgeObstacles[bridge] = new List<GameObject>();

        // Tạo các chướng ngại vật
        for (int i = 0; i < actualObstacles; i++)
        {
            // Kiểm tra xem có đủ chỗ không
            if (currentPosition >= bridgeLength - startOffset)
                break;

            // Tính vị trí ngẫu nhiên dọc theo cầu
            float positionOnBridge = Random.Range(currentPosition, bridgeLength - startOffset);

            // Kiểm tra xem vị trí này có quá gần chướng ngại vật nào khác không
            bool tooClose = false;
            foreach (float pos in obstaclePositions)
            {
                if (Mathf.Abs(positionOnBridge - pos) < minObstacleSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            // Nếu quá gần, bỏ qua và thử vị trí tiếp theo
            if (tooClose)
            {
                currentPosition += minObstacleSpacing;
                continue;
            }

            // Kiểm tra tỉ lệ xuất hiện
            if (Random.value <= obstacleSpawnChance)
            {
                // Chọn một prefab chướng ngại vật ngẫu nhiên
                GameObject obstaclePrefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];

                // Tính vị trí thực tế trên cầu
                Vector3 obstaclePosition = startPoint + bridgeDirection * positionOnBridge;

                // Thêm offset để chướng ngại vật nằm trên cầu
                obstaclePosition += Vector3.up * obstacleHeightOffset;

                // Tạo chướng ngại vật
                GameObject obstacle = Instantiate(obstaclePrefab, obstaclePosition, bridge.transform.rotation);

                // Xoay chướng ngại vật để phù hợp với hướng cầu
                obstacle.transform.forward = bridge.transform.forward;

                // Đặt chướng ngại vật là con của cầu
                obstacle.transform.parent = bridge.transform;

                // Đặt tag
                obstacle.tag = "Obstacle";

                // Thêm vào danh sách chướng ngại vật của cầu này
                bridgeObstacles[bridge].Add(obstacle);

                // Lưu vị trí để tránh đặt chướng ngại vật quá gần nhau
                obstaclePositions.Add(positionOnBridge);

                Debug.Log("Đã tạo chướng ngại vật tại vị trí: " + obstaclePosition);
            }

            // Di chuyển đến vị trí tiếp theo
            currentPosition = positionOnBridge + minObstacleSpacing;
        }
    }

    #region IsBuildingBridgeMethod
    /// <summary>
    /// Kiểm tra xem đang trong quá trình xây dựng cầu hay không.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="None">Không có tham số truyền vào.</param>
    /// <returns>True nếu đang xây dựng cầu, ngược lại là false.</returns>
    /// <remarks>
    /// - Phương thức này cho phép các thành phần khác kiểm tra trạng thái xây dựng cầu.
    /// - Thường được sử dụng để điều khiển hành vi của người chơi trong quá trình xây cầu.
    /// </remarks>
    #endregion
    public bool IsBuildingBridge()
    {
        return isBuildingBridge;
    }

    #region FindNearestPillarMethod
    /// <summary>
    /// Tìm trụ gần nhất với một điểm cho trước.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="point">Điểm cần tìm trụ gần nhất.</param>
    /// <returns>Transform của trụ gần nhất, hoặc null nếu không tìm thấy.</returns>
    /// <remarks>
    /// - Lấy danh sách các trụ từ PillarController.
    /// - Tính khoảng cách từ điểm đến từng trụ.
    /// - Trả về trụ có khoảng cách gần nhất.
    /// - Được sử dụng để xác định trụ đích khi hoàn thành xây cầu.
    /// </remarks>
    #endregion
    private Transform FindNearestPillarToPoint(Vector3 point)
    {
        if (pillarController == null) return null;

        List<Transform> pillars = pillarController.GetAllPillars();
        if (pillars.Count == 0) return null;

        Transform nearest = null;
        float minDistance = float.MaxValue;

        foreach (Transform pillar in pillars)
        {
            float distance = Vector3.Distance(point, pillar.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = pillar;
            }
        }

        return nearest;
    }

    #region ClearAllBridgesMethod
    /// <summary>
    /// Xóa tất cả các cầu và chướng ngại vật đã tạo.
    /// Người tạo: Phạm Anh Dũng - Ngày tạo: 05/03/2025
    /// Ngày sửa: None
    /// </summary>
    /// <param name="None">Không có tham số truyền vào.</param>
    /// <returns>Không trả về giá trị.</returns>
    /// <remarks>
    /// - Xóa tất cả các chướng ngại vật được lưu trữ trong bridgeObstacles.
    /// - Xóa tất cả các cầu trong danh sách activeBridges.
    /// - Xóa cầu đang được xây dựng nếu có.
    /// - Đặt lại trạng thái isBuildingBridge thành false.
    /// - Thường được gọi khi reset trò chơi hoặc chuyển sang cấp độ mới.
    /// </remarks>
    #endregion
    public void ClearAllBridges()
    {
        // Xóa chướng ngại vật
        foreach (var obstacleList in bridgeObstacles.Values)
        {
            foreach (GameObject obstacle in obstacleList)
            {
                if (obstacle != null)
                {
                    Destroy(obstacle);
                }
            }
        }
        bridgeObstacles.Clear();

        // Xóa cầu
        foreach (GameObject bridge in activeBridges)
        {
            if (bridge != null)
            {
                Destroy(bridge);
            }
        }
        activeBridges.Clear();

        if (activeBridge != null)
        {
            Destroy(activeBridge);
            activeBridge = null;
        }

        isBuildingBridge = false;
    }
}