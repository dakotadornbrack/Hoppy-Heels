using UnityEngine;

/// <summary>
/// Shows the game-over overlay. Hook GameManager.onGameOver → Show() in the Inspector
/// (or let SceneBuilder do it automatically).
/// ScoreManager.StopTracking() populates the score texts before this is called.
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
