using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Turnos locales de Jenga (3 jugadores). Un turno tiene dos fases:
/// extraer un bloque y colocarlo en la cima. Un movimiento inválido
/// no cambia de jugador.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public enum TurnPhase
    {
        Extract,
        Place
    }

    [Header("Configuración de jugadores")]
    public string[] playerNames = { "Jugador 1", "Jugador 2", "Jugador 3" };
    int currentPlayerIndex;
    bool gameOver;
    TurnPhase phase = TurnPhase.Extract;

    [Header("Eventos (conéctalos en el Inspector a tu UI)")]
    public UnityEvent<string> OnTurnChanged;
    public UnityEvent<string> OnGameOver;
    public UnityEvent<string> OnInvalidMove;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        phase = TurnPhase.Extract;
        AnnounceTurn();
    }

    public string CurrentPlayer => playerNames[Mathf.Clamp(currentPlayerIndex, 0, playerNames.Length - 1)];
    public bool IsGameOver => gameOver;
    public TurnPhase Phase => phase;

    public string StatusLine
    {
        get
        {
            if (gameOver) return "Fin de la partida";
            if (phase == TurnPhase.Place)
                return $"{CurrentPlayer} — Coloca el bloque en la cima";
            return $"{CurrentPlayer} — Extrae un bloque (no el de arriba)";
        }
    }

    public void EnterPlacePhase()
    {
        if (gameOver) return;
        phase = TurnPhase.Place;
        AnnounceTurn();
    }

    public void CompleteTurn()
    {
        if (gameOver) return;
        currentPlayerIndex = (currentPlayerIndex + 1) % playerNames.Length;
        phase = TurnPhase.Extract;
        AnnounceTurn();
    }

    public void NextTurn()
    {
        CompleteTurn();
    }

    public void NotifyInvalidMove(string reason)
    {
        if (gameOver) return;
        OnInvalidMove?.Invoke(reason);
        Debug.Log($"Movimiento inválido ({CurrentPlayer}): {reason}");
    }

    void AnnounceTurn()
    {
        OnTurnChanged?.Invoke(CurrentPlayer);
        Debug.Log(StatusLine);
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
