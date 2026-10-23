using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { MainMenu, Playing, Paused, GameOver }
    public GameState State { get; private set; } = GameState.MainMenu;

    public UnityEvent onGameStarted;
    public UnityEvent onGameOver;
    public UnityEvent onGamePaused;
    public UnityEvent onGameResumed;

    // Survives the scene reload so "Try Again" skips the main menu.
    static bool startOnLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => startOnLoad = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Time.timeScale = 0f;
    }

    void Start()
    {
        if (!startOnLoad) return;
        startOnLoad = false;
        StartGame();
    }

    public void StartGame()
    {
        if (State != GameState.MainMenu) return;
        State = GameState.Playing;
        Time.timeScale = 1f;
        onGameStarted?.Invoke();
    }

    public void PauseGame()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Time.timeScale = 0f;
        onGamePaused?.Invoke();
    }

    public void ResumeGame()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
        Time.timeScale = 1f;
        onGameResumed?.Invoke();
    }

    // ── App lifecycle: never keep a run going while the player can't see it ──

    void OnApplicationPause(bool paused)
    {
        if (paused) PauseGame();   // home button, app switcher, incoming call
    }

#if !UNITY_EDITOR
    // Notification shade, permission dialogs. Skipped in the editor so clicking
    // another window while testing doesn't pause the game.
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) PauseGame();
    }
#endif

    public void TriggerGameOver()
    {
        if (State == GameState.GameOver) return;
        State = GameState.GameOver;
        ScoreManager.Instance?.StopTracking();
        onGameOver?.Invoke();
    }

    /// <summary>Reloads the scene and goes straight into a new run.</summary>
    public void RestartGame()
    {
        startOnLoad = true;
        ReloadScene();
    }

    /// <summary>Reloads the scene and stops on the main menu.</summary>
    public void QuitToMenu()
    {
        startOnLoad = false;
        ReloadScene();
    }

    static void ReloadScene()
    {
        Time.timeScale = 1f;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
    }
}
