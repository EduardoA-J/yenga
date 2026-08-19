using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestiona el turno entre 3 jugadores locales y el estado de fin de partida.
/// Colócalo en un GameObject vacío llamado "GameManager" en la escena.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    [Header("Configuración de jugadores")]
    public string[] playerNames = { "Jugador 1", "Jugador 2", "Jugador 3" };
    private int currentPlayerIndex = 0;
    private bool gameOver = false;

    [Header("Eventos (conéctalos en el Inspector a tu UI)")]
    public UnityEvent<string> OnTurnChanged;
    public UnityEvent<string> OnGameOver;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        AnnounceTurn();
    }

    public string CurrentPlayer => playerNames[currentPlayerIndex];
    public bool IsGameOver => gameOver;

    public void NextTurn()
    {
        if (gameOver) return;
        currentPlayerIndex = (currentPlayerIndex + 1) % playerNames.Length;
        AnnounceTurn();
    }

    void AnnounceTurn()
    {
        OnTurnChanged?.Invoke(CurrentPlayer);
        Debug.Log($"Turno de: {CurrentPlayer}");
    }

    public void EndGame(string reason)
    {
        if (gameOver) return;
        gameOver = true;
        string message = $"¡{CurrentPlayer} derribó la torre! {reason}";
        OnGameOver?.Invoke(message);
        Debug.Log(message);
    }
}