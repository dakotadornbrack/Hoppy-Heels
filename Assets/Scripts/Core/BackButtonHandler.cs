using UnityEngine;

/// <summary>
/// Android back button (Escape on desktop). Lives on the GameManager GameObject.
/// Closes Settings first; otherwise pauses/resumes a run, leaves the game-over
/// screen for the menu, and quits from the main menu.
/// </summary>
public class BackButtonHandler : MonoBehaviour
{
    [SerializeField] SettingsPanel settingsPanel;

    void Awake()
    {
        if (settingsPanel == null)
            settingsPanel = FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleBack();
    }

    public void HandleBack()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
        {
            settingsPanel.Hide();
            return;
        }

        switch (gm.State)
        {
            case GameManager.GameState.Playing:  gm.PauseGame();  break;
            case GameManager.GameState.Paused:   gm.ResumeGame(); break;
            case GameManager.GameState.GameOver: gm.QuitToMenu(); break;
            case GameManager.GameState.MainMenu: Application.Quit(); break;
        }
    }
}
