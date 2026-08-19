using UnityEngine;
using TMPro;

/// <summary>
/// Conecta este script a un Canvas con un Text (TMP) de turno y un panel de Game Over.
/// Se puede enlazar directamente a los UnityEvents de TurnManager desde el Inspector.
/// </summary>
public class UIManager : MonoBehaviour
{
    public TMP_Text turnText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;

    public void UpdateTurnText(string playerName)
    {
        if (turnText != null)
            turnText.text = $"Turno: {playerName}";
    }

    public void ShowGameOver(string message)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverText != null) gameOverText.text = message;
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}