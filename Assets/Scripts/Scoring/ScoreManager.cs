using UnityEngine;
using TMPro;

/// <summary>
/// Converts the player's climb height into a score and persists the high score.
/// Wire up the TMP_Text references in the Inspector — all are optional.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("HUD (optional)")]
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text highScoreText;
    [SerializeField] TMP_Text hudGemsText;

    [Header("Game Over (optional)")]
    [SerializeField] TMP_Text gameOverScoreText;
    [SerializeField] TMP_Text gameOverHighScoreText;

    [Header("Main Menu (optional)")]
    [SerializeField] TMP_Text menuHighScoreText;

    [Header("Game Over Gems (optional)")]
    [SerializeField] TMP_Text gameOverGemsText;

    [Header("Total Gems (optional)")]
    [SerializeField] TMP_Text menuTotalGemsText;
    [SerializeField] TMP_Text gameOverTotalGemsText;

    [Header("Tuning")]
    [SerializeField] float pointsPerUnit = 10f;

    const string HighScoreKey = "HoppyHeels_HighScore";
    const string TotalGemsKey = "HoppyHeels_TotalGems";

    Transform player;
    float startY;
    bool tracking;

    public int CurrentScore { get; private set; }
    public int HighScore { get; private set; }
    public int GemsCollected { get; private set; }
    public int TotalGems { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        TotalGems = PlayerPrefs.GetInt(TotalGemsKey, 0);
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        startY = player != null ? player.position.y : 0f;
        tracking = false;

        if (highScoreText) highScoreText.text = $"Best: {HighScore}";
        if (menuHighScoreText) menuHighScoreText.text = $"Best: {HighScore}";
        if (menuTotalGemsText) menuTotalGemsText.text = $"Total Gems: {TotalGems}";
    }

    /// <summary>Called when gameplay begins (from main menu).</summary>
    public void StartTracking()
    {
        startY = player != null ? player.position.y : 0f;
        CurrentScore = 0;
        GemsCollected = 0;
        tracking = true;
        if (scoreText) scoreText.text = "0";
        if (hudGemsText) hudGemsText.text = "Gems: 0";
    }

    void Update()
    {
        if (!tracking || player == null) return;

        float height = Mathf.Max(0f, player.position.y - startY);
        int score = Mathf.FloorToInt(height * pointsPerUnit);
        if (score > CurrentScore)
            CurrentScore = score;

        if (scoreText) scoreText.text = CurrentScore.ToString();
    }

    /// <summary>Called by GameManager when the run ends.</summary>
    public void StopTracking()
    {
        tracking = false;
        TrySaveHighScore();
        SaveTotalGems();

        if (gameOverScoreText)      gameOverScoreText.text      = $"Score: {CurrentScore}";
        if (gameOverHighScoreText)  gameOverHighScoreText.text  = $"Best: {HighScore}";
        if (gameOverGemsText)       gameOverGemsText.text       = $"Gems: {GemsCollected}";
        if (gameOverTotalGemsText)  gameOverTotalGemsText.text  = $"Total Gems: {TotalGems}";
    }

    /// <summary>Called by Collectible when the player picks up a gem.</summary>
    public void CollectGem(int value)
    {
        GemsCollected++;
        CurrentScore += value;
        if (scoreText) scoreText.text = CurrentScore.ToString();
        if (hudGemsText) hudGemsText.text = $"Gems: {GemsCollected}";
    }

    void TrySaveHighScore()
    {
        if (CurrentScore <= HighScore) return;
        HighScore = CurrentScore;
        PlayerPrefs.SetInt(HighScoreKey, HighScore);
        PlayerPrefs.Save();
    }

    void SaveTotalGems()
    {
        TotalGems += GemsCollected;
        PlayerPrefs.SetInt(TotalGemsKey, TotalGems);
        PlayerPrefs.Save();
    }
}
