using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using Animancer;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseMovementSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public float rotationSpeed = 100f;
    public float jumpForce = 15f;
    public float crouchHeight = 0.5f;
    public float normalHeight = 1f;

    [Header("Debug Settings")]
    public bool showDebugInfo = false; // Turn off for better performance

    [Header("Slope Physics")]
    public float maxSlopeAngle = 45f;
    public float slopeAcceleration = 2f;
    public float slopeDeceleration = 4f;
    public float slipThreshold = 0.3f;

    [Header("References")]
    public Transform cameraTransform;
    public Transform groundCheck;
    public LayerMask groundMask;
    //public Animator animator;

    // Private variables
    private Rigidbody rb;
    //private CapsuleCollider playerCollider;
    private BridgeController bridgeController;
    private GameManager gameManager;
    private float currentSpeed;
    private bool isGrounded;
    private bool isCrouching;
    private bool canJump = true;
    private bool isJumping = false;
    private bool isOnBridge = false;
    private Vector3 slopeNormal;
    private float groundCheckRadius = 0.3f;
    private Transform currentPillar;
    private int consecutiveSuccessfulMoves = 0;
    private bool isSprinting = false;
    private bool isCutting = false;

    // Input variables
    private float horizontalInput;
    private float verticalInput;
    private float rotationInput;
    private bool jumpInput;
    private bool crouchInput;
    public float maxBridgeLength = 20f;

    // Animation references
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip walkClip;
    [SerializeField] private AnimationClip jumpClip;
    [SerializeField] private AnimationClip cutClip;

    // Public access to state
    public bool isGroundedPublic { get { return isGrounded; } }

    void Start()
    {
        // Ensure Rigidbody exists
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Make sure it's not Kinematic
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Smoother movement
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Better collision

        // Ensure CapsuleCollider exists
        //playerCollider = GetComponent<CapsuleCollider>();
        //if (playerCollider == null)
        //{
            //playerCollider = gameObject.AddComponent<CapsuleCollider>();
            //playerCollider.height = normalHeight;
            //playerCollider.center = Vector3.zero;
        //}

        // Ensure Animator has reference
        // if (animator == null)
        //     animator = GetComponent<Animator>();

        // Ensure AnimancerComponent has reference
        if (animancer == null)
            animancer = GetComponent<AnimancerComponent>();

        if (animancer != null && idleClip != null)
        {
            animancer.Play(idleClip);
        }

        // Find controllers without causing errors if not found
        bridgeController = FindObjectOfType<BridgeController>();
        gameManager = FindObjectOfType<GameManager>();

        // Set movement speed
        currentSpeed = baseMovementSpeed;

        // Check groundCheck
        if (groundCheck == null)
        {
            GameObject check = new GameObject("GroundCheck");
            check.transform.parent = transform;
            check.transform.localPosition = new Vector3(0, -0.95f, 0);
            groundCheck = check.transform;
        }

        // Ensure groundMask is set
        if (groundMask.value == 0)
        {
            groundMask = LayerMask.GetMask("Default");
        }

        // Camera setup
        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
            }
            else
            {
                cameraTransform = Camera.main.transform;
            }
        }
    }

    void Update()
    {
        // Check if game is over or paused
        if (gameManager != null && (gameManager.IsGameOver() || gameManager.IsGamePaused()))
        {
            return;
        }

        // Get input
        GetInputs();

        // Check for right mouse button (cut animation)
        if (Input.GetMouseButtonDown(1) && !isCutting && cutClip != null)
        {
            StartCoroutine(PlayCutAnimation());
        }

        // Only process normal movement if not in cutting animation
        if (!isCutting)
        {
            // Handle arrow key rotation
            HandleHorizontalRotation();

            // Handle crouching
            HandleCrouching();

            // Check if on ground
            CheckGrounded();

            // Check if on bridge
            CheckIfOnBridge();

            // Clear jump state if landed after jumping
            if (isJumping && isGrounded && rb.linearVelocity.y <= 0.1f)
            {
                isJumping = false;
            }

            // Handle jump
            if (jumpInput && isGrounded && canJump && !isJumping)
            {
                Jump();
            }

            // Update animation parameters if animator exists
            // if (animator != null)
            // {
            //     UpdateAnimations();
            // }

            // Handle animations based on movement
            HandleAnimations();
        }
    }

    void FixedUpdate()
    {
        // Check if game is over or paused
        if (gameManager != null && (gameManager.IsGameOver() || gameManager.IsGamePaused()))
        {
            return;
        }

        // Only process movement if not in cutting animation
        if (!isCutting)
        {
            HandleMovement();
        }
    }

    void OnDrawGizmos()
    {
        // Draw ground check sphere
        if (groundCheck != null && showDebugInfo)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    IEnumerator PlayCutAnimation()
    {
        isCutting = true;

        if (animancer != null && cutClip != null)
        {
            // Stop current animations and play cut animation
            animancer.Stop();
            var cutState = animancer.Play(cutClip);

            // Wait for animation to complete
            yield return new WaitForSeconds(cutClip.length);

            // Return to idle animation
            if (idleClip != null)
                animancer.Play(idleClip);
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }

        isCutting = false;
    }

    void GetInputs()
    {
        // Use Unity's Input system for simpler input handling
        verticalInput = Input.GetAxis("Vertical");

        // Use Q/E for strafing
        horizontalInput = 0;
        if (Input.GetKey(KeyCode.Q))
            horizontalInput = -1;
        else if (Input.GetKey(KeyCode.E))
            horizontalInput = 1;

        // Use A/D or arrow keys for rotation
        rotationInput = Input.GetAxis("Horizontal");

        // Other inputs
        jumpInput = Input.GetKeyDown(KeyCode.Space);
        crouchInput = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        isSprinting = Input.GetKey(KeyCode.LeftShift);
    }

    void HandleHorizontalRotation()
    {
        // Apply rotation
        if (Mathf.Abs(rotationInput) > 0.01f)
        {
            transform.Rotate(Vector3.up, rotationInput * rotationSpeed * Time.deltaTime);
        }
    }

    void HandleCrouching()
    {
        if (crouchInput && !isCrouching && isGrounded)
        {
            isCrouching = true;
            //playerCollider.height = crouchHeight;
            //playerCollider.center = new Vector3(0, -0.25f, 0);
            currentSpeed = baseMovementSpeed * 0.5f;
        }
        else if (!crouchInput && isCrouching)
        {
            isCrouching = false;
            //playerCollider.height = normalHeight;
            //playerCollider.center = Vector3.zero;
            UpdateMovementSpeed();
        }
    }

    void UpdateMovementSpeed()
    {
        if (isSprinting)
        {
            currentSpeed = baseMovementSpeed * sprintMultiplier;
        }
        else
        {
            currentSpeed = baseMovementSpeed;
        }

        if (isCrouching)
        {
            currentSpeed = baseMovementSpeed * 0.5f;
        }
    }

    void CheckGrounded()
    {
        // Save previous ground state
        bool wasGrounded = isGrounded;

        // Check if player is on ground using sphere cast
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask);

        // If we're on the ground, we can jump again (but only when we're not in the middle of a jump)
        if (isGrounded && !isJumping)
            canJump = true;
    }

    void CheckIfOnBridge()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 1.5f))
        {
            if (hit.collider.CompareTag("Bridge"))
            {
                isOnBridge = true;
                slopeNormal = hit.normal;
            }
            else
            {
                isOnBridge = false;

                if (hit.collider.CompareTag("Pillar"))
                {
                    // Check if we've moved to a new pillar
                    if (currentPillar != hit.transform)
                    {
                        Transform previousPillar = currentPillar;
                        currentPillar = hit.transform;

                        // Notify GameManager about the reached pillar
                        if (gameManager != null)
                        {
                            gameManager.OnPillarReached(currentPillar);
                        }

                        OnPillarReached();
                    }
                }
            }
        }
        else
        {
            isOnBridge = false;
        }
    }

    void HandleMovement()
    {
        UpdateMovementSpeed();

        float slopeMultiplier = 1f;

        if (isOnBridge)
        {
            float slopeAngle = Vector3.Angle(Vector3.up, slopeNormal);
            Vector3 slopeDirection = Vector3.Cross(Vector3.Cross(slopeNormal, Vector3.down), slopeNormal);
            bool isGoingUphill = Vector3.Dot(slopeDirection, transform.forward * verticalInput) < 0;

            if (isGoingUphill)
            {
                slopeMultiplier = Mathf.Max(0.5f, 1f - (slopeAngle / maxSlopeAngle) * 0.5f);
            }
            else
            {
                slopeMultiplier = Mathf.Min(1.5f, 1f + (slopeAngle / maxSlopeAngle) * 0.5f);
            }

            if (slopeAngle > maxSlopeAngle * 0.7f && !isGoingUphill)
            {
                rb.AddForce(slopeDirection.normalized * slopeAcceleration, ForceMode.Acceleration);
            }
        }

        // Calculate movement direction based on player's current orientation
        Vector3 moveDirection = transform.forward * verticalInput + transform.right * horizontalInput;

        if (moveDirection.magnitude > 0.1f)
        {
            moveDirection.Normalize();
            Vector3 movement = moveDirection * currentSpeed * slopeMultiplier;

            // Ensure we're not using MovePosition if Rigidbody has FreezePosition set
            if ((rb.constraints & RigidbodyConstraints.FreezePositionX) != 0 ||
                (rb.constraints & RigidbodyConstraints.FreezePositionZ) != 0)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            // Use either MovePosition or velocity based on preference
            // MovePosition method:
            rb.MovePosition(rb.position + movement * Time.fixedDeltaTime);

            // Alternative velocity method:
            // rb.velocity = new Vector3(movement.x, rb.velocity.y, movement.z);
        }
        else if (isGrounded)
        {
            // Stop horizontal movement when no input and grounded
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

    void HandleAnimations()
    {
        // Skip if we're cutting or have jumped
        if (isCutting) return;

        // If we're jumping and not on ground, don't change animation
        if (isJumping && !isGrounded) return;

        // Check if animancer and clips exist
        if (animancer == null || (idleClip == null && walkClip == null))
        {
            return;
        }

        // If we've landed from a jump, play idle or walk based on movement
        if (isGrounded)
        {
            if (Mathf.Abs(verticalInput) > 0.1f || Mathf.Abs(horizontalInput) > 0.1f)
            {
                // Play walk animation
                if (walkClip != null)
                {
                    var walkState = animancer.Play(walkClip);

                    // Adjust animation speed based on whether player is sprinting
                    if (isSprinting)
                    {
                        // Increase animation speed when sprinting
                        walkState.Speed = 1.5f;
                    }
                    else
                    {
                        // Normal animation speed when walking
                        walkState.Speed = 1.0f;
                    }
                }
            }
            else
            {
                // Play idle animation when standing still
                if (idleClip != null)
                {
                    animancer.Play(idleClip);
                }
            }
        }
    }

    void Jump()
    {
        // Reset vertical velocity to ensure consistent jump height
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        // Set jump flags
        canJump = false;
        isJumping = true;

        // Play jump animation
        PlayJumpAnimation();
    }

    void PlayJumpAnimation()
    {
        if (jumpClip == null || animancer == null)
        {
            return;
        }

        try
        {
            // Stop current animation
            animancer.Stop();

            // Play jump animation
            animancer.Play(jumpClip);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error playing jump animation: {e.Message}");
        }
    }

    void OnPillarReached()
    {
        consecutiveSuccessfulMoves++;
    }

    // void UpdateAnimations()
    // {
    //     if (animator == null) return;
    //
    //     animator.SetBool("IsGrounded", isGrounded);
    //     animator.SetBool("IsCrouching", isCrouching);
    //     animator.SetBool("IsJumping", isJumping);
    //     animator.SetFloat("Speed", Mathf.Sqrt(horizontalInput * horizontalInput + verticalInput * verticalInput) * currentSpeed);
    //     animator.SetBool("IsSprinting", isSprinting);
    //     animator.SetBool("IsCutting", isCutting);
    //
    //     if (jumpInput && isGrounded && !isJumping)
    //         animator.SetTrigger("Jump");
    // }

    IEnumerator FallFromBridge()
    {
        this.enabled = false;
        rb.AddForce(Vector3.down * 2f + Random.insideUnitSphere * 2f, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
        yield return new WaitForSeconds(2f);

        if (gameManager != null)
        {
            gameManager.PlayerDied();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            currentSpeed = baseMovementSpeed * 0.5f;
            StartCoroutine(RestoreSpeed());
        }

        if (collision.gameObject.CompareTag("Ground") && gameManager.isStarted)
        {
            gameManager.PlayerDied();
        }
    }

    IEnumerator RestoreSpeed()
    {
        yield return new WaitForSeconds(2f);
        UpdateMovementSpeed();
    }

    void OnTriggerEnter(Collider other)
    {
        // Handle reward collection - check for RewardItem component
        if (other.CompareTag("Reward") || other.CompareTag("Collectible"))
        {
            //RewardItem reward = other.GetComponent<RewardItem>();
            //if (reward != null)
            //{
            //    // Add points through game manager
            //    if (gameManager != null)
            //    {
            //        gameManager.AddPoints(reward.pointValue);
            //    }

            //    // Destroy the reward
            //    Destroy(other.gameObject);
            //}
        }
    }

    // Used by GameManager to reset player position
    public void ResetPosition(Vector3 position)
    {
        transform.position = position;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // For external access to check if player is on bridge
    public bool IsOnBridge()
    {
        return isOnBridge;
    }

    // For external access to get current pillar
    public Transform GetCurrentPillar()
    {
        return currentPillar;
    }
}