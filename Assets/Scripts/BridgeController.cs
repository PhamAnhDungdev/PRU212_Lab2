using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BridgeController : MonoBehaviour
{
    [Header("Bridge Settings")]
    public GameObject bridgePrefab;
    public float minBridgeLength = 2f;
    public float maxBridgeLength = 20f;
    public float bridgeWidth = 1.5f;
    public float bridgeHeight = 0.1f;
    public float bridgeBuildSpeed = 5f;
    public float offsetDistanceBridge = .3f;

    [Header("Obstacle Settings")]
    public GameObject[] obstaclePrefabs;
    public float obstacleSpawnChance = 0.7f;
    public float minObstacleSpacing = 2f;
    public float obstacleHeightOffset = 0.5f;

    [Header("Visual Effects")]
    public Material validBridgeMaterial;
    public Material invalidBridgeMaterial;

    // Private variables
    private bool isBuildingBridge = false;
    private bool isRotatingBridge = false;
    private float currentBridgeLength = 0f;
    private float buildStartTime;
    private GameObject activeBridge;
    private Vector3 bridgeStartPosition;
    private Vector3 bridgeDirection;
    private List<GameObject> activeBridges = new List<GameObject>();
    private Dictionary<GameObject, List<GameObject>> bridgeObstacles = new Dictionary<GameObject, List<GameObject>>();

    // Biến để lưu trữ kết quả va chạm
    private bool bridgeConnectedToPillar = false;
    private Transform connectedPillar = null;

    // References
    private PlayerController player;
    private PillarController pillarController;
    private GameManager gameManager;

    void Start()
    {
        // Tìm Player và PillarController trong scene
        player = FindObjectOfType<PlayerController>();
        pillarController = FindObjectOfType<PillarController>();
        gameManager = FindObjectOfType<GameManager>();

        if (bridgePrefab == null)
        {
            Debug.LogError("Chưa thiết lập Bridge Prefab cho BridgeController!");
        }
    }

    void Update()
    {
        // Kiểm tra nếu game kết thúc hoặc tạm dừng
        if (gameManager != null && (gameManager.IsGameOver() || gameManager.IsGamePaused()))
        {
            return;
        }

        // Chỉ cho phép tạo cầu mới khi không đang xây hoặc đổ cầu
        if (Input.GetKeyDown(KeyCode.C) && !isBuildingBridge && !isRotatingBridge)
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

    IEnumerator RotateBridgeOverTime(Transform bridge, Vector3 pivot, Vector3 axis, float angle, float duration)
    {
        isRotatingBridge = true;
        float elapsedTime = 0f;
        float currentAngle = 0f;

        // Đảm bảo các trụ vẫn đứng yên khi bắt đầu xoay cầu
        if (pillarController != null)
        {
            pillarController.PauseAllPillarMovement();
        }

        // Tạo một GameObject để phát hiện va chạm
        GameObject detector = new GameObject("BridgeEndPoint");
        detector.transform.parent = bridge;
        detector.transform.localPosition = new Vector3(0, bridge.localScale.y / 2, 0); // Đặt ở đầu cầu
        SphereCollider sphereCollider = detector.AddComponent<SphereCollider>();
        sphereCollider.radius = 0.5f;
        sphereCollider.isTrigger = true;

        // Thêm rigidbody cho detector
        Rigidbody detectorRb = detector.AddComponent<Rigidbody>();
        detectorRb.isKinematic = true;
        detectorRb.useGravity = false;

        // Bắt đầu kiểm tra va chạm
        StartCoroutine(CheckCollisionWithPillar(detector.transform));

        while (elapsedTime < duration)
        {
            float step = (angle / duration) * Time.deltaTime;
            bridge.transform.RotateAround(pivot, axis, step);

            currentAngle += step;
            elapsedTime += Time.deltaTime;

            // Nếu đã kết nối với trụ, dừng xoay và giữ trụ đó đứng yên
            if (bridgeConnectedToPillar && connectedPillar != null)
            {
                if (pillarController != null)
                {
                    pillarController.StopPillarMovement(connectedPillar);
                    Debug.Log("Cầu đã kết nối với trụ: " + connectedPillar.name);
                }

                // Thêm BoxCollider làm đường đi cho người chơi
                GameObject walkway = new GameObject("BridgeWalkway");
                walkway.transform.parent = bridge;
                walkway.transform.localPosition = Vector3.zero;
                walkway.transform.localRotation = Quaternion.identity;

                BoxCollider walkwayCollider = walkway.AddComponent<BoxCollider>();
                walkwayCollider.size = new Vector3(bridgeWidth * 0.8f, 0.1f, bridge.localScale.y);
                walkwayCollider.center = new Vector3(0, bridgeHeight / 2, 0);
                walkway.tag = "Ground";

                break;
            }

            if (currentAngle >= angle) break;
            yield return null;
        }

        // Xử lý kết quả cuối cùng của cầu
        if (bridgeConnectedToPillar && connectedPillar != null)
        {
            // Cầu đã kết nối thành công với trụ

            // Thêm Rigidbody để cầu không bị ảnh hưởng bởi vật lý
            Rigidbody rb = bridge.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // Chỉ giữ trụ đã kết nối, cho phép các trụ khác di chuyển
            if (pillarController != null)
            {
                pillarController.ResumeAllPillarMovementExcept(connectedPillar);
                Debug.Log("Trụ " + connectedPillar.name + " đứng yên, các trụ khác tiếp tục di chuyển");
            }
        }
        else
        {
            // Cầu không kết nối với trụ nào

            // Đảm bảo cầu đúng góc cuối cùng
            bridge.transform.RotateAround(pivot, axis, angle - currentAngle);

            // Cho tất cả các trụ tiếp tục di chuyển
            if (pillarController != null)
            {
                pillarController.ResumeAllPillarMovement();
                Debug.Log("Không va chạm với trụ nào, tất cả trụ tiếp tục di chuyển");
            }

            // Thêm Rigidbody để cầu rơi xuống
            Rigidbody rb = bridge.gameObject.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.useGravity = true;
            rb.angularDamping = 0.5f;

            // Đặt thời gian hủy cầu sau 3 giây
            Destroy(bridge.gameObject, 3f);
        }

        // Xóa detector khi đã xong
        Destroy(detector);

        // Đánh dấu xoay cầu đã hoàn thành
        isRotatingBridge = false;
        Debug.Log("Hoàn thành xoay cầu");
    }

    // Phương thức kiểm tra va chạm
    private IEnumerator CheckCollisionWithPillar(Transform detectorTransform)
    {
        bool detected = false;

        while (detectorTransform != null && !detected)
        {
            // Kiểm tra các collider trong phạm vi
            Collider[] hitColliders = Physics.OverlapSphere(detectorTransform.position, 0.5f);

            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Pillar"))
                {
                    bridgeConnectedToPillar = true;
                    connectedPillar = hitCollider.transform;
                    detected = true;
                    Debug.Log("Đã phát hiện va chạm với trụ: " + hitCollider.name);
                    break;
                }
            }

            yield return new WaitForFixedUpdate();
        }
    }

    void StartBuildingBridge()
    {
        // Lấy pillar mà player đang đứng
        Transform currentPillar = player.GetCurrentPillar();

        if (currentPillar == null)
        {
            Debug.LogWarning("Không thể tạo cầu - Player không đứng trên pillar nào!");
            return;
        }

        // Tạm dừng chuyển động của tất cả các trụ ngay lập tức
        if (pillarController != null)
        {
            pillarController.PauseAllPillarMovement();
            Debug.Log("Tạm dừng tất cả trụ trong quá trình xây cầu");
        }

        // Lấy vị trí đỉnh của pillar
        Vector3 pillarTop = GetPillarTopPosition(currentPillar);

        // Lấy vị trí và hướng của player
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = player.transform.forward;
        playerForward.y = 0;
        playerForward.Normalize();

        // Tính vị trí cầu dựa trên vị trí player
        Vector3 bridgeStartPos = new Vector3(playerPos.x, pillarTop.y, playerPos.z) + playerForward * offsetDistanceBridge;

        // Hướng cầu dựng đứng
        Vector3 bridgeDir = Vector3.up;

        // Gán các giá trị khởi tạo
        isBuildingBridge = true;
        bridgeStartPosition = bridgeStartPos;
        bridgeDirection = bridgeDir;
        currentBridgeLength = minBridgeLength;
        buildStartTime = Time.time;

        // Tạo cầu mới
        CreateInitialBridge();
    }

    Vector3 GetPillarTopPosition(Transform pillar)
    {
        if (pillar == null) return Vector3.zero;

        CapsuleCollider capsule = pillar.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            float height = capsule.height * pillar.lossyScale.y;
            Vector3 topPosition = pillar.position + Vector3.up * ((height / 2) + (capsule.center.y * pillar.lossyScale.y));
            return topPosition;
        }

        return pillar.position + Vector3.up * (pillar.localScale.y / 2);
    }

    void CreateInitialBridge()
    {
        // Tạo bridge mới
        activeBridge = Instantiate(bridgePrefab);

        // Thiết lập kích thước ban đầu
        Vector3 scale = activeBridge.transform.localScale;
        scale.y = currentBridgeLength;
        scale.x = bridgeWidth;
        scale.z = bridgeHeight;
        activeBridge.transform.localScale = scale;

        // Lấy hướng nhìn của player
        Vector3 playerForward = player.transform.forward;
        playerForward.y = 0;
        playerForward.Normalize();

        // CẦU DỰNG ĐỨNG KHI TẠO
        Quaternion targetRotation = Quaternion.LookRotation(bridgeDirection);
        targetRotation *= Quaternion.Euler(0, 0, 0);
        activeBridge.transform.rotation = targetRotation;

        // Điều chỉnh để cầu nhìn theo hướng player
        activeBridge.transform.forward = playerForward;

        // Thiết lập vị trí sao cho một đầu cầu ở đúng vị trí đỉnh trụ
        Vector3 position = bridgeStartPosition + bridgeDirection * (currentBridgeLength / 2);
        activeBridge.transform.position = position;

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

    void UpdateBridgeWhileBuilding()
    {
        if (activeBridge == null) return;

        // Tính thời gian đã giữ phím
        float timeHeld = Time.time - buildStartTime;

        // Tính toán độ dài mới dựa trên thời gian và tốc độ
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

    void FinishBuildingBridge()
    {
        if (activeBridge == null) return;

        // Thêm vào danh sách cầu đã tạo
        activeBridges.Add(activeBridge);

        // Hướng nhìn ngang của player
        Vector3 playerForward = player.transform.forward;
        playerForward.y = 0;
        playerForward.Normalize();

        // Tính trục xoay: cầu xoay quanh cạnh dưới
        Vector3 pivotPoint = bridgeStartPosition;
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, playerForward);

        // Reset biến trước khi xoay
        bridgeConnectedToPillar = false;
        connectedPillar = null;

        // Bắt đầu Coroutine để xoay cầu
        StartCoroutine(RotateBridgeOverTime(activeBridge.transform, pivotPoint, rotationAxis, 90f, 1.0f));

        // Reset biến
        isBuildingBridge = false;
        activeBridge = null;
    }

    public bool IsBuildingBridge()
    {
        return isBuildingBridge;
    }

    public bool IsRotatingBridge()
    {
        return isRotatingBridge;
    }

    public bool IsBridgeConnected()
    {
        return bridgeConnectedToPillar && connectedPillar != null;
    }

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
        isRotatingBridge = false;
    }
}