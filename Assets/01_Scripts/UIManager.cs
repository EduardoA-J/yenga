using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// UI de turno, pistas de Jenga, aviso de tracking y movimientos inválidos.
/// </summary>
public class UIManager : MonoBehaviour
{
    public TMP_Text turnText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;

    TMP_Text hintText;
    TMP_Text trackingText;
    TMP_Text invalidText;
    Coroutine invalidRoutine;

    void Start()
    {
        EnsureOverlays();
        ApplyVisualStyles();

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnInvalidMove.AddListener(ShowInvalidMove);
            UpdateTurnText(TurnManager.Instance.CurrentPlayer);
        }

        if (ARTrackingGate.Instance != null)
        {
            ARTrackingGate.Instance.OnStabilityChanged += OnTrackingChanged;
            OnTrackingChanged(ARTrackingGate.Instance.IsStable);
        }
        else
        {
            OnTrackingChanged(false);
        }
    }

    void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnInvalidMove.RemoveListener(ShowInvalidMove);

        if (ARTrackingGate.Instance != null)
            ARTrackingGate.Instance.OnStabilityChanged -= OnTrackingChanged;
    }

    public void UpdateTurnText(string playerName)
    {
        if (turnText == null) return;

        if (TurnManager.Instance != null)
            turnText.text = TurnManager.Instance.StatusLine;
        else
            turnText.text = $"Turno: {playerName}";
    }

    public void ShowGameOver(string message)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverText != null) gameOverText.text = message;
        SetMessageVisible(hintText, false);
        SetMessageVisible(invalidText, false);
    }

    public void ShowInvalidMove(string reason)
    {
        if (invalidText == null) return;
        if (invalidRoutine != null) StopCoroutine(invalidRoutine);
        invalidRoutine = StartCoroutine(ShowInvalidRoutine(reason));
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    void OnTrackingChanged(bool stable)
    {
        if (trackingText != null)
        {
            SetMessageVisible(trackingText, !stable);
            trackingText.text = "Apunta a la imagen impresa para jugar.\nLa torre solo se puede tocar con seguimiento estable.";
        }

        if (hintText != null)
        {
            bool playing = stable && (TurnManager.Instance == null || !TurnManager.Instance.IsGameOver);
            SetMessageVisible(hintText, playing);
            if (playing)
            {
                hintText.text = TurnManager.Instance != null && TurnManager.Instance.Phase == TurnManager.TurnPhase.Place
                    ? "Arrastra el bloque a una ranura verde de la cima y suéltalo."
                    : "Toca un bloque (no el de arriba), sácalo y luego colócalo en la cima.";
            }
        }

        UpdateTurnText(TurnManager.Instance != null ? TurnManager.Instance.CurrentPlayer : "");
    }

    IEnumerator ShowInvalidRoutine(string reason)
    {
        invalidText.text = reason;
        SetMessageVisible(invalidText, true);
        yield return new WaitForSeconds(2.4f);
        SetMessageVisible(invalidText, false);
        invalidRoutine = null;
    }

    void Update()
    {
        if (hintText == null || TurnManager.Instance == null) return;
        if (ARTrackingGate.Instance != null && !ARTrackingGate.Instance.IsStable) return;
        if (TurnManager.Instance.IsGameOver) return;

        SetMessageVisible(hintText, true);
        hintText.text = TurnManager.Instance.Phase == TurnManager.TurnPhase.Place
            ? "Arrastra el bloque a una ranura verde de la cima y suéltalo."
            : "Toca un bloque (no el de arriba), sácalo y luego colócalo en la cima.";
    }

    void EnsureOverlays()
    {
        Canvas canvas = turnText != null
            ? turnText.GetComponentInParent<Canvas>()
            : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        UIStyleHelper.ConfigureCanvas(scaler);

        trackingText = UIStyleHelper.GetOrCreateMessage(
            canvas.transform, "TrackingWarning", UIStyleHelper.MessageKind.Tracking, new Vector2(0f, 260f));
        hintText = UIStyleHelper.GetOrCreateMessage(
            canvas.transform, "HintText", UIStyleHelper.MessageKind.Hint, new Vector2(0f, -300f));
        invalidText = UIStyleHelper.GetOrCreateMessage(
            canvas.transform, "InvalidMoveText", UIStyleHelper.MessageKind.Invalid, new Vector2(0f, 100f));

        SetMessageVisible(trackingText, false);
        SetMessageVisible(hintText, false);
        SetMessageVisible(invalidText, false);
    }

    void ApplyVisualStyles()
    {
        UIStyleHelper.StyleTurnBanner(turnText);

        Button restartButton = gameOverPanel != null
            ? gameOverPanel.GetComponentInChildren<Button>(true)
            : null;
        UIStyleHelper.StyleGameOverPanel(gameOverPanel, gameOverText, restartButton);
    }

    static void SetMessageVisible(TMP_Text text, bool visible)
    {
        if (text == null) return;

        Transform card = text.transform.parent;
        if (card != null && card.GetComponent<Image>() != null)
            card.gameObject.SetActive(visible);
        else
            text.gameObject.SetActive(visible);
    }
}
