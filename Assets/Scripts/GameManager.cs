using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int startingLevel = 1;
    public int pointsToNextLevel = 100;
    public float gameStartDelay = 2f;
    public float levelCompleteDelay = 3f;

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI currentLevelText;
    public GameObject gameOverPanel;
    public GameObject levelCompletePanel;
    public GameObject startPanel;
    public Button startButton;
    public Button restartButton;
    public Button nextLevelButton;

    [Header("Audio")]
    public AudioClip startGameSound;
    public AudioClip gameOverSound;
    public AudioClip levelCompleteSound;
    public AudioClip pointsSound;

    // Game state
    private int currentScore = 0;
    private int currentLevel = 1;
    private bool isGameOver = false;
    private bool isGamePaused = false;
    private int pillarsReached = 0;
    private int bridgesBuilt = 0;

    // References to other controllers
    private PlayerController playerController;
    private BridgeController bridgeController;
    private PillarController pillarController;
    private AudioSource audioSource;

    public bool isStarted = false;
    // Singleton instance
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        // Setup singleton pattern
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Get references
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        startButton.onClick.RemoveAllListeners();
        restartButton.onClick.RemoveAllListeners();
        nextLevelButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(OnButtonStartGame);
        restartButton.onClick.AddListener(RestartGame);
        nextLevelButton.onClick.AddListener(OnButtonNextLevel);
    }

    private void Start()
    {
        // Find references to other controllers
        playerController = FindObjectOfType<PlayerController>();
        bridgeController = FindObjectOfType<BridgeController>();
        pillarController = FindObjectOfType<PillarController>();
        Time.timeScale = 0;
        isStarted = false;
        // Start a new game

    }

    public void OnButtonStartGame()
    {
        startPanel.SetActive(false);
        StartNewGame();
        Time.timeScale = 1;
    }

    private void StartNewGame()
    {
        // Reset game state
        currentScore = 0;
        currentLevel = startingLevel;
        isGameOver = false;
        isGamePaused = false;
        pillarsReached = 0;
        bridgesBuilt = 0;

        // Hide panels
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (levelCompletePanel) levelCompletePanel.SetActive(false);

        // Show starting message
        //ShowMessage("Level " + currentLevel + "\nGet Ready!", 2f);

        // Play start game sound
        PlaySound(startGameSound);

        // Wait for game start delay
        //yield return new WaitForSeconds(gameStartDelay);

        // Initialize the play area
        if (pillarController != null)
        {
            pillarController.ResetPlayArea();
        }

        // Clear any existing bridges
        if (bridgeController != null)
        {
            bridgeController.ClearAllBridges();
        }

        // Update UI
        UpdateUI();

        Debug.Log("New game started at level " + currentLevel);
    }

    public void OnPillarReached(Transform pillar)
    {
        if (isGameOver || isGamePaused) return;

        pillarsReached++;

        // Update UI and check for level completion
        UpdateUI();
        CheckLevelProgress();

        Debug.Log("Pillar reached: " + pillarsReached);
    }

    public void OnBridgePlaced(Transform targetPillar)
    {
        if (isGameOver || isGamePaused) return;

        bridgesBuilt++;

        // Award points for building bridge
        AddPoints(5);

        Debug.Log("Bridge placed: " + bridgesBuilt);
    }

    public void AddPoints(int points)
    {
        if (isGameOver || isGamePaused) return;

        currentScore += points;

        // Play point sound
        PlaySound(pointsSound);

        // Update UI and check for level completion
        UpdateUI();
        CheckLevelProgress();

        Debug.Log("Points added: " + points + ", Total: " + currentScore);
    }

    private void CheckLevelProgress()
    {
        // Check if we have enough points to complete the level
        if (currentScore >= pointsToNextLevel * currentLevel)
        {
            CompleteLevel();
        }
    }

    private void CompleteLevel()
    {
        // Play level complete sound
        PlaySound(levelCompleteSound);

        // Show level complete panel or message
        if (levelCompletePanel)
        {
            levelCompletePanel.SetActive(true);
            currentLevelText.text = "Current Level: "+ currentLevel.ToString();
            Time.timeScale = 0;
        }
        else
        {
            ShowMessage("Level " + currentLevel + " Complete!", 2f);
        }

        // Wait for delay


        // Hide level complete pane
    }

    public void OnButtonNextLevel()
    {
        levelCompletePanel.SetActive(false);

        // Increment level
        Time.timeScale = 1;
        currentLevel++;

        // Increase difficulty with level
        AdjustDifficultyForLevel();

        // Show new level message
        //ShowMessage("Level " + currentLevel + "\nGet Ready!", 2f);

        // Update UI
        UpdateUI();

        Debug.Log("Advanced to level " + currentLevel);
    }

    private void AdjustDifficultyForLevel()
    {
        // Example: Adjust difficulty based on level
        // This can be customized based on your game's mechanics

        // Example: Make pillars further apart in higher levels
        if (pillarController != null)
        {
            // Increase minimum and maximum pillar distance
            float distanceMultiplier = 1f + (currentLevel - 1) * 0.1f; // Increases by 10% per level

            // Adjust parameters (example)
            // pillarController.minPillarDistance *= distanceMultiplier;
            // pillarController.maxPillarDistance *= distanceMultiplier;
        }

        // Example: Add more obstacles in higher levels
        if (bridgeController != null)
        {
            // Increase obstacle spawn chance
            float obstacleIncrease = (currentLevel - 1) * 0.05f; // Increases by 5% per level

            // Adjust parameters (example)
            // bridgeController.obstacleSpawnChance = Mathf.Min(0.8f, bridgeController.obstacleSpawnChance + obstacleIncrease);
        }
    }

    public void PlayerDied()
    {
        if (isGameOver) return;

        GameOver();
    }

    public void GameOver()
    {
        if (isGameOver) return;

        isGameOver = true;

        // Play game over sound
        PlaySound(gameOverSound);

        // Show game over panel
        if (gameOverPanel)
        {
            gameOverPanel.SetActive(true);
            Time.timeScale = 0;
        }
        else
        {
            ShowMessage("Game Over!\nScore: " + currentScore, 3f);
        }

        Debug.Log("Game Over. Final score: " + currentScore);
    }

    public void RestartGame()
    {
        // Reload the current scene
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void PauseGame(bool pause)
    {
        isGamePaused = pause;
        Time.timeScale = pause ? 0f : 1f;

        // Additional pause logic here
    }

    private void UpdateUI()
    {
        // Update score text
        if (scoreText != null)
        {
            scoreText.text = "Score: " + currentScore;
        }

        // Update level text
        if (levelText != null)
        {
            levelText.text = "Level: " + currentLevel;
        }
    }

    private void ShowMessage(string message, float duration)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(true);

            // Hide message after duration
            StartCoroutine(HideMessageAfterDelay(duration));
        }
    }

    private IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // For external access to game state
    public int GetCurrentScore() { return currentScore; }
    public int GetCurrentLevel() { return currentLevel; }
    public bool IsGameOver() { return isGameOver; }
    public bool IsGamePaused() { return isGamePaused; }
}